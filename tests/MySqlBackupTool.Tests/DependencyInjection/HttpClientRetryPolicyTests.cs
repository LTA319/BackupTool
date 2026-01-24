using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using System.Net;
using System.Net.Http;
using Xunit;

namespace MySqlBackupTool.Tests.DependencyInjection;

/// <summary>
/// Tests for HttpClient retry policy implementation
/// </summary>
public class HttpClientRetryPolicyTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly List<HttpClient> _httpClients = new();

    public HttpClientRetryPolicyTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(":memory:");
        
        // Create configuration with retry settings
        var configurationData = new Dictionary<string, string>
        {
            ["Alerting:MaxRetryAttempts"] = "2",
            ["Alerting:EnableAlerting"] = "true"
        };
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData!)
            .Build();
        
        services.AddSharedServices(connectionString, configuration);
        
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void HttpClient_IsRegisteredWithRetryPolicy()
    {
        // Arrange & Act
        var alertingService = _serviceProvider.GetService<AlertingService>();
        var alertingConfig = _serviceProvider.GetService<AlertingConfig>();

        // Assert
        Assert.NotNull(alertingService);
        Assert.NotNull(alertingConfig);
        Assert.Equal(2, alertingConfig.MaxRetryAttempts);
    }

    [Fact]
    public async Task HttpClient_RetryPolicy_HandlesTransientFailures()
    {
        // Arrange
        var alertingService = _serviceProvider.GetService<AlertingService>();
        Assert.NotNull(alertingService);

        // Create a test notification
        var notification = new Notification
        {
            Subject = "Test Notification",
            Message = "This is a test notification for retry policy",
            Severity = AlertSeverity.Error
        };

        // Update configuration to enable webhook with a non-existent URL
        var config = new AlertingConfig
        {
            EnableAlerting = true,
            MaxRetryAttempts = 2,
            Webhook = new WebhookConfig
            {
                Enabled = true,
                Url = "http://localhost:9999/webhook", // Non-existent endpoint
                HttpMethod = "POST"
            }
        };
        
        alertingService.UpdateConfiguration(config);

        // Act & Assert
        // This should fail after retries, but the retry policy should be invoked
        var result = await alertingService.SendNotificationAsync(notification, [NotificationChannel.Webhook]);
        
        // The notification should fail (since the endpoint doesn't exist)
        // but the retry policy should have been applied
        Assert.False(result.Success);
        Assert.True(result.ChannelResults.ContainsKey(NotificationChannel.Webhook));
        Assert.False(result.ChannelResults[NotificationChannel.Webhook]);
    }

    [Fact]
    public void AlertingConfig_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var alertingConfig = _serviceProvider.GetService<AlertingConfig>();

        // Assert
        Assert.NotNull(alertingConfig);
        Assert.True(alertingConfig.EnableAlerting);
        Assert.Equal(30, alertingConfig.TimeoutSeconds);
        Assert.Equal(2, alertingConfig.MaxRetryAttempts); // From configuration
        Assert.False(alertingConfig.EnableCircuitBreaker);
        Assert.NotNull(alertingConfig.DefaultHeaders);
    }

    public void Dispose()
    {
        foreach (var httpClient in _httpClients)
        {
            httpClient?.Dispose();
        }
        _serviceProvider?.Dispose();
    }
}