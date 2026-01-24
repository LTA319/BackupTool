using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// High-level service interface for backup logging operations
/// </summary>
public interface IBackupLogService
{
    /// <summary>
    /// Starts a new backup operation log
    /// </summary>
    Task<BackupLog> StartBackupAsync(int configurationId, string? resumeToken = null);

    /// <summary>
    /// Updates the status of a backup operation
    /// </summary>
    Task UpdateBackupStatusAsync(int backupLogId, BackupStatus status, string? currentOperation = null);

    /// <summary>
    /// Completes a backup operation with final status and details
    /// </summary>
    Task CompleteBackupAsync(int backupLogId, BackupStatus finalStatus, string? filePath = null, long? fileSize = null, string? errorMessage = null);

    /// <summary>
    /// Logs a transfer chunk operation
    /// </summary>
    Task LogTransferChunkAsync(int backupLogId, int chunkIndex, long chunkSize, string status, string? errorMessage = null);

    /// <summary>
    /// Gets backup logs with optional filtering
    /// </summary>
    Task<IEnumerable<BackupLog>> GetBackupLogsAsync(BackupLogFilter? filter = null);

    /// <summary>
    /// Gets detailed backup log with transfer logs
    /// </summary>
    Task<BackupLog?> GetBackupLogDetailsAsync(int backupLogId);

    /// <summary>
    /// Gets backup statistics for reporting
    /// </summary>
    Task<BackupStatistics> GetBackupStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// Searches backup logs based on criteria
    /// </summary>
    Task<BackupLogSearchResult> SearchBackupLogsAsync(BackupLogSearchCriteria criteria);

    /// <summary>
    /// Gets running backup operations
    /// </summary>
    Task<IEnumerable<BackupLog>> GetRunningBackupsAsync();

    /// <summary>
    /// Cancels a running backup operation
    /// </summary>
    Task CancelBackupAsync(int backupLogId, string reason);

    /// <summary>
    /// Cleans up old backup logs based on retention policy
    /// </summary>
    Task<int> CleanupOldLogsAsync(RetentionPolicy policy);
}

/// <summary>
/// Filter criteria for backup logs
/// </summary>
public class BackupLogFilter
{
    public int? ConfigurationId { get; set; }
    public BackupStatus? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? MaxResults { get; set; }
    public bool IncludeTransferLogs { get; set; } = false;
}

/// <summary>
/// Search criteria for backup logs
/// </summary>
public class BackupLogSearchCriteria
{
    public string? SearchText { get; set; }
    public int? ConfigurationId { get; set; }
    public BackupStatus? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public long? MinFileSize { get; set; }
    public long? MaxFileSize { get; set; }
    public bool? HasErrors { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string SortBy { get; set; } = "StartTime";
    public bool SortDescending { get; set; } = true;
}

/// <summary>
/// Search result for backup logs
/// </summary>
public class BackupLogSearchResult
{
    public IEnumerable<BackupLog> Logs { get; set; } = new List<BackupLog>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;
}