using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;

namespace MySqlBackupTool.Tests.Properties;

/// <summary>
/// Property-based tests for log retention and reporting functionality
/// **Validates: Requirements 7.5, 7.6**
/// </summary>
public class LogRetentionReportingPropertyTests
{
    private readonly Mock<IRetentionPolicyRepository> _mockRetentionRepository;
    private readonly Mock<IBackupLogRepository> _mockBackupLogRepository;
    private readonly Mock<IBackupConfigurationRepository> _mockConfigRepository;
    private readonly Mock<ILogger<RetentionManagementService>> _mockRetentionLogger;
    private readonly Mock<ILogger<BackupReportingService>> _mockReportingLogger;

    public LogRetentionReportingPropertyTests()
    {
        _mockRetentionRepository = new Mock<IRetentionPolicyRepository>();
        _mockBackupLogRepository = new Mock<IBackupLogRepository>();
        _mockConfigRepository = new Mock<IBackupConfigurationRepository>();
        _mockRetentionLogger = new Mock<ILogger<RetentionManagementService>>();
        _mockReportingLogger = new Mock<ILogger<BackupReportingService>>();
    }

    /// <summary>
    /// **Property 17: Log Retention Policy Enforcement**
    /// For any valid retention policy and set of backup logs, applying the policy should 
    /// retain logs that meet the criteria and remove logs that don't, with the total 
    /// number of retained logs never exceeding the policy limits.
    /// **Validates: Requirements 7.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LogRetentionPolicyEnforcement()
    {
        var policyGen = Arb.Generate<RetentionPolicy>().Where(p => 
            !string.IsNullOrEmpty(p.Name) && 
            (p.MaxAgeDays.HasValue || p.MaxCount.HasValue || p.MaxStorageBytes.HasValue) &&
            p.MaxAgeDays is null or > 0 &&
            p.MaxCount is null or > 0 &&
            p.MaxStorageBytes is null or > 0)
            .Select(p => 
            {
                p.IsEnabled = true;
                p.CreatedAt = DateTime.UtcNow;
                return p;
            });

        var backupLogsGen = Arb.Generate<List<BackupLog>>().Where(logs => logs.Count <= 100)
            .Select(logs => logs.Select(log =>
            {
                log.StartTime = DateTime.UtcNow.AddDays(-System.Random.Shared.Next(0, 365));
                log.FileSize = System.Random.Shared.Next(1000, 1000000);
                log.FilePath = $"/path/backup{log.Id}.zip";
                return log;
            }).ToList());

        return Prop.ForAll(policyGen.ToArbitrary(), backupLogsGen.ToArbitrary(), (policy, backupLogs) =>
        {
            // Arrange
            var logsWithFiles = backupLogs.Where(bl => !string.IsNullOrEmpty(bl.FilePath) && bl.FileSize.HasValue).ToList();

            // Act - Apply retention logic manually (simulating the service behavior)
            var retainedLogs = new List<BackupLog>();
            var currentBackupCount = logsWithFiles.Count;
            var currentStorageUsed = logsWithFiles.Sum(bl => bl.FileSize ?? 0);

            foreach (var backupLog in logsWithFiles.OrderByDescending(bl => bl.StartTime))
            {
                var shouldRetain = policy.ShouldRetainBackup(
                    backupLog.StartTime,
                    currentBackupCount,
                    currentStorageUsed,
                    backupLog.FileSize ?? 0);

                if (shouldRetain)
                {
                    retainedLogs.Add(backupLog);
                }
                else
                {
                    currentBackupCount--;
                    currentStorageUsed -= backupLog.FileSize ?? 0;
                }
            }

            // Assert - Verify retention policy constraints are respected
            var ageConstraintSatisfied = !policy.MaxAgeDays.HasValue || 
                retainedLogs.All(log => (DateTime.UtcNow - log.StartTime).TotalDays <= policy.MaxAgeDays.Value);

            var countConstraintSatisfied = !policy.MaxCount.HasValue || 
                retainedLogs.Count <= policy.MaxCount.Value;

            var storageConstraintSatisfied = !policy.MaxStorageBytes.HasValue || 
                retainedLogs.Sum(log => log.FileSize ?? 0) <= policy.MaxStorageBytes.Value;

            return ageConstraintSatisfied && countConstraintSatisfied && storageConstraintSatisfied;
        });
    }

    /// <summary>
    /// **Property 18: Backup Report Generation**
    /// For any set of backup logs and report criteria, generating a report should produce 
    /// accurate statistics that correctly reflect the input data, with all totals and 
    /// percentages calculated correctly.
    /// **Validates: Requirements 7.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BackupReportGeneration()
    {
        var backupLogsGen = Arb.Generate<List<BackupLog>>().Where(logs => logs.Count <= 50)
            .Select(logs => logs.Select(log =>
            {
                log.StartTime = DateTime.UtcNow.AddDays(-System.Random.Shared.Next(0, 30));
                log.FileSize = System.Random.Shared.Next(1000, 100000000);
                log.BackupConfigId = System.Random.Shared.Next(1, 6);
                return log;
            }).ToList());

        var configurationsGen = Arb.Generate<List<BackupConfiguration>>().Where(configs => configs.Count <= 5)
            .Select(configs => configs.Select((config, index) =>
            {
                config.Id = index + 1;
                config.Name = $"Config{index + 1}";
                return config;
            }).ToList());

        return Prop.ForAll(backupLogsGen.ToArbitrary(), configurationsGen.ToArbitrary(), (backupLogs, configurations) =>
        {
            // Arrange
            _mockBackupLogRepository.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(backupLogs);
            _mockConfigRepository.Setup(r => r.GetAllAsync())
                .ReturnsAsync(configurations);

            var service = new BackupReportingService(
                _mockBackupLogRepository.Object,
                _mockConfigRepository.Object,
                _mockReportingLogger.Object);

            var criteria = new ReportCriteria
            {
                StartDate = DateTime.UtcNow.AddDays(-30),
                EndDate = DateTime.UtcNow,
                IncludeConfigurationBreakdown = true,
                IncludeDailyBreakdown = false, // Skip daily breakdown for performance
                IncludeRecentFailures = false,
                IncludeStorageStatistics = true,
                IncludePerformanceMetrics = false
            };

            // Act
            var report = service.GenerateReportAsync(criteria).Result;

            // Assert - Verify report statistics are accurate
            var expectedTotalBackups = backupLogs.Count;
            var expectedSuccessfulBackups = backupLogs.Count(bl => bl.Status == BackupStatus.Completed);
            var expectedFailedBackups = backupLogs.Count(bl => bl.Status == BackupStatus.Failed);
            var expectedCancelledBackups = backupLogs.Count(bl => bl.Status == BackupStatus.Cancelled);

            var totalBackupsCorrect = report.OverallStatistics.TotalBackups == expectedTotalBackups;
            var successfulBackupsCorrect = report.OverallStatistics.SuccessfulBackups == expectedSuccessfulBackups;
            var failedBackupsCorrect = report.OverallStatistics.FailedBackups == expectedFailedBackups;
            var cancelledBackupsCorrect = report.OverallStatistics.CancelledBackups == expectedCancelledBackups;

            // Verify success rate calculation
            var expectedSuccessRate = expectedTotalBackups > 0 
                ? (double)expectedSuccessfulBackups / expectedTotalBackups * 100 
                : 0;
            var successRateCorrect = Math.Abs(report.OverallStatistics.SuccessRate - expectedSuccessRate) < 0.01;

            // Verify storage statistics
            var logsWithSize = backupLogs.Where(bl => bl.FileSize.HasValue && bl.FileSize.Value > 0).ToList();
            var expectedTotalStorage = logsWithSize.Sum(bl => bl.FileSize!.Value);
            var storageCorrect = report.StorageStatistics.TotalStorageUsed == expectedTotalStorage;

            // Verify configuration breakdown
            var configBreakdownCorrect = true;
            if (report.ConfigurationStatistics.Any())
            {
                var totalFromConfigs = report.ConfigurationStatistics.Sum(cs => cs.TotalBackups);
                configBreakdownCorrect = totalFromConfigs == expectedTotalBackups;
            }

            return totalBackupsCorrect && 
                   successfulBackupsCorrect && 
                   failedBackupsCorrect && 
                   cancelledBackupsCorrect && 
                   successRateCorrect && 
                   storageCorrect && 
                   configBreakdownCorrect;
        });
    }

    /// <summary>
    /// **Property: Retention Policy Validation**
    /// For any retention policy input, the validation should accept valid policies and 
    /// reject invalid policies consistently, ensuring data integrity.
    /// **Validates: Requirements 7.5**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property RetentionPolicyValidation()
    {
        var validPolicyGen = Arb.Generate<RetentionPolicy>()
            .Where(p => !string.IsNullOrEmpty(p.Name) && 
                       (p.MaxAgeDays.HasValue || p.MaxCount.HasValue || p.MaxStorageBytes.HasValue) &&
                       p.MaxAgeDays is null or > 0 &&
                       p.MaxCount is null or > 0 &&
                       p.MaxStorageBytes is null or > 0);

        var invalidPolicyGen = Arb.Generate<RetentionPolicy>()
            .Select(p => 
            {
                // Make it invalid by removing name or criteria
                if (System.Random.Shared.Next(0, 2) == 0)
                {
                    p.Name = ""; // Invalid name
                    p.MaxAgeDays = 30;
                }
                else
                {
                    p.Name = "Valid Name";
                    p.MaxAgeDays = null;
                    p.MaxCount = null;
                    p.MaxStorageBytes = null; // No criteria
                }
                return p;
            });

        return Prop.ForAll(validPolicyGen.ToArbitrary(), invalidPolicyGen.ToArbitrary(), (validPolicy, invalidPolicy) =>
        {
            // Arrange
            _mockRetentionRepository.Setup(r => r.IsNameUniqueAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(true);
            _mockRetentionRepository.Setup(r => r.AddAsync(It.IsAny<RetentionPolicy>()))
                .ReturnsAsync((RetentionPolicy p) => { p.Id = 1; return p; });
            _mockRetentionRepository.Setup(r => r.SaveChangesAsync())
                .ReturnsAsync(1);

            var service = new RetentionManagementService(
                _mockRetentionRepository.Object,
                _mockBackupLogRepository.Object,
                _mockRetentionLogger.Object);

            // Act & Assert
            var validPolicyResult = true;
            var invalidPolicyResult = false;

            try
            {
                var result = service.CreateRetentionPolicyAsync(validPolicy).Result;
                validPolicyResult = result != null && result.Name == validPolicy.Name;
            }
            catch
            {
                validPolicyResult = false;
            }

            try
            {
                var result = service.CreateRetentionPolicyAsync(invalidPolicy).Result;
                invalidPolicyResult = false; // Should not succeed
            }
            catch
            {
                invalidPolicyResult = true; // Should throw exception
            }

            return validPolicyResult && invalidPolicyResult;
        });
    }

    /// <summary>
    /// **Property: Report Export Consistency**
    /// For any backup report, exporting to different formats should preserve the core 
    /// statistical information, ensuring data consistency across formats.
    /// **Validates: Requirements 7.6**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property ReportExportConsistency()
    {
        var reportGen = Arb.Generate<BackupSummaryReport>()
            .Select(report =>
            {
                report.GeneratedAt = DateTime.UtcNow;
                report.ReportStartDate = DateTime.UtcNow.AddDays(-7);
                report.ReportEndDate = DateTime.UtcNow;
                report.OverallStatistics = new BackupStatistics
                {
                    TotalBackups = System.Random.Shared.Next(0, 1000),
                    SuccessfulBackups = System.Random.Shared.Next(0, 500),
                    FailedBackups = System.Random.Shared.Next(0, 500),
                    CancelledBackups = System.Random.Shared.Next(0, 100),
                    TotalBytesTransferred = System.Random.Shared.Next(0, 1000000000),
                    SuccessRate = System.Random.Shared.NextDouble() * 100
                };
                return report;
            });

        return Prop.ForAll(reportGen.ToArbitrary(), report =>
        {
            // Arrange
            var service = new BackupReportingService(
                _mockBackupLogRepository.Object,
                _mockConfigRepository.Object,
                _mockReportingLogger.Object);

            // Act - Export to different formats
            var jsonExport = service.ExportReportAsync(report, "json").Result;
            var csvExport = service.ExportReportAsync(report, "csv").Result;
            var htmlExport = service.ExportReportAsync(report, "html").Result;

            // Assert - Verify core data is present in all formats
            var totalBackupsStr = report.OverallStatistics.TotalBackups.ToString();
            var successfulBackupsStr = report.OverallStatistics.SuccessfulBackups.ToString();
            var failedBackupsStr = report.OverallStatistics.FailedBackups.ToString();

            var jsonContainsData = jsonExport.Contains(totalBackupsStr) && 
                                  jsonExport.Contains(successfulBackupsStr) && 
                                  jsonExport.Contains(failedBackupsStr);

            var csvContainsData = csvExport.Contains(totalBackupsStr) && 
                                 csvExport.Contains(successfulBackupsStr) && 
                                 csvExport.Contains(failedBackupsStr);

            var htmlContainsData = htmlExport.Contains(totalBackupsStr) && 
                                  htmlExport.Contains(successfulBackupsStr) && 
                                  htmlExport.Contains(failedBackupsStr);

            // Verify format-specific characteristics
            var jsonIsValidFormat = jsonExport.Contains("{") && jsonExport.Contains("}");
            var csvIsValidFormat = csvExport.Contains(",") && csvExport.Contains("Report Generated");
            var htmlIsValidFormat = htmlExport.Contains("<html>") && htmlExport.Contains("</html>");

            return jsonContainsData && csvContainsData && htmlContainsData &&
                   jsonIsValidFormat && csvIsValidFormat && htmlIsValidFormat;
        });
    }
}