using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;
using MySqlBackupTool.Shared.Services;
using Xunit;
using Xunit.Abstractions;

namespace MySqlBackupTool.Tests.DependencyInjection;

/// <summary>
/// Tests for dependency resolution error handling functionality
/// </summary>
public class DependencyResolutionErrorHandlingTests
{
    private readonly ITestOutputHelper _output;

    public DependencyResolutionErrorHandlingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ValidateCriticalDependencies_WithMissingHttpClient_ProvidesDetailedErrorMessage()
    {
        // Arrange - Create service collection without HttpClient registration
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        
        // Add AlertingConfig but not HttpClient (this will cause AlertingService to fail)
        services.AddSingleton<MySqlBackupTool.Shared.Models.AlertingConfig>();
        services.AddScoped<MySqlBackupTool.Shared.Interfaces.IAlertingService, MySqlBackupTool.Shared.Services.AlertingService>();
        
        // Add the dependency validator
        services.AddSingleton<DependencyResolutionValidator>();
        
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var result = serviceProvider.ValidateCriticalDependencies();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("IAlertingService", result.FailedServices.Keys);
        
        var alertingServiceError = result.FailedServices["IAlertingService"];
        Assert.Contains("HttpClient", alertingServiceError.ErrorSummary);
        Assert.Contains("AddHttpClient", alertingServiceError.ResolutionGuidance);
        
        _output.WriteLine("Error Details:");
        _output.WriteLine(alertingServiceError.DetailedError);
    }

    [Fact]
    public void ValidateCriticalDependencies_WithMissingAlertingConfig_ProvidesDetailedErrorMessage()
    {
        // Arrange - Create service collection without AlertingConfig registration
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddHttpClient<MySqlBackupTool.Shared.Services.AlertingService>();
        
        // Add AlertingService but not AlertingConfig (this will cause AlertingService to fail)
        services.AddScoped<MySqlBackupTool.Shared.Interfaces.IAlertingService, MySqlBackupTool.Shared.Services.AlertingService>();
        
        // Add the dependency validator
        services.AddSingleton<DependencyResolutionValidator>();
        
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var result = serviceProvider.ValidateCriticalDependencies();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("IAlertingService", result.FailedServices.Keys);
        
        var alertingServiceError = result.FailedServices["IAlertingService"];
        Assert.Contains("AlertingConfig", alertingServiceError.ErrorSummary);
        Assert.Contains("singleton", alertingServiceError.ResolutionGuidance);
        
        _output.WriteLine("Error Details:");
        _output.WriteLine(alertingServiceError.DetailedError);
    }

    [Fact]
    public void ValidateCriticalDependencies_WithAllDependenciesRegistered_ReturnsValid()
    {
        // Arrange - Create service collection with all required dependencies
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        
        // Add AlertingConfig
        services.AddSingleton<MySqlBackupTool.Shared.Models.AlertingConfig>();
        
        // Add HttpClient for AlertingService
        services.AddHttpClient<MySqlBackupTool.Shared.Services.AlertingService>();
        
        // Add AlertingService
        services.AddScoped<MySqlBackupTool.Shared.Interfaces.IAlertingService, MySqlBackupTool.Shared.Services.AlertingService>();
        
        // Add the dependency validator
        services.AddSingleton<DependencyResolutionValidator>();
        
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var result = serviceProvider.ValidateCriticalDependencies();

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains("IAlertingService", result.ValidServices);
        Assert.Contains("AlertingConfig", result.ValidServices);
        Assert.Contains("IHttpClientFactory", result.ValidServices);
        
        _output.WriteLine($"Successfully validated {result.ValidServices.Count} services");
    }

    [Fact]
    public void ValidateCriticalDependenciesOrThrow_WithMissingDependencies_ThrowsDetailedException()
    {
        // Arrange - Create service collection with missing dependencies
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        
        // Add AlertingService but not its dependencies
        services.AddScoped<MySqlBackupTool.Shared.Interfaces.IAlertingService, MySqlBackupTool.Shared.Services.AlertingService>();
        
        // Add the dependency validator
        services.AddSingleton<DependencyResolutionValidator>();
        
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.ValidateCriticalDependenciesOrThrow());

        Assert.Contains("Critical dependency validation failed", exception.Message);
        Assert.Contains("IAlertingService", exception.Message);
        Assert.Contains("Resolution Guidance", exception.Message);
        Assert.Contains("Dependency Chain", exception.Message);
        
        _output.WriteLine("Exception Message:");
        _output.WriteLine(exception.Message);
    }

    [Fact]
    public void DependencyResolutionValidator_AnalyzesConstructorDependencies_Correctly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<DependencyResolutionValidator>();
        
        var serviceProvider = services.BuildServiceProvider();
        var validator = serviceProvider.GetRequiredService<DependencyResolutionValidator>();

        // Act
        var result = validator.ValidateCriticalServices(serviceProvider);

        // Assert
        Assert.False(result.IsValid);
        
        // Check that constructor analysis is performed
        var failedService = result.FailedServices.Values.FirstOrDefault();
        Assert.NotNull(failedService);
        Assert.NotNull(failedService.ConstructorAnalysis);
        
        if (failedService.ConstructorAnalysis != null)
        {
            Assert.True(failedService.ConstructorAnalysis.RequiredDependencies.Count > 0);
            _output.WriteLine($"Constructor analysis for {failedService.ServiceName}:");
            _output.WriteLine($"  Required dependencies: {failedService.ConstructorAnalysis.RequiredDependencyCount}");
            _output.WriteLine($"  Optional dependencies: {failedService.ConstructorAnalysis.OptionalDependencyCount}");
        }
    }

    [Fact]
    public void BuildDetailedValidationErrorMessage_WithMultipleFailures_IncludesDependencyChains()
    {
        // Arrange - Create a scenario with multiple dependency failures
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        
        // Add services that will fail due to missing dependencies
        services.AddScoped<MySqlBackupTool.Shared.Interfaces.IAlertingService, MySqlBackupTool.Shared.Services.AlertingService>();
        
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.ValidateServicesOrThrowAsync().GetAwaiter().GetResult());

        // Assert
        Assert.Contains("Service validation failed", exception.Message);
        Assert.Contains("Dependency Chain", exception.Message);
        Assert.Contains("Guidance", exception.Message);
        
        _output.WriteLine("Detailed Error Message:");
        _output.WriteLine(exception.Message);
    }
}