using Microsoft.Extensions.Logging;
using Moq;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using System.Net.Http;

namespace MySqlBackupTool.Tests.Services;

public class AlertingServiceTests
{
    private readonly Mock<ILogger<AlertingService>> _mockLogger;
    private readonly AlertingService _service;
    private readonly AlertingConfig _config;

    public AlertingServiceTests()
    {
        _mockLogger = new Mock<ILogger<AlertingService>>();
        
        _config = new AlertingConfig
        {
            EnableAlerting = true,
            MinimumSeverity = AlertSeverity.Warning,
            MaxAlertsPerHour = 10,
            NotificationTimeout = TimeSpan.FromSeconds(30),
            FileLog = new FileLogConfig
            {
                Enabled = true,
                LogDirectory = Path.GetTempPath(),
                FileNamePattern = "test_alerts_{yyyy-MM-dd}.log",
                MaxFileSizeMB = 1,
                MaxFileCount = 5
            },
            Email = new EmailConfig
            {
                Enabled = false // Disabled for testing
            },
            Webhook = new WebhookConfig
            {
                Enabled = false // Disabled for testing
            }
        };

        _service = new AlertingService(_mockLogger.Object, new HttpClient(), _config);
    }

    [Fact]
    public async Task SendCriticalErrorAlertAsync_AlertingDisabled_ReturnsFalse()
    {
        // Arrange
        _config.EnableAlerting = false;
        _service.UpdateConfiguration(_config);
        
        var alert = new CriticalErrorAlert
        {
            OperationId = "test-op",
            ErrorType = "TestError",
            ErrorMessage = "Test error message"
        };

        // Act
        var result = await _service.SendCriticalErrorAlertAsync(alert);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SendCriticalErrorAlertAsync_NoChannelsEnabled_ReturnsFalse()
    {
        // Arrange
        _config.FileLog.Enabled = false;
        _service.UpdateConfiguration(_config);
        
        var alert = new CriticalErrorAlert
        {
            OperationId = "test-op",
            ErrorType = "TestError",
            ErrorMessage = "Test error message"
        };

        // Act
        var result = await _service.SendCriticalErrorAlertAsync(alert);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SendCriticalErrorAlertAsync_FileLogEnabled_WritesToFile()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var logFileName = $"test_alerts_{DateTime.UtcNow:yyyy-MM-dd}.log";
        var logFilePath = Path.Combine(tempDir, logFileName);
        
        // Clean up any existing test file
        if (File.Exists(logFilePath))
        {
            File.Delete(logFilePath);
        }

        var alert = new CriticalErrorAlert
        {
            OperationId = "test-op",
            ErrorType = "TestError",
            ErrorMessage = "Test error message"
        };

        // Act
        var result = await _service.SendCriticalErrorAlertAsync(alert);

        // Assert
        Assert.True(result);
        Assert.True(File.Exists(logFilePath));
        
        var logContent = await File.ReadAllTextAsync(logFilePath);
        Assert.Contains("TestError", logContent);
        Assert.Contains("Test error message", logContent);
        
        // Clean up
        if (File.Exists(logFilePath))
        {
            File.Delete(logFilePath);
        }
    }

    [Fact]
    public async Task SendNotificationAsync_SeverityBelowThreshold_ReturnsFalse()
    {
        // Arrange
        _config.MinimumSeverity = AlertSeverity.Error;
        _service.UpdateConfiguration(_config);
        
        var notification = new Notification
        {
            Subject = "Test Warning",
            Message = "This is a warning",
            Severity = AlertSeverity.Warning
        };

        // Act
        var result = await _service.SendNotificationAsync(notification);

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task SendNotificationAsync_ValidNotification_SendsToEnabledChannels()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var logFileName = $"test_alerts_{DateTime.UtcNow:yyyy-MM-dd}.log";
        var logFilePath = Path.Combine(tempDir, logFileName);
        
        // Clean up any existing test file
        if (File.Exists(logFilePath))
        {
            File.Delete(logFilePath);
        }

        var notification = new Notification
        {
            Subject = "Test Error",
            Message = "This is an error notification",
            Severity = AlertSeverity.Error
        };

        // Act
        var result = await _service.SendNotificationAsync(notification);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.SuccessfulChannels); // Only FileLog is enabled
        Assert.True(result.ChannelResults[NotificationChannel.FileLog]);
        
        // Verify file was written
        Assert.True(File.Exists(logFilePath));
        var logContent = await File.ReadAllTextAsync(logFilePath);
        Assert.Contains("Test Error", logContent);
        
        // Clean up
        if (File.Exists(logFilePath))
        {
            File.Delete(logFilePath);
        }
    }

    [Fact]
    public async Task TestNotificationChannelsAsync_FileLogEnabled_ReturnsTrue()
    {
        // Act
        var results = await _service.TestNotificationChannelsAsync();

        // Assert
        Assert.NotNull(results);
        Assert.True(results.ContainsKey(NotificationChannel.FileLog));
        Assert.True(results[NotificationChannel.FileLog]);
    }

    [Fact]
    public void UpdateConfiguration_ValidConfig_UpdatesConfiguration()
    {
        // Arrange
        var newConfig = new AlertingConfig
        {
            EnableAlerting = false,
            MinimumSeverity = AlertSeverity.Critical
        };

        // Act
        _service.UpdateConfiguration(newConfig);

        // Assert
        Assert.False(_service.Configuration.EnableAlerting);
        Assert.Equal(AlertSeverity.Critical, _service.Configuration.MinimumSeverity);
    }

    [Fact]
    public void UpdateConfiguration_NullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.UpdateConfiguration(null!));
    }
}