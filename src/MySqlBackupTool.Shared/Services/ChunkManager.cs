using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Security.Cryptography;
using System.Text;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 在大文件传输过程中管理文件块的服务 / Manages file chunks during large file transfers
/// </summary>
public class ChunkManager : IChunkManager
{
    /// <summary>
    /// 日志记录器 / Logger
    /// </summary>
    private readonly ILogger<ChunkManager> _logger;
    
    /// <summary>
    /// 校验和服务 / Checksum service
    /// </summary>
    private readonly IChecksumService _checksumService;
    
    /// <summary>
    /// 恢复令牌存储库 / Resume token repository
    /// </summary>
    private readonly IResumeTokenRepository _resumeTokenRepository;
    
    /// <summary>
    /// 活动会话字典 / Active sessions dictionary
    /// </summary>
    private readonly Dictionary<string, TransferSession> _activeSessions = new();
    
    /// <summary>
    /// 锁对象，用于线程安全 / Lock object for thread safety
    /// </summary>
    private readonly object _lockObject = new();

    /// <summary>
    /// 初始化块管理器 / Initializes the chunk manager
    /// </summary>
    /// <param name="logger">日志记录器 / Logger</param>
    /// <param name="checksumService">校验和服务 / Checksum service</param>
    /// <param name="resumeTokenRepository">恢复令牌存储库 / Resume token repository</param>
    /// <exception cref="ArgumentNullException">当必需参数为null时抛出 / Thrown when required parameters are null</exception>
    public ChunkManager(ILogger<ChunkManager> logger, IChecksumService checksumService, IResumeTokenRepository resumeTokenRepository)
    {
        _logger = logger;
        _checksumService = checksumService ?? throw new ArgumentNullException(nameof(checksumService));
        _resumeTokenRepository = resumeTokenRepository ?? throw new ArgumentNullException(nameof(resumeTokenRepository));
    }

    /// <summary>
    /// 初始化新的文件传输会话 / Initializes a new file transfer session
    /// </summary>
    /// <param name="metadata">文件元数据 / File metadata</param>
    /// <returns>传输ID / Transfer ID</returns>
    public async Task<string> InitializeTransferAsync(FileMetadata metadata)
    {
        var transferId = Guid.NewGuid().ToString();
        
        try
        {
            var session = new TransferSession
            {
                TransferId = transferId,
                Metadata = metadata,
                CreatedAt = DateTime.Now,
                TempDirectory = Path.Combine(Path.GetTempPath(), "MySqlBackup", transferId),
                CompletedChunks = new HashSet<int>(),
                LastActivity = DateTime.Now
            };

            // 为块创建临时目录 / Create temporary directory for chunks
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
    /// 接收并处理文件块 / Receives and processes a file chunk
    /// </summary>
    /// <param name="transferId">传输ID / Transfer ID</param>
    /// <param name="chunk">块数据 / Chunk data</param>
    /// <returns>块处理结果 / Chunk processing result</returns>
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
                session.LastActivity = DateTime.Now;
            }

            // 验证块校验和 / Validate chunk checksum
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

            // 将块保存到临时文件 / Save chunk to temporary file
            var chunkPath = Path.Combine(session.TempDirectory, $"chunk_{chunk.ChunkIndex:D6}.dat");
            await File.WriteAllBytesAsync(chunkPath, chunk.Data);

            lock (_lockObject)
            {
                session.CompletedChunks.Add(chunk.ChunkIndex);
            }

            // 如果存在恢复令牌，则更新它 / Update resume token if it exists
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
                // 不要因为恢复令牌问题而使块处理失败 / Don't fail the chunk processing for resume token issues
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
    /// 通过重新组装所有块来完成文件传输 / Finalizes a file transfer by reassembling all chunks
    /// </summary>
    /// <param name="transferId">传输ID / Transfer ID</param>
    /// <param name="targetPath">最终文件的目标路径（可选，如果未提供则使用临时目录） / Target path for the final file (optional, uses temp directory if not provided)</param>
    /// <returns>最终文件路径 / Final file path</returns>
    /// <exception cref="InvalidOperationException">当传输会话不存在或文件验证失败时抛出 / Thrown when transfer session doesn't exist or file validation fails</exception>
    public async Task<string> FinalizeTransferAsync(string transferId, string? targetPath = null)
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

            // 创建最终文件路径 / Create final file path
            // 如果提供了目标路径则使用它，否则使用临时目录 / Use provided target path if available, otherwise use temp directory
            var finalPath = !string.IsNullOrWhiteSpace(targetPath) 
                ? targetPath 
                : Path.Combine(
                    Path.GetDirectoryName(session.TempDirectory) ?? Path.GetTempPath(),
                    session.Metadata.FileName);
            
            _logger.LogInformation("Using {PathType} for final file: {FinalPath}", 
                !string.IsNullOrWhiteSpace(targetPath) ? "provided target path" : "temp directory path",
                finalPath);

            // 确保目标目录存在 / Ensure target directory exists
            var targetDir = Path.GetDirectoryName(finalPath);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
                _logger.LogDebug("Created target directory: {Directory}", targetDir);
            }

            // 重新组装块 / Reassemble chunks
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
            } // 确保FileStream在验证前被释放 / Ensure FileStream is disposed before validation

            // 强制垃圾回收以确保文件句柄被释放 / Force garbage collection to ensure file handle is released
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // 验证最终文件 / Validate final file
            var fileInfo = new FileInfo(finalPath);
            if (fileInfo.Length != session.Metadata.FileSize)
            {
                throw new InvalidOperationException(
                    $"Final file size {fileInfo.Length} does not match expected size {session.Metadata.FileSize}");
            }

            // 如果提供了校验和，则验证校验和 / Validate checksum if provided
            if (!string.IsNullOrEmpty(session.Metadata.ChecksumMD5) || !string.IsNullOrEmpty(session.Metadata.ChecksumSHA256))
            {
                var isValid = await _checksumService.ValidateFileIntegrityAsync(
                    finalPath, 
                    session.Metadata.ChecksumMD5, 
                    session.Metadata.ChecksumSHA256);
                
                if (!isValid)
                {
                    // 记录详细的校验和信息用于调试 / Log detailed checksum information for debugging
                    var actualChecksum = await _checksumService.CalculateFileMD5Async(finalPath);
                    _logger.LogError("Checksum validation failed. Expected: {Expected}, Actual: {Actual}, File: {FilePath}", 
                        session.Metadata.ChecksumMD5, actualChecksum, finalPath);
                    throw new InvalidOperationException($"Final file checksum validation failed. Expected: {session.Metadata.ChecksumMD5}, Actual: {actualChecksum}");
                }
            }

            // 清理临时文件 / Clean up temporary files
            await CleanupSessionAsync(transferId);

            // 如果存在恢复令牌，则标记为已完成 / Mark resume token as completed if it exists
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
                // 不要因为恢复令牌问题而使完成操作失败 / Don't fail the finalization for resume token issues
            }

            _logger.LogInformation("Successfully finalized transfer {TransferId} to {FilePath}", 
                transferId, finalPath);

            return finalPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finalizing transfer {TransferId}", transferId);
            
            // 出错时清理 / Clean up on error
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
                LastActivity = DateTime.Now,
                IsCompleted = false
            };

            await _resumeTokenRepository.AddAsync(tokenEntity);
            await _resumeTokenRepository.SaveChangesAsync();

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
                LastActivity = DateTime.Now
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