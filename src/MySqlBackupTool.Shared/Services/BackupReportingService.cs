using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 生成综合备份报告的服务 / Service for generating comprehensive backup reports
/// </summary>
public class BackupReportingService
{
    /// <summary>
    /// 备份日志存储库 / Backup log repository
    /// </summary>
    private readonly IBackupLogRepository _backupLogRepository;
    
    /// <summary>
    /// 备份配置存储库 / Backup configuration repository
    /// </summary>
    private readonly IBackupConfigurationRepository _backupConfigurationRepository;
    
    /// <summary>
    /// 日志记录器 / Logger
    /// </summary>
    private readonly ILogger<BackupReportingService> _logger;

    /// <summary>
    /// 初始化备份报告服务 / Initializes the backup reporting service
    /// </summary>
    /// <param name="backupLogRepository">备份日志存储库 / Backup log repository</param>
    /// <param name="backupConfigurationRepository">备份配置存储库 / Backup configuration repository</param>
    /// <param name="logger">日志记录器 / Logger</param>
    /// <exception cref="ArgumentNullException">当必需参数为null时抛出 / Thrown when required parameters are null</exception>
    public BackupReportingService(
        IBackupLogRepository backupLogRepository,
        IBackupConfigurationRepository backupConfigurationRepository,
        ILogger<BackupReportingService> logger)
    {
        _backupLogRepository = backupLogRepository ?? throw new ArgumentNullException(nameof(backupLogRepository));
        _backupConfigurationRepository = backupConfigurationRepository ?? throw new ArgumentNullException(nameof(backupConfigurationRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 生成综合备份摘要报告 / Generates a comprehensive backup summary report
    /// </summary>
    /// <param name="criteria">报告条件 / Report criteria</param>
    /// <returns>备份摘要报告 / Backup summary report</returns>
    public async Task<BackupSummaryReport> GenerateReportAsync(ReportCriteria criteria)
    {
        _logger.LogInformation("Generating backup report from {StartDate} to {EndDate}", 
            criteria.StartDate, criteria.EndDate);

        var report = new BackupSummaryReport
        {
            GeneratedAt = DateTime.UtcNow,
            ReportStartDate = criteria.StartDate,
            ReportEndDate = criteria.EndDate
        };

        try
        {
            // 获取指定时间段内的所有备份日志 / Get all backup logs for the period
            var backupLogs = await _backupLogRepository.GetByDateRangeAsync(criteria.StartDate, criteria.EndDate);
            
            // 如果指定了配置，则按配置过滤 / Filter by configuration if specified
            if (criteria.ConfigurationId.HasValue)
            {
                backupLogs = backupLogs.Where(bl => bl.BackupConfigId == criteria.ConfigurationId.Value);
            }

            var backupLogsList = backupLogs.ToList();

            // 生成总体统计信息 / Generate overall statistics
            report.OverallStatistics = await GenerateOverallStatisticsAsync(backupLogsList);

            // 生成配置特定的统计信息 / Generate configuration-specific statistics
            if (criteria.IncludeConfigurationBreakdown)
            {
                report.ConfigurationStatistics = await GenerateConfigurationStatisticsAsync(backupLogsList);
            }

            // 生成每日分解 / Generate daily breakdown
            if (criteria.IncludeDailyBreakdown)
            {
                report.DailyBreakdown = GenerateDailyBreakdown(backupLogsList, criteria.StartDate, criteria.EndDate);
            }

            // 包含最近的失败记录 / Include recent failures
            if (criteria.IncludeRecentFailures)
            {
                report.RecentFailures = backupLogsList
                    .Where(bl => bl.Status == BackupStatus.Failed)
                    .OrderByDescending(bl => bl.StartTime)
                    .Take(criteria.MaxRecentFailures)
                    .ToList();
            }

            // 生成存储统计信息 / Generate storage statistics
            if (criteria.IncludeStorageStatistics)
            {
                report.StorageStatistics = await GenerateStorageStatisticsAsync(backupLogsList);
            }

            // 生成性能指标 / Generate performance metrics
            if (criteria.IncludePerformanceMetrics)
            {
                report.PerformanceMetrics = GeneratePerformanceMetrics(backupLogsList);
            }

            _logger.LogInformation("Successfully generated backup report with {TotalBackups} backups", 
                report.OverallStatistics.TotalBackups);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating backup report");
            throw;
        }
    }

    /// <summary>
    /// 生成总体统计信息 / Generates overall statistics
    /// </summary>
    /// <param name="backupLogs">备份日志列表 / List of backup logs</param>
    /// <returns>备份统计信息 / Backup statistics</returns>
    private async Task<BackupStatistics> GenerateOverallStatisticsAsync(List<BackupLog> backupLogs)
    {
        var totalBackups = backupLogs.Count;
        var successfulBackups = backupLogs.Count(bl => bl.Status == BackupStatus.Completed);
        var failedBackups = backupLogs.Count(bl => bl.Status == BackupStatus.Failed);
        var cancelledBackups = backupLogs.Count(bl => bl.Status == BackupStatus.Cancelled);

        var totalBytesTransferred = backupLogs.Where(bl => bl.FileSize.HasValue).Sum(bl => bl.FileSize!.Value);
        var backupsWithSize = backupLogs.Count(bl => bl.FileSize.HasValue);
        var completedLogs = backupLogs.Where(bl => bl.EndTime.HasValue).ToList();
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
    /// 生成配置特定的统计信息 / Generates configuration-specific statistics
    /// </summary>
    /// <param name="backupLogs">备份日志列表 / List of backup logs</param>
    /// <returns>配置统计信息列表 / List of configuration statistics</returns>
    private async Task<List<ConfigurationStatistics>> GenerateConfigurationStatisticsAsync(List<BackupLog> backupLogs)
    {
        var configurationStats = new List<ConfigurationStatistics>();
        var configurations = await _backupConfigurationRepository.GetAllAsync();
        var configDict = configurations.ToDictionary(c => c.Id, c => c.Name);

        var groupedLogs = backupLogs.GroupBy(bl => bl.BackupConfigId);

        foreach (var group in groupedLogs)
        {
            var configId = group.Key;
            var logs = group.ToList();

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

            var lastBackup = logs.OrderByDescending(bl => bl.StartTime).FirstOrDefault();

            configurationStats.Add(new ConfigurationStatistics
            {
                ConfigurationId = configId,
                ConfigurationName = configDict.GetValueOrDefault(configId, $"Configuration {configId}"),
                TotalBackups = totalBackups,
                SuccessfulBackups = successfulBackups,
                FailedBackups = failedBackups,
                CancelledBackups = cancelledBackups,
                SuccessRate = successRate,
                TotalBytesTransferred = totalBytesTransferred,
                TotalDuration = totalDuration,
                AverageDuration = averageDuration,
                AverageBackupSize = averageBackupSize,
                LastBackupTime = lastBackup?.StartTime,
                LastBackupStatus = lastBackup?.Status
            });
        }

        return configurationStats.OrderBy(cs => cs.ConfigurationName).ToList();
    }

    /// <summary>
    /// 生成每日分解统计 / Generates daily breakdown statistics
    /// </summary>
    /// <param name="backupLogs">备份日志列表 / List of backup logs</param>
    /// <param name="startDate">开始日期 / Start date</param>
    /// <param name="endDate">结束日期 / End date</param>
    /// <returns>每日统计信息列表 / List of daily statistics</returns>
    private List<DailyStatistics> GenerateDailyBreakdown(List<BackupLog> backupLogs, DateTime startDate, DateTime endDate)
    {
        var dailyStats = new List<DailyStatistics>();
        var currentDate = startDate.Date;

        while (currentDate <= endDate.Date)
        {
            var dayLogs = backupLogs.Where(bl => bl.StartTime.Date == currentDate).ToList();

            var totalBackups = dayLogs.Count;
            var successfulBackups = dayLogs.Count(bl => bl.Status == BackupStatus.Completed);
            var failedBackups = dayLogs.Count(bl => bl.Status == BackupStatus.Failed);
            var cancelledBackups = dayLogs.Count(bl => bl.Status == BackupStatus.Cancelled);
            var totalBytesTransferred = dayLogs.Where(bl => bl.FileSize.HasValue).Sum(bl => bl.FileSize!.Value);
            var completedLogs = dayLogs.Where(bl => bl.EndTime.HasValue).ToList();
            var totalDuration = TimeSpan.FromTicks(completedLogs.Sum(bl => bl.Duration?.Ticks ?? 0));

            dailyStats.Add(new DailyStatistics
            {
                Date = currentDate,
                TotalBackups = totalBackups,
                SuccessfulBackups = successfulBackups,
                FailedBackups = failedBackups,
                CancelledBackups = cancelledBackups,
                TotalBytesTransferred = totalBytesTransferred,
                TotalDuration = totalDuration
            });

            currentDate = currentDate.AddDays(1);
        }

        return dailyStats;
    }

    /// <summary>
    /// 生成存储统计信息 / Generates storage statistics
    /// </summary>
    /// <param name="backupLogs">备份日志列表 / List of backup logs</param>
    /// <returns>存储统计信息 / Storage statistics</returns>
    private async Task<StorageStatistics> GenerateStorageStatisticsAsync(List<BackupLog> backupLogs)
    {
        var logsWithSize = backupLogs.Where(bl => bl.FileSize.HasValue && bl.FileSize.Value > 0).ToList();
        
        if (!logsWithSize.Any())
        {
            return new StorageStatistics();
        }

        var totalStorageUsed = logsWithSize.Sum(bl => bl.FileSize!.Value);
        var largestBackupSize = logsWithSize.Max(bl => bl.FileSize!.Value);
        var smallestBackupSize = logsWithSize.Min(bl => bl.FileSize!.Value);
        var averageBackupSize = (double)totalStorageUsed / logsWithSize.Count;

        // 按配置生成存储统计 / Generate storage by configuration
        var configurations = await _backupConfigurationRepository.GetAllAsync();
        var configDict = configurations.ToDictionary(c => c.Id, c => c.Name);

        var storageByConfig = logsWithSize
            .GroupBy(bl => bl.BackupConfigId)
            .Select(group => new StorageByConfiguration
            {
                ConfigurationId = group.Key,
                ConfigurationName = configDict.GetValueOrDefault(group.Key, $"Configuration {group.Key}"),
                StorageUsed = group.Sum(bl => bl.FileSize!.Value),
                BackupCount = group.Count(),
                AverageBackupSize = group.Average(bl => bl.FileSize!.Value)
            })
            .OrderByDescending(sbc => sbc.StorageUsed)
            .ToList();

        return new StorageStatistics
        {
            TotalStorageUsed = totalStorageUsed,
            LargestBackupSize = largestBackupSize,
            SmallestBackupSize = smallestBackupSize,
            AverageBackupSize = averageBackupSize,
            TotalBackupFiles = logsWithSize.Count,
            StorageByConfiguration = storageByConfig
        };
    }

    /// <summary>
    /// 生成性能指标 / Generates performance metrics
    /// </summary>
    /// <param name="backupLogs">备份日志列表 / List of backup logs</param>
    /// <returns>性能指标 / Performance metrics</returns>
    private PerformanceMetrics GeneratePerformanceMetrics(List<BackupLog> backupLogs)
    {
        var completedLogs = backupLogs
            .Where(bl => bl.Status == BackupStatus.Completed && 
                        bl.EndTime.HasValue && 
                        bl.FileSize.HasValue && 
                        bl.FileSize.Value > 0)
            .ToList();

        if (!completedLogs.Any())
        {
            return new PerformanceMetrics();
        }

        // 计算传输速率（字节每秒）/ Calculate transfer rates (bytes per second)
        var transferRates = completedLogs
            .Where(bl => bl.Duration.HasValue && bl.Duration.Value.TotalSeconds > 0)
            .Select(bl => bl.FileSize!.Value / bl.Duration!.Value.TotalSeconds)
            .ToList();

        var durations = completedLogs
            .Where(bl => bl.Duration.HasValue)
            .Select(bl => bl.Duration!.Value)
            .ToList();

        var averageTransferRate = transferRates.Any() ? transferRates.Average() : 0;
        var fastestTransferRate = transferRates.Any() ? transferRates.Max() : 0;
        var slowestTransferRate = transferRates.Any() ? transferRates.Min() : 0;

        var fastestBackupTime = durations.Any() ? durations.Min() : TimeSpan.Zero;
        var slowestBackupTime = durations.Any() ? durations.Max() : TimeSpan.Zero;
        var averageBackupTime = durations.Any() 
            ? TimeSpan.FromTicks((long)durations.Average(d => d.Ticks)) 
            : TimeSpan.Zero;

        // 压缩比需要原始文件大小，我们没有跟踪这个信息 / Compression ratio would require original file sizes, which we don't track
        // 现在假设一个典型的压缩比 / For now, assume a typical compression ratio
        var compressionRatio = 0.7; // 原始大小的70%（30%压缩）/ 70% of original size (30% compression)

        return new PerformanceMetrics
        {
            AverageTransferRate = averageTransferRate,
            FastestTransferRate = fastestTransferRate,
            SlowestTransferRate = slowestTransferRate,
            FastestBackupTime = fastestBackupTime,
            SlowestBackupTime = slowestBackupTime,
            AverageBackupTime = averageBackupTime,
            CompressionRatio = compressionRatio
        };
    }

    /// <summary>
    /// 将报告导出为各种格式 / Exports a report to various formats
    /// </summary>
    /// <param name="report">备份摘要报告 / Backup summary report</param>
    /// <param name="format">导出格式 / Export format</param>
    /// <returns>导出的报告字符串 / Exported report string</returns>
    /// <exception cref="ArgumentException">当格式不支持时抛出 / Thrown when format is not supported</exception>
    public async Task<string> ExportReportAsync(BackupSummaryReport report, string format = "json")
    {
        _logger.LogInformation("Exporting backup report to {Format} format", format);

        try
        {
            return format.ToLower() switch
            {
                "json" => System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                }),
                "csv" => await ExportToCsvAsync(report),
                "html" => await ExportToHtmlAsync(report),
                _ => throw new ArgumentException($"Unsupported export format: {format}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting report to {Format}", format);
            throw;
        }
    }

    /// <summary>
    /// 导出为CSV格式 / Exports to CSV format
    /// </summary>
    /// <param name="report">备份摘要报告 / Backup summary report</param>
    /// <returns>CSV格式的报告 / Report in CSV format</returns>
    private async Task<string> ExportToCsvAsync(BackupSummaryReport report)
    {
        var csv = new System.Text.StringBuilder();
        
        // 标题行 / Header
        csv.AppendLine("Report Generated,Start Date,End Date,Total Backups,Successful,Failed,Cancelled,Success Rate,Total Bytes,Average Duration");
        
        // 总体统计信息 / Overall statistics
        csv.AppendLine($"{report.GeneratedAt:yyyy-MM-dd HH:mm:ss}," +
                      $"{report.ReportStartDate:yyyy-MM-dd}," +
                      $"{report.ReportEndDate:yyyy-MM-dd}," +
                      $"{report.OverallStatistics.TotalBackups}," +
                      $"{report.OverallStatistics.SuccessfulBackups}," +
                      $"{report.OverallStatistics.FailedBackups}," +
                      $"{report.OverallStatistics.CancelledBackups}," +
                      $"{report.OverallStatistics.SuccessRate:F2}%," +
                      $"{report.OverallStatistics.TotalBytesTransferred}," +
                      $"{report.OverallStatistics.AverageDuration}");

        return csv.ToString();
    }

    /// <summary>
    /// 导出为HTML格式 / Exports to HTML format
    /// </summary>
    /// <param name="report">备份摘要报告 / Backup summary report</param>
    /// <returns>HTML格式的报告 / Report in HTML format</returns>
    private async Task<string> ExportToHtmlAsync(BackupSummaryReport report)
    {
        var html = new System.Text.StringBuilder();
        
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html><head><title>Backup Summary Report</title>");
        html.AppendLine("<style>");
        html.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
        html.AppendLine("table { border-collapse: collapse; width: 100%; margin: 20px 0; }");
        html.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
        html.AppendLine("th { background-color: #f2f2f2; }");
        html.AppendLine(".success { color: green; }");
        html.AppendLine(".failure { color: red; }");
        html.AppendLine("</style></head><body>");
        
        html.AppendLine($"<h1>Backup Summary Report</h1>");
        html.AppendLine($"<p><strong>Generated:</strong> {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}</p>");
        html.AppendLine($"<p><strong>Period:</strong> {report.ReportStartDate:yyyy-MM-dd} to {report.ReportEndDate:yyyy-MM-dd}</p>");
        
        // 总体统计信息 / Overall statistics
        html.AppendLine("<h2>Overall Statistics</h2>");
        html.AppendLine("<table>");
        html.AppendLine("<tr><th>Metric</th><th>Value</th></tr>");
        html.AppendLine($"<tr><td>Total Backups</td><td>{report.OverallStatistics.TotalBackups}</td></tr>");
        html.AppendLine($"<tr><td>Successful</td><td class='success'>{report.OverallStatistics.SuccessfulBackups}</td></tr>");
        html.AppendLine($"<tr><td>Failed</td><td class='failure'>{report.OverallStatistics.FailedBackups}</td></tr>");
        html.AppendLine($"<tr><td>Cancelled</td><td>{report.OverallStatistics.CancelledBackups}</td></tr>");
        html.AppendLine($"<tr><td>Success Rate</td><td>{report.OverallStatistics.SuccessRate:F2}%</td></tr>");
        html.AppendLine("</table>");
        
        html.AppendLine("</body></html>");
        
        return html.ToString();
    }
}