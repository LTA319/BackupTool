using Microsoft.Extensions.Logging;
using Moq;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;

namespace MySqlBackupTool.Tests.Services;

public class BackupReportingServiceTests
{
    private readonly Mock<IBackupLogRepository> _mockBackupLogRepository;
    private readonly Mock<IBackupConfigurationRepository> _mockConfigRepository;
    private readonly Mock<ILogger<BackupReportingService>> _mockLogger;
    private readonly BackupReportingService _service;

    public BackupReportingServiceTests()
    {
        _mockBackupLogRepository = new Mock<IBackupLogRepository>();
        _mockConfigRepository = new Mock<IBackupConfigurationRepository>();
        _mockLogger = new Mock<ILogger<BackupReportingService>>();
        _service = new BackupReportingService(
            _mockBackupLogRepository.Object,
            _mockConfigRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GenerateReportAsync_WithBasicCriteria_ShouldGenerateReport()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var criteria = new ReportCriteria
        {
            StartDate = startDate,
            EndDate = endDate,
            IncludeDailyBreakdown = true,
            IncludeConfigurationBreakdown = true
        };

        var backupLogs = new List<BackupLog>
        {
            new() 
            { 
                Id = 1, 
                BackupConfigId = 1, 
                Status = BackupStatus.Completed,
                StartTime = startDate.AddHours(1),
                EndTime = startDate.AddHours(2),
                FileSize = 1000000
            },
            new() 
            { 
                Id = 2, 
                BackupConfigId = 1, 
                Status = BackupStatus.Failed,
                StartTime = startDate.AddHours(3),
                EndTime = startDate.AddHours(4),
                ErrorMessage = "Test error"
            }
        };

        var configurations = new List<BackupConfiguration>
        {
            new() { Id = 1, Name = "Test Config 1" }
        };

        _mockBackupLogRepository.Setup(r => r.GetByDateRangeAsync(startDate, endDate))
            .ReturnsAsync(backupLogs);
        _mockConfigRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(configurations);

        // Act
        var result = await _service.GenerateReportAsync(criteria);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(startDate, result.ReportStartDate);
        Assert.Equal(endDate, result.ReportEndDate);
        Assert.Equal(2, result.OverallStatistics.TotalBackups);
        Assert.Equal(1, result.OverallStatistics.SuccessfulBackups);
        Assert.Equal(1, result.OverallStatistics.FailedBackups);
        Assert.Equal(50.0, result.OverallStatistics.SuccessRate);
        Assert.Single(result.ConfigurationStatistics);
        Assert.NotEmpty(result.DailyBreakdown);
    }

    [Fact]
    public async Task GenerateReportAsync_WithConfigurationFilter_ShouldFilterByConfiguration()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var criteria = new ReportCriteria
        {
            StartDate = startDate,
            EndDate = endDate,
            ConfigurationId = 1
        };

        var backupLogs = new List<BackupLog>
        {
            new() { Id = 1, BackupConfigId = 1, Status = BackupStatus.Completed, StartTime = startDate.AddHours(1) },
            new() { Id = 2, BackupConfigId = 2, Status = BackupStatus.Completed, StartTime = startDate.AddHours(2) }
        };

        _mockBackupLogRepository.Setup(r => r.GetByDateRangeAsync(startDate, endDate))
            .ReturnsAsync(backupLogs);

        // Act
        var result = await _service.GenerateReportAsync(criteria);

        // Assert
        Assert.Equal(1, result.OverallStatistics.TotalBackups);
    }

    [Fact]
    public async Task GenerateReportAsync_WithRecentFailures_ShouldIncludeFailures()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var criteria = new ReportCriteria
        {
            StartDate = startDate,
            EndDate = endDate,
            IncludeRecentFailures = true,
            MaxRecentFailures = 5
        };

        var backupLogs = new List<BackupLog>
        {
            new() 
            { 
                Id = 1, 
                BackupConfigId = 1, 
                Status = BackupStatus.Failed,
                StartTime = startDate.AddHours(1),
                ErrorMessage = "Error 1"
            },
            new() 
            { 
                Id = 2, 
                BackupConfigId = 1, 
                Status = BackupStatus.Failed,
                StartTime = startDate.AddHours(2),
                ErrorMessage = "Error 2"
            },
            new() 
            { 
                Id = 3, 
                BackupConfigId = 1, 
                Status = BackupStatus.Completed,
                StartTime = startDate.AddHours(3)
            }
        };

        _mockBackupLogRepository.Setup(r => r.GetByDateRangeAsync(startDate, endDate))
            .ReturnsAsync(backupLogs);

        // Act
        var result = await _service.GenerateReportAsync(criteria);

        // Assert
        Assert.Equal(2, result.RecentFailures.Count);
        Assert.All(result.RecentFailures, failure => Assert.Equal(BackupStatus.Failed, failure.Status));
        // Should be ordered by StartTime descending (most recent first)
        Assert.True(result.RecentFailures[0].StartTime > result.RecentFailures[1].StartTime);
    }

    [Fact]
    public async Task GenerateReportAsync_WithStorageStatistics_ShouldCalculateStorage()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var criteria = new ReportCriteria
        {
            StartDate = startDate,
            EndDate = endDate,
            IncludeStorageStatistics = true,
            IncludeConfigurationBreakdown = true
        };

        var backupLogs = new List<BackupLog>
        {
            new() 
            { 
                Id = 1, 
                BackupConfigId = 1, 
                Status = BackupStatus.Completed,
                StartTime = startDate.AddHours(1),
                FileSize = 1000000
            },
            new() 
            { 
                Id = 2, 
                BackupConfigId = 1, 
                Status = BackupStatus.Completed,
                StartTime = startDate.AddHours(2),
                FileSize = 2000000
            },
            new() 
            { 
                Id = 3, 
                BackupConfigId = 2, 
                Status = BackupStatus.Completed,
                StartTime = startDate.AddHours(3),
                FileSize = 500000
            }
        };

        var configurations = new List<BackupConfiguration>
        {
            new() { Id = 1, Name = "Config 1" },
            new() { Id = 2, Name = "Config 2" }
        };

        _mockBackupLogRepository.Setup(r => r.GetByDateRangeAsync(startDate, endDate))
            .ReturnsAsync(backupLogs);
        _mockConfigRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(configurations);

        // Act
        var result = await _service.GenerateReportAsync(criteria);

        // Assert
        Assert.Equal(3500000, result.StorageStatistics.TotalStorageUsed);
        Assert.Equal(2000000, result.StorageStatistics.LargestBackupSize);
        Assert.Equal(500000, result.StorageStatistics.SmallestBackupSize);
        Assert.Equal(3, result.StorageStatistics.TotalBackupFiles);
        Assert.Equal(2, result.StorageStatistics.StorageByConfiguration.Count);
        
        var config1Storage = result.StorageStatistics.StorageByConfiguration.First(s => s.ConfigurationId == 1);
        Assert.Equal(3000000, config1Storage.StorageUsed);
        Assert.Equal(2, config1Storage.BackupCount);
    }

    [Fact]
    public async Task GenerateReportAsync_WithPerformanceMetrics_ShouldCalculateMetrics()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var criteria = new ReportCriteria
        {
            StartDate = startDate,
            EndDate = endDate,
            IncludePerformanceMetrics = true
        };

        var backupLogs = new List<BackupLog>
        {
            new() 
            { 
                Id = 1, 
                BackupConfigId = 1, 
                Status = BackupStatus.Completed,
                StartTime = startDate.AddHours(1),
                EndTime = startDate.AddHours(1).AddMinutes(30), // 30 minutes
                FileSize = 1800000 // 1.8MB in 30 minutes = 1KB/s
            },
            new() 
            { 
                Id = 2, 
                BackupConfigId = 1, 
                Status = BackupStatus.Completed,
                StartTime = startDate.AddHours(2),
                EndTime = startDate.AddHours(2).AddMinutes(10), // 10 minutes
                FileSize = 600000 // 600KB in 10 minutes = 1KB/s
            }
        };

        _mockBackupLogRepository.Setup(r => r.GetByDateRangeAsync(startDate, endDate))
            .ReturnsAsync(backupLogs);

        // Act
        var result = await _service.GenerateReportAsync(criteria);

        // Assert
        Assert.NotNull(result.PerformanceMetrics);
        Assert.Equal(TimeSpan.FromMinutes(10), result.PerformanceMetrics.FastestBackupTime);
        Assert.Equal(TimeSpan.FromMinutes(30), result.PerformanceMetrics.SlowestBackupTime);
        Assert.Equal(TimeSpan.FromMinutes(20), result.PerformanceMetrics.AverageBackupTime);
    }

    [Fact]
    public async Task ExportReportAsync_ToJson_ShouldReturnJsonString()
    {
        // Arrange
        var report = new BackupSummaryReport
        {
            GeneratedAt = DateTime.UtcNow,
            ReportStartDate = DateTime.UtcNow.AddDays(-7),
            ReportEndDate = DateTime.UtcNow,
            OverallStatistics = new BackupStatistics
            {
                TotalBackups = 10,
                SuccessfulBackups = 8,
                FailedBackups = 2
            }
        };

        // Act
        var result = await _service.ExportReportAsync(report, "json");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("\"totalBackups\": 10", result);
        Assert.Contains("\"successfulBackups\": 8", result);
        Assert.Contains("\"failedBackups\": 2", result);
    }

    [Fact]
    public async Task ExportReportAsync_ToCsv_ShouldReturnCsvString()
    {
        // Arrange
        var report = new BackupSummaryReport
        {
            GeneratedAt = DateTime.UtcNow,
            ReportStartDate = DateTime.UtcNow.AddDays(-7),
            ReportEndDate = DateTime.UtcNow,
            OverallStatistics = new BackupStatistics
            {
                TotalBackups = 10,
                SuccessfulBackups = 8,
                FailedBackups = 2,
                SuccessRate = 80.0
            }
        };

        // Act
        var result = await _service.ExportReportAsync(report, "csv");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Report Generated,Start Date,End Date", result);
        Assert.Contains("10,8,2", result);
        Assert.Contains("80.00%", result);
    }

    [Fact]
    public async Task ExportReportAsync_ToHtml_ShouldReturnHtmlString()
    {
        // Arrange
        var report = new BackupSummaryReport
        {
            GeneratedAt = DateTime.UtcNow,
            ReportStartDate = DateTime.UtcNow.AddDays(-7),
            ReportEndDate = DateTime.UtcNow,
            OverallStatistics = new BackupStatistics
            {
                TotalBackups = 10,
                SuccessfulBackups = 8,
                FailedBackups = 2
            }
        };

        // Act
        var result = await _service.ExportReportAsync(report, "html");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("<!DOCTYPE html>", result);
        Assert.Contains("<title>Backup Summary Report</title>", result);
        Assert.Contains("<td>10</td>", result);
        Assert.Contains("<td class='success'>8</td>", result);
        Assert.Contains("<td class='failure'>2</td>", result);
    }

    [Fact]
    public async Task ExportReportAsync_UnsupportedFormat_ShouldThrowException()
    {
        // Arrange
        var report = new BackupSummaryReport();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.ExportReportAsync(report, "xml"));
    }
}