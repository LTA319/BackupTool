using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Service for generating comprehensive backup reports
/// </summary>
public class BackupReportingService
{
    private readonly IBackupLogRepository _backupLogRepository;
    private readonly IBackupConfigurationRepository _backupConfigurationRepository;
    private readonly ILogger<BackupReportingService> _logger;

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
    /// Generates a comprehensive backup summary report
    /// </summary>
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
            // Get all backup logs for the period
            var backupLogs = await _backupLogRepository.GetByDateRangeAsync(criteria.StartDate, criteria.EndDate);
            
            // Filter by configuration if specified
            if (criteria.ConfigurationId.HasValue)
            {
                backupLogs = backupLogs.Where(bl => bl.BackupConfigId == criteria.ConfigurationId.Value);
            }

            var backupLogsList = backupLogs.ToList();

            // Generate overall statistics
            report.OverallStatistics = await GenerateOverallStatisticsAsync(backupLogsList);

            // Generate configuration-specific statistics
            if (criteria.IncludeConfigurationBreakdown)
            {
                report.ConfigurationStatistics = await GenerateConfigurationStatisticsAsync(backupLogsList);
            }

            // Generate daily breakdown
            if (criteria.IncludeDailyBreakdown)
            {
                report.DailyBreakdown = GenerateDailyBreakdown(backupLogsList, criteria.StartDate, criteria.EndDate);
            }

            // Include recent failures
            if (criteria.IncludeRecentFailures)
            {
                report.RecentFailures = backupLogsList
                    .Where(bl => bl.Status == BackupStatus.Failed)
                    .OrderByDescending(bl => bl.StartTime)
                    .Take(criteria.MaxRecentFailures)
                    .ToList();
            }

            // Generate storage statistics
            if (criteria.IncludeStorageStatistics)
            {
                report.StorageStatistics = await GenerateStorageStatisticsAsync(backupLogsList);
            }

            // Generate performance metrics
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

        // Generate storage by configuration
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

        // Calculate transfer rates (bytes per second)
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

        // Compression ratio would require original file sizes, which we don't track
        // For now, assume a typical compression ratio
        var compressionRatio = 0.7; // 70% of original size (30% compression)

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
    /// Exports a report to various formats
    /// </summary>
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

    private async Task<string> ExportToCsvAsync(BackupSummaryReport report)
    {
        var csv = new System.Text.StringBuilder();
        
        // Header
        csv.AppendLine("Report Generated,Start Date,End Date,Total Backups,Successful,Failed,Cancelled,Success Rate,Total Bytes,Average Duration");
        
        // Overall statistics
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
        
        // Overall statistics
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