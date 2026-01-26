using MySqlBackupTool.Shared.Models;
using System.ServiceProcess;


namespace MySqlBackupTool.Shared.Interfaces;

public interface IServiceChecker
{
    /// <summary>
    /// 检查指定服务的状态和权限
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    /// <returns>服务检查结果</returns>
    Task<ServiceCheckResult> CheckServiceAsync(string serviceName);

    /// <summary>
    /// 获取所有MySQL相关服务
    /// </summary>
    Task<List<ServiceInfo>> ListMySQLServicesAsync();

    /// <summary>
    /// 验证服务是否存在
    /// </summary>
    Task<bool> ServiceExistsAsync(string serviceName);

    /// <summary>
    /// 获取服务状态
    /// </summary>
    Task<ServiceControllerStatus?> GetServiceStatusAsync(string serviceName);

    /// <summary>
    /// 检查是否有权限控制服务
    /// </summary>
    Task<bool> HasPermissionToControlServiceAsync(string serviceName);

    /// <summary>
    /// 检查服务是否可以停止
    /// </summary>
    Task<bool> CanServiceBeStoppedAsync(string serviceName);

    /// <summary>
    /// 获取服务详细信息
    /// </summary>
    Task<ServiceDetailInfo?> GetServiceDetailAsync(string serviceName);
}
