using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 网络重试服务，提供指数退避重试逻辑的网络操作处理 / Service for handling network operations with exponential backoff retry logic
/// 支持网络连接测试、重试机制和连接恢复等待功能 / Supports network connectivity testing, retry mechanisms and connectivity restoration waiting
/// </summary>
public class NetworkRetryService : INetworkRetryService
{
    private readonly ILogger<NetworkRetryService> _logger;
    private readonly NetworkRetryConfig _config;

    /// <summary>
    /// 初始化网络重试服务 / Initialize network retry service
    /// </summary>
    /// <param name="logger">日志记录器 / Logger instance</param>
    /// <param name="config">网络重试配置（可选） / Network retry configuration (optional)</param>
    /// <exception cref="ArgumentNullException">当logger为null时抛出 / Thrown when logger is null</exception>
    public NetworkRetryService(ILogger<NetworkRetryService> logger, NetworkRetryConfig? config = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? new NetworkRetryConfig();
    }

    /// <summary>
    /// 使用指数退避重试逻辑执行网络操作 / Executes a network operation with exponential backoff retry logic
    /// 支持可配置的重试次数、延迟时间和抖动机制 / Supports configurable retry attempts, delay times and jitter mechanism
    /// </summary>
    /// <typeparam name="T">操作返回类型 / Operation return type</typeparam>
    /// <param name="operation">要执行的网络操作 / Network operation to execute</param>
    /// <param name="operationName">操作名称用于日志记录 / Operation name for logging</param>
    /// <param name="operationId">操作ID用于跟踪 / Operation ID for tracking</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>操作结果 / Operation result</returns>
    /// <exception cref="NetworkRetryException">当所有重试都失败时抛出 / Thrown when all retries fail</exception>
    /// <exception cref="OperationCanceledException">当操作被取消时抛出 / Thrown when operation is cancelled</exception>
    public async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        Exception? lastException = null;

        _logger.LogInformation("Starting network operation {OperationName} (ID: {OperationId}) with retry policy: {MaxRetries} attempts, base delay {BaseDelay}ms",
            operationName, operationId, _config.MaxRetryAttempts, _config.BaseRetryDelay.TotalMilliseconds);

        for (int attempt = 1; attempt <= _config.MaxRetryAttempts; attempt++)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogDebug("Network operation {OperationName} attempt {Attempt}/{MaxAttempts} (ID: {OperationId})",
                    operationName, attempt, _config.MaxRetryAttempts, operationId);

                var result = await operation(cancellationToken);

                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation("Network operation {OperationName} succeeded on attempt {Attempt} after {Duration}ms (ID: {OperationId})",
                    operationName, attempt, duration.TotalMilliseconds, operationId);

                return result;
            }
            catch (Exception ex) when (IsRetriableException(ex))
            {
                lastException = ex;
                var duration = DateTime.UtcNow - startTime;

                _logger.LogWarning(ex, "Network operation {OperationName} attempt {Attempt}/{MaxAttempts} failed after {Duration}ms (ID: {OperationId}): {ErrorMessage}",
                    operationName, attempt, _config.MaxRetryAttempts, duration.TotalMilliseconds, operationId, ex.Message);

                // 最后一次尝试后不延迟 / Don't delay after the last attempt
                if (attempt < _config.MaxRetryAttempts)
                {
                    var delay = CalculateExponentialBackoffDelay(attempt);
                    _logger.LogDebug("Waiting {DelayMs}ms before retry attempt {NextAttempt} for operation {OperationName} (ID: {OperationId})",
                        delay.TotalMilliseconds, attempt + 1, operationName, operationId);

                    await Task.Delay(delay, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                // 不可重试的异常，立即失败 / Non-retriable exception, fail immediately
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Network operation {OperationName} failed with non-retriable error after {Duration}ms (ID: {OperationId}): {ErrorMessage}",
                    operationName, duration.TotalMilliseconds, operationId, ex.Message);
                throw;
            }
        }

        var totalDuration = DateTime.UtcNow - startTime;
        _logger.LogError(lastException, "Network operation {OperationName} failed after {MaxAttempts} attempts and {Duration}ms (ID: {OperationId})",
            operationName, _config.MaxRetryAttempts, totalDuration.TotalMilliseconds, operationId);

        throw new NetworkRetryException(operationId, operationName, _config.MaxRetryAttempts, totalDuration, lastException);
    }

    /// <summary>
    /// 使用指数退避重试逻辑执行网络操作（无返回值） / Executes a network operation with exponential backoff retry logic (void return)
    /// 适用于不需要返回值的网络操作 / Suitable for network operations that don't require return values
    /// </summary>
    /// <param name="operation">要执行的网络操作 / Network operation to execute</param>
    /// <param name="operationName">操作名称用于日志记录 / Operation name for logging</param>
    /// <param name="operationId">操作ID用于跟踪 / Operation ID for tracking</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <exception cref="NetworkRetryException">当所有重试都失败时抛出 / Thrown when all retries fail</exception>
    /// <exception cref="OperationCanceledException">当操作被取消时抛出 / Thrown when operation is cancelled</exception>
    public async Task ExecuteWithRetryAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWithRetryAsync(async (ct) =>
        {
            await operation(ct);
            return true; // 虚拟返回值 / Dummy return value
        }, operationName, operationId, cancellationToken);
    }

    /// <summary>
    /// 测试到指定端点的网络连接性 / Tests network connectivity to a specific endpoint
    /// 包括Ping测试和TCP连接测试 / Includes ping test and TCP connection test
    /// </summary>
    /// <param name="host">目标主机 / Target host</param>
    /// <param name="port">目标端口 / Target port</param>
    /// <param name="timeout">超时时间 / Timeout duration</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>网络连接性测试结果 / Network connectivity test result</returns>
    public async Task<NetworkConnectivityResult> TestConnectivityAsync(
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogDebug("Testing network connectivity to {Host}:{Port} with timeout {Timeout}ms (ID: {OperationId})",
                host, port, timeout.TotalMilliseconds, operationId);

            // 首先尝试ping主机 / First, try to ping the host
            var pingResult = await PingHostAsync(host, timeout, cancellationToken);
            
            // 然后尝试建立TCP连接 / Then, try to establish a TCP connection
            var tcpResult = await TestTcpConnectionAsync(host, port, timeout, cancellationToken);

            var duration = DateTime.UtcNow - startTime;
            var result = new NetworkConnectivityResult
            {
                Host = host,
                Port = port,
                IsReachable = pingResult.Success && tcpResult.Success,
                PingSuccessful = pingResult.Success,
                TcpConnectionSuccessful = tcpResult.Success,
                ResponseTime = duration,
                ErrorMessage = pingResult.ErrorMessage ?? tcpResult.ErrorMessage
            };

            _logger.LogInformation("Network connectivity test to {Host}:{Port} completed in {Duration}ms: Reachable={IsReachable}, Ping={PingSuccess}, TCP={TcpSuccess} (ID: {OperationId})",
                host, port, duration.TotalMilliseconds, result.IsReachable, result.PingSuccessful, result.TcpConnectionSuccessful, operationId);

            return result;
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Network connectivity test to {Host}:{Port} failed after {Duration}ms (ID: {OperationId}): {ErrorMessage}",
                host, port, duration.TotalMilliseconds, operationId, ex.Message);

            return new NetworkConnectivityResult
            {
                Host = host,
                Port = port,
                IsReachable = false,
                PingSuccessful = false,
                TcpConnectionSuccessful = false,
                ResponseTime = duration,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// 等待网络连接恢复 / Waits for network connectivity to be restored
    /// 定期检查连接状态直到恢复或超时 / Periodically checks connection status until restored or timeout
    /// </summary>
    /// <param name="host">目标主机 / Target host</param>
    /// <param name="port">目标端口 / Target port</param>
    /// <param name="maxWaitTime">最大等待时间 / Maximum wait time</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>连接恢复返回true，超时返回false / Returns true if connectivity restored, false if timeout</returns>
    public async Task<bool> WaitForConnectivityAsync(
        string host,
        int port,
        TimeSpan maxWaitTime,
        CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow;
        var checkInterval = TimeSpan.FromSeconds(5);

        _logger.LogInformation("Waiting for network connectivity to {Host}:{Port} for up to {MaxWaitTime} (ID: {OperationId})",
            host, port, maxWaitTime, operationId);

        while (DateTime.UtcNow - startTime < maxWaitTime)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var connectivityResult = await TestConnectivityAsync(host, port, TimeSpan.FromSeconds(10), cancellationToken);
            
            if (connectivityResult.IsReachable)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation("Network connectivity to {Host}:{Port} restored after {Duration}ms (ID: {OperationId})",
                    host, port, duration.TotalMilliseconds, operationId);
                return true;
            }

            _logger.LogDebug("Network connectivity to {Host}:{Port} not yet available, waiting {CheckInterval}ms before next check (ID: {OperationId})",
                host, port, checkInterval.TotalMilliseconds, operationId);

            await Task.Delay(checkInterval, cancellationToken);
        }

        var totalDuration = DateTime.UtcNow - startTime;
        _logger.LogWarning("Network connectivity to {Host}:{Port} was not restored within {MaxWaitTime} (waited {ActualDuration}ms) (ID: {OperationId})",
            host, port, maxWaitTime, totalDuration.TotalMilliseconds, operationId);

        return false;
    }

    #region Private Helper Methods

    /// <summary>
    /// 判断异常是否可重试 / Determines if an exception is retriable for network operations
    /// 根据异常类型和配置决定是否应该重试 / Decides whether to retry based on exception type and configuration
    /// </summary>
    /// <param name="ex">要检查的异常 / Exception to check</param>
    /// <returns>可重试返回true，否则返回false / Returns true if retriable, false otherwise</returns>
    private bool IsRetriableException(Exception ex)
    {
        return ex switch
        {
            SocketException socketEx => IsRetriableSocketException(socketEx),
            TimeoutException => true,
            HttpRequestException => true,
            NetworkRetryException => false, // 不重试我们自己的重试异常 / Don't retry our own retry exceptions
            OperationCanceledException => false, // 不重试取消操作 / Don't retry cancellation
            _ => _config.RetryOnUnknownExceptions
        };
    }

    /// <summary>
    /// 判断Socket异常是否可重试 / Determines if a socket exception is retriable
    /// 根据Socket错误代码判断是否应该重试 / Decides whether to retry based on socket error code
    /// </summary>
    /// <param name="ex">Socket异常 / Socket exception</param>
    /// <returns>可重试返回true，否则返回false / Returns true if retriable, false otherwise</returns>
    private bool IsRetriableSocketException(SocketException ex)
    {
        return ex.SocketErrorCode switch
        {
            SocketError.ConnectionRefused => true,    // 连接被拒绝 / Connection refused
            SocketError.ConnectionReset => true,      // 连接被重置 / Connection reset
            SocketError.ConnectionAborted => true,    // 连接被中止 / Connection aborted
            SocketError.TimedOut => true,             // 超时 / Timed out
            SocketError.NetworkUnreachable => true,  // 网络不可达 / Network unreachable
            SocketError.HostUnreachable => true,     // 主机不可达 / Host unreachable
            SocketError.HostDown => true,             // 主机关闭 / Host down
            SocketError.NetworkDown => true,          // 网络关闭 / Network down
            SocketError.TryAgain => true,             // 重试 / Try again
            _ => false
        };
    }

    /// <summary>
    /// 计算带抖动的指数退避延迟 / Calculates exponential backoff delay with jitter
    /// 使用指数增长延迟并添加随机抖动以防止雷群效应 / Uses exponential growth delay with random jitter to prevent thundering herd
    /// </summary>
    /// <param name="attemptNumber">尝试次数 / Attempt number</param>
    /// <returns>延迟时间 / Delay duration</returns>
    private TimeSpan CalculateExponentialBackoffDelay(int attemptNumber)
    {
        var exponentialDelay = TimeSpan.FromMilliseconds(
            _config.BaseRetryDelay.TotalMilliseconds * Math.Pow(2, attemptNumber - 1));

        // 限制在最大延迟时间内 / Cap at maximum delay
        if (exponentialDelay > _config.MaxRetryDelay)
        {
            exponentialDelay = _config.MaxRetryDelay;
        }

        // 添加抖动以防止雷群效应 / Add jitter to prevent thundering herd
        if (_config.EnableJitter)
        {
            var jitterRange = exponentialDelay.TotalMilliseconds * 0.1; // 10%抖动 / 10% jitter
            var random = new Random();
            var jitter = TimeSpan.FromMilliseconds(random.NextDouble() * jitterRange);
            exponentialDelay = exponentialDelay.Add(jitter);
        }

        return exponentialDelay;
    }

    /// <summary>
    /// Ping主机以测试基本网络连接性 / Pings a host to test basic network connectivity
    /// 使用ICMP协议测试主机是否可达 / Uses ICMP protocol to test if host is reachable
    /// </summary>
    /// <param name="host">目标主机 / Target host</param>
    /// <param name="timeout">超时时间 / Timeout duration</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>成功状态和错误消息 / Success status and error message</returns>
    private async Task<(bool Success, string? ErrorMessage)> PingHostAsync(
        string host,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, (int)timeout.TotalMilliseconds);
            
            if (reply.Status == IPStatus.Success)
            {
                return (true, null);
            }
            else
            {
                return (false, $"Ping failed: {reply.Status}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Ping error: {ex.Message}");
        }
    }

    /// <summary>
    /// 测试到指定主机和端口的TCP连接 / Tests TCP connection to a specific host and port
    /// 尝试建立TCP连接以验证服务可用性 / Attempts to establish TCP connection to verify service availability
    /// </summary>
    /// <param name="host">目标主机 / Target host</param>
    /// <param name="port">目标端口 / Target port</param>
    /// <param name="timeout">超时时间 / Timeout duration</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>成功状态和错误消息 / Success status and error message</returns>
    private async Task<(bool Success, string? ErrorMessage)> TestTcpConnectionAsync(
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            using var tcpClient = new TcpClient();
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await tcpClient.ConnectAsync(host, port, combinedCts.Token);
            
            if (tcpClient.Connected)
            {
                return (true, null);
            }
            else
            {
                return (false, "TCP connection failed");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return (false, "TCP connection test was cancelled");
        }
        catch (OperationCanceledException)
        {
            return (false, "TCP connection timed out");
        }
        catch (Exception ex)
        {
            return (false, $"TCP connection error: {ex.Message}");
        }
    }

    #endregion
}