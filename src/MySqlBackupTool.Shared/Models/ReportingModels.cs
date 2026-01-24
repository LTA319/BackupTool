using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// Statistics for backup operations
/// </summary>
public class BackupStatistics
{
    public int TotalBackups { get; set; }
    public int SuccessfulBackups { get; set; }
    public int FailedBackups { get; set; }
    public int CancelledBackups { get; set; }
    public long TotalBytesTransferred { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public double AverageBackupSize { get; set; }
    public double SuccessRate { get; set; }
    public TimeSpan AverageDuration { get; set; }
}

/// <summary>
/// Comprehensive backup summary report
/// </summary>
public class BackupSummaryReport
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime ReportStartDate { get; set; }
    public DateTime ReportEndDate { get; set; }
    public TimeSpan ReportPeriod => ReportEndDate - ReportStartDate;

    // Overall Statistics
    public BackupStatistics OverallStatistics { get; set; } = new();

    // Configuration-specific statistics
    public List<ConfigurationStatistics> ConfigurationStatistics { get; set; } = new();

    // Daily breakdown
    public List<DailyStatistics> DailyBreakdown { get; set; } = new();

    // Recent failures
    public List<BackupLog> RecentFailures { get; set; } = new();

    // Storage usage
    public StorageStatistics StorageStatistics { get; set; } = new();

    // Performance metrics
    public PerformanceMetrics PerformanceMetrics { get; set; } = new();

    /// <summary>
    /// Gets a summary description of the report
    /// </summary>
    public string GetSummaryDescription()
    {
        var period = ReportPeriod.Days == 1 ? "1 day" : $"{ReportPeriod.Days} days";
        return $"Backup report for {period} ({ReportStartDate:yyyy-MM-dd} to {ReportEndDate:yyyy-MM-dd})";
    }
}

/// <summary>
/// Statistics for a specific backup configuration
/// </summary>
public class ConfigurationStatistics
{
    public int ConfigurationId { get; set; }
    public string ConfigurationName { get; set; } = string.Empty;
    public int TotalBackups { get; set; }
    public int SuccessfulBackups { get; set; }
    public int FailedBackups { get; set; }
    public int CancelledBackups { get; set; }
    public double SuccessRate { get; set; }
    public long TotalBytesTransferred { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public double AverageBackupSize { get; set; }
    public DateTime? LastBackupTime { get; set; }
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