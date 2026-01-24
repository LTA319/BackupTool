using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Repository interface for schedule configuration data access
/// </summary>
public interface IScheduleConfigurationRepository : IRepository<ScheduleConfiguration>
{
    /// <summary>
    /// Gets all schedule configurations for a specific backup configuration
    /// </summary>
    /// <param name="backupConfigId">Backup configuration ID</param>
    /// <returns>List of schedule configurations</returns>
    Task<List<ScheduleConfiguration>> GetByBackupConfigIdAsync(int backupConfigId);

    /// <summary>
    /// Gets all enabled schedule configurations
    /// </summary>
    /// <returns>List of enabled schedule configurations</returns>
    Task<List<ScheduleConfiguration>> GetEnabledSchedulesAsync();

    /// <summary>
    /// Gets schedule configurations that are due for execution
    /// </summary>
    /// <param name="currentTime">Current time to check against</param>
    /// <returns>List of schedule configurations due for execution</returns>
    Task<List<ScheduleConfiguration>> GetDueSchedulesAsync(DateTime currentTime);

    /// <summary>
    /// Updates the last executed time for a schedule
    /// </summary>
    /// <param name="scheduleId">Schedule ID</param>
    /// <param name="executedTime">Execution time</param>
    Task UpdateLastExecutedAsync(int scheduleId, DateTime executedTime);

    /// <summary>
    /// Updates the next execution time for a schedule
    /// </summary>
    /// <param name="scheduleId">Schedule ID</param>
    /// <param name="nextExecutionTime">Next execution time</param>
    Task UpdateNextExecutionAsync(int scheduleId, DateTime? nextExecutionTime);

    /// <summary>
    /// Enables or disables a schedule
    /// </summary>
    /// <param name="scheduleId">Schedule ID</param>
    /// <param name="enabled">Whether to enable or disable</param>
    Task SetEnabledAsync(int scheduleId, bool enabled);
}