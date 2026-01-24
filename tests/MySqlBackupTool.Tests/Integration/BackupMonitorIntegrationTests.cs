using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;

namespace MySqlBackupTool.Tests.Integration;

public class BackupMonitorIntegrationTests
{
    [Fact]
    public void BackupMonitor_CanResolveAlertingService_WithoutDependencyInjectionErrors()
    {
        // Arrange - Simulate the client application setup
        var services = new ServiceCollection();
        services.AddLogging();
        
        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(":memory:");
        
        // This simulates how the client application would set up services
        services.AddSharedServices(connectionString);
        
        var serviceProvider = services.BuildServiceProvider();

        // Act - This simulates what happens when Backup Monitor tries to access AlertingService
        var alertingService = serviceProvider.GetService<IAlertingService>();

        // Assert - Verify that AlertingService can be resolved without errors
        Assert.NotNull(alertingService);
        Assert.IsType<AlertingService>(alertingService);
        
        // Verify that the service is functional by checking its configuration
        var concreteService = (AlertingService)alertingService;
        Assert.NotNull(concreteService.Configuration);
    }

    [Fact]
    public void BackupMonitor_CanResolveAlertingService_WithCustomConfiguration()
    {
        // Arrange - Simulate the client application setup with custom configuration
        var services = new ServiceCollection();
        services.AddLogging();
        
        var configurationData = new Dictionary<string, string?>
        {
            ["Alerting:EnableAlerting"] = "true",
            ["Alerting:MinimumSeverity"] = "Warning",
            ["Alerting:MaxAlertsPerHour"] = "100",
            ["Alerting:FileLog:Enabled"] = "true",
            ["Alerting:FileLog:LogDirectory"] = "backup-monitor-logs"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();
        
        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(":memory:");
        
        // This simulates how the client application would set up services with configuration
        services.AddSharedServices(connectionString, configuration);
        
        var serviceProvider = services.BuildServiceProvider();

        // Act - This simulates what happens when Backup Monitor tries to access AlertingService
        var alertingService = serviceProvider.GetService<IAlertingService>();

        // Assert - Verify that AlertingService can be resolved without errors
        Assert.NotNull(alertingService);
        Assert.IsType<AlertingService>(alertingService);
        
        // Verify that the custom configuration was applied
        var concreteService = (AlertingService)alertingService;
        Assert.NotNull(concreteService.Configuration);
        Assert.True(concreteService.Configuration.EnableAlerting);
        Assert.Equal(AlertSeverity.Warning, concreteService.Configuration.MinimumSeverity);
        Assert.Equal(100, concreteService.Configuration.MaxAlertsPerHour);
        Assert.Equal("backup-monitor-logs", concreteService.Configuration.FileLog.LogDirectory);
    }

    [Fact]
    public async Task BackupMonitor_AlertingService_CanTestChannels()
    {
        // Arrange - Simulate the client application setup
        var services = new ServiceCollection();
        services.AddLogging();
        
        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(":memory:");
        services.AddSharedServices(connectionString);
        
        var serviceProvider = services.BuildServiceProvider();
        var alertingService = serviceProvider.GetService<IAlertingService>();

        // Act - This simulates testing notification channels from Backup Monitor
        var testResults = await alertingService!.TestNotificationChannelsAsync();

        // Assert - Verify that the test completes without errors
        Assert.NotNull(testResults);
        // The actual results depend on configuration, but the important thing is no exceptions
    }
}