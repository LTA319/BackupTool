using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 管理备份调度和执行的后台服务 / Background service that manages backup scheduling and execution
/// </summary>
public class BackupSchedulerService : BackgroundService, IBackupScheduler
{
    /// <summary>
    /// 服务提供者 / Service provider
    /// </summary>
    private readonly IServiceProvider _serviceProvider;
    
    /// <summary>
    /// 日志记录器 / Logger
    /// </summary>
    private readonly ILogger<BackupSchedulerService> _logger;
    
    /// <summary>
    /// 检查间隔时间，每分钟检查一次 / Check interval, check every minute
    /// </summary>
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
    
    /// <summary>
    /// 服务是否正在运行 / Whether the service is running
    /// </summary>
    private bool _isRunning = false;

    /// <summary>
    /// 初始化备份调度服务 / Initializes the backup scheduler service
    /// </summary>
    /// <param name="serviceProvider">服务提供者 / Service provider</param>
    /// <param name="logger">日志记录器 / Logger</param>
    public BackupSchedulerService(
        IServiceProvider serviceProvider,
        ILogger<BackupSchedulerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// 获取所需服务的辅助方法，具有适当的错误处理 / Helper method to get required service with proper error handling
    /// </summary>
    /// <typeparam name="T">服务类型 / Service type</typeparam>
    /// <param name="serviceProvider">服务提供者 / Service provider</param>
    /// <returns>服务实例 / Service instance</returns>
    /// <exception cref="InvalidOperationException">当服务未注册时抛出 / Thrown when service is not registered</exception>
    private T GetRequiredService<T>(IServiceProvider serviceProvider) where T : class
    {
        var service = serviceProvider.GetService<T>();
        if (service == null)
        {
            throw new InvalidOperationException($"Required service {typeof(T).Name} is not registered");
        }
        return service;
    }

    /// <summary>
    /// 后台服务执行循环 / Background service execution loop
    /// </summary>
    /// <param name="stoppingToken">停止令牌 / Stopping token</param>
    /// <returns>任务 / Task</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Backup scheduler service started");
        _isRunning = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndExecuteDueBackupsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while checking for due backups");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // 请求取消时的预期异常 / Expected when cancellation is requested
                break;
            }
        }

        _isRunning = false;
        _logger.LogInformation("Backup scheduler service stopped");
    }

    /// <summary>
    /// 启动备份调度服务 / Starts the backup scheduler service
    /// </summary>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>任务 / Task</returns>
    public new async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting backup scheduler service");
        await base.StartAsync(cancellationToken);
    }

    /// <summary>
    /// 停止备份调度服务 / Stops the backup scheduler service
    /// </summary>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>任务 / Task</returns>
    public new async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping backup scheduler service");
        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// 添加或更新调度配置 / Adds or updates a schedule configuration
    /// </summary>
    /// <param name="scheduleConfig">调度配置 / Schedule configuration</param>
    /// <returns>保存的调度配置 / Saved schedule configuration</returns>
    /// <exception cref="ArgumentException">当调度配置无效时抛出 / Thrown when schedule configuration is invalid</exception>
    public async Task<ScheduleConfiguration> AddOrUpdateScheduleAsync(ScheduleConfiguration scheduleConfig)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = GetRequiredService<IScheduleConfigurationRepository>(scope.ServiceProvider);

        // 验证调度配置 / Validate the schedule configuration
        var (isValid, errors) = await ValidateScheduleAsync(scheduleConfig);
        if (!isValid)
        {
            throw new ArgumentException($"Invalid schedule configuration: {string.Join(", ", errors)}");
        }

        // 计算下次执行时间 / Calculate next execution time
        scheduleConfig.NextExecution = scheduleConfig.CalculateNextExecution();

        ScheduleConfiguration savedConfig;
        if (scheduleConfig.Id == 0)
        {
            // 添加新调度 / Add new schedule
            savedConfig = await repository.AddAsync(scheduleConfig);
            _logger.LogInformation("Added new schedule configuration with ID {ScheduleId} for backup config {BackupConfigId}", 
                savedConfig.Id, savedConfig.BackupConfigId);
        }
        else
        {
            // 更新现有调度 / Update existing schedule
            savedConfig = await repository.UpdateAsync(scheduleConfig);
            _logger.LogInformation("Updated schedule configuration with ID {ScheduleId}", savedConfig.Id);
        }

        return savedConfig;
    }

    /// <summary>
    /// 移除调度配置 / Removes a schedule configuration
    /// </summary>
    /// <param name="scheduleId">调度ID / Schedule ID</param>
    /// <returns>任务 / Task</returns>
    public async Task RemoveScheduleAsync(int scheduleId)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = GetRequiredService<IScheduleConfigurationRepository>(scope.ServiceProvider);

        await repository.DeleteAsync(scheduleId);
        _logger.LogInformation("Removed schedule configuration with ID {ScheduleId}", scheduleId);
    }

    /// <summary>
    /// 获取所有调度配置 / Gets all schedule configurations
    /// </summary>
    /// <returns>调度配置列表 / List of schedule configurations</returns>
    public async Task<IEnumerable<ScheduleConfiguration>> GetAllSchedulesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = GetRequiredService<IScheduleConfigurationRepository>(scope.ServiceProvider);

        return await repository.GetAllAsync();
    }

    /// <summary>
    /// 获取特定备份配置的调度配置 / Gets schedule configurations for a specific backup configuration
    /// </summary>
    /// <param name="backupConfigId">备份配置ID / Backup configuration ID</param>
    /// <returns>调度配置列表 / List of schedule configurations</returns>
    public async Task<List<ScheduleConfiguration>> GetSchedulesForBackupConfigAsync(int backupConfigId)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = GetRequiredService<IScheduleConfigurationRepository>(scope.ServiceProvider);

        return await repository.GetByBackupConfigIdAsync(backupConfigId);
    }

    /// <summary>
    /// 启用或禁用调度 / Enables or disables a schedule
    /// </summary>
    /// <param name="scheduleId">调度ID / Schedule ID</param>
    /// <param name="enabled">是否启用 / Whether to enable</param>
    /// <returns>任务 / Task</returns>
    public async Task SetScheduleEnabledAsync(int scheduleId, bool enabled)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = GetRequiredService<IScheduleConfigurationRepository>(scope.ServiceProvider);

        await repository.SetEnabledAsync(scheduleId, enabled);
        _logger.LogInformation("Set schedule {ScheduleId} enabled status to {Enabled}", scheduleId, enabled);
    }

    /// <summary>
    /// 获取所有调度中的下一个计划备份时间 / Gets the next scheduled backup time across all schedules
    /// </summary>
    /// <returns>下一个计划时间，如果没有则返回null / Next scheduled time, or null if none</returns>
    public async Task<DateTime?> GetNextScheduledTimeAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = GetRequiredService<IScheduleConfigurationRepository>(scope.ServiceProvider);

        var enabledSchedules = await repository.GetEnabledSchedulesAsync();
        return enabledSchedules
            .Where(s => s.NextExecution.HasValue)
            .Min(s => s.NextExecution);
    }

    /// <summary>
    /// 手动触发计划备份 / Manually triggers a scheduled backup
    /// </summary>
    /// <param name="scheduleId">调度ID / Schedule ID</param>
    /// <returns>任务 / Task</returns>
    /// <exception cref="ArgumentException">当调度不存在或没有关联的备份配置时抛出 / Thrown when schedule is not found or has no associated backup configuration</exception>
    public async Task TriggerScheduledBackupAsync(int scheduleId)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = GetRequiredService<IScheduleConfigurationRepository>(scope.ServiceProvider);
        var orchestrator = GetRequiredService<IBackupOrchestrator>(scope.ServiceProvider);

        var schedule = await repository.GetByIdAsync(scheduleId);
        if (schedule?.BackupConfiguration == null)
        {
            throw new ArgumentException($"Schedule with ID {scheduleId} not found or has no associated backup configuration");
        }

        _logger.LogInformation("Manually triggering backup for schedule {ScheduleId}", scheduleId);

        try
        {
            // 执行备份 / Execute the backup
            var result = await orchestrator.ExecuteBackupAsync(schedule.BackupConfiguration);
            
            if (result.Success)
            {
                // 更新最后执行时间并计算下次执行时间 / Update last executed time and calculate next execution
                await repository.UpdateLastExecutedAsync(scheduleId, DateTime.Now);
                _logger.LogInformation("Successfully executed manual backup for schedule {ScheduleId}", scheduleId);
            }
            else
            {
                _logger.LogError("Manual backup failed for schedule {ScheduleId}: {Error}", scheduleId, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing manual backup for schedule {ScheduleId}", scheduleId);
            throw;
        }
    }

    /// <summary>
    /// 验证调度配置 / Validates a schedule configuration
    /// </summary>
    /// <param name="scheduleConfig">调度配置 / Schedule configuration</param>
    /// <returns>验证结果和错误列表 / Validation result and error list</returns>
    public async Task<(bool IsValid, List<string> Errors)> ValidateScheduleAsync(ScheduleConfiguration scheduleConfig)
    {
        var errors = new List<string>();

        // 使用数据注解验证 / Validate using data annotations
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(scheduleConfig);
        
        if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(scheduleConfig, validationContext, validationResults, true))
        {
            errors.AddRange(validationResults.Select(vr => vr.ErrorMessage ?? "Unknown validation error"));
        }

        // 验证备份配置是否存在 / Validate that the backup configuration exists
        if (scheduleConfig.BackupConfigId > 0)
        {
            using var scope = _serviceProvider.CreateScope();
            var backupConfigRepository = GetRequiredService<IBackupConfigurationRepository>(scope.ServiceProvider);
            var backupConfig = await backupConfigRepository.GetByIdAsync(scheduleConfig.BackupConfigId);
            
            if (backupConfig == null)
            {
                errors.Add($"Backup configuration with ID {scheduleConfig.BackupConfigId} does not exist");
            }
            else if (!backupConfig.IsActive)
            {
                errors.Add($"Backup configuration with ID {scheduleConfig.BackupConfigId} is not active");
            }
        }

        // 验证可以计算下次执行时间 / Validate that next execution time can be calculated
        var nextExecution = scheduleConfig.CalculateNextExecution();
        if (scheduleConfig.IsEnabled && !nextExecution.HasValue)
        {
            errors.Add("Unable to calculate next execution time from the provided schedule configuration");
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// 检查到期的备份并执行它们 / Checks for due backups and executes them
    /// </summary>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>任务 / Task</returns>
    private async Task CheckAndExecuteDueBackupsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = GetRequiredService<IScheduleConfigurationRepository>(scope.ServiceProvider);
        var orchestrator = GetRequiredService<IBackupOrchestrator>(scope.ServiceProvider);

        var currentTime = DateTime.Now;
        var dueSchedules = await repository.GetDueSchedulesAsync(currentTime);

        if (dueSchedules.Count > 0)
        {
            _logger.LogInformation("Found {Count} due backup schedules", dueSchedules.Count);
        }

        foreach (var schedule in dueSchedules)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                _logger.LogInformation("Executing scheduled backup for schedule {ScheduleId} (Config: {ConfigName})", 
                    schedule.Id, schedule.BackupConfiguration?.Name ?? "Unknown");

                // 执行备份 / Execute the backup
                var result = await orchestrator.ExecuteBackupAsync(schedule.BackupConfiguration!);
                
                if (result.Success)
                {
                    // 更新最后执行时间并计算下次执行时间 / Update last executed time and calculate next execution
                    await repository.UpdateLastExecutedAsync(schedule.Id, currentTime);
                    _logger.LogInformation("Successfully executed scheduled backup for schedule {ScheduleId}", schedule.Id);
                }
                else
                {
                    _logger.LogError("Scheduled backup failed for schedule {ScheduleId}: {Error}", schedule.Id, result.ErrorMessage);
                    
                    // 即使备份失败也要更新下次执行时间 / Still update next execution time even if backup failed
                    var nextExecution = schedule.CalculateNextExecution();
                    await repository.UpdateNextExecutionAsync(schedule.Id, nextExecution);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing scheduled backup for schedule {ScheduleId}", schedule.Id);
                
                // 即使出现错误也要更新下次执行时间 / Update next execution time even if there was an error
                try
                {
                    var nextExecution = schedule.CalculateNextExecution();
                    await repository.UpdateNextExecutionAsync(schedule.Id, nextExecution);
                }
                catch (Exception updateEx)
                {
                    _logger.LogError(updateEx, "Error updating next execution time for schedule {ScheduleId}", schedule.Id);
                }
            }
        }
    }
}