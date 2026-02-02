using Microsoft.Extensions.Logging;
using Moq;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;

namespace MySqlBackupTool.Tests.Services;

public class NotificationServiceTests
{
    private readonly Mock<ILogger<NotificationService>> _mockLogger;
    private readonly NotificationService _notificationService;

    public NotificationServiceTests()
    {
        _mockLogger = new Mock<ILogger<NotificationService>>();
        _notificationService = new NotificationService(_mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithValidLogger_ShouldInitializeService()
    {
        // Arrange & Act
        var service = new NotificationService(_mockLogger.Object);

        // Assert
        Assert.NotNull(service);
        Assert.NotNull(service.Configuration);
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new NotificationService(null!));
    }

    [Fact]
    public void UpdateConfiguration_WithValidConfig_ShouldUpdateConfiguration()
    {
        // Arrange
        var config = new SmtpConfig
        {
            Host = "smtp.test.com",
            Port = 587,
            EnableSsl = true,
            Username = "test@test.com",
            Password = "password",
            FromAddress = "from@test.com",
            FromName = "Test Sender"
        };

        // Act
        _notificationService.UpdateConfiguration(config);

        // Assert
        Assert.Equal(config.Host, _notificationService.Configuration.Host);
        Assert.Equal(config.Port, _notificationService.Configuration.Port);
        Assert.Equal(config.EnableSsl, _notificationService.Configuration.EnableSsl);
        Assert.Equal(config.Username, _notificationService.Configuration.Username);
        Assert.Equal(config.Password, _notificationService.Configuration.Password);
        Assert.Equal(config.FromAddress, _notificationService.Configuration.FromAddress);
        Assert.Equal(config.FromName, _notificationService.Configuration.FromName);
    }

    [Fact]
    public void UpdateConfiguration_WithNullConfig_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => _notificationService.UpdateConfiguration(null!));
    }

    [Fact]
    public async Task SendEmailAsync_WithNullMessage_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _notificationService.SendEmailAsync(null!));
    }

    [Fact]
    public async Task GetStatusAsync_WithNullOrEmptyId_ShouldThrowArgumentException()
    {
        // Arrange, Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _notificationService.GetStatusAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => _notificationService.GetStatusAsync(string.Empty));
    }

    [Fact]
    public async Task GetStatusAsync_WithValidId_ShouldReturnNullForNonExistentId()
    {
        // Arrange
        var notificationId = Guid.NewGuid().ToString();

        // Act
        var result = await _notificationService.GetStatusAsync(notificationId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetTemplatesAsync_ShouldReturnActiveTemplates()
    {
        // Act
        var templates = await _notificationService.GetTemplatesAsync();

        // Assert
        Assert.NotNull(templates);
        var templateList = templates.ToList();
        Assert.True(templateList.Count >= 2); // Should have at least the default templates
        Assert.All(templateList, template => Assert.True(template.IsActive));
    }

    [Fact]
    public async Task GetTemplatesByCategoryAsync_WithBackupCategory_ShouldReturnBackupTemplates()
    {
        // Act
        var templates = await _notificationService.GetTemplatesByCategoryAsync("Backup");

        // Assert
        Assert.NotNull(templates);
        var templateList = templates.ToList();
        Assert.True(templateList.Count >= 2); // Should have backup success and failure templates
        Assert.All(templateList, template => Assert.Equal("Backup", template.Category));
    }

    [Fact]
    public async Task GetTemplateAsync_WithValidName_ShouldReturnTemplate()
    {
        // Act
        var template = await _notificationService.GetTemplateAsync("backup-success");

        // Assert
        Assert.NotNull(template);
        Assert.Equal("backup-success", template.Name);
        Assert.Equal("Backup", template.Category);
        Assert.NotEmpty(template.Subject);
        Assert.NotEmpty(template.HtmlBody);
        Assert.NotEmpty(template.TextBody);
        Assert.NotEmpty(template.RequiredVariables);
    }

    [Fact]
    public async Task GetTemplateAsync_WithInvalidName_ShouldReturnNull()
    {
        // Act
        var template = await _notificationService.GetTemplateAsync("non-existent-template");

        // Assert
        Assert.Null(template);
    }

    [Fact]
    public async Task GetTemplateAsync_WithNullOrEmptyName_ShouldThrowArgumentException()
    {
        // Arrange, Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _notificationService.GetTemplateAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => _notificationService.GetTemplateAsync(string.Empty));
    }

    [Fact]
    public async Task CreateEmailFromTemplateAsync_WithValidTemplate_ShouldCreateEmail()
    {
        // Arrange
        var variables = new Dictionary<string, object>
        {
            { "DatabaseName", "TestDB" },
            { "BackupFileName", "TestDB_backup.zip" },
            { "FileSize", "100 MB" },
            { "Duration", "5 minutes" },
            { "CompletedAt", "2024-01-01 12:00:00" }
        };

        // Act
        var email = await _notificationService.CreateEmailFromTemplateAsync(
            "backup-success", "test@example.com", variables);

        // Assert
        Assert.NotNull(email);
        Assert.Equal("test@example.com", email.To);
        Assert.Contains("TestDB", email.Subject);
        Assert.Contains("TestDB", email.Body);
        Assert.Contains("TestDB_backup.zip", email.Body);
        Assert.True(email.IsHtml);
    }

    [Fact]
    public async Task CreateEmailFromTemplateAsync_WithMissingVariables_ShouldReturnNull()
    {
        // Arrange
        var variables = new Dictionary<string, object>
        {
            { "DatabaseName", "TestDB" }
            // Missing required variables
        };

        // Act
        var email = await _notificationService.CreateEmailFromTemplateAsync(
            "backup-success", "test@example.com", variables);

        // Assert
        Assert.Null(email);
    }

    [Fact]
    public async Task CreateEmailFromTemplateAsync_WithNonExistentTemplate_ShouldReturnNull()
    {
        // Arrange
        var variables = new Dictionary<string, object>();

        // Act
        var email = await _notificationService.CreateEmailFromTemplateAsync(
            "non-existent", "test@example.com", variables);

        // Assert
        Assert.Null(email);
    }

    [Fact]
    public async Task CreateEmailFromTemplateAsync_WithNullOrEmptyParameters_ShouldThrowArgumentException()
    {
        // Arrange
        var variables = new Dictionary<string, object>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _notificationService.CreateEmailFromTemplateAsync(null!, "test@example.com", variables));
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _notificationService.CreateEmailFromTemplateAsync(string.Empty, "test@example.com", variables));
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _notificationService.CreateEmailFromTemplateAsync("template", null!, variables));
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _notificationService.CreateEmailFromTemplateAsync("template", string.Empty, variables));
    }

    [Fact]
    public async Task TestSmtpConnectionAsync_WithNullConfig_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _notificationService.TestSmtpConnectionAsync(null!));
    }

    [Fact]
    public async Task SendBulkEmailAsync_WithNullMessages_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _notificationService.SendBulkEmailAsync(null!));
    }

    [Fact]
    public async Task SendBulkEmailAsync_WithEmptyMessages_ShouldReturnEmptyResults()
    {
        // Arrange
        var messages = new List<EmailMessage>();

        // Act
        var results = await _notificationService.SendBulkEmailAsync(messages);

        // Assert
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async Task GetStatisticsAsync_ShouldReturnValidStatistics()
    {
        // Arrange
        var fromDate = DateTime.Now.AddDays(-7);
        var toDate = DateTime.Now;

        // Act
        var statistics = await _notificationService.GetStatisticsAsync(fromDate, toDate);

        // Assert
        Assert.NotNull(statistics);
        Assert.Equal(fromDate, statistics.FromDate);
        Assert.Equal(toDate, statistics.ToDate);
        Assert.True(statistics.GeneratedAt <= DateTime.Now);
        Assert.NotNull(statistics.FailureReasons);
        Assert.NotNull(statistics.DeliveriesByHour);
    }

    [Fact]
    public async Task GetStatisticsAsync_WithNullDates_ShouldUseDefaultRange()
    {
        // Act
        var statistics = await _notificationService.GetStatisticsAsync();

        // Assert
        Assert.NotNull(statistics);
        Assert.True(statistics.FromDate <= statistics.ToDate);
        Assert.True(statistics.ToDate <= DateTime.Now);
    }

    [Theory]
    [InlineData("backup-success")]
    [InlineData("backup-failure")]
    public async Task DefaultTemplates_ShouldHaveRequiredProperties(string templateName)
    {
        // Act
        var template = await _notificationService.GetTemplateAsync(templateName);

        // Assert
        Assert.NotNull(template);
        Assert.Equal(templateName, template.Name);
        Assert.NotEmpty(template.Description);
        Assert.NotEmpty(template.Subject);
        Assert.NotEmpty(template.HtmlBody);
        Assert.NotEmpty(template.TextBody);
        Assert.NotEmpty(template.RequiredVariables);
        Assert.True(template.IsActive);
        Assert.Equal("Backup", template.Category);
    }

    [Fact]
    public void EmailMessage_DefaultProperties_ShouldBeValid()
    {
        // Act
        var message = new EmailMessage
        {
            To = "test@example.com",
            Subject = "Test Subject",
            Body = "Test Body"
        };

        // Assert
        Assert.NotEmpty(message.Id);
        Assert.Equal("test@example.com", message.To);
        Assert.Equal("Test Subject", message.Subject);
        Assert.Equal("Test Body", message.Body);
        Assert.True(message.IsHtml);
        Assert.Equal(EmailPriority.Normal, message.Priority);
        Assert.Empty(message.Attachments);
        Assert.Empty(message.Headers);
        Assert.True(message.CreatedAt <= DateTime.Now);
    }

    [Fact]
    public void SmtpConfig_DefaultProperties_ShouldBeValid()
    {
        // Act
        var config = new SmtpConfig();

        // Assert
        Assert.Equal(string.Empty, config.Host);
        Assert.Equal(587, config.Port);
        Assert.True(config.EnableSsl);
        Assert.Equal(string.Empty, config.Username);
        Assert.Equal(string.Empty, config.Password);
        Assert.Equal(string.Empty, config.FromAddress);
        Assert.Equal("MySQL Backup Tool", config.FromName);
        Assert.Equal(30, config.TimeoutSeconds);
    }

    [Fact]
    public void NotificationStatus_DefaultProperties_ShouldBeValid()
    {
        // Act
        var status = new NotificationStatus();

        // Assert
        Assert.Equal(string.Empty, status.NotificationId);
        Assert.Equal(NotificationDeliveryStatus.Pending, status.Status);
        Assert.True(status.CreatedAt <= DateTime.Now);
        Assert.Null(status.SentAt);
        Assert.Equal(0, status.AttemptCount);
        Assert.Null(status.LastError);
        Assert.Equal(string.Empty, status.Recipient);
        Assert.Equal(string.Empty, status.Subject);
        Assert.Null(status.NextRetryAt);
    }
}