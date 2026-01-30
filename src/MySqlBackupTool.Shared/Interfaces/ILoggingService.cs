using Microsoft.Extensions.Logging;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 应用程序特定的日志服务接口 / Application-specific logging service interface
/// 提供结构化日志记录功能，包括不同级别的日志记录和备份操作专用的日志方法
/// Provides structured logging functionality including different log levels and backup operation-specific logging methods
/// </summary>
public interface ILoggingService
{
    /// <summary>
    /// 记录信息级别的消息 / Logs an information message
    /// 用于记录一般的操作信息和状态更新
    /// Used for recording general operation information and status updates
    /// </summary>
    /// <param name="message">要记录的消息模板 / Message template to log</param>
    /// <param name="args">消息模板的参数 / Parameters for the message template</param>
    void LogInformation(string message, params object[] args);

    /// <summary>
    /// 记录调试级别的消息 / Logs a debug message
    /// 用于记录详细的调试信息，通常在开发和故障排除时使用
    /// Used for recording detailed debug information, typically used during development and troubleshooting
    /// </summary>
    /// <param name="message">要记录的消息模板 / Message template to log</param>
    /// <param name="args">消息模板的参数 / Parameters for the message template</param>
    void LogDebug(string message, params object[] args);

    /// <summary>
    /// 记录警告级别的消息 / Logs a warning message
    /// 用于记录可能的问题或需要注意的情况
    /// Used for recording potential issues or situations that require attention
    /// </summary>
    /// <param name="message">要记录的消息模板 / Message template to log</param>
    /// <param name="args">消息模板的参数 / Parameters for the message template</param>
    void LogWarning(string message, params object[] args);

    /// <summary>
    /// 记录错误级别的消息 / Logs an error message
    /// 用于记录操作失败和错误情况
    /// Used for recording operation failures and error conditions
    /// </summary>
    /// <param name="message">要记录的消息模板 / Message template to log</param>
    /// <param name="args">消息模板的参数 / Parameters for the message template</param>
    void LogError(string message, params object[] args);

    /// <summary>
    /// 记录带有异常信息的错误消息 / Logs an error message with exception
    /// 用于记录包含异常详细信息的错误情况
    /// Used for recording error conditions with detailed exception information
    /// </summary>
    /// <param name="exception">相关的异常对象 / Related exception object</param>
    /// <param name="message">要记录的消息模板 / Message template to log</param>
    /// <param name="args">消息模板的参数 / Parameters for the message template</param>
    void LogError(Exception exception, string message, params object[] args);

    /// <summary>
    /// 记录关键级别的消息 / Logs a critical message
    /// 用于记录系统关键错误和严重问题
    /// Used for recording system critical errors and severe issues
    /// </summary>
    /// <param name="message">要记录的消息模板 / Message template to log</param>
    /// <param name="args">消息模板的参数 / Parameters for the message template</param>
    void LogCritical(string message, params object[] args);

    /// <summary>
    /// 记录带有异常信息的关键消息 / Logs a critical message with exception
    /// 用于记录包含异常详细信息的关键错误情况
    /// Used for recording critical error conditions with detailed exception information
    /// </summary>
    /// <param name="exception">相关的异常对象 / Related exception object</param>
    /// <param name="message">要记录的消息模板 / Message template to log</param>
    /// <param name="args">消息模板的参数 / Parameters for the message template</param>
    void LogCritical(Exception exception, string message, params object[] args);

    /// <summary>
    /// 检查指定的日志级别是否已启用 / Checks if a log level is enabled
    /// 用于在记录日志前检查是否需要执行昂贵的日志格式化操作
    /// Used to check if expensive log formatting operations need to be performed before logging
    /// </summary>
    /// <param name="logLevel">要检查的日志级别 / Log level to check</param>
    /// <returns>如果日志级别已启用返回true，否则返回false / True if log level is enabled, false otherwise</returns>
    bool IsEnabled(LogLevel logLevel);

    /// <summary>
    /// 创建日志作用域 / Creates a logging scope
    /// 用于为一组相关的日志条目创建上下文作用域
    /// Used to create contextual scope for a group of related log entries
    /// </summary>
    /// <typeparam name="TState">作用域状态的类型 / Type of scope state</typeparam>
    /// <param name="state">作用域状态对象 / Scope state object</param>
    /// <returns>可释放的作用域对象 / Disposable scope object</returns>
    IDisposable BeginScope<TState>(TState state) where TState : notnull;

    /// <summary>
    /// 记录带有事件ID的结构化消息 / Logs a structured message with event ID
    /// 提供完整的结构化日志记录功能，包括事件ID、状态和格式化器
    /// Provides complete structured logging functionality including event ID, state and formatter
    /// </summary>
    /// <typeparam name="TState">状态对象的类型 / Type of state object</typeparam>
    /// <param name="logLevel">日志级别 / Log level</param>
    /// <param name="eventId">事件标识符 / Event identifier</param>
    /// <param name="state">状态对象 / State object</param>
    /// <param name="exception">相关的异常，可选 / Related exception, optional</param>
    /// <param name="formatter">消息格式化器 / Message formatter</param>
    void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter);

    /// <summary>
    /// 记录备份操作开始 / Logs backup operation start
    /// 专用于记录备份操作的开始，包括配置ID和可选的恢复令牌
    /// Specifically for logging backup operation start, including configuration ID and optional resume token
    /// </summary>
    /// <param name="configurationId">备份配置的ID / Backup configuration ID</param>
    /// <param name="resumeToken">恢复令牌，可选 / Resume token, optional</param>
    void LogBackupStart(int configurationId, string? resumeToken = null);

    /// <summary>
    /// 记录备份操作完成 / Logs backup operation completion
    /// 专用于记录备份操作的完成状态，包括成功状态、文件信息和错误消息
    /// Specifically for logging backup operation completion status, including success status, file information and error messages
    /// </summary>
    /// <param name="backupLogId">备份日志的ID / Backup log ID</param>
    /// <param name="success">操作是否成功 / Whether operation was successful</param>
    /// <param name="filePath">备份文件路径，可选 / Backup file path, optional</param>
    /// <param name="fileSize">文件大小，可选 / File size, optional</param>
    /// <param name="errorMessage">错误消息，可选 / Error message, optional</param>
    void LogBackupComplete(int backupLogId, bool success, string? filePath = null, long? fileSize = null, string? errorMessage = null);

    /// <summary>
    /// 记录文件传输进度 / Logs file transfer progress
    /// 专用于记录文件传输的进度信息，包括分块索引、大小和进度百分比
    /// Specifically for logging file transfer progress information, including chunk index, size and progress percentage
    /// </summary>
    /// <param name="backupLogId">备份日志的ID / Backup log ID</param>
    /// <param name="chunkIndex">分块索引 / Chunk index</param>
    /// <param name="chunkSize">分块大小 / Chunk size</param>
    /// <param name="progressPercentage">进度百分比 / Progress percentage</param>
    void LogTransferProgress(int backupLogId, int chunkIndex, long chunkSize, double progressPercentage);

    /// <summary>
    /// 记录MySQL服务操作 / Logs MySQL service operation
    /// 专用于记录MySQL服务相关的操作，包括操作类型、服务名称和结果
    /// Specifically for logging MySQL service-related operations, including operation type, service name and result
    /// </summary>
    /// <param name="operation">操作类型 / Operation type</param>
    /// <param name="serviceName">服务名称 / Service name</param>
    /// <param name="success">操作是否成功 / Whether operation was successful</param>
    /// <param name="errorMessage">错误消息，可选 / Error message, optional</param>
    void LogMySqlOperation(string operation, string serviceName, bool success, string? errorMessage = null);

    /// <summary>
    /// 记录压缩操作 / Logs compression operation
    /// 专用于记录文件压缩操作的详细信息，包括路径、大小和耗时
    /// Specifically for logging detailed compression operation information, including paths, sizes and duration
    /// </summary>
    /// <param name="inputPath">输入文件路径 / Input file path</param>
    /// <param name="outputPath">输出文件路径 / Output file path</param>
    /// <param name="originalSize">原始文件大小 / Original file size</param>
    /// <param name="compressedSize">压缩后文件大小 / Compressed file size</param>
    /// <param name="duration">压缩耗时 / Compression duration</param>
    void LogCompressionOperation(string inputPath, string outputPath, long originalSize, long compressedSize, TimeSpan duration);

    /// <summary>
    /// 记录网络操作 / Logs network operation
    /// 专用于记录网络相关的操作，包括操作类型、端点和结果
    /// Specifically for logging network-related operations, including operation type, endpoint and result
    /// </summary>
    /// <param name="operation">操作类型 / Operation type</param>
    /// <param name="endpoint">网络端点 / Network endpoint</param>
    /// <param name="success">操作是否成功 / Whether operation was successful</param>
    /// <param name="errorMessage">错误消息，可选 / Error message, optional</param>
    void LogNetworkOperation(string operation, string endpoint, bool success, string? errorMessage = null);
}