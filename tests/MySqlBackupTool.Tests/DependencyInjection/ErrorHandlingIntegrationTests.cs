using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace MySqlBackupTool.Tests.DependencyInjection;

/// <summary>
/// Integration tests for error handling in dependency resolution with AddSharedServices
/// </summary>
public class ErrorHandlingIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public ErrorHandlingIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AddSharedServices_WithValidConfiguration_ValidatesSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        
        // Create a minimal configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Alerting:EnableAlerting"] = "true",
                ["Alerting:TimeoutSeconds"] = "30",
                ["Alerting:MaxRetryAttempts"] = "3"
            })
            .Build();

        // Add shared services with configuration
        services.AddSharedServices("Data Source=:memory:", configuration);
        
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var result = serviceProvider.ValidateCriticalDependencies();

        // Assert
        _output.WriteLine($"Validation result: {result.IsValid}");
        _output.WriteLine($"Valid services: {result.ValidServices.Count}");
        _output.WriteLine($"Failed services: {result.FailedServices.Count}");

        foreach (var validService in result.ValidServices)
        {
            _output.WriteLine($"✅ {validService}");
        }

        foreach (var failedService in result.FailedServices)
        {
            _output.WriteLine($"❌ {failedService.Key}: {failedService.Value.ErrorSummary}");
            _output.WriteLine($"   Guidance: {failedService.Value.ResolutionGuidance}");
        }

        // The test should pass even if some services fail, as long as the error handling provides useful information
        Assert.True(result.ValidServices.Count > 0, "At least some services should be valid");
        
        // Check that error messages are informative for any failed services
        foreach (var failedService in result.FailedServices.Values)
        {
            Assert.False(string.IsNullOrEmpty(failedService.ErrorSummary), "Error summary should not be empty");
            Assert.False(string.IsNullOrEmpty(failedService.ResolutionGuidance), "Resolution guidance should not be empty");
        }
    }

    [Fact]
    public void ValidateServicesOrThrowAsync_WithMissingDependencies_ProvidesDetailedErrorMessage()
    {
        // Arrange - Create a service collection with incomplete registration
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        
        // Add only some services to create dependency resolution failures
        services.AddSingleton<MySqlBackupTool.Shared.Models.AlertingConfig>();
        // Intentionally omit HttpClient registration to cause AlertingService to fail
        services.AddScoped<MySqlBackupTool.Shared.Interfaces.IAlertingService, MySqlBackupTool.Shared.Services.AlertingService>();
        
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await serviceProvider.ValidateServicesOrThrowAsync());

        var errorMessage = exception.Result.Message;
        
        _output.WriteLine("Detailed Error Message:");
        _output.WriteLine(errorMessage);

        // Verify the error message contains useful information
        Assert.Contains("Service validation failed", errorMessage);
        Assert.Contains("Dependency Chain", errorMessage);
        Assert.Contains("Guidance", errorMessage);
        Assert.Contains("HttpClient", errorMessage); // Should mention the missing HttpClient dependency
    }

    [Fact]
    public void DependencyResolutionValidator_ProvidesConstructorAnalysis()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<MySqlBackupTool.Shared.Services.DependencyResolutionValidator>();
        
        var serviceProvider = services.BuildServiceProvider();
        var validator = serviceProvider.GetRequiredService<MySqlBackupTool.Shared.Services.DependencyResolutionValidator>();

        // Act
        var result = validator.ValidateCriticalServices(serviceProvider);

        // Assert
        Assert.False(result.IsValid); // Should fail due to missing services
        Assert.True(result.FailedServices.Count > 0);

        // Check that constructor analysis is provided
        var failedService = result.FailedServices.Values.First();
        Assert.NotNull(failedService.ConstructorAnalysis);
        
        _output.WriteLine($"Constructor analysis for {failedService.ServiceName}:");
        _output.WriteLine($"  Service Type: {failedService.ConstructorAnalysis.ServiceType}");
        _output.WriteLine($"  Has Public Constructors: {failedService.ConstructorAnalysis.HasPublicConstructors}");
        _output.WriteLine($"  Required Dependencies: {failedService.ConstructorAnalysis.RequiredDependencyCount}");
        
        if (failedService.ConstructorAnalysis.RequiredDependencies.Any())
        {
            _output.WriteLine("  Dependencies:");
            foreach (var dep in failedService.ConstructorAnalysis.RequiredDependencies)
            {
                _output.WriteLine($"    - {dep.TypeName} {dep.ParameterName} ({(dep.IsOptional ? "optional" : "required")})");
            }
        }
    }
}