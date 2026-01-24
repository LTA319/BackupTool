using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Interface for managing backup scheduling
/// </summary>
public interface IBackupScheduler
{
    /// <summary>
    /// Starts the backup scheduler service
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the backup scheduler service
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates a schedule configuration
    /// </summary>
    /// <param name="scheduleConfig">Schedule configuration to add or update</param>
    /// <returns>The saved schedule configuration</returns>
    Task<ScheduleConfiguration> AddOrUpdateScheduleAsync(ScheduleConfiguration scheduleConfig);

    /// <summary>
    /// Removes a schedule configuration
    /// </summary>
    /// <param name="scheduleId">ID of the schedule to remove</param>
    Task RemoveScheduleAsync(int scheduleId);

    /// <summary>
    /// Gets all schedule configurations
    /// </summary>
    /// <returns>List of all schedule configurations</returns>
    Task<IEnumerable<ScheduleConfiguration>> GetAllSchedulesAsync();

    /// <summary>
    /// Gets schedule configurations for a specific backup configuration
    /// </summary>
    /// <param name="backupConfigId">Backup configuration ID</param>
    /// <returns>List of schedule configurations</returns>
    Task<List<ScheduleConfiguration>> GetSchedulesForBackupConfigAsync(int backupConfigId);

    /// <summary>
    /// Enables or disables a schedule
    /// </summary>
    /// <param name="scheduleId">Schedule ID</param>
    /// <param name="enabled">Whether to enable or disable the schedule</param>
    Task SetScheduleEnabledAsync(int scheduleId, bool enabled);

    /// <summary>
    /// Gets the next scheduled backup time across all schedules
    /// </summary>
    /// <returns>The next scheduled backup time, or null if no schedules are enabled</returns>
    Task<DateTime?> GetNextScheduledTimeAsync();

    /// <summary>
    /// Manually triggers a scheduled backup
    /// </summary>
    /// <param name="scheduleId">Schedule ID to trigger</param>
    Task TriggerScheduledBackupAsync(int scheduleId);

    /// <summary>
    /// Validates a schedule configuration
    /// </summary>
    /// <param name="scheduleConfig">Schedule configuration to validate</param>
    /// <returns>Validation result</returns>
    Task<(bool IsValid, List<string> Errors)> ValidateScheduleAsync(ScheduleConfiguration scheduleConfig);
}