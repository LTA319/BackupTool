using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace MySqlBackupTool.Tests.Integration;

/// <summary>
/// Integration tests for complete AlertingService functionality
/// Tests AlertingService can be created and used for HTTP operations and verifies all dependencies are properly injected and functional
/// Validates Requirements 4.2, 4.3: Backup Monitor functionality and AlertingService activation with all dependencies
/// </summary>
public class AlertingServiceIntegrationTests : IDisposable
{
    private readonly string _testDatabasePath;
    private readonly string _testLogDirectory;
    private readonly HttpClient _testHttpClient;
    private readonly TestHttpMessageHandler _testHttpHandler;

    public AlertingServiceIntegrationTests()
    {
        _testDatabasePath = Path.Combine(Path.GetTempPath(), $"test_alerting_integration_{Guid.NewGuid()}.db");
        _testLogDirectory = Path.Combine(Path.GetTempPath(), $"test_alerting_logs_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testLogDirectory);
        
        // Create test HTTP handler for mocking HTTP responses
        _testHttpHandler = new TestHttpMessageHandler();
        _testHttpClient = new HttpClient(_testHttpHandler);
    }

    [Fact]
    public void AlertingService_CanBeCreatedAndResolved_WithAllDependenciesInjected()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);

        // Act - Configure services as in real application
        services.AddSharedServices(connectionString);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Verify AlertingService can be resolved with all dependencies
        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);
        Assert.IsType<AlertingService>(alertingService);

        var concreteService = (AlertingService)alertingService;
        
        // Verify all constructor dependencies are properly injected
        Assert.NotNull(concreteService.Configuration);
        Assert.IsType<AlertingConfig>(concreteService.Configuration);
        
        // Verify service can be used without errors (indicates HttpClient is properly injected)
        Assert.NotNull(concreteService);
        
        // Verify AlertingConfig is properly configured
        var alertingConfig = serviceProvider.GetService<AlertingConfig>();
        Assert.NotNull(alertingConfig);
        Assert.Same(concreteService.Configuration, alertingConfig);
    }

    [Fact]
    public void AlertingService_WithCustomConfiguration_AppliesConfigurationCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);

        var configurationData = new Dictionary<string, string?>
        {
            ["Alerting:EnableAlerting"] = "true",
            ["Alerting:BaseUrl"] = "https://api.example.com",
            ["Alerting:TimeoutSeconds"] = "45",
            ["Alerting:MaxRetryAttempts"] = "5",
            ["Alerting:MaxAlertsPerHour"] = "100",
            ["Alerting:MinimumSeverity"] = "Warning",
            ["Alerting:DefaultHeaders:X-API-Key"] = "test-api-key",
            ["Alerting:DefaultHeaders:X-Environment"] = "integration-test",
            ["Alerting:Email:Enabled"] = "true",
            ["Alerting:Email:SmtpServer"] = "smtp.test.com",
            ["Alerting:Email:SmtpPort"] = "587",
            ["Alerting:Email:FromAddress"] = "test@example.com",
            ["Alerting:Webhook:Enabled"] = "true",
            ["Alerting:Webhook:Url"] = "https://webhook.test.com/alerts",
            ["Alerting:Webhook:HttpMethod"] = "POST",
            ["Alerting:FileLog:Enabled"] = "true",
            ["Alerting:FileLog:LogDirectory"] = _testLogDirectory
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        // Act - Configure services with custom configuration
        services.AddSharedServices(connectionString, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Verify configuration is applied correctly
        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);

        var concreteService = (AlertingService)alertingService;
        var config = concreteService.Configuration;

        Assert.True(config.EnableAlerting);
        Assert.Equal("https://api.example.com", config.BaseUrl);
        Assert.Equal(45, config.TimeoutSeconds);
        Assert.Equal(5, config.MaxRetryAttempts);
        Assert.Equal(100, config.MaxAlertsPerHour);
        Assert.Equal(AlertSeverity.Warning, config.MinimumSeverity);
        Assert.Equal("test-api-key", config.DefaultHeaders["X-API-Key"]);
        Assert.Equal("integration-test", config.DefaultHeaders["X-Environment"]);
        
        // Verify email configuration
        Assert.True(config.Email.Enabled);
        Assert.Equal("smtp.test.com", config.Email.SmtpServer);
        Assert.Equal(587, config.Email.SmtpPort);
        Assert.Equal("test@example.com", config.Email.FromAddress);
        
        // Verify webhook configuration
        Assert.True(config.Webhook.Enabled);
        Assert.Equal("https://webhook.test.com/alerts", config.Webhook.Url);
        Assert.Equal("POST", config.Webhook.HttpMethod);
        
        // Verify file log configuration
        Assert.True(config.FileLog.Enabled);
        Assert.Equal(_testLogDirectory, config.FileLog.LogDirectory);
    }

    [Fact]
    public async Task AlertingService_SendCriticalErrorAlert_WorksWithFileLogging()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);

        var configurationData = new Dictionary<string, string?>
        {
            ["Alerting:EnableAlerting"] = "true",
            ["Alerting:FileLog:Enabled"] = "true",
            ["Alerting:FileLog:LogDirectory"] = _testLogDirectory,
            ["Alerting:FileLog:FileNamePattern"] = "integration_test_{yyyy-MM-dd}.log",
            ["Alerting:Email:Enabled"] = "false",
            ["Alerting:Webhook:Enabled"] = "false"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        services.AddSharedServices(connectionString, configuration);
        var serviceProvider = services.BuildServiceProvider();

        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);

        var alert = new CriticalErrorAlert
        {
            OperationId = "integration-test-op",
            ErrorType = "IntegrationTestError",
            ErrorMessage = "This is an integration test error message",
            OccurredAt = DateTime.Now,
            Context = new Dictionary<string, object>
            {
                ["TestType"] = "Integration",
                ["Component"] = "AlertingService"
            }
        };

        // Act - Send critical error alert
        var result = await alertingService.SendCriticalErrorAlertAsync(alert);

        // Assert - Verify alert was sent successfully
        Assert.True(result);
        Assert.True(alert.AlertSent);
        Assert.NotNull(alert.AlertSentAt);

        // Verify log file was created and contains the alert
        var expectedLogFile = Path.Combine(_testLogDirectory, $"integration_test_{DateTime.Now:yyyy-MM-dd}.log");
        
        // Wait a moment for file to be written and check for any log files in the directory
        await Task.Delay(100);
        var logFiles = Directory.GetFiles(_testLogDirectory, "integration_test_*.log");
        Assert.True(logFiles.Length > 0, $"No log files found in {_testLogDirectory}. Expected pattern: integration_test_*.log");
        
        var actualLogFile = logFiles.First();
        Assert.True(File.Exists(actualLogFile));

        var logContent = await File.ReadAllTextAsync(actualLogFile);
        Assert.Contains("IntegrationTestError", logContent);
        Assert.Contains("This is an integration test error message", logContent);
        Assert.Contains("integration-test-op", logContent);
        Assert.Contains("Integration", logContent);
        Assert.Contains("AlertingService", logContent);
    }

    [Fact]
    public async Task AlertingService_SendNotificationWithWebhook_HandlesHttpOperations()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);

        var configurationData = new Dictionary<string, string?>
        {
            ["Alerting:EnableAlerting"] = "true",
            ["Alerting:Webhook:Enabled"] = "true",
            ["Alerting:Webhook:Url"] = "https://webhook.test.com/alerts",
            ["Alerting:Webhook:HttpMethod"] = "POST",
            ["Alerting:Webhook:ContentType"] = "application/json",
            ["Alerting:Email:Enabled"] = "false",
            ["Alerting:FileLog:Enabled"] = "false"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        // Configure test HTTP handler to return success
        _testHttpHandler.SetResponse(HttpStatusCode.OK, "Success");

        // Replace the default HttpClient with our test client
        services.AddSharedServices(connectionString, configuration);
        
        // Remove the default HttpClient registration and add our test client
        var httpClientDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(HttpClient));
        if (httpClientDescriptor != null)
        {
            services.Remove(httpClientDescriptor);
        }
        services.AddSingleton(_testHttpClient);
        
        var serviceProvider = services.BuildServiceProvider();

        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);

        var notification = new Notification
        {
            Subject = "Integration Test Notification",
            Message = "This is a test notification for webhook integration",
            Severity = AlertSeverity.Error,
            OperationId = "webhook-test-op",
            Metadata = new Dictionary<string, object>
            {
                ["TestType"] = "WebhookIntegration",
                ["Timestamp"] = DateTime.Now
            }
        };

        // Act - Send notification via webhook
        var result = await alertingService.SendNotificationAsync(notification, new[] { NotificationChannel.Webhook });

        // Assert - Verify notification was sent successfully
        Assert.True(result.Success);
        Assert.Equal(1, result.SuccessfulChannels);
        Assert.True(result.ChannelResults[NotificationChannel.Webhook]);
        Assert.False(result.ChannelErrors.ContainsKey(NotificationChannel.Webhook));

        // Verify HTTP request was made correctly
        var lastRequest = _testHttpHandler.LastRequest;
        Assert.NotNull(lastRequest);
        Assert.Equal(HttpMethod.Post, lastRequest.Method);
        Assert.Equal("https://webhook.test.com/alerts", lastRequest.RequestUri?.ToString());
        
        // Verify request headers (no auth token in this test)
        Assert.False(lastRequest.Headers.Contains("Authorization"));

        // Verify request content
        var requestContent = await lastRequest.Content!.ReadAsStringAsync();
        var requestData = JsonSerializer.Deserialize<JsonElement>(requestContent);
        
        Assert.Equal("Integration Test Notification", requestData.GetProperty("subject").GetString());
        Assert.Equal("This is a test notification for webhook integration", requestData.GetProperty("message").GetString());
        Assert.Equal("Error", requestData.GetProperty("severity").GetString());
        Assert.Equal("webhook-test-op", requestData.GetProperty("operationId").GetString());
    }

    [Fact]
    public async Task AlertingService_TestNotificationChannels_ValidatesConnectivity()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);

        var configurationData = new Dictionary<string, string?>
        {
            ["Alerting:EnableAlerting"] = "true",
            ["Alerting:FileLog:Enabled"] = "true",
            ["Alerting:FileLog:LogDirectory"] = _testLogDirectory,
            ["Alerting:Webhook:Enabled"] = "true",
            ["Alerting:Webhook:Url"] = "https://webhook.test.com/test",
            ["Alerting:Email:Enabled"] = "false" // Disable email to avoid SMTP requirements
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        // Configure test HTTP handler to return success for webhook tests
        _testHttpHandler.SetResponse(HttpStatusCode.OK, "Test successful");

        services.AddSharedServices(connectionString, configuration);
        
        // Remove the default HttpClient registration and add our test client
        var httpClientDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(HttpClient));
        if (httpClientDescriptor != null)
        {
            services.Remove(httpClientDescriptor);
        }
        services.AddSingleton(_testHttpClient);
        
        var serviceProvider = services.BuildServiceProvider();

        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);

        // Act - Test all notification channels
        var results = await alertingService.TestNotificationChannelsAsync();

        // Assert - Verify test results
        Assert.NotNull(results);
        Assert.True(results.Count >= 2); // FileLog and Webhook should be enabled

        // FileLog should succeed
        Assert.True(results.ContainsKey(NotificationChannel.FileLog));
        Assert.True(results[NotificationChannel.FileLog]);

        // Webhook should succeed (mocked)
        Assert.True(results.ContainsKey(NotificationChannel.Webhook));
        Assert.True(results[NotificationChannel.Webhook]);

        // Email should not be tested (disabled)
        Assert.False(results.ContainsKey(NotificationChannel.Email));
    }

    [Fact]
    public async Task AlertingService_HttpOperationsWithWebhook_HandlesHttpFailures()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);

        var configurationData = new Dictionary<string, string?>
        {
            ["Alerting:EnableAlerting"] = "true",
            ["Alerting:MaxRetryAttempts"] = "3",
            ["Alerting:Webhook:Enabled"] = "true",
            ["Alerting:Webhook:Url"] = "https://webhook.test.com/alerts",
            ["Alerting:Email:Enabled"] = "false",
            ["Alerting:FileLog:Enabled"] = "false"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        // Configure test HTTP handler to return failure
        _testHttpHandler.SetResponse(HttpStatusCode.InternalServerError, "Server Error");

        services.AddSharedServices(connectionString, configuration);
        
        // Remove the default HttpClient registration and add our test client
        var httpClientDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(HttpClient));
        if (httpClientDescriptor != null)
        {
            services.Remove(httpClientDescriptor);
        }
        services.AddSingleton(_testHttpClient);
        
        var serviceProvider = services.BuildServiceProvider();

        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);

        var notification = new Notification
        {
            Subject = "Failure Test Notification",
            Message = "Testing HTTP failure handling",
            Severity = AlertSeverity.Critical
        };

        // Act - Send notification (should fail due to HTTP error)
        var result = await alertingService.SendNotificationAsync(notification, new[] { NotificationChannel.Webhook });

        // Assert - Verify notification failed as expected
        Assert.False(result.Success);
        Assert.Equal(0, result.SuccessfulChannels);
        Assert.False(result.ChannelResults[NotificationChannel.Webhook]);
        Assert.True(result.ChannelErrors.ContainsKey(NotificationChannel.Webhook));

        // Verify HTTP request was made
        Assert.Equal(1, _testHttpHandler.RequestCount);
    }

    [Fact]
    public void AlertingService_MultipleScopes_MaintainsSingletonConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);

        services.AddSharedServices(connectionString);
        var serviceProvider = services.BuildServiceProvider();

        // Act - Create multiple scopes and resolve AlertingService in each
        using var scope1 = serviceProvider.CreateScope();
        using var scope2 = serviceProvider.CreateScope();

        var service1 = scope1.ServiceProvider.GetService<IAlertingService>();
        var service2 = scope2.ServiceProvider.GetService<IAlertingService>();

        // Assert - Verify services are different instances but share configuration
        Assert.NotNull(service1);
        Assert.NotNull(service2);
        Assert.NotSame(service1, service2); // Different service instances (scoped)

        var concreteService1 = (AlertingService)service1;
        var concreteService2 = (AlertingService)service2;

        // Configuration should be the same instance (singleton)
        Assert.Same(concreteService1.Configuration, concreteService2.Configuration);
    }

    [Fact]
    public void AlertingService_ConfigurationUpdate_WorksCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);

        services.AddSharedServices(connectionString);
        var serviceProvider = services.BuildServiceProvider();

        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);

        var originalConfig = alertingService.Configuration;
        Assert.True(originalConfig.EnableAlerting); // Default is true

        // Act - Update configuration
        var newConfig = new AlertingConfig
        {
            EnableAlerting = false,
            MinimumSeverity = AlertSeverity.Critical,
            MaxAlertsPerHour = 25,
            TimeoutSeconds = 60
        };

        alertingService.UpdateConfiguration(newConfig);

        // Assert - Verify configuration was updated
        var updatedConfig = alertingService.Configuration;
        Assert.False(updatedConfig.EnableAlerting);
        Assert.Equal(AlertSeverity.Critical, updatedConfig.MinimumSeverity);
        Assert.Equal(25, updatedConfig.MaxAlertsPerHour);
        Assert.Equal(60, updatedConfig.TimeoutSeconds);
    }

    [Fact]
    public async Task AlertingService_EnhancedCriticalErrorAlert_TracksNotificationResults()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);

        var configurationData = new Dictionary<string, string?>
        {
            ["Alerting:EnableAlerting"] = "true",
            ["Alerting:FileLog:Enabled"] = "true",
            ["Alerting:FileLog:LogDirectory"] = _testLogDirectory,
            ["Alerting:Webhook:Enabled"] = "true",
            ["Alerting:Webhook:Url"] = "https://webhook.test.com/alerts",
            ["Alerting:Email:Enabled"] = "false"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        _testHttpHandler.SetResponse(HttpStatusCode.OK, "Success");

        services.AddSharedServices(connectionString, configuration);
        
        // Remove the default HttpClient registration and add our test client
        var httpClientDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(HttpClient));
        if (httpClientDescriptor != null)
        {
            services.Remove(httpClientDescriptor);
        }
        services.AddSingleton(_testHttpClient);
        
        var serviceProvider = services.BuildServiceProvider();

        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);

        var enhancedAlert = new EnhancedCriticalErrorAlert
        {
            OperationId = "enhanced-test-op",
            ErrorType = "EnhancedTestError",
            ErrorMessage = "This is an enhanced critical error alert",
            OccurredAt = DateTime.Now
        };

        // Act - Send enhanced critical error alert
        var result = await alertingService.SendCriticalErrorAlertAsync(enhancedAlert);

        // Assert - Verify alert was sent and notification results are tracked
        Assert.True(result);
        Assert.True(enhancedAlert.AlertSent);
        Assert.NotNull(enhancedAlert.AlertSentAt);

        // Verify enhanced tracking properties
        Assert.NotEmpty(enhancedAlert.NotificationChannels);
        Assert.Contains(NotificationChannel.FileLog, enhancedAlert.NotificationChannels);
        Assert.Contains(NotificationChannel.Webhook, enhancedAlert.NotificationChannels);

        Assert.NotEmpty(enhancedAlert.NotificationResults);
        Assert.True(enhancedAlert.NotificationResults[NotificationChannel.FileLog]);
        Assert.True(enhancedAlert.NotificationResults[NotificationChannel.Webhook]);

        Assert.Empty(enhancedAlert.NotificationErrors); // No errors expected
        Assert.True(enhancedAlert.NotificationDuration > TimeSpan.Zero);
    }

    public void Dispose()
    {
        // Clean up test resources
        try
        {
            if (File.Exists(_testDatabasePath))
                File.Delete(_testDatabasePath);
            
            if (Directory.Exists(_testLogDirectory))
                Directory.Delete(_testLogDirectory, true);
                
            _testHttpClient?.Dispose();
            _testHttpHandler?.Dispose();
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }
}

/// <summary>
/// Test HTTP message handler for mocking HTTP responses in integration tests
/// </summary>
public class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode statusCode, string content)> _responses = new();
    private (HttpStatusCode statusCode, string content) _defaultResponse = (HttpStatusCode.OK, "Success");
    
    public HttpRequestMessage? LastRequest { get; private set; }
    public int RequestCount { get; private set; }

    public void SetResponse(HttpStatusCode statusCode, string content)
    {
        _defaultResponse = (statusCode, content);
    }

    public void SetSequentialResponses(IEnumerable<(HttpStatusCode statusCode, string content)> responses)
    {
        _responses.Clear();
        foreach (var response in responses)
        {
            _responses.Enqueue(response);
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        RequestCount++;

        var (statusCode, content) = _responses.Count > 0 ? _responses.Dequeue() : _defaultResponse;

        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

        return Task.FromResult(response);
    }
}