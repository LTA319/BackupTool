using Microsoft.Extensions.Logging;
using Moq;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;

namespace MySqlBackupTool.Tests.Services;

public class RetentionManagementServiceTests
{
    private readonly Mock<IRetentionPolicyRepository> _mockRetentionRepository;
    private readonly Mock<IBackupLogRepository> _mockBackupLogRepository;
    private readonly Mock<ILogger<RetentionManagementService>> _mockLogger;
    private readonly RetentionManagementService _service;

    public RetentionManagementServiceTests()
    {
        _mockRetentionRepository = new Mock<IRetentionPolicyRepository>();
        _mockBackupLogRepository = new Mock<IBackupLogRepository>();
        _mockLogger = new Mock<ILogger<RetentionManagementService>>();
        _service = new RetentionManagementService(
            _mockRetentionRepository.Object,
            _mockBackupLogRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteRetentionPoliciesAsync_NoEnabledPolicies_ShouldReturnEmptyResult()
    {
        // Arrange
        _mockRetentionRepository.Setup(r => r.GetEnabledPoliciesAsync())
            .ReturnsAsync(new List<RetentionPolicy>());

        // Act
        var result = await _service.ExecuteRetentionPoliciesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(0, result.LogsDeleted);
        Assert.Equal(0, result.FilesDeleted);
        Assert.Equal(0, result.BytesFreed);
        Assert.Empty(result.AppliedPolicies);
    }

    [Fact]
    public async Task ExecuteRetentionPoliciesAsync_WithEnabledPolicies_ShouldApplyPolicies()
    {
        // Arrange
        var policies = new List<RetentionPolicy>
        {
            new()
            {
                Id = 1,
                Name = "30 Day Policy",
                MaxAgeDays = 30,
                IsEnabled = true
            },
            new()
            {
                Id = 2,
                Name = "100 Backup Policy",
                MaxCount = 100,
                IsEnabled = true
            }
        };

        _mockRetentionRepository.Setup(r => r.GetEnabledPoliciesAsync())
            .ReturnsAsync(policies);
        _mockBackupLogRepository.Setup(r => r.CleanupOldLogsAsync(30, null))
            .ReturnsAsync(5);
        _mockBackupLogRepository.Setup(r => r.CleanupOldLogsAsync(int.MaxValue, 100))
            .ReturnsAsync(3);
        _mockBackupLogRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<BackupLog>());

        // Act
        var result = await _service.ExecuteRetentionPoliciesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(8, result.LogsDeleted); // 5 + 3
        Assert.Equal(2, result.AppliedPolicies.Count);
        Assert.Contains(policies[0], result.AppliedPolicies);
        Assert.Contains(policies[1], result.AppliedPolicies);
    }

    [Fact]
    public async Task ApplyRetentionPolicyAsync_DisabledPolicy_ShouldReturnEmptyResult()
    {
        // Arrange
        var policy = new RetentionPolicy
        {
            Id = 1,
            Name = "Disabled Policy",
            MaxAgeDays = 30,
            IsEnabled = false
        };

        // Act
        var result = await _service.ApplyRetentionPolicyAsync(policy);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.LogsDeleted);
        Assert.Equal(0, result.FilesDeleted);
        Assert.Equal(0, result.BytesFreed);
    }

    [Fact]
    public async Task ApplyRetentionPolicyAsync_WithMaxAgeDays_ShouldCleanupOldLogs()
    {
        // Arrange
        var policy = new RetentionPolicy
        {
            Id = 1,
            Name = "30 Day Policy",
            MaxAgeDays = 30,
            IsEnabled = true
        };

        _mockBackupLogRepository.Setup(r => r.CleanupOldLogsAsync(30, null))
            .ReturnsAsync(5);
        _mockBackupLogRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<BackupLog>());

        // Act
        var result = await _service.ApplyRetentionPolicyAsync(policy);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(5, result.LogsDeleted);
        Assert.Single(result.AppliedPolicies);
        Assert.Equal(policy, result.AppliedPolicies[0]);
        
        _mockBackupLogRepository.Verify(r => r.CleanupOldLogsAsync(30, null), Times.Once);
    }

    [Fact]
    public async Task ApplyRetentionPolicyAsync_WithMaxCount_ShouldCleanupExcessLogs()
    {
        // Arrange
        var policy = new RetentionPolicy
        {
            Id = 1,
            Name = "100 Backup Policy",
            MaxCount = 100,
            IsEnabled = true
        };

        _mockBackupLogRepository.Setup(r => r.CleanupOldLogsAsync(int.MaxValue, 100))
            .ReturnsAsync(10);
        _mockBackupLogRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<BackupLog>());

        // Act
        var result = await _service.ApplyRetentionPolicyAsync(policy);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(10, result.LogsDeleted);
        
        _mockBackupLogRepository.Verify(r => r.CleanupOldLogsAsync(int.MaxValue, 100), Times.Once);
    }

    [Fact]
    public async Task CreateRetentionPolicyAsync_ValidPolicy_ShouldCreatePolicy()
    {
        // Arrange
        var policy = new RetentionPolicy
        {
            Name = "Test Policy",
            Description = "Test Description",
            MaxAgeDays = 30,
            IsEnabled = true
        };

        var createdPolicy = new RetentionPolicy
        {
            Id = 1,
            Name = policy.Name,
            Description = policy.Description,
            MaxAgeDays = policy.MaxAgeDays,
            IsEnabled = policy.IsEnabled,
            CreatedAt = DateTime.Now
        };

        _mockRetentionRepository.Setup(r => r.IsNameUniqueAsync(policy.Name, 0))
            .ReturnsAsync(true);
        _mockRetentionRepository.Setup(r => r.AddAsync(It.IsAny<RetentionPolicy>()))
            .ReturnsAsync(createdPolicy);
        _mockRetentionRepository.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _service.CreateRetentionPolicyAsync(policy);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal(policy.Name, result.Name);
        Assert.Equal(policy.Description, result.Description);
        Assert.Equal(policy.MaxAgeDays, result.MaxAgeDays);
        Assert.Equal(policy.IsEnabled, result.IsEnabled);
        
        _mockRetentionRepository.Verify(r => r.AddAsync(It.IsAny<RetentionPolicy>()), Times.Once);
        _mockRetentionRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateRetentionPolicyAsync_DuplicateName_ShouldThrowException()
    {
        // Arrange
        var policy = new RetentionPolicy
        {
            Name = "Existing Policy",
            MaxAgeDays = 30,
            IsEnabled = true
        };

        _mockRetentionRepository.Setup(r => r.IsNameUniqueAsync(policy.Name, 0))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _service.CreateRetentionPolicyAsync(policy));
        
        Assert.Contains("already exists", exception.Message);
    }

    [Theory]
    [InlineData("", "Policy name is required")]
    [InlineData(null, "Policy name is required")]
    public async Task CreateRetentionPolicyAsync_InvalidName_ShouldThrowException(string name, string expectedError)
    {
        // Arrange
        var policy = new RetentionPolicy
        {
            Name = name,
            MaxAgeDays = 30,
            IsEnabled = true
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.CreateRetentionPolicyAsync(policy));
        
        Assert.Contains(expectedError, exception.Message);
    }

    [Fact]
    public async Task CreateRetentionPolicyAsync_NoRetentionCriteria_ShouldThrowException()
    {
        // Arrange
        var policy = new RetentionPolicy
        {
            Name = "Invalid Policy",
            IsEnabled = true
            // No MaxAgeDays, MaxCount, or MaxStorageBytes
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.CreateRetentionPolicyAsync(policy));
        
        Assert.Contains("At least one retention criteria", exception.Message);
    }

    [Theory]
    [InlineData(0, "MaxAgeDays must be at least 1")]
    [InlineData(-1, "MaxAgeDays must be at least 1")]
    public async Task CreateRetentionPolicyAsync_InvalidMaxAgeDays_ShouldThrowException(int maxAgeDays, string expectedError)
    {
        // Arrange
        var policy = new RetentionPolicy
        {
            Name = "Test Policy",
            MaxAgeDays = maxAgeDays,
            IsEnabled = true
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.CreateRetentionPolicyAsync(policy));
        
        Assert.Contains(expectedError, exception.Message);
    }

    [Fact]
    public async Task UpdateRetentionPolicyAsync_ValidPolicy_ShouldUpdatePolicy()
    {
        // Arrange
        var policy = new RetentionPolicy
        {
            Id = 1,
            Name = "Updated Policy",
            Description = "Updated Description",
            MaxAgeDays = 60,
            IsEnabled = true
        };

        _mockRetentionRepository.Setup(r => r.IsNameUniqueAsync(policy.Name, policy.Id))
            .ReturnsAsync(true);
        _mockRetentionRepository.Setup(r => r.UpdateAsync(policy))
            .ReturnsAsync(policy);
        _mockRetentionRepository.Setup(r => r.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var result = await _service.UpdateRetentionPolicyAsync(policy);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(policy.Id, result.Id);
        Assert.Equal(policy.Name, result.Name);
        
        _mockRetentionRepository.Verify(r => r.UpdateAsync(policy), Times.Once);
        _mockRetentionRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetRetentionPolicyRecommendationsAsync_NoBackupHistory_ShouldReturnDefaultRecommendations()
    {
        // Arrange
        _mockBackupLogRepository.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<BackupLog>());

        // Act
        var result = await _service.GetRetentionPolicyRecommendationsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains(result, p => p.Name.Contains("30 Days"));
        Assert.Contains(result, p => p.Name.Contains("50 Backups"));
        Assert.All(result, p => Assert.False(p.IsEnabled)); // Recommendations should not be enabled by default
    }

    [Fact]
    public async Task GetRetentionPolicyRecommendationsAsync_WithBackupHistory_ShouldGenerateRecommendations()
    {
        // Arrange
        var backupLogs = new List<BackupLog>();
        for (int i = 0; i < 30; i++)
        {
            backupLogs.Add(new BackupLog
            {
                Id = i + 1,
                BackupConfigId = 1,
                Status = BackupStatus.Completed,
                StartTime = DateTime.Now.AddDays(-i),
                FileSize = 1000000 // 1MB
            });
        }

        _mockBackupLogRepository.Setup(r => r.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(backupLogs);

        // Act
        var result = await _service.GetRetentionPolicyRecommendationsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains(result, p => p.Name.Contains("Conservative"));
        Assert.Contains(result, p => p.Name.Contains("Balanced"));
        Assert.Contains(result, p => p.Name.Contains("Aggressive"));
        Assert.Contains(result, p => p.Name.Contains("Storage-Based"));
        Assert.All(result, p => Assert.False(p.IsEnabled)); // Recommendations should not be enabled by default
    }

    [Fact]
    public void RetentionPolicy_ShouldRetainBackup_ValidatesCorrectly()
    {
        // Arrange
        var policy = new RetentionPolicy
        {
            MaxAgeDays = 30,
            MaxCount = 100,
            MaxStorageBytes = 1000000000, // 1GB
            IsEnabled = true
        };

        var recentBackupDate = DateTime.Now.AddDays(-10);
        var oldBackupDate = DateTime.Now.AddDays(-40);

        // Act & Assert
        // Recent backup should be retained
        Assert.True(policy.ShouldRetainBackup(recentBackupDate, 50, 500000000, 1000000));
        
        // Old backup should not be retained (exceeds MaxAgeDays)
        Assert.False(policy.ShouldRetainBackup(oldBackupDate, 50, 500000000, 1000000));
        
        // Backup that would exceed count limit should not be retained
        Assert.False(policy.ShouldRetainBackup(recentBackupDate, 150, 500000000, 1000000));
        
        // Backup that would exceed storage limit should not be retained
        Assert.False(policy.ShouldRetainBackup(recentBackupDate, 50, 999000000, 2000000));
    }

    [Fact]
    public void RetentionPolicy_GetPolicyDescription_ReturnsCorrectDescription()
    {
        // Arrange
        var policy = new RetentionPolicy
        {
            MaxAgeDays = 30,
            MaxCount = 100,
            MaxStorageBytes = 1073741824 // 1GB
        };

        // Act
        var description = policy.GetPolicyDescription();

        // Assert
        Assert.Contains("Keep for 30 days", description);
        Assert.Contains("Keep max 100 backups", description);
        Assert.Contains("Use max 1.0 GB storage", description);
    }
}