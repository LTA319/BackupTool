using Microsoft.EntityFrameworkCore;
using MySqlBackupTool.Shared.Data;
using MySqlBackupTool.Shared.Data.Repositories;
using MySqlBackupTool.Shared.Models;
using Xunit;

namespace MySqlBackupTool.Tests.Data;

public class BackupLogRepositoryTests : IDisposable
{
    private readonly BackupDbContext _context;
    private readonly BackupLogRepository _repository;

    public BackupLogRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<BackupDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new BackupDbContext(options);
        _repository = new BackupLogRepository(_context);
    }

    [Fact]
    public async Task AddAsync_ValidBackupLog_ShouldAddSuccessfully()
    {
        // Arrange
        var backupLog = CreateValidBackupLog(1);

        // Act
        var result = await _repository.AddAsync(backupLog);
        await _repository.SaveChangesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.True(result.StartTime > DateTime.MinValue);
    }

    [Fact]
    public async Task GetByConfigurationIdAsync_ExistingLogs_ShouldReturnOrderedByStartTime()
    {
        // Arrange
        var configId = 1;
        var log1 = CreateValidBackupLog(configId);
        log1.StartTime = DateTime.UtcNow.AddHours(-2);
        
        var log2 = CreateValidBackupLog(configId);
        log2.StartTime = DateTime.UtcNow.AddHours(-1);
        
        var log3 = CreateValidBackupLog(2); // Different config

        await _repository.AddAsync(log1);
        await _repository.AddAsync(log2);
        await _repository.AddAsync(log3);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _repository.GetByConfigurationIdAsync(configId);

        // Assert
        Assert.Equal(2, result.Count());
        var orderedResults = result.ToList();
        Assert.True(orderedResults[0].StartTime > orderedResults[1].StartTime); // Descending order
    }

    [Fact]
    public async Task GetByDateRangeAsync_LogsInRange_ShouldReturnMatchingLogs()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var log1 = CreateValidBackupLog(1);
        log1.StartTime = baseTime.AddDays(-5);
        
        var log2 = CreateValidBackupLog(1);
        log2.StartTime = baseTime.AddDays(-3);
        
        var log3 = CreateValidBackupLog(1);
        log3.StartTime = baseTime.AddDays(-1);

        await _repository.AddAsync(log1);
        await _repository.AddAsync(log2);
        await _repository.AddAsync(log3);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _repository.GetByDateRangeAsync(baseTime.AddDays(-4), baseTime.AddDays(-2));

        // Assert
        Assert.Single(result);
        Assert.Equal(log2.StartTime.Date, result.First().StartTime.Date);
    }

    [Fact]
    public async Task GetByStatusAsync_SpecificStatus_ShouldReturnMatchingLogs()
    {
        // Arrange
        var log1 = CreateValidBackupLog(1);
        log1.Status = BackupStatus.Completed;
        
        var log2 = CreateValidBackupLog(1);
        log2.Status = BackupStatus.Failed;
        
        var log3 = CreateValidBackupLog(1);
        log3.Status = BackupStatus.Completed;

        await _repository.AddAsync(log1);
        await _repository.AddAsync(log2);
        await _repository.AddAsync(log3);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _repository.GetByStatusAsync(BackupStatus.Completed);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, log => Assert.Equal(BackupStatus.Completed, log.Status));
    }

    [Fact]
    public async Task GetRunningBackupsAsync_MixedStatuses_ShouldReturnOnlyRunning()
    {
        // Arrange
        var runningLog1 = CreateValidBackupLog(1);
        runningLog1.Status = BackupStatus.Compressing;
        
        var runningLog2 = CreateValidBackupLog(1);
        runningLog2.Status = BackupStatus.Transferring;
        
        var completedLog = CreateValidBackupLog(1);
        completedLog.Status = BackupStatus.Completed;
        
        var failedLog = CreateValidBackupLog(1);
        failedLog.Status = BackupStatus.Failed;

        await _repository.AddAsync(runningLog1);
        await _repository.AddAsync(runningLog2);
        await _repository.AddAsync(completedLog);
        await _repository.AddAsync(failedLog);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _repository.GetRunningBackupsAsync();

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, log => Assert.True(log.IsRunning));
    }

    [Fact]
    public async Task GetFailedBackupsAsync_MixedStatuses_ShouldReturnOnlyFailed()
    {
        // Arrange
        var failedLog1 = CreateValidBackupLog(1);
        failedLog1.Status = BackupStatus.Failed;
        
        var failedLog2 = CreateValidBackupLog(1);
        failedLog2.Status = BackupStatus.Failed;
        
        var completedLog = CreateValidBackupLog(1);
        completedLog.Status = BackupStatus.Completed;

        await _repository.AddAsync(failedLog1);
        await _repository.AddAsync(failedLog2);
        await _repository.AddAsync(completedLog);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _repository.GetFailedBackupsAsync();

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, log => Assert.Equal(BackupStatus.Failed, log.Status));
    }

    [Fact]
    public async Task GetStatisticsAsync_VariousLogs_ShouldCalculateCorrectly()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var startDate = baseTime.AddDays(-7);
        var endDate = baseTime;

        var completedLog1 = CreateValidBackupLog(1);
        completedLog1.Status = BackupStatus.Completed;
        completedLog1.StartTime = baseTime.AddDays(-5);
        completedLog1.EndTime = baseTime.AddDays(-5).AddHours(1);
        completedLog1.FileSize = 1000000; // 1MB

        var completedLog2 = CreateValidBackupLog(1);
        completedLog2.Status = BackupStatus.Completed;
        completedLog2.StartTime = baseTime.AddDays(-3);
        completedLog2.EndTime = baseTime.AddDays(-3).AddHours(2);
        completedLog2.FileSize = 2000000; // 2MB

        var failedLog = CreateValidBackupLog(1);
        failedLog.Status = BackupStatus.Failed;
        failedLog.StartTime = baseTime.AddDays(-2);
        failedLog.EndTime = baseTime.AddDays(-2).AddMinutes(30);

        var cancelledLog = CreateValidBackupLog(1);
        cancelledLog.Status = BackupStatus.Cancelled;
        cancelledLog.StartTime = baseTime.AddDays(-1);

        await _repository.AddAsync(completedLog1);
        await _repository.AddAsync(completedLog2);
        await _repository.AddAsync(failedLog);
        await _repository.AddAsync(cancelledLog);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _repository.GetStatisticsAsync(startDate, endDate);

        // Assert
        Assert.Equal(4, result.TotalBackups);
        Assert.Equal(2, result.SuccessfulBackups);
        Assert.Equal(1, result.FailedBackups);
        Assert.Equal(1, result.CancelledBackups);
        Assert.Equal(3000000, result.TotalBytesTransferred); // 3MB total
        Assert.Equal(50.0, result.SuccessRate); // 2/4 * 100
        Assert.Equal(1500000.0, result.AverageBackupSize); // 3MB / 2 successful
    }

    [Fact]
    public async Task UpdateStatusAsync_ExistingLog_ShouldUpdateStatus()
    {
        // Arrange
        var backupLog = CreateValidBackupLog(1);
        backupLog.Status = BackupStatus.Queued;
        var added = await _repository.AddAsync(backupLog);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _repository.UpdateStatusAsync(added.Id, BackupStatus.Compressing, "Starting compression");

        // Assert
        Assert.True(result);
        var updated = await _repository.GetByIdAsync(added.Id);
        Assert.NotNull(updated);
        Assert.Equal(BackupStatus.Compressing, updated.Status);
        Assert.Equal("Starting compression", updated.ErrorMessage);
    }

    [Fact]
    public async Task CompleteBackupAsync_ExistingLog_ShouldSetEndTimeAndFinalStatus()
    {
        // Arrange
        var backupLog = CreateValidBackupLog(1);
        backupLog.Status = BackupStatus.Transferring;
        var added = await _repository.AddAsync(backupLog);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _repository.CompleteBackupAsync(
            added.Id, 
            BackupStatus.Completed, 
            "/path/to/backup.zip", 
            5000000, 
            null);

        // Assert
        Assert.True(result);
        var updated = await _repository.GetByIdAsync(added.Id);
        Assert.NotNull(updated);
        Assert.Equal(BackupStatus.Completed, updated.Status);
        Assert.NotNull(updated.EndTime);
        Assert.Equal("/path/to/backup.zip", updated.FilePath);
        Assert.Equal(5000000, updated.FileSize);
    }

    [Fact]
    public async Task GetMostRecentAsync_MultipleLogsForConfig_ShouldReturnMostRecent()
    {
        // Arrange
        var configId = 1;
        var baseTime = DateTime.UtcNow;
        
        var oldLog = CreateValidBackupLog(configId);
        oldLog.StartTime = baseTime.AddDays(-5);
        
        var recentLog = CreateValidBackupLog(configId);
        recentLog.StartTime = baseTime.AddDays(-1);
        
        var otherConfigLog = CreateValidBackupLog(2);
        otherConfigLog.StartTime = baseTime; // Most recent overall, but different config

        await _repository.AddAsync(oldLog);
        await _repository.AddAsync(recentLog);
        await _repository.AddAsync(otherConfigLog);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _repository.GetMostRecentAsync(configId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(recentLog.StartTime.Date, result.StartTime.Date);
        Assert.Equal(configId, result.BackupConfigId);
    }

    [Fact]
    public async Task CleanupOldLogsAsync_OldLogs_ShouldDeleteOldOnes()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var maxAgeDays = 30;
        
        var oldLog1 = CreateValidBackupLog(1);
        oldLog1.StartTime = baseTime.AddDays(-35);
        
        var oldLog2 = CreateValidBackupLog(1);
        oldLog2.StartTime = baseTime.AddDays(-40);
        
        var recentLog = CreateValidBackupLog(1);
        recentLog.StartTime = baseTime.AddDays(-10);

        await _repository.AddAsync(oldLog1);
        await _repository.AddAsync(oldLog2);
        await _repository.AddAsync(recentLog);
        await _repository.SaveChangesAsync();

        // Act
        var deletedCount = await _repository.CleanupOldLogsAsync(maxAgeDays);

        // Assert
        Assert.Equal(2, deletedCount);
        var remainingLogs = await _repository.GetAllAsync();
        Assert.Single(remainingLogs);
        Assert.Equal(recentLog.StartTime.Date, remainingLogs.First().StartTime.Date);
    }

    private static BackupLog CreateValidBackupLog(int configId)
    {
        return new BackupLog
        {
            BackupConfigId = configId,
            StartTime = DateTime.UtcNow,
            Status = BackupStatus.Queued
        };
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}