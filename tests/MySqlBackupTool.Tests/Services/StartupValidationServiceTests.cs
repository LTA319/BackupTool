using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;

namespace MySqlBackupTool.Tests.Services;

public class StartupValidationServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly string _connectionString;

    public StartupValidationServiceTests()
    {
        _connectionString = "Data Source=:memory:";
        
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddSharedServices(_connectionString);
        
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task ValidateServicesAsync_WithValidConfiguration_ReturnsSuccessfulResult()
    {
        // Arrange
        var validationService = _serviceProvider.GetRequiredService<StartupValidationService>();

        // Act
        var result = await validationService.ValidateServicesAsync();

        // Assert
        Assert.True(result.IsValid, $"Service validation failed. Failed services: {string.Join(", ", result.FailedServices.Keys)}");
        Assert.Empty(result.FailedServices);
        Assert.NotEmpty(result.ValidatedServices);
        Assert.True(result.ValidationDuration > TimeSpan.Zero);
        Assert.Null(result.ValidationException);
        Assert.Equal(100.0, result.SuccessRate);
    }

    [Fact]
    public async Task ValidateServicesAsync_ValidatesHttpClientServices()
    {
        // Arrange
        var validationService = _serviceProvider.GetRequiredService<StartupValidationService>();

        // Act
        var result = await validationService.ValidateServicesAsync();

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains("IHttpClientFactory", result.ValidatedServices.Keys);
        Assert.Contains("AlertingConfig", result.ValidatedServices.Keys);
        Assert.Contains("IAlertingService", result.ValidatedServices.Keys);
        Assert.Contains("HttpClient for AlertingService", result.ValidatedServices.Keys);
    }

    [Fact]
    public async Task ValidateServicesAsync_ValidatesCoreServices()
    {
        // Arrange
        var validationService = _serviceProvider.GetRequiredService<StartupValidationService>();

        // Act
        var result = await validationService.ValidateServicesAsync();

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains("ILogger<T>", result.ValidatedServices.Keys);
        Assert.Contains("ILoggingService", result.ValidatedServices.Keys);
        Assert.Contains("BackupDbContext", result.ValidatedServices.Keys);
        Assert.Contains("DatabaseMigrationService", result.ValidatedServices.Keys);
        Assert.Contains("IMemoryProfiler", result.ValidatedServices.Keys);
        Assert.Contains("IEncryptionService", result.ValidatedServices.Keys);
        Assert.Contains("IValidationService", result.ValidatedServices.Keys);
    }

    [Fact]
    public async Task ValidateServicesAsync_ValidatesRepositoryServices()
    {
        // Arrange
        var validationService = _serviceProvider.GetRequiredService<StartupValidationService>();

        // Act
        var result = await validationService.ValidateServicesAsync();

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains("IBackupConfigurationRepository", result.ValidatedServices.Keys);
        Assert.Contains("IBackupLogRepository", result.ValidatedServices.Keys);
        Assert.Contains("IRetentionPolicyRepository", result.ValidatedServices.Keys);
        Assert.Contains("IResumeTokenRepository", result.ValidatedServices.Keys);
        Assert.Contains("IScheduleConfigurationRepository", result.ValidatedServices.Keys);
    }

    [Fact]
    public async Task ValidateServicesAsync_ValidatesBusinessServices()
    {
        // Arrange
        var validationService = _serviceProvider.GetRequiredService<StartupValidationService>();

        // Act
        var result = await validationService.ValidateServicesAsync();

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains("IBackupLogService", result.ValidatedServices.Keys);
        Assert.Contains("BackupReportingService", result.ValidatedServices.Keys);
        Assert.Contains("IRetentionPolicyService", result.ValidatedServices.Keys);
        Assert.Contains("RetentionManagementService", result.ValidatedServices.Keys);
        Assert.Contains("INetworkRetryService", result.ValidatedServices.Keys);
        Assert.Contains("IAuthenticationService", result.ValidatedServices.Keys);
        Assert.Contains("IAuthorizationService", result.ValidatedServices.Keys);
        Assert.Contains("IErrorRecoveryManager", result.ValidatedServices.Keys);
    }

    [Fact]
    public async Task ValidateServicesAsync_WithConfiguration_LogsConfigurationDetails()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Alerting:EnableAlerting"] = "true",
                ["Alerting:TimeoutSeconds"] = "45",
                ["Alerting:MaxRetryAttempts"] = "5",
                ["Alerting:BaseUrl"] = "https://api.example.com",
                ["Alerting:Email:Enabled"] = "true",
                ["Alerting:Webhook:Enabled"] = "false",
                ["Alerting:FileLog:Enabled"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddSharedServices(_connectionString, configuration);
        
        using var serviceProvider = services.BuildServiceProvider();
        var validationService = serviceProvider.GetRequiredService<StartupValidationService>();

        // Act
        var result = await validationService.ValidateServicesAsync();

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains("AlertingConfig", result.ValidatedServices.Keys);
        
        // Verify configuration was applied
        var alertingConfig = serviceProvider.GetRequiredService<AlertingConfig>();
        Assert.True(alertingConfig.EnableAlerting);
        Assert.Equal(45, alertingConfig.TimeoutSeconds);
        Assert.Equal(5, alertingConfig.MaxRetryAttempts);
        Assert.Equal("https://api.example.com", alertingConfig.BaseUrl);
        Assert.True(alertingConfig.Email.Enabled);
        Assert.False(alertingConfig.Webhook.Enabled);
        Assert.True(alertingConfig.FileLog.Enabled);
    }

    [Fact]
    public async Task ServiceProviderExtension_ValidateServicesAsync_ReturnsValidationResult()
    {
        // Act
        var result = await _serviceProvider.ValidateServicesAsync();

        // Assert
        Assert.True(result.IsValid);
        Assert.NotEmpty(result.ValidatedServices);
        Assert.Empty(result.FailedServices);
    }

    [Fact]
    public async Task ServiceProviderExtension_ValidateServicesOrThrowAsync_DoesNotThrowWhenValid()
    {
        // Act & Assert - Should not throw
        await _serviceProvider.ValidateServicesOrThrowAsync();
    }

    [Fact]
    public async Task ServiceProviderExtension_ValidateServicesOrThrowAsync_ThrowsWhenInvalid()
    {
        // Arrange - Create a service provider with missing services
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        // Intentionally not adding shared services to cause validation failure
        
        using var invalidServiceProvider = services.BuildServiceProvider();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => invalidServiceProvider.ValidateServicesOrThrowAsync());
        
        Assert.Contains("Service validation failed", exception.Message);
    }

    [Fact]
    public void StartupValidationResult_CalculatesSuccessRateCorrectly()
    {
        // Arrange
        var result = new StartupValidationResult();
        result.ValidatedServices["Service1"] = "Success";
        result.ValidatedServices["Service2"] = "Success";
        result.FailedServices["Service3"] = "Failed";

        // Act & Assert
        Assert.Equal(3, result.TotalServicesChecked);
        Assert.Equal(66.67, Math.Round(result.SuccessRate, 2));
    }

    [Fact]
    public void StartupValidationResult_EmptyResult_HasZeroSuccessRate()
    {
        // Arrange
        var result = new StartupValidationResult();

        // Act & Assert
        Assert.Equal(0, result.TotalServicesChecked);
        Assert.Equal(0.0, result.SuccessRate);
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}