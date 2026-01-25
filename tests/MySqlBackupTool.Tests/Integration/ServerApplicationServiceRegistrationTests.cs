using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using System.Net.Http;

namespace MySqlBackupTool.Tests.Integration;

/// <summary>
/// Tests for service registration in server application context
/// Validates Requirements 5.2, 5.4: Cross-application compatibility and no conflicts with existing services
/// </summary>
public class ServerApplicationServiceRegistrationTests : IDisposable
{
    private readonly string _testDatabasePath;
    private readonly string _testStoragePath;

    public ServerApplicationServiceRegistrationTests()
    {
        _testDatabasePath = Path.Combine(Path.GetTempPath(), $"test_server_services_{Guid.NewGuid()}.db");
        _testStoragePath = Path.Combine(Path.GetTempPath(), $"test_server_storage_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testStoragePath);
    }

    [Fact]
    public void ServerApplication_HttpClientAndAlertingService_ResolveCorrectlyWithoutConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);

        // Act - Configure services as in server application
        services.AddSharedServices(connectionString);
        services.AddServerServices(_testStoragePath);
        
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Verify HttpClient and AlertingService resolve correctly
        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);
        Assert.IsType<AlertingService>(alertingService);

        var alertingConfig = serviceProvider.GetService<AlertingConfig>();
        Assert.NotNull(alertingConfig);
        
        // Verify AlertingService has all required dependencies injected
        var concreteService = (AlertingService)alertingService;
        Assert.NotNull(concreteService.Configuration);
        
        // Verify default configuration values are applied
        Assert.True(alertingConfig.EnableAlerting);
        Assert.Equal(30, alertingConfig.TimeoutSeconds);
        Assert.Equal(3, alertingConfig.MaxRetryAttempts);
    }

    [Fact]
    public void ServerApplication_HttpClientAndAlertingService_ResolveCorrectlyWithConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);

        var configurationData = new Dictionary<string, string?>
        {
            ["Alerting:EnableAlerting"] = "true",
            ["Alerting:BaseUrl"] = "https://server-api.example.com",
            ["Alerting:TimeoutSeconds"] = "60",
            ["Alerting:MaxRetryAttempts"] = "4",
            ["Alerting:MaxAlertsPerHour"] = "100",
            ["Alerting:Email:Enabled"] = "true",
            ["Alerting:Email:SmtpServer"] = "smtp.server.com",
            ["Alerting:Webhook:Enabled"] = "true",
            ["Alerting:Webhook:Url"] = "https://webhook.server.com/alerts"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        // Act - Configure services as in server application with configuration
        services.AddSharedServices(connectionString, configuration);
        services.AddServerServices(_testStoragePath);
        
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Verify HttpClient and AlertingService resolve correctly with configuration
        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);
        Assert.IsType<AlertingService>(alertingService);

        var alertingConfig = serviceProvider.GetService<AlertingConfig>();
        Assert.NotNull(alertingConfig);
        
        // Verify configuration values are applied correctly
        Assert.True(alertingConfig.EnableAlerting);
        Assert.Equal("https://server-api.example.com", alertingConfig.BaseUrl);
        Assert.Equal(60, alertingConfig.TimeoutSeconds);
        Assert.Equal(4, alertingConfig.MaxRetryAttempts);
        Assert.Equal(100, alertingConfig.MaxAlertsPerHour);
        Assert.True(alertingConfig.Email.Enabled);
        Assert.Equal("smtp.server.com", alertingConfig.Email.SmtpServer);
        Assert.True(alertingConfig.Webhook.Enabled);
        Assert.Equal("https://webhook.server.com/alerts", alertingConfig.Webhook.Url);
    }

    [Fact]
    public void ServerApplication_NoConflictsWithExistingServerServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);

        // Act - Configure services as in server application
        services.AddSharedServices(connectionString);
        services.AddServerServices(_testStoragePath);
        
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Verify all expected server services can be resolved without conflicts
        
        // Shared services (including HttpClient and AlertingService)
        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);
        
        var alertingConfig = serviceProvider.GetService<AlertingConfig>();
        Assert.NotNull(alertingConfig);
        
        var networkRetryService = serviceProvider.GetService<INetworkRetryService>();
        Assert.NotNull(networkRetryService);
        
        // Server-specific services
        var fileReceiver = serviceProvider.GetService<IFileReceiver>();
        Assert.NotNull(fileReceiver);
        
        var chunkManager = serviceProvider.GetService<IChunkManager>();
        Assert.NotNull(chunkManager);
        
        var storageManager = serviceProvider.GetService<IStorageManager>();
        Assert.NotNull(storageManager);
        
        var checksumService = serviceProvider.GetService<IChecksumService>();
        Assert.NotNull(checksumService);
        
        // Database services
        var backupLogService = serviceProvider.GetService<IBackupLogService>();
        Assert.NotNull(backupLogService);
        
        var retentionPolicyService = serviceProvider.GetService<IRetentionPolicyService>();
        Assert.NotNull(retentionPolicyService);
        
        // Verify no service registration conflicts by ensuring all services are of expected types
        Assert.IsType<AlertingService>(alertingService);
        Assert.IsType<NetworkRetryService>(networkRetryService);
        Assert.IsType<ChunkManager>(chunkManager);
        Assert.IsType<ChecksumService>(checksumService);
        Assert.IsType<BackupLogService>(backupLogService);
        Assert.IsType<RetentionManagementService>(retentionPolicyService);
    }

    [Fact]
    public void ServerApplication_AlertingServiceDependencyInjection_IsComplete()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);

        // Act - Configure services as in server application
        services.AddSharedServices(connectionString);
        services.AddServerServices(_testStoragePath);
        
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Verify AlertingService has all required dependencies injected
        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);
        
        var concreteService = (AlertingService)alertingService;
        
        // Verify ILogger dependency is injected (implicit - service creation would fail without it)
        Assert.NotNull(concreteService);
        
        // Verify HttpClient dependency is injected (implicit - service creation would fail without it)
        // We can't directly access the HttpClient, but the service creation success indicates it's injected
        
        // Verify AlertingConfig dependency is injected
        Assert.NotNull(concreteService.Configuration);
        Assert.IsType<AlertingConfig>(concreteService.Configuration);
    }

    [Fact]
    public void ServerApplication_MultipleServiceResolutions_WorkConsistently()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);

        // Act - Configure services as in server application
        services.AddSharedServices(connectionString);
        services.AddServerServices(_testStoragePath);
        
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Verify multiple resolutions work consistently
        
        // AlertingConfig should be singleton - same instance each time
        var config1 = serviceProvider.GetService<AlertingConfig>();
        var config2 = serviceProvider.GetService<AlertingConfig>();
        Assert.NotNull(config1);
        Assert.NotNull(config2);
        Assert.Same(config1, config2);
        
        // AlertingService should be scoped - different instances in different scopes
        using var scope1 = serviceProvider.CreateScope();
        using var scope2 = serviceProvider.CreateScope();
        
        var service1 = scope1.ServiceProvider.GetService<IAlertingService>();
        var service2 = scope2.ServiceProvider.GetService<IAlertingService>();
        
        Assert.NotNull(service1);
        Assert.NotNull(service2);
        Assert.NotSame(service1, service2);
        
        // But both should have the same configuration instance
        var concreteService1 = (AlertingService)service1;
        var concreteService2 = (AlertingService)service2;
        Assert.Same(concreteService1.Configuration, concreteService2.Configuration);
    }

    [Fact]
    public void ServerApplication_ServiceRegistration_HandlesInvalidConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);

        var configurationData = new Dictionary<string, string?>
        {
            ["Alerting:TimeoutSeconds"] = "-10", // Invalid negative value
            ["Alerting:MaxRetryAttempts"] = "50", // Invalid high value
            ["Alerting:BaseUrl"] = "not-a-valid-url", // Invalid URL
            ["Alerting:Email:Enabled"] = "true", // Enable email to trigger validation
            ["Alerting:Email:SmtpPort"] = "99999" // Invalid port
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        // Act - Configure services with invalid configuration
        services.AddSharedServices(connectionString, configuration);
        services.AddServerServices(_testStoragePath);
        
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Verify services still resolve with corrected/default values
        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);
        
        var alertingConfig = serviceProvider.GetService<AlertingConfig>();
        Assert.NotNull(alertingConfig);
        
        // Verify invalid values are corrected to defaults/valid ranges
        Assert.Equal(30, alertingConfig.TimeoutSeconds); // Should be corrected to default 30
        Assert.Equal(10, alertingConfig.MaxRetryAttempts); // Should be capped at 10
        Assert.Null(alertingConfig.BaseUrl); // Should be cleared due to invalid URL
        // Email port should be corrected to default 587
        Assert.Equal(587, alertingConfig.Email.SmtpPort);
    }

    [Fact]
    public void ServerApplication_ServiceValidation_PassesForAllCriticalServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);

        // Act - Configure services as in server application
        services.AddSharedServices(connectionString);
        services.AddServerServices(_testStoragePath);
        
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Verify critical dependency validation passes
        var validationResult = serviceProvider.ValidateCriticalDependencies();
        
        Assert.True(validationResult.IsValid, 
            $"Critical dependency validation failed. Failed services: {string.Join(", ", validationResult.FailedServices.Keys)}");
        
        Assert.Empty(validationResult.FailedServices);
        Assert.Contains("IAlertingService", validationResult.ValidServices);
        
        // Verify specific services that are critical for all applications (not server-specific)
        var criticalServices = new[]
        {
            "IAlertingService",
            "AlertingConfig",
            "IHttpClientFactory",
            "BackupDbContext",
            "ILogger<T>",
            "ILoggingService"
        };
        
        foreach (var serviceName in criticalServices)
        {
            Assert.Contains(serviceName, validationResult.ValidServices);
        }
    }

    [Fact]
    public void ServerApplication_HttpClientConfiguration_IsAppliedCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);

        var configurationData = new Dictionary<string, string?>
        {
            ["Alerting:TimeoutSeconds"] = "45",
            ["Alerting:BaseUrl"] = "https://api.server.com",
            ["Alerting:DefaultHeaders:X-Server-Token"] = "server-token-123",
            ["Alerting:DefaultHeaders:X-Environment"] = "production"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        // Act - Configure services with HttpClient configuration
        services.AddSharedServices(connectionString, configuration);
        services.AddServerServices(_testStoragePath);
        
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Verify AlertingService can be created (indicating HttpClient is properly configured)
        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);
        
        var alertingConfig = serviceProvider.GetService<AlertingConfig>();
        Assert.NotNull(alertingConfig);
        
        // Verify configuration values that affect HttpClient
        Assert.Equal(45, alertingConfig.TimeoutSeconds);
        Assert.Equal("https://api.server.com", alertingConfig.BaseUrl);
        Assert.Equal("server-token-123", alertingConfig.DefaultHeaders["X-Server-Token"]);
        Assert.Equal("production", alertingConfig.DefaultHeaders["X-Environment"]);
        
        // Verify the service can be used (indicating HttpClient is functional)
        var concreteService = (AlertingService)alertingService;
        Assert.NotNull(concreteService.Configuration);
    }

    public void Dispose()
    {
        // Clean up test files
        try
        {
            if (File.Exists(_testDatabasePath))
                File.Delete(_testDatabasePath);
            
            if (Directory.Exists(_testStoragePath))
                Directory.Delete(_testStoragePath, true);
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }
}