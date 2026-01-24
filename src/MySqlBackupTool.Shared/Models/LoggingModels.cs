using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// Status of a backup operation
/// </summary>
public enum BackupStatus
{
    Queued,
    StoppingMySQL,
    Compressing,
    Transferring,
    StartingMySQL,
    Verifying,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Log entry for a backup operation
/// </summary>
public class BackupLog
{
    public int Id { get; set; }

    public int BackupConfigId { get; set; }

    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    public DateTime? EndTime { get; set; }

    public BackupStatus Status { get; set; } = BackupStatus.Queued;

    [StringLength(500)]
    public string? FilePath { get; set; }

    [Range(0, long.MaxValue)]
    public long? FileSize { get; set; }

    public string? ErrorMessage { get; set; }

    [StringLength(100)]
    public string? ResumeToken { get; set; }

    public List<TransferLog> TransferLogs { get; set; } = new();

    /// <summary>
    /// Gets the duration of the backup operation
    /// </summary>
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;

    /// <summary>
    /// Indicates if the backup operation is currently running
    /// </summary>
    public bool IsRunning => Status != BackupStatus.Completed && 
                            Status != BackupStatus.Failed && 
                            Status != BackupStatus.Cancelled;
}

/// <summary>
/// Log entry for a file transfer chunk
/// </summary>
public class TransferLog
{
    public int Id { get; set; }

    public int BackupLogId { get; set; }

    [Range(0, int.MaxValue)]
    public int ChunkIndex { get; set; }

    [Range(0, long.MaxValue)]
    public long ChunkSize { get; set; }

    public DateTime TransferTime { get; set; } = DateTime.UtcNow;

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Pending";

    public string? ErrorMessage { get; set; }

    // Navigation property
    public BackupLog? BackupLog { get; set; }
}

/// <summary>
/// Progress information for a backup operation
/// </summary>
public class BackupProgress
{
    public Guid OperationId { get; set; } = Guid.NewGuid();

    public BackupStatus CurrentStatus { get; set; } = BackupStatus.Queued;

    [Range(0.0, 1.0)]
    public double OverallProgress { get; set; } = 0.0;

    [StringLength(200)]
    public string CurrentOperation { get; set; } = string.Empty;

    [Range(0, long.MaxValue)]
    public long BytesTransferred { get; set; } = 0;

    [Range(0, long.MaxValue)]
    public long TotalBytes { get; set; } = 0;

    public TimeSpan ElapsedTime { get; set; } = TimeSpan.Zero;

    public TimeSpan? EstimatedTimeRemaining { get; set; }

    [Range(0, double.MaxValue)]
    public double TransferRate { get; set; } = 0; // bytes per second

    /// <summary>
    /// Gets the percentage completion as a value between 0 and 100
    /// </summary>
    public double PercentComplete => OverallProgress * 100;

    /// <summary>
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
/// Progress information for compression operations
/// </summary>
public class CompressionProgress
{
    [Range(0.0, 1.0)]
    public double Progress { get; set; } = 0.0;

    [StringLength(500)]
    public string CurrentFile { get; set; } = string.Empty;

    [Range(0, long.MaxValue)]
    public long ProcessedBytes { get; set; } = 0;

    [Range(0, long.MaxValue)]
    public long TotalBytes { get; set; } = 0;

    public int ProcessedFiles { get; set; } = 0;

    public int TotalFiles { get; set; } = 0;

    /// <summary>
    /// Gets the percentage completion as a value between 0 and 100
    /// </summary>
    public double PercentComplete => Progress * 100;
}