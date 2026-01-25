using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Tests.Integration;

/// <summary>
/// Integration test to verify the actual server Program.cs setup works correctly
/// This test mimics the exact service registration used in the server application
/// </summary>
public class ServerProgramIntegrationTest : IDisposable
{
    private readonly string _testDatabasePath;
    private readonly string _testStoragePath;

    public ServerProgramIntegrationTest()
    {
        _testDatabasePath = Path.Combine(Path.GetTempPath(), $"test_server_program_{Guid.NewGuid()}.db");
        _testStoragePath = Path.Combine(Path.GetTempPath(), $"test_server_program_storage_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testStoragePath);
    }

    [Fact]
    public void ServerProgram_ServiceConfiguration_MatchesActualImplementation()
    {
        // Arrange - Use the exact same service configuration as in Server/Program.cs
        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Add shared services (exactly as in Program.cs)
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);
                services.AddSharedServices(connectionString);
                
                // Add server-specific services (exactly as in Program.cs)
                services.AddServerServices(_testStoragePath);
                
                // Add retention policy background service (exactly as in Program.cs)
                services.AddRetentionPolicyBackgroundService(TimeSpan.FromHours(24));
            });

        // Act - Build the host (this will validate all service registrations)
        using var host = hostBuilder.Build();
        var serviceProvider = host.Services;

        // Assert - Verify critical services can be resolved
        var alertingService = serviceProvider.GetService<IAlertingService>();
        Assert.NotNull(alertingService);
        
        var alertingConfig = serviceProvider.GetService<AlertingConfig>();
        Assert.NotNull(alertingConfig);
        
        // Verify server-specific services
        var fileReceiver = serviceProvider.GetService<IFileReceiver>();
        Assert.NotNull(fileReceiver);
        
        var storageManager = serviceProvider.GetService<IStorageManager>();
        Assert.NotNull(storageManager);
        
        // Verify database services
        var backupLogService = serviceProvider.GetService<IBackupLogService>();
        Assert.NotNull(backupLogService);
        
        // Verify logging works
        var logger = serviceProvider.GetService<ILogger<ServerProgramIntegrationTest>>();
        Assert.NotNull(logger);
        
        // Log success message to verify logging infrastructure
        logger.LogInformation("Server program integration test completed successfully");
    }

    [Fact]
    public void ServerProgram_CriticalDependencyValidation_PassesForActualConfiguration()
    {
        // Arrange - Use the exact same service configuration as in Server/Program.cs
        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);
                services.AddSharedServices(connectionString);
                services.AddServerServices(_testStoragePath);
                services.AddRetentionPolicyBackgroundService(TimeSpan.FromHours(24));
            });

        // Act - Build the host and validate critical dependencies
        using var host = hostBuilder.Build();
        var validationResult = host.Services.ValidateCriticalDependencies();

        // Assert - All critical dependencies should be valid
        Assert.True(validationResult.IsValid, 
            $"Critical dependency validation failed for server program configuration. Failed services: {string.Join(", ", validationResult.FailedServices.Keys)}");
        
        Assert.Empty(validationResult.FailedServices);
        
        // Verify specific critical services are resolved
        Assert.Contains("IAlertingService", validationResult.ValidServices);
        Assert.Contains("AlertingConfig", validationResult.ValidServices);
        Assert.Contains("IHttpClientFactory", validationResult.ValidServices);
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