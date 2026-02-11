using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 基于Windows服务的MySQL实例管理器 / Windows service-based MySQL instance manager
/// 提供MySQL服务的启动、停止和连接验证功能 / Provides MySQL service start, stop and connection verification functionality
/// 支持多种停止方法和错误恢复机制 / Supports multiple stop methods and error recovery mechanisms
/// </summary>
public class MySQLManager : IMySQLManager, IBackupService
{
    private readonly ILogger<MySQLManager> _logger;
    private readonly IMemoryProfiler? _memoryProfiler;
    
    /// <summary>
    /// 服务操作超时时间常量 / Service operation timeout constants
    /// </summary>
    private const int DefaultTimeoutSeconds = 30;
    private const int ServiceOperationTimeoutSeconds = 60;
    
    /// <summary>
    /// 连接重试配置常量 / Connection retry configuration constants
    /// </summary>
    private const int MaxConnectionRetries = 3;
    private const int BaseRetryDelayMs = 1000;
    private const int StatusCheckIntervalMs = 500;
    
    /// <summary>
    /// 默认超时时间 / Default timeout duration
    /// </summary>
    private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(DefaultTimeoutSeconds);
    
    /// <summary>
    /// 服务操作超时时间 / Service operation timeout duration
    /// </summary>
    private readonly TimeSpan _serviceOperationTimeout = TimeSpan.FromSeconds(ServiceOperationTimeoutSeconds);

    /// <summary>
    /// 构造函数，初始化MySQL管理器 / Constructor to initialize MySQL manager
    /// </summary>
    /// <param name="logger">日志记录器 / Logger instance</param>
    /// <param name="memoryProfiler">内存分析器（可选） / Memory profiler (optional)</param>
    /// <exception cref="ArgumentNullException">当logger为null时抛出 / Thrown when logger is null</exception>
    public MySQLManager(ILogger<MySQLManager> logger, IMemoryProfiler? memoryProfiler = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryProfiler = memoryProfiler;
    }

    /// <summary>
    /// 停止MySQL服务实例，支持超时和错误恢复 / Stops MySQL service instance with timeout and error recovery support
    /// 首先尝试使用ServiceController，失败时回退到命令行方法 / First tries ServiceController, falls back to command line methods on failure
    /// 支持多种停止方法：ServiceController、net stop、sc stop、PowerShell / Supports multiple stop methods: ServiceController, net stop, sc stop, PowerShell
    /// </summary>
    /// <param name="serviceName">要停止的服务名称 / Name of the service to stop</param>
    /// <returns>如果成功停止返回true，否则返回false / Returns true if successfully stopped, false otherwise</returns>
    /// <exception cref="ArgumentException">当服务名称为空时抛出 / Thrown when service name is empty</exception>
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
            // 首先尝试使用ServiceController / First try using ServiceController
            var result = await StopUsingServiceControllerAsync(serviceName, operationId);
            if (result)
            {
                _memoryProfiler?.StopProfiling(operationId);
                return true;
            }

            // 如果ServiceController失败，尝试命令行方式 / If ServiceController fails, try command line methods
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

    /// <summary>
    /// 使用ServiceController停止服务 / Stops service using ServiceController
    /// 提供权限检查和状态验证功能 / Provides permission checking and status verification
    /// </summary>
    /// <param name="serviceName">服务名称 / Service name</param>
    /// <param name="operationId">操作ID用于内存分析 / Operation ID for memory profiling</param>
    /// <returns>成功返回true，失败返回false / Returns true on success, false on failure</returns>
    private async Task<bool> StopUsingServiceControllerAsync(string serviceName, string operationId)
    {
        try
        {
            using var service = new ServiceController(serviceName);

            // 检查服务是否存在 / Check if service exists
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

            // 如果已经停止，返回成功 / If already stopped, return success
            if (service.Status == ServiceControllerStatus.Stopped)
            {
                _logger.LogInformation("MySQL service '{ServiceName}' is already stopped", serviceName);
                _memoryProfiler?.RecordSnapshot(operationId, "AlreadyStopped", "Service already in stopped state");
                return true;
            }

            // 检查权限 / Check permissions
            if (!HasPermissionToControlService(serviceName))
            {
                _logger.LogError("Insufficient permissions to control service: {ServiceName}", serviceName);
                _memoryProfiler?.RecordSnapshot(operationId, "Error", "Insufficient permissions");
                return false;
            }

            // 停止服务 / Stop the service
            _memoryProfiler?.RecordSnapshot(operationId, "SendStopCommand", "Sending stop command to service");
            service.Stop();
            _logger.LogInformation("Stop command sent to MySQL service '{ServiceName}'", serviceName);

            // 等待服务停止 / Wait for service to stop
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

    /// <summary>
    /// 使用命令行方法停止服务 / Stops service using command line methods
    /// 依次尝试net stop、sc stop和PowerShell方法 / Tries net stop, sc stop, and PowerShell methods in sequence
    /// </summary>
    /// <param name="serviceName">服务名称 / Service name</param>
    /// <param name="operationId">操作ID用于内存分析 / Operation ID for memory profiling</param>
    /// <returns>成功返回true，失败返回false / Returns true on success, false on failure</returns>
    private async Task<bool> StopUsingCommandLineAsync(string serviceName, string operationId)
    {
        try
        {
            _logger.LogInformation("Attempting to stop service via command line: {ServiceName}", serviceName);
            _memoryProfiler?.RecordSnapshot(operationId, "CommandLineStart", "Starting command line stop");

            // 方法1: 使用net stop / Method 1: Use net stop
            var result = await StopServiceWithNetStop(serviceName, operationId);
            if (result)
            {
                return true;
            }

            // 方法2: 使用sc stop / Method 2: Use sc stop
            result = await StopServiceWithScStop(serviceName, operationId);
            if (result)
            {
                return true;
            }

            // 方法3: 使用PowerShell / Method 3: Use PowerShell
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

    /// <summary>
    /// 使用net stop命令停止服务 / Stops service using net stop command
    /// 执行Windows net stop命令并监控输出 / Executes Windows net stop command and monitors output
    /// </summary>
    /// <param name="serviceName">服务名称 / Service name</param>
    /// <param name="operationId">操作ID用于内存分析 / Operation ID for memory profiling</param>
    /// <returns>成功返回true，失败返回false / Returns true on success, false on failure</returns>
    private async Task<bool> StopServiceWithNetStop(string serviceName, string operationId)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "net",
                Arguments = $"stop \"{serviceName}\" /y", // /y参数表示跳过确认 / /y parameter skips confirmation
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

                var completed = await Task.Run(() => process.WaitForExit(30000)); // 30秒超时 / 30 second timeout

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

                    // 验证服务确实停止了 / Verify service actually stopped
                    await Task.Delay(2000); // 等待2秒让服务完全停止 / Wait 2 seconds for service to fully stop
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

    /// <summary>
    /// 使用sc stop命令停止服务 / Stops service using sc stop command
    /// 执行Windows sc stop命令并监控输出 / Executes Windows sc stop command and monitors output
    /// </summary>
    /// <param name="serviceName">服务名称 / Service name</param>
    /// <param name="operationId">操作ID用于内存分析 / Operation ID for memory profiling</param>
    /// <returns>成功返回true，失败返回false / Returns true on success, false on failure</returns>
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

    /// <summary>
    /// 使用PowerShell停止服务 / Stops service using PowerShell
    /// 执行PowerShell Stop-Service命令 / Executes PowerShell Stop-Service command
    /// </summary>
    /// <param name="serviceName">服务名称 / Service name</param>
    /// <param name="operationId">操作ID用于内存分析 / Operation ID for memory profiling</param>
    /// <returns>成功返回true，失败返回false / Returns true on success, false on failure</returns>
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
    /// <summary>
    /// 验证服务是否已停止 / Verifies if service has stopped
    /// 多次检查服务状态以确认停止状态 / Checks service status multiple times to confirm stopped state
    /// </summary>
    /// <param name="serviceName">服务名称 / Service name</param>
    /// <param name="operationId">操作ID用于内存分析 / Operation ID for memory profiling</param>
    /// <returns>服务已停止返回true，否则返回false / Returns true if service is stopped, false otherwise</returns>
    private async Task<bool> IsServiceStopped(string serviceName, string operationId)
    {
        try
        {
            for (int i = 0; i < 5; i++) // 最多重试5次 / Maximum 5 retry attempts
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
                await Task.Delay(1000); // 等待1秒后重试 / Wait 1 second before retry
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

    /// <summary>
    /// 检查是否有权限控制服务 / Checks if has permission to control service
    /// 通过尝试读取服务属性来验证权限 / Verifies permissions by attempting to read service properties
    /// </summary>
    /// <param name="serviceName">服务名称 / Service name</param>
    /// <returns>有权限返回true，否则返回false / Returns true if has permission, false otherwise</returns>
    private bool HasPermissionToControlService(string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);

            // 尝试读取服务的CanStop属性 / Try to read service's CanStop property
            // 这可能会在权限不足时抛出异常 / This may throw exception when permissions are insufficient
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
    /// 启动MySQL服务实例，支持超时和错误恢复 / Starts MySQL service instance with timeout and error recovery support
    /// 检查服务状态并等待启动完成 / Checks service status and waits for startup completion
    /// </summary>
    /// <param name="serviceName">要启动的服务名称 / Name of the service to start</param>
    /// <returns>如果成功启动返回true，否则返回false / Returns true if successfully started, false otherwise</returns>
    /// <exception cref="ArgumentException">当服务名称为空时抛出 / Thrown when service name is empty</exception>
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
            
            // 检查服务是否存在 / Check if service exists
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

            // 如果已经运行，返回成功 / If already running, return success
            if (service.Status == ServiceControllerStatus.Running)
            {
                _logger.LogInformation("MySQL service '{ServiceName}' is already running", serviceName);
                return true;
            }

            // 如果正在启动，等待完成 / If starting, wait for completion
            if (service.Status == ServiceControllerStatus.StartPending)
            {
                _logger.LogInformation("MySQL service '{ServiceName}' is already starting, waiting for completion", serviceName);
                await WaitForServiceStatusAsync(service, ServiceControllerStatus.Running, _serviceOperationTimeout);
                return service.Status == ServiceControllerStatus.Running;
            }

            // 启动服务 / Start the service
            service.Start();
            _logger.LogInformation("Start command sent to MySQL service '{ServiceName}'", serviceName);

            // 等待服务启动 / Wait for the service to start
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
    /// 验证MySQL实例是否可用并接受连接 / Verifies that a MySQL instance is available and accepting connections
    /// 使用默认30秒超时 / Uses default 30 second timeout
    /// </summary>
    /// <param name="connection">MySQL连接信息 / MySQL connection information</param>
    /// <returns>连接可用返回true，否则返回false / Returns true if connection is available, false otherwise</returns>
    /// <exception cref="ArgumentNullException">当连接信息为null时抛出 / Thrown when connection information is null</exception>
    public async Task<bool> VerifyInstanceAvailabilityAsync(MySQLConnectionInfo connection)
    {
        return await VerifyInstanceAvailabilityAsync(connection, 30); // 默认30秒超时 / Default 30 second timeout
    }

    /// <summary>
    /// 验证MySQL实例是否可用并接受连接，支持可配置超时 / Verifies that a MySQL instance is available and accepting connections with configurable timeout
    /// 使用重试机制和指数退避策略 / Uses retry mechanism with exponential backoff strategy
    /// </summary>
    /// <param name="connection">MySQL连接信息 / MySQL connection information</param>
    /// <param name="timeoutSeconds">超时时间（秒） / Timeout in seconds</param>
    /// <returns>连接可用返回true，否则返回false / Returns true if connection is available, false otherwise</returns>
    /// <exception cref="ArgumentNullException">当连接信息为null时抛出 / Thrown when connection information is null</exception>
    /// <exception cref="ArgumentException">当超时时间小于等于0时抛出 / Thrown when timeout is less than or equal to 0</exception>
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
            // 首先验证连接配置 / First, validate the connection configuration
            var validationResults = connection.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(connection));
            if (validationResults.Any())
            {
                _logger.LogError("Invalid connection configuration: {Errors}", 
                    string.Join(", ", validationResults.Select(r => r.ErrorMessage)));
                return false;
            }

            // 使用重试逻辑和可配置超时测试连接 / Test the connection with retry logic and configurable timeout
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
    /// 等待服务达到指定状态 / Waits for a service to reach the specified status within the given timeout
    /// 定期检查服务状态直到达到目标状态或超时 / Periodically checks service status until target status is reached or timeout occurs
    /// </summary>
    /// <param name="service">服务控制器实例 / ServiceController instance</param>
    /// <param name="desiredStatus">期望的服务状态 / Desired service status</param>
    /// <param name="timeout">超时时间 / Timeout duration</param>
    /// <exception cref="System.ServiceProcess.TimeoutException">当服务未在超时时间内达到期望状态时抛出 / Thrown when service doesn't reach desired status within timeout</exception>
    private async Task WaitForServiceStatusAsync(ServiceController service, ServiceControllerStatus desiredStatus, TimeSpan timeout)
    {
        var startTime = DateTime.Now;

        while (DateTime.Now - startTime < timeout)
        {
            try
            {
                service.Refresh();
                if (service.Status == desiredStatus)
                {
                    _logger.LogDebug("Service reached desired status: {Status} after {Elapsed}s",
                        desiredStatus, (DateTime.Now - startTime).TotalSeconds);
                    return;
                }

                // 记录中间状态以便调试 / Log intermediate states for debugging
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

            await Task.Delay(1000); // 每秒检查一次 / Check every second
        }

        _logger.LogWarning("Service did not reach status {Status} within timeout of {Timeout}s",
            desiredStatus, timeout.TotalSeconds);

        throw new System.ServiceProcess.TimeoutException($"Service did not reach {desiredStatus} status within {timeout.TotalSeconds} seconds");
    }
}