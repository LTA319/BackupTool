using Microsoft.EntityFrameworkCore;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Data.Repositories;

/// <summary>
/// 备份日志实体的存储库实现
/// Repository implementation for BackupLog entities
/// </summary>
public class BackupLogRepository : Repository<BackupLog>, IBackupLogRepository
{
    public BackupLogRepository(BackupDbContext context) : base(context)
    {
    }

    /// <summary>
    /// 根据配置ID获取备份日志
    /// Gets backup logs by configuration ID
    /// </summary>
    public async Task<IEnumerable<BackupLog>> GetByConfigurationIdAsync(int configurationId)
    {
        return await _dbSet
            .Where(bl => bl.BackupConfigId == configurationId)
            .OrderByDescending(bl => bl.StartTime)
            .ToListAsync();
    }

    /// <summary>
    /// 根据日期范围获取备份日志
    /// Gets backup logs by date range
    /// </summary>
    public async Task<IEnumerable<BackupLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Where(bl => bl.StartTime >= startDate && bl.StartTime <= endDate)
            .OrderByDescending(bl => bl.StartTime)
            .ToListAsync();
    }

    /// <summary>
    /// 根据状态获取备份日志
    /// Gets backup logs by status
    /// </summary>
    public async Task<IEnumerable<BackupLog>> GetByStatusAsync(BackupStatus status)
    {
        return await _dbSet
            .Where(bl => bl.Status == status)
            .OrderByDescending(bl => bl.StartTime)
            .ToListAsync();
    }

    /// <summary>
    /// 获取正在运行的备份
    /// Gets running backups
    /// </summary>
    public async Task<IEnumerable<BackupLog>> GetRunningBackupsAsync()
    {
        var runningStatuses = new[]
        {
            BackupStatus.Queued,
            BackupStatus.StoppingMySQL,
            BackupStatus.Compressing,
            BackupStatus.Transferring,
            BackupStatus.StartingMySQL,
            BackupStatus.Verifying
        };

        return await _dbSet
            .Where(bl => runningStatuses.Contains(bl.Status))
            .OrderBy(bl => bl.StartTime)
            .ToListAsync();
    }

    /// <summary>
    /// 获取失败的备份
    /// Gets failed backups
    /// </summary>
    public async Task<IEnumerable<BackupLog>> GetFailedBackupsAsync()
    {
        return await _dbSet
            .Where(bl => bl.Status == BackupStatus.Failed)
            .OrderByDescending(bl => bl.StartTime)
            .ToListAsync();
    }

    /// <summary>
    /// 获取包含传输日志的备份日志
    /// Gets backup log with transfer logs
    /// </summary>
    public async Task<BackupLog?> GetWithTransferLogsAsync(int id)
    {
        return await _dbSet
            .Include(bl => bl.TransferLogs)
            .FirstOrDefaultAsync(bl => bl.Id == id);
    }

    /// <summary>
    /// 获取备份统计信息
    /// Gets backup statistics
    /// </summary>
    public async Task<BackupStatistics> GetStatisticsAsync(DateTime startDate, DateTime endDate)
    {
        var logs = await _dbSet
            .Where(bl => bl.StartTime >= startDate && bl.StartTime <= endDate)
            .ToListAsync();

        var totalBackups = logs.Count;
        var successfulBackups = logs.Count(bl => bl.Status == BackupStatus.Completed);
        var failedBackups = logs.Count(bl => bl.Status == BackupStatus.Failed);
        var cancelledBackups = logs.Count(bl => bl.Status == BackupStatus.Cancelled);

        var totalBytesTransferred = logs.Where(bl => bl.FileSize.HasValue).Sum(bl => bl.FileSize!.Value);
        var backupsWithSize = logs.Count(bl => bl.FileSize.HasValue);
        var completedLogs = logs.Where(bl => bl.EndTime.HasValue).ToList();
        var totalDuration = TimeSpan.FromTicks(completedLogs.Sum(bl => bl.Duration?.Ticks ?? 0));

        var averageBackupSize = backupsWithSize > 0 ? (double)totalBytesTransferred / backupsWithSize : 0;
        var successRate = totalBackups > 0 ? (double)successfulBackups / totalBackups * 100 : 0;
        var averageDuration = completedLogs.Count > 0 
            ? TimeSpan.FromTicks(totalDuration.Ticks / completedLogs.Count) 
            : TimeSpan.Zero;

        return new BackupStatistics
        {
            TotalBackups = totalBackups,
            SuccessfulBackups = successfulBackups,
            FailedBackups = failedBackups,
            CancelledBackups = cancelledBackups,
            TotalBytesTransferred = totalBytesTransferred,
            TotalDuration = totalDuration,
            AverageBackupSize = averageBackupSize,
            SuccessRate = successRate,
            AverageDuration = averageDuration
        };
    }

    /// <summary>
    /// 清理旧的日志记录
    /// Cleans up old log records
    /// </summary>
    public async Task<int> CleanupOldLogsAsync(int maxAgeDays, int? maxCount = null)
    {
        var cutoffDate = DateTime.Now.AddDays(-maxAgeDays);
        var query = _dbSet.Where(bl => bl.StartTime < cutoffDate);

        // If maxCount is specified, keep the most recent logs up to that count
        if (maxCount.HasValue)
        {
            var recentLogs = await _dbSet
                .OrderByDescending(bl => bl.StartTime)
                .Take(maxCount.Value)
                .Select(bl => bl.Id)
                .ToListAsync();

            query = query.Where(bl => !recentLogs.Contains(bl.Id));
        }

        var logsToDelete = await query.ToListAsync();
        var deletedCount = logsToDelete.Count;

        if (deletedCount > 0)
        {
            _dbSet.RemoveRange(logsToDelete);
            await SaveChangesAsync();
        }

        return deletedCount;
    }

    /// <summary>
    /// 获取最近的备份日志
    /// Gets the most recent backup log
    /// </summary>
    public async Task<BackupLog?> GetMostRecentAsync(int configurationId)
    {
        return await _dbSet
            .Where(bl => bl.BackupConfigId == configurationId)
            .OrderByDescending(bl => bl.StartTime)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// 更新备份状态
    /// Updates backup status
    /// </summary>
    public async Task<bool> UpdateStatusAsync(int id, BackupStatus status, string? errorMessage = null)
    {
        var log = await GetByIdAsync(id);
        if (log == null)
            return false;

        log.Status = status;
        if (!string.IsNullOrWhiteSpace(errorMessage))
            log.ErrorMessage = errorMessage;

        await UpdateAsync(log);
        await SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 完成备份操作
    /// Completes backup operation
    /// </summary>
    public async Task<bool> CompleteBackupAsync(int id, BackupStatus finalStatus, string? filePath = null, long? fileSize = null, string? errorMessage = null)
    {
        var log = await GetByIdAsync(id);
        if (log == null)
            return false;

        log.Status = finalStatus;
        log.EndTime = DateTime.Now;
        
        if (!string.IsNullOrWhiteSpace(filePath))
            log.FilePath = filePath;
        
        if (fileSize.HasValue)
            log.FileSize = fileSize.Value;
        
        if (!string.IsNullOrWhiteSpace(errorMessage))
            log.ErrorMessage = errorMessage;

        await UpdateAsync(log);
        await SaveChangesAsync();
        return true;
    }

    public override async Task<BackupLog> AddAsync(BackupLog entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        // Ensure StartTime is set
        if (entity.StartTime == default)
            entity.StartTime = DateTime.Now;

        return await base.AddAsync(entity);
    }
}