using Microsoft.Extensions.Logging;
using Moq;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;

namespace MySqlBackupTool.Tests.Services;

public class BackgroundTaskManagerTests : IDisposable
{
    private readonly Mock<IBackupOrchestrator> _mockOrchestrator;
    private readonly Mock<ILogger<BackgroundTaskManager>> _mockLogger;
    private readonly BackgroundTaskManager _taskManager;

    public BackgroundTaskManagerTests()
    {
        _mockOrchestrator = new Mock<IBackupOrchestrator>();
        _mockLogger = new Mock<ILogger<BackgroundTaskManager>>();
        
        var config = new BackgroundTaskConfiguration
        {
            MaxConcurrentBackups = 2,
            ProgressUpdateIntervalMs = 100,
            BackupTimeoutMinutes = 1,
            AutoCleanupCompletedTasks = false // Disable for testing
        };
        
        _taskManager = new BackgroundTaskManager(_mockOrchestrator.Object, _mockLogger.Object, config);
    }

    [Fact]
    public async Task StartBackupAsync_ShouldExecuteBackupSuccessfully()
    {
        // Arrange
        var configuration = new BackupConfiguration
        {
            Id = 1,
            Name = "Test Backup",
            MySQLConnection = new MySQLConnectionInfo
            {
                ServiceName = "MySQL80",
                DataDirectoryPath = @"C:\ProgramData\MySQL\MySQL Server 8.0\Data"
            },
            TargetServer = new ServerEndpoint
            {
                IPAddress = "127.0.0.1",
                Port = 8080
            },
            TargetDirectory = @"C:\Backups",
            NamingStrategy = new FileNamingStrategy()
        };

        var expectedResult = new BackupResult
        {
            OperationId = Guid.NewGuid(),
            Success = true,
            BackupFilePath = "test_backup.zip",
            FileSize = 1024,
            Duration = TimeSpan.FromMinutes(1)
        };

        _mockOrchestrator
            .Setup(x => x.ExecuteBackupAsync(It.IsAny<BackupConfiguration>(), It.IsAny<IProgress<BackupProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _taskManager.StartBackupAsync(configuration);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedResult.BackupFilePath, result.BackupFilePath);
        Assert.Equal(expectedResult.FileSize, result.FileSize);
        
        _mockOrchestrator.Verify(x => x.ExecuteBackupAsync(
            It.Is<BackupConfiguration>(c => c.Name == "Test Backup"),
            It.IsAny<IProgress<BackupProgress>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartBackupAsync_ShouldHandleBackupFailure()
    {
        // Arrange
        var configuration = new BackupConfiguration
        {
            Id = 1,
            Name = "Test Backup",
            MySQLConnection = new MySQLConnectionInfo
            {
                ServiceName = "MySQL80",
                DataDirectoryPath = @"C:\ProgramData\MySQL\MySQL Server 8.0\Data"
            },
            TargetServer = new ServerEndpoint
            {
                IPAddress = "127.0.0.1",
                Port = 8080
            },
            TargetDirectory = @"C:\Backups",
            NamingStrategy = new FileNamingStrategy()
        };

        var expectedResult = new BackupResult
        {
            OperationId = Guid.NewGuid(),
            Success = false,
            ErrorMessage = "Test error",
            Duration = TimeSpan.FromMinutes(1)
        };

        _mockOrchestrator
            .Setup(x => x.ExecuteBackupAsync(It.IsAny<BackupConfiguration>(), It.IsAny<IProgress<BackupProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _taskManager.StartBackupAsync(configuration);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Test error", result.ErrorMessage);
    }

    [Fact]
    public async Task StartBackupAsync_ShouldReportProgress()
    {
        // Arrange
        var configuration = new BackupConfiguration
        {
            Id = 1,
            Name = "Test Backup",
            MySQLConnection = new MySQLConnectionInfo
            {
                ServiceName = "MySQL80",
                DataDirectoryPath = @"C:\ProgramData\MySQL\MySQL Server 8.0\Data"
            },
            TargetServer = new ServerEndpoint
            {
                IPAddress = "127.0.0.1",
                Port = 8080
            },
            TargetDirectory = @"C:\Backups",
            NamingStrategy = new FileNamingStrategy()
        };

        var progressReports = new List<BackupProgress>();
        var progressReporter = new Progress<BackupProgress>(progress => progressReports.Add(progress));
        var progressEventFired = false;

        var expectedResult = new BackupResult
        {
            OperationId = Guid.NewGuid(),
            Success = true,
            Duration = TimeSpan.FromMinutes(1)
        };

        _mockOrchestrator
            .Setup(x => x.ExecuteBackupAsync(It.IsAny<BackupConfiguration>(), It.IsAny<IProgress<BackupProgress>>(), It.IsAny<CancellationToken>()))
            .Callback<BackupConfiguration, IProgress<BackupProgress>?, CancellationToken>((config, progress, token) =>
            {
                // Simulate progress reporting
                progress?.Report(new BackupProgress
                {
                    CurrentStatus = BackupStatus.StoppingMySQL,
                    OverallProgress = 0.1,
                    CurrentOperation = "Stopping MySQL"
                });
                
                progress?.Report(new BackupProgress
                {
                    CurrentStatus = BackupStatus.Compressing,
                    OverallProgress = 0.5,
                    CurrentOperation = "Compressing files"
                });
                
                progress?.Report(new BackupProgress
                {
                    CurrentStatus = BackupStatus.Completed,
                    OverallProgress = 1.0,
                    CurrentOperation = "Backup completed"
                });
            })
            .ReturnsAsync(expectedResult);

        // Subscribe to progress events
        _taskManager.ProgressUpdated += (sender, args) =>
        {
            progressEventFired = true;
        };

        // Act
        var result = await _taskManager.StartBackupAsync(configuration, progressReporter);

        // Assert
        Assert.True(result.Success);
        Assert.True(progressEventFired, "Progress event should have been fired");
    }

    [Fact]
    public async Task GetRunningBackupsAsync_ShouldReturnEmptyWhenNoBackupsRunning()
    {
        // Act
        var runningBackups = await _taskManager.GetRunningBackupsAsync();

        // Assert
        Assert.Empty(runningBackups);
    }

    [Fact]
    public void GetStatistics_ShouldReturnInitialStatistics()
    {
        // Act
        var stats = _taskManager.GetStatistics();

        // Assert
        Assert.Equal(0, stats.TotalTasksStarted);
        Assert.Equal(0, stats.TasksCompleted);
        Assert.Equal(0, stats.TasksFailed);
        Assert.Equal(0, stats.TasksCancelled);
        Assert.Equal(0, stats.CurrentlyRunning);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _taskManager?.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}