using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Interface for network operations with retry logic
/// </summary>
public interface INetworkRetryService
{
    /// <summary>
    /// Executes a network operation with exponential backoff retry logic
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operation">The network operation to execute</param>
    /// <param name="operationName">Name of the operation for logging</param>
    /// <param name="operationId">Unique identifier for the operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the operation</returns>
    Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        string operationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a network operation with exponential backoff retry logic (void return)
    /// </summary>
    /// <param name="operation">The network operation to execute</param>
    /// <param name="operationName">Name of the operation for logging</param>
    /// <param name="operationId">Unique identifier for the operation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ExecuteWithRetryAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        string operationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests network connectivity to a specific endpoint
    /// </summary>
    /// <param name="host">Target host</param>
    /// <param name="port">Target port</param>
    /// <param name="timeout">Connection timeout</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Connectivity test result</returns>
    Task<NetworkConnectivityResult> TestConnectivityAsync(
        string host,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for network connectivity to be restored
    /// </summary>
    /// <param name="host">Target host</param>
    /// <param name="port">Target port</param>
    /// <param name="maxWaitTime">Maximum time to wait</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if connectivity was restored, false if timeout occurred</returns>
    Task<bool> WaitForConnectivityAsync(
        string host,
        int port,
        TimeSpan maxWaitTime,
        CancellationToken cancellationToken = default);
}