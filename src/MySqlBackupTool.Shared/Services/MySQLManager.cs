using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 基于Windows服务的MySQL实例管理器
/// 提供MySQL服务的启动、停止和连接验证功能
/// </summary>
public class MySQLManager : IMySQLManager, IBackupService
{
    private readonly ILogger<MySQLManager> _logger;
    private readonly IMemoryProfiler? _memoryProfiler;
    
    // 服务操作超时时间
    private const int DefaultTimeoutSeconds = 30;
    private const int ServiceOperationTimeoutSeconds = 60;
    
    // 连接重试配置
    private const int MaxConnectionRetries = 3;
    private const int BaseRetryDelayMs = 1000;
    private const int StatusCheckIntervalMs = 500;
    
    private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);
    private readonly TimeSpan _serviceOperationTimeout = TimeSpan.FromSeconds(ServiceOperationTimeoutSeconds);

    /// <summary>
    /// 构造函数，初始化MySQL管理器
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="memoryProfiler">内存分析器（可选）</param>
    public MySQLManager(ILogger<MySQLManager> logger, IMemoryProfiler? memoryProfiler = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryProfiler = memoryProfiler;
    }

    /// <summary>
    /// 停止MySQL服务实例，支持超时和错误恢复
    /// </summary>
    /// <param name="serviceName">要停止的服务名称</param>
    /// <returns>如果成功停止返回true，否则返回false</returns>
    public async Task<bool> StopInstanceAsync(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            _logger.LogError("Service name cannot be null or empty");
            return false;
        }

        var operationId = $"stop-{serviceName}-{Guid.NewGuid():N}";
        _memoryProfiler?.StartProfiling(operationId, "MySQL-Stop");
        _memoryProfiler?.RecordSnapshot(operationId, "Start", $"Starting MySQL stop for service: {serviceName}");

        _logger.LogInformation("Attempting to stop MySQL service: {ServiceName}", serviceName);

        try
        {
            // 首先尝试使用ServiceController
            var result = await StopUsingServiceControllerAsync(serviceName, operationId);
            if (result)
            {
                _memoryProfiler?.StopProfiling(operationId);
                return true;
            }

            // 如果ServiceController失败，尝试命令行方式
            _logger.LogWarning("ServiceController method failed, trying command line method for service: {ServiceName}", serviceName);
            _memoryProfiler?.RecordSnapshot(operationId, "Fallback", "Falling back to command line method");

            result = await StopUsingCommandLineAsync(serviceName, operationId);

            _memoryProfiler?.StopProfiling(operationId);
            return result;

            //using var service = new ServiceController(serviceName);
            
            //// 检查服务是否存在
            //try
            //{
            //    var status = service.Status;
            //    _logger.LogDebug("Current service status: {Status}", status);
            //    _memoryProfiler?.RecordSnapshot(operationId, "StatusCheck", $"Service status: {status}");
            //}
            //catch (InvalidOperationException ex)
            //{
            //    _logger.LogError(ex, "MySQL service '{ServiceName}' not found", serviceName);
            //    _memoryProfiler?.RecordSnapshot(operationId, "Error", "Service not found");
            //    _memoryProfiler?.StopProfiling(operationId);
            //    return false;
            //}

            //// 如果已经停止，返回成功
            //if (service.Status == ServiceControllerStatus.Stopped)
            //{
            //    _logger.LogInformation("MySQL service '{ServiceName}' is already stopped", serviceName);
            //    _memoryProfiler?.RecordSnapshot(operationId, "AlreadyStopped", "Service already in stopped state");
            //    _memoryProfiler?.StopProfiling(operationId);
            //    return true;
            //}

            //// 如果正在停止，等待完成
            //if (service.Status == ServiceControllerStatus.StopPending)
            //{
            //    _logger.LogInformation("MySQL service '{ServiceName}' is already stopping, waiting for completion", serviceName);
            //    _memoryProfiler?.RecordSnapshot(operationId, "WaitingForStop", "Service is stopping, waiting for completion");
            //    await WaitForServiceStatusAsync(service, ServiceControllerStatus.Stopped, _serviceOperationTimeout);
            //    var result = service.Status == ServiceControllerStatus.Stopped;
            //    _memoryProfiler?.RecordSnapshot(operationId, "WaitComplete", $"Wait completed, success: {result}");
            //    _memoryProfiler?.StopProfiling(operationId);
            //    return result;
            //}

            //// 停止服务
            //_memoryProfiler?.RecordSnapshot(operationId, "SendStopCommand", "Sending stop command to service");
            //service.Stop();
            //_logger.LogInformation("Stop command sent to MySQL service '{ServiceName}'", serviceName);

            //// 等待服务停止
            //_memoryProfiler?.RecordSnapshot(operationId, "WaitingForStop", "Waiting for service to stop");
            //await WaitForServiceStatusAsync(service, ServiceControllerStatus.Stopped, _serviceOperationTimeout);

            //if (service.Status == ServiceControllerStatus.Stopped)
            //{
            //    _logger.LogInformation("MySQL service '{ServiceName}' stopped successfully", serviceName);
            //    _memoryProfiler?.RecordSnapshot(operationId, "Success", "Service stopped successfully");
            //    _memoryProfiler?.StopProfiling(operationId);
            //    return true;
            //}
            //else
            //{
            //    _logger.LogError("MySQL service '{ServiceName}' failed to stop within timeout. Current status: {Status}", 
            //        serviceName, service.Status);
            //    _memoryProfiler?.RecordSnapshot(operationId, "Timeout", $"Stop timeout, status: {service.Status}");
            //    _memoryProfiler?.StopProfiling(operationId);
            //    return false;
            //}
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to stop MySQL service '{ServiceName}': Service not found or access denied", serviceName);
            _memoryProfiler?.RecordSnapshot(operationId, "Exception", $"InvalidOperationException: {ex.Message}");
            _memoryProfiler?.StopProfiling(operationId);
            return false;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogError(ex, "Failed to stop MySQL service '{ServiceName}': Windows error", serviceName);
            _memoryProfiler?.RecordSnapshot(operationId, "Exception", $"Win32Exception: {ex.Message}");
            _memoryProfiler?.StopProfiling(operationId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error stopping MySQL service '{ServiceName}'", serviceName);
            _memoryProfiler?.RecordSnapshot(operationId, "Exception", $"Unexpected error: {ex.Message}");
            _memoryProfiler?.StopProfiling(operationId);
            return false;
        }
    }

    private async Task<bool> StopUsingServiceControllerAsync(string serviceName, string operationId)
    {
        try
        {
            using var service = new ServiceController(serviceName);

            // 检查服务是否存在
            try
            {
                var status = service.Status;
                _logger.LogDebug("Current service status: {Status}", status);
                _memoryProfiler?.RecordSnapshot(operationId, "StatusCheck", $"Service status: {status}");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "MySQL service '{ServiceName}' not found", serviceName);
                _memoryProfiler?.RecordSnapshot(operationId, "Error", "Service not found");
                return false;
            }

            // 如果已经停止，返回成功
            if (service.Status == ServiceControllerStatus.Stopped)
            {
                _logger.LogInformation("MySQL service '{ServiceName}' is already stopped", serviceName);
                _memoryProfiler?.RecordSnapshot(operationId, "AlreadyStopped", "Service already in stopped state");
                return true;
            }

            // 检查权限
            if (!HasPermissionToControlService(serviceName))
            {
                _logger.LogError("Insufficient permissions to control service: {ServiceName}", serviceName);
                _memoryProfiler?.RecordSnapshot(operationId, "Error", "Insufficient permissions");
                return false;
            }

            // 停止服务
            _memoryProfiler?.RecordSnapshot(operationId, "SendStopCommand", "Sending stop command to service");
            service.Stop();
            _logger.LogInformation("Stop command sent to MySQL service '{ServiceName}'", serviceName);

            // 等待服务停止
            _memoryProfiler?.RecordSnapshot(operationId, "WaitingForStop", "Waiting for service to stop");
            await WaitForServiceStatusAsync(service, ServiceControllerStatus.Stopped, _serviceOperationTimeout);

            if (service.Status == ServiceControllerStatus.Stopped)
            {
                _logger.LogInformation("MySQL service '{ServiceName}' stopped successfully", serviceName);
                _memoryProfiler?.RecordSnapshot(operationId, "Success", "Service stopped successfully");
                return true;
            }
            else
            {
                _logger.LogError("MySQL service '{ServiceName}' failed to stop within timeout. Current status: {Status}",
                    serviceName, service.Status);
                _memoryProfiler?.RecordSnapshot(operationId, "Timeout", $"Stop timeout, status: {service.Status}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ServiceController method failed for service '{ServiceName}'", serviceName);
            _memoryProfiler?.RecordSnapshot(operationId, "Exception", $"{ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> StopUsingCommandLineAsync(string serviceName, string operationId)
    {
        try
        {
            _logger.LogInformation("Attempting to stop service via command line: {ServiceName}", serviceName);
            _memoryProfiler?.RecordSnapshot(operationId, "CommandLineStart", "Starting command line stop");

            // 方法1: 使用net stop
            var result = await StopServiceWithNetStop(serviceName, operationId);
            if (result)
            {
                return true;
            }

            // 方法2: 使用sc stop
            result = await StopServiceWithScStop(serviceName, operationId);
            if (result)
            {
                return true;
            }

            // 方法3: 使用PowerShell
            result = await StopServiceWithPowerShell(serviceName, operationId);
            if (result)
            {
                return true;
            }

            _logger.LogError("All command line methods failed to stop service: {ServiceName}", serviceName);
            _memoryProfiler?.RecordSnapshot(operationId, "AllMethodsFailed", "All command line methods failed");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command line method failed for service '{ServiceName}'", serviceName);
            _memoryProfiler?.RecordSnapshot(operationId, "CommandLineException", $"{ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
    private async Task<bool> StopServiceWithNetStop(string serviceName, string operationId)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "net",
                Arguments = $"stop \"{serviceName}\" /y", // /y参数表示跳过确认
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _logger.LogDebug("Executing: {FileName} {Arguments}", processInfo.FileName, processInfo.Arguments);
            _memoryProfiler?.RecordSnapshot(operationId, "NetStop", $"Executing: net stop {serviceName}");

            var output = new StringBuilder();
            var error = new StringBuilder();

            using (var process = new Process { StartInfo = processInfo })
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.AppendLine(e.Data);
                        _logger.LogDebug("net stop output: {Output}", e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        error.AppendLine(e.Data);
                        _logger.LogDebug("net stop error: {Error}", e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var completed = await Task.Run(() => process.WaitForExit(30000)); // 30秒超时

                if (!completed)
                {
                    _logger.LogWarning("net stop timed out for service: {ServiceName}", serviceName);
                    process.Kill();
                    return false;
                }

                if (process.ExitCode == 0)
                {
                    _logger.LogInformation("net stop successful for service: {ServiceName}", serviceName);
                    _memoryProfiler?.RecordSnapshot(operationId, "NetStopSuccess", "net stop succeeded");

                    // 验证服务确实停止了
                    await Task.Delay(2000); // 等待2秒让服务完全停止
                    return await IsServiceStopped(serviceName, operationId);
                }
                else
                {
                    _logger.LogWarning("net stop failed with exit code {ExitCode} for service: {ServiceName}. Output: {Output}",
                        process.ExitCode, serviceName, output.ToString());
                    _memoryProfiler?.RecordSnapshot(operationId, "NetStopFailed", $"Exit code: {process.ExitCode}");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing net stop for service: {ServiceName}", serviceName);
            _memoryProfiler?.RecordSnapshot(operationId, "NetStopError", ex.Message);
            return false;
        }
    }

    private async Task<bool> StopServiceWithScStop(string serviceName, string operationId)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "sc",
                Arguments = $"stop \"{serviceName}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _logger.LogDebug("Executing: {FileName} {Arguments}", processInfo.FileName, processInfo.Arguments);
            _memoryProfiler?.RecordSnapshot(operationId, "ScStop", $"Executing: sc stop {serviceName}");

            var output = new StringBuilder();
            var error = new StringBuilder();

            using (var process = new Process { StartInfo = processInfo })
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.AppendLine(e.Data);
                        _logger.LogDebug("sc stop output: {Output}", e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        error.AppendLine(e.Data);
                        _logger.LogDebug("sc stop error: {Error}", e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var completed = await Task.Run(() => process.WaitForExit(30000));

                if (!completed)
                {
                    _logger.LogWarning("sc stop timed out for service: {ServiceName}", serviceName);
                    process.Kill();
                    return false;
                }

                if (process.ExitCode == 0 || output.ToString().Contains("SERVICE_STOPPED", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("sc stop successful for service: {ServiceName}", serviceName);
                    _memoryProfiler?.RecordSnapshot(operationId, "ScStopSuccess", "sc stop succeeded");

                    await Task.Delay(2000);
                    return await IsServiceStopped(serviceName, operationId);
                }
                else
                {
                    _logger.LogWarning("sc stop failed with exit code {ExitCode} for service: {ServiceName}. Output: {Output}",
                        process.ExitCode, serviceName, output.ToString());
                    _memoryProfiler?.RecordSnapshot(operationId, "ScStopFailed", $"Exit code: {process.ExitCode}");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing sc stop for service: {ServiceName}", serviceName);
            _memoryProfiler?.RecordSnapshot(operationId, "ScStopError", ex.Message);
            return false;
        }
    }

    private async Task<bool> StopServiceWithPowerShell(string serviceName, string operationId)
    {
        try
        {
            var command = $"-Command \"Stop-Service -Name '{serviceName}' -Force -ErrorAction Stop\"";

            var processInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = command,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _logger.LogDebug("Executing PowerShell: Stop-Service -Name '{ServiceName}' -Force", serviceName);
            _memoryProfiler?.RecordSnapshot(operationId, "PowerShellStop", $"Executing: Stop-Service {serviceName}");

            var output = new StringBuilder();
            var error = new StringBuilder();

            using (var process = new Process { StartInfo = processInfo })
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.AppendLine(e.Data);
                        _logger.LogDebug("PowerShell output: {Output}", e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        error.AppendLine(e.Data);
                        _logger.LogDebug("PowerShell error: {Error}", e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var completed = await Task.Run(() => process.WaitForExit(30000));

                if (!completed)
                {
                    _logger.LogWarning("PowerShell stop timed out for service: {ServiceName}", serviceName);
                    process.Kill();
                    return false;
                }

                if (process.ExitCode == 0)
                {
                    _logger.LogInformation("PowerShell stop successful for service: {ServiceName}", serviceName);
                    _memoryProfiler?.RecordSnapshot(operationId, "PowerShellSuccess", "PowerShell succeeded");

                    await Task.Delay(2000);
                    return await IsServiceStopped(serviceName, operationId);
                }
                else
                {
                    _logger.LogWarning("PowerShell stop failed with exit code {ExitCode} for service: {ServiceName}. Error: {Error}",
                        process.ExitCode, serviceName, error.ToString());
                    _memoryProfiler?.RecordSnapshot(operationId, "PowerShellFailed", $"Exit code: {process.ExitCode}");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing PowerShell stop for service: {ServiceName}", serviceName);
            _memoryProfiler?.RecordSnapshot(operationId, "PowerShellError", ex.Message);
            return false;
        }
    }
    private async Task<bool> IsServiceStopped(string serviceName, string operationId)
    {
        try
        {
            for (int i = 0; i < 5; i++) // 最多重试5次
            {
                using var service = new ServiceController(serviceName);
                if (service.Status == ServiceControllerStatus.Stopped)
                {
                    _logger.LogDebug("Service '{ServiceName}' is confirmed stopped (attempt {Attempt})",
                        serviceName, i + 1);
                    _memoryProfiler?.RecordSnapshot(operationId, "VerifiedStopped", $"Verified on attempt {i + 1}");
                    return true;
                }

                _logger.LogDebug("Service '{ServiceName}' status: {Status} (attempt {Attempt})",
                    serviceName, service.Status, i + 1);
                await Task.Delay(1000);
            }

            _logger.LogWarning("Service '{ServiceName}' is not in stopped state after verification attempts", serviceName);
            _memoryProfiler?.RecordSnapshot(operationId, "VerificationFailed", "Service not confirmed as stopped");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying service status for: {ServiceName}", serviceName);
            _memoryProfiler?.RecordSnapshot(operationId, "VerificationError", ex.Message);
            return false;
        }
    }

    private bool HasPermissionToControlService(string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);

            // 尝试读取服务的CanStop属性
            // 这可能会在权限不足时抛出异常
            var canStop = service.CanStop;

            _logger.LogDebug("Permission check: CanStop={CanStop} for service: {ServiceName}", canStop, serviceName);
            return canStop;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogWarning(ex, "Permission denied for service: {ServiceName}. Error code: {ErrorCode}",
                serviceName, ex.NativeErrorCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking permissions for service: {ServiceName}", serviceName);
            return false;
        }
    }

    /// <summary>
    /// 启动MySQL服务实例，支持超时和错误恢复
    /// </summary>
    /// <param name="serviceName">要启动的服务名称</param>
    /// <returns>如果成功启动返回true，否则返回false</returns>
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
            for (int attempt = 1; attempt <= MaxConnectionRetries; attempt++)
            {
                try
                {
                    _logger.LogDebug("Connection attempt {Attempt}/{MaxRetries} with {Timeout}s timeout", 
                        attempt, MaxConnectionRetries, timeoutSeconds);
                    
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
                        
                        if (attempt < MaxConnectionRetries)
                        {
                            var delay = TimeSpan.FromMilliseconds(BaseRetryDelayMs * Math.Pow(2, attempt - 1));
                            _logger.LogDebug("Waiting {Delay}ms before retry", delay.TotalMilliseconds);
                            await Task.Delay(delay);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Connection attempt {Attempt} failed with exception", attempt);
                    
                    if (attempt < MaxConnectionRetries)
                    {
                        var delay = TimeSpan.FromMilliseconds(BaseRetryDelayMs * Math.Pow(2, attempt - 1));
                        await Task.Delay(delay);
                    }
                }
            }

            _logger.LogError("Failed to verify MySQL instance availability after {MaxRetries} attempts", MaxConnectionRetries);
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
    //private async Task WaitForServiceStatusAsync(ServiceController service, ServiceControllerStatus targetStatus, TimeSpan timeout)
    //{
    //    var stopwatch = Stopwatch.StartNew();
        
    //    while (stopwatch.Elapsed < timeout)
    //    {
    //        try
    //        {
    //            service.Refresh();
    //            if (service.Status == targetStatus)
    //            {
    //                return;
    //            }
                
    //            await Task.Delay(StatusCheckIntervalMs); // Check every 500ms
    //        }
    //        catch (Exception ex)
    //        {
    //            _logger.LogWarning(ex, "Error checking service status during wait");
    //            break;
    //        }
    //    }
        
    //    _logger.LogWarning("Timeout waiting for service to reach status {TargetStatus}. Current status: {CurrentStatus}", 
    //        targetStatus, service.Status);
    //}

    private async Task WaitForServiceStatusAsync(ServiceController service, ServiceControllerStatus desiredStatus, TimeSpan timeout)
    {
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            try
            {
                service.Refresh();
                if (service.Status == desiredStatus)
                {
                    _logger.LogDebug("Service reached desired status: {Status} after {Elapsed}s",
                        desiredStatus, (DateTime.UtcNow - startTime).TotalSeconds);
                    return;
                }

                if (desiredStatus == ServiceControllerStatus.Stopped &&
                    service.Status == ServiceControllerStatus.StopPending)
                {
                    _logger.LogDebug("Service is stopping, current status: StopPending");
                }
                else if (desiredStatus == ServiceControllerStatus.Running &&
                         service.Status == ServiceControllerStatus.StartPending)
                {
                    _logger.LogDebug("Service is starting, current status: StartPending");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error refreshing service status");
            }

            await Task.Delay(1000);
        }

        _logger.LogWarning("Service did not reach status {Status} within timeout of {Timeout}s",
            desiredStatus, timeout.TotalSeconds);

        throw new System.ServiceProcess.TimeoutException($"Service did not reach {desiredStatus} status within {timeout.TotalSeconds} seconds");
    }
}