using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Interface for managing background backup operations
/// </summary>
public interface IBackgroundTaskManager
{
    /// <summary>
    /// Starts a backup operation in the background
    /// </summary>
    /// <param name="configuration">Backup configuration</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the backup operation</returns>
    Task<BackupResult> StartBackupAsync(BackupConfiguration configuration, IProgress<BackupProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a running backup operation
    /// </summary>
    /// <param name="operationId">ID of the operation to cancel</param>
    /// <returns>True if cancellation was successful</returns>
    Task<bool> CancelBackupAsync(Guid operationId);

    /// <summary>
    /// Gets the status of a backup operation
    /// </summary>
    /// <param name="operationId">ID of the operation</param>
    /// <returns>Current backup progress or null if not found</returns>
    Task<BackupProgress?> GetBackupStatusAsync(Guid operationId);

    /// <summary>
    /// Gets all currently running backup operations
    /// </summary>
    /// <returns>List of running backup operations</returns>
    Task<IEnumerable<BackupProgress>> GetRunningBackupsAsync();

    /// <summary>
    /// Event raised when backup progress is updated
    /// </summary>
    event EventHandler<BackupProgressEventArgs>? ProgressUpdated;

    /// <summary>
    /// Event raised when a backup operation completes
    /// </summary>
    event EventHandler<BackupCompletedEventArgs>? BackupCompleted;
}