using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 传输日志存储库接口
/// Transfer log repository interface
/// </summary>
public interface ITransferLogRepository : IRepository<TransferLog>
{
    /// <summary>
    /// 根据备份日志ID获取所有传输日志
    /// Gets all transfer logs by backup log ID
    /// </summary>
    /// <param name="backupLogId">备份日志ID</param>
    /// <returns>传输日志列表</returns>
    Task<IEnumerable<TransferLog>> GetByBackupLogIdAsync(int backupLogId);

    /// <summary>
    /// 根据状态获取传输日志
    /// Gets transfer logs by status
    /// </summary>
    /// <param name="status">传输状态</param>
    /// <returns>传输日志列表</returns>
    Task<IEnumerable<TransferLog>> GetByStatusAsync(string status);

    /// <summary>
    /// 获取失败的传输日志
    /// Gets failed transfer logs
    /// </summary>
    /// <returns>失败的传输日志列表</returns>
    Task<IEnumerable<TransferLog>> GetFailedTransfersAsync();

    /// <summary>
    /// 获取正在进行的传输日志
    /// Gets ongoing transfer logs
    /// </summary>
    /// <returns>正在进行的传输日志列表</returns>
    Task<IEnumerable<TransferLog>> GetOngoingTransfersAsync();

    /// <summary>
    /// 获取日期范围内的传输日志
    /// Gets transfer logs within date range
    /// </summary>
    /// <param name="startDate">开始日期</param>
    /// <param name="endDate">结束日期</param>
    /// <returns>传输日志列表</returns>
    Task<IEnumerable<TransferLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// 获取传输统计信息
    /// Gets transfer statistics
    /// </summary>
    /// <param name="backupLogId">备份日志ID（可选）</param>
    /// <returns>传输统计信息</returns>
    Task<TransferStatistics> GetTransferStatisticsAsync(int? backupLogId = null);

    /// <summary>
    /// 获取传输进度信息
    /// Gets transfer progress information
    /// </summary>
    /// <param name="backupLogId">备份日志ID</param>
    /// <returns>传输进度信息</returns>
    Task<TransferProgress> GetTransferProgressAsync(int backupLogId);

    /// <summary>
    /// 批量更新传输日志状态
    /// Batch update transfer log status
    /// </summary>
    /// <param name="transferLogIds">传输日志ID列表</param>
    /// <param name="newStatus">新状态</param>
    /// <returns>更新的记录数</returns>
    Task<int> BatchUpdateStatusAsync(IEnumerable<int> transferLogIds, string newStatus);

    /// <summary>
    /// 清理旧的传输日志
    /// Cleanup old transfer logs
    /// </summary>
    /// <param name="maxAgeDays">最大保留天数</param>
    /// <returns>清理的记录数</returns>
    Task<int> CleanupOldTransferLogsAsync(int maxAgeDays);

    /// <summary>
    /// 获取传输错误摘要
    /// Gets transfer error summary
    /// </summary>
    /// <param name="startDate">开始日期</param>
    /// <param name="endDate">结束日期</param>
    /// <returns>错误摘要列表</returns>
    Task<IEnumerable<TransferErrorSummary>> GetTransferErrorSummaryAsync(DateTime startDate, DateTime endDate);
}