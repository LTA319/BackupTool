using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 管理后台备份操作的接口
/// Interface for managing background backup operations
/// </summary>
public interface IBackgroundTaskManager
{
    /// <summary>
    /// 在后台启动备份操作
    /// Starts a backup operation in the background
    /// </summary>
    /// <param name="configuration">备份配置 / Backup configuration</param>
    /// <param name="progress">进度报告器 / Progress reporter</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>表示备份操作的任务 / Task representing the backup operation</returns>
    Task<BackupResult> StartBackupAsync(BackupConfiguration configuration, IProgress<BackupProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消正在运行的备份操作
    /// Cancels a running backup operation
    /// </summary>
    /// <param name="operationId">要取消的操作ID / ID of the operation to cancel</param>
    /// <returns>如果取消成功返回true / True if cancellation was successful</returns>
    Task<bool> CancelBackupAsync(Guid operationId);

    /// <summary>
    /// 获取备份操作的状态
    /// Gets the status of a backup operation
    /// </summary>
    /// <param name="operationId">操作ID / ID of the operation</param>
    /// <returns>当前备份进度，如果未找到则返回null / Current backup progress or null if not found</returns>
    Task<BackupProgress?> GetBackupStatusAsync(Guid operationId);

    /// <summary>
    /// 获取所有当前正在运行的备份操作
    /// Gets all currently running backup operations
    /// </summary>
    /// <returns>正在运行的备份操作列表 / List of running backup operations</returns>
    Task<IEnumerable<BackupProgress>> GetRunningBackupsAsync();

    /// <summary>
    /// 备份进度更新时引发的事件
    /// Event raised when backup progress is updated
    /// </summary>
    event EventHandler<BackupProgressEventArgs>? ProgressUpdated;

    /// <summary>
    /// 备份操作完成时引发的事件
    /// Event raised when a backup operation completes
    /// </summary>
    event EventHandler<BackupCompletedEventArgs>? BackupCompleted;
}