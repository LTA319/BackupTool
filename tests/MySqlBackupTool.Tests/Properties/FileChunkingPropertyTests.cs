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
/// Property-based tests for file chunking functionality
/// **Validates: Requirements 4.1, 4.2**
/// </summary>
public class FileChunkingPropertyTests : IDisposable
{
    private readonly ChunkManager _chunkManager;
    private readonly List<string> _tempFiles;
    private readonly List<string> _tempDirectories;

    public FileChunkingPropertyTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<BackupDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        var dbContext = new BackupDbContext(options);
        dbContext.Database.EnsureCreated();

        var logger = new LoggerFactory().CreateLogger<ChunkManager>();
        var checksumLogger = new LoggerFactory().CreateLogger<ChecksumService>();
        var repoLogger = new LoggerFactory().CreateLogger<ResumeTokenRepository>();
        
        var checksumService = new ChecksumService(checksumLogger);
        var resumeTokenRepository = new ResumeTokenRepository(dbContext, repoLogger);
        _chunkManager = new ChunkManager(logger, checksumService, resumeTokenRepository);
        _tempFiles = new List<string>();
        _tempDirectories = new List<string>();
    }

    /// <summary>
    /// Property 8: File Chunking for Large Files
    /// For any backup file exceeding the configured size threshold, the system should split it into chunks 
    /// and reassemble them correctly at the destination, with the reassembled file being identical to the original.
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Property(MaxTest = 15)]
    public bool FileChunkingForLargeFilesProperty()
    {
        try
        {
            // Generate test parameters
            var fileSize = Gen.Choose(1024, 10 * 1024 * 1024).Sample(0, 1).First(); // 1KB to 10MB
            var chunkSize = Gen.Choose(512, 1024 * 1024).Sample(0, 1).First(); // 512B to 1MB
            
            // Only test chunking when file is larger than chunk size
            if (fileSize <= chunkSize)
                return true; // Skip this test case
            
            // Arrange - Create test file
            var originalFile = CreateTestFile(fileSize);
            string originalChecksum;
            
            // Calculate checksum and ensure file is closed
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

            var chunkingStrategy = new ChunkingStrategy
            {
                ChunkSize = chunkSize,
                MaxConcurrentChunks = 2,
                EnableCompression = false
            };

            // Act - Initialize transfer and process chunks
            var transferId = _chunkManager.InitializeTransferAsync(metadata).Result;
            var chunks = CreateFileChunks(originalFile, chunkingStrategy, transferId);
            
            // Process each chunk
            foreach (var chunk in chunks)
            {
                var chunkResult = _chunkManager.ReceiveChunkAsync(transferId, chunk).Result;
                if (!chunkResult.Success)
                {
                    Console.WriteLine($"Chunk processing failed: {chunkResult.ErrorMessage}");
                    return false;
                }
            }

            // Finalize transfer
            var reassembledFile = _chunkManager.FinalizeTransferAsync(transferId).Result;
            _tempFiles.Add(reassembledFile);

            // Assert - Verify reassembled file matches original
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
            Console.WriteLine($"File chunking property test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Property test for chunk ordering and integrity
    /// Chunks should be processed in order and maintain data integrity
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Property(MaxTest = 10)]
    public bool ChunkOrderingAndIntegrityProperty()
    {
        try
        {
            // Generate test parameters
            var fileSize = Gen.Choose(2048, 1024 * 1024).Sample(0, 1).First(); // 2KB to 1MB
            var chunkSize = Gen.Choose(256, 8192).Sample(0, 1).First(); // 256B to 8KB
            
            // Only test when we'll have multiple chunks
            if (fileSize <= chunkSize)
                return true; // Skip this test case
            
            // Arrange - Create test file with known pattern
            var originalFile = CreatePatternedTestFile(fileSize);
            string originalChecksum;
            
            // Calculate checksum and ensure file is closed
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

            var chunkingStrategy = new ChunkingStrategy
            {
                ChunkSize = chunkSize,
                MaxConcurrentChunks = 1,
                EnableCompression = false
            };

            // Act - Process chunks in order
            var transferId = _chunkManager.InitializeTransferAsync(metadata).Result;
            var chunks = CreateFileChunks(originalFile, chunkingStrategy, transferId);
            
            // Verify chunk checksums before processing
            var allChunkChecksumsValid = true;
            foreach (var chunk in chunks)
            {
                var calculatedChecksum = CalculateMD5(chunk.Data);
                if (calculatedChecksum != chunk.ChunkChecksum)
                {
                    allChunkChecksumsValid = false;
                    break;
                }
            }

            // Process chunks
            foreach (var chunk in chunks)
            {
                var chunkResult = _chunkManager.ReceiveChunkAsync(transferId, chunk).Result;
                if (!chunkResult.Success)
                    return false;
            }

            // Finalize and verify
            var reassembledFile = _chunkManager.FinalizeTransferAsync(transferId).Result;
            _tempFiles.Add(reassembledFile);
            
            string reassembledChecksum;
            using (var fileStream = new FileStream(reassembledFile, FileMode.Open, FileAccess.Read))
            {
                using var md5 = MD5.Create();
                var hash = md5.ComputeHash(fileStream);
                reassembledChecksum = Convert.ToHexString(hash).ToLowerInvariant();
            }
            
            var integrityMaintained = originalChecksum == reassembledChecksum;

            return allChunkChecksumsValid && integrityMaintained;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Chunk ordering and integrity test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Property test for chunk size calculation accuracy
    /// The chunking strategy should correctly calculate the number of chunks needed
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 20)]
    public bool ChunkSizeCalculationProperty()
    {
        try
        {
            // Generate test parameters
            var fileSize = Gen.Choose(1, 100 * 1024 * 1024).Sample(0, 1).First(); // 1B to 100MB
            var chunkSize = Gen.Choose(1024, 10 * 1024 * 1024).Sample(0, 1).First(); // 1KB to 10MB
            
            var chunkingStrategy = new ChunkingStrategy
            {
                ChunkSize = chunkSize
            };

            // Act - Calculate expected chunk count
            var calculatedChunkCount = chunkingStrategy.CalculateChunkCount(fileSize);
            var expectedChunkCount = (int)Math.Ceiling((double)fileSize / chunkSize);

            // Assert - Verify calculation accuracy
            var calculationCorrect = calculatedChunkCount == expectedChunkCount;
            var chunkCountReasonable = calculatedChunkCount >= 0;
            
            // For files smaller than chunk size, should have 1 chunk (or 0 for empty files)
            var edgeCaseHandled = fileSize == 0 ? calculatedChunkCount == 0 : 
                                 fileSize <= chunkSize ? calculatedChunkCount == 1 : 
                                 calculatedChunkCount > 1;

            return calculationCorrect && chunkCountReasonable && edgeCaseHandled;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Chunk size calculation test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Creates a test file with random content
    /// </summary>
    private string CreateTestFile(long size)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"ChunkTest_{Guid.NewGuid():N}.dat");
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
    /// Creates a test file with a known pattern for integrity verification
    /// </summary>
    private string CreatePatternedTestFile(long size)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"PatternTest_{Guid.NewGuid():N}.dat");
        _tempFiles.Add(tempFile);

        using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write);
        
        // Create a repeating pattern
        var pattern = Encoding.UTF8.GetBytes("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");
        long written = 0;

        while (written < size)
        {
            var bytesToWrite = (int)Math.Min(pattern.Length, size - written);
            fileStream.Write(pattern, 0, bytesToWrite);
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
    }
}