using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// 用于存储恢复令牌信息的数据库实体
/// Database entity for storing resume token information
/// </summary>
public class ResumeToken
{
    /// <summary>
    /// 主键标识符
    /// Primary key identifier
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 恢复令牌字符串，最大长度100字符
    /// Resume token string, maximum 100 characters
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 传输标识符，最大长度100字符
    /// Transfer identifier, maximum 100 characters
    /// </summary>
    [Required]
    [StringLength(100)]
    public string TransferId { get; set; } = string.Empty;

    /// <summary>
    /// 关联的备份日志ID
    /// Associated backup log ID
    /// </summary>
    public int? BackupLogId { get; set; }

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
    public string? ChecksumMD5 { get; set; }

    /// <summary>
    /// SHA256校验和，最大长度64字符
    /// SHA256 checksum, maximum 64 characters
    /// </summary>
    [StringLength(64)]
    public string? ChecksumSHA256 { get; set; }

    /// <summary>
    /// 临时目录路径，最大长度500字符
    /// Temporary directory path, maximum 500 characters
    /// </summary>
    [StringLength(500)]
    public string? TempDirectory { get; set; }

    /// <summary>
    /// 创建时间，默认为当前UTC时间
    /// Creation time, defaults to current UTC time
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 最后活动时间，默认为当前UTC时间
    /// Last activity time, defaults to current UTC time
    /// </summary>
    public DateTime LastActivity { get; set; } = DateTime.Now;

    /// <summary>
    /// 是否已完成，默认为false
    /// Whether completed, defaults to false
    /// </summary>
    public bool IsCompleted { get; set; } = false;

    /// <summary>
    /// 导航属性：关联的备份日志
    /// Navigation property: associated backup log
    /// </summary>
    public BackupLog? BackupLog { get; set; }

    /// <summary>
    /// 已完成的分块集合
    /// Collection of completed chunks
    /// </summary>
    public List<ResumeChunk> CompletedChunks { get; set; } = new();
}

/// <summary>
/// Database entity for tracking completed chunks in a resume session
/// </summary>
public class ResumeChunk
{
    public int Id { get; set; }

    public int ResumeTokenId { get; set; }

    [Range(0, int.MaxValue)]
    public int ChunkIndex { get; set; }

    [Range(0, long.MaxValue)]
    public long ChunkSize { get; set; }

    [StringLength(32)]
    public string? ChunkChecksum { get; set; }

    public DateTime CompletedAt { get; set; } = DateTime.Now;

    // Navigation property
    public ResumeToken? ResumeToken { get; set; }
}

/// <summary>
/// Transfer state information for persistence
/// </summary>
public class TransferState
{
    [Required]
    public string TransferId { get; set; } = string.Empty;

    [Required]
    public FileMetadata Metadata { get; set; } = new();

    public List<int> CompletedChunks { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime LastActivity { get; set; } = DateTime.Now;

    [StringLength(500)]
    public string? TempDirectory { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets the last completed chunk index
    /// </summary>
    public int LastCompletedChunk => CompletedChunks.Count > 0 ? CompletedChunks.Max() : -1;

    /// <summary>
    /// Gets the total number of completed chunks
    /// </summary>
    public int CompletedChunkCount => CompletedChunks.Count;

    /// <summary>
    /// Calculates the completion percentage
    /// </summary>
    public double CalculateProgress(ChunkingStrategy strategy)
    {
        var totalChunks = strategy.CalculateChunkCount(Metadata.FileSize);
        return totalChunks > 0 ? (double)CompletedChunkCount / totalChunks : 0.0;
    }
}