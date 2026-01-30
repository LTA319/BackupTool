using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// BackupLog实体的存储库接口
/// Repository interface for BackupLog entities
/// </summary>
public interface IBackupLogRepository : IRepository<BackupLog>
{
    /// <summary>
    /// 获取特定配置的备份日志
    /// Gets backup logs for a specific configuration
    /// </summary>
    Task<IEnumerable<BackupLog>> GetByConfigurationIdAsync(int configurationId);

    /// <summary>
    /// 获取日期范围内的备份日志
    /// Gets backup logs within a date range
    /// </summary>
    Task<IEnumerable<BackupLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// 根据状态获取备份日志
    /// Gets backup logs by status
    /// </summary>
    Task<IEnumerable<BackupLog>> GetByStatusAsync(BackupStatus status);

    /// <summary>
    /// 获取正在运行的备份操作
    /// Gets running backup operations
    /// </summary>
    Task<IEnumerable<BackupLog>> GetRunningBackupsAsync();

    /// <summary>
    /// 获取失败的备份操作
    /// Gets failed backup operations
    /// </summary>
    Task<IEnumerable<BackupLog>> GetFailedBackupsAsync();

    /// <summary>
    /// 获取包含传输日志的备份日志
    /// Gets backup logs with transfer logs included
    /// </summary>
    Task<BackupLog?> GetWithTransferLogsAsync(int id);

    /// <summary>
    /// 获取日期范围内的备份统计信息
    /// Gets backup statistics for a date range
    /// </summary>
    Task<BackupStatistics> GetStatisticsAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// 根据保留策略清理旧的备份日志
    /// Cleans up old backup logs based on retention policy
    /// </summary>
    Task<int> CleanupOldLogsAsync(int maxAgeDays, int? maxCount = null);

    /// <summary>
    /// 获取配置的最新备份日志
    /// Gets the most recent backup log for a configuration
    /// </summary>
    Task<BackupLog?> GetMostRecentAsync(int configurationId);

    /// <summary>
    /// 更新备份日志的状态
    /// Updates the status of a backup log
    /// </summary>
    Task<bool> UpdateStatusAsync(int id, BackupStatus status, string? errorMessage = null);

    /// <summary>
    /// 完成备份日志，设置结束时间和最终状态
    /// Completes a backup log with end time and final status
    /// </summary>
    Task<bool> CompleteBackupAsync(int id, BackupStatus finalStatus, string? filePath = null, long? fileSize = null, string? errorMessage = null);
}