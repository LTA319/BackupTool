using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// 文件传输操作的配置
/// Configuration for file transfer operations
/// </summary>
public class TransferConfig
{
    /// <summary>
    /// 目标服务器端点
    /// Target server endpoint
    /// </summary>
    [Required]
    public ServerEndpoint TargetServer { get; set; } = new();

    /// <summary>
    /// 目标目录路径，最大长度500字符
    /// Target directory path, maximum 500 characters
    /// </summary>
    [Required]
    [StringLength(500)]
    public string TargetDirectory { get; set; } = string.Empty;

    /// <summary>
    /// 文件名，最大长度255字符
    /// File name, maximum 255 characters
    /// </summary>
    [Required]
    [StringLength(255)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 分块策略配置
    /// Chunking strategy configuration
    /// </summary>
    public ChunkingStrategy ChunkingStrategy { get; set; } = new();

    /// <summary>
    /// 超时时间（秒），默认为300秒（5分钟）
    /// Timeout in seconds, defaults to 300 seconds (5 minutes)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// 最大重试次数，默认为3次
    /// Maximum retry attempts, defaults to 3
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// 文件传输请求
/// Request for file transfer
/// </summary>
public class TransferRequest
{
    /// <summary>
    /// 传输标识符，默认生成新的GUID
    /// Transfer identifier, defaults to new GUID
    /// </summary>
    [Required]
    public string TransferId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 文件元数据信息
    /// File metadata information
    /// </summary>
    [Required]
    public FileMetadata Metadata { get; set; } = new();

    /// <summary>
    /// 分块策略配置
    /// Chunking strategy configuration
    /// </summary>
    public ChunkingStrategy ChunkingStrategy { get; set; } = new();

    /// <summary>
    /// 是否恢复传输，默认为false
    /// Whether to resume transfer, defaults to false
    /// </summary>
    public bool ResumeTransfer { get; set; } = false;

    /// <summary>
    /// 恢复传输的令牌
    /// Resume token for transfer
    /// </summary>
    public string? ResumeToken { get; set; }

    /// <summary>
    /// 传输请求的身份验证令牌
    /// Authentication token for the transfer request
    /// </summary>
    [Required]
    public string AuthenticationToken { get; set; } = string.Empty;

    /// <summary>
    /// 发起请求的客户端ID
    /// Client ID making the request
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;
}

/// <summary>
/// 被传输文件的元数据信息
/// Metadata about a file being transferred
/// </summary>
public class FileMetadata
{
    /// <summary>
    /// 文件名，最大长度255字符
    /// File name, maximum 255 characters
    /// </summary>
    [Required]
    [StringLength(255)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小（字节），必须为非负数
    /// File size in bytes, must be non-negative
    /// </summary>
    [Range(0, long.MaxValue)]
    public long FileSize { get; set; }

    /// <summary>
    /// MD5校验和，最大长度32字符
    /// MD5 checksum, maximum 32 characters
    /// </summary>
    [StringLength(32)]
    public string ChecksumMD5 { get; set; } = string.Empty;

    /// <summary>
    /// SHA256校验和，最大长度64字符
    /// SHA256 checksum, maximum 64 characters
    /// </summary>
    [StringLength(64)]
    public string ChecksumSHA256 { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间，默认为当前UTC时间
    /// Creation time, defaults to current UTC time
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 源备份配置
    /// Source backup configuration
    /// </summary>
    public BackupConfiguration? SourceConfig { get; set; }
}

/// <summary>
/// 具有优化默认值的大文件分块策略
/// Strategy for chunking large files with optimized defaults
/// </summary>
public class ChunkingStrategy
{
    /// <summary>
    /// 分块大小（字节），最小1KB，默认为10MB
    /// Chunk size in bytes, minimum 1KB, defaults to 10MB
    /// </summary>
    [Range(1024, long.MaxValue)]
    public long ChunkSize { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// 最大并发分块数，范围1-10，默认为4
    /// Maximum concurrent chunks, range 1-10, defaults to 4
    /// </summary>
    [Range(1, 10)]
    public int MaxConcurrentChunks { get; set; } = 4;

    /// <summary>
    /// 是否启用压缩，默认为true
    /// Whether to enable compression, defaults to true
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// 计算文件所需的总分块数
    /// Calculates the total number of chunks needed for a file
    /// </summary>
    /// <param name="fileSize">文件大小 / File size</param>
    /// <returns>分块数量 / Number of chunks</returns>
    public int CalculateChunkCount(long fileSize)
    {
        return (int)Math.Ceiling((double)fileSize / ChunkSize);
    }

    /// <summary>
    /// 根据文件大小获取优化的分块策略
    /// Gets an optimized chunking strategy based on file size
    /// </summary>
    /// <param name="fileSize">文件大小 / File size</param>
    /// <returns>优化的分块策略 / Optimized chunking strategy</returns>
    public static ChunkingStrategy GetOptimizedStrategy(long fileSize)
    {
        if (fileSize < 10 * 1024 * 1024) // < 10MB
        {
            return new ChunkingStrategy
            {
                ChunkSize = 1 * 1024 * 1024, // 1MB chunks for small files
                MaxConcurrentChunks = 2,
                EnableCompression = true
            };
        }
        else if (fileSize < 100 * 1024 * 1024) // < 100MB
        {
            return new ChunkingStrategy
            {
                ChunkSize = 5 * 1024 * 1024, // 5MB chunks for medium files
                MaxConcurrentChunks = 4,
                EnableCompression = true
            };
        }
        else if (fileSize < 1024 * 1024 * 1024) // < 1GB
        {
            return new ChunkingStrategy
            {
                ChunkSize = 10 * 1024 * 1024, // 10MB chunks for large files
                MaxConcurrentChunks = 6,
                EnableCompression = true
            };
        }
        else // >= 1GB
        {
            return new ChunkingStrategy
            {
                ChunkSize = 25 * 1024 * 1024, // 25MB chunks for huge files
                MaxConcurrentChunks = 8,
                EnableCompression = true
            };
        }
    }
}

/// <summary>
/// Data for a single file chunk
/// </summary>
public class ChunkData
{
    [Required]
    public string TransferId { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int ChunkIndex { get; set; }

    [Required]
    public byte[] Data { get; set; } = Array.Empty<byte>();

    [StringLength(32)]
    public string ChunkChecksum { get; set; } = string.Empty;

    public bool IsLastChunk { get; set; } = false;
}

/// <summary>
/// Result of a file transfer operation
/// </summary>
public class TransferResult
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public long BytesTransferred { get; set; }

    public TimeSpan Duration { get; set; }

    public string? ResumeToken { get; set; }

    public string? ChecksumHash { get; set; }

    public double TransferRate => Duration.TotalSeconds > 0 ? BytesTransferred / Duration.TotalSeconds : 0;
}

/// <summary>
/// Response from server for transfer operations
/// </summary>
public class TransferResponse
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public string? AdditionalInfo { get; set; }
}

/// <summary>
/// Request for receiving a file
/// </summary>
public class ReceiveRequest
{
    [Required]
    public string TransferId { get; set; } = string.Empty;

    [Required]
    public FileMetadata Metadata { get; set; } = new();

    [Required]
    [StringLength(500)]
    public string TargetPath { get; set; } = string.Empty;

    /// <summary>
    /// Authentication token for the receive request
    /// </summary>
    [Required]
    public string AuthenticationToken { get; set; } = string.Empty;

    /// <summary>
    /// Client ID making the request
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;
}

/// <summary>
/// Result of a file reception operation
/// </summary>
public class ReceiveResult
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    [StringLength(500)]
    public string? FilePath { get; set; }

    public long BytesReceived { get; set; }

    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Result of processing a file chunk
/// </summary>
public class ChunkResult
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public int ChunkIndex { get; set; }

    public bool IsComplete { get; set; } = false;
}

/// <summary>
/// Information needed to resume an interrupted transfer
/// </summary>
public class ResumeInfo
{
    [Required]
    public string TransferId { get; set; } = string.Empty;

    [Required]
    public FileMetadata Metadata { get; set; } = new();

    public int LastCompletedChunk { get; set; } = -1;

    public List<int> CompletedChunks { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}