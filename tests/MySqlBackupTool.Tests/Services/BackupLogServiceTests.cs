using Microsoft.Extensions.Logging;
using Moq;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;

namespace MySqlBackupTool.Tests.Services;

public class BackupLogServiceTests
{
    private readonly Mock<IBackupLogRepository> _mockRepository;
    private readonly Mock<ILogger<BackupLogService>> _mockLogger;
    private readonly BackupLogService _service;

    public BackupLogServiceTests()
    {
        _mockRepository = new Mock<IBackupLogRepository>();
        _mockLogger = new Mock<ILogger<BackupLogService>>();
        _service = new BackupLogService(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task StartBackupAsync_ValidConfiguration_ShouldCreateBackupLog()
    {
        // Arrange
        var configId = 1;
        var resumeToken = "test-token";
        var expectedLog = new BackupLog
        {
            Id = 1,
            BackupConfigId = configId,
            Status = BackupStatus.Queued,
            ResumeToken = resumeToken,
            StartTime = DateTime.UtcNow
        };

        _mockRepository.Setup(r => r.AddAsync(It.IsAny<BackupLog>()))
            .ReturnsAsync(expectedLog);
        _mockRepository.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _service.StartBackupAsync(configId, resumeToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(configId, result.BackupConfigId);
        Assert.Equal(BackupStatus.Queued, result.Status);
        Assert.Equal(resumeToken, result.ResumeToken);
        
        _mockRepository.Verify(r => r.AddAsync(It.Is<BackupLog>(log => 
            log.BackupConfigId == configId && 
            log.Status == BackupStatus.Queued &&
            log.ResumeToken == resumeToken)), Times.Once);
        _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateBackupStatusAsync_ValidBackupLog_ShouldUpdateStatus()
    {
        // Arrange
        var backupLogId = 1;
        var newStatus = BackupStatus.Compressing;
        var operation = "Compressing data directory";

        _mockRepository.Setup(r => r.UpdateStatusAsync(backupLogId, newStatus, operation))
            .ReturnsAsync(true);

        // Act
        await _service.UpdateBackupStatusAsync(backupLogId, newStatus, operation);

        // Assert
        _mockRepository.Verify(r => r.UpdateStatusAsync(backupLogId, newStatus, operation), Times.Once);
    }

    [Fact]
    public async Task UpdateBackupStatusAsync_NonExistentBackupLog_ShouldThrowException()
    {
        // Arrange
        var backupLogId = 999;
        var newStatus = BackupStatus.Compressing;

        _mockRepository.Setup(r => r.UpdateStatusAsync(backupLogId, newStatus, null))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _service.UpdateBackupStatusAsync(backupLogId, newStatus));
    }

    [Fact]
    public async Task CompleteBackupAsync_SuccessfulBackup_ShouldCompleteWithDetails()
    {
        // Arrange
        var backupLogId = 1;
        var finalStatus = BackupStatus.Completed;
        var filePath = "/path/to/backup.zip";
        var fileSize = 1000000L;

        _mockRepository.Setup(r => r.CompleteBackupAsync(backupLogId, finalStatus, filePath, fileSize, null))
            .ReturnsAsync(true);

        // Act
        await _service.CompleteBackupAsync(backupLogId, finalStatus, filePath, fileSize);

        // Assert
        _mockRepository.Verify(r => r.CompleteBackupAsync(backupLogId, finalStatus, filePath, fileSize, null), Times.Once);
    }

    [Fact]
    public async Task LogTransferChunkAsync_ValidChunk_ShouldAddTransferLog()
    {
        // Arrange
        var backupLogId = 1;
        var chunkIndex = 0;
        var chunkSize = 1024L;
        var status = "Completed";
        
        var backupLog = new BackupLog
        {
            Id = backupLogId,
            BackupConfigId = 1,
            TransferLogs = new List<TransferLog>()
        };

        _mockRepository.Setup(r => r.GetByIdAsync(backupLogId))
            .ReturnsAsync(backupLog);
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<BackupLog>()))
            .ReturnsAsync(backupLog);
        _mockRepository.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        await _service.LogTransferChunkAsync(backupLogId, chunkIndex, chunkSize, status);

        // Assert
        Assert.Single(backupLog.TransferLogs);
        var transferLog = backupLog.TransferLogs.First();
        Assert.Equal(backupLogId, transferLog.BackupLogId);
        Assert.Equal(chunkIndex, transferLog.ChunkIndex);
        Assert.Equal(chunkSize, transferLog.ChunkSize);
        Assert.Equal(status, transferLog.Status);
        
        _mockRepository.Verify(r => r.UpdateAsync(backupLog), Times.Once);
        _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetBackupLogsAsync_NoFilter_ShouldReturnAllLogs()
    {
        // Arrange
        var logs = new List<BackupLog>
        {
            new() { Id = 1, BackupConfigId = 1, StartTime = DateTime.UtcNow.AddHours(-2) },
            new() { Id = 2, BackupConfigId = 1, StartTime = DateTime.UtcNow.AddHours(-1) }
        };

        _mockRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(logs);

        // Act
        var result = await _service.GetBackupLogsAsync();

        // Assert
        Assert.Equal(2, result.Count());
        // Should be ordered by StartTime descending
        var orderedResult = result.ToList();
        Assert.True(orderedResult[0].StartTime > orderedResult[1].StartTime);
    }

    [Fact]
    public async Task GetBackupLogsAsync_WithConfigurationFilter_ShouldReturnFilteredLogs()
    {
        // Arrange
        var configId = 1;
        var filter = new BackupLogFilter { ConfigurationId = configId };
        var logs = new List<BackupLog>
        {
            new() { Id = 1, BackupConfigId = configId },
            new() { Id = 2, BackupConfigId = configId }
        };

        _mockRepository.Setup(r => r.GetByConfigurationIdAsync(configId))
            .ReturnsAsync(logs);

        // Act
        var result = await _service.GetBackupLogsAsync(filter);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, log => Assert.Equal(configId, log.BackupConfigId));
    }

    [Fact]
    public async Task SearchBackupLogsAsync_WithCriteria_ShouldReturnPagedResults()
    {
        // Arrange
        var criteria = new BackupLogSearchCriteria
        {
            ConfigurationId = 1,
            Status = BackupStatus.Completed,
            PageNumber = 1,
            PageSize = 10
        };

        var allLogs = new List<BackupLog>
        {
            new() { Id = 1, BackupConfigId = 1, Status = BackupStatus.Completed, StartTime = DateTime.UtcNow.AddHours(-1) },
            new() { Id = 2, BackupConfigId = 1, Status = BackupStatus.Completed, StartTime = DateTime.UtcNow.AddHours(-2) },
            new() { Id = 3, BackupConfigId = 2, Status = BackupStatus.Failed, StartTime = DateTime.UtcNow.AddHours(-3) }
        };

        _mockRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(allLogs);

        // Act
        var result = await _service.SearchBackupLogsAsync(criteria);

        // Assert
        Assert.Equal(2, result.Logs.Count());
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(10, result.PageSize);
        Assert.All(result.Logs, log => 
        {
            Assert.Equal(1, log.BackupConfigId);
            Assert.Equal(BackupStatus.Completed, log.Status);
        });
    }

    [Fact]
    public async Task GetRunningBackupsAsync_ShouldReturnRunningBackups()
    {
        // Arrange
        var runningLogs = new List<BackupLog>
        {
            new() { Id = 1, Status = BackupStatus.Compressing },
            new() { Id = 2, Status = BackupStatus.Transferring }
        };

        _mockRepository.Setup(r => r.GetRunningBackupsAsync())
            .ReturnsAsync(runningLogs);

        // Act
        var result = await _service.GetRunningBackupsAsync();

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, log => Assert.True(log.IsRunning));
    }

    [Fact]
    public async Task CancelBackupAsync_ValidBackup_ShouldCancelAndComplete()
    {
        // Arrange
        var backupLogId = 1;
        var reason = "User requested cancellation";

        _mockRepository.Setup(r => r.UpdateStatusAsync(backupLogId, BackupStatus.Cancelled, reason))
            .ReturnsAsync(true);
        _mockRepository.Setup(r => r.CompleteBackupAsync(backupLogId, BackupStatus.Cancelled, null, null, reason))
            .ReturnsAsync(true);

        // Act
        await _service.CancelBackupAsync(backupLogId, reason);

        // Assert
        _mockRepository.Verify(r => r.UpdateStatusAsync(backupLogId, BackupStatus.Cancelled, reason), Times.Once);
        _mockRepository.Verify(r => r.CompleteBackupAsync(backupLogId, BackupStatus.Cancelled, null, null, reason), Times.Once);
    }

    [Fact]
    public async Task GetBackupStatisticsAsync_WithDateRange_ShouldReturnStatistics()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var expectedStats = new BackupStatistics
        {
            TotalBackups = 10,
            SuccessfulBackups = 8,
            FailedBackups = 2,
            SuccessRate = 80.0
        };

        _mockRepository.Setup(r => r.GetStatisticsAsync(startDate, endDate))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _service.GetBackupStatisticsAsync(startDate, endDate);

        // Assert
        Assert.Equal(expectedStats.TotalBackups, result.TotalBackups);
        Assert.Equal(expectedStats.SuccessfulBackups, result.SuccessfulBackups);
        Assert.Equal(expectedStats.FailedBackups, result.FailedBackups);
        Assert.Equal(expectedStats.SuccessRate, result.SuccessRate);
    }

    [Fact]
    public async Task CleanupOldLogsAsync_WithRetentionPolicy_ShouldCleanupLogs()
    {
        // Arrange
        var policy = new RetentionPolicy
        {
            Name = "Test Policy",
            MaxAgeDays = 30,
            MaxCount = 100
        };

        _mockRepository.Setup(r => r.CleanupOldLogsAsync(30, 100))
            .ReturnsAsync(5);

        // Act
        var result = await _service.CleanupOldLogsAsync(policy);

        // Assert
        Assert.Equal(5, result);
        _mockRepository.Verify(r => r.CleanupOldLogsAsync(30, 100), Times.Once);
    }
}