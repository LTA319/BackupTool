using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 按计划自动执行保留策略的后台服务 / Background service that automatically executes retention policies on a schedule
/// 定期运行保留策略以清理过期的备份文件和日志 / Periodically runs retention policies to clean up expired backup files and logs
/// </summary>
public class RetentionPolicyBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RetentionPolicyBackgroundService> _logger;
    private readonly TimeSpan _executionInterval;

    /// <summary>
    /// 初始化保留策略后台服务（使用默认6小时间隔） / Initialize retention policy background service (with default 6-hour interval)
    /// </summary>
    /// <param name="serviceProvider">服务提供者 / Service provider</param>
    /// <param name="logger">日志记录器 / Logger instance</param>
    /// <exception cref="ArgumentNullException">当任何参数为null时抛出 / Thrown when any parameter is null</exception>
    public RetentionPolicyBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<RetentionPolicyBackgroundService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // 默认每6小时运行一次保留策略 / Default to running retention policies every 6 hours
        _executionInterval = TimeSpan.FromHours(6);
    }

    /// <summary>
    /// 初始化保留策略后台服务（使用自定义执行间隔） / Initialize retention policy background service (with custom execution interval)
    /// </summary>
    /// <param name="serviceProvider">服务提供者 / Service provider</param>
    /// <param name="logger">日志记录器 / Logger instance</param>
    /// <param name="executionInterval">执行间隔 / Execution interval</param>
    /// <exception cref="ArgumentNullException">当任何参数为null时抛出 / Thrown when any parameter is null</exception>
    public RetentionPolicyBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<RetentionPolicyBackgroundService> logger,
        TimeSpan executionInterval)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _executionInterval = executionInterval;
    }

    /// <summary>
    /// 执行后台服务的主要逻辑 / Executes the main logic of the background service
    /// 定期运行保留策略执行，直到服务被取消 / Periodically runs retention policy execution until service is cancelled
    /// </summary>
    /// <param name="stoppingToken">停止令牌 / Stopping token</param>
    /// <returns>异步任务 / Async task</returns>
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
                // 取消请求时的预期行为 / Expected when cancellation is requested
                break;
            }
        }

        _logger.LogInformation("Retention Policy Background Service stopped");
    }

    /// <summary>
    /// 执行保留策略的私有方法 / Private method to execute retention policies
    /// 创建服务作用域并调用保留策略服务 / Creates service scope and calls retention policy service
    /// </summary>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>异步任务 / Async task</returns>
    private async Task ExecuteRetentionPoliciesAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting scheduled retention policy execution");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var retentionService = scope.ServiceProvider.GetRequiredService<IRetentionPolicyService>();

            // 检查是否有启用的策略 / Check if there are any enabled policies
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

    /// <summary>
    /// 停止后台服务 / Stops the background service
    /// </summary>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>异步任务 / Async task</returns>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retention Policy Background Service is stopping");
        await base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// 保留策略后台服务的配置选项 / Configuration options for the retention policy background service
/// </summary>
public class RetentionPolicyBackgroundServiceOptions
{
    /// <summary>
    /// 保留策略执行之间的间隔 / Interval between retention policy executions
    /// </summary>
    public TimeSpan ExecutionInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>
    /// 后台服务是否启用 / Whether the background service is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 首次执行的首选时间（24小时格式） / Time of day to run the first execution (24-hour format)
    /// </summary>
    public TimeSpan? PreferredExecutionTime { get; set; }
}