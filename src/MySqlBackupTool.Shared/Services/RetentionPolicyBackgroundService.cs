using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Background service that automatically executes retention policies on a schedule
/// </summary>
public class RetentionPolicyBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RetentionPolicyBackgroundService> _logger;
    private readonly TimeSpan _executionInterval;

    public RetentionPolicyBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<RetentionPolicyBackgroundService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Default to running retention policies every 6 hours
        _executionInterval = TimeSpan.FromHours(6);
    }

    public RetentionPolicyBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<RetentionPolicyBackgroundService> logger,
        TimeSpan executionInterval)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _executionInterval = executionInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Retention Policy Background Service started with interval: {Interval}", _executionInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteRetentionPoliciesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while executing retention policies in background service");
            }

            try
            {
                await Task.Delay(_executionInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
        }

        _logger.LogInformation("Retention Policy Background Service stopped");
    }

    private async Task ExecuteRetentionPoliciesAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting scheduled retention policy execution");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var retentionService = scope.ServiceProvider.GetRequiredService<IRetentionPolicyService>();

            // Check if there are any enabled policies
            var enabledPolicies = await retentionService.GetEnabledRetentionPoliciesAsync();
            var policiesList = enabledPolicies.ToList();

            if (!policiesList.Any())
            {
                _logger.LogDebug("No enabled retention policies found, skipping execution");
                return;
            }

            _logger.LogInformation("Executing {PolicyCount} enabled retention policies", policiesList.Count);

            var result = await retentionService.ExecuteRetentionPoliciesAsync();

            if (result.Success)
            {
                _logger.LogInformation("Retention policies executed successfully: {Summary}", result.GetSummary());
            }
            else
            {
                _logger.LogWarning("Retention policies executed with errors: {ErrorCount} errors occurred", result.Errors.Count);
                foreach (var error in result.Errors)
                {
                    _logger.LogWarning("Retention policy error: {Error}", error);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during scheduled retention policy execution");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retention Policy Background Service is stopping");
        await base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Configuration options for the retention policy background service
/// </summary>
public class RetentionPolicyBackgroundServiceOptions
{
    /// <summary>
    /// Interval between retention policy executions
    /// </summary>
    public TimeSpan ExecutionInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>
    /// Whether the background service is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Time of day to run the first execution (24-hour format)
    /// </summary>
    public TimeSpan? PreferredExecutionTime { get; set; }
}