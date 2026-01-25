using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Decorator for IMySQLManager that adds timeout protection to all operations
/// </summary>
public class TimeoutProtectedMySQLManager : IMySQLManager, IBackupService
{
    private readonly IMySQLManager _innerManager;
    private readonly IErrorRecoveryManager _errorRecoveryManager;
    private readonly ILogger<TimeoutProtectedMySQLManager> _logger;

    public TimeoutProtectedMySQLManager(
        IMySQLManager innerManager,
        IErrorRecoveryManager errorRecoveryManager,
        ILogger<TimeoutProtectedMySQLManager> logger)
    {
        _innerManager = innerManager ?? throw new ArgumentNullException(nameof(innerManager));
        _errorRecoveryManager = errorRecoveryManager ?? throw new ArgumentNullException(nameof(errorRecoveryManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

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

    public async Task<bool> VerifyInstanceAvailabilityAsync(MySQLConnectionInfo connection)
    {
        return await VerifyInstanceAvailabilityAsync(connection, 30); // Default 30 second timeout
    }

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