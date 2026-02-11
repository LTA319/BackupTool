using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 管理备份调度的接口
/// Interface for managing backup scheduling
/// </summary>
public interface IBackupScheduler
{
    /// <summary>
    /// 启动备份调度器服务
    /// Starts the backup scheduler service
    /// </summary>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止备份调度器服务
    /// Stops the backup scheduler service
    /// </summary>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 添加或更新调度配置
    /// Adds or updates a schedule configuration
    /// </summary>
    /// <param name="scheduleConfig">要添加或更新的调度配置 / Schedule configuration to add or update</param>
    /// <returns>保存的调度配置 / The saved schedule configuration</returns>
    Task<ScheduleConfiguration> AddOrUpdateScheduleAsync(ScheduleConfiguration scheduleConfig);

    /// <summary>
    /// 移除调度配置
    /// Removes a schedule configuration
    /// </summary>
    /// <param name="scheduleId">要移除的调度ID / ID of the schedule to remove</param>
    Task RemoveScheduleAsync(int scheduleId);

    /// <summary>
    /// 获取所有调度配置
    /// Gets all schedule configurations
    /// </summary>
    /// <returns>所有调度配置的列表 / List of all schedule configurations</returns>
    Task<IEnumerable<ScheduleConfiguration>> GetAllSchedulesAsync();

    /// <summary>
    /// 获取特定备份配置的调度配置
    /// Gets schedule configurations for a specific backup configuration
    /// </summary>
    /// <param name="backupConfigId">备份配置ID / Backup configuration ID</param>
    /// <returns>调度配置列表 / List of schedule configurations</returns>
    Task<List<ScheduleConfiguration>> GetSchedulesForBackupConfigAsync(int backupConfigId);

    /// <summary>
    /// 启用或禁用调度
    /// Enables or disables a schedule
    /// </summary>
    /// <param name="scheduleId">调度ID / Schedule ID</param>
    /// <param name="enabled">是否启用调度 / Whether to enable or disable the schedule</param>
    Task SetScheduleEnabledAsync(int scheduleId, bool enabled);

    /// <summary>
    /// 获取所有调度中的下一个备份时间
    /// Gets the next scheduled backup time across all schedules
    /// </summary>
    /// <returns>下一个计划的备份时间，如果没有启用的调度则返回null / The next scheduled backup time, or null if no schedules are enabled</returns>
    Task<DateTime?> GetNextScheduledTimeAsync();

    /// <summary>
    /// 手动触发计划的备份
    /// Manually triggers a scheduled backup
    /// </summary>
    /// <param name="scheduleId">要触发的调度ID / Schedule ID to trigger</param>
    Task TriggerScheduledBackupAsync(int scheduleId);

    /// <summary>
    /// 验证调度配置
    /// Validates a schedule configuration
    /// </summary>
    /// <param name="scheduleConfig">要验证的调度配置 / Schedule configuration to validate</param>
    /// <returns>验证结果 / Validation result</returns>
    Task<(bool IsValid, List<string> Errors)> ValidateScheduleAsync(ScheduleConfiguration scheduleConfig);
}