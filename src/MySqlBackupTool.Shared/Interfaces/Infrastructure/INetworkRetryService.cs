using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 具有重试逻辑的网络操作接口 / Interface for network operations with retry logic
/// 提供网络操作的自动重试机制，包括指数退避策略和连接性测试功能
/// Provides automatic retry mechanisms for network operations including exponential backoff strategy and connectivity testing
/// </summary>
public interface INetworkRetryService
{
    /// <summary>
    /// 使用指数退避重试逻辑执行网络操作 / Executes a network operation with exponential backoff retry logic
    /// 当网络操作失败时，自动进行重试，重试间隔按指数增长以避免网络拥塞
    /// Automatically retries when network operations fail, with exponentially increasing intervals to avoid network congestion
    /// </summary>
    /// <typeparam name="T">操作的返回类型 / Return type of the operation</typeparam>
    /// <param name="operation">要执行的网络操作 / The network operation to execute</param>
    /// <param name="operationName">用于日志记录的操作名称 / Name of the operation for logging</param>
    /// <param name="operationId">操作的唯一标识符 / Unique identifier for the operation</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>操作的结果 / Result of the operation</returns>
    /// <exception cref="ArgumentNullException">当操作参数为null时抛出 / Thrown when operation parameter is null</exception>
    /// <exception cref="OperationCanceledException">当操作被取消时抛出 / Thrown when operation is cancelled</exception>
    /// <exception cref="NetworkException">当所有重试都失败时抛出 / Thrown when all retries fail</exception>
    Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        string operationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用指数退避重试逻辑执行网络操作（无返回值） / Executes a network operation with exponential backoff retry logic (void return)
    /// 当网络操作失败时，自动进行重试，适用于无返回值的网络操作
    /// Automatically retries when network operations fail, suitable for void network operations
    /// </summary>
    /// <param name="operation">要执行的网络操作 / The network operation to execute</param>
    /// <param name="operationName">用于日志记录的操作名称 / Name of the operation for logging</param>
    /// <param name="operationId">操作的唯一标识符 / Unique identifier for the operation</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <exception cref="ArgumentNullException">当操作参数为null时抛出 / Thrown when operation parameter is null</exception>
    /// <exception cref="OperationCanceledException">当操作被取消时抛出 / Thrown when operation is cancelled</exception>
    /// <exception cref="NetworkException">当所有重试都失败时抛出 / Thrown when all retries fail</exception>
    Task ExecuteWithRetryAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        string operationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 测试到特定端点的网络连接性 / Tests network connectivity to a specific endpoint
    /// 尝试连接到指定的主机和端口，验证网络连接是否可用
    /// Attempts to connect to specified host and port to verify network connectivity availability
    /// </summary>
    /// <param name="host">目标主机 / Target host</param>
    /// <param name="port">目标端口 / Target port</param>
    /// <param name="timeout">连接超时时间 / Connection timeout</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>连接性测试结果 / Connectivity test result</returns>
    /// <exception cref="ArgumentException">当主机名无效时抛出 / Thrown when hostname is invalid</exception>
    /// <exception cref="ArgumentOutOfRangeException">当端口号无效时抛出 / Thrown when port number is invalid</exception>
    Task<NetworkConnectivityResult> TestConnectivityAsync(
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 等待网络连接性恢复 / Waits for network connectivity to be restored
    /// 持续监控网络连接状态，直到连接恢复或达到最大等待时间
    /// Continuously monitors network connection status until connectivity is restored or maximum wait time is reached
    /// </summary>
    /// <param name="host">目标主机 / Target host</param>
    /// <param name="port">目标端口 / Target port</param>
    /// <param name="maxWaitTime">最大等待时间 / Maximum time to wait</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>如果连接性恢复返回true，如果超时返回false / True if connectivity was restored, false if timeout occurred</returns>
    /// <exception cref="ArgumentException">当主机名无效时抛出 / Thrown when hostname is invalid</exception>
    /// <exception cref="ArgumentOutOfRangeException">当端口号或等待时间无效时抛出 / Thrown when port number or wait time is invalid</exception>
    Task<bool> WaitForConnectivityAsync(
        string host,
        int port,
        TimeSpan maxWaitTime,
        CancellationToken cancellationToken = default);
}