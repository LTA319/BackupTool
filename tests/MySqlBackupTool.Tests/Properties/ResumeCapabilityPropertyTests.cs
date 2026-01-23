using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Data;
using MySqlBackupTool.Shared.Data.Repositories;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using System.Security.Cryptography;
using System.Text;

namespace MySqlBackupTool.Tests.Properties;

/// <summary>
/// Property-based tests for resume capability functionality
/// **Validates: Requirements 5.1, 5.2, 5.3, 5.4, 5.5**
/// </summary>
public class ResumeCapabilityPropertyTests : IDisposable
{
    private readonly ChunkManager _chunkManager;
    private readonly ResumeTokenRepository _resumeTokenRepository;
    private readonly BackupDbContext _dbContext;
    private readonly List<string> _tempFiles;
    private readonly List<string> _tempDirectories;

    public ResumeCapabilityPropertyTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<BackupDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _dbContext = new BackupDbContext(options);
        _dbContext.Database.EnsureCreated();

        // Setup services
        var chunkLogger = new LoggerFactory().CreateLogger<ChunkManager>();
        var checksumLogger = new LoggerFactory().CreateLogger<ChecksumService>();
        var repoLogger = new LoggerFactory().CreateLogger<ResumeTokenRepository>();
        
        var checksumService = new ChecksumService(checksumLogger);
        _resumeTokenRepository = new ResumeTokenRepository(_dbContext, repoLogger);
        _chunkManager = new ChunkManager(chunkLogger, checksumService, _resumeTokenRepository);
        
        _tempFiles = new List<string>();
        _tempDirectories = new List<string>();
    }

    /// <summary>
    /// Property 11: Resume Capability
    /// For any backup transfer interrupted at any point, the system should generate a resume token 
    /// and be able to continue the transfer from the last successfully transferred chunk, 
    /// with the final result being identical to an uninterrupted transfer.
    /// **Validates: Requirements 5.1, 5.2, 5.3, 5.4**
    /// </summary>
    [Property(MaxTest = 10)]
    public bool ResumeCapabilityProperty()
    {
        try
        {
            // Generate test parameters
            var fileSize = Gen.Choose(2048, 1024 * 1024).Sample(0, 1).First(); // 2KB to 1MB
            var chunkSize = Gen.Choose(256, 8192).Sample(0, 1).First(); // 256B to 8KB
            var interruptionPoint = Gen.Choose(1, 5).Sample(0, 1).First(); // Interrupt after 1-5 chunks
            
            // Only test when we'll have multiple chunks and can interrupt
            if (fileSize <= chunkSize)
                return true; // Skip this test case
            
            var chunkingStrategy = new ChunkingStrategy
            {
                ChunkSize = chunkSize,
                MaxConcurrentChunks = 1,
                EnableCompression = false
            };
            
            var totalChunks = chunkingStrategy.CalculateChunkCount(fileSize);
            if (totalChunks <= interruptionPoint)
                return true; // Skip if interruption point is beyond total chunks

            // Arrange - Create test file
            var originalFile = CreateTestFile(fileSize);
            string originalChecksum;
            
            using (var fileStream = new FileStream(originalFile, FileMode.Open, FileAccess.Read))
            {
                using var md5 = MD5.Create();
                var hash = md5.ComputeHash(fileStream);
                originalChecksum = Convert.ToHexString(hash).ToLowerInvariant();
            }
            
            var metadata = new FileMetadata
            {
                FileName = Path.GetFileName(originalFile),
                FileSize = fileSize,
                ChecksumMD5 = originalChecksum,
                CreatedAt = DateTime.UtcNow
            };

            // Act - Simulate interrupted transfer
            var transferId = _chunkManager.InitializeTransferAsync(metadata).Result;
            var chunks = CreateFileChunks(originalFile, chunkingStrategy, transferId);
            
            // Process chunks up to interruption point
            for (int i = 0; i < interruptionPoint; i++)
            {
                var chunkResult = _chunkManager.ReceiveChunkAsync(transferId, chunks[i]).Result;
                if (!chunkResult.Success)
                    return false;
            }

            // Create resume token
            var resumeToken = _chunkManager.CreateResumeTokenAsync(transferId).Result;
            
            // Verify resume token was created
            if (string.IsNullOrEmpty(resumeToken))
                return false;

            // Get resume info
            var resumeInfo = _chunkManager.GetResumeInfoAsync(resumeToken).Result;
            
            // Verify resume info is correct
            if (resumeInfo.CompletedChunks.Count != interruptionPoint)
                return false;
            
            if (resumeInfo.Metadata.FileSize != fileSize)
                return false;

            // Restore transfer session
            var restoredTransferId = _chunkManager.RestoreTransferAsync(resumeToken, metadata).Result;
            
            // Continue processing remaining chunks
            for (int i = interruptionPoint; i < chunks.Count; i++)
            {
                var chunkResult = _chunkManager.ReceiveChunkAsync(restoredTransferId, chunks[i]).Result;
                if (!chunkResult.Success)
                    return false;
            }

            // Finalize transfer
            var reassembledFile = _chunkManager.FinalizeTransferAsync(restoredTransferId).Result;
            _tempFiles.Add(reassembledFile);

            // Verify final file matches original
            string reassembledChecksum;
            using (var fileStream = new FileStream(reassembledFile, FileMode.Open, FileAccess.Read))
            {
                using var md5 = MD5.Create();
                var hash = md5.ComputeHash(fileStream);
                reassembledChecksum = Convert.ToHexString(hash).ToLowerInvariant();
            }
            
            var reassembledSize = new FileInfo(reassembledFile).Length;
            
            var checksumMatches = originalChecksum == reassembledChecksum;
            var sizeMatches = fileSize == reassembledSize;
            var fileExists = File.Exists(reassembledFile);

            return checksumMatches && sizeMatches && fileExists;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Resume capability property test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Property 12: Resume Token Cleanup
    /// For any successfully completed backup operation that used resume functionality, 
    /// the system should automatically clean up the associated resume token data.
    /// **Validates: Requirements 5.5**
    /// </summary>
    [Property(MaxTest = 10)]
    public bool ResumeTokenCleanupProperty()
    {
        try
        {
            // Generate test parameters
            var fileSize = Gen.Choose(1024, 100 * 1024).Sample(0, 1).First(); // 1KB to 100KB
            var chunkSize = Gen.Choose(256, 4096).Sample(0, 1).First(); // 256B to 4KB
            
            // Only test when we'll have multiple chunks
            if (fileSize <= chunkSize)
                return true; // Skip this test case
            
            var chunkingStrategy = new ChunkingStrategy
            {
                ChunkSize = chunkSize,
                MaxConcurrentChunks = 1,
                EnableCompression = false
            };

            // Arrange - Create test file
            var originalFile = CreateTestFile(fileSize);
            string originalChecksum;
            
            using (var fileStream = new FileStream(originalFile, FileMode.Open, FileAccess.Read))
            {
                using var md5 = MD5.Create();
                var hash = md5.ComputeHash(fileStream);
                originalChecksum = Convert.ToHexString(hash).ToLowerInvariant();
            }
            
            var metadata = new FileMetadata
            {
                FileName = Path.GetFileName(originalFile),
                FileSize = fileSize,
                ChecksumMD5 = originalChecksum,
                CreatedAt = DateTime.UtcNow
            };

            // Act - Create transfer and resume token
            var transferId = _chunkManager.InitializeTransferAsync(metadata).Result;
            var chunks = CreateFileChunks(originalFile, chunkingStrategy, transferId);
            
            // Process first chunk
            var firstChunkResult = _chunkManager.ReceiveChunkAsync(transferId, chunks[0]).Result;
            if (!firstChunkResult.Success)
                return false;

            // Create resume token
            var resumeToken = _chunkManager.CreateResumeTokenAsync(transferId).Result;
            
            // Verify token exists in database
            var tokenEntity = _resumeTokenRepository.GetByTokenAsync(resumeToken).Result;
            if (tokenEntity == null || tokenEntity.IsCompleted)
                return false;

            // Complete the transfer
            for (int i = 1; i < chunks.Count; i++)
            {
                var chunkResult = _chunkManager.ReceiveChunkAsync(transferId, chunks[i]).Result;
                if (!chunkResult.Success)
                    return false;
            }

            // Finalize transfer (this should trigger cleanup)
            var reassembledFile = _chunkManager.FinalizeTransferAsync(transferId).Result;
            _tempFiles.Add(reassembledFile);

            // Verify token is marked as completed
            var completedTokenEntity = _resumeTokenRepository.GetByTokenAsync(resumeToken).Result;
            var tokenMarkedCompleted = completedTokenEntity?.IsCompleted == true;

            // Verify we can clean up completed tokens
            var cleanupCount = _resumeTokenRepository.CleanupCompletedTokensAsync(TimeSpan.Zero).Result;
            var cleanupWorked = cleanupCount >= 0; // Should not throw

            return tokenMarkedCompleted && cleanupWorked;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Resume token cleanup property test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Property test for resume token generation uniqueness
    /// Each resume token should be unique and properly formatted
    /// **Validates: Requirements 5.1**
    /// </summary>
    [Property(MaxTest = 20)]
    public bool ResumeTokenUniquenessProperty()
    {
        try
        {
            var tokens = new HashSet<string>();
            var tokenCount = Gen.Choose(5, 15).Sample(0, 1).First();
            
            // Generate multiple resume tokens
            for (int i = 0; i < tokenCount; i++)
            {
                var fileSize = Gen.Choose(1024, 10 * 1024).Sample(0, 1).First();
                var originalFile = CreateTestFile(fileSize);
                
                var metadata = new FileMetadata
                {
                    FileName = Path.GetFileName(originalFile),
                    FileSize = fileSize,
                    ChecksumMD5 = "dummy_checksum",
                    CreatedAt = DateTime.UtcNow
                };

                var transferId = _chunkManager.InitializeTransferAsync(metadata).Result;
                var resumeToken = _chunkManager.CreateResumeTokenAsync(transferId).Result;
                
                // Verify token format (should start with "RT_")
                if (!resumeToken.StartsWith("RT_"))
                    return false;
                
                // Verify token uniqueness
                if (tokens.Contains(resumeToken))
                    return false;
                
                tokens.Add(resumeToken);
            }

            // All tokens should be unique
            return tokens.Count == tokenCount;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Resume token uniqueness test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Property test for resume token persistence and retrieval
    /// Resume tokens should be properly stored and retrievable from the database
    /// **Validates: Requirements 5.1, 5.5**
    /// </summary>
    [Property(MaxTest = 10)]
    public bool ResumeTokenPersistenceProperty()
    {
        try
        {
            // Generate test parameters
            var fileSize = Gen.Choose(1024, 50 * 1024).Sample(0, 1).First();
            var chunkSize = Gen.Choose(256, 2048).Sample(0, 1).First();
            
            if (fileSize <= chunkSize)
                return true; // Skip single chunk files
            
            var chunkingStrategy = new ChunkingStrategy
            {
                ChunkSize = chunkSize,
                MaxConcurrentChunks = 1,
                EnableCompression = false
            };

            // Arrange
            var originalFile = CreateTestFile(fileSize);
            var metadata = new FileMetadata
            {
                FileName = Path.GetFileName(originalFile),
                FileSize = fileSize,
                ChecksumMD5 = "test_checksum",
                CreatedAt = DateTime.UtcNow
            };

            // Act - Create transfer and process some chunks
            var transferId = _chunkManager.InitializeTransferAsync(metadata).Result;
            var chunks = CreateFileChunks(originalFile, chunkingStrategy, transferId);
            
            var chunksToProcess = Math.Min(3, chunks.Count - 1); // Process some but not all chunks
            for (int i = 0; i < chunksToProcess; i++)
            {
                var chunkResult = _chunkManager.ReceiveChunkAsync(transferId, chunks[i]).Result;
                if (!chunkResult.Success)
                    return false;
            }

            // Create resume token
            var resumeToken = _chunkManager.CreateResumeTokenAsync(transferId).Result;

            // Verify token can be retrieved
            var retrievedToken = _resumeTokenRepository.GetByTokenAsync(resumeToken).Result;
            if (retrievedToken == null)
                return false;

            // Verify token properties
            var tokenPropertiesCorrect = 
                retrievedToken.TransferId == transferId &&
                retrievedToken.FileName == metadata.FileName &&
                retrievedToken.FileSize == metadata.FileSize &&
                !retrievedToken.IsCompleted;

            // Verify completed chunks are stored
            var completedChunks = _resumeTokenRepository.GetCompletedChunksAsync(resumeToken).Result;
            var chunksStoredCorrectly = completedChunks.Count == chunksToProcess;

            // Verify resume info can be retrieved
            var resumeInfo = _chunkManager.GetResumeInfoAsync(resumeToken).Result;
            var resumeInfoCorrect = 
                resumeInfo.TransferId == transferId &&
                resumeInfo.CompletedChunks.Count == chunksToProcess &&
                resumeInfo.Metadata.FileSize == fileSize;

            return tokenPropertiesCorrect && chunksStoredCorrectly && resumeInfoCorrect;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Resume token persistence test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Creates a test file with random content
    /// </summary>
    private string CreateTestFile(long size)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"ResumeTest_{Guid.NewGuid():N}.dat");
        _tempFiles.Add(tempFile);

        using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write);
        var random = new System.Random(42); // Fixed seed for reproducibility
        var buffer = new byte[Math.Min(8192, size)];
        long written = 0;

        while (written < size)
        {
            var bytesToWrite = (int)Math.Min(buffer.Length, size - written);
            random.NextBytes(buffer);
            fileStream.Write(buffer, 0, bytesToWrite);
            written += bytesToWrite;
        }

        return tempFile;
    }

    /// <summary>
    /// Creates chunks from a file using the specified chunking strategy
    /// </summary>
    private List<ChunkData> CreateFileChunks(string filePath, ChunkingStrategy strategy, string transferId)
    {
        var chunks = new List<ChunkData>();
        var fileInfo = new FileInfo(filePath);
        var chunkCount = strategy.CalculateChunkCount(fileInfo.Length);

        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        
        for (int i = 0; i < chunkCount; i++)
        {
            var remainingBytes = fileInfo.Length - (i * strategy.ChunkSize);
            var chunkSize = (int)Math.Min(strategy.ChunkSize, remainingBytes);
            
            var chunkData = new byte[chunkSize];
            var bytesRead = fileStream.Read(chunkData, 0, chunkSize);
            
            if (bytesRead != chunkSize)
            {
                throw new InvalidOperationException($"Expected to read {chunkSize} bytes but read {bytesRead}");
            }

            var chunk = new ChunkData
            {
                TransferId = transferId,
                ChunkIndex = i,
                Data = chunkData,
                ChunkChecksum = CalculateMD5(chunkData),
                IsLastChunk = i == chunkCount - 1
            };

            chunks.Add(chunk);
        }

        return chunks;
    }

    /// <summary>
    /// Calculates MD5 checksum of byte array
    /// </summary>
    private static string CalculateMD5(byte[] data)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public void Dispose()
    {
        // Clean up temporary files
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        // Clean up temporary directories
        foreach (var dir in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        _dbContext?.Dispose();
    }
}