using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Interface for orchestrating the complete backup workflow
/// </summary>
public interface IBackupOrchestrator
{
    /// <summary>
    /// Executes the complete backup workflow
    /// </summary>
    /// <param name="configuration">Backup configuration</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Backup result</returns>
    Task<BackupResult> ExecuteBackupAsync(BackupConfiguration configuration, IProgress<BackupProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a backup configuration before execution
    /// </summary>
    /// <param name="configuration">Configuration to validate</param>
    /// <returns>Validation result</returns>
    Task<BackupValidationResult> ValidateConfigurationAsync(BackupConfiguration configuration);
}