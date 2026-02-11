using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 处理系统启动时自动启动备份操作的服务 / Service that handles automatic startup of backup operations when the system boots
/// 在系统启动后延迟一段时间，然后检查并执行需要在启动时运行的备份任务
/// Delays after system startup, then checks and executes backup tasks that should run on startup
/// </summary>
public class AutoStartupService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutoStartupService> _logger;
    //private readonly TimeSpan _startupDelay = TimeSpan.FromMinutes(2); // 启动后等待2分钟 / Wait 2 minutes after startup
    private readonly TimeSpan _startupDelay = TimeSpan.FromSeconds(10); // 启动后等待10秒（用于测试） / Wait 10 seconds after startup (for testing)

    /// <summary>
    /// 初始化自动启动服务 / Initializes auto-startup service
    /// </summary>
    /// <param name="serviceProvider">服务提供者用于获取依赖服务 / Service provider for obtaining dependent services</param>
    /// <param name="logger">日志记录器 / Logger instance</param>
    public AutoStartupService(
        IServiceProvider serviceProvider,
        ILogger<AutoStartupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// 启动自动启动服务 / Starts the auto-startup service
    /// 安排初始化任务在后台运行，不阻塞应用程序启动
    /// Schedules initialization task to run in background without blocking application startup
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Auto-startup service starting");

        // 在后台启动初始化，不阻塞 / Start the initialization in the background without blocking
        _ = Task.Run(async () =>
        {
            try
            {
                // 延迟启动以允许系统稳定 / Delay startup to allow system to stabilize
                await Task.Delay(_startupDelay, cancellationToken);
                await InitializeAutoStartupBackupsAsync(cancellationToken);
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("Auto-startup service cancelled during startup delay");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during auto-startup initialization");
            }
        }, cancellationToken);

        // 立即返回以不阻塞应用程序启动 / Return immediately to not block application startup
        _logger.LogInformation("Auto-startup service initialization scheduled");
    }

    /// <summary>
    /// 停止自动启动服务 / Stops the auto-startup service
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Auto-startup service stopping");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 通过检查应在启动时运行的配置来初始化自动启动备份 / Initializes auto-startup backups by checking for configurations that should run on startup
    /// 获取活动的备份配置，检查调度设置，执行需要的备份任务
    /// Gets active backup configurations, checks schedule settings, executes needed backup tasks
    /// </summary>
    private async Task InitializeAutoStartupBackupsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var backupConfigRepository = scope.ServiceProvider.GetService<IBackupConfigurationRepository>();
            var scheduleRepository = scope.ServiceProvider.GetService<IScheduleConfigurationRepository>();
            var orchestrator = scope.ServiceProvider.GetService<IBackupOrchestrator>();

            if (backupConfigRepository == null || scheduleRepository == null || orchestrator == null)
            {
                _logger.LogWarning("Required services not available for auto-startup initialization. This is normal for client applications.");
                return;
            }

            _logger.LogInformation("Checking for auto-startup backup configurations");

            // 获取所有活动的备份配置 / Get all active backup configurations
            var activeConfigs = (await backupConfigRepository.GetActiveConfigurationsAsync()).ToList();
            _logger.LogInformation("Found {Count} active backup configurations", activeConfigs.Count);

            // 检查每个配置的自动启动调度 / Check each configuration for auto-startup schedules
            foreach (var config in activeConfigs)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await ProcessAutoStartupForConfigAsync(config, scheduleRepository, orchestrator, cancellationToken);
            }

            // 更新所有启用调度的下次执行时间 / Update next execution times for all enabled schedules
            await UpdateAllScheduleExecutionTimesAsync(scheduleRepository);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing auto-startup backup configurations");
        }
    }

    /// <summary>
    /// 处理特定备份配置的自动启动逻辑 / Processes auto-startup logic for a specific backup configuration
    /// 检查配置的调度设置，确定是否需要在启动时执行备份
    /// Checks configuration's schedule settings, determines if backup should be executed on startup
    /// </summary>
    private async Task ProcessAutoStartupForConfigAsync(
        BackupConfiguration config,
        IScheduleConfigurationRepository scheduleRepository,
        IBackupOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        try
        {
            var schedules = await scheduleRepository.GetByBackupConfigIdAsync(config.Id);
            var enabledSchedules = schedules.Where(s => s.IsEnabled).ToList();

            if (enabledSchedules.Count == 0)
            {
                _logger.LogDebug("No enabled schedules found for backup configuration '{ConfigName}'", config.Name);
                return;
            }

            _logger.LogInformation("Processing {Count} enabled schedules for backup configuration '{ConfigName}'", 
                enabledSchedules.Count, config.Name);

            // 检查是否有任何调度表明此备份应在启动时运行 / Check if any schedule indicates this backup should run on startup
            var shouldRunOnStartup = await ShouldRunBackupOnStartupAsync(enabledSchedules);

            if (shouldRunOnStartup)
            {
                _logger.LogInformation("Executing auto-startup backup for configuration '{ConfigName}'", config.Name);

                try
                {
                    var result = await orchestrator.ExecuteBackupAsync(config, cancellationToken: cancellationToken);
                    
                    if (result.Success)
                    {
                        _logger.LogInformation("Successfully completed auto-startup backup for configuration '{ConfigName}'", config.Name);
                        
                        // 更新此配置所有调度的最后执行时间 / Update last executed time for all schedules of this configuration
                        var currentTime = DateTime.Now;
                        foreach (var schedule in enabledSchedules)
                        {
                            await scheduleRepository.UpdateLastExecutedAsync(schedule.Id, currentTime);
                        }
                    }
                    else
                    {
                        _logger.LogError("Auto-startup backup failed for configuration '{ConfigName}': {Error}", 
                            config.Name, result.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing auto-startup backup for configuration '{ConfigName}'", config.Name);
                }
            }
            else
            {
                _logger.LogDebug("Auto-startup backup not required for configuration '{ConfigName}'", config.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing auto-startup for backup configuration '{ConfigName}'", config.Name);
        }
    }

    /// <summary>
    /// 根据调度确定备份是否应在启动时运行 / Determines if a backup should run on startup based on its schedules
    /// 检查调度是否被错过（应该运行但没有运行）
    /// Checks if schedules were missed (should have run but didn't)
    /// </summary>
    private async Task<bool> ShouldRunBackupOnStartupAsync(List<ScheduleConfiguration> schedules)
    {
        var currentTime = DateTime.Now;
        
        foreach (var schedule in schedules)
        {
            // 检查调度是否被错过（应该运行但没有运行） / Check if the schedule was missed (should have run but didn't)
            if (schedule.NextExecution.HasValue && schedule.NextExecution.Value < currentTime)
            {
                // 检查自上次执行以来是否已超过调度间隔 / Check if it's been more than the schedule interval since last execution
                var timeSinceLastExecution = schedule.LastExecuted.HasValue 
                    ? currentTime - schedule.LastExecuted.Value 
                    : TimeSpan.MaxValue;

                var shouldRun = schedule.ScheduleType switch
                {
                    ScheduleType.Daily => timeSinceLastExecution > TimeSpan.FromHours(20), // 允许一些灵活性 / Allow some flexibility
                    ScheduleType.Weekly => timeSinceLastExecution > TimeSpan.FromDays(6),
                    ScheduleType.Monthly => timeSinceLastExecution > TimeSpan.FromDays(25),
                    _ => false
                };

                if (shouldRun)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 更新所有启用调度的下次执行时间 / Updates next execution times for all enabled schedules
    /// 重新计算并更新所有启用调度的下次执行时间
    /// Recalculates and updates next execution times for all enabled schedules
    /// </summary>
    private async Task UpdateAllScheduleExecutionTimesAsync(IScheduleConfigurationRepository scheduleRepository)
    {
        try
        {
            var enabledSchedules = await scheduleRepository.GetEnabledSchedulesAsync();
            
            foreach (var schedule in enabledSchedules)
            {
                var nextExecution = schedule.CalculateNextExecution();
                if (nextExecution != schedule.NextExecution)
                {
                    await scheduleRepository.UpdateNextExecutionAsync(schedule.Id, nextExecution);
                }
            }

            _logger.LogInformation("Updated next execution times for {Count} enabled schedules", enabledSchedules.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating schedule execution times");
        }
    }
}