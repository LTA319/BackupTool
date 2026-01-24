using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// Represents a backup operation record
/// </summary>
public class BackupOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public string DatabaseName { get; set; } = string.Empty;
    
    [Required]
    public string BackupPath { get; set; } = string.Empty;
    
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public BackupStatus Status { get; set; } = BackupStatus.Queued;
    public long FileSize { get; set; }
    public double CompressionRatio { get; set; }
    public string? ErrorMessage { get; set; }
    public BackupType BackupType { get; set; } = BackupType.Full;
    public string? ChecksumHash { get; set; }
    
    // Calculated properties
    public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;
    public bool IsCompleted => Status == BackupStatus.Completed;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
}

/// <summary>
/// Type of backup operation
/// </summary>
public enum BackupType
{
    Full,
    Incremental,
    Differential,
    Transaction
}

/// <summary>
/// Result of a backup operation
/// </summary>
public class BackupResult
{
    public Guid OperationId { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? BackupFilePath { get; set; }
    public long FileSize { get; set; }
    public TimeSpan Duration { get; set; }
    public double CompressionRatio { get; set; }
    public string? ChecksumHash { get; set; }
    public DateTime CompletedAt { get; set; }
    public IEnumerable<string> Warnings { get; set; } = new List<string>();
}

/// <summary>
/// Progress information for backup operations
/// </summary>
public class BackupProgressInfo
{
    public Guid OperationId { get; set; }
    public int PercentComplete { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public long BytesProcessed { get; set; }
    public long TotalBytes { get; set; }
    public TimeSpan ElapsedTime { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    public string? StatusMessage { get; set; }
}

/// <summary>
/// Validation result for backup configurations
/// </summary>
public class BackupValidationResult
{
    public bool IsValid { get; set; }
    public IEnumerable<string> Errors { get; set; } = new List<string>();
    public IEnumerable<string> Warnings { get; set; } = new List<string>();
    public IEnumerable<string> Recommendations { get; set; } = new List<string>();
}