using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Security.Cryptography;
using System.Text;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Manages file chunks during large file transfers
/// </summary>
public class ChunkManager : IChunkManager
{
    private readonly ILogger<ChunkManager> _logger;
    private readonly IChecksumService _checksumService;
    private readonly IResumeTokenRepository _resumeTokenRepository;
    private readonly Dictionary<string, TransferSession> _activeSessions = new();
    private readonly object _lockObject = new();

    public ChunkManager(ILogger<ChunkManager> logger, IChecksumService checksumService, IResumeTokenRepository resumeTokenRepository)
    {
        _logger = logger;
        _checksumService = checksumService ?? throw new ArgumentNullException(nameof(checksumService));
        _resumeTokenRepository = resumeTokenRepository ?? throw new ArgumentNullException(nameof(resumeTokenRepository));
    }

    /// <summary>
    /// Initializes a new file transfer session
    /// </summary>
    public async Task<string> InitializeTransferAsync(FileMetadata metadata)
    {
        var transferId = Guid.NewGuid().ToString();
        
        try
        {
            var session = new TransferSession
            {
                TransferId = transferId,
                Metadata = metadata,
                CreatedAt = DateTime.UtcNow,
                TempDirectory = Path.Combine(Path.GetTempPath(), "MySqlBackup", transferId),
                CompletedChunks = new HashSet<int>(),
                LastActivity = DateTime.UtcNow
            };

            // Create temporary directory for chunks
            Directory.CreateDirectory(session.TempDirectory);

            lock (_lockObject)
            {
                _activeSessions[transferId] = session;
            }

            _logger.LogInformation("Initialized transfer session {TransferId} for file {FileName}", 
                transferId, metadata.FileName);

            return transferId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing transfer session for file {FileName}", metadata.FileName);
            throw;
        }
    }

    /// <summary>
    /// Receives and processes a file chunk
    /// </summary>
    public async Task<ChunkResult> ReceiveChunkAsync(string transferId, ChunkData chunk)
    {
        try
        {
            TransferSession? session;
            lock (_lockObject)
            {
                if (!_activeSessions.TryGetValue(transferId, out session))
                {
                    return new ChunkResult
                    {
                        Success = false,
                        ErrorMessage = $"Transfer session {transferId} not found",
                        ChunkIndex = chunk.ChunkIndex
                    };
                }
                session.LastActivity = DateTime.UtcNow;
            }

            // Validate chunk checksum
            if (!string.IsNullOrEmpty(chunk.ChunkChecksum))
            {
                if (!_checksumService.ValidateChunkIntegrity(chunk.Data, chunk.ChunkChecksum))
                {
                    return new ChunkResult
                    {
                        Success = false,
                        ErrorMessage = $"Chunk {chunk.ChunkIndex} checksum validation failed",
                        ChunkIndex = chunk.ChunkIndex
                    };
                }
            }

            // Save chunk to temporary file
            var chunkPath = Path.Combine(session.TempDirectory, $"chunk_{chunk.ChunkIndex:D6}.dat");
            await File.WriteAllBytesAsync(chunkPath, chunk.Data);

            lock (_lockObject)
            {
                session.CompletedChunks.Add(chunk.ChunkIndex);
            }

            // Update resume token if it exists
            try
            {
                var existingToken = await _resumeTokenRepository.GetByTransferIdAsync(transferId);
                if (existingToken != null)
                {
                    await _resumeTokenRepository.AddCompletedChunkAsync(existingToken.Token, chunk.ChunkIndex, chunk.Data.Length, chunk.ChunkChecksum);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update resume token for chunk {ChunkIndex} in transfer {TransferId}", 
                    chunk.ChunkIndex, transferId);
                // Don't fail the chunk processing for resume token issues
            }

            _logger.LogDebug("Received chunk {ChunkIndex} for transfer {TransferId}", 
                chunk.ChunkIndex, transferId);

            return new ChunkResult
            {
                Success = true,
                ChunkIndex = chunk.ChunkIndex,
                IsComplete = chunk.IsLastChunk && session.CompletedChunks.Count == chunk.ChunkIndex + 1
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving chunk {ChunkIndex} for transfer {TransferId}", 
                chunk.ChunkIndex, transferId);
            
            return new ChunkResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ChunkIndex = chunk.ChunkIndex
            };
        }
    }

    /// <summary>
    /// Finalizes a file transfer by reassembling all chunks
    /// </summary>
    public async Task<string> FinalizeTransferAsync(string transferId)
    {
        try
        {
            TransferSession? session;
            lock (_lockObject)
            {
                if (!_activeSessions.TryGetValue(transferId, out session))
                {
                    throw new InvalidOperationException($"Transfer session {transferId} not found");
                }
            }

            _logger.LogInformation("Finalizing transfer {TransferId} for file {FileName}", 
                transferId, session.Metadata.FileName);

            // Create final file path
            var finalPath = Path.Combine(
                Path.GetDirectoryName(session.TempDirectory) ?? Path.GetTempPath(),
                session.Metadata.FileName);

            // Reassemble chunks
            using (var outputStream = new FileStream(finalPath, FileMode.Create, FileAccess.Write))
            {
                var chunkFiles = Directory.GetFiles(session.TempDirectory, "chunk_*.dat")
                    .Select(f => new { 
                        FilePath = f, 
                        ChunkIndex = ExtractChunkIndex(Path.GetFileName(f)) 
                    })
                    .OrderBy(x => x.ChunkIndex)
                    .Select(x => x.FilePath)
                    .ToList();

                foreach (var chunkFile in chunkFiles)
                {
                    var chunkData = await File.ReadAllBytesAsync(chunkFile);
                    await outputStream.WriteAsync(chunkData);
                }

                await outputStream.FlushAsync();
            } // Ensure FileStream is disposed before validation

            // Force garbage collection to ensure file handle is released
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Validate final file
            var fileInfo = new FileInfo(finalPath);
            if (fileInfo.Length != session.Metadata.FileSize)
            {
                throw new InvalidOperationException(
                    $"Final file size {fileInfo.Length} does not match expected size {session.Metadata.FileSize}");
            }

            // Validate checksum if provided
            if (!string.IsNullOrEmpty(session.Metadata.ChecksumMD5) || !string.IsNullOrEmpty(session.Metadata.ChecksumSHA256))
            {
                var isValid = await _checksumService.ValidateFileIntegrityAsync(
                    finalPath, 
                    session.Metadata.ChecksumMD5, 
                    session.Metadata.ChecksumSHA256);
                
                if (!isValid)
                {
                    // Log detailed checksum information for debugging
                    var actualChecksum = await _checksumService.CalculateFileMD5Async(finalPath);
                    _logger.LogError("Checksum validation failed. Expected: {Expected}, Actual: {Actual}, File: {FilePath}", 
                        session.Metadata.ChecksumMD5, actualChecksum, finalPath);
                    throw new InvalidOperationException($"Final file checksum validation failed. Expected: {session.Metadata.ChecksumMD5}, Actual: {actualChecksum}");
                }
            }

            // Clean up temporary files
            await CleanupSessionAsync(transferId);

            // Mark resume token as completed if it exists
            try
            {
                var existingToken = await _resumeTokenRepository.GetByTransferIdAsync(transferId);
                if (existingToken != null)
                {
                    await _resumeTokenRepository.MarkCompletedAsync(existingToken.Token);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to mark resume token as completed for transfer {TransferId}", transferId);
                // Don't fail the finalization for resume token issues
            }

            _logger.LogInformation("Successfully finalized transfer {TransferId} to {FilePath}", 
                transferId, finalPath);

            return finalPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finalizing transfer {TransferId}", transferId);
            
            // Clean up on error
            try
            {
                await CleanupSessionAsync(transferId);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Error cleaning up failed transfer {TransferId}", transferId);
            }
            
            throw;
        }
    }

    /// <summary>
    /// Gets information needed to resume an interrupted transfer
    /// </summary>
    public async Task<ResumeInfo> GetResumeInfoAsync(string resumeToken)
    {
        try
        {
            var tokenEntity = await _resumeTokenRepository.GetByTokenAsync(resumeToken);
            if (tokenEntity == null)
            {
                throw new InvalidOperationException($"Resume token {resumeToken} not found");
            }

            var resumeInfo = new ResumeInfo
            {
                TransferId = tokenEntity.TransferId,
                Metadata = new FileMetadata
                {
                    FileName = tokenEntity.FileName,
                    FileSize = tokenEntity.FileSize,
                    ChecksumMD5 = tokenEntity.ChecksumMD5 ?? string.Empty,
                    ChecksumSHA256 = tokenEntity.ChecksumSHA256 ?? string.Empty,
                    CreatedAt = tokenEntity.CreatedAt
                },
                CompletedChunks = tokenEntity.CompletedChunks.Select(c => c.ChunkIndex).ToList(),
                CreatedAt = tokenEntity.CreatedAt
            };

            if (resumeInfo.CompletedChunks.Count > 0)
            {
                resumeInfo.LastCompletedChunk = resumeInfo.CompletedChunks.Max();
            }

            _logger.LogInformation("Retrieved resume info for token {ResumeToken}. Completed chunks: {Count}", 
                resumeToken, resumeInfo.CompletedChunks.Count);

            return resumeInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting resume info for token {ResumeToken}", resumeToken);
            throw;
        }
    }

    /// <summary>
    /// Creates a resume token for an interrupted transfer
    /// </summary>
    public async Task<string> CreateResumeTokenAsync(string transferId)
    {
        try
        {
            TransferSession? session;
            lock (_lockObject)
            {
                if (!_activeSessions.TryGetValue(transferId, out session))
                {
                    throw new InvalidOperationException($"Transfer session {transferId} not found");
                }
            }

            var resumeToken = GenerateResumeToken();
            
            var tokenEntity = new ResumeToken
            {
                Token = resumeToken,
                TransferId = transferId,
                FileName = session.Metadata.FileName,
                FileSize = session.Metadata.FileSize,
                ChecksumMD5 = session.Metadata.ChecksumMD5,
                ChecksumSHA256 = session.Metadata.ChecksumSHA256,
                TempDirectory = session.TempDirectory,
                CreatedAt = session.CreatedAt,
                LastActivity = DateTime.UtcNow,
                IsCompleted = false
            };

            await _resumeTokenRepository.AddAsync(tokenEntity);

            // Add completed chunks
            foreach (var chunkIndex in session.CompletedChunks)
            {
                await _resumeTokenRepository.AddCompletedChunkAsync(resumeToken, chunkIndex, 0);
            }

            _logger.LogInformation("Created resume token {ResumeToken} for transfer {TransferId}", 
                resumeToken, transferId);

            return resumeToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating resume token for transfer {TransferId}", transferId);
            throw;
        }
    }

    /// <summary>
    /// Cleans up resume token data after successful completion
    /// </summary>
    public async Task CleanupResumeTokenAsync(string resumeToken)
    {
        try
        {
            await _resumeTokenRepository.MarkCompletedAsync(resumeToken);
            
            _logger.LogInformation("Marked resume token {ResumeToken} for cleanup", resumeToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up resume token {ResumeToken}", resumeToken);
            throw;
        }
    }

    /// <summary>
    /// Restores a transfer session from a resume token
    /// </summary>
    public async Task<string> RestoreTransferAsync(string resumeToken, FileMetadata metadata)
    {
        try
        {
            var tokenEntity = await _resumeTokenRepository.GetByTokenAsync(resumeToken);
            if (tokenEntity == null)
            {
                throw new InvalidOperationException($"Resume token {resumeToken} not found");
            }

            if (tokenEntity.IsCompleted)
            {
                throw new InvalidOperationException($"Resume token {resumeToken} is already completed");
            }

            var transferId = tokenEntity.TransferId;
            
            var session = new TransferSession
            {
                TransferId = transferId,
                Metadata = metadata,
                CreatedAt = tokenEntity.CreatedAt,
                TempDirectory = tokenEntity.TempDirectory ?? Path.Combine(Path.GetTempPath(), "MySqlBackup", transferId),
                CompletedChunks = new HashSet<int>(tokenEntity.CompletedChunks.Select(c => c.ChunkIndex)),
                LastActivity = DateTime.UtcNow
            };

            // Ensure temp directory exists
            Directory.CreateDirectory(session.TempDirectory);

            lock (_lockObject)
            {
                _activeSessions[transferId] = session;
            }

            // Update last activity
            await _resumeTokenRepository.UpdateLastActivityAsync(resumeToken);

            _logger.LogInformation("Restored transfer session {TransferId} from resume token {ResumeToken}. Completed chunks: {Count}", 
                transferId, resumeToken, session.CompletedChunks.Count);

            return transferId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring transfer from resume token {ResumeToken}", resumeToken);
            throw;
        }
    }

    /// <summary>
    /// Cleans up a transfer session
    /// </summary>
    private async Task CleanupSessionAsync(string transferId)
    {
        try
        {
            TransferSession? session;
            lock (_lockObject)
            {
                if (!_activeSessions.TryGetValue(transferId, out session))
                {
                    return;
                }
                _activeSessions.Remove(transferId);
            }

            if (Directory.Exists(session.TempDirectory))
            {
                Directory.Delete(session.TempDirectory, true);
            }

            _logger.LogDebug("Cleaned up transfer session {TransferId}", transferId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error cleaning up transfer session {TransferId}", transferId);
        }
    }

    /// <summary>
    /// Generates a unique resume token
    /// </summary>
    private static string GenerateResumeToken()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var randomBytes = new byte[8];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        var randomHex = Convert.ToHexString(randomBytes);
        return $"RT_{timestamp}_{randomHex}";
    }

    /// <summary>
    /// Extracts chunk index from chunk filename
    /// </summary>
    private static int ExtractChunkIndex(string fileName)
    {
        // Expected format: chunk_000001.dat
        var match = System.Text.RegularExpressions.Regex.Match(fileName, @"chunk_(\d+)\.dat");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int index))
        {
            return index;
        }
        throw new InvalidOperationException($"Invalid chunk filename format: {fileName}");
    }

    /// <summary>
    /// Represents an active transfer session
    /// </summary>
    private class TransferSession
    {
        public string TransferId { get; set; } = string.Empty;
        public FileMetadata Metadata { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public string TempDirectory { get; set; } = string.Empty;
        public HashSet<int> CompletedChunks { get; set; } = new();
    }
}