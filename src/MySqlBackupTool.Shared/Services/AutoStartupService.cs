using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Service that handles automatic startup of backup operations when the system boots
/// </summary>
public class AutoStartupService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutoStartupService> _logger;
    private readonly TimeSpan _startupDelay = TimeSpan.FromMinutes(2); // Wait 2 minutes after startup

    public AutoStartupService(
        IServiceProvider serviceProvider,
        ILogger<AutoStartupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Starts the auto-startup service
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Auto-startup service starting");

        // Start the initialization in the background without blocking
        _ = Task.Run(async () =>
        {
            try
            {
                // Delay startup to allow system to stabilize
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

        // Return immediately to not block application startup
        _logger.LogInformation("Auto-startup service initialization scheduled");
    }

    /// <summary>
    /// Stops the auto-startup service
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Auto-startup service stopping");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Initializes auto-startup backups by checking for configurations that should run on startup
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

            // Get all active backup configurations
            var activeConfigs = (await backupConfigRepository.GetActiveConfigurationsAsync()).ToList();
            _logger.LogInformation("Found {Count} active backup configurations", activeConfigs.Count);

            // Check each configuration for auto-startup schedules
            foreach (var config in activeConfigs)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await ProcessAutoStartupForConfigAsync(config, scheduleRepository, orchestrator, cancellationToken);
            }

            // Update next execution times for all enabled schedules
            await UpdateAllScheduleExecutionTimesAsync(scheduleRepository);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing auto-startup backup configurations");
        }
    }

    /// <summary>
    /// Processes auto-startup logic for a specific backup configuration
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

            // Check if any schedule indicates this backup should run on startup
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
                        
                        // Update last executed time for all schedules of this configuration
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
    /// Determines if a backup should run on startup based on its schedules
    /// </summary>
    private async Task<bool> ShouldRunBackupOnStartupAsync(List<ScheduleConfiguration> schedules)
    {
        var currentTime = DateTime.Now;
        
        foreach (var schedule in schedules)
        {
            // Check if the schedule was missed (should have run but didn't)
            if (schedule.NextExecution.HasValue && schedule.NextExecution.Value < currentTime)
            {
                // Check if it's been more than the schedule interval since last execution
                var timeSinceLastExecution = schedule.LastExecuted.HasValue 
                    ? currentTime - schedule.LastExecuted.Value 
                    : TimeSpan.MaxValue;

                var shouldRun = schedule.ScheduleType switch
                {
                    ScheduleType.Daily => timeSinceLastExecution > TimeSpan.FromHours(20), // Allow some flexibility
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
    /// Updates next execution times for all enabled schedules
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