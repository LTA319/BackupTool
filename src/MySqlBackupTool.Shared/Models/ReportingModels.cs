using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// 备份操作的统计信息
/// Statistics for backup operations
/// </summary>
public class BackupStatistics
{
    /// <summary>
    /// 总备份数量
    /// Total number of backups
    /// </summary>
    public int TotalBackups { get; set; }
    
    /// <summary>
    /// 成功备份数量
    /// Number of successful backups
    /// </summary>
    public int SuccessfulBackups { get; set; }
    
    /// <summary>
    /// 失败备份数量
    /// Number of failed backups
    /// </summary>
    public int FailedBackups { get; set; }
    
    /// <summary>
    /// 取消备份数量
    /// Number of cancelled backups
    /// </summary>
    public int CancelledBackups { get; set; }
    
    /// <summary>
    /// 总传输字节数
    /// Total bytes transferred
    /// </summary>
    public long TotalBytesTransferred { get; set; }
    
    /// <summary>
    /// 总持续时间
    /// Total duration
    /// </summary>
    public TimeSpan TotalDuration { get; set; }
    
    /// <summary>
    /// 平均备份大小
    /// Average backup size
    /// </summary>
    public double AverageBackupSize { get; set; }
    
    /// <summary>
    /// 成功率（百分比）
    /// Success rate (percentage)
    /// </summary>
    public double SuccessRate { get; set; }
    
    /// <summary>
    /// 平均持续时间
    /// Average duration
    /// </summary>
    public TimeSpan AverageDuration { get; set; }
}

/// <summary>
/// 综合备份摘要报告
/// Comprehensive backup summary report
/// </summary>
public class BackupSummaryReport
{
    /// <summary>
    /// 报告生成时间，默认为当前UTC时间
    /// Report generation time, defaults to current UTC time
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 报告开始日期
    /// Report start date
    /// </summary>
    public DateTime ReportStartDate { get; set; }
    
    /// <summary>
    /// 报告结束日期
    /// Report end date
    /// </summary>
    public DateTime ReportEndDate { get; set; }
    
    /// <summary>
    /// 报告时间段
    /// Report time period
    /// </summary>
    public TimeSpan ReportPeriod => ReportEndDate - ReportStartDate;

    /// <summary>
    /// 整体统计信息
    /// Overall statistics
    /// </summary>
    public BackupStatistics OverallStatistics { get; set; } = new();

    /// <summary>
    /// 特定配置的统计信息
    /// Configuration-specific statistics
    /// </summary>
    public List<ConfigurationStatistics> ConfigurationStatistics { get; set; } = new();

    /// <summary>
    /// 每日统计明细
    /// Daily breakdown statistics
    /// </summary>
    public List<DailyStatistics> DailyBreakdown { get; set; } = new();

    /// <summary>
    /// 最近的失败记录
    /// Recent failure records
    /// </summary>
    public List<BackupLog> RecentFailures { get; set; } = new();

    /// <summary>
    /// 存储使用统计
    /// Storage usage statistics
    /// </summary>
    public StorageStatistics StorageStatistics { get; set; } = new();

    /// <summary>
    /// 性能指标
    /// Performance metrics
    /// </summary>
    public PerformanceMetrics PerformanceMetrics { get; set; } = new();

    /// <summary>
    /// 获取报告的摘要描述
    /// Gets a summary description of the report
    /// </summary>
    /// <returns>报告摘要描述字符串 / Report summary description string</returns>
    public string GetSummaryDescription()
    {
        var period = ReportPeriod.Days == 1 ? "1 day" : $"{ReportPeriod.Days} days";
        return $"Backup report for {period} ({ReportStartDate:yyyy-MM-dd} to {ReportEndDate:yyyy-MM-dd})";
    }
}

/// <summary>
/// 特定备份配置的统计信息
/// Statistics for a specific backup configuration
/// </summary>
public class ConfigurationStatistics
{
    /// <summary>
    /// 配置ID
    /// Configuration ID
    /// </summary>
    public int ConfigurationId { get; set; }
    
    /// <summary>
    /// 配置名称
    /// Configuration name
    /// </summary>
    public string ConfigurationName { get; set; } = string.Empty;
    
    /// <summary>
    /// 总备份数量
    /// Total number of backups
    /// </summary>
    public int TotalBackups { get; set; }
    
    /// <summary>
    /// 成功备份数量
    /// Number of successful backups
    /// </summary>
    public int SuccessfulBackups { get; set; }
    
    /// <summary>
    /// 失败备份数量
    /// Number of failed backups
    /// </summary>
    public int FailedBackups { get; set; }
    
    /// <summary>
    /// 取消备份数量
    /// Number of cancelled backups
    /// </summary>
    public int CancelledBackups { get; set; }
    
    /// <summary>
    /// 成功率（百分比）
    /// Success rate (percentage)
    /// </summary>
    public double SuccessRate { get; set; }
    
    /// <summary>
    /// 总传输字节数
    /// Total bytes transferred
    /// </summary>
    public long TotalBytesTransferred { get; set; }
    
    /// <summary>
    /// 总持续时间
    /// Total duration
    /// </summary>
    public TimeSpan TotalDuration { get; set; }
    
    /// <summary>
    /// 平均持续时间
    /// Average duration
    /// </summary>
    public TimeSpan AverageDuration { get; set; }
    
    /// <summary>
    /// 平均备份大小
    /// Average backup size
    /// </summary>
    public double AverageBackupSize { get; set; }
    
    /// <summary>
    /// 最后备份时间
    /// Last backup time
    /// </summary>
    public DateTime? LastBackupTime { get; set; }
    
    /// <summary>
    /// 最后备份状态
    /// Last backup status
    /// </summary>
    public BackupStatus? LastBackupStatus { get; set; }
}

/// <summary>
/// Daily statistics breakdown
/// </summary>
public class DailyStatistics
{
    public DateTime Date { get; set; }
    public int TotalBackups { get; set; }
    public int SuccessfulBackups { get; set; }
    public int FailedBackups { get; set; }
    public int CancelledBackups { get; set; }
    public long TotalBytesTransferred { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public double SuccessRate => TotalBackups > 0 ? (double)SuccessfulBackups / TotalBackups * 100 : 0;
}

/// <summary>
/// Storage usage statistics
/// </summary>
public class StorageStatistics
{
    public long TotalStorageUsed { get; set; }
    public long LargestBackupSize { get; set; }
    public long SmallestBackupSize { get; set; }
    public double AverageBackupSize { get; set; }
    public int TotalBackupFiles { get; set; }
    public List<StorageByConfiguration> StorageByConfiguration { get; set; } = new();

    /// <summary>
    /// Gets formatted storage size string
    /// </summary>
    public string GetFormattedTotalStorage() => FormatBytes(TotalStorageUsed);
    public string GetFormattedLargestBackup() => FormatBytes(LargestBackupSize);
    public string GetFormattedSmallestBackup() => FormatBytes(SmallestBackupSize);
    public string GetFormattedAverageSize() => FormatBytes((long)AverageBackupSize);

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";
        
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}

/// <summary>
/// Storage usage by configuration
/// </summary>
public class StorageByConfiguration
{
    public int ConfigurationId { get; set; }
    public string ConfigurationName { get; set; } = string.Empty;
    public long StorageUsed { get; set; }
    public int BackupCount { get; set; }
    public double AverageBackupSize { get; set; }
    public string GetFormattedStorageUsed() => FormatBytes(StorageUsed);

    public static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";
        
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}

/// <summary>
/// Performance metrics for backup operations
/// </summary>
public class PerformanceMetrics
{
    public double AverageTransferRate { get; set; } // bytes per second
    public double FastestTransferRate { get; set; }
    public double SlowestTransferRate { get; set; }
    public TimeSpan FastestBackupTime { get; set; }
    public TimeSpan SlowestBackupTime { get; set; }
    public TimeSpan AverageBackupTime { get; set; }
    public double CompressionRatio { get; set; } // average compression achieved

    /// <summary>
    /// Gets formatted transfer rate strings
    /// </summary>
    public string GetFormattedAverageRate() => FormatTransferRate(AverageTransferRate);
    public string GetFormattedFastestRate() => FormatTransferRate(FastestTransferRate);
    public string GetFormattedSlowestRate() => FormatTransferRate(SlowestTransferRate);

    private static string FormatTransferRate(double bytesPerSecond)
    {
        if (bytesPerSecond < 1024)
            return $"{bytesPerSecond:F1} B/s";
        else if (bytesPerSecond < 1024 * 1024)
            return $"{bytesPerSecond / 1024:F1} KB/s";
        else if (bytesPerSecond < 1024 * 1024 * 1024)
            return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
        else
            return $"{bytesPerSecond / (1024 * 1024 * 1024):F1} GB/s";
    }
}

/// <summary>
/// Criteria for generating backup reports
/// </summary>
public class ReportCriteria
{
    public DateTime StartDate { get; set; } = DateTime.UtcNow.AddDays(-30);
    public DateTime EndDate { get; set; } = DateTime.UtcNow;
    public int? ConfigurationId { get; set; }
    public bool IncludeDailyBreakdown { get; set; } = true;
    public bool IncludeConfigurationBreakdown { get; set; } = true;
    public bool IncludeRecentFailures { get; set; } = true;
    public bool IncludeStorageStatistics { get; set; } = true;
    public bool IncludePerformanceMetrics { get; set; } = true;
    public int MaxRecentFailures { get; set; } = 10;
}

/// <summary>
/// Retention policy execution result
/// </summary>
public class RetentionExecutionResult
{
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration { get; set; }
    public int LogsDeleted { get; set; }
    public int FilesDeleted { get; set; }
    public long BytesFreed { get; set; }
    public List<string> DeletedFiles { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<RetentionPolicy> AppliedPolicies { get; set; } = new();
    public bool Success => Errors.Count == 0;

    /// <summary>
    /// Gets a summary of the retention execution
    /// </summary>
    public string GetSummary()
    {
        if (Success)
        {
            var bytesStr = FormatBytes(BytesFreed);
            return $"Successfully cleaned up {LogsDeleted} logs and {FilesDeleted} files, freed {bytesStr}";
        }
        else
        {
            return $"Retention execution completed with {Errors.Count} errors";
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";
        
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}