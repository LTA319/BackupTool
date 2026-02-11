using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// IMySQLManager的装饰器，为所有操作添加超时保护 / Decorator for IMySQLManager that adds timeout protection to all operations
/// 使用错误恢复管理器提供MySQL服务操作的超时检测和恢复机制 / Uses error recovery manager to provide timeout detection and recovery mechanisms for MySQL service operations
/// </summary>
public class TimeoutProtectedMySQLManager : IMySQLManager, IBackupService
{
    private readonly IMySQLManager _innerManager; // 内部MySQL管理器 / Inner MySQL manager
    private readonly IErrorRecoveryManager _errorRecoveryManager; // 错误恢复管理器 / Error recovery manager
    private readonly ILogger<TimeoutProtectedMySQLManager> _logger;

    /// <summary>
    /// 构造函数，初始化超时保护MySQL管理器 / Constructor, initializes timeout-protected MySQL manager
    /// </summary>
    /// <param name="innerManager">内部MySQL管理器实现 / Inner MySQL manager implementation</param>
    /// <param name="errorRecoveryManager">错误恢复管理器 / Error recovery manager</param>
    /// <param name="logger">日志服务 / Logger service</param>
    public TimeoutProtectedMySQLManager(
        IMySQLManager innerManager,
        IErrorRecoveryManager errorRecoveryManager,
        ILogger<TimeoutProtectedMySQLManager> logger)
    {
        _innerManager = innerManager ?? throw new ArgumentNullException(nameof(innerManager));
        _errorRecoveryManager = errorRecoveryManager ?? throw new ArgumentNullException(nameof(errorRecoveryManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 带超时保护的MySQL实例停止操作 / MySQL instance stop operation with timeout protection
    /// 使用错误恢复管理器执行停止操作，提供超时检测和恢复机制 / Uses error recovery manager to execute stop operation with timeout detection and recovery mechanisms
    /// </summary>
    /// <param name="serviceName">MySQL服务名称 / MySQL service name</param>
    /// <returns>如果成功停止返回true，否则返回false / Returns true if successfully stopped, false otherwise</returns>
    public async Task<bool> StopInstanceAsync(string serviceName)
    {
        var operationId = Guid.NewGuid().ToString();
        
        try
        {
            _logger.LogDebug("Starting timeout-protected MySQL stop operation for service {ServiceName}", serviceName);

            return await _errorRecoveryManager.ExecuteWithTimeoutAsync(
                async (cancellationToken) => await _innerManager.StopInstanceAsync(serviceName),
                _errorRecoveryManager.Configuration.MySQLOperationTimeout,
                "MySQL Stop",
                operationId);
        }
        catch (OperationTimeoutException ex)
        {
            _logger.LogError(ex, "MySQL stop operation timed out for service {ServiceName}", serviceName);
            
            var mysqlException = new MySQLServiceException(operationId, serviceName, MySQLServiceOperation.Stop, 
                $"MySQL stop operation timed out after {ex.ActualDuration.TotalSeconds:F1} seconds", ex);
            
            var recoveryResult = 
                await _errorRecoveryManager.HandleMySQLServiceFailureAsync(
                    mysqlException,
                    cancellationToken: default,
                    mysqlManager: _innerManager);
            
            if (!recoveryResult.Success)
            {
                _logger.LogError("Recovery failed for MySQL stop timeout: {Message}", recoveryResult.Message);
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during timeout-protected MySQL stop for service {ServiceName}", serviceName);
            
            var mysqlException = new MySQLServiceException(operationId, serviceName, MySQLServiceOperation.Stop, 
                "Unexpected error during MySQL stop operation", ex);
            
            await _errorRecoveryManager.HandleMySQLServiceFailureAsync(
                mysqlException,
                cancellationToken: default,
                mysqlManager: _innerManager);
            return false;
        }
    }

    /// <summary>
    /// 带超时保护的MySQL实例启动操作 / MySQL instance start operation with timeout protection
    /// 使用错误恢复管理器执行启动操作，提供超时检测和恢复机制 / Uses error recovery manager to execute start operation with timeout detection and recovery mechanisms
    /// </summary>
    /// <param name="serviceName">MySQL服务名称 / MySQL service name</param>
    /// <returns>如果成功启动返回true，否则返回false / Returns true if successfully started, false otherwise</returns>
    public async Task<bool> StartInstanceAsync(string serviceName)
    {
        var operationId = Guid.NewGuid().ToString();
        
        try
        {
            _logger.LogDebug("Starting timeout-protected MySQL start operation for service {ServiceName}", serviceName);

            return await _errorRecoveryManager.ExecuteWithTimeoutAsync(
                async (cancellationToken) => await _innerManager.StartInstanceAsync(serviceName),
                _errorRecoveryManager.Configuration.MySQLOperationTimeout,
                "MySQL Start",
                operationId);
        }
        catch (OperationTimeoutException ex)
        {
            _logger.LogError(ex, "MySQL start operation timed out for service {ServiceName}", serviceName);
            
            var mysqlException = new MySQLServiceException(operationId, serviceName, MySQLServiceOperation.Start, 
                $"MySQL start operation timed out after {ex.ActualDuration.TotalSeconds:F1} seconds", ex);
            
            var recoveryResult = 
                await _errorRecoveryManager.HandleMySQLServiceFailureAsync(
                    mysqlException, 
                    cancellationToken: default,
                    mysqlManager: _innerManager);
            
            if (!recoveryResult.Success)
            {
                _logger.LogError("Recovery failed for MySQL start timeout: {Message}", recoveryResult.Message);
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during timeout-protected MySQL start for service {ServiceName}", serviceName);
            
            var mysqlException = new MySQLServiceException(operationId, serviceName, MySQLServiceOperation.Start, 
                "Unexpected error during MySQL start operation", ex);
            
            await _errorRecoveryManager.HandleMySQLServiceFailureAsync(
                mysqlException, 
                cancellationToken: default,
                mysqlManager: _innerManager);
            return false;
        }
    }

    /// <summary>
    /// 验证MySQL实例可用性（使用默认30秒超时） / Verifies MySQL instance availability (with default 30 second timeout)
    /// </summary>
    /// <param name="connection">MySQL连接信息 / MySQL connection information</param>
    /// <returns>如果实例可用返回true，否则返回false / Returns true if instance is available, false otherwise</returns>
    public async Task<bool> VerifyInstanceAvailabilityAsync(MySQLConnectionInfo connection)
    {
        return await VerifyInstanceAvailabilityAsync(connection, 30); // Default 30 second timeout
    }

    /// <summary>
    /// 带超时保护的MySQL实例可用性验证操作 / MySQL instance availability verification with timeout protection
    /// 使用指定的超时时间验证MySQL实例是否可用 / Verifies MySQL instance availability with specified timeout
    /// </summary>
    /// <param name="connection">MySQL连接信息 / MySQL connection information</param>
    /// <param name="timeoutSeconds">超时时间（秒） / Timeout in seconds</param>
    /// <returns>如果实例可用返回true，否则返回false / Returns true if instance is available, false otherwise</returns>
    public async Task<bool> VerifyInstanceAvailabilityAsync(MySQLConnectionInfo connection, int timeoutSeconds)
    {
        var operationId = Guid.NewGuid().ToString();
        
        try
        {
            _logger.LogDebug("Starting timeout-protected MySQL verification for service {ServiceName} with {TimeoutSeconds}s timeout", 
                connection.ServiceName, timeoutSeconds);

            var customTimeout = TimeSpan.FromSeconds(timeoutSeconds);
            
            return await _errorRecoveryManager.ExecuteWithTimeoutAsync(
                async (cancellationToken) => await _innerManager.VerifyInstanceAvailabilityAsync(connection, timeoutSeconds),
                customTimeout,
                "MySQL Verification",
                operationId);
        }
        catch (OperationTimeoutException ex)
        {
            _logger.LogError(ex, "MySQL verification timed out for service {ServiceName}", connection.ServiceName);
            
            var mysqlException = new MySQLServiceException(operationId, connection.ServiceName, MySQLServiceOperation.VerifyAvailability, 
                $"MySQL verification timed out after {ex.ActualDuration.TotalSeconds:F1} seconds", ex);
            
            var recoveryResult = 
                await _errorRecoveryManager.HandleMySQLServiceFailureAsync(
                    mysqlException, 
                    cancellationToken: default,
                    mysqlManager: _innerManager);
            
            if (!recoveryResult.Success)
            {
                _logger.LogError("Recovery failed for MySQL verification timeout: {Message}", recoveryResult.Message);
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during timeout-protected MySQL verification for service {ServiceName}", connection.ServiceName);
            
            var mysqlException = new MySQLServiceException(operationId, connection.ServiceName, MySQLServiceOperation.VerifyAvailability, 
                "Unexpected error during MySQL verification", ex);
            
            await _errorRecoveryManager.HandleMySQLServiceFailureAsync(
                mysqlException, 
                cancellationToken: default,
                mysqlManager: _innerManager);
            return false;
        }
    }
}