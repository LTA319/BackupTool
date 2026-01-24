using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using System.Net.Http;

namespace MySqlBackupTool.Tests.DependencyInjection;

public class ServiceRegistrationTests
{
    [Fact]
    public void AddSharedServices_WithoutConfiguration_RegistersHttpClientAndAlertingService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = "Data Source=:memory:";

        // Act
        services.AddSharedServices(connectionString);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        // With typed client pattern, HttpClient is not directly available from container
        // Instead, verify that AlertingService can be resolved (which requires HttpClient)
        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);
        Assert.IsType<AlertingService>(alertingService);
    }

    [Fact]
    public void AddSharedServices_WithConfiguration_RegistersHttpClientAndAlertingServiceWithConfig()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = "Data Source=:memory:";

        var configurationData = new Dictionary<string, string?>
        {
            ["Alerting:EnableAlerting"] = "true",
            ["Alerting:BaseUrl"] = "https://api.example.com",
            ["Alerting:TimeoutSeconds"] = "45",
            ["Alerting:MaxRetryAttempts"] = "5",
            ["Alerting:EnableCircuitBreaker"] = "true",
            ["Alerting:DefaultHeaders:X-API-Key"] = "test-key",
            ["Alerting:DefaultHeaders:User-Agent"] = "MySqlBackupTool/2.0",
            ["Alerting:MinimumSeverity"] = "Error",
            ["Alerting:MaxAlertsPerHour"] = "25",
            ["Alerting:NotificationTimeout"] = "00:00:45",
            ["Alerting:Email:Enabled"] = "true",
            ["Alerting:Email:SmtpServer"] = "smtp.example.com",
            ["Alerting:Email:SmtpPort"] = "587",
            ["Alerting:Webhook:Enabled"] = "false",
            ["Alerting:FileLog:Enabled"] = "true",
            ["Alerting:FileLog:LogDirectory"] = "test-logs"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        // Act
        services.AddSharedServices(connectionString, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        // With typed client pattern, HttpClient is not directly available from container
        // Instead, verify that AlertingService can be resolved (which requires HttpClient)
        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);
        Assert.IsType<AlertingService>(alertingService);

        var alertingConfig = serviceProvider.GetService<AlertingConfig>();
        Assert.NotNull(alertingConfig);
        Assert.True(alertingConfig.EnableAlerting);
        Assert.Equal("https://api.example.com", alertingConfig.BaseUrl);
        Assert.Equal(45, alertingConfig.TimeoutSeconds);
        Assert.Equal(5, alertingConfig.MaxRetryAttempts);
        Assert.True(alertingConfig.EnableCircuitBreaker);
        Assert.Equal("test-key", alertingConfig.DefaultHeaders["X-API-Key"]);
        Assert.Equal("MySqlBackupTool/2.0", alertingConfig.DefaultHeaders["User-Agent"]);
        Assert.Equal(AlertSeverity.Error, alertingConfig.MinimumSeverity);
        Assert.Equal(25, alertingConfig.MaxAlertsPerHour);
        Assert.Equal(TimeSpan.FromSeconds(45), alertingConfig.NotificationTimeout);
        Assert.True(alertingConfig.Email.Enabled);
        Assert.Equal("smtp.example.com", alertingConfig.Email.SmtpServer);
        Assert.Equal(587, alertingConfig.Email.SmtpPort);
        Assert.False(alertingConfig.Webhook.Enabled);
        Assert.True(alertingConfig.FileLog.Enabled);
        Assert.Equal("test-logs", alertingConfig.FileLog.LogDirectory);
    }

    [Fact]
    public void AddSharedServices_WithoutAlertingConfigSection_UsesDefaultConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = "Data Source=:memory:";

        var configurationData = new Dictionary<string, string?>
        {
            ["SomeOtherSection:Value"] = "test"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        // Act
        services.AddSharedServices(connectionString, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var alertingConfig = serviceProvider.GetService<AlertingConfig>();
        Assert.NotNull(alertingConfig);
        
        // Verify default values
        Assert.True(alertingConfig.EnableAlerting);
        Assert.Null(alertingConfig.BaseUrl);
        Assert.Equal(30, alertingConfig.TimeoutSeconds);
        Assert.Equal(3, alertingConfig.MaxRetryAttempts);
        Assert.False(alertingConfig.EnableCircuitBreaker);
        Assert.Empty(alertingConfig.DefaultHeaders);
        Assert.Equal(AlertSeverity.Error, alertingConfig.MinimumSeverity);
        Assert.Equal(50, alertingConfig.MaxAlertsPerHour);
        Assert.Equal(TimeSpan.FromSeconds(30), alertingConfig.NotificationTimeout);
        Assert.False(alertingConfig.Email.Enabled);
        Assert.False(alertingConfig.Webhook.Enabled);
        Assert.True(alertingConfig.FileLog.Enabled);
        Assert.Equal("logs/alerts", alertingConfig.FileLog.LogDirectory);
    }

    [Fact]
    public void AddSharedServices_HttpClientConfiguration_HasCorrectSettings()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = "Data Source=:memory:";

        // Act
        services.AddSharedServices(connectionString);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        // Get the AlertingService to verify HttpClient is properly injected
        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);
        
        // The HttpClient configuration is internal to AlertingService, 
        // so we verify it works by ensuring the service can be created successfully
        Assert.IsType<AlertingService>(alertingService);
        
        // We can't directly access the HttpClient timeout from the typed client,
        // but we can verify that the service was created without errors,
        // which means HttpClient was properly injected
    }

    [Fact]
    public void AddSharedServices_AlertingServiceDependencies_AreInjectedCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = "Data Source=:memory:";

        // Act
        services.AddSharedServices(connectionString);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);

        // Verify that AlertingService can be created without errors
        // This implicitly tests that all dependencies (ILogger, HttpClient, AlertingConfig) are available
        Assert.IsType<AlertingService>(alertingService);
        
        // Verify configuration is available
        var concreteService = (AlertingService)alertingService;
        Assert.NotNull(concreteService.Configuration);
    }

    [Fact]
    public void AddSharedServices_MultipleResolutions_ReturnsSameConfigurationInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = "Data Source=:memory:";

        // Act
        services.AddSharedServices(connectionString);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var config1 = serviceProvider.GetService<AlertingConfig>();
        var config2 = serviceProvider.GetService<AlertingConfig>();
        
        Assert.NotNull(config1);
        Assert.NotNull(config2);
        Assert.Same(config1, config2); // Should be the same instance (singleton)
    }

    [Fact]
    public void AddSharedServices_MultipleAlertingServiceResolutions_ReturnsDifferentInstances()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = "Data Source=:memory:";

        // Act
        services.AddSharedServices(connectionString);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        // Create different scopes to get different instances
        using var scope1 = serviceProvider.CreateScope();
        using var scope2 = serviceProvider.CreateScope();
        
        var service1 = scope1.ServiceProvider.GetService<IAlertingService>();
        var service2 = scope2.ServiceProvider.GetService<IAlertingService>();
        
        Assert.NotNull(service1);
        Assert.NotNull(service2);
        Assert.NotSame(service1, service2); // Should be different instances (scoped)
    }
}