using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// 备份操作的状态
/// Status of a backup operation
/// </summary>
public enum BackupStatus
{
    /// <summary>
    /// 已排队等待执行
    /// Queued for execution
    /// </summary>
    Queued,
    
    /// <summary>
    /// 正在停止MySQL服务
    /// Stopping MySQL service
    /// </summary>
    StoppingMySQL,
    
    /// <summary>
    /// 正在压缩文件
    /// Compressing files
    /// </summary>
    Compressing,
    
    /// <summary>
    /// 正在传输文件
    /// Transferring files
    /// </summary>
    Transferring,
    
    /// <summary>
    /// 正在启动MySQL服务
    /// Starting MySQL service
    /// </summary>
    StartingMySQL,
    
    /// <summary>
    /// 正在验证备份
    /// Verifying backup
    /// </summary>
    Verifying,
    
    /// <summary>
    /// 已完成
    /// Completed successfully
    /// </summary>
    Completed,
    
    /// <summary>
    /// 执行失败
    /// Failed to execute
    /// </summary>
    Failed,
    
    /// <summary>
    /// 已取消
    /// Cancelled by user
    /// </summary>
    Cancelled
}

/// <summary>
/// 备份操作的日志记录
/// Log entry for a backup operation
/// </summary>
public class BackupLog
{
    /// <summary>
    /// 日志记录的唯一标识符
    /// Unique identifier for the log entry
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 关联的备份配置ID
    /// Associated backup configuration ID
    /// </summary>
    public int BackupConfigId { get; set; }

    /// <summary>
    /// 备份开始时间，默认为当前UTC时间
    /// Backup start time, defaults to current UTC time
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 备份结束时间，null表示尚未结束
    /// Backup end time, null if not yet finished
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 当前备份状态，默认为已排队
    /// Current backup status, defaults to Queued
    /// </summary>
    public BackupStatus Status { get; set; } = BackupStatus.Queued;

    /// <summary>
    /// 备份文件路径，最大长度500字符
    /// Backup file path, maximum 500 characters
    /// </summary>
    [StringLength(500)]
    public string? FilePath { get; set; }

    /// <summary>
    /// 备份文件大小（字节），必须为非负数
    /// Backup file size in bytes, must be non-negative
    /// </summary>
    [Range(0, long.MaxValue)]
    public long? FileSize { get; set; }

    /// <summary>
    /// 错误消息（如果备份失败）
    /// Error message (if backup failed)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 断点续传令牌，最大长度100字符
    /// Resume token for interrupted transfers, maximum 100 characters
    /// </summary>
    [StringLength(100)]
    public string? ResumeToken { get; set; }

    /// <summary>
    /// 关联的传输日志记录列表
    /// Associated transfer log entries
    /// </summary>
    public List<TransferLog> TransferLogs { get; set; } = new();

    /// <summary>
    /// 获取备份操作的持续时间
    /// Gets the duration of the backup operation
    /// </summary>
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;

    /// <summary>
    /// 指示备份操作是否正在运行
    /// Indicates if the backup operation is currently running
    /// </summary>
    public bool IsRunning => Status != BackupStatus.Completed && 
                            Status != BackupStatus.Failed && 
                            Status != BackupStatus.Cancelled;
}

/// <summary>
/// 文件传输分块的日志记录
/// Log entry for a file transfer chunk
/// </summary>
public class TransferLog
{
    /// <summary>
    /// 日志记录的唯一标识符
    /// Unique identifier for the log entry
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 关联的备份日志ID
    /// Associated backup log ID
    /// </summary>
    public int BackupLogId { get; set; }

    /// <summary>
    /// 分块索引，必须为非负数
    /// Chunk index, must be non-negative
    /// </summary>
    [Range(0, int.MaxValue)]
    public int ChunkIndex { get; set; }

    /// <summary>
    /// 分块大小（字节），必须为非负数
    /// Chunk size in bytes, must be non-negative
    /// </summary>
    [Range(0, long.MaxValue)]
    public long ChunkSize { get; set; }

    /// <summary>
    /// 传输时间，默认为当前UTC时间
    /// Transfer time, defaults to current UTC time
    /// </summary>
    public DateTime TransferTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 传输状态，默认为"Pending"，最大长度20字符
    /// Transfer status, defaults to "Pending", maximum 20 characters
    /// </summary>
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// 错误消息（如果传输失败）
    /// Error message (if transfer failed)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 导航属性：关联的备份日志
    /// Navigation property: associated backup log
    /// </summary>
    public BackupLog? BackupLog { get; set; }
}

/// <summary>
/// 传输统计信息
/// Transfer statistics information
/// </summary>
public class TransferStatistics
{
    /// <summary>
    /// 总传输数量
    /// Total transfer count
    /// </summary>
    public int TotalTransfers { get; set; }

    /// <summary>
    /// 成功传输数量
    /// Successful transfer count
    /// </summary>
    public int SuccessfulTransfers { get; set; }

    /// <summary>
    /// 失败传输数量
    /// Failed transfer count
    /// </summary>
    public int FailedTransfers { get; set; }

    /// <summary>
    /// 正在进行的传输数量
    /// Ongoing transfer count
    /// </summary>
    public int OngoingTransfers { get; set; }

    /// <summary>
    /// 总传输字节数
    /// Total bytes transferred
    /// </summary>
    public long TotalBytesTransferred { get; set; }

    /// <summary>
    /// 平均传输速度（字节/秒）
    /// Average transfer speed (bytes/second)
    /// </summary>
    public double AverageTransferSpeed { get; set; }

    /// <summary>
    /// 成功率百分比
    /// Success rate percentage
    /// </summary>
    public double SuccessRate => TotalTransfers > 0 ? (double)SuccessfulTransfers / TotalTransfers * 100 : 0;

    /// <summary>
    /// 失败率百分比
    /// Failure rate percentage
    /// </summary>
    public double FailureRate => TotalTransfers > 0 ? (double)FailedTransfers / TotalTransfers * 100 : 0;
}

/// <summary>
/// 传输进度信息
/// Transfer progress information
/// </summary>
public class TransferProgress
{
    /// <summary>
    /// 备份日志ID
    /// Backup log ID
    /// </summary>
    public int BackupLogId { get; set; }

    /// <summary>
    /// 总分块数量
    /// Total chunk count
    /// </summary>
    public int TotalChunks { get; set; }

    /// <summary>
    /// 已完成分块数量
    /// Completed chunk count
    /// </summary>
    public int CompletedChunks { get; set; }

    /// <summary>
    /// 失败分块数量
    /// Failed chunk count
    /// </summary>
    public int FailedChunks { get; set; }

    /// <summary>
    /// 总字节数
    /// Total bytes
    /// </summary>
    public long TotalBytes { get; set; }

    /// <summary>
    /// 已传输字节数
    /// Transferred bytes
    /// </summary>
    public long TransferredBytes { get; set; }

    /// <summary>
    /// 进度百分比
    /// Progress percentage
    /// </summary>
    public double ProgressPercentage => TotalChunks > 0 ? (double)CompletedChunks / TotalChunks * 100 : 0;

    /// <summary>
    /// 字节进度百分比
    /// Byte progress percentage
    /// </summary>
    public double ByteProgressPercentage => TotalBytes > 0 ? (double)TransferredBytes / TotalBytes * 100 : 0;

    /// <summary>
    /// 是否完成
    /// Is completed
    /// </summary>
    public bool IsCompleted => CompletedChunks == TotalChunks && FailedChunks == 0;

    /// <summary>
    /// 最后更新时间
    /// Last update time
    /// </summary>
    public DateTime LastUpdateTime { get; set; } = DateTime.Now;
}

/// <summary>
/// 传输错误摘要
/// Transfer error summary
/// </summary>
public class TransferErrorSummary
{
    /// <summary>
    /// 错误消息
    /// Error message
    /// </summary>
    [Required]
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// 错误发生次数
    /// Error occurrence count
    /// </summary>
    public int OccurrenceCount { get; set; }

    /// <summary>
    /// 首次发生时间
    /// First occurrence time
    /// </summary>
    public DateTime FirstOccurrence { get; set; }

    /// <summary>
    /// 最后发生时间
    /// Last occurrence time
    /// </summary>
    public DateTime LastOccurrence { get; set; }

    /// <summary>
    /// 影响的备份日志ID列表
    /// Affected backup log IDs
    /// </summary>
    public List<int> AffectedBackupLogIds { get; set; } = new();
}

/// <summary>
/// 备份操作的进度信息
/// Progress information for a backup operation
/// </summary>
public class BackupProgress
{
    /// <summary>
    /// 操作标识符，默认生成新的GUID
    /// Operation identifier, defaults to new GUID
    /// </summary>
    public Guid OperationId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 当前备份状态，默认为已排队
    /// Current backup status, defaults to Queued
    /// </summary>
    public BackupStatus CurrentStatus { get; set; } = BackupStatus.Queued;

    /// <summary>
    /// 整体进度（0.0-1.0），默认为0.0
    /// Overall progress (0.0-1.0), defaults to 0.0
    /// </summary>
    [Range(0.0, 1.0)]
    public double OverallProgress { get; set; } = 0.0;

    /// <summary>
    /// 当前操作描述，最大长度200字符
    /// Current operation description, maximum 200 characters
    /// </summary>
    [StringLength(200)]
    public string CurrentOperation { get; set; } = string.Empty;

    /// <summary>
    /// 已传输的字节数，必须为非负数，默认为0
    /// Bytes transferred, must be non-negative, defaults to 0
    /// </summary>
    [Range(0, long.MaxValue)]
    public long BytesTransferred { get; set; } = 0;

    /// <summary>
    /// 总字节数，必须为非负数，默认为0
    /// Total bytes, must be non-negative, defaults to 0
    /// </summary>
    [Range(0, long.MaxValue)]
    public long TotalBytes { get; set; } = 0;

    /// <summary>
    /// 已用时间，默认为零
    /// Elapsed time, defaults to zero
    /// </summary>
    public TimeSpan ElapsedTime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// 预计剩余时间
    /// Estimated time remaining
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; set; }

    /// <summary>
    /// 传输速率（字节/秒），必须为非负数，默认为0
    /// Transfer rate (bytes per second), must be non-negative, defaults to 0
    /// </summary>
    [Range(0, double.MaxValue)]
    public double TransferRate { get; set; } = 0;

    /// <summary>
    /// 获取完成百分比（0-100之间的值）
    /// Gets the percentage completion as a value between 0 and 100
    /// </summary>
    public double PercentComplete => OverallProgress * 100;

    /// <summary>
    /// 获取可读的传输速率字符串
    /// Gets a human-readable transfer rate string
    /// </summary>
    public string TransferRateString
    {
        get
        {
            if (TransferRate < 1024)
                return $"{TransferRate:F1} B/s";
            else if (TransferRate < 1024 * 1024)
                return $"{TransferRate / 1024:F1} KB/s";
            else if (TransferRate < 1024 * 1024 * 1024)
                return $"{TransferRate / (1024 * 1024):F1} MB/s";
            else
                return $"{TransferRate / (1024 * 1024 * 1024):F1} GB/s";
        }
    }
}

/// <summary>
/// 压缩操作的进度信息
/// Progress information for compression operations
/// </summary>
public class CompressionProgress
{
    /// <summary>
    /// 压缩进度（0.0-1.0），默认为0.0
    /// Compression progress (0.0-1.0), defaults to 0.0
    /// </summary>
    [Range(0.0, 1.0)]
    public double Progress { get; set; } = 0.0;

    /// <summary>
    /// 当前正在处理的文件，最大长度500字符
    /// Current file being processed, maximum 500 characters
    /// </summary>
    [StringLength(500)]
    public string CurrentFile { get; set; } = string.Empty;

    /// <summary>
    /// 已处理的字节数，必须为非负数，默认为0
    /// Processed bytes, must be non-negative, defaults to 0
    /// </summary>
    [Range(0, long.MaxValue)]
    public long ProcessedBytes { get; set; } = 0;

    /// <summary>
    /// 总字节数，必须为非负数，默认为0
    /// Total bytes, must be non-negative, defaults to 0
    /// </summary>
    [Range(0, long.MaxValue)]
    public long TotalBytes { get; set; } = 0;

    /// <summary>
    /// 已处理的文件数，默认为0
    /// Number of processed files, defaults to 0
    /// </summary>
    public int ProcessedFiles { get; set; } = 0;

    /// <summary>
    /// 总文件数，默认为0
    /// Total number of files, defaults to 0
    /// </summary>
    public int TotalFiles { get; set; } = 0;

    /// <summary>
    /// 获取完成百分比（0-100之间的值）
    /// Gets the percentage completion as a value between 0 and 100
    /// </summary>
    public double PercentComplete => Progress * 100;
}