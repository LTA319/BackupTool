using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Net.Mail;
using System.Text;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Manages error recovery and handling strategies for backup operations
/// </summary>
public class ErrorRecoveryManager : IErrorRecoveryManager
{
    private readonly ILogger<ErrorRecoveryManager> _logger;
    //private readonly IMySQLManager _mysqlManager;
    //private readonly ICompressionService _compressionService;
    private readonly IAlertingService? _alertingService;
    private ErrorRecoveryConfig _configuration;

    public ErrorRecoveryManager(
        ILogger<ErrorRecoveryManager> logger,
        //IMySQLManager mysqlManager,
        //ICompressionService compressionService,
        ErrorRecoveryConfig? configuration = null,
        IAlertingService? alertingService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
       // _mysqlManager = mysqlManager ?? throw new ArgumentNullException(nameof(mysqlManager));
       //_compressionService = compressionService ?? throw new ArgumentNullException(nameof(compressionService));
        _alertingService = alertingService;
        _configuration = configuration ?? new ErrorRecoveryConfig();
    }

    public ErrorRecoveryConfig Configuration => _configuration;

    public void UpdateConfiguration(ErrorRecoveryConfig config)
    {
        _configuration = config ?? throw new ArgumentNullException(nameof(config));
        _logger.LogInformation("Error recovery configuration updated");
    }

    public async Task<RecoveryResult> HandleMySQLServiceFailureAsync(
        MySQLServiceException error, 
        CancellationToken cancellationToken = default,
        IMySQLManager? mysqlManager = null)
    {
        _logger.LogError(error, "MySQL service failure occurred for operation {OperationId}, service {ServiceName}, operation {Operation}",
            error.OperationId, error.ServiceName, error.Operation);

        var startTime = DateTime.UtcNow;

        try
        {
            // Strategy depends on the type of MySQL operation that failed
            switch (error.Operation)
            {
                case MySQLServiceOperation.Stop:
                    return await HandleMySQLStopFailureAsync(error, cancellationToken, mysqlManager);

                case MySQLServiceOperation.Start:
                    return await HandleMySQLStartFailureAsync(error, cancellationToken, mysqlManager);

                case MySQLServiceOperation.VerifyAvailability:
                    return await HandleMySQLVerificationFailureAsync(error, cancellationToken, mysqlManager);

                case MySQLServiceOperation.Restart:
                    return await HandleMySQLRestartFailureAsync(error, cancellationToken, mysqlManager);

                default:
                    _logger.LogWarning("Unknown MySQL operation type: {Operation}", error.Operation);
                    return RecoveryResult.Failed(RecoveryStrategy.ManualIntervention, 
                        $"Unknown MySQL operation type: {error.Operation}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recovery failed for MySQL service failure");
            return RecoveryResult.Failed(RecoveryStrategy.ManualIntervention, 
                "Error recovery process failed", ex);
        }
        finally
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("MySQL service failure recovery completed in {Duration}ms", duration.TotalMilliseconds);
        }
    }

    public async Task<RecoveryResult> HandleCompressionFailureAsync(
        CompressionException error, 
        CancellationToken cancellationToken = default,
        ICompressionService? compressionService = null)
    {
        _logger.LogError(error, "Compression failure occurred for operation {OperationId}, source path {SourcePath}",
            error.OperationId, error.SourcePath);

        var startTime = DateTime.UtcNow;

        try
        {
            if (compressionService == null)
            {
                _logger.LogWarning("CompressionService not provided for compression failure recovery");
                return RecoveryResult.Failed(RecoveryStrategy.ManualIntervention,
                    "CompressionService not available");
            }
            // Clean up any partial compression files
            if (!string.IsNullOrEmpty(error.TargetPath) && File.Exists(error.TargetPath))
            {
                _logger.LogInformation("Cleaning up partial compression file: {TargetPath}", error.TargetPath);
                await compressionService.CleanupAsync(error.TargetPath);
            }

            // If MySQL was stopped for this operation, ensure it's restarted
            if (_configuration.AutoRestartMySQLOnFailure)
            {
                _logger.LogInformation("Attempting to restart MySQL after compression failure");
                // MySQL restart requires service name from context - this should be provided by the caller
                _logger.LogWarning("MySQL restart required but service name not available in compression error context");
            }

            // Send critical error alert if configured
            if (_configuration.EnableCriticalErrorAlerts)
            {
                var alert = new CriticalErrorAlert
                {
                    OperationId = error.OperationId,
                    ErrorType = "CompressionFailure",
                    ErrorMessage = error.Message,
                    StackTrace = error.StackTrace,
                    Context = new Dictionary<string, object>
                    {
                        ["SourcePath"] = error.SourcePath,
                        ["TargetPath"] = error.TargetPath ?? "Unknown",
                        ["ProcessedBytes"] = error.ProcessedBytes ?? 0
                    }
                };

                await SendCriticalErrorAlertAsync(alert, cancellationToken);
            }

            return RecoveryResult.Successful(RecoveryStrategy.CleanupAndRetry, 
                "Cleaned up partial files and prepared for retry");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recovery failed for compression failure");
            return RecoveryResult.Failed(RecoveryStrategy.ManualIntervention, 
                "Compression error recovery failed", ex);
        }
        finally
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Compression failure recovery completed in {Duration}ms", duration.TotalMilliseconds);
        }
    }

    public async Task<RecoveryResult> HandleTransferFailureAsync(TransferException error, CancellationToken cancellationToken = default)
    {
        _logger.LogError(error, "Transfer failure occurred for operation {OperationId}, file {FilePath}",
            error.OperationId, error.FilePath);

        var startTime = DateTime.UtcNow;

        try
        {
            // If we have a resume token, the transfer can be resumed
            if (!string.IsNullOrEmpty(error.ResumeToken))
            {
                _logger.LogInformation("Transfer failure has resume token {ResumeToken}, marking for retry", error.ResumeToken);
                return RecoveryResult.Successful(RecoveryStrategy.Retry, 
                    $"Transfer can be resumed using token: {error.ResumeToken}");
            }

            // If MySQL was stopped for this operation, ensure it's restarted
            if (_configuration.AutoRestartMySQLOnFailure)
            {
                _logger.LogInformation("Attempting to restart MySQL after transfer failure");
                // MySQL restart requires service name from context - this should be provided by the caller
                _logger.LogWarning("MySQL restart required but service name not available in transfer error context");
            }

            // Send critical error alert if configured
            if (_configuration.EnableCriticalErrorAlerts)
            {
                var alert = new CriticalErrorAlert
                {
                    OperationId = error.OperationId,
                    ErrorType = "TransferFailure",
                    ErrorMessage = error.Message,
                    StackTrace = error.StackTrace,
                    Context = new Dictionary<string, object>
                    {
                        ["FilePath"] = error.FilePath,
                        ["RemoteEndpoint"] = error.RemoteEndpoint ?? "Unknown",
                        ["BytesTransferred"] = error.BytesTransferred ?? 0,
                        ["ResumeToken"] = error.ResumeToken ?? "None"
                    }
                };

                await SendCriticalErrorAlertAsync(alert, cancellationToken);
            }

            return RecoveryResult.Successful(RecoveryStrategy.RestartMySQL, 
                "Prepared for retry after MySQL restart");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recovery failed for transfer failure");
            return RecoveryResult.Failed(RecoveryStrategy.ManualIntervention, 
                "Transfer error recovery failed", ex);
        }
        finally
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Transfer failure recovery completed in {Duration}ms", duration.TotalMilliseconds);
        }
    }

    public async Task<RecoveryResult> HandleTimeoutFailureAsync(OperationTimeoutException error, CancellationToken cancellationToken = default)
    {
        _logger.LogError(error, "Operation timeout occurred for operation {OperationId}, type {OperationType}, configured timeout {ConfiguredTimeout}, actual duration {ActualDuration}",
            error.OperationId, error.OperationType, error.ConfiguredTimeout, error.ActualDuration);

        var startTime = DateTime.UtcNow;

        try
        {
            // Send critical error alert for timeouts
            if (_configuration.EnableCriticalErrorAlerts)
            {
                var alert = new CriticalErrorAlert
                {
                    OperationId = error.OperationId,
                    ErrorType = "OperationTimeout",
                    ErrorMessage = error.Message,
                    StackTrace = error.StackTrace,
                    Context = new Dictionary<string, object>
                    {
                        ["OperationType"] = error.OperationType,
                        ["ConfiguredTimeout"] = error.ConfiguredTimeout.ToString(),
                        ["ActualDuration"] = error.ActualDuration.ToString()
                    }
                };

                await SendCriticalErrorAlertAsync(alert, cancellationToken);
            }

            // For timeout errors, we typically need manual intervention
            return RecoveryResult.Failed(RecoveryStrategy.ManualIntervention, 
                $"Operation {error.OperationType} timed out and requires manual intervention");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recovery failed for timeout failure");
            return RecoveryResult.Failed(RecoveryStrategy.ManualIntervention, 
                "Timeout error recovery failed", ex);
        }
        finally
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Timeout failure recovery completed in {Duration}ms", duration.TotalMilliseconds);
        }
    }

    public async Task<RecoveryResult> HandleGeneralFailureAsync(BackupException error, CancellationToken cancellationToken = default)
    {
        _logger.LogError(error, "General backup failure occurred for operation {OperationId}",
            error.OperationId);

        var startTime = DateTime.UtcNow;

        try
        {
            // Send critical error alert
            if (_configuration.EnableCriticalErrorAlerts)
            {
                var alert = new CriticalErrorAlert
                {
                    OperationId = error.OperationId,
                    ErrorType = error.GetType().Name,
                    ErrorMessage = error.Message,
                    StackTrace = error.StackTrace,
                    Context = new Dictionary<string, object>
                    {
                        ["ContextInfo"] = error.ContextInfo ?? "None"
                    }
                };

                await SendCriticalErrorAlertAsync(alert, cancellationToken);
            }

            return RecoveryResult.Failed(RecoveryStrategy.ManualIntervention, 
                "General backup failure requires manual intervention");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recovery failed for general failure");
            return RecoveryResult.Failed(RecoveryStrategy.ManualIntervention, 
                "General error recovery failed", ex);
        }
        finally
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("General failure recovery completed in {Duration}ms", duration.TotalMilliseconds);
        }
    }

    public async Task<bool> SendCriticalErrorAlertAsync(CriticalErrorAlert alert, CancellationToken cancellationToken = default)
    {
        if (_alertingService == null)
        {
            _logger.LogDebug("No alerting service configured, logging alert for operation {OperationId}", alert.OperationId);
            _logger.LogWarning("Critical error alert: {ErrorType} - {ErrorMessage}", alert.ErrorType, alert.ErrorMessage);
            return false;
        }

        try
        {
            return await _alertingService.SendCriticalErrorAlertAsync(alert, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send critical error alert through alerting service for operation {OperationId}", alert.OperationId);
            return false;
        }
    }

    public async Task<T> ExecuteWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        string operationType,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogDebug("Executing {OperationType} operation {OperationId} with timeout {Timeout}",
                operationType, operationId, timeout);

            var result = await operation(combinedCts.Token);

            var duration = DateTime.UtcNow - startTime;
            _logger.LogDebug("Operation {OperationType} completed successfully in {Duration}ms",
                operationType, duration.TotalMilliseconds);

            return result;
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            var actualDuration = DateTime.UtcNow - startTime;
            var timeoutException = new OperationTimeoutException(operationId, operationType, timeout, actualDuration);
            
            _logger.LogError(timeoutException, "Operation {OperationType} timed out after {ActualDuration}",
                operationType, actualDuration);

            throw timeoutException;
        }
    }

    public async Task ExecuteWithTimeoutAsync(
        Func<CancellationToken, Task> operation,
        TimeSpan timeout,
        string operationType,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWithTimeoutAsync(async (ct) =>
        {
            await operation(ct);
            return true; // Dummy return value for the generic version
        }, timeout, operationType, operationId, cancellationToken);
    }

    public async Task<bool> CleanupAfterErrorAsync(
        string operationId, IEnumerable<string> filePaths, 
        CancellationToken cancellationToken = default,
        ICompressionService? compressionService = null)
    {
        if (!_configuration.CleanupTemporaryFilesOnError)
        {
            _logger.LogDebug("Cleanup after error is disabled for operation {OperationId}", operationId);
            return true;
        }

        var cleanupSuccessful = true;
        var cleanedFiles = new List<string>();

        try
        {
            _logger.LogInformation("Starting cleanup after error for operation {OperationId}", operationId);

            foreach (var filePath in filePaths)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        if (compressionService == null)
                        {
                            _logger.LogWarning("CompressionService not provided for cleanup");
                            return false;
                        }

                        await compressionService.CleanupAsync(filePath);
                        cleanedFiles.Add(filePath);
                        _logger.LogDebug("Cleaned up file: {FilePath}", filePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up file {FilePath}", filePath);
                    cleanupSuccessful = false;
                }
            }

            _logger.LogInformation("Cleanup completed for operation {OperationId}. Cleaned {CleanedCount} files, success: {Success}",
                operationId, cleanedFiles.Count, cleanupSuccessful);

            return cleanupSuccessful;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cleanup after error failed for operation {OperationId}", operationId);
            return false;
        }
    }

    #region Private Helper Methods

    private async Task<RecoveryResult> HandleMySQLStopFailureAsync(
        MySQLServiceException error,
        CancellationToken cancellationToken,
        IMySQLManager? mysqlManager = null)
    {
        _logger.LogInformation("Attempting recovery for MySQL stop failure");

        // Try escalating stop strategies
        for (int attempt = 1; attempt <= _configuration.MaxRetryAttempts; attempt++)
        {
            try
            {
                _logger.LogInformation("MySQL stop recovery attempt {Attempt}/{MaxAttempts}", attempt, _configuration.MaxRetryAttempts);

                // Wait with exponential backoff
                if (attempt > 1)
                {
                    var delay = TimeSpan.FromMilliseconds(_configuration.BaseRetryDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                    delay = delay > _configuration.MaxRetryDelay ? _configuration.MaxRetryDelay : delay;
                    
                    _logger.LogDebug("Waiting {Delay}ms before retry attempt", delay.TotalMilliseconds);
                    await Task.Delay(delay, cancellationToken);
                }
                if (mysqlManager == null)
                {
                    _logger.LogWarning("MySQLManager not available for MySQL stop recovery");
                    return RecoveryResult.Failed(RecoveryStrategy.ManualIntervention,
                        "MySQLManager not available");
                }

                // Try to stop the service again
                var stopResult = await mysqlManager.StopInstanceAsync(error.ServiceName);
                if (stopResult)
                {
                    _logger.LogInformation("MySQL stop recovery successful on attempt {Attempt}", attempt);
                    return RecoveryResult.Successful(RecoveryStrategy.Retry, $"MySQL stopped successfully on attempt {attempt}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MySQL stop recovery attempt {Attempt} failed", attempt);
            }
        }

        _logger.LogError("All MySQL stop recovery attempts failed");
        return RecoveryResult.Failed(RecoveryStrategy.ManualIntervention, 
            "MySQL service could not be stopped after multiple attempts");
    }

    private async Task<RecoveryResult> HandleMySQLStartFailureAsync(
        MySQLServiceException error, 
        CancellationToken cancellationToken,
        IMySQLManager? mysqlManager = null)
    {
        _logger.LogInformation("Attempting recovery for MySQL start failure");

        // Try multiple start attempts with increasing delays
        for (int attempt = 1; attempt <= _configuration.MaxRetryAttempts; attempt++)
        {
            try
            {
                _logger.LogInformation("MySQL start recovery attempt {Attempt}/{MaxAttempts}", attempt, _configuration.MaxRetryAttempts);

                if (attempt > 1)
                {
                    var delay = TimeSpan.FromMilliseconds(_configuration.BaseRetryDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                    delay = delay > _configuration.MaxRetryDelay ? _configuration.MaxRetryDelay : delay;
                    
                    await Task.Delay(delay, cancellationToken);
                }
                if (mysqlManager == null)
                {
                    _logger.LogWarning("MySQLManager not available for MySQL stop recovery");
                    return RecoveryResult.Failed(RecoveryStrategy.ManualIntervention,
                        "MySQLManager not available");
                }
                var startResult = await mysqlManager.StartInstanceAsync(error.ServiceName);
                if (startResult)
                {
                    _logger.LogInformation("MySQL start recovery successful on attempt {Attempt}", attempt);
                    return RecoveryResult.Successful(RecoveryStrategy.Retry, $"MySQL started successfully on attempt {attempt}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MySQL start recovery attempt {Attempt} failed", attempt);
            }
        }

        _logger.LogError("All MySQL start recovery attempts failed");
        return RecoveryResult.Failed(RecoveryStrategy.ManualIntervention, 
            "MySQL service could not be started after multiple attempts");
    }

    private async Task<RecoveryResult> HandleMySQLVerificationFailureAsync(
        MySQLServiceException error, 
        CancellationToken cancellationToken,
        IMySQLManager? mysqlManager = null)
    {
        _logger.LogInformation("Attempting recovery for MySQL verification failure");

        // For verification failures, we might need to restart the service
        try
        {
            _logger.LogInformation("Attempting to restart MySQL service for verification recovery");

            if (mysqlManager == null)
            {
                _logger.LogWarning("MySQLManager not available for MySQL stop recovery");
                return RecoveryResult.Failed(RecoveryStrategy.ManualIntervention,
                    "MySQLManager not available");
            }
            var stopResult = await mysqlManager.StopInstanceAsync(error.ServiceName);

            if (!stopResult)
            {
                _logger.LogWarning("Failed to stop MySQL service during verification recovery");
            }

            await Task.Delay(_configuration.BaseRetryDelay, cancellationToken);

            var startResult = await mysqlManager.StartInstanceAsync(error.ServiceName);
            if (startResult)
            {
                _logger.LogInformation("MySQL verification recovery successful after restart");
                return RecoveryResult.Successful(RecoveryStrategy.RestartMySQL, "MySQL restarted successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MySQL verification recovery failed");
        }

        return RecoveryResult.Failed(RecoveryStrategy.ManualIntervention, 
            "MySQL verification failed and restart was unsuccessful");
    }

    private async Task<RecoveryResult> HandleMySQLRestartFailureAsync(
        MySQLServiceException error, 
        CancellationToken cancellationToken,
        IMySQLManager? mysqlManager = null)
    {
        _logger.LogInformation("Attempting recovery for MySQL restart failure");

        // For restart failures, try individual stop and start operations
        try
        {
            _logger.LogInformation("Attempting manual stop/start sequence for restart recovery");

            if (mysqlManager == null)
            {
                _logger.LogWarning("MySQLManager not available for MySQL stop recovery");
                return RecoveryResult.Failed(RecoveryStrategy.ManualIntervention,
                    "MySQLManager not available");
            }
            var stopResult = await mysqlManager.StopInstanceAsync(error.ServiceName);
            await Task.Delay(_configuration.BaseRetryDelay, cancellationToken);
            
            var startResult = await mysqlManager.StartInstanceAsync(error.ServiceName);
            
            if (stopResult && startResult)
            {
                _logger.LogInformation("MySQL restart recovery successful with manual sequence");
                return RecoveryResult.Successful(RecoveryStrategy.RestartMySQL, "MySQL restarted with manual stop/start sequence");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MySQL restart recovery failed");
        }

        return RecoveryResult.Failed(RecoveryStrategy.ManualIntervention, 
            "MySQL restart failed and manual stop/start sequence was unsuccessful");
    }

    private string BuildAlertEmailBody(CriticalErrorAlert alert)
    {
        var sb = new StringBuilder();
        sb.AppendLine("MySQL Backup Tool Critical Error Alert");
        sb.AppendLine("=====================================");
        sb.AppendLine();
        sb.AppendLine($"Error ID: {alert.Id}");
        sb.AppendLine($"Operation ID: {alert.OperationId}");
        sb.AppendLine($"Error Type: {alert.ErrorType}");
        sb.AppendLine($"Occurred At: {alert.OccurredAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("Error Message:");
        sb.AppendLine(alert.ErrorMessage);
        sb.AppendLine();

        if (alert.Context.Any())
        {
            sb.AppendLine("Context Information:");
            foreach (var kvp in alert.Context)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(alert.StackTrace))
        {
            sb.AppendLine("Stack Trace:");
            sb.AppendLine(alert.StackTrace);
        }

        return sb.ToString();
    }

    #endregion
}