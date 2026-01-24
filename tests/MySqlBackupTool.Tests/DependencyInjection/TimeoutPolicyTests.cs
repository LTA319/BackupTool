using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using System.Net.Http;
using Xunit;

namespace MySqlBackupTool.Tests.DependencyInjection;

/// <summary>
/// Tests for HTTP timeout policy configuration
/// </summary>
public class TimeoutPolicyTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public TimeoutPolicyTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(":memory:");

        // Create configuration with custom timeout values
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Alerting:TimeoutSeconds"] = "60",
                ["Alerting:MaxRetryAttempts"] = "2"
            })
            .Build();

        services.AddSharedServices(connectionString, configuration);

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void AddSharedServices_WithTimeoutConfiguration_ConfiguresHttpClientWithCorrectTimeout()
    {
        // Arrange & Act
        var alertingConfig = _serviceProvider.GetRequiredService<AlertingConfig>();
        var httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(nameof(AlertingService));

        // Assert
        Assert.Equal(60, alertingConfig.TimeoutSeconds);
        Assert.Equal(TimeSpan.FromSeconds(60), httpClient.Timeout);
    }

    [Fact]
    public void AddSharedServices_WithoutConfiguration_UsesDefaultTimeout()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(":memory:");

        // Act - No configuration provided
        services.AddSharedServices(connectionString);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var alertingConfig = serviceProvider.GetRequiredService<AlertingConfig>();
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(nameof(AlertingService));

        Assert.Equal(30, alertingConfig.TimeoutSeconds); // Default value
        Assert.Equal(TimeSpan.FromSeconds(30), httpClient.Timeout);

        serviceProvider.Dispose();
    }

    [Fact]
    public void AddSharedServices_WithConfiguration_RegistersAlertingServiceWithDependencies()
    {
        // Arrange & Act
        var alertingService = _serviceProvider.GetRequiredService<IAlertingService>();

        // Assert
        Assert.NotNull(alertingService);
        Assert.IsType<AlertingService>(alertingService);
    }

    [Fact]
    public void AlertingConfig_WithConfiguration_BindsCorrectValues()
    {
        // Arrange & Act
        var alertingConfig = _serviceProvider.GetRequiredService<AlertingConfig>();

        // Assert
        Assert.Equal(60, alertingConfig.TimeoutSeconds);
        Assert.Equal(2, alertingConfig.MaxRetryAttempts);
        Assert.True(alertingConfig.EnableAlerting);
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}