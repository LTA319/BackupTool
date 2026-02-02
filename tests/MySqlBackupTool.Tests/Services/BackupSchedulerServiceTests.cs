using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;

namespace MySqlBackupTool.Tests.Services;

public class BackupSchedulerServiceTests : IDisposable
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private readonly Mock<IScheduleConfigurationRepository> _mockScheduleRepository;
    private readonly Mock<IBackupConfigurationRepository> _mockBackupConfigRepository;
    private readonly Mock<IBackupOrchestrator> _mockOrchestrator;
    private readonly Mock<ILogger<BackupSchedulerService>> _mockLogger;
    private readonly BackupSchedulerService _schedulerService;

    public BackupSchedulerServiceTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockServiceScope = new Mock<IServiceScope>();
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScheduleRepository = new Mock<IScheduleConfigurationRepository>();
        _mockBackupConfigRepository = new Mock<IBackupConfigurationRepository>();
        _mockOrchestrator = new Mock<IBackupOrchestrator>();
        _mockLogger = new Mock<ILogger<BackupSchedulerService>>();

        // Setup service provider chain
        _mockServiceScope.Setup(x => x.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockServiceScopeFactory.Setup(x => x.CreateScope()).Returns(_mockServiceScope.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(_mockServiceScopeFactory.Object);
        
        // Setup service resolution
        _mockServiceProvider.Setup(x => x.GetRequiredService<IScheduleConfigurationRepository>())
            .Returns(_mockScheduleRepository.Object);
        _mockServiceProvider.Setup(x => x.GetRequiredService<IBackupConfigurationRepository>())
            .Returns(_mockBackupConfigRepository.Object);
        _mockServiceProvider.Setup(x => x.GetRequiredService<IBackupOrchestrator>())
            .Returns(_mockOrchestrator.Object);

        _schedulerService = new BackupSchedulerService(_mockServiceProvider.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task AddOrUpdateScheduleAsync_ShouldAddNewSchedule_WhenIdIsZero()
    {
        // Arrange
        var backupConfig = CreateTestBackupConfiguration();
        var scheduleConfig = CreateTestScheduleConfiguration(backupConfig.Id);
        scheduleConfig.Id = 0; // New schedule

        _mockBackupConfigRepository
            .Setup(x => x.GetByIdAsync(backupConfig.Id))
            .ReturnsAsync(backupConfig);

        _mockScheduleRepository
            .Setup(x => x.AddAsync(It.IsAny<ScheduleConfiguration>()))
            .ReturnsAsync(scheduleConfig);

        // Act
        var result = await _schedulerService.AddOrUpdateScheduleAsync(scheduleConfig);

        // Assert
        Assert.NotNull(result);
        _mockScheduleRepository.Verify(x => x.AddAsync(It.IsAny<ScheduleConfiguration>()), Times.Once);
        _mockScheduleRepository.Verify(x => x.UpdateAsync(It.IsAny<ScheduleConfiguration>()), Times.Never);
    }

    [Fact]
    public async Task AddOrUpdateScheduleAsync_ShouldUpdateExistingSchedule_WhenIdIsNotZero()
    {
        // Arrange
        var backupConfig = CreateTestBackupConfiguration();
        var scheduleConfig = CreateTestScheduleConfiguration(backupConfig.Id);
        scheduleConfig.Id = 1; // Existing schedule

        _mockBackupConfigRepository
            .Setup(x => x.GetByIdAsync(backupConfig.Id))
            .ReturnsAsync(backupConfig);

        _mockScheduleRepository
            .Setup(x => x.UpdateAsync(It.IsAny<ScheduleConfiguration>()))
            .ReturnsAsync(scheduleConfig);

        // Act
        var result = await _schedulerService.AddOrUpdateScheduleAsync(scheduleConfig);

        // Assert
        Assert.NotNull(result);
        _mockScheduleRepository.Verify(x => x.UpdateAsync(It.IsAny<ScheduleConfiguration>()), Times.Once);
        _mockScheduleRepository.Verify(x => x.AddAsync(It.IsAny<ScheduleConfiguration>()), Times.Never);
    }

    [Fact]
    public async Task AddOrUpdateScheduleAsync_ShouldThrowException_WhenValidationFails()
    {
        // Arrange
        var scheduleConfig = new ScheduleConfiguration
        {
            BackupConfigId = 999, // Non-existent backup config
            ScheduleType = ScheduleType.Daily,
            ScheduleTime = "invalid-time",
            IsEnabled = true
        };

        _mockBackupConfigRepository
            .Setup(x => x.GetByIdAsync(999))
            .ReturnsAsync((BackupConfiguration?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _schedulerService.AddOrUpdateScheduleAsync(scheduleConfig));
    }

    [Fact]
    public async Task RemoveScheduleAsync_ShouldDeleteSchedule()
    {
        // Arrange
        var scheduleId = 1;

        // Act
        await _schedulerService.RemoveScheduleAsync(scheduleId);

        // Assert
        _mockScheduleRepository.Verify(x => x.DeleteAsync(scheduleId), Times.Once);
    }

    [Fact]
    public async Task GetAllSchedulesAsync_ShouldReturnAllSchedules()
    {
        // Arrange
        var schedules = new List<ScheduleConfiguration>
        {
            CreateTestScheduleConfiguration(1),
            CreateTestScheduleConfiguration(2)
        };

        _mockScheduleRepository
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(schedules);

        // Act
        var result = await _schedulerService.GetAllSchedulesAsync();

        // Assert
        Assert.Equal(2, result.Count());
        _mockScheduleRepository.Verify(x => x.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task GetSchedulesForBackupConfigAsync_ShouldReturnSchedulesForSpecificConfig()
    {
        // Arrange
        var backupConfigId = 1;
        var schedules = new List<ScheduleConfiguration>
        {
            CreateTestScheduleConfiguration(backupConfigId)
        };

        _mockScheduleRepository
            .Setup(x => x.GetByBackupConfigIdAsync(backupConfigId))
            .ReturnsAsync(schedules);

        // Act
        var result = await _schedulerService.GetSchedulesForBackupConfigAsync(backupConfigId);

        // Assert
        Assert.Single(result);
        Assert.Equal(backupConfigId, result[0].BackupConfigId);
        _mockScheduleRepository.Verify(x => x.GetByBackupConfigIdAsync(backupConfigId), Times.Once);
    }

    [Fact]
    public async Task SetScheduleEnabledAsync_ShouldUpdateScheduleEnabledStatus()
    {
        // Arrange
        var scheduleId = 1;
        var enabled = false;

        // Act
        await _schedulerService.SetScheduleEnabledAsync(scheduleId, enabled);

        // Assert
        _mockScheduleRepository.Verify(x => x.SetEnabledAsync(scheduleId, enabled), Times.Once);
    }

    [Fact]
    public async Task GetNextScheduledTimeAsync_ShouldReturnEarliestNextExecution()
    {
        // Arrange
        var now = DateTime.Now;
        var schedules = new List<ScheduleConfiguration>
        {
            new ScheduleConfiguration { NextExecution = now.AddHours(2) },
            new ScheduleConfiguration { NextExecution = now.AddHours(1) }, // Earliest
            new ScheduleConfiguration { NextExecution = now.AddHours(3) }
        };

        _mockScheduleRepository
            .Setup(x => x.GetEnabledSchedulesAsync())
            .ReturnsAsync(schedules);

        // Act
        var result = await _schedulerService.GetNextScheduledTimeAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(now.AddHours(1).ToString(), result.Value.ToString());
    }

    [Fact]
    public async Task GetNextScheduledTimeAsync_ShouldReturnNull_WhenNoEnabledSchedules()
    {
        // Arrange
        _mockScheduleRepository
            .Setup(x => x.GetEnabledSchedulesAsync())
            .ReturnsAsync(new List<ScheduleConfiguration>());

        // Act
        var result = await _schedulerService.GetNextScheduledTimeAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task TriggerScheduledBackupAsync_ShouldExecuteBackup_WhenScheduleExists()
    {
        // Arrange
        var scheduleId = 1;
        var backupConfig = CreateTestBackupConfiguration();
        var schedule = CreateTestScheduleConfiguration(backupConfig.Id);
        schedule.Id = scheduleId;
        schedule.BackupConfiguration = backupConfig;

        var successfulResult = new BackupResult
        {
            OperationId = Guid.NewGuid(),
            Success = true,
            BackupFilePath = "test_backup.zip"
        };

        _mockScheduleRepository
            .Setup(x => x.GetByIdAsync(scheduleId))
            .ReturnsAsync(schedule);

        _mockOrchestrator
            .Setup(x => x.ExecuteBackupAsync(It.IsAny<BackupConfiguration>(), It.IsAny<IProgress<BackupProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successfulResult);

        // Act
        await _schedulerService.TriggerScheduledBackupAsync(scheduleId);

        // Assert
        _mockOrchestrator.Verify(x => x.ExecuteBackupAsync(
            It.Is<BackupConfiguration>(c => c.Id == backupConfig.Id),
            It.IsAny<IProgress<BackupProgress>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockScheduleRepository.Verify(x => x.UpdateLastExecutedAsync(
            scheduleId, 
            It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task TriggerScheduledBackupAsync_ShouldThrowException_WhenScheduleNotFound()
    {
        // Arrange
        var scheduleId = 999;

        _mockScheduleRepository
            .Setup(x => x.GetByIdAsync(scheduleId))
            .ReturnsAsync((ScheduleConfiguration?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _schedulerService.TriggerScheduledBackupAsync(scheduleId));
    }

    [Fact]
    public async Task ValidateScheduleAsync_ShouldReturnValid_ForValidSchedule()
    {
        // Arrange
        var backupConfig = CreateTestBackupConfiguration();
        var scheduleConfig = CreateTestScheduleConfiguration(backupConfig.Id);

        _mockBackupConfigRepository
            .Setup(x => x.GetByIdAsync(backupConfig.Id))
            .ReturnsAsync(backupConfig);

        // Act
        var (isValid, errors) = await _schedulerService.ValidateScheduleAsync(scheduleConfig);

        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task ValidateScheduleAsync_ShouldReturnInvalid_ForInvalidScheduleTime()
    {
        // Arrange
        var backupConfig = CreateTestBackupConfiguration();
        var scheduleConfig = CreateTestScheduleConfiguration(backupConfig.Id);
        scheduleConfig.ScheduleTime = "invalid-time";

        _mockBackupConfigRepository
            .Setup(x => x.GetByIdAsync(backupConfig.Id))
            .ReturnsAsync(backupConfig);

        // Act
        var (isValid, errors) = await _schedulerService.ValidateScheduleAsync(scheduleConfig);

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public async Task ValidateScheduleAsync_ShouldReturnInvalid_WhenBackupConfigNotFound()
    {
        // Arrange
        var scheduleConfig = CreateTestScheduleConfiguration(999); // Non-existent backup config

        _mockBackupConfigRepository
            .Setup(x => x.GetByIdAsync(999))
            .ReturnsAsync((BackupConfiguration?)null);

        // Act
        var (isValid, errors) = await _schedulerService.ValidateScheduleAsync(scheduleConfig);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("does not exist"));
    }

    private BackupConfiguration CreateTestBackupConfiguration(int id = 1, string name = "Test Config")
    {
        return new BackupConfiguration
        {
            Id = id,
            Name = name,
            IsActive = true,
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

    private ScheduleConfiguration CreateTestScheduleConfiguration(int backupConfigId)
    {
        return new ScheduleConfiguration
        {
            Id = 1,
            BackupConfigId = backupConfigId,
            ScheduleType = ScheduleType.Daily,
            ScheduleTime = "02:00",
            IsEnabled = true,
            CreatedAt = DateTime.Now,
            NextExecution = DateTime.Now.AddHours(1)
        };
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}