using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Background service that manages backup scheduling and execution
/// </summary>
public class BackupSchedulerService : BackgroundService, IBackupScheduler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackupSchedulerService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1); // Check every minute
    private bool _isRunning = false;

    public BackupSchedulerService(
        IServiceProvider serviceProvider,
        ILogger<BackupSchedulerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Helper method to get required service with proper error handling
    /// </summary>
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
    /// Background service execution loop
    /// </summary>
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
                // Expected when cancellation is requested
                break;
            }
        }

        _isRunning = false;
        _logger.LogInformation("Backup scheduler service stopped");
    }

    /// <summary>
    /// Starts the backup scheduler service
    /// </summary>
    public new async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting backup scheduler service");
        await base.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Stops the backup scheduler service
    /// </summary>
    public new async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping backup scheduler service");
        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Adds or updates a schedule configuration
    /// </summary>
    public async Task<ScheduleConfiguration> AddOrUpdateScheduleAsync(ScheduleConfiguration scheduleConfig)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = GetRequiredService<IScheduleConfigurationRepository>(scope.ServiceProvider);

        // Validate the schedule configuration
        var (isValid, errors) = await ValidateScheduleAsync(scheduleConfig);
        if (!isValid)
        {
            throw new ArgumentException($"Invalid schedule configuration: {string.Join(", ", errors)}");
        }

        // Calculate next execution time
        scheduleConfig.NextExecution = scheduleConfig.CalculateNextExecution();

        ScheduleConfiguration savedConfig;
        if (scheduleConfig.Id == 0)
        {
            // Add new schedule
            savedConfig = await repository.AddAsync(scheduleConfig);
            _logger.LogInformation("Added new schedule configuration with ID {ScheduleId} for backup config {BackupConfigId}", 
                savedConfig.Id, savedConfig.BackupConfigId);
        }
        else
        {
            // Update existing schedule
            savedConfig = await repository.UpdateAsync(scheduleConfig);
            _logger.LogInformation("Updated schedule configuration with ID {ScheduleId}", savedConfig.Id);
        }

        return savedConfig;
    }

    /// <summary>
    /// Removes a schedule configuration
    /// </summary>
    public async Task RemoveScheduleAsync(int scheduleId)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = GetRequiredService<IScheduleConfigurationRepository>(scope.ServiceProvider);

        await repository.DeleteAsync(scheduleId);
        _logger.LogInformation("Removed schedule configuration with ID {ScheduleId}", scheduleId);
    }

    /// <summary>
    /// Gets all schedule configurations
    /// </summary>
    public async Task<IEnumerable<ScheduleConfiguration>> GetAllSchedulesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = GetRequiredService<IScheduleConfigurationRepository>(scope.ServiceProvider);

        return await repository.GetAllAsync();
    }

    /// <summary>
    /// Gets schedule configurations for a specific backup configuration
    /// </summary>
    public async Task<List<ScheduleConfiguration>> GetSchedulesForBackupConfigAsync(int backupConfigId)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = GetRequiredService<IScheduleConfigurationRepository>(scope.ServiceProvider);

        return await repository.GetByBackupConfigIdAsync(backupConfigId);
    }

    /// <summary>
    /// Enables or disables a schedule
    /// </summary>
    public async Task SetScheduleEnabledAsync(int scheduleId, bool enabled)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = GetRequiredService<IScheduleConfigurationRepository>(scope.ServiceProvider);

        await repository.SetEnabledAsync(scheduleId, enabled);
        _logger.LogInformation("Set schedule {ScheduleId} enabled status to {Enabled}", scheduleId, enabled);
    }

    /// <summary>
    /// Gets the next scheduled backup time across all schedules
    /// </summary>
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
    /// Manually triggers a scheduled backup
    /// </summary>
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
            // Execute the backup
            var result = await orchestrator.ExecuteBackupAsync(schedule.BackupConfiguration);
            
            if (result.Success)
            {
                // Update last executed time and calculate next execution
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
    /// Validates a schedule configuration
    /// </summary>
    public async Task<(bool IsValid, List<string> Errors)> ValidateScheduleAsync(ScheduleConfiguration scheduleConfig)
    {
        var errors = new List<string>();

        // Validate using data annotations
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(scheduleConfig);
        
        if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(scheduleConfig, validationContext, validationResults, true))
        {
            errors.AddRange(validationResults.Select(vr => vr.ErrorMessage ?? "Unknown validation error"));
        }

        // Validate that the backup configuration exists
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

        // Validate that next execution time can be calculated
        var nextExecution = scheduleConfig.CalculateNextExecution();
        if (scheduleConfig.IsEnabled && !nextExecution.HasValue)
        {
            errors.Add("Unable to calculate next execution time from the provided schedule configuration");
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// Checks for due backups and executes them
    /// </summary>
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

                // Execute the backup
                var result = await orchestrator.ExecuteBackupAsync(schedule.BackupConfiguration!);
                
                if (result.Success)
                {
                    // Update last executed time and calculate next execution
                    await repository.UpdateLastExecutedAsync(schedule.Id, currentTime);
                    _logger.LogInformation("Successfully executed scheduled backup for schedule {ScheduleId}", schedule.Id);
                }
                else
                {
                    _logger.LogError("Scheduled backup failed for schedule {ScheduleId}: {Error}", schedule.Id, result.ErrorMessage);
                    
                    // Still update next execution time even if backup failed
                    var nextExecution = schedule.CalculateNextExecution();
                    await repository.UpdateNextExecutionAsync(schedule.Id, nextExecution);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing scheduled backup for schedule {ScheduleId}", schedule.Id);
                
                // Update next execution time even if there was an error
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