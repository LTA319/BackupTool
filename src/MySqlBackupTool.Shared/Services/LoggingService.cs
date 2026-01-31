using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 应用程序特定的日志服务实现，提供结构化日志记录 / Application-specific logging service implementation that provides structured logging
/// 为常见操作提供备份特定的方法 / with backup-specific methods for common operations
/// </summary>
public class LoggingService : ILoggingService
{
    private readonly ILogger<LoggingService> _logger;

    /// <summary>
    /// 初始化日志服务 / Initialize logging service
    /// </summary>
    /// <param name="logger">日志记录器实例 / Logger instance</param>
    /// <exception cref="ArgumentNullException">当logger为null时抛出 / Thrown when logger is null</exception>
    public LoggingService(ILogger<LoggingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 记录信息级别日志 / Log information level message
    /// </summary>
    /// <param name="message">日志消息 / Log message</param>
    /// <param name="args">消息参数 / Message arguments</param>
    public void LogInformation(string message, params object[] args)
    {
        _logger.LogInformation(message, args);
    }

    /// <summary>
    /// 记录调试级别日志 / Log debug level message
    /// </summary>
    /// <param name="message">日志消息 / Log message</param>
    /// <param name="args">消息参数 / Message arguments</param>
    public void LogDebug(string message, params object[] args)
    {
        _logger.LogDebug(message, args);
    }

    /// <summary>
    /// 记录警告级别日志 / Log warning level message
    /// </summary>
    /// <param name="message">日志消息 / Log message</param>
    /// <param name="args">消息参数 / Message arguments</param>
    public void LogWarning(string message, params object[] args)
    {
        _logger.LogWarning(message, args);
    }

    /// <summary>
    /// 记录错误级别日志 / Log error level message
    /// </summary>
    /// <param name="message">日志消息 / Log message</param>
    /// <param name="args">消息参数 / Message arguments</param>
    public void LogError(string message, params object[] args)
    {
        _logger.LogError(message, args);
    }

    /// <summary>
    /// 记录带异常的错误级别日志 / Log error level message with exception
    /// </summary>
    /// <param name="exception">异常信息 / Exception information</param>
    /// <param name="message">日志消息 / Log message</param>
    /// <param name="args">消息参数 / Message arguments</param>
    public void LogError(Exception exception, string message, params object[] args)
    {
        _logger.LogError(exception, message, args);
    }

    /// <summary>
    /// 记录关键级别日志 / Log critical level message
    /// </summary>
    /// <param name="message">日志消息 / Log message</param>
    /// <param name="args">消息参数 / Message arguments</param>
    public void LogCritical(string message, params object[] args)
    {
        _logger.LogCritical(message, args);
    }

    /// <summary>
    /// 记录带异常的关键级别日志 / Log critical level message with exception
    /// </summary>
    /// <param name="exception">异常信息 / Exception information</param>
    /// <param name="message">日志消息 / Log message</param>
    /// <param name="args">消息参数 / Message arguments</param>
    public void LogCritical(Exception exception, string message, params object[] args)
    {
        _logger.LogCritical(exception, message, args);
    }

    /// <summary>
    /// 检查指定日志级别是否启用 / Check if specified log level is enabled
    /// </summary>
    /// <param name="logLevel">日志级别 / Log level</param>
    /// <returns>是否启用 / Whether enabled</returns>
    public bool IsEnabled(LogLevel logLevel)
    {
        return _logger.IsEnabled(logLevel);
    }

    /// <summary>
    /// 开始日志作用域 / Begin logging scope
    /// </summary>
    /// <typeparam name="TState">状态类型 / State type</typeparam>
    /// <param name="state">作用域状态 / Scope state</param>
    /// <returns>可释放的作用域 / Disposable scope</returns>
    /// <exception cref="InvalidOperationException">创建作用域失败时抛出 / Thrown when scope creation fails</exception>
    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return _logger.BeginScope(state) ?? throw new InvalidOperationException("Failed to create logging scope");
    }

    /// <summary>
    /// 记录指定级别的日志 / Log message at specified level
    /// </summary>
    /// <typeparam name="TState">状态类型 / State type</typeparam>
    /// <param name="logLevel">日志级别 / Log level</param>
    /// <param name="eventId">事件ID / Event ID</param>
    /// <param name="state">日志状态 / Log state</param>
    /// <param name="exception">异常信息 / Exception information</param>
    /// <param name="formatter">格式化函数 / Formatter function</param>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _logger.Log(logLevel, eventId, state, exception, formatter);
    }

    /// <summary>
    /// 记录备份操作的开始 / Logs the start of a backup operation
    /// </summary>
    /// <param name="configurationId">正在执行的备份配置ID / The ID of the backup configuration being executed</param>
    /// <param name="resumeToken">可选的恢复令牌（如果这是恢复的备份操作） / Optional resume token if this is a resumed backup operation</param>
    public void LogBackupStart(int configurationId, string? resumeToken = null)
    {
        if (string.IsNullOrEmpty(resumeToken))
        {
            _logger.LogInformation("Starting backup operation for configuration {ConfigurationId}", configurationId);
        }
        else
        {
            _logger.LogInformation("Resuming backup operation for configuration {ConfigurationId} with token {ResumeToken}", 
                configurationId, resumeToken);
        }
    }

    /// <summary>
    /// 记录备份操作的完成 / Logs the completion of a backup operation
    /// </summary>
    /// <param name="backupLogId">备份日志条目的ID / The ID of the backup log entry</param>
    /// <param name="success">备份是否成功完成 / Whether the backup completed successfully</param>
    /// <param name="filePath">备份文件路径（如果成功） / Path to the backup file (if successful)</param>
    /// <param name="fileSize">备份文件大小（字节）（如果成功） / Size of the backup file in bytes (if successful)</param>
    /// <param name="errorMessage">错误消息（如果失败） / Error message (if failed)</param>
    public void LogBackupComplete(int backupLogId, bool success, string? filePath = null, long? fileSize = null, string? errorMessage = null)
    {
        if (success)
        {
            _logger.LogInformation("Backup {BackupLogId} completed successfully. File: {FilePath}, Size: {FileSize} bytes", 
                backupLogId, filePath, fileSize);
        }
        else
        {
            _logger.LogError("Backup {BackupLogId} failed: {ErrorMessage}", backupLogId, errorMessage);
        }
    }

    /// <summary>
    /// 记录传输进度 / Log transfer progress
    /// </summary>
    /// <param name="backupLogId">备份日志ID / Backup log ID</param>
    /// <param name="chunkIndex">块索引 / Chunk index</param>
    /// <param name="chunkSize">块大小 / Chunk size</param>
    /// <param name="progressPercentage">进度百分比 / Progress percentage</param>
    public void LogTransferProgress(int backupLogId, int chunkIndex, long chunkSize, double progressPercentage)
    {
        _logger.LogDebug("Transfer progress for backup {BackupLogId}: chunk {ChunkIndex} ({ChunkSize} bytes) - {ProgressPercentage:F1}% complete", 
            backupLogId, chunkIndex, chunkSize, progressPercentage);
    }

    /// <summary>
    /// 记录MySQL操作 / Log MySQL operation
    /// </summary>
    /// <param name="operation">操作名称 / Operation name</param>
    /// <param name="serviceName">服务名称 / Service name</param>
    /// <param name="success">是否成功 / Whether successful</param>
    /// <param name="errorMessage">错误消息（如果失败） / Error message (if failed)</param>
    public void LogMySqlOperation(string operation, string serviceName, bool success, string? errorMessage = null)
    {
        if (success)
        {
            _logger.LogInformation("MySQL operation '{Operation}' completed successfully for service '{ServiceName}'", 
                operation, serviceName);
        }
        else
        {
            _logger.LogError("MySQL operation '{Operation}' failed for service '{ServiceName}': {ErrorMessage}", 
                operation, serviceName, errorMessage);
        }
    }

    /// <summary>
    /// 记录压缩操作 / Log compression operation
    /// </summary>
    /// <param name="inputPath">输入路径 / Input path</param>
    /// <param name="outputPath">输出路径 / Output path</param>
    /// <param name="originalSize">原始大小 / Original size</param>
    /// <param name="compressedSize">压缩后大小 / Compressed size</param>
    /// <param name="duration">持续时间 / Duration</param>
    public void LogCompressionOperation(string inputPath, string outputPath, long originalSize, long compressedSize, TimeSpan duration)
    {
        var compressionRatio = originalSize > 0 ? (double)compressedSize / originalSize : 0;
        var compressionPercentage = (1 - compressionRatio) * 100;

        _logger.LogInformation("Compression completed: {InputPath} -> {OutputPath}. " +
            "Original: {OriginalSize} bytes, Compressed: {CompressedSize} bytes " +
            "({CompressionPercentage:F1}% reduction), Duration: {Duration:F2}s",
            inputPath, outputPath, originalSize, compressedSize, compressionPercentage, duration.TotalSeconds);
    }

    /// <summary>
    /// 记录网络操作 / Log network operation
    /// </summary>
    /// <param name="operation">操作名称 / Operation name</param>
    /// <param name="endpoint">端点地址 / Endpoint address</param>
    /// <param name="success">是否成功 / Whether successful</param>
    /// <param name="errorMessage">错误消息（如果失败） / Error message (if failed)</param>
    public void LogNetworkOperation(string operation, string endpoint, bool success, string? errorMessage = null)
    {
        if (success)
        {
            _logger.LogInformation("Network operation '{Operation}' completed successfully for endpoint '{Endpoint}'", 
                operation, endpoint);
        }
        else
        {
            _logger.LogError("Network operation '{Operation}' failed for endpoint '{Endpoint}': {ErrorMessage}", 
                operation, endpoint, errorMessage);
        }
    }
}