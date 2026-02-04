using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 传输日志管理服务接口
/// Transfer log management service interface
/// </summary>
public interface ITransferLogService
{
    /// <summary>
    /// 记录传输分块开始
    /// Records transfer chunk start
    /// </summary>
    /// <param name="backupLogId">备份日志ID</param>
    /// <param name="chunkIndex">分块索引</param>
    /// <param name="chunkSize">分块大小</param>
    /// <returns>传输日志ID</returns>
    Task<int> StartTransferChunkAsync(int backupLogId, int chunkIndex, long chunkSize);

    /// <summary>
    /// 更新传输分块状态
    /// Updates transfer chunk status
    /// </summary>
    /// <param name="transferLogId">传输日志ID</param>
    /// <param name="status">新状态</param>
    /// <param name="errorMessage">错误消息（可选）</param>
    Task UpdateTransferChunkStatusAsync(int transferLogId, string status, string? errorMessage = null);

    /// <summary>
    /// 完成传输分块
    /// Completes transfer chunk
    /// </summary>
    /// <param name="transferLogId">传输日志ID</param>
    /// <param name="success">是否成功</param>
    /// <param name="errorMessage">错误消息（如果失败）</param>
    Task CompleteTransferChunkAsync(int transferLogId, bool success, string? errorMessage = null);

    /// <summary>
    /// 批量记录传输分块
    /// Batch records transfer chunks
    /// </summary>
    /// <param name="backupLogId">备份日志ID</param>
    /// <param name="chunks">分块信息列表</param>
    /// <returns>创建的传输日志ID列表</returns>
    Task<IEnumerable<int>> BatchCreateTransferChunksAsync(int backupLogId, IEnumerable<ChunkInfo> chunks);

    /// <summary>
    /// 获取传输进度
    /// Gets transfer progress
    /// </summary>
    /// <param name="backupLogId">备份日志ID</param>
    /// <returns>传输进度信息</returns>
    Task<TransferProgress> GetTransferProgressAsync(int backupLogId);

    /// <summary>
    /// 获取传输统计信息
    /// Gets transfer statistics
    /// </summary>
    /// <param name="backupLogId">备份日志ID（可选）</param>
    /// <param name="startDate">开始日期（可选）</param>
    /// <param name="endDate">结束日期（可选）</param>
    /// <returns>传输统计信息</returns>
    Task<TransferStatistics> GetTransferStatisticsAsync(int? backupLogId = null, DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// 获取失败的传输分块
    /// Gets failed transfer chunks
    /// </summary>
    /// <param name="backupLogId">备份日志ID（可选）</param>
    /// <returns>失败的传输日志列表</returns>
    Task<IEnumerable<TransferLog>> GetFailedTransferChunksAsync(int? backupLogId = null);

    /// <summary>
    /// 重试失败的传输分块
    /// Retries failed transfer chunks
    /// </summary>
    /// <param name="transferLogIds">传输日志ID列表</param>
    /// <returns>重置的传输分块数量</returns>
    Task<int> RetryFailedTransferChunksAsync(IEnumerable<int> transferLogIds);

    /// <summary>
    /// 清理旧的传输日志
    /// Cleans up old transfer logs
    /// </summary>
    /// <param name="maxAgeDays">最大保留天数</param>
    /// <param name="keepFailedLogs">是否保留失败的日志</param>
    /// <returns>清理的记录数</returns>
    Task<int> CleanupOldTransferLogsAsync(int maxAgeDays, bool keepFailedLogs = true);

    /// <summary>
    /// 获取传输错误摘要
    /// Gets transfer error summary
    /// </summary>
    /// <param name="startDate">开始日期</param>
    /// <param name="endDate">结束日期</param>
    /// <returns>错误摘要列表</returns>
    Task<IEnumerable<TransferErrorSummary>> GetTransferErrorSummaryAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// 导出传输日志
    /// Exports transfer logs
    /// </summary>
    /// <param name="backupLogId">备份日志ID</param>
    /// <param name="format">导出格式（CSV, JSON）</param>
    /// <returns>导出的文件内容</returns>
    Task<byte[]> ExportTransferLogsAsync(int backupLogId, string format = "CSV");

    /// <summary>
    /// 获取传输性能指标
    /// Gets transfer performance metrics
    /// </summary>
    /// <param name="backupLogId">备份日志ID</param>
    /// <returns>性能指标</returns>
    Task<TransferPerformanceMetrics> GetTransferPerformanceMetricsAsync(int backupLogId);
}

/// <summary>
/// 分块信息
/// Chunk information
/// </summary>
public class ChunkInfo
{
    /// <summary>
    /// 分块索引
    /// Chunk index
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// 分块大小
    /// Chunk size
    /// </summary>
    public long ChunkSize { get; set; }

    /// <summary>
    /// 初始状态
    /// Initial status
    /// </summary>
    public string Status { get; set; } = "Pending";
}

/// <summary>
/// 传输性能指标
/// Transfer performance metrics
/// </summary>
public class TransferPerformanceMetrics
{
    /// <summary>
    /// 备份日志ID
    /// Backup log ID
    /// </summary>
    public int BackupLogId { get; set; }

    /// <summary>
    /// 总传输时间（秒）
    /// Total transfer time in seconds
    /// </summary>
    public double TotalTransferTimeSeconds { get; set; }

    /// <summary>
    /// 平均分块传输时间（秒）
    /// Average chunk transfer time in seconds
    /// </summary>
    public double AverageChunkTransferTimeSeconds { get; set; }

    /// <summary>
    /// 最快分块传输时间（秒）
    /// Fastest chunk transfer time in seconds
    /// </summary>
    public double FastestChunkTransferTimeSeconds { get; set; }

    /// <summary>
    /// 最慢分块传输时间（秒）
    /// Slowest chunk transfer time in seconds
    /// </summary>
    public double SlowestChunkTransferTimeSeconds { get; set; }

    /// <summary>
    /// 平均传输速度（字节/秒）
    /// Average transfer speed in bytes per second
    /// </summary>
    public double AverageTransferSpeedBytesPerSecond { get; set; }

    /// <summary>
    /// 峰值传输速度（字节/秒）
    /// Peak transfer speed in bytes per second
    /// </summary>
    public double PeakTransferSpeedBytesPerSecond { get; set; }

    /// <summary>
    /// 传输效率百分比
    /// Transfer efficiency percentage
    /// </summary>
    public double TransferEfficiencyPercentage { get; set; }

    /// <summary>
    /// 重试次数
    /// Retry count
    /// </summary>
    public int RetryCount { get; set; }
}