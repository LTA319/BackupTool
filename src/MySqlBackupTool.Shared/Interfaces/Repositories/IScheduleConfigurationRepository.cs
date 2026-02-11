using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 调度配置数据访问的存储库接口 / Repository interface for schedule configuration data access
/// 提供调度配置的存储、查询、更新和管理功能，支持备份任务的定时执行
/// Provides storage, querying, updating and management functionality for schedule configurations, supporting timed execution of backup tasks
/// </summary>
public interface IScheduleConfigurationRepository : IRepository<ScheduleConfiguration>
{
    /// <summary>
    /// 获取特定备份配置的所有调度配置 / Gets all schedule configurations for a specific backup configuration
    /// 返回与指定备份配置关联的所有调度设置
    /// Returns all schedule settings associated with specified backup configuration
    /// </summary>
    /// <param name="backupConfigId">备份配置ID / Backup configuration ID</param>
    /// <returns>调度配置列表 / List of schedule configurations</returns>
    Task<List<ScheduleConfiguration>> GetByBackupConfigIdAsync(int backupConfigId);

    /// <summary>
    /// 获取所有已启用的调度配置 / Gets all enabled schedule configurations
    /// 返回当前处于启用状态的所有调度配置
    /// Returns all schedule configurations currently in enabled state
    /// </summary>
    /// <returns>已启用的调度配置列表 / List of enabled schedule configurations</returns>
    Task<List<ScheduleConfiguration>> GetEnabledSchedulesAsync();

    /// <summary>
    /// 获取到期需要执行的调度配置 / Gets schedule configurations that are due for execution
    /// 根据当前时间查找需要执行的调度任务
    /// Finds scheduled tasks that need to be executed based on current time
    /// </summary>
    /// <param name="currentTime">用于检查的当前时间 / Current time to check against</param>
    /// <returns>到期执行的调度配置列表 / List of schedule configurations due for execution</returns>
    Task<List<ScheduleConfiguration>> GetDueSchedulesAsync(DateTime currentTime);

    /// <summary>
    /// 更新调度的最后执行时间 / Updates the last executed time for a schedule
    /// 记录调度任务的最近一次执行时间
    /// Records the most recent execution time of scheduled task
    /// </summary>
    /// <param name="scheduleId">调度ID / Schedule ID</param>
    /// <param name="executedTime">执行时间 / Execution time</param>
    Task UpdateLastExecutedAsync(int scheduleId, DateTime executedTime);

    /// <summary>
    /// 更新调度的下次执行时间 / Updates the next execution time for a schedule
    /// 设置调度任务的下一次预定执行时间
    /// Sets the next scheduled execution time for the scheduled task
    /// </summary>
    /// <param name="scheduleId">调度ID / Schedule ID</param>
    /// <param name="nextExecutionTime">下次执行时间 / Next execution time</param>
    Task UpdateNextExecutionAsync(int scheduleId, DateTime? nextExecutionTime);

    /// <summary>
    /// 启用或禁用调度 / Enables or disables a schedule
    /// 设置指定调度的启用状态
    /// Sets the enabled state of specified schedule
    /// </summary>
    /// <param name="scheduleId">调度ID / Schedule ID</param>
    /// <param name="enabled">是否启用 / Whether to enable or disable</param>
    Task SetEnabledAsync(int scheduleId, bool enabled);
}