using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Data.Repositories;

/// <summary>
/// 传输日志存储库实现
/// Transfer log repository implementation
/// </summary>
public class TransferLogRepository : Repository<TransferLog>, ITransferLogRepository
{
    private readonly ILogger<TransferLogRepository> _logger;

    /// <summary>
    /// 构造函数
    /// Constructor
    /// </summary>
    /// <param name="context">数据库上下文</param>
    /// <param name="logger">日志记录器</param>
    public TransferLogRepository(BackupDbContext context, ILogger<TransferLogRepository> logger) 
        : base(context)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 根据备份日志ID获取所有传输日志
    /// Gets all transfer logs by backup log ID
    /// </summary>
    public async Task<IEnumerable<TransferLog>> GetByBackupLogIdAsync(int backupLogId)
    {
        _logger.LogDebug("Getting transfer logs for backup log ID: {BackupLogId}", backupLogId);
        
        return await _dbSet
            .Where(tl => tl.BackupLogId == backupLogId)
            .OrderBy(tl => tl.ChunkIndex)
            .ToListAsync();
    }

    /// <summary>
    /// 根据状态获取传输日志
    /// Gets transfer logs by status
    /// </summary>
    public async Task<IEnumerable<TransferLog>> GetByStatusAsync(string status)
    {
        _logger.LogDebug("Getting transfer logs with status: {Status}", status);
        
        return await _dbSet
            .Where(tl => tl.Status == status)
            .OrderByDescending(tl => tl.TransferTime)
            .ToListAsync();
    }

    /// <summary>
    /// 获取失败的传输日志
    /// Gets failed transfer logs
    /// </summary>
    public async Task<IEnumerable<TransferLog>> GetFailedTransfersAsync()
    {
        _logger.LogDebug("Getting failed transfer logs");
        
        return await _dbSet
            .Where(tl => tl.Status == "Failed")
            .Include(tl => tl.BackupLog)
            .OrderByDescending(tl => tl.TransferTime)
            .ToListAsync();
    }

    /// <summary>
    /// 获取正在进行的传输日志
    /// Gets ongoing transfer logs
    /// </summary>
    public async Task<IEnumerable<TransferLog>> GetOngoingTransfersAsync()
    {
        _logger.LogDebug("Getting ongoing transfer logs");
        
        var ongoingStatuses = new[] { "Pending", "InProgress", "Uploading" };
        
        return await _dbSet
            .Where(tl => ongoingStatuses.Contains(tl.Status))
            .Include(tl => tl.BackupLog)
            .OrderByDescending(tl => tl.TransferTime)
            .ToListAsync();
    }

    /// <summary>
    /// 获取日期范围内的传输日志
    /// Gets transfer logs within date range
    /// </summary>
    public async Task<IEnumerable<TransferLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        _logger.LogDebug("Getting transfer logs between {StartDate} and {EndDate}", startDate, endDate);
        
        return await _dbSet
            .Where(tl => tl.TransferTime >= startDate && tl.TransferTime <= endDate)
            .Include(tl => tl.BackupLog)
            .OrderByDescending(tl => tl.TransferTime)
            .ToListAsync();
    }

    /// <summary>
    /// 获取传输统计信息
    /// Gets transfer statistics
    /// </summary>
    public async Task<TransferStatistics> GetTransferStatisticsAsync(int? backupLogId = null)
    {
        _logger.LogDebug("Getting transfer statistics for backup log ID: {BackupLogId}", backupLogId);
        
        var query = _dbSet.AsQueryable();
        
        if (backupLogId.HasValue)
        {
            query = query.Where(tl => tl.BackupLogId == backupLogId.Value);
        }

        var totalTransfers = await query.CountAsync();
        var successfulTransfers = await query.CountAsync(tl => tl.Status == "Completed" || tl.Status == "Success");
        var failedTransfers = await query.CountAsync(tl => tl.Status == "Failed");
        var ongoingTransfers = await query.CountAsync(tl => tl.Status == "Pending" || tl.Status == "InProgress" || tl.Status == "Uploading");
        var totalBytes = await query.SumAsync(tl => tl.ChunkSize);

        // 计算平均传输速度（简化计算，基于完成的传输）
        var completedTransfers = await query
            .Where(tl => tl.Status == "Completed" || tl.Status == "Success")
            .ToListAsync();

        double averageSpeed = 0;
        if (completedTransfers.Any())
        {
            // 假设每个分块传输时间为1秒（实际应该记录传输开始和结束时间）
            var totalCompletedBytes = completedTransfers.Sum(tl => tl.ChunkSize);
            var estimatedTotalTime = completedTransfers.Count; // 简化计算
            averageSpeed = estimatedTotalTime > 0 ? (double)totalCompletedBytes / estimatedTotalTime : 0;
        }

        return new TransferStatistics
        {
            TotalTransfers = totalTransfers,
            SuccessfulTransfers = successfulTransfers,
            FailedTransfers = failedTransfers,
            OngoingTransfers = ongoingTransfers,
            TotalBytesTransferred = totalBytes,
            AverageTransferSpeed = averageSpeed
        };
    }

    /// <summary>
    /// 获取传输进度信息
    /// Gets transfer progress information
    /// </summary>
    public async Task<TransferProgress> GetTransferProgressAsync(int backupLogId)
    {
        _logger.LogDebug("Getting transfer progress for backup log ID: {BackupLogId}", backupLogId);
        
        var transferLogs = await _dbSet
            .Where(tl => tl.BackupLogId == backupLogId)
            .ToListAsync();

        var totalChunks = transferLogs.Count;
        var completedChunks = transferLogs.Count(tl => tl.Status == "Completed" || tl.Status == "Success");
        var failedChunks = transferLogs.Count(tl => tl.Status == "Failed");
        var totalBytes = transferLogs.Sum(tl => tl.ChunkSize);
        var transferredBytes = transferLogs
            .Where(tl => tl.Status == "Completed" || tl.Status == "Success")
            .Sum(tl => tl.ChunkSize);

        var lastUpdateTime = transferLogs.Any() 
            ? transferLogs.Max(tl => tl.TransferTime) 
            : DateTime.Now;

        return new TransferProgress
        {
            BackupLogId = backupLogId,
            TotalChunks = totalChunks,
            CompletedChunks = completedChunks,
            FailedChunks = failedChunks,
            TotalBytes = totalBytes,
            TransferredBytes = transferredBytes,
            LastUpdateTime = lastUpdateTime
        };
    }

    /// <summary>
    /// 批量更新传输日志状态
    /// Batch update transfer log status
    /// </summary>
    public async Task<int> BatchUpdateStatusAsync(IEnumerable<int> transferLogIds, string newStatus)
    {
        _logger.LogDebug("Batch updating transfer log status to {Status} for {Count} logs", 
            newStatus, transferLogIds.Count());
        
        var transferLogs = await _dbSet
            .Where(tl => transferLogIds.Contains(tl.Id))
            .ToListAsync();

        foreach (var transferLog in transferLogs)
        {
            transferLog.Status = newStatus;
        }

        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Updated {Count} transfer logs to status {Status}", 
            transferLogs.Count, newStatus);
        
        return transferLogs.Count;
    }

    /// <summary>
    /// 清理旧的传输日志
    /// Cleanup old transfer logs
    /// </summary>
    public async Task<int> CleanupOldTransferLogsAsync(int maxAgeDays)
    {
        _logger.LogDebug("Cleaning up transfer logs older than {MaxAgeDays} days", maxAgeDays);
        
        var cutoffDate = DateTime.Now.AddDays(-maxAgeDays);
        
        var oldTransferLogs = await _dbSet
            .Where(tl => tl.TransferTime < cutoffDate)
            .ToListAsync();

        if (oldTransferLogs.Any())
        {
            _dbSet.RemoveRange(oldTransferLogs);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Cleaned up {Count} old transfer logs", oldTransferLogs.Count);
        }

        return oldTransferLogs.Count;
    }

    /// <summary>
    /// 获取传输错误摘要
    /// Gets transfer error summary
    /// </summary>
    public async Task<IEnumerable<TransferErrorSummary>> GetTransferErrorSummaryAsync(DateTime startDate, DateTime endDate)
    {
        _logger.LogDebug("Getting transfer error summary between {StartDate} and {EndDate}", startDate, endDate);
        
        var failedTransfers = await _dbSet
            .Where(tl => tl.Status == "Failed" && 
                        tl.TransferTime >= startDate && 
                        tl.TransferTime <= endDate &&
                        !string.IsNullOrEmpty(tl.ErrorMessage))
            .GroupBy(tl => tl.ErrorMessage)
            .Select(g => new TransferErrorSummary
            {
                ErrorMessage = g.Key!,
                OccurrenceCount = g.Count(),
                FirstOccurrence = g.Min(tl => tl.TransferTime),
                LastOccurrence = g.Max(tl => tl.TransferTime),
                AffectedBackupLogIds = g.Select(tl => tl.BackupLogId).Distinct().ToList()
            })
            .OrderByDescending(es => es.OccurrenceCount)
            .ToListAsync();

        return failedTransfers;
    }
}