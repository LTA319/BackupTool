using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 备份日志操作的高级服务 / High-level service for backup logging operations
/// </summary>
public class BackupLogService : IBackupLogService
{
    /// <summary>
    /// 备份日志存储库 / Backup log repository
    /// </summary>
    private readonly IBackupLogRepository _backupLogRepository;
    
    /// <summary>
    /// 传输日志服务 / Transfer log service
    /// </summary>
    private readonly ITransferLogService? _transferLogService;
    
    /// <summary>
    /// 日志记录器 / Logger
    /// </summary>
    private readonly ILogger<BackupLogService> _logger;

    /// <summary>
    /// 初始化备份日志服务 / Initializes the backup log service
    /// </summary>
    /// <param name="backupLogRepository">备份日志存储库 / Backup log repository</param>
    /// <param name="logger">日志记录器 / Logger</param>
    /// <param name="transferLogService">传输日志服务（可选） / Transfer log service (optional)</param>
    /// <exception cref="ArgumentNullException">当必需参数为null时抛出 / Thrown when required parameters are null</exception>
    public BackupLogService(
        IBackupLogRepository backupLogRepository, 
        ILogger<BackupLogService> logger,
        ITransferLogService? transferLogService = null)
    {
        _backupLogRepository = backupLogRepository ?? throw new ArgumentNullException(nameof(backupLogRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _transferLogService = transferLogService;
    }

    /// <summary>
    /// 启动备份操作并创建日志记录 / Starts a backup operation and creates a log record
    /// </summary>
    /// <param name="configurationId">配置ID / Configuration ID</param>
    /// <param name="resumeToken">恢复令牌，用于断点续传 / Resume token for resuming interrupted backups</param>
    /// <returns>创建的备份日志 / Created backup log</returns>
    public async Task<BackupLog> StartBackupAsync(int configurationId, string? resumeToken = null)
    {
        _logger.LogInformation("Starting backup operation for configuration {ConfigurationId}", configurationId);

        var backupLog = new BackupLog
        {
            BackupConfigId = configurationId,
            StartTime = DateTime.Now,
            Status = BackupStatus.Queued,
            ResumeToken = resumeToken
        };

        var result = await _backupLogRepository.AddAsync(backupLog);
        await _backupLogRepository.SaveChangesAsync();

        _logger.LogInformation("Created backup log {BackupLogId} for configuration {ConfigurationId}", 
            result.Id, configurationId);

        return result;
    }

    /// <summary>
    /// 更新备份状态 / Updates backup status
    /// </summary>
    /// <param name="backupLogId">备份日志ID / Backup log ID</param>
    /// <param name="status">新状态 / New status</param>
    /// <param name="currentOperation">当前操作描述 / Current operation description</param>
    /// <exception cref="InvalidOperationException">当更新失败时抛出 / Thrown when update fails</exception>
    public async Task UpdateBackupStatusAsync(int backupLogId, BackupStatus status, string? currentOperation = null)
    {
        _logger.LogDebug("Updating backup {BackupLogId} status to {Status}", backupLogId, status);

        var success = await _backupLogRepository.UpdateStatusAsync(backupLogId, status, currentOperation);
        
        if (!success)
        {
            _logger.LogWarning("Failed to update status for backup log {BackupLogId}", backupLogId);
            throw new InvalidOperationException($"Failed to update backup log {backupLogId}");
        }

        _logger.LogInformation("Updated backup {BackupLogId} status to {Status}", backupLogId, status);
    }

    /// <summary>
    /// 完成备份操作并更新最终状态 / Completes backup operation and updates final status
    /// </summary>
    /// <param name="backupLogId">备份日志ID / Backup log ID</param>
    /// <param name="finalStatus">最终状态 / Final status</param>
    /// <param name="filePath">备份文件路径 / Backup file path</param>
    /// <param name="fileSize">文件大小 / File size</param>
    /// <param name="errorMessage">错误消息 / Error message</param>
    /// <exception cref="InvalidOperationException">当完成操作失败时抛出 / Thrown when completion fails</exception>
    public async Task CompleteBackupAsync(int backupLogId, BackupStatus finalStatus, string? filePath = null, long? fileSize = null, string? errorMessage = null)
    {
        _logger.LogInformation("Completing backup {BackupLogId} with status {Status}", backupLogId, finalStatus);

        var success = await _backupLogRepository.CompleteBackupAsync(backupLogId, finalStatus, filePath, fileSize, errorMessage);
        
        if (!success)
        {
            _logger.LogWarning("Failed to complete backup log {BackupLogId}", backupLogId);
            throw new InvalidOperationException($"Failed to complete backup log {backupLogId}");
        }

        if (finalStatus == BackupStatus.Completed)
        {
            _logger.LogInformation("Backup {BackupLogId} completed successfully. File: {FilePath}, Size: {FileSize} bytes", 
                backupLogId, filePath, fileSize);
        }
        else if (finalStatus == BackupStatus.Failed)
        {
            _logger.LogError("Backup {BackupLogId} failed: {ErrorMessage}", backupLogId, errorMessage);
        }
        else if (finalStatus == BackupStatus.Cancelled)
        {
            _logger.LogWarning("Backup {BackupLogId} was cancelled", backupLogId);
        }
    }

    /// <summary>
    /// 记录传输块信息 / Logs transfer chunk information
    /// </summary>
    /// <param name="backupLogId">备份日志ID / Backup log ID</param>
    /// <param name="chunkIndex">块索引 / Chunk index</param>
    /// <param name="chunkSize">块大小 / Chunk size</param>
    /// <param name="status">传输状态 / Transfer status</param>
    /// <param name="errorMessage">错误消息 / Error message</param>
    /// <exception cref="InvalidOperationException">当备份日志不存在时抛出 / Thrown when backup log is not found</exception>
    public async Task LogTransferChunkAsync(int backupLogId, int chunkIndex, long chunkSize, string status, string? errorMessage = null)
    {
        _logger.LogDebug("Logging transfer chunk {ChunkIndex} for backup {BackupLogId}", chunkIndex, backupLogId);

        var transferLog = new TransferLog
        {
            BackupLogId = backupLogId,
            ChunkIndex = chunkIndex,
            ChunkSize = chunkSize,
            TransferTime = DateTime.Now,
            Status = status,
            ErrorMessage = errorMessage
        };

        // 通过备份日志存储库的上下文添加传输日志 / Add transfer log through the backup log repository's context
        var backupLog = await _backupLogRepository.GetByIdAsync(backupLogId);
        if (backupLog == null)
        {
            _logger.LogWarning("Backup log {BackupLogId} not found for transfer chunk logging", backupLogId);
            throw new InvalidOperationException($"Backup log {backupLogId} not found");
        }

        backupLog.TransferLogs.Add(transferLog);
        await _backupLogRepository.UpdateAsync(backupLog);
        await _backupLogRepository.SaveChangesAsync();

        if (status == "Failed" && !string.IsNullOrEmpty(errorMessage))
        {
            _logger.LogWarning("Transfer chunk {ChunkIndex} failed for backup {BackupLogId}: {ErrorMessage}", 
                chunkIndex, backupLogId, errorMessage);
        }
    }

    /// <summary>
    /// 获取备份日志列表 / Gets backup logs list
    /// </summary>
    /// <param name="filter">过滤条件 / Filter criteria</param>
    /// <returns>备份日志列表 / List of backup logs</returns>
    public async Task<IEnumerable<BackupLog>> GetBackupLogsAsync(BackupLogFilter? filter = null)
    {
        _logger.LogDebug("Getting backup logs with filter");

        if (filter == null)
        {
            var allLogs = await _backupLogRepository.GetAllAsync();
            return allLogs.OrderByDescending(log => log.StartTime);
        }

        IEnumerable<BackupLog> logs;

        if (filter.ConfigurationId.HasValue)
        {
            logs = await _backupLogRepository.GetByConfigurationIdAsync(filter.ConfigurationId.Value);
        }
        else if (filter.Status.HasValue)
        {
            logs = await _backupLogRepository.GetByStatusAsync(filter.Status.Value);
        }
        else if (filter.StartDate.HasValue && filter.EndDate.HasValue)
        {
            logs = await _backupLogRepository.GetByDateRangeAsync(filter.StartDate.Value, filter.EndDate.Value);
        }
        else
        {
            logs = await _backupLogRepository.GetAllAsync();
            logs = logs.OrderByDescending(log => log.StartTime);
        }

        // 应用额外的过滤器 / Apply additional filters
        if (filter.StartDate.HasValue && !filter.EndDate.HasValue)
        {
            logs = logs.Where(log => log.StartTime >= filter.StartDate.Value);
        }
        
        if (filter.EndDate.HasValue && !filter.StartDate.HasValue)
        {
            logs = logs.Where(log => log.StartTime <= filter.EndDate.Value);
        }

        if (filter.MaxResults.HasValue)
        {
            logs = logs.Take(filter.MaxResults.Value);
        }

        return logs;
    }

    /// <summary>
    /// 获取备份日志详细信息 / Gets backup log details
    /// </summary>
    /// <param name="backupLogId">备份日志ID / Backup log ID</param>
    /// <returns>备份日志详细信息，如果未找到则返回null / Backup log details, or null if not found</returns>
    public async Task<BackupLog?> GetBackupLogDetailsAsync(int backupLogId)
    {
        _logger.LogDebug("Getting backup log details for {BackupLogId}", backupLogId);
        return await _backupLogRepository.GetWithTransferLogsAsync(backupLogId);
    }

    /// <summary>
    /// 获取备份统计信息 / Gets backup statistics
    /// </summary>
    /// <param name="startDate">开始日期 / Start date</param>
    /// <param name="endDate">结束日期 / End date</param>
    /// <returns>备份统计信息 / Backup statistics</returns>
    public async Task<BackupStatistics> GetBackupStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var start = startDate ?? DateTime.Now.AddDays(-30);
        var end = endDate ?? DateTime.Now;

        _logger.LogDebug("Getting backup statistics from {StartDate} to {EndDate}", start, end);

        return await _backupLogRepository.GetStatisticsAsync(start, end);
    }

    /// <summary>
    /// 搜索备份日志 / Searches backup logs
    /// </summary>
    /// <param name="criteria">搜索条件 / Search criteria</param>
    /// <returns>搜索结果 / Search results</returns>
    public async Task<BackupLogSearchResult> SearchBackupLogsAsync(BackupLogSearchCriteria criteria)
    {
        _logger.LogDebug("Searching backup logs with criteria");

        // 从所有日志开始 / Start with all logs
        var query = await _backupLogRepository.GetAllAsync();
        var filteredQuery = query.AsQueryable();

        // 应用过滤器 / Apply filters
        if (criteria.ConfigurationId.HasValue)
        {
            filteredQuery = filteredQuery.Where(log => log.BackupConfigId == criteria.ConfigurationId.Value);
        }

        if (criteria.Status.HasValue)
        {
            filteredQuery = filteredQuery.Where(log => log.Status == criteria.Status.Value);
        }

        if (criteria.StartDate.HasValue)
        {
            filteredQuery = filteredQuery.Where(log => log.StartTime >= criteria.StartDate.Value);
        }

        if (criteria.EndDate.HasValue)
        {
            filteredQuery = filteredQuery.Where(log => log.StartTime <= criteria.EndDate.Value);
        }

        if (criteria.MinFileSize.HasValue)
        {
            filteredQuery = filteredQuery.Where(log => log.FileSize >= criteria.MinFileSize.Value);
        }

        if (criteria.MaxFileSize.HasValue)
        {
            filteredQuery = filteredQuery.Where(log => log.FileSize <= criteria.MaxFileSize.Value);
        }

        if (criteria.HasErrors.HasValue)
        {
            if (criteria.HasErrors.Value)
            {
                filteredQuery = filteredQuery.Where(log => !string.IsNullOrEmpty(log.ErrorMessage));
            }
            else
            {
                filteredQuery = filteredQuery.Where(log => string.IsNullOrEmpty(log.ErrorMessage));
            }
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchText))
        {
            var searchText = criteria.SearchText.ToLower();
            filteredQuery = filteredQuery.Where(log => 
                (log.FilePath != null && log.FilePath.ToLower().Contains(searchText)) ||
                (log.ErrorMessage != null && log.ErrorMessage.ToLower().Contains(searchText)) ||
                (log.ResumeToken != null && log.ResumeToken.ToLower().Contains(searchText)));
        }

        // 在分页前获取总数 / Get total count before pagination
        var totalCount = filteredQuery.Count();

        // 应用排序 / Apply sorting
        filteredQuery = criteria.SortBy.ToLower() switch
        {
            "starttime" => criteria.SortDescending 
                ? filteredQuery.OrderByDescending(log => log.StartTime)
                : filteredQuery.OrderBy(log => log.StartTime),
            "endtime" => criteria.SortDescending
                ? filteredQuery.OrderByDescending(log => log.EndTime)
                : filteredQuery.OrderBy(log => log.EndTime),
            "status" => criteria.SortDescending
                ? filteredQuery.OrderByDescending(log => log.Status)
                : filteredQuery.OrderBy(log => log.Status),
            "filesize" => criteria.SortDescending
                ? filteredQuery.OrderByDescending(log => log.FileSize)
                : filteredQuery.OrderBy(log => log.FileSize),
            _ => criteria.SortDescending
                ? filteredQuery.OrderByDescending(log => log.StartTime)
                : filteredQuery.OrderBy(log => log.StartTime)
        };

        // 应用分页 / Apply pagination
        var pagedLogs = filteredQuery
            .Skip((criteria.PageNumber - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToList();

        return new BackupLogSearchResult
        {
            Logs = pagedLogs,
            TotalCount = totalCount,
            PageNumber = criteria.PageNumber,
            PageSize = criteria.PageSize
        };
    }

    /// <summary>
    /// 获取正在运行的备份操作 / Gets running backup operations
    /// </summary>
    /// <returns>正在运行的备份日志列表 / List of running backup logs</returns>
    public async Task<IEnumerable<BackupLog>> GetRunningBackupsAsync()
    {
        _logger.LogDebug("Getting running backup operations");
        return await _backupLogRepository.GetRunningBackupsAsync();
    }

    /// <summary>
    /// 取消备份操作 / Cancels backup operation
    /// </summary>
    /// <param name="backupLogId">备份日志ID / Backup log ID</param>
    /// <param name="reason">取消原因 / Cancellation reason</param>
    /// <exception cref="InvalidOperationException">当取消失败时抛出 / Thrown when cancellation fails</exception>
    public async Task CancelBackupAsync(int backupLogId, string reason)
    {
        _logger.LogInformation("Cancelling backup {BackupLogId}: {Reason}", backupLogId, reason);

        var success = await _backupLogRepository.UpdateStatusAsync(backupLogId, BackupStatus.Cancelled, reason);
        
        if (!success)
        {
            _logger.LogWarning("Failed to cancel backup log {BackupLogId}", backupLogId);
            throw new InvalidOperationException($"Failed to cancel backup log {backupLogId}");
        }

        // 以取消状态完成备份 / Complete the backup with cancelled status
        await CompleteBackupAsync(backupLogId, BackupStatus.Cancelled, errorMessage: reason);
    }

    /// <summary>
    /// 清理旧的日志记录 / Cleans up old log records
    /// </summary>
    /// <param name="policy">保留策略 / Retention policy</param>
    /// <returns>删除的日志数量 / Number of deleted logs</returns>
    public async Task<int> CleanupOldLogsAsync(RetentionPolicy policy)
    {
        _logger.LogInformation("Cleaning up old backup logs using policy: {PolicyName}", policy.Name);

        var deletedCount = 0;

        if (policy.MaxAgeDays.HasValue)
        {
            deletedCount += await _backupLogRepository.CleanupOldLogsAsync(policy.MaxAgeDays.Value, policy.MaxCount);
        }
        else if (policy.MaxCount.HasValue)
        {
            // 如果只指定了最大数量，使用一个非常大的年龄（实际上无限制）/ If only max count is specified, use a very large age (effectively unlimited)
            deletedCount += await _backupLogRepository.CleanupOldLogsAsync(int.MaxValue, policy.MaxCount.Value);
        }

        if (deletedCount > 0)
        {
            _logger.LogInformation("Cleaned up {DeletedCount} old backup logs", deletedCount);
        }

        return deletedCount;
    }
}