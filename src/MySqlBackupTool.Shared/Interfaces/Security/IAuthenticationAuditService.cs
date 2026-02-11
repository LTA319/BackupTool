using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 身份验证审计服务接口
/// 定义审计日志记录的标准操作
/// </summary>
public interface IAuthenticationAuditService
{
    /// <summary>
    /// 记录身份验证审计日志
    /// </summary>
    /// <param name="auditLog">审计日志条目</param>
    /// <returns>异步任务</returns>
    Task LogAuthenticationEventAsync(AuthenticationAuditLog auditLog);

    /// <summary>
    /// 获取指定时间范围内的审计日志
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="clientId">可选的客户端ID过滤</param>
    /// <returns>审计日志列表</returns>
    Task<List<AuthenticationAuditLog>> GetAuditLogsAsync(DateTime startTime, DateTime endTime, string? clientId = null);

    /// <summary>
    /// 清理过期的审计日志
    /// </summary>
    /// <param name="retentionDays">保留天数</param>
    /// <returns>清理的日志条目数量</returns>
    Task<int> CleanupExpiredLogsAsync(int retentionDays);
}