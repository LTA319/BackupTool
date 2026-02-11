using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 管理错误恢复和处理策略的接口 / Interface for managing error recovery and handling strategies
/// 提供各种类型错误的恢复机制，包括MySQL服务错误、压缩错误、传输错误、超时错误等
/// Provides recovery mechanisms for various types of errors including MySQL service errors, compression errors, transfer errors, timeout errors, etc.
/// </summary>
public interface IErrorRecoveryManager
{
    /// <summary>
    /// 处理MySQL服务操作失败 / Handles MySQL service operation failures
    /// 当MySQL服务出现连接失败、查询超时、权限错误等问题时，尝试进行恢复操作
    /// Attempts recovery operations when MySQL service encounters connection failures, query timeouts, permission errors, etc.
    /// </summary>
    /// <param name="error">发生的MySQL服务异常 / The MySQL service exception that occurred</param>
    /// <param name="cancellationToken">恢复操作的取消令牌 / Cancellation token for the recovery operation</param>
    /// <param name="mysqlManager">MySQL管理器实例，可选参数 / MySQL manager instance, optional parameter</param>
    /// <returns>恢复尝试的结果 / Result of the recovery attempt</returns>
    Task<RecoveryResult> HandleMySQLServiceFailureAsync(MySQLServiceException error, CancellationToken cancellationToken = default, IMySQLManager? mysqlManager = null);

    /// <summary>
    /// 处理文件压缩操作失败 / Handles file compression operation failures
    /// 当压缩操作出现磁盘空间不足、文件访问权限问题、压缩算法错误等情况时进行恢复
    /// Performs recovery when compression operations encounter insufficient disk space, file access permission issues, compression algorithm errors, etc.
    /// </summary>
    /// <param name="error">发生的压缩异常 / The compression exception that occurred</param>
    /// <param name="cancellationToken">恢复操作的取消令牌 / Cancellation token for the recovery operation</param>
    /// <param name="compressionService">压缩服务实例，可选参数 / Compression service instance, optional parameter</param>
    /// <returns>恢复尝试的结果 / Result of the recovery attempt</returns>
    Task<RecoveryResult> HandleCompressionFailureAsync(CompressionException error, CancellationToken cancellationToken = default, ICompressionService? compressionService = null);

    /// <summary>
    /// 处理文件传输操作失败 / Handles file transfer operation failures
    /// 当文件传输遇到网络中断、服务器不可达、传输超时等问题时执行恢复策略
    /// Executes recovery strategies when file transfer encounters network interruptions, server unreachable, transfer timeouts, etc.
    /// </summary>
    /// <param name="error">发生的传输异常 / The transfer exception that occurred</param>
    /// <param name="cancellationToken">恢复操作的取消令牌 / Cancellation token for the recovery operation</param>
    /// <returns>恢复尝试的结果 / Result of the recovery attempt</returns>
    Task<RecoveryResult> HandleTransferFailureAsync(TransferException error, CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理操作超时失败 / Handles operation timeout failures
    /// 当操作执行时间超过预设阈值时，尝试重新执行或调整超时设置
    /// Attempts to re-execute or adjust timeout settings when operation execution time exceeds preset threshold
    /// </summary>
    /// <param name="error">发生的超时异常 / The timeout exception that occurred</param>
    /// <param name="cancellationToken">恢复操作的取消令牌 / Cancellation token for the recovery operation</param>
    /// <returns>恢复尝试的结果 / Result of the recovery attempt</returns>
    Task<RecoveryResult> HandleTimeoutFailureAsync(OperationTimeoutException error, CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理一般备份操作失败 / Handles general backup operation failures
    /// 处理不属于特定类型的备份操作错误，提供通用的错误恢复机制
    /// Handles backup operation errors that don't belong to specific types, provides generic error recovery mechanisms
    /// </summary>
    /// <param name="error">发生的备份异常 / The backup exception that occurred</param>
    /// <param name="cancellationToken">恢复操作的取消令牌 / Cancellation token for the recovery operation</param>
    /// <returns>恢复尝试的结果 / Result of the recovery attempt</returns>
    Task<RecoveryResult> HandleGeneralFailureAsync(BackupException error, CancellationToken cancellationToken = default);

    /// <summary>
    /// 向配置的接收者发送关键错误警报 / Sends critical error alerts to configured recipients
    /// 当发生严重错误时，通过邮件、短信、推送通知等方式发送警报信息
    /// Sends alert information via email, SMS, push notifications, etc. when critical errors occur
    /// </summary>
    /// <param name="alert">要发送的关键错误警报 / The critical error alert to send</param>
    /// <param name="cancellationToken">警报操作的取消令牌 / Cancellation token for the alert operation</param>
    /// <returns>如果警报发送成功返回true，否则返回false / True if alert was sent successfully, false otherwise</returns>
    Task<bool> SendCriticalErrorAlertAsync(CriticalErrorAlert alert, CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行带有超时保护的操作 / Executes an operation with timeout protection
    /// 在指定的超时时间内执行操作，如果超时则取消操作并抛出超时异常
    /// Executes operation within specified timeout period, cancels operation and throws timeout exception if exceeded
    /// </summary>
    /// <typeparam name="T">操作的返回类型 / Return type of the operation</typeparam>
    /// <param name="operation">要执行的操作 / The operation to execute</param>
    /// <param name="timeout">操作允许的最大时间 / Maximum time allowed for the operation</param>
    /// <param name="operationType">操作类型，用于错误报告 / Type of operation for error reporting</param>
    /// <param name="operationId">操作的唯一标识符 / Unique identifier for the operation</param>
    /// <param name="cancellationToken">操作的取消令牌 / Cancellation token for the operation</param>
    /// <returns>操作的结果 / Result of the operation</returns>
    Task<T> ExecuteWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        string operationType,
        string operationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行带有超时保护的操作（无返回值） / Executes an operation with timeout protection (void return)
    /// 在指定的超时时间内执行无返回值的操作，如果超时则取消操作
    /// Executes void operation within specified timeout period, cancels operation if exceeded
    /// </summary>
    /// <param name="operation">要执行的操作 / The operation to execute</param>
    /// <param name="timeout">操作允许的最大时间 / Maximum time allowed for the operation</param>
    /// <param name="operationType">操作类型，用于错误报告 / Type of operation for error reporting</param>
    /// <param name="operationId">操作的唯一标识符 / Unique identifier for the operation</param>
    /// <param name="cancellationToken">操作的取消令牌 / Cancellation token for the operation</param>
    Task ExecuteWithTimeoutAsync(
        Func<CancellationToken, Task> operation,
        TimeSpan timeout,
        string operationType,
        string operationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在错误发生后清理临时文件和资源 / Cleans up temporary files and resources after an error
    /// 删除操作过程中产生的临时文件，释放占用的系统资源
    /// Deletes temporary files generated during operations and releases occupied system resources
    /// </summary>
    /// <param name="operationId">操作的唯一标识符 / Unique identifier for the operation</param>
    /// <param name="filePaths">要清理的文件路径列表 / List of file paths to clean up</param>
    /// <param name="cancellationToken">清理操作的取消令牌 / Cancellation token for the cleanup operation</param>
    /// <param name="compressionService">压缩服务实例，可选参数 / Compression service instance, optional parameter</param>
    /// <returns>如果清理成功返回true，否则返回false / True if cleanup was successful, false otherwise</returns>
    Task<bool> CleanupAfterErrorAsync(string operationId, IEnumerable<string> filePaths, CancellationToken cancellationToken = default, ICompressionService? compressionService = null);

    /// <summary>
    /// 获取当前的错误恢复配置 / Gets the current error recovery configuration
    /// 返回当前使用的错误恢复策略配置，包括重试次数、超时设置、警报配置等
    /// Returns currently used error recovery strategy configuration including retry counts, timeout settings, alert configuration, etc.
    /// </summary>
    ErrorRecoveryConfig Configuration { get; }

    /// <summary>
    /// 更新错误恢复配置 / Updates the error recovery configuration
    /// 设置新的错误恢复策略配置，立即生效并应用于后续的错误处理操作
    /// Sets new error recovery strategy configuration, takes effect immediately and applies to subsequent error handling operations
    /// </summary>
    /// <param name="config">新的配置设置 / New configuration settings</param>
    void UpdateConfiguration(ErrorRecoveryConfig config);
}