using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 备份日志操作的高级服务接口
/// High-level service interface for backup logging operations
/// </summary>
public interface IBackupLogService
{
    /// <summary>
    /// 开始新的备份操作日志
    /// Starts a new backup operation log
    /// </summary>
    Task<BackupLog> StartBackupAsync(int configurationId, string? resumeToken = null);

    /// <summary>
    /// 更新备份操作的状态
    /// Updates the status of a backup operation
    /// </summary>
    Task UpdateBackupStatusAsync(int backupLogId, BackupStatus status, string? currentOperation = null);

    /// <summary>
    /// 完成备份操作，设置最终状态和详细信息
    /// Completes a backup operation with final status and details
    /// </summary>
    Task CompleteBackupAsync(int backupLogId, BackupStatus finalStatus, string? filePath = null, long? fileSize = null, string? errorMessage = null);

    /// <summary>
    /// 记录传输分块操作
    /// Logs a transfer chunk operation
    /// </summary>
    Task LogTransferChunkAsync(int backupLogId, int chunkIndex, long chunkSize, string status, string? errorMessage = null);

    /// <summary>
    /// 获取备份日志，支持可选过滤
    /// Gets backup logs with optional filtering
    /// </summary>
    Task<IEnumerable<BackupLog>> GetBackupLogsAsync(BackupLogFilter? filter = null);

    /// <summary>
    /// 获取包含传输日志的详细备份日志
    /// Gets detailed backup log with transfer logs
    /// </summary>
    Task<BackupLog?> GetBackupLogDetailsAsync(int backupLogId);

    /// <summary>
    /// 获取用于报告的备份统计信息
    /// Gets backup statistics for reporting
    /// </summary>
    Task<BackupStatistics> GetBackupStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// 根据条件搜索备份日志
    /// Searches backup logs based on criteria
    /// </summary>
    Task<BackupLogSearchResult> SearchBackupLogsAsync(BackupLogSearchCriteria criteria);

    /// <summary>
    /// 获取正在运行的备份操作
    /// Gets running backup operations
    /// </summary>
    Task<IEnumerable<BackupLog>> GetRunningBackupsAsync();

    /// <summary>
    /// 取消正在运行的备份操作
    /// Cancels a running backup operation
    /// </summary>
    Task CancelBackupAsync(int backupLogId, string reason);

    /// <summary>
    /// 根据保留策略清理旧的备份日志
    /// Cleans up old backup logs based on retention policy
    /// </summary>
    Task<int> CleanupOldLogsAsync(RetentionPolicy policy);
}

/// <summary>
/// 备份日志的过滤条件
/// Filter criteria for backup logs
/// </summary>
public class BackupLogFilter
{
    /// <summary>
    /// 配置ID
    /// Configuration ID
    /// </summary>
    public int? ConfigurationId { get; set; }
    
    /// <summary>
    /// 备份状态
    /// Backup status
    /// </summary>
    public BackupStatus? Status { get; set; }
    
    /// <summary>
    /// 开始日期
    /// Start date
    /// </summary>
    public DateTime? StartDate { get; set; }
    
    /// <summary>
    /// 结束日期
    /// End date
    /// </summary>
    public DateTime? EndDate { get; set; }
    
    /// <summary>
    /// 最大结果数
    /// Maximum number of results
    /// </summary>
    public int? MaxResults { get; set; }
    
    /// <summary>
    /// 是否包含传输日志，默认为false
    /// Whether to include transfer logs, defaults to false
    /// </summary>
    public bool IncludeTransferLogs { get; set; } = false;
}

/// <summary>
/// 备份日志的搜索条件
/// Search criteria for backup logs
/// </summary>
public class BackupLogSearchCriteria
{
    /// <summary>
    /// 搜索文本
    /// Search text
    /// </summary>
    public string? SearchText { get; set; }
    
    /// <summary>
    /// 配置ID
    /// Configuration ID
    /// </summary>
    public int? ConfigurationId { get; set; }
    
    /// <summary>
    /// 备份状态
    /// Backup status
    /// </summary>
    public BackupStatus? Status { get; set; }
    
    /// <summary>
    /// 开始日期
    /// Start date
    /// </summary>
    public DateTime? StartDate { get; set; }
    
    /// <summary>
    /// 结束日期
    /// End date
    /// </summary>
    public DateTime? EndDate { get; set; }
    
    /// <summary>
    /// 最小文件大小
    /// Minimum file size
    /// </summary>
    public long? MinFileSize { get; set; }
    
    /// <summary>
    /// 最大文件大小
    /// Maximum file size
    /// </summary>
    public long? MaxFileSize { get; set; }
    
    /// <summary>
    /// 是否有错误
    /// Whether has errors
    /// </summary>
    public bool? HasErrors { get; set; }
    
    /// <summary>
    /// 页码，默认为1
    /// Page number, defaults to 1
    /// </summary>
    public int PageNumber { get; set; } = 1;
    
    /// <summary>
    /// 页面大小，默认为50
    /// Page size, defaults to 50
    /// </summary>
    public int PageSize { get; set; } = 50;
    
    /// <summary>
    /// 排序字段，默认为"StartTime"
    /// Sort field, defaults to "StartTime"
    /// </summary>
    public string SortBy { get; set; } = "StartTime";
    
    /// <summary>
    /// 是否降序排序，默认为true
    /// Whether to sort descending, defaults to true
    /// </summary>
    public bool SortDescending { get; set; } = true;
}

/// <summary>
/// 备份日志的搜索结果
/// Search result for backup logs
/// </summary>
public class BackupLogSearchResult
{
    /// <summary>
    /// 日志列表
    /// List of logs
    /// </summary>
    public IEnumerable<BackupLog> Logs { get; set; } = new List<BackupLog>();
    
    /// <summary>
    /// 总数量
    /// Total count
    /// </summary>
    public int TotalCount { get; set; }
    
    /// <summary>
    /// 页码
    /// Page number
    /// </summary>
    public int PageNumber { get; set; }
    
    /// <summary>
    /// 页面大小
    /// Page size
    /// </summary>
    public int PageSize { get; set; }
    
    /// <summary>
    /// 总页数
    /// Total pages
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    
    /// <summary>
    /// 是否有下一页
    /// Whether has next page
    /// </summary>
    public bool HasNextPage => PageNumber < TotalPages;
    
    /// <summary>
    /// 是否有上一页
    /// Whether has previous page
    /// </summary>
    public bool HasPreviousPage => PageNumber > 1;
}