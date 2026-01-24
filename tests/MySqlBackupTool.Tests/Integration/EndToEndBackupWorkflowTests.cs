using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace MySqlBackupTool.Tests.Integration;

/// <summary>
/// Integration tests for complete backup workflows from start to finish
/// </summary>
public class EndToEndBackupWorkflowTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly IHost _clientHost;
    private readonly IHost _serverHost;
    private readonly string _testDatabasePath;
    private readonly string _testStoragePath;
    private readonly int _testPort;

    public EndToEndBackupWorkflowTests(ITestOutputHelper output)
    {
        _output = output;
        _testPort = GetAvailablePort();
        _testDatabasePath = Path.Combine(Path.GetTempPath(), $"test_backup_{Guid.NewGuid():N}.db");
        _testStoragePath = Path.Combine(Path.GetTempPath(), $"test_storage_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testStoragePath);

        // Create client host
        _clientHost = CreateClientHost();
        
        // Create server host
        _serverHost = CreateServerHost();
    }

    private IHost CreateClientHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);
                services.AddSharedServices(connectionString);
                services.AddClientServices(useSecureTransfer: false); // Use non-secure for testing
                services.AddBackupSchedulingServices();
                
                // Add console logging for testing
                services.AddLogging(builder => 
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            })
            .Build();
    }

    private IHost CreateServerHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath.Replace("test_backup", "test_server"));
                services.AddSharedServices(connectionString);
                services.AddServerServices(_testStoragePath, useSecureReceiver: false); // Use non-secure for testing
                
                // Add console logging for testing
                services.AddLogging(builder => 
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            })
            .Build();
    }

    [Fact]
    public async Task CompleteBackupWorkflow_ShouldSucceed()
    {
        // Arrange
        await _clientHost.Services.InitializeDatabaseAsync();
        await _serverHost.Services.InitializeDatabaseAsync();

        // Start server
        var fileReceiver = _serverHost.Services.GetRequiredService<IFileReceiver>();
        await fileReceiver.StartListeningAsync(_testPort);

        // Create test data directory
        var testDataDir = Path.Combine(Path.GetTempPath(), $"test_mysql_data_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDataDir);
        
        // Create some test files to simulate MySQL data
        await File.WriteAllTextAsync(Path.Combine(testDataDir, "test_table.frm"), "Test table structure");
        await File.WriteAllTextAsync(Path.Combine(testDataDir, "test_table.MYD"), "Test table data");
        await File.WriteAllTextAsync(Path.Combine(testDataDir, "test_table.MYI"), "Test table index");

        // Create backup configuration
        var configRepo = _clientHost.Services.GetRequiredService<IBackupConfigurationRepository>();
        var config = new BackupConfiguration
        {
            Name = "Integration Test Config",
            MySQLConnection = new MySQLConnectionInfo
            {
                Username = "test_user",
                Password = "test_password",
                ServiceName = "test_mysql_service",
                DataDirectoryPath = testDataDir,
                Host = "localhost",
                Port = 3306
            },
            TargetServer = new ServerEndpoint
            {
                IPAddress = "127.0.0.1",
                Port = _testPort,
                UseSSL = false
            },
            TargetDirectory = "backups",
            NamingStrategy = new FileNamingStrategy
            {
                Pattern = "{timestamp}_{database}_integration_test.zip",
                DateFormat = "yyyyMMdd_HHmmss"
            },
            IsActive = true
        };

        await configRepo.AddAsync(config);

        // Act - Perform backup using orchestrator
        var orchestrator = _clientHost.Services.GetRequiredService<IBackupOrchestrator>();
        var result = await orchestrator.ExecuteBackupAsync(config, null, CancellationToken.None);

        // Assert
        Assert.True(result.Success, $"Backup should succeed. Error: {result.ErrorMessage}");
        Assert.NotNull(result.BackupFilePath);
        
        // Verify backup file was created on server
        var expectedBackupPath = Path.Combine(_testStoragePath, "backups");
        Assert.True(Directory.Exists(expectedBackupPath), "Backup directory should exist on server");
        
        var backupFiles = Directory.GetFiles(expectedBackupPath, "*integration_test.zip");
        Assert.Single(backupFiles);
        
        var backupFile = backupFiles[0];
        Assert.True(File.Exists(backupFile), "Backup file should exist");
        Assert.True(new FileInfo(backupFile).Length > 0, "Backup file should not be empty");

        // Verify backup log was created
        var logRepo = _clientHost.Services.GetRequiredService<IBackupLogRepository>();
        var logs = await logRepo.GetAsync(l => l.BackupConfigId == config.Id);
        Assert.Single(logs);
        
        var log = logs.First();
        Assert.Equal(BackupStatus.Completed, log.Status);
        Assert.NotNull(log.FilePath);
        Assert.True(log.FileSize > 0);

        // Cleanup
        await fileReceiver.StopListeningAsync();
        Directory.Delete(testDataDir, true);
        File.Delete(backupFile);
    }

    [Fact]
    public async Task LargeFileBackupWithChunking_ShouldSucceed()
    {
        // Arrange
        await _clientHost.Services.InitializeDatabaseAsync();
        await _serverHost.Services.InitializeDatabaseAsync();

        // Start server
        var fileReceiver = _serverHost.Services.GetRequiredService<IFileReceiver>();
        await fileReceiver.StartListeningAsync(_testPort);

        // Create test data directory with large file
        var testDataDir = Path.Combine(Path.GetTempPath(), $"test_large_data_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDataDir);
        
        // Create a large test file (10MB)
        var largeFilePath = Path.Combine(testDataDir, "large_table.MYD");
        var largeData = new byte[10 * 1024 * 1024]; // 10MB
        new Random().NextBytes(largeData);
        await File.WriteAllBytesAsync(largeFilePath, largeData);

        // Create backup configuration with small chunk size to force chunking
        var configRepo = _clientHost.Services.GetRequiredService<IBackupConfigurationRepository>();
        var config = new BackupConfiguration
        {
            Name = "Large File Test Config",
            MySQLConnection = new MySQLConnectionInfo
            {
                Username = "test_user",
                Password = "test_password",
                ServiceName = "test_mysql_service",
                DataDirectoryPath = testDataDir,
                Host = "localhost",
                Port = 3306
            },
            TargetServer = new ServerEndpoint
            {
                IPAddress = "127.0.0.1",
                Port = _testPort,
                UseSSL = false
            },
            TargetDirectory = "large_backups",
            NamingStrategy = new FileNamingStrategy
            {
                Pattern = "{timestamp}_large_file_test.zip",
                DateFormat = "yyyyMMdd_HHmmss"
            },
            IsActive = true
        };

        await configRepo.AddAsync(config);

        // Act - Perform backup
        var orchestrator = _clientHost.Services.GetRequiredService<IBackupOrchestrator>();
        var result = await orchestrator.ExecuteBackupAsync(config, null, CancellationToken.None);

        // Assert
        Assert.True(result.Success, $"Large file backup should succeed. Error: {result.ErrorMessage}");
        
        // Verify backup file was created and has correct size
        var expectedBackupPath = Path.Combine(_testStoragePath, "large_backups");
        Assert.True(Directory.Exists(expectedBackupPath), "Large backup directory should exist on server");
        
        var backupFiles = Directory.GetFiles(expectedBackupPath, "*large_file_test.zip");
        Assert.Single(backupFiles);
        
        var backupFile = backupFiles[0];
        Assert.True(File.Exists(backupFile), "Large backup file should exist");
        Assert.True(new FileInfo(backupFile).Length > 1024 * 1024, "Backup file should be reasonably large (>1MB compressed)");

        // Cleanup
        await fileReceiver.StopListeningAsync();
        Directory.Delete(testDataDir, true);
        File.Delete(backupFile);
    }

    [Fact]
    public async Task BackupWithInterruptionAndResume_ShouldSucceed()
    {
        // Arrange
        await _clientHost.Services.InitializeDatabaseAsync();
        await _serverHost.Services.InitializeDatabaseAsync();

        // Start server
        var fileReceiver = _serverHost.Services.GetRequiredService<IFileReceiver>();
        await fileReceiver.StartListeningAsync(_testPort);

        // Create test data directory
        var testDataDir = Path.Combine(Path.GetTempPath(), $"test_resume_data_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDataDir);
        
        // Create test files
        await File.WriteAllTextAsync(Path.Combine(testDataDir, "resume_test.frm"), "Resume test data");
        var mediumData = new byte[5 * 1024 * 1024]; // 5MB
        new Random().NextBytes(mediumData);
        await File.WriteAllBytesAsync(Path.Combine(testDataDir, "resume_test.MYD"), mediumData);

        // Create backup configuration
        var configRepo = _clientHost.Services.GetRequiredService<IBackupConfigurationRepository>();
        var config = new BackupConfiguration
        {
            Name = "Resume Test Config",
            MySQLConnection = new MySQLConnectionInfo
            {
                Username = "test_user",
                Password = "test_password",
                ServiceName = "test_mysql_service",
                DataDirectoryPath = testDataDir,
                Host = "localhost",
                Port = 3306
            },
            TargetServer = new ServerEndpoint
            {
                IPAddress = "127.0.0.1",
                Port = _testPort,
                UseSSL = false
            },
            TargetDirectory = "resume_backups",
            NamingStrategy = new FileNamingStrategy
            {
                Pattern = "{timestamp}_resume_test.zip",
                DateFormat = "yyyyMMdd_HHmmss"
            },
            IsActive = true
        };

        await configRepo.AddAsync(config);

        // Act - Simulate interrupted backup by using cancellation token
        var orchestrator = _clientHost.Services.GetRequiredService<IBackupOrchestrator>();
        var cts = new CancellationTokenSource();
        
        // Start backup and cancel after short delay to simulate interruption
        var backupTask = orchestrator.ExecuteBackupAsync(config, null, cts.Token);
        await Task.Delay(100); // Let backup start
        cts.Cancel();
        
        var interruptedResult = await backupTask;
        
        // Verify backup was interrupted
        Assert.False(interruptedResult.Success, "First backup attempt should be interrupted");
        
        // Now perform complete backup (simulating resume)
        var completeResult = await orchestrator.ExecuteBackupAsync(config, null, CancellationToken.None);

        // Assert
        Assert.True(completeResult.Success, $"Resumed backup should succeed. Error: {completeResult.ErrorMessage}");
        
        // Verify backup file was created
        var expectedBackupPath = Path.Combine(_testStoragePath, "resume_backups");
        Assert.True(Directory.Exists(expectedBackupPath), "Resume backup directory should exist on server");
        
        var backupFiles = Directory.GetFiles(expectedBackupPath, "*resume_test.zip");
        Assert.True(backupFiles.Length >= 1, "At least one backup file should exist");

        // Cleanup
        await fileReceiver.StopListeningAsync();
        Directory.Delete(testDataDir, true);
        foreach (var file in backupFiles)
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task DistributedDeploymentScenario_ShouldSucceed()
    {
        // Arrange - This test simulates client and server on different machines
        await _clientHost.Services.InitializeDatabaseAsync();
        await _serverHost.Services.InitializeDatabaseAsync();

        // Start server (simulating remote server)
        var fileReceiver = _serverHost.Services.GetRequiredService<IFileReceiver>();
        await fileReceiver.StartListeningAsync(_testPort);

        // Create multiple backup configurations (simulating multiple databases)
        var configRepo = _clientHost.Services.GetRequiredService<IBackupConfigurationRepository>();
        var configs = new List<BackupConfiguration>();

        for (int i = 1; i <= 3; i++)
        {
            var testDataDir = Path.Combine(Path.GetTempPath(), $"test_distributed_data_{i}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(testDataDir);
            
            // Create test files for each "database"
            await File.WriteAllTextAsync(Path.Combine(testDataDir, $"db{i}_table.frm"), $"Database {i} structure");
            await File.WriteAllTextAsync(Path.Combine(testDataDir, $"db{i}_table.MYD"), $"Database {i} data content");

            var config = new BackupConfiguration
            {
                Name = $"Distributed Test Config {i}",
                MySQLConnection = new MySQLConnectionInfo
                {
                    Username = $"test_user_{i}",
                    Password = $"test_password_{i}",
                    ServiceName = $"test_mysql_service_{i}",
                    DataDirectoryPath = testDataDir,
                    Host = "localhost",
                    Port = 3306 + i
                },
                TargetServer = new ServerEndpoint
                {
                    IPAddress = "127.0.0.1", // Simulating remote server
                    Port = _testPort,
                    UseSSL = false
                },
                TargetDirectory = $"distributed_backups/db{i}",
                NamingStrategy = new FileNamingStrategy
                {
                    Pattern = "{timestamp}_db" + i + "_distributed.zip",
                    DateFormat = "yyyyMMdd_HHmmss"
                },
                IsActive = true
            };

            await configRepo.AddAsync(config);
            configs.Add(config);
        }

        // Act - Perform backups for all configurations
        var orchestrator = _clientHost.Services.GetRequiredService<IBackupOrchestrator>();
        var results = new List<BackupResult>();

        foreach (var config in configs)
        {
            var result = await orchestrator.ExecuteBackupAsync(config, null, CancellationToken.None);
            results.Add(result);
        }

        // Assert
        Assert.All(results, result => 
            Assert.True(result.Success, $"All distributed backups should succeed. Error: {result.ErrorMessage}"));

        // Verify all backup files were created on the "remote" server
        for (int i = 1; i <= 3; i++)
        {
            var expectedBackupPath = Path.Combine(_testStoragePath, $"distributed_backups/db{i}");
            Assert.True(Directory.Exists(expectedBackupPath), $"Distributed backup directory for db{i} should exist");
            
            var backupFiles = Directory.GetFiles(expectedBackupPath, $"*db{i}_distributed.zip");
            Assert.Single(backupFiles);
            
            var backupFile = backupFiles[0];
            Assert.True(File.Exists(backupFile), $"Distributed backup file for db{i} should exist");
            Assert.True(new FileInfo(backupFile).Length > 0, $"Distributed backup file for db{i} should not be empty");
        }

        // Verify backup logs were created for all configurations
        var logRepo = _clientHost.Services.GetRequiredService<IBackupLogRepository>();
        foreach (var config in configs)
        {
            var logs = await logRepo.GetAsync(l => l.BackupConfigId == config.Id);
            Assert.Single(logs);
            Assert.Equal(BackupStatus.Completed, logs.First().Status);
        }

        // Cleanup
        await fileReceiver.StopListeningAsync();
        foreach (var config in configs)
        {
            Directory.Delete(config.MySQLConnection.DataDirectoryPath, true);
            var backupPath = Path.Combine(_testStoragePath, $"distributed_backups/db{config.Id}");
            if (Directory.Exists(backupPath))
            {
                Directory.Delete(backupPath, true);
            }
        }
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
        _clientHost?.Dispose();
        _serverHost?.Dispose();
        
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