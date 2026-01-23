using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Repository interface for BackupLog entities
/// </summary>
public interface IBackupLogRepository : IRepository<BackupLog>
{
    /// <summary>
    /// Gets backup logs for a specific configuration
    /// </summary>
    Task<IEnumerable<BackupLog>> GetByConfigurationIdAsync(int configurationId);

    /// <summary>
    /// Gets backup logs within a date range
    /// </summary>
    Task<IEnumerable<BackupLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Gets backup logs by status
    /// </summary>
    Task<IEnumerable<BackupLog>> GetByStatusAsync(BackupStatus status);

    /// <summary>
    /// Gets running backup operations
    /// </summary>
    Task<IEnumerable<BackupLog>> GetRunningBackupsAsync();

    /// <summary>
    /// Gets failed backup operations
    /// </summary>
    Task<IEnumerable<BackupLog>> GetFailedBackupsAsync();

    /// <summary>
    /// Gets backup logs with transfer logs included
    /// </summary>
    Task<BackupLog?> GetWithTransferLogsAsync(int id);

    /// <summary>
    /// Gets backup statistics for a date range
    /// </summary>
    Task<BackupStatistics> GetStatisticsAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Cleans up old backup logs based on retention policy
    /// </summary>
    Task<int> CleanupOldLogsAsync(int maxAgeDays, int? maxCount = null);

    /// <summary>
    /// Gets the most recent backup log for a configuration
    /// </summary>
    Task<BackupLog?> GetMostRecentAsync(int configurationId);

    /// <summary>
    /// Updates the status of a backup log
    /// </summary>
    Task<bool> UpdateStatusAsync(int id, BackupStatus status, string? errorMessage = null);

    /// <summary>
    /// Completes a backup log with end time and final status
    /// </summary>
    Task<bool> CompleteBackupAsync(int id, BackupStatus finalStatus, string? filePath = null, long? fileSize = null, string? errorMessage = null);
}

/// <summary>
/// Statistics for backup operations
/// </summary>
public class BackupStatistics
{
    public int TotalBackups { get; set; }
    public int SuccessfulBackups { get; set; }
    public int FailedBackups { get; set; }
    public int CancelledBackups { get; set; }
    public long TotalBytesTransferred { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public double AverageBackupSize { get; set; }
    public double SuccessRate { get; set; }
    public TimeSpan AverageDuration { get; set; }
}