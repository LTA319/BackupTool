using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Service for handling network operations with exponential backoff retry logic
/// </summary>
public class NetworkRetryService : INetworkRetryService
{
    private readonly ILogger<NetworkRetryService> _logger;
    private readonly NetworkRetryConfig _config;

    public NetworkRetryService(ILogger<NetworkRetryService> logger, NetworkRetryConfig? config = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? new NetworkRetryConfig();
    }

    /// <summary>
    /// Executes a network operation with exponential backoff retry logic
    /// </summary>
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

                // Don't delay after the last attempt
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
                // Non-retriable exception, fail immediately
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
    /// Executes a network operation with exponential backoff retry logic (void return)
    /// </summary>
    public async Task ExecuteWithRetryAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWithRetryAsync(async (ct) =>
        {
            await operation(ct);
            return true; // Dummy return value
        }, operationName, operationId, cancellationToken);
    }

    /// <summary>
    /// Tests network connectivity to a specific endpoint
    /// </summary>
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

            // First, try to ping the host
            var pingResult = await PingHostAsync(host, timeout, cancellationToken);
            
            // Then, try to establish a TCP connection
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
    /// Waits for network connectivity to be restored
    /// </summary>
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
    /// Determines if an exception is retriable for network operations
    /// </summary>
    private bool IsRetriableException(Exception ex)
    {
        return ex switch
        {
            SocketException socketEx => IsRetriableSocketException(socketEx),
            TimeoutException => true,
            HttpRequestException => true,
            NetworkRetryException => false, // Don't retry our own retry exceptions
            OperationCanceledException => false, // Don't retry cancellation
            _ => _config.RetryOnUnknownExceptions
        };
    }

    /// <summary>
    /// Determines if a socket exception is retriable
    /// </summary>
    private bool IsRetriableSocketException(SocketException ex)
    {
        return ex.SocketErrorCode switch
        {
            SocketError.ConnectionRefused => true,
            SocketError.ConnectionReset => true,
            SocketError.ConnectionAborted => true,
            SocketError.TimedOut => true,
            SocketError.NetworkUnreachable => true,
            SocketError.HostUnreachable => true,
            SocketError.HostDown => true,
            SocketError.NetworkDown => true,
            SocketError.TryAgain => true,
            _ => false
        };
    }

    /// <summary>
    /// Calculates exponential backoff delay with jitter
    /// </summary>
    private TimeSpan CalculateExponentialBackoffDelay(int attemptNumber)
    {
        var exponentialDelay = TimeSpan.FromMilliseconds(
            _config.BaseRetryDelay.TotalMilliseconds * Math.Pow(2, attemptNumber - 1));

        // Cap at maximum delay
        if (exponentialDelay > _config.MaxRetryDelay)
        {
            exponentialDelay = _config.MaxRetryDelay;
        }

        // Add jitter to prevent thundering herd
        if (_config.EnableJitter)
        {
            var jitterRange = exponentialDelay.TotalMilliseconds * 0.1; // 10% jitter
            var random = new Random();
            var jitter = TimeSpan.FromMilliseconds(random.NextDouble() * jitterRange);
            exponentialDelay = exponentialDelay.Add(jitter);
        }

        return exponentialDelay;
    }

    /// <summary>
    /// Pings a host to test basic network connectivity
    /// </summary>
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
    /// Tests TCP connection to a specific host and port
    /// </summary>
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