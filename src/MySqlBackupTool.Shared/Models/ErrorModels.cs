using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// Base class for all backup-related exceptions
/// </summary>
public abstract class BackupException : Exception
{
    public string OperationId { get; }
    public DateTime OccurredAt { get; }
    public string? ContextInfo { get; set; }

    protected BackupException(string operationId, string message) : base(message)
    {
        OperationId = operationId;
        OccurredAt = DateTime.UtcNow;
    }

    protected BackupException(string operationId, string message, Exception innerException) 
        : base(message, innerException)
    {
        OperationId = operationId;
        OccurredAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Exception thrown when MySQL service operations fail
/// </summary>
public class MySQLServiceException : BackupException
{
    public string ServiceName { get; }
    public MySQLServiceOperation Operation { get; }
    public int? ExitCode { get; set; }

    public MySQLServiceException(string operationId, string serviceName, MySQLServiceOperation operation, string message)
        : base(operationId, message)
    {
        ServiceName = serviceName;
        Operation = operation;
    }

    public MySQLServiceException(string operationId, string serviceName, MySQLServiceOperation operation, string message, Exception innerException)
        : base(operationId, message, innerException)
    {
        ServiceName = serviceName;
        Operation = operation;
    }
}

/// <summary>
/// Exception thrown when file compression operations fail
/// </summary>
public class CompressionException : BackupException
{
    public string SourcePath { get; }
    public string? TargetPath { get; set; }
    public long? ProcessedBytes { get; set; }

    public CompressionException(string operationId, string sourcePath, string message)
        : base(operationId, message)
    {
        SourcePath = sourcePath;
    }

    public CompressionException(string operationId, string sourcePath, string message, Exception innerException)
        : base(operationId, message, innerException)
    {
        SourcePath = sourcePath;
    }
}

/// <summary>
/// Exception thrown when file transfer operations fail
/// </summary>
public class TransferException : BackupException
{
    public string FilePath { get; }
    public string? RemoteEndpoint { get; set; }
    public long? BytesTransferred { get; set; }
    public string? ResumeToken { get; set; }

    public TransferException(string operationId, string filePath, string message)
        : base(operationId, message)
    {
        FilePath = filePath;
    }

    public TransferException(string operationId, string filePath, string message, Exception innerException)
        : base(operationId, message, innerException)
    {
        FilePath = filePath;
    }
}

/// <summary>
/// Exception thrown when operations exceed configured timeout limits
/// </summary>
public class OperationTimeoutException : BackupException
{
    public TimeSpan ConfiguredTimeout { get; }
    public TimeSpan ActualDuration { get; }
    public string OperationType { get; }

    public OperationTimeoutException(string operationId, string operationType, TimeSpan configuredTimeout, TimeSpan actualDuration)
        : base(operationId, $"{operationType} operation timed out after {actualDuration.TotalSeconds:F1}s (limit: {configuredTimeout.TotalSeconds:F1}s)")
    {
        OperationType = operationType;
        ConfiguredTimeout = configuredTimeout;
        ActualDuration = actualDuration;
    }
}

/// <summary>
/// Types of MySQL service operations
/// </summary>
public enum MySQLServiceOperation
{
    Stop,
    Start,
    Restart,
    VerifyAvailability
}

/// <summary>
/// Error recovery strategy types
/// </summary>
public enum RecoveryStrategy
{
    None,
    Retry,
    RestartMySQL,
    CleanupAndRetry,
    ManualIntervention,
    Abort
}

/// <summary>
/// Result of an error recovery attempt
/// </summary>
public class RecoveryResult
{
    public bool Success { get; set; }
    public RecoveryStrategy StrategyUsed { get; set; }
    public string? Message { get; set; }
    public Exception? Exception { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration { get; set; }
    public Dictionary<string, object> Context { get; set; } = new();

    /// <summary>
    /// Creates a successful recovery result
    /// </summary>
    public static RecoveryResult Successful(RecoveryStrategy strategy, string? message = null)
    {
        return new RecoveryResult
        {
            Success = true,
            StrategyUsed = strategy,
            Message = message
        };
    }

    /// <summary>
    /// Creates a failed recovery result
    /// </summary>
    public static RecoveryResult Failed(RecoveryStrategy strategy, string message, Exception? exception = null)
    {
        return new RecoveryResult
        {
            Success = false,
            StrategyUsed = strategy,
            Message = message,
            Exception = exception
        };
    }
}

/// <summary>
/// Configuration for error recovery behavior
/// </summary>
public class ErrorRecoveryConfig
{
    /// <summary>
    /// Maximum number of retry attempts for recoverable errors
    /// </summary>
    [Range(0, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay between retry attempts (exponential backoff)
    /// </summary>
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum delay between retry attempts
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Timeout for MySQL service operations
    /// </summary>
    public TimeSpan MySQLOperationTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Timeout for compression operations
    /// </summary>
    public TimeSpan CompressionTimeout { get; set; } = TimeSpan.FromHours(2);

    /// <summary>
    /// Timeout for file transfer operations
    /// </summary>
    public TimeSpan TransferTimeout { get; set; } = TimeSpan.FromHours(4);

    /// <summary>
    /// Whether to automatically restart MySQL on backup failures
    /// </summary>
    public bool AutoRestartMySQLOnFailure { get; set; } = true;

    /// <summary>
    /// Whether to send alerts for critical errors
    /// </summary>
    public bool EnableCriticalErrorAlerts { get; set; } = true;

    /// <summary>
    /// Email addresses to notify for critical errors
    /// </summary>
    public List<string> AlertEmailAddresses { get; set; } = new();

    /// <summary>
    /// Whether to clean up temporary files on errors
    /// </summary>
    public bool CleanupTemporaryFilesOnError { get; set; } = true;
}

/// <summary>
/// Critical error alert information
/// </summary>
public class CriticalErrorAlert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string OperationId { get; set; } = string.Empty;
    public string ErrorType { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public Dictionary<string, object> Context { get; set; } = new();
    public bool AlertSent { get; set; }
    public DateTime? AlertSentAt { get; set; }
    public List<string> AlertRecipients { get; set; } = new();
}