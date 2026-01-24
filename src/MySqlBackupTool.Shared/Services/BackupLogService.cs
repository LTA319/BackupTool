using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// High-level service for backup logging operations
/// </summary>
public class BackupLogService : IBackupLogService
{
    private readonly IBackupLogRepository _backupLogRepository;
    private readonly ILogger<BackupLogService> _logger;

    public BackupLogService(IBackupLogRepository backupLogRepository, ILogger<BackupLogService> logger)
    {
        _backupLogRepository = backupLogRepository ?? throw new ArgumentNullException(nameof(backupLogRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BackupLog> StartBackupAsync(int configurationId, string? resumeToken = null)
    {
        _logger.LogInformation("Starting backup operation for configuration {ConfigurationId}", configurationId);

        var backupLog = new BackupLog
        {
            BackupConfigId = configurationId,
            StartTime = DateTime.UtcNow,
            Status = BackupStatus.Queued,
            ResumeToken = resumeToken
        };

        var result = await _backupLogRepository.AddAsync(backupLog);
        await _backupLogRepository.SaveChangesAsync();

        _logger.LogInformation("Created backup log {BackupLogId} for configuration {ConfigurationId}", 
            result.Id, configurationId);

        return result;
    }

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

    public async Task LogTransferChunkAsync(int backupLogId, int chunkIndex, long chunkSize, string status, string? errorMessage = null)
    {
        _logger.LogDebug("Logging transfer chunk {ChunkIndex} for backup {BackupLogId}", chunkIndex, backupLogId);

        var transferLog = new TransferLog
        {
            BackupLogId = backupLogId,
            ChunkIndex = chunkIndex,
            ChunkSize = chunkSize,
            TransferTime = DateTime.UtcNow,
            Status = status,
            ErrorMessage = errorMessage
        };

        // Add transfer log through the backup log repository's context
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

        // Apply additional filters
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

    public async Task<BackupLog?> GetBackupLogDetailsAsync(int backupLogId)
    {
        _logger.LogDebug("Getting backup log details for {BackupLogId}", backupLogId);
        return await _backupLogRepository.GetWithTransferLogsAsync(backupLogId);
    }

    public async Task<BackupStatistics> GetBackupStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-30);
        var end = endDate ?? DateTime.UtcNow;

        _logger.LogDebug("Getting backup statistics from {StartDate} to {EndDate}", start, end);

        return await _backupLogRepository.GetStatisticsAsync(start, end);
    }

    public async Task<BackupLogSearchResult> SearchBackupLogsAsync(BackupLogSearchCriteria criteria)
    {
        _logger.LogDebug("Searching backup logs with criteria");

        // Start with all logs
        var query = await _backupLogRepository.GetAllAsync();
        var filteredQuery = query.AsQueryable();

        // Apply filters
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

        // Get total count before pagination
        var totalCount = filteredQuery.Count();

        // Apply sorting
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

        // Apply pagination
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

    public async Task<IEnumerable<BackupLog>> GetRunningBackupsAsync()
    {
        _logger.LogDebug("Getting running backup operations");
        return await _backupLogRepository.GetRunningBackupsAsync();
    }

    public async Task CancelBackupAsync(int backupLogId, string reason)
    {
        _logger.LogInformation("Cancelling backup {BackupLogId}: {Reason}", backupLogId, reason);

        var success = await _backupLogRepository.UpdateStatusAsync(backupLogId, BackupStatus.Cancelled, reason);
        
        if (!success)
        {
            _logger.LogWarning("Failed to cancel backup log {BackupLogId}", backupLogId);
            throw new InvalidOperationException($"Failed to cancel backup log {backupLogId}");
        }

        // Complete the backup with cancelled status
        await CompleteBackupAsync(backupLogId, BackupStatus.Cancelled, errorMessage: reason);
    }

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
            // If only max count is specified, use a very large age (effectively unlimited)
            deletedCount += await _backupLogRepository.CleanupOldLogsAsync(int.MaxValue, policy.MaxCount.Value);
        }

        if (deletedCount > 0)
        {
            _logger.LogInformation("Cleaned up {DeletedCount} old backup logs", deletedCount);
        }

        return deletedCount;
    }
}