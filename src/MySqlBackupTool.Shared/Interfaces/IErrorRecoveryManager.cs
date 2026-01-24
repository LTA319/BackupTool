using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Interface for managing error recovery and handling strategies
/// </summary>
public interface IErrorRecoveryManager
{
    /// <summary>
    /// Handles MySQL service operation failures
    /// </summary>
    /// <param name="error">The MySQL service exception that occurred</param>
    /// <param name="cancellationToken">Cancellation token for the recovery operation</param>
    /// <returns>Result of the recovery attempt</returns>
    Task<RecoveryResult> HandleMySQLServiceFailureAsync(MySQLServiceException error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles file compression operation failures
    /// </summary>
    /// <param name="error">The compression exception that occurred</param>
    /// <param name="cancellationToken">Cancellation token for the recovery operation</param>
    /// <returns>Result of the recovery attempt</returns>
    Task<RecoveryResult> HandleCompressionFailureAsync(CompressionException error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles file transfer operation failures
    /// </summary>
    /// <param name="error">The transfer exception that occurred</param>
    /// <param name="cancellationToken">Cancellation token for the recovery operation</param>
    /// <returns>Result of the recovery attempt</returns>
    Task<RecoveryResult> HandleTransferFailureAsync(TransferException error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles operation timeout failures
    /// </summary>
    /// <param name="error">The timeout exception that occurred</param>
    /// <param name="cancellationToken">Cancellation token for the recovery operation</param>
    /// <returns>Result of the recovery attempt</returns>
    Task<RecoveryResult> HandleTimeoutFailureAsync(OperationTimeoutException error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles general backup operation failures
    /// </summary>
    /// <param name="error">The backup exception that occurred</param>
    /// <param name="cancellationToken">Cancellation token for the recovery operation</param>
    /// <returns>Result of the recovery attempt</returns>
    Task<RecoveryResult> HandleGeneralFailureAsync(BackupException error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends critical error alerts to configured recipients
    /// </summary>
    /// <param name="alert">The critical error alert to send</param>
    /// <param name="cancellationToken">Cancellation token for the alert operation</param>
    /// <returns>True if alert was sent successfully, false otherwise</returns>
    Task<bool> SendCriticalErrorAlertAsync(CriticalErrorAlert alert, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an operation with timeout protection
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operation">The operation to execute</param>
    /// <param name="timeout">Maximum time allowed for the operation</param>
    /// <param name="operationType">Type of operation for error reporting</param>
    /// <param name="operationId">Unique identifier for the operation</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result of the operation</returns>
    Task<T> ExecuteWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        string operationType,
        string operationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an operation with timeout protection (void return)
    /// </summary>
    /// <param name="operation">The operation to execute</param>
    /// <param name="timeout">Maximum time allowed for the operation</param>
    /// <param name="operationType">Type of operation for error reporting</param>
    /// <param name="operationId">Unique identifier for the operation</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    Task ExecuteWithTimeoutAsync(
        Func<CancellationToken, Task> operation,
        TimeSpan timeout,
        string operationType,
        string operationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up temporary files and resources after an error
    /// </summary>
    /// <param name="operationId">Unique identifier for the operation</param>
    /// <param name="filePaths">List of file paths to clean up</param>
    /// <param name="cancellationToken">Cancellation token for the cleanup operation</param>
    /// <returns>True if cleanup was successful, false otherwise</returns>
    Task<bool> CleanupAfterErrorAsync(string operationId, IEnumerable<string> filePaths, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current error recovery configuration
    /// </summary>
    ErrorRecoveryConfig Configuration { get; }

    /// <summary>
    /// Updates the error recovery configuration
    /// </summary>
    /// <param name="config">New configuration settings</param>
    void UpdateConfiguration(ErrorRecoveryConfig config);
}