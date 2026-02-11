using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// MySQL实例生命周期管理操作接口
/// 提供MySQL服务的启动、停止和连接验证功能
/// </summary>
public interface IMySQLManager
{
    /// <summary>
    /// 停止MySQL服务实例
    /// </summary>
    /// <param name="serviceName">要停止的MySQL服务名称</param>
    /// <returns>如果服务成功停止返回true，否则返回false</returns>
    Task<bool> StopInstanceAsync(string serviceName);

    /// <summary>
    /// 启动MySQL服务实例
    /// </summary>
    /// <param name="serviceName">要启动的MySQL服务名称</param>
    /// <returns>如果服务成功启动返回true，否则返回false</returns>
    Task<bool> StartInstanceAsync(string serviceName);

    /// <summary>
    /// 验证MySQL实例是否可用并接受连接
    /// </summary>
    /// <param name="connection">MySQL实例的连接信息</param>
    /// <returns>如果实例可用并接受连接返回true，否则返回false</returns>
    Task<bool> VerifyInstanceAvailabilityAsync(MySQLConnectionInfo connection);

    /// <summary>
    /// 验证MySQL实例是否可用并接受连接，支持可配置的超时时间
    /// </summary>
    /// <param name="connection">MySQL实例的连接信息</param>
    /// <param name="timeoutSeconds">连接超时时间（秒）</param>
    /// <returns>如果实例可用并接受连接返回true，否则返回false</returns>
    Task<bool> VerifyInstanceAvailabilityAsync(MySQLConnectionInfo connection, int timeoutSeconds);
}