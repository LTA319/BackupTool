using System.Diagnostics;
using System.ServiceProcess;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Windows service-based MySQL instance manager
/// </summary>
public class MySQLManager : IMySQLManager, IBackupService
{
    private readonly ILogger<MySQLManager> _logger;
    private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _serviceOperationTimeout = TimeSpan.FromSeconds(60);

    public MySQLManager(ILogger<MySQLManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Stops a MySQL service instance with timeout and error recovery
    /// </summary>
    public async Task<bool> StopInstanceAsync(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            _logger.LogError("Service name cannot be null or empty");
            return false;
        }

        _logger.LogInformation("Attempting to stop MySQL service: {ServiceName}", serviceName);

        try
        {
            using var service = new ServiceController(serviceName);
            
            // Check if service exists
            try
            {
                var status = service.Status;
                _logger.LogDebug("Current service status: {Status}", status);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "MySQL service '{ServiceName}' not found", serviceName);
                return false;
            }

            // If already stopped, return success
            if (service.Status == ServiceControllerStatus.Stopped)
            {
                _logger.LogInformation("MySQL service '{ServiceName}' is already stopped", serviceName);
                return true;
            }

            // If stopping, wait for it to complete
            if (service.Status == ServiceControllerStatus.StopPending)
            {
                _logger.LogInformation("MySQL service '{ServiceName}' is already stopping, waiting for completion", serviceName);
                await WaitForServiceStatusAsync(service, ServiceControllerStatus.Stopped, _serviceOperationTimeout);
                return service.Status == ServiceControllerStatus.Stopped;
            }

            // Stop the service
            service.Stop();
            _logger.LogInformation("Stop command sent to MySQL service '{ServiceName}'", serviceName);

            // Wait for the service to stop
            await WaitForServiceStatusAsync(service, ServiceControllerStatus.Stopped, _serviceOperationTimeout);

            if (service.Status == ServiceControllerStatus.Stopped)
            {
                _logger.LogInformation("MySQL service '{ServiceName}' stopped successfully", serviceName);
                return true;
            }
            else
            {
                _logger.LogError("MySQL service '{ServiceName}' failed to stop within timeout. Current status: {Status}", 
                    serviceName, service.Status);
                return false;
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to stop MySQL service '{ServiceName}': Service not found or access denied", serviceName);
            return false;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogError(ex, "Failed to stop MySQL service '{ServiceName}': Windows error", serviceName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error stopping MySQL service '{ServiceName}'", serviceName);
            return false;
        }
    }

    /// <summary>
    /// Starts a MySQL service instance with timeout and error recovery
    /// </summary>
    public async Task<bool> StartInstanceAsync(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            _logger.LogError("Service name cannot be null or empty");
            return false;
        }

        _logger.LogInformation("Attempting to start MySQL service: {ServiceName}", serviceName);

        try
        {
            using var service = new ServiceController(serviceName);
            
            // Check if service exists
            try
            {
                var status = service.Status;
                _logger.LogDebug("Current service status: {Status}", status);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "MySQL service '{ServiceName}' not found", serviceName);
                return false;
            }

            // If already running, return success
            if (service.Status == ServiceControllerStatus.Running)
            {
                _logger.LogInformation("MySQL service '{ServiceName}' is already running", serviceName);
                return true;
            }

            // If starting, wait for it to complete
            if (service.Status == ServiceControllerStatus.StartPending)
            {
                _logger.LogInformation("MySQL service '{ServiceName}' is already starting, waiting for completion", serviceName);
                await WaitForServiceStatusAsync(service, ServiceControllerStatus.Running, _serviceOperationTimeout);
                return service.Status == ServiceControllerStatus.Running;
            }

            // Start the service
            service.Start();
            _logger.LogInformation("Start command sent to MySQL service '{ServiceName}'", serviceName);

            // Wait for the service to start
            await WaitForServiceStatusAsync(service, ServiceControllerStatus.Running, _serviceOperationTimeout);

            if (service.Status == ServiceControllerStatus.Running)
            {
                _logger.LogInformation("MySQL service '{ServiceName}' started successfully", serviceName);
                return true;
            }
            else
            {
                _logger.LogError("MySQL service '{ServiceName}' failed to start within timeout. Current status: {Status}", 
                    serviceName, service.Status);
                return false;
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to start MySQL service '{ServiceName}': Service not found or access denied", serviceName);
            return false;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogError(ex, "Failed to start MySQL service '{ServiceName}': Windows error", serviceName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error starting MySQL service '{ServiceName}'", serviceName);
            return false;
        }
    }

    /// <summary>
    /// Verifies that a MySQL instance is available and accepting connections
    /// </summary>
    public async Task<bool> VerifyInstanceAvailabilityAsync(MySQLConnectionInfo connection)
    {
        return await VerifyInstanceAvailabilityAsync(connection, 30); // Default 30 second timeout
    }

    /// <summary>
    /// Verifies that a MySQL instance is available and accepting connections with configurable timeout
    /// </summary>
    public async Task<bool> VerifyInstanceAvailabilityAsync(MySQLConnectionInfo connection, int timeoutSeconds)
    {
        if (connection == null)
        {
            _logger.LogError("Connection information cannot be null");
            return false;
        }

        if (timeoutSeconds <= 0)
        {
            _logger.LogError("Timeout must be greater than 0 seconds");
            return false;
        }

        _logger.LogInformation("Verifying MySQL instance availability for {Host}:{Port} with {Timeout}s timeout", 
            connection.Host, connection.Port, timeoutSeconds);

        try
        {
            // First, validate the connection configuration
            var validationResults = connection.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(connection));
            if (validationResults.Any())
            {
                _logger.LogError("Invalid connection configuration: {Errors}", 
                    string.Join(", ", validationResults.Select(r => r.ErrorMessage)));
                return false;
            }

            // Test the connection with retry logic and configurable timeout
            const int maxRetries = 3;
            const int baseDelayMs = 1000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logger.LogDebug("Connection attempt {Attempt}/{MaxRetries} with {Timeout}s timeout", 
                        attempt, maxRetries, timeoutSeconds);
                    
                    var (isValid, errors) = await connection.ValidateConnectionAsync(timeoutSeconds);
                    
                    if (isValid)
                    {
                        _logger.LogInformation("MySQL instance availability verified successfully");
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("Connection validation failed on attempt {Attempt}: {Errors}", 
                            attempt, string.Join(", ", errors));
                        
                        if (attempt < maxRetries)
                        {
                            var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt - 1));
                            _logger.LogDebug("Waiting {Delay}ms before retry", delay.TotalMilliseconds);
                            await Task.Delay(delay);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Connection attempt {Attempt} failed with exception", attempt);
                    
                    if (attempt < maxRetries)
                    {
                        var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt - 1));
                        await Task.Delay(delay);
                    }
                }
            }

            _logger.LogError("Failed to verify MySQL instance availability after {MaxRetries} attempts", maxRetries);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error verifying MySQL instance availability");
            return false;
        }
    }

    /// <summary>
    /// Waits for a service to reach the specified status within the given timeout
    /// </summary>
    private async Task WaitForServiceStatusAsync(ServiceController service, ServiceControllerStatus targetStatus, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                service.Refresh();
                if (service.Status == targetStatus)
                {
                    return;
                }
                
                await Task.Delay(500); // Check every 500ms
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking service status during wait");
                break;
            }
        }
        
        _logger.LogWarning("Timeout waiting for service to reach status {TargetStatus}. Current status: {CurrentStatus}", 
            targetStatus, service.Status);
    }
}