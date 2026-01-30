using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// 所有备份相关异常的基类
/// Base class for all backup-related exceptions
/// </summary>
public abstract class BackupException : Exception
{
    /// <summary>
    /// 操作标识符
    /// Operation identifier
    /// </summary>
    public string OperationId { get; }
    
    /// <summary>
    /// 异常发生时间
    /// Time when the exception occurred
    /// </summary>
    public DateTime OccurredAt { get; }
    
    /// <summary>
    /// 上下文信息
    /// Context information
    /// </summary>
    public string? ContextInfo { get; set; }

    /// <summary>
    /// 初始化备份异常实例
    /// Initializes a backup exception instance
    /// </summary>
    /// <param name="operationId">操作标识符 / Operation identifier</param>
    /// <param name="message">异常消息 / Exception message</param>
    protected BackupException(string operationId, string message) : base(message)
    {
        OperationId = operationId;
        OccurredAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 初始化备份异常实例（包含内部异常）
    /// Initializes a backup exception instance with inner exception
    /// </summary>
    /// <param name="operationId">操作标识符 / Operation identifier</param>
    /// <param name="message">异常消息 / Exception message</param>
    /// <param name="innerException">内部异常 / Inner exception</param>
    protected BackupException(string operationId, string message, Exception innerException) 
        : base(message, innerException)
    {
        OperationId = operationId;
        OccurredAt = DateTime.UtcNow;
    }
}

/// <summary>
/// MySQL服务操作失败时抛出的异常
/// Exception thrown when MySQL service operations fail
/// </summary>
public class MySQLServiceException : BackupException
{
    /// <summary>
    /// 服务名称
    /// Service name
    /// </summary>
    public string ServiceName { get; }
    
    /// <summary>
    /// 执行的操作类型
    /// Type of operation performed
    /// </summary>
    public MySQLServiceOperation Operation { get; }
    
    /// <summary>
    /// 操作的退出代码（如果可用）
    /// Exit code of the operation (if available)
    /// </summary>
    public int? ExitCode { get; set; }

    /// <summary>
    /// 初始化MySQL服务异常实例
    /// Initializes a MySQL service exception instance
    /// </summary>
    /// <param name="operationId">操作标识符 / Operation identifier</param>
    /// <param name="serviceName">服务名称 / Service name</param>
    /// <param name="operation">操作类型 / Operation type</param>
    /// <param name="message">异常消息 / Exception message</param>
    public MySQLServiceException(string operationId, string serviceName, MySQLServiceOperation operation, string message)
        : base(operationId, message)
    {
        ServiceName = serviceName;
        Operation = operation;
    }

    /// <summary>
    /// 初始化MySQL服务异常实例（包含内部异常）
    /// Initializes a MySQL service exception instance with inner exception
    /// </summary>
    /// <param name="operationId">操作标识符 / Operation identifier</param>
    /// <param name="serviceName">服务名称 / Service name</param>
    /// <param name="operation">操作类型 / Operation type</param>
    /// <param name="message">异常消息 / Exception message</param>
    /// <param name="innerException">内部异常 / Inner exception</param>
    public MySQLServiceException(string operationId, string serviceName, MySQLServiceOperation operation, string message, Exception innerException)
        : base(operationId, message, innerException)
    {
        ServiceName = serviceName;
        Operation = operation;
    }
}

/// <summary>
/// 文件压缩操作失败时抛出的异常
/// Exception thrown when file compression operations fail
/// </summary>
public class CompressionException : BackupException
{
    /// <summary>
    /// 源文件路径
    /// Source file path
    /// </summary>
    public string SourcePath { get; }
    
    /// <summary>
    /// 目标文件路径（如果可用）
    /// Target file path (if available)
    /// </summary>
    public string? TargetPath { get; set; }
    
    /// <summary>
    /// 已处理的字节数（如果可用）
    /// Number of bytes processed (if available)
    /// </summary>
    public long? ProcessedBytes { get; set; }

    /// <summary>
    /// 初始化压缩异常实例
    /// Initializes a compression exception instance
    /// </summary>
    /// <param name="operationId">操作标识符 / Operation identifier</param>
    /// <param name="sourcePath">源文件路径 / Source file path</param>
    /// <param name="message">异常消息 / Exception message</param>
    public CompressionException(string operationId, string sourcePath, string message)
        : base(operationId, message)
    {
        SourcePath = sourcePath;
    }

    /// <summary>
    /// 初始化压缩异常实例（包含内部异常）
    /// Initializes a compression exception instance with inner exception
    /// </summary>
    /// <param name="operationId">操作标识符 / Operation identifier</param>
    /// <param name="sourcePath">源文件路径 / Source file path</param>
    /// <param name="message">异常消息 / Exception message</param>
    /// <param name="innerException">内部异常 / Inner exception</param>
    public CompressionException(string operationId, string sourcePath, string message, Exception innerException)
        : base(operationId, message, innerException)
    {
        SourcePath = sourcePath;
    }
}

/// <summary>
/// 文件传输操作失败时抛出的异常
/// Exception thrown when file transfer operations fail
/// </summary>
public class TransferException : BackupException
{
    /// <summary>
    /// 文件路径
    /// File path
    /// </summary>
    public string FilePath { get; }
    
    /// <summary>
    /// 远程端点地址（如果可用）
    /// Remote endpoint address (if available)
    /// </summary>
    public string? RemoteEndpoint { get; set; }
    
    /// <summary>
    /// 已传输的字节数（如果可用）
    /// Number of bytes transferred (if available)
    /// </summary>
    public long? BytesTransferred { get; set; }
    
    /// <summary>
    /// 断点续传令牌（如果可用）
    /// Resume token (if available)
    /// </summary>
    public string? ResumeToken { get; set; }

    /// <summary>
    /// 初始化传输异常实例
    /// Initializes a transfer exception instance
    /// </summary>
    /// <param name="operationId">操作标识符 / Operation identifier</param>
    /// <param name="filePath">文件路径 / File path</param>
    /// <param name="message">异常消息 / Exception message</param>
    public TransferException(string operationId, string filePath, string message)
        : base(operationId, message)
    {
        FilePath = filePath;
    }

    /// <summary>
    /// 初始化传输异常实例（包含内部异常）
    /// Initializes a transfer exception instance with inner exception
    /// </summary>
    /// <param name="operationId">操作标识符 / Operation identifier</param>
    /// <param name="filePath">文件路径 / File path</param>
    /// <param name="message">异常消息 / Exception message</param>
    /// <param name="innerException">内部异常 / Inner exception</param>
    public TransferException(string operationId, string filePath, string message, Exception innerException)
        : base(operationId, message, innerException)
    {
        FilePath = filePath;
    }
}

/// <summary>
/// 操作超过配置的超时限制时抛出的异常
/// Exception thrown when operations exceed configured timeout limits
/// </summary>
public class OperationTimeoutException : BackupException
{
    /// <summary>
    /// 配置的超时时间
    /// Configured timeout duration
    /// </summary>
    public TimeSpan ConfiguredTimeout { get; }
    
    /// <summary>
    /// 实际执行时间
    /// Actual execution duration
    /// </summary>
    public TimeSpan ActualDuration { get; }
    
    /// <summary>
    /// 操作类型
    /// Type of operation
    /// </summary>
    public string OperationType { get; }

    /// <summary>
    /// 初始化操作超时异常实例
    /// Initializes an operation timeout exception instance
    /// </summary>
    /// <param name="operationId">操作标识符 / Operation identifier</param>
    /// <param name="operationType">操作类型 / Operation type</param>
    /// <param name="configuredTimeout">配置的超时时间 / Configured timeout</param>
    /// <param name="actualDuration">实际执行时间 / Actual duration</param>
    public OperationTimeoutException(string operationId, string operationType, TimeSpan configuredTimeout, TimeSpan actualDuration)
        : base(operationId, $"{operationType} operation timed out after {actualDuration.TotalSeconds:F1}s (limit: {configuredTimeout.TotalSeconds:F1}s)")
    {
        OperationType = operationType;
        ConfiguredTimeout = configuredTimeout;
        ActualDuration = actualDuration;
    }
}

/// <summary>
/// MySQL服务操作的类型
/// Types of MySQL service operations
/// </summary>
public enum MySQLServiceOperation
{
    /// <summary>
    /// 停止服务
    /// Stop service
    /// </summary>
    Stop,
    
    /// <summary>
    /// 启动服务
    /// Start service
    /// </summary>
    Start,
    
    /// <summary>
    /// 重启服务
    /// Restart service
    /// </summary>
    Restart,
    
    /// <summary>
    /// 验证服务可用性
    /// Verify service availability
    /// </summary>
    VerifyAvailability
}

/// <summary>
/// 错误恢复策略类型
/// Error recovery strategy types
/// </summary>
public enum RecoveryStrategy
{
    /// <summary>
    /// 无恢复策略
    /// No recovery strategy
    /// </summary>
    None,
    
    /// <summary>
    /// 重试操作
    /// Retry operation
    /// </summary>
    Retry,
    
    /// <summary>
    /// 重启MySQL服务
    /// Restart MySQL service
    /// </summary>
    RestartMySQL,
    
    /// <summary>
    /// 清理后重试
    /// Cleanup and retry
    /// </summary>
    CleanupAndRetry,
    
    /// <summary>
    /// 需要手动干预
    /// Manual intervention required
    /// </summary>
    ManualIntervention,
    
    /// <summary>
    /// 中止操作
    /// Abort operation
    /// </summary>
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