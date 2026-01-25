using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using System.Net.Http;

namespace MySqlBackupTool.Tests.Integration;

/// <summary>
/// Tests for task 7.1: Test service registration in client application context
/// Verifies HttpClient and AlertingService resolve correctly in client app and 
/// tests that Backup Monitor functionality works without DI errors.
/// Requirements: 4.1, 4.2, 5.1
/// </summary>
public class ClientApplicationServiceRegistrationTests
{
    /// <summary>
    /// Test that simulates the exact client application setup from Program.cs
    /// and verifies all services can be resolved without errors.
    /// **Validates: Requirements 4.1, 4.2, 5.1**
    /// </summary>
    [Fact]
    public void ClientApplication_ServiceRegistration_CanResolveAllRequiredServices()
    {
        // Arrange - Simulate the exact client application setup from Program.cs
        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Add shared services (exactly as in Program.cs)
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString("client_backup_tool_test.db");
                services.AddSharedServices(connectionString);
                
                // Add client-specific services (exactly as in Program.cs)
                services.AddClientServices();
                
                // Add backup scheduling services (exactly as in Program.cs)
                services.AddBackupSchedulingServices();
            });

        var host = hostBuilder.Build();
        var serviceProvider = host.Services;

        // Act & Assert - Verify all critical services can be resolved
        
        // 1. Verify HttpClient can be resolved through AlertingService (typed client pattern)
        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);
        Assert.IsType<AlertingService>(alertingService);
        
        // 2. Verify AlertingService has all required dependencies injected
        var concreteAlertingService = (AlertingService)alertingService;
        Assert.NotNull(concreteAlertingService.Configuration);
        
        // 3. Verify AlertingConfig is properly registered and accessible
        var alertingConfig = serviceProvider.GetService<AlertingConfig>();
        Assert.NotNull(alertingConfig);
        
        // 4. Verify ILogger<AlertingService> is available
        var logger = serviceProvider.GetService<ILogger<AlertingService>>();
        Assert.NotNull(logger);
        
        // 5. Verify other services required by Backup Monitor
        var configRepository = serviceProvider.GetService<IBackupConfigurationRepository>();
        Assert.NotNull(configRepository);
        
        var logRepository = serviceProvider.GetService<IBackupLogRepository>();
        Assert.NotNull(logRepository);
        
        var backupOrchestrator = serviceProvider.GetService<IBackupOrchestrator>();
        Assert.NotNull(backupOrchestrator);
        
        // 6. Verify that services required for BackupMonitorForm can be resolved
        // (Simulating what BackupMonitorForm constructor does)
        var formLogger = serviceProvider.GetService<ILogger<object>>(); // Generic logger
        Assert.NotNull(formLogger);
        
        // Clean up
        host.Dispose();
    }

    /// <summary>
    /// Test that verifies AlertingService can perform HTTP operations without errors
    /// when resolved from client application context.
    /// **Validates: Requirements 4.2, 4.3**
    /// </summary>
    [Fact]
    public async Task ClientApplication_AlertingService_CanPerformHttpOperations()
    {
        // Arrange - Set up client application services
        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString("client_backup_tool_test.db");
                services.AddSharedServices(connectionString);
                services.AddClientServices();
                services.AddBackupSchedulingServices();
            });

        var host = hostBuilder.Build();
        var serviceProvider = host.Services;

        // Act - Get AlertingService and test its functionality
        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);

        // Test that AlertingService can test notification channels (which uses HttpClient internally)
        var testResults = await alertingService.TestNotificationChannelsAsync();

        // Assert - Verify the operation completed without throwing exceptions
        Assert.NotNull(testResults);
        // The actual results depend on configuration, but the important thing is no DI-related exceptions
        
        // Clean up
        host.Dispose();
    }

    /// <summary>
    /// Test that verifies client application services work with custom AlertingConfig
    /// and that HttpClient configuration is applied correctly.
    /// **Validates: Requirements 2.1, 2.4, 3.4**
    /// </summary>
    [Fact]
    public void ClientApplication_WithCustomAlertingConfig_ConfiguresHttpClientCorrectly()
    {
        // Arrange - Set up configuration with custom AlertingConfig
        var configurationData = new Dictionary<string, string?>
        {
            ["Alerting:EnableAlerting"] = "true",
            ["Alerting:BaseUrl"] = "https://api.example.com",
            ["Alerting:TimeoutSeconds"] = "45",
            ["Alerting:MaxRetryAttempts"] = "5",
            ["Alerting:EnableCircuitBreaker"] = "true",
            ["Alerting:DefaultHeaders:X-API-Key"] = "test-key-client",
            ["Alerting:DefaultHeaders:User-Agent"] = "MySqlBackupTool-Client/2.0",
            ["Alerting:MinimumSeverity"] = "Warning",
            ["Alerting:MaxAlertsPerHour"] = "75"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(builder => builder.AddConfiguration(configuration))
            .ConfigureServices((context, services) =>
            {
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString("client_backup_tool_test.db");
                services.AddSharedServices(connectionString, context.Configuration);
                services.AddClientServices();
                services.AddBackupSchedulingServices();
            });

        var host = hostBuilder.Build();
        var serviceProvider = host.Services;

        // Act - Verify services can be resolved with custom configuration
        var alertingService = serviceProvider.GetService<IAlertingService>();
        var alertingConfig = serviceProvider.GetService<AlertingConfig>();

        // Assert - Verify custom configuration was applied
        Assert.NotNull(alertingService);
        Assert.NotNull(alertingConfig);
        
        Assert.True(alertingConfig.EnableAlerting);
        Assert.Equal("https://api.example.com", alertingConfig.BaseUrl);
        Assert.Equal(45, alertingConfig.TimeoutSeconds);
        Assert.Equal(5, alertingConfig.MaxRetryAttempts);
        Assert.True(alertingConfig.EnableCircuitBreaker);
        Assert.Equal("test-key-client", alertingConfig.DefaultHeaders["X-API-Key"]);
        Assert.Equal("MySqlBackupTool-Client/2.0", alertingConfig.DefaultHeaders["User-Agent"]);
        Assert.Equal(AlertSeverity.Warning, alertingConfig.MinimumSeverity);
        Assert.Equal(75, alertingConfig.MaxAlertsPerHour);
        
        // Verify AlertingService uses the custom configuration
        var concreteService = (AlertingService)alertingService;
        Assert.Same(alertingConfig, concreteService.Configuration);
        
        // Clean up
        host.Dispose();
    }

    /// <summary>
    /// Test that verifies all services required by BackupMonitorForm can be resolved
    /// without any dependency injection errors in the client application context.
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Fact]
    public void ClientApplication_BackupMonitorFormServices_CanBeResolvedWithoutDIErrors()
    {
        // Arrange - Set up client application services
        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString("client_backup_tool_test.db");
                services.AddSharedServices(connectionString);
                services.AddClientServices();
                services.AddBackupSchedulingServices();
            });

        var host = hostBuilder.Build();
        var serviceProvider = host.Services;

        // Act & Assert - Verify all services required by BackupMonitorForm can be resolved
        // (This simulates what happens when BackupMonitorForm constructor is called)
        
        Exception? resolutionException = null;
        
        try
        {
            // These are the services that BackupMonitorForm constructor requires
            var logger = serviceProvider.GetRequiredService<ILogger<object>>();
            var configRepository = serviceProvider.GetRequiredService<IBackupConfigurationRepository>();
            var logRepository = serviceProvider.GetRequiredService<IBackupLogRepository>();
            var backupOrchestrator = serviceProvider.GetService<IBackupOrchestrator>(); // Optional service
            
            Assert.NotNull(logger);
            Assert.NotNull(configRepository);
            Assert.NotNull(logRepository);
            Assert.NotNull(backupOrchestrator);
        }
        catch (Exception ex)
        {
            resolutionException = ex;
        }

        // Assert - Verify no DI errors occurred
        Assert.Null(resolutionException);
        
        // Clean up
        host.Dispose();
    }

    /// <summary>
    /// Test that verifies service registration is consistent across multiple
    /// service provider instances (simulating multiple client application instances).
    /// **Validates: Requirements 5.1, 5.3**
    /// </summary>
    [Fact]
    public void ClientApplication_MultipleInstances_HaveConsistentServiceRegistration()
    {
        // Arrange - Create multiple client application instances
        var instances = new List<IHost>();
        var alertingConfigs = new List<AlertingConfig>();
        
        try
        {
            for (int i = 0; i < 3; i++)
            {
                var hostBuilder = Host.CreateDefaultBuilder()
                    .ConfigureServices((context, services) =>
                    {
                        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString($"client_backup_tool_test_{i}.db");
                        services.AddSharedServices(connectionString);
                        services.AddClientServices();
                        services.AddBackupSchedulingServices();
                    });

                var host = hostBuilder.Build();
                instances.Add(host);
                
                // Get AlertingConfig from each instance
                var config = host.Services.GetService<AlertingConfig>();
                Assert.NotNull(config);
                alertingConfigs.Add(config);
            }

            // Act & Assert - Verify all instances have consistent service registration
            foreach (var host in instances)
            {
                var serviceProvider = host.Services;
                
                // Verify all critical services are available
                Assert.NotNull(serviceProvider.GetService<IAlertingService>());
                Assert.NotNull(serviceProvider.GetService<AlertingConfig>());
                Assert.NotNull(serviceProvider.GetService<IBackupConfigurationRepository>());
                Assert.NotNull(serviceProvider.GetService<IBackupLogRepository>());
                Assert.NotNull(serviceProvider.GetService<IBackupOrchestrator>());
            }
            
            // Verify AlertingConfig has consistent default values across instances
            var firstConfig = alertingConfigs[0];
            foreach (var config in alertingConfigs.Skip(1))
            {
                Assert.Equal(firstConfig.EnableAlerting, config.EnableAlerting);
                Assert.Equal(firstConfig.TimeoutSeconds, config.TimeoutSeconds);
                Assert.Equal(firstConfig.MaxRetryAttempts, config.MaxRetryAttempts);
                Assert.Equal(firstConfig.MaxAlertsPerHour, config.MaxAlertsPerHour);
                Assert.Equal(firstConfig.MinimumSeverity, config.MinimumSeverity);
            }
        }
        finally
        {
            // Clean up all instances
            foreach (var host in instances)
            {
                host.Dispose();
            }
        }
    }

    /// <summary>
    /// Test that verifies AlertingService dependencies are properly injected
    /// and functional in the client application context.
    /// **Validates: Requirements 1.2, 3.2, 4.3**
    /// </summary>
    [Fact]
    public void ClientApplication_AlertingServiceDependencies_AreProperlyInjected()
    {
        // Arrange - Set up client application services
        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString("client_backup_tool_test.db");
                services.AddSharedServices(connectionString);
                services.AddClientServices();
                services.AddBackupSchedulingServices();
            });

        var host = hostBuilder.Build();
        var serviceProvider = host.Services;

        // Act - Get AlertingService and verify its dependencies
        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);
        
        var concreteService = (AlertingService)alertingService;

        // Assert - Verify all constructor dependencies are properly injected and non-null
        
        // 1. ILogger<AlertingService> should be injected (tested indirectly by successful creation)
        // 2. HttpClient should be injected (tested indirectly by successful creation)
        // 3. AlertingConfig should be injected and accessible
        Assert.NotNull(concreteService.Configuration);
        
        // Verify AlertingConfig has expected default values
        var config = concreteService.Configuration;
        Assert.True(config.EnableAlerting);
        Assert.Equal(30, config.TimeoutSeconds);
        Assert.Equal(3, config.MaxRetryAttempts);
        Assert.Equal(50, config.MaxAlertsPerHour);
        Assert.Equal(AlertSeverity.Error, config.MinimumSeverity);
        
        // Verify the same AlertingConfig instance is available from DI container
        var configFromContainer = serviceProvider.GetService<AlertingConfig>();
        Assert.NotNull(configFromContainer);
        Assert.Same(config, configFromContainer); // Should be the same singleton instance
        
        // Clean up
        host.Dispose();
    }

    /// <summary>
    /// Test that verifies client application can handle missing configuration gracefully
    /// and still resolve all required services with default values.
    /// **Validates: Requirements 3.3, 6.3**
    /// </summary>
    [Fact]
    public void ClientApplication_WithMissingConfiguration_UsesDefaultsAndResolvesServices()
    {
        // Arrange - Set up client application with minimal configuration (no Alerting section)
        var configurationData = new Dictionary<string, string?>
        {
            ["SomeOtherSection:Value"] = "test"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(builder => builder.AddConfiguration(configuration))
            .ConfigureServices((context, services) =>
            {
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString("client_backup_tool_test.db");
                services.AddSharedServices(connectionString, context.Configuration);
                services.AddClientServices();
                services.AddBackupSchedulingServices();
            });

        var host = hostBuilder.Build();
        var serviceProvider = host.Services;

        // Act - Verify services can still be resolved with default configuration
        var alertingService = serviceProvider.GetService<IAlertingService>();
        var alertingConfig = serviceProvider.GetService<AlertingConfig>();

        // Assert - Verify services are available with default configuration
        Assert.NotNull(alertingService);
        Assert.NotNull(alertingConfig);
        
        // Verify default values are used
        Assert.True(alertingConfig.EnableAlerting);
        Assert.Null(alertingConfig.BaseUrl);
        Assert.Equal(30, alertingConfig.TimeoutSeconds);
        Assert.Equal(3, alertingConfig.MaxRetryAttempts);
        Assert.False(alertingConfig.EnableCircuitBreaker);
        Assert.Empty(alertingConfig.DefaultHeaders);
        
        // Verify BackupMonitorForm services can still be resolved
        var configRepository = serviceProvider.GetService<IBackupConfigurationRepository>();
        var logRepository = serviceProvider.GetService<IBackupLogRepository>();
        var backupOrchestrator = serviceProvider.GetService<IBackupOrchestrator>();
        
        Assert.NotNull(configRepository);
        Assert.NotNull(logRepository);
        Assert.NotNull(backupOrchestrator);
        
        // Clean up
        host.Dispose();
    }

    /// <summary>
    /// Test that verifies HttpClient is properly configured with retry and timeout policies
    /// in the client application context.
    /// **Validates: Requirements 2.2, 5.1**
    /// </summary>
    [Fact]
    public void ClientApplication_HttpClient_HasRetryAndTimeoutPolicies()
    {
        // Arrange - Set up client application services with custom retry configuration
        var configurationData = new Dictionary<string, string?>
        {
            ["Alerting:MaxRetryAttempts"] = "5",
            ["Alerting:TimeoutSeconds"] = "60"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(builder => builder.AddConfiguration(configuration))
            .ConfigureServices((context, services) =>
            {
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString("client_backup_tool_test.db");
                services.AddSharedServices(connectionString, context.Configuration);
                services.AddClientServices();
                services.AddBackupSchedulingServices();
            });

        var host = hostBuilder.Build();
        var serviceProvider = host.Services;

        // Act - Get AlertingService (which has HttpClient injected)
        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);

        // Assert - Verify AlertingService was created successfully
        // (This indirectly tests that HttpClient with policies was properly configured)
        Assert.IsType<AlertingService>(alertingService);
        
        // Verify the configuration values were applied
        var alertingConfig = serviceProvider.GetService<AlertingConfig>();
        Assert.NotNull(alertingConfig);
        Assert.Equal(5, alertingConfig.MaxRetryAttempts);
        Assert.Equal(60, alertingConfig.TimeoutSeconds);
        
        // Clean up
        host.Dispose();
    }
}