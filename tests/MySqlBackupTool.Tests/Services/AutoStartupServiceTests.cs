using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;

namespace MySqlBackupTool.Tests.Services;

public class AutoStartupServiceTests : IDisposable
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IBackupConfigurationRepository> _mockBackupConfigRepository;
    private readonly Mock<IScheduleConfigurationRepository> _mockScheduleRepository;
    private readonly Mock<IBackupOrchestrator> _mockOrchestrator;
    private readonly Mock<ILogger<AutoStartupService>> _mockLogger;
    private readonly AutoStartupService _autoStartupService;

    public AutoStartupServiceTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockBackupConfigRepository = new Mock<IBackupConfigurationRepository>();
        _mockScheduleRepository = new Mock<IScheduleConfigurationRepository>();
        _mockOrchestrator = new Mock<IBackupOrchestrator>();
        _mockLogger = new Mock<ILogger<AutoStartupService>>();

        // Setup service provider to return mocked services
        var mockScope = new Mock<IServiceScope>();
        mockScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);
        
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);
        
        _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
            .Returns(mockScopeFactory.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IBackupConfigurationRepository)))
            .Returns(_mockBackupConfigRepository.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IScheduleConfigurationRepository)))
            .Returns(_mockScheduleRepository.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IBackupOrchestrator)))
            .Returns(_mockOrchestrator.Object);

        _autoStartupService = new AutoStartupService(_mockServiceProvider.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task StartAsync_ShouldExecuteAutoStartupBackups_WhenSchedulesAreDue()
    {
        // Arrange
        var backupConfig = CreateTestBackupConfiguration();
        var scheduleConfig = CreateTestScheduleConfiguration(backupConfig.Id, isDue: true);

        _mockBackupConfigRepository
            .Setup(x => x.GetActiveConfigurationsAsync())
            .ReturnsAsync(new[] { backupConfig });

        _mockScheduleRepository
            .Setup(x => x.GetByBackupConfigIdAsync(backupConfig.Id))
            .ReturnsAsync(new List<ScheduleConfiguration> { scheduleConfig });

        var successfulResult = new BackupResult
        {
            OperationId = Guid.NewGuid(),
            Success = true,
            BackupFilePath = "test_backup.zip",
            FileSize = 1024,
            Duration = TimeSpan.FromMinutes(1)
        };

        _mockOrchestrator
            .Setup(x => x.ExecuteBackupAsync(It.IsAny<BackupConfiguration>(), It.IsAny<IProgress<BackupProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successfulResult);

        // Act
        await _autoStartupService.StartAsync(CancellationToken.None);

        // Assert
        _mockOrchestrator.Verify(x => x.ExecuteBackupAsync(
            It.Is<BackupConfiguration>(c => c.Id == backupConfig.Id),
            It.IsAny<IProgress<BackupProgress>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockScheduleRepository.Verify(x => x.UpdateLastExecutedAsync(
            scheduleConfig.Id, 
            It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_ShouldNotExecuteBackups_WhenNoSchedulesAreDue()
    {
        // Arrange
        var backupConfig = CreateTestBackupConfiguration();
        var scheduleConfig = CreateTestScheduleConfiguration(backupConfig.Id, isDue: false);

        _mockBackupConfigRepository
            .Setup(x => x.GetActiveConfigurationsAsync())
            .ReturnsAsync(new[] { backupConfig });

        _mockScheduleRepository
            .Setup(x => x.GetByBackupConfigIdAsync(backupConfig.Id))
            .ReturnsAsync(new List<ScheduleConfiguration> { scheduleConfig });

        // Act
        await _autoStartupService.StartAsync(CancellationToken.None);

        // Assert
        _mockOrchestrator.Verify(x => x.ExecuteBackupAsync(
            It.IsAny<BackupConfiguration>(),
            It.IsAny<IProgress<BackupProgress>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartAsync_ShouldHandleBackupFailure_AndContinueProcessing()
    {
        // Arrange
        var backupConfig1 = CreateTestBackupConfiguration(id: 1, name: "Config1");
        var backupConfig2 = CreateTestBackupConfiguration(id: 2, name: "Config2");
        var scheduleConfig1 = CreateTestScheduleConfiguration(backupConfig1.Id, isDue: true);
        var scheduleConfig2 = CreateTestScheduleConfiguration(backupConfig2.Id, isDue: true);

        _mockBackupConfigRepository
            .Setup(x => x.GetActiveConfigurationsAsync())
            .ReturnsAsync(new[] { backupConfig1, backupConfig2 });

        _mockScheduleRepository
            .Setup(x => x.GetByBackupConfigIdAsync(backupConfig1.Id))
            .ReturnsAsync(new List<ScheduleConfiguration> { scheduleConfig1 });

        _mockScheduleRepository
            .Setup(x => x.GetByBackupConfigIdAsync(backupConfig2.Id))
            .ReturnsAsync(new List<ScheduleConfiguration> { scheduleConfig2 });

        var failedResult = new BackupResult
        {
            OperationId = Guid.NewGuid(),
            Success = false,
            ErrorMessage = "Test failure"
        };

        var successfulResult = new BackupResult
        {
            OperationId = Guid.NewGuid(),
            Success = true,
            BackupFilePath = "test_backup.zip"
        };

        _mockOrchestrator
            .SetupSequence(x => x.ExecuteBackupAsync(It.IsAny<BackupConfiguration>(), It.IsAny<IProgress<BackupProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedResult)
            .ReturnsAsync(successfulResult);

        // Act
        await _autoStartupService.StartAsync(CancellationToken.None);

        // Assert
        _mockOrchestrator.Verify(x => x.ExecuteBackupAsync(
            It.IsAny<BackupConfiguration>(),
            It.IsAny<IProgress<BackupProgress>>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task StartAsync_ShouldSkipInactiveConfigurations()
    {
        // Arrange
        var inactiveConfig = CreateTestBackupConfiguration(isActive: false);

        _mockBackupConfigRepository
            .Setup(x => x.GetActiveConfigurationsAsync())
            .ReturnsAsync(Array.Empty<BackupConfiguration>());

        // Act
        await _autoStartupService.StartAsync(CancellationToken.None);

        // Assert
        _mockScheduleRepository.Verify(x => x.GetByBackupConfigIdAsync(It.IsAny<int>()), Times.Never);
        _mockOrchestrator.Verify(x => x.ExecuteBackupAsync(
            It.IsAny<BackupConfiguration>(),
            It.IsAny<IProgress<BackupProgress>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartAsync_ShouldUpdateNextExecutionTimes_ForAllEnabledSchedules()
    {
        // Arrange
        var backupConfig = CreateTestBackupConfiguration();
        var scheduleConfig1 = CreateTestScheduleConfiguration(backupConfig.Id, isDue: false);
        var scheduleConfig2 = CreateTestScheduleConfiguration(backupConfig.Id, isDue: false);

        _mockBackupConfigRepository
            .Setup(x => x.GetActiveConfigurationsAsync())
            .ReturnsAsync(new[] { backupConfig });

        _mockScheduleRepository
            .Setup(x => x.GetEnabledSchedulesAsync())
            .ReturnsAsync(new List<ScheduleConfiguration> { scheduleConfig1, scheduleConfig2 });

        // Act
        await _autoStartupService.StartAsync(CancellationToken.None);

        // Assert
        _mockScheduleRepository.Verify(x => x.UpdateNextExecutionAsync(
            It.IsAny<int>(), 
            It.IsAny<DateTime?>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task StartAsync_ShouldHandleCancellation_Gracefully()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel(); // Cancel immediately

        _mockBackupConfigRepository
            .Setup(x => x.GetActiveConfigurationsAsync())
            .ReturnsAsync(Array.Empty<BackupConfiguration>());

        // Act & Assert - Should not throw
        await _autoStartupService.StartAsync(cancellationTokenSource.Token);
    }

    [Fact]
    public async Task StartAsync_ShouldLogAppropriateMessages()
    {
        // Arrange
        var backupConfig = CreateTestBackupConfiguration();

        _mockBackupConfigRepository
            .Setup(x => x.GetActiveConfigurationsAsync())
            .ReturnsAsync(new[] { backupConfig });

        _mockScheduleRepository
            .Setup(x => x.GetByBackupConfigIdAsync(backupConfig.Id))
            .ReturnsAsync(new List<ScheduleConfiguration>());

        _mockScheduleRepository
            .Setup(x => x.GetEnabledSchedulesAsync())
            .ReturnsAsync(new List<ScheduleConfiguration>());

        // Act
        await _autoStartupService.StartAsync(CancellationToken.None);

        // Assert
        VerifyLogMessage(LogLevel.Information, "Auto-startup service starting");
        VerifyLogMessage(LogLevel.Information, "Checking for auto-startup backup configurations");
    }

    [Fact]
    public void StopAsync_ShouldComplete_Successfully()
    {
        // Act & Assert - Should not throw
        var stopTask = _autoStartupService.StopAsync(CancellationToken.None);
        Assert.True(stopTask.IsCompletedSuccessfully);
    }

    private BackupConfiguration CreateTestBackupConfiguration(int id = 1, string name = "Test Config", bool isActive = true)
    {
        return new BackupConfiguration
        {
            Id = id,
            Name = name,
            IsActive = isActive,
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
    }

    private ScheduleConfiguration CreateTestScheduleConfiguration(int backupConfigId, bool isDue = false)
    {
        var schedule = new ScheduleConfiguration
        {
            Id = 1,
            BackupConfigId = backupConfigId,
            ScheduleType = ScheduleType.Daily,
            ScheduleTime = "02:00",
            IsEnabled = true,
            CreatedAt = DateTime.Now
        };

        if (isDue)
        {
            // Set last executed to more than 24 hours ago to make it due
            schedule.LastExecuted = DateTime.Now.AddDays(-2);
            schedule.NextExecution = DateTime.Now.AddMinutes(-10); // Past due
        }
        else
        {
            // Set next execution to future
            schedule.NextExecution = DateTime.Now.AddHours(1);
            schedule.LastExecuted = DateTime.Now.AddHours(-1);
        }

        return schedule;
    }

    private void VerifyLogMessage(LogLevel level, string message)
    {
        _mockLogger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}