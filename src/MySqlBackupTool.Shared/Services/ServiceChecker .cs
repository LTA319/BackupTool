using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.ServiceProcess;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Windows服务检查器实现 / Windows service checker implementation
/// 提供服务状态检查、权限验证、依赖关系分析和备份建议功能 / Provides service status checking, permission validation, dependency analysis, and backup advice
/// </summary>
public class ServiceChecker : IServiceChecker
{
    private readonly ILogger<ServiceChecker> _logger;
    private readonly IMemoryProfiler _memoryProfiler;

    /// <summary>
    /// 构造函数，初始化服务检查器 / Constructor, initializes service checker
    /// </summary>
    /// <param name="logger">日志服务 / Logger service</param>
    /// <param name="memoryProfiler">内存分析器（可选） / Memory profiler (optional)</param>
    public ServiceChecker(ILogger<ServiceChecker> logger, IMemoryProfiler memoryProfiler = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryProfiler = memoryProfiler;
    }

    /// <summary>
    /// 异步检查指定服务的状态和属性 / Asynchronously checks the status and properties of specified service
    /// 检查服务是否存在、状态、权限、依赖关系并生成备份建议 / Checks if service exists, status, permissions, dependencies and generates backup advice
    /// </summary>
    /// <param name="serviceName">服务名称 / Service name</param>
    /// <returns>服务检查结果 / Service check result</returns>
    /// <exception cref="ArgumentException">当服务名称为空时抛出 / Thrown when service name is empty</exception>
    public async Task<ServiceCheckResult> CheckServiceAsync(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));
        }

        var operationId = $"service-check-{serviceName}-{Guid.NewGuid():N}";
        _memoryProfiler?.StartProfiling(operationId, "Service-Check");
        _memoryProfiler?.RecordSnapshot(operationId, "Start", $"Checking service: {serviceName}");

        var result = new ServiceCheckResult
        {
            ServiceName = serviceName
        };

        try
        {
            using var service = await GetServiceControllerAsync(serviceName);
            if (service == null)
            {
                result.Exists = false;
                result.ErrorMessage = $"服务 '{serviceName}' 不存在";
                _memoryProfiler?.RecordSnapshot(operationId, "NotFound", "Service not found");
                return result;
            }

            result.Exists = true;
            result.DisplayName = service.DisplayName;
            result.Status = service.Status;
            result.ServiceType = service.ServiceType;

            // 检查权限和能力
            await CheckServiceCapabilitiesAsync(service, result, operationId);

            // 检查依赖关系
            await CheckServiceDependenciesAsync(service, result, operationId);

            // 生成备份建议
            GenerateBackupAdvice(result);

            _logger.LogInformation("Service check completed: {@Result}", result);
            _memoryProfiler?.RecordSnapshot(operationId, "Completed", "Service check completed");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            result.Exists = false;
            result.ErrorMessage = $"服务 '{serviceName}' 不存在";
            _logger.LogWarning(ex, "Service not found: {ServiceName}", serviceName);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"检查服务时出错: {ex.Message}";
            _logger.LogError(ex, "Error checking service: {ServiceName}", serviceName);
            _memoryProfiler?.RecordSnapshot(operationId, "Error", ex.Message);
        }
        finally
        {
            _memoryProfiler?.StopProfiling(operationId);
        }

        return result;
    }

    /// <summary>
    /// 列出系统中所有MySQL相关服务 / Lists all MySQL-related services in the system
    /// 扫描所有Windows服务，筛选出MySQL和MariaDB相关的服务 / Scans all Windows services and filters MySQL and MariaDB related services
    /// </summary>
    /// <returns>MySQL服务信息列表 / List of MySQL service information</returns>
    public async Task<List<ServiceInfo>> ListMySQLServicesAsync()
    {
        var services = new List<ServiceInfo>();

        try
        {
            var allServices = await Task.Run(() => ServiceController.GetServices());

            foreach (var service in allServices)
            {
                if (IsMySQLService(service.ServiceName))
                {
                    var serviceInfo = new ServiceInfo
                    {
                        ServiceName = service.ServiceName,
                        DisplayName = service.DisplayName,
                        Status = service.Status,
                        ServiceType = service.ServiceType
                    };

                    // 检查是否可停止
                    try
                    {
                        serviceInfo.IsStoppable = service.CanStop;
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        serviceInfo.IsStoppable = false; // 无权限
                    }

                    services.Add(serviceInfo);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing MySQL services");
        }

        return services.OrderBy(s => s.ServiceName).ToList();
    }

    /// <summary>
    /// 检查指定服务是否存在 / Checks if specified service exists
    /// </summary>
    /// <param name="serviceName">服务名称 / Service name</param>
    /// <returns>如果服务存在返回true，否则返回false / Returns true if service exists, false otherwise</returns>
    public async Task<bool> ServiceExistsAsync(string serviceName)
    {
        try
        {
            using var service = await GetServiceControllerAsync(serviceName);
            return service != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取指定服务的当前状态 / Gets the current status of specified service
    /// </summary>
    /// <param name="serviceName">服务名称 / Service name</param>
    /// <returns>服务状态，服务不存在时返回null / Service status, null if service doesn't exist</returns>
    public async Task<ServiceControllerStatus?> GetServiceStatusAsync(string serviceName)
    {
        try
        {
            using var service = await GetServiceControllerAsync(serviceName);
            return service?.Status;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 检查是否有权限控制指定服务 / Checks if has permission to control specified service
    /// 尝试访问服务的控制属性来验证权限 / Attempts to access service control properties to verify permissions
    /// </summary>
    /// <param name="serviceName">服务名称 / Service name</param>
    /// <returns>如果有权限返回true，否则返回false / Returns true if has permission, false otherwise</returns>
    public async Task<bool> HasPermissionToControlServiceAsync(string serviceName)
    {
        try
        {
            using var service = await GetServiceControllerAsync(serviceName);
            if (service == null) return false;

            // 尝试读取 CanStop 属性，这会在权限不足时抛出异常
            var canStop = service.CanStop;
            return true;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogWarning(ex, "Permission denied for service: {ServiceName}", serviceName);
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检查服务是否可以被停止 / Checks if service can be stopped
    /// 验证服务是否支持停止操作且当前正在运行 / Verifies if service supports stop operation and is currently running
    /// </summary>
    /// <param name="serviceName">服务名称 / Service name</param>
    /// <returns>如果可以停止返回true，否则返回false / Returns true if can be stopped, false otherwise</returns>
    public async Task<bool> CanServiceBeStoppedAsync(string serviceName)
    {
        try
        {
            using var service = await GetServiceControllerAsync(serviceName);
            if (service == null) return false;

            return service.CanStop &&
                   service.Status == ServiceControllerStatus.Running;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取服务的详细信息 / Gets detailed information of service
    /// 包括服务能力、依赖关系等详细属性 / Includes service capabilities, dependencies and other detailed properties
    /// </summary>
    /// <param name="serviceName">服务名称 / Service name</param>
    /// <returns>服务详细信息，服务不存在时返回null / Service detail information, null if service doesn't exist</returns>
    public async Task<ServiceDetailInfo?> GetServiceDetailAsync(string serviceName)
    {
        try
        {
            using var service = await GetServiceControllerAsync(serviceName);
            if (service == null) return null;

            var detail = new ServiceDetailInfo
            {
                ServiceName = service.ServiceName,
                DisplayName = service.DisplayName,
                Status = service.Status,
                ServiceType = service.ServiceType
            };

            try
            {
                detail.CanStop = service.CanStop;
                detail.CanPauseAndContinue = service.CanPauseAndContinue;
                detail.CanShutdown = service.CanShutdown;
                detail.DependentServices = service.DependentServices.Select(s => s.ServiceName).ToArray();
                detail.ServicesDependedOn = service.ServicesDependedOn.Select(s => s.ServiceName).ToArray();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                _logger.LogWarning(ex, "Permission denied when getting service details: {ServiceName}", serviceName);
            }


            return detail;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting service details: {ServiceName}", serviceName);
            return null;
        }
    }

    private async Task<ServiceController?> GetServiceControllerAsync(string serviceName)
    {
        return await Task.Run(() =>
        {
            try
            {
                return new ServiceController(serviceName);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        });
    }

    private async Task CheckServiceCapabilitiesAsync(ServiceController service, ServiceCheckResult result, string operationId)
    {
        try
        {
            result.CanStop = service.CanStop;
            result.CanPauseAndContinue = service.CanPauseAndContinue;
            result.CanShutdown = service.CanShutdown;
            _memoryProfiler?.RecordSnapshot(operationId, "Capabilities",
                $"CanStop: {result.CanStop}, CanPause: {result.CanPauseAndContinue}");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            result.AccessError = $"权限不足: {ex.Message} (Error code: {ex.NativeErrorCode})";
            _logger.LogWarning(ex, "Permission error checking service capabilities: {ServiceName}", service.ServiceName);
            _memoryProfiler?.RecordSnapshot(operationId, "PermissionError", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking service capabilities: {ServiceName}", service.ServiceName);
        }
    }

    private async Task CheckServiceDependenciesAsync(ServiceController service, ServiceCheckResult result, string operationId)
    {
        try
        {
            result.DependentServices = service.DependentServices
                .Select(s => s.ServiceName)
                .ToArray();

            result.ServicesDependedOn = service.ServicesDependedOn
                .Select(s => s.ServiceName)
                .ToArray();

            if (result.DependentServices.Any())
            {
                _logger.LogDebug("Service {ServiceName} has {Count} dependent services",
                    service.ServiceName, result.DependentServices.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking service dependencies: {ServiceName}", service.ServiceName);
        }
    }

    private void GenerateBackupAdvice(ServiceCheckResult result)
    {
        if (!result.Exists)
        {
            result.BackupAdvice = "服务不存在，无法进行备份。请检查服务名称是否正确。";
            return;
        }

        if (result.Status == ServiceControllerStatus.Stopped)
        {
            result.BackupAdvice = "服务已停止，可以进行备份。";
            return;
        }

        if (result.CanStop == true && result.Status == ServiceControllerStatus.Running)
        {
            if (result.DependentServices.Any())
            {
                result.BackupAdvice = $"服务正在运行且有{result.DependentServices.Length}个依赖程序。备份时会自动停止服务，可能会影响这些程序。";
            }
            else
            {
                result.BackupAdvice = "服务正在运行，备份时会自动停止服务。";
            }
        }
        else if (result.CanStop == false)
        {
            result.BackupAdvice = "服务无法停止，备份时可能会出错。请以管理员身份运行程序。";
        }
        else if (!string.IsNullOrEmpty(result.AccessError))
        {
            result.BackupAdvice = $"权限不足: {result.AccessError}。请以管理员身份运行程序。";
        }
    }

    private bool IsMySQLService(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            return false;

        var lowerName = serviceName.ToLowerInvariant();
        return lowerName.Contains("mysql") || lowerName.Contains("mariadb");
    }
}