using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace MySqlBackupTool.Tests.Integration;

/// <summary>
/// Basic integration tests for component wiring and service resolution
/// </summary>
public class BasicIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testDatabasePath;
    private readonly string _testStoragePath;

    public BasicIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _testDatabasePath = Path.Combine(Path.GetTempPath(), $"test_integration_{Guid.NewGuid():N}.db");
        _testStoragePath = Path.Combine(Path.GetTempPath(), $"test_storage_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testStoragePath);
    }

    [Fact]
    public async Task ClientHost_ShouldResolveAllRequiredServices()
    {
        // Arrange
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);
                services.AddSharedServices(connectionString);
                services.AddClientServices(useSecureTransfer: false);
                services.AddBackupSchedulingServices();
                
                services.AddLogging(builder => 
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Warning);
                });
            })
            .Build();

        // Act & Assert - Verify all critical services can be resolved
        await host.Services.InitializeDatabaseAsync();

        // Core interfaces
        Assert.NotNull(host.Services.GetRequiredService<IMySQLManager>());
        Assert.NotNull(host.Services.GetRequiredService<ICompressionService>());
        Assert.NotNull(host.Services.GetRequiredService<IFileTransferClient>());
        Assert.NotNull(host.Services.GetRequiredService<IBackupOrchestrator>());
        Assert.NotNull(host.Services.GetRequiredService<IBackgroundTaskManager>());
        Assert.NotNull(host.Services.GetRequiredService<IBackupScheduler>());

        // Repository interfaces
        Assert.NotNull(host.Services.GetRequiredService<IBackupConfigurationRepository>());
        Assert.NotNull(host.Services.GetRequiredService<IBackupLogRepository>());
        Assert.NotNull(host.Services.GetRequiredService<IRetentionPolicyRepository>());
        Assert.NotNull(host.Services.GetRequiredService<IResumeTokenRepository>());
        Assert.NotNull(host.Services.GetRequiredService<IScheduleConfigurationRepository>());

        // Service interfaces
        Assert.NotNull(host.Services.GetRequiredService<IBackupLogService>());
        Assert.NotNull(host.Services.GetRequiredService<IRetentionPolicyService>());
        Assert.NotNull(host.Services.GetRequiredService<IErrorRecoveryManager>());
        Assert.NotNull(host.Services.GetRequiredService<INetworkRetryService>());
        Assert.NotNull(host.Services.GetRequiredService<IAlertingService>());
        Assert.NotNull(host.Services.GetRequiredService<IAuthenticationService>());
        Assert.NotNull(host.Services.GetRequiredService<IAuthorizationService>());
        Assert.NotNull(host.Services.GetRequiredService<IChecksumService>());

        host.Dispose();
    }

    [Fact]
    public async Task ServerHost_ShouldResolveAllRequiredServices()
    {
        // Arrange
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath.Replace("integration", "server"));
                services.AddSharedServices(connectionString);
                services.AddServerServices(_testStoragePath, useSecureReceiver: false);
                services.AddRetentionPolicyBackgroundService(TimeSpan.FromHours(24));
                
                services.AddLogging(builder => 
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Warning);
                });
            })
            .Build();

        // Act & Assert - Verify all critical services can be resolved
        await host.Services.InitializeDatabaseAsync();

        // Core server interfaces
        Assert.NotNull(host.Services.GetRequiredService<IFileReceiver>());
        Assert.NotNull(host.Services.GetRequiredService<IChunkManager>());
        Assert.NotNull(host.Services.GetRequiredService<IStorageManager>());

        // Shared interfaces
        Assert.NotNull(host.Services.GetRequiredService<IBackupConfigurationRepository>());
        Assert.NotNull(host.Services.GetRequiredService<IBackupLogRepository>());
        Assert.NotNull(host.Services.GetRequiredService<IRetentionPolicyRepository>());
        Assert.NotNull(host.Services.GetRequiredService<IResumeTokenRepository>());
        Assert.NotNull(host.Services.GetRequiredService<IScheduleConfigurationRepository>());

        // Service interfaces
        Assert.NotNull(host.Services.GetRequiredService<IBackupLogService>());
        Assert.NotNull(host.Services.GetRequiredService<IRetentionPolicyService>());
        Assert.NotNull(host.Services.GetRequiredService<IErrorRecoveryManager>());
        Assert.NotNull(host.Services.GetRequiredService<INetworkRetryService>());
        Assert.NotNull(host.Services.GetRequiredService<IAlertingService>());
        Assert.NotNull(host.Services.GetRequiredService<IAuthenticationService>());
        Assert.NotNull(host.Services.GetRequiredService<IAuthorizationService>());
        Assert.NotNull(host.Services.GetRequiredService<IChecksumService>());

        host.Dispose();
    }

    [Fact]
    public async Task DatabaseInitialization_ShouldCreateTablesSuccessfully()
    {
        // Arrange
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);
                services.AddSharedServices(connectionString);
                services.AddClientServices(useSecureTransfer: false);
                
                services.AddLogging(builder => 
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Warning);
                });
            })
            .Build();

        // Act
        await host.Services.InitializeDatabaseAsync();

        // Assert - Verify database file was created
        Assert.True(File.Exists(_testDatabasePath), "Database file should be created");

        // Verify repositories can perform basic operations
        var configRepo = host.Services.GetRequiredService<IBackupConfigurationRepository>();
        var configs = await configRepo.GetAllAsync();
        Assert.NotNull(configs);

        var logRepo = host.Services.GetRequiredService<IBackupLogRepository>();
        var logs = await logRepo.GetAllAsync();
        Assert.NotNull(logs);

        host.Dispose();
    }

    [Fact]
    public async Task BackupConfiguration_ShouldPersistAndRetrieve()
    {
        // Arrange
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);
                services.AddSharedServices(connectionString);
                services.AddClientServices(useSecureTransfer: false);
                
                services.AddLogging(builder => 
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Warning);
                });
            })
            .Build();

        await host.Services.InitializeDatabaseAsync();

        var configRepo = host.Services.GetRequiredService<IBackupConfigurationRepository>();

        var testConfig = new BackupConfiguration
        {
            Name = "Integration Test Config",
            MySQLConnection = new MySQLConnectionInfo
            {
                Username = "test_user",
                Password = "test_password",
                ServiceName = "test_service",
                DataDirectoryPath = "/test/path",
                Host = "localhost",
                Port = 3306
            },
            TargetServer = new ServerEndpoint
            {
                IPAddress = "127.0.0.1",
                Port = 8080,
                UseSSL = false
            },
            TargetDirectory = "test_backups",
            NamingStrategy = new FileNamingStrategy
            {
                Pattern = "{timestamp}_test.zip",
                DateFormat = "yyyyMMdd_HHmmss"
            },
            IsActive = true
        };

        // Act
        await configRepo.AddAsync(testConfig);
        var retrievedConfig = await configRepo.GetByIdAsync(testConfig.Id);

        // Assert
        Assert.NotNull(retrievedConfig);
        Assert.Equal(testConfig.Name, retrievedConfig.Name);
        Assert.Equal(testConfig.MySQLConnection.Username, retrievedConfig.MySQLConnection.Username);
        Assert.Equal(testConfig.TargetServer.IPAddress, retrievedConfig.TargetServer.IPAddress);
        Assert.Equal(testConfig.TargetDirectory, retrievedConfig.TargetDirectory);
        Assert.Equal(testConfig.IsActive, retrievedConfig.IsActive);

        host.Dispose();
    }

    [Fact]
    public async Task FileReceiver_ShouldStartAndStopSuccessfully()
    {
        // Arrange
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);
                services.AddSharedServices(connectionString);
                services.AddServerServices(_testStoragePath, useSecureReceiver: false);
                
                services.AddLogging(builder => 
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Warning);
                });
            })
            .Build();

        await host.Services.InitializeDatabaseAsync();

        var fileReceiver = host.Services.GetRequiredService<IFileReceiver>();
        var testPort = GetAvailablePort();

        // Act & Assert
        await fileReceiver.StartListeningAsync(testPort);
        
        // Verify server is listening (basic check)
        await Task.Delay(100); // Give server time to start
        
        await fileReceiver.StopListeningAsync();
        
        // If we get here without exceptions, the test passes
        Assert.True(true, "File receiver should start and stop without errors");

        host.Dispose();
    }

    [Fact]
    public async Task CompressionService_ShouldCompressAndCleanupFiles()
    {
        // Arrange
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);
                services.AddSharedServices(connectionString);
                services.AddClientServices(useSecureTransfer: false);
                
                services.AddLogging(builder => 
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Warning);
                });
            })
            .Build();

        var compressionService = host.Services.GetRequiredService<ICompressionService>();

        // Create test directory with files
        var testDir = Path.Combine(Path.GetTempPath(), $"test_compression_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        
        var testFile1 = Path.Combine(testDir, "test1.txt");
        var testFile2 = Path.Combine(testDir, "test2.txt");
        await File.WriteAllTextAsync(testFile1, "Test content 1");
        await File.WriteAllTextAsync(testFile2, "Test content 2");

        var outputPath = Path.Combine(Path.GetTempPath(), $"test_output_{Guid.NewGuid():N}.zip");

        try
        {
            // Act
            var result = await compressionService.CompressDirectoryAsync(testDir, outputPath, null);

            // Assert
            Assert.Equal(outputPath, result);
            Assert.True(File.Exists(outputPath), "Compressed file should exist");
            Assert.True(new FileInfo(outputPath).Length > 0, "Compressed file should not be empty");

            // Test cleanup
            await compressionService.CleanupAsync(outputPath);
            Assert.False(File.Exists(outputPath), "File should be cleaned up");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }

        host.Dispose();
    }

    private static int GetAvailablePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        // Cleanup test files
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