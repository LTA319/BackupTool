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
/// Integration tests for backup workflow components working together
/// </summary>
public class BackupWorkflowIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testDatabasePath;
    private readonly string _testStoragePath;
    private readonly int _testPort;

    public BackupWorkflowIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _testPort = GetAvailablePort();
        _testDatabasePath = Path.Combine(Path.GetTempPath(), $"test_workflow_{Guid.NewGuid():N}.db");
        _testStoragePath = Path.Combine(Path.GetTempPath(), $"test_workflow_storage_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testStoragePath);
    }

    [Fact]
    public async Task FileTransferWorkflow_ShouldTransferFileSuccessfully()
    {
        // Arrange
        var serverHost = CreateServerHost();
        var clientHost = CreateClientHost();

        await serverHost.Services.InitializeDatabaseAsync();
        await clientHost.Services.InitializeDatabaseAsync();

        // Start file receiver
        var fileReceiver = serverHost.Services.GetRequiredService<IFileReceiver>();
        await fileReceiver.StartListeningAsync(_testPort);

        // Create test file to transfer
        var testFilePath = Path.Combine(Path.GetTempPath(), $"test_transfer_{Guid.NewGuid():N}.txt");
        var testContent = "This is test content for file transfer integration test.";
        await File.WriteAllTextAsync(testFilePath, testContent);

        try
        {
            // Get file transfer client
            var fileTransferClient = clientHost.Services.GetRequiredService<IFileTransferClient>();

            var transferConfig = new TransferConfig
            {
                TargetServer = new ServerEndpoint
                {
                    IPAddress = "127.0.0.1",
                    Port = _testPort,
                    UseSSL = false
                },
                TargetDirectory = "test_transfers",
                FileName = Path.GetFileName(testFilePath)
            };

            // Act
            var result = await fileTransferClient.TransferFileAsync(testFilePath, transferConfig, CancellationToken.None);

            // Assert
            Assert.True(result.Success, $"File transfer should succeed. Error: {result.ErrorMessage}");
            
            // Verify file was received on server
            var expectedPath = Path.Combine(_testStoragePath, "test_transfers", Path.GetFileName(testFilePath));
            Assert.True(File.Exists(expectedPath), "Transferred file should exist on server");
            
            var receivedContent = await File.ReadAllTextAsync(expectedPath);
            Assert.Equal(testContent, receivedContent);
        }
        finally
        {
            await fileReceiver.StopListeningAsync();
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }

        serverHost.Dispose();
        clientHost.Dispose();
    }

    [Fact]
    public async Task CompressionAndTransferWorkflow_ShouldWork()
    {
        // Arrange
        var serverHost = CreateServerHost();
        var clientHost = CreateClientHost();

        await serverHost.Services.InitializeDatabaseAsync();
        await clientHost.Services.InitializeDatabaseAsync();

        // Start file receiver
        var fileReceiver = serverHost.Services.GetRequiredService<IFileReceiver>();
        await fileReceiver.StartListeningAsync(_testPort);

        // Create test directory with multiple files
        var testDir = Path.Combine(Path.GetTempPath(), $"test_compress_dir_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        
        await File.WriteAllTextAsync(Path.Combine(testDir, "file1.txt"), "Content of file 1");
        await File.WriteAllTextAsync(Path.Combine(testDir, "file2.txt"), "Content of file 2");
        await File.WriteAllTextAsync(Path.Combine(testDir, "file3.txt"), "Content of file 3");

        var compressedFilePath = Path.Combine(Path.GetTempPath(), $"test_compressed_{Guid.NewGuid():N}.zip");

        try
        {
            // Get services
            var compressionService = clientHost.Services.GetRequiredService<ICompressionService>();
            var fileTransferClient = clientHost.Services.GetRequiredService<IFileTransferClient>();

            // Act - Compress directory
            var compressedPath = await compressionService.CompressDirectoryAsync(testDir, compressedFilePath, null);
            Assert.Equal(compressedFilePath, compressedPath);
            Assert.True(File.Exists(compressedFilePath), "Compressed file should exist");

            // Act - Transfer compressed file
            var transferConfig = new TransferConfig
            {
                TargetServer = new ServerEndpoint
                {
                    IPAddress = "127.0.0.1",
                    Port = _testPort,
                    UseSSL = false
                },
                TargetDirectory = "compressed_backups",
                FileName = Path.GetFileName(compressedFilePath)
            };

            var transferResult = await fileTransferClient.TransferFileAsync(compressedFilePath, transferConfig, CancellationToken.None);

            // Assert
            Assert.True(transferResult.Success, $"Compressed file transfer should succeed. Error: {transferResult.ErrorMessage}");
            
            // Verify compressed file was received on server
            var expectedPath = Path.Combine(_testStoragePath, "compressed_backups", Path.GetFileName(compressedFilePath));
            Assert.True(File.Exists(expectedPath), "Transferred compressed file should exist on server");
            Assert.True(new FileInfo(expectedPath).Length > 0, "Transferred file should not be empty");

            // Act - Cleanup
            await compressionService.CleanupAsync(compressedFilePath);
            Assert.False(File.Exists(compressedFilePath), "Original compressed file should be cleaned up");
        }
        finally
        {
            await fileReceiver.StopListeningAsync();
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
            if (File.Exists(compressedFilePath))
                File.Delete(compressedFilePath);
        }

        serverHost.Dispose();
        clientHost.Dispose();
    }

    [Fact]
    public async Task ChunkingWorkflow_ShouldHandleLargeFiles()
    {
        // Arrange
        var serverHost = CreateServerHost();
        var clientHost = CreateClientHost();

        await serverHost.Services.InitializeDatabaseAsync();
        await clientHost.Services.InitializeDatabaseAsync();

        // Start file receiver
        var fileReceiver = serverHost.Services.GetRequiredService<IFileReceiver>();
        await fileReceiver.StartListeningAsync(_testPort);

        // Create a larger test file (1MB)
        var testFilePath = Path.Combine(Path.GetTempPath(), $"test_large_{Guid.NewGuid():N}.dat");
        var testData = new byte[1024 * 1024]; // 1MB
        new Random().NextBytes(testData);
        await File.WriteAllBytesAsync(testFilePath, testData);

        try
        {
            // Get services
            var fileTransferClient = clientHost.Services.GetRequiredService<IFileTransferClient>();
            var chunkManager = serverHost.Services.GetRequiredService<IChunkManager>();

            var transferConfig = new TransferConfig
            {
                TargetServer = new ServerEndpoint
                {
                    IPAddress = "127.0.0.1",
                    Port = _testPort,
                    UseSSL = false
                },
                TargetDirectory = "chunked_transfers",
                FileName = Path.GetFileName(testFilePath),
                ChunkingStrategy = new ChunkingStrategy
                {
                    ChunkSize = 256 * 1024 // 256KB chunks to force chunking
                }
            };

            // Act
            var result = await fileTransferClient.TransferFileAsync(testFilePath, transferConfig, CancellationToken.None);

            // Assert
            Assert.True(result.Success, $"Chunked file transfer should succeed. Error: {result.ErrorMessage}");
            
            // Verify file was received and reassembled correctly
            var expectedPath = Path.Combine(_testStoragePath, "chunked_transfers", Path.GetFileName(testFilePath));
            Assert.True(File.Exists(expectedPath), "Reassembled file should exist on server");
            
            var receivedData = await File.ReadAllBytesAsync(expectedPath);
            Assert.Equal(testData.Length, receivedData.Length);
            Assert.Equal(testData, receivedData);
        }
        finally
        {
            await fileReceiver.StopListeningAsync();
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }

        serverHost.Dispose();
        clientHost.Dispose();
    }

    [Fact]
    public async Task BackupLoggingWorkflow_ShouldLogOperations()
    {
        // Arrange
        var clientHost = CreateClientHost();
        await clientHost.Services.InitializeDatabaseAsync();

        var configRepo = clientHost.Services.GetRequiredService<IBackupConfigurationRepository>();
        var logService = clientHost.Services.GetRequiredService<IBackupLogService>();

        // Create test configuration
        var config = new BackupConfiguration
        {
            Name = "Logging Test Config",
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
                Port = _testPort,
                UseSSL = false
            },
            TargetDirectory = "test_logs",
            NamingStrategy = new FileNamingStrategy
            {
                Pattern = "{timestamp}_logging_test.zip",
                DateFormat = "yyyyMMdd_HHmmss"
            },
            IsActive = true
        };

        await configRepo.AddAsync(config);

        // Act - Create backup log
        var backupLog = await logService.StartBackupAsync(config.Id);
        
        // Simulate backup progress
        await logService.UpdateBackupStatusAsync(backupLog.Id, BackupStatus.Compressing, "Compressing data directory");
        await logService.UpdateBackupStatusAsync(backupLog.Id, BackupStatus.Transferring, "Transferring backup file");
        await logService.UpdateBackupStatusAsync(backupLog.Id, BackupStatus.Verifying, "Verifying backup integrity");
        
        // Complete backup
        await logService.CompleteBackupAsync(backupLog.Id, BackupStatus.Completed, "test_backup_file.zip", 1024 * 1024);

        // Assert
        var logRepo = clientHost.Services.GetRequiredService<IBackupLogRepository>();
        var log = await logRepo.GetByIdAsync(backupLog.Id);
        
        Assert.NotNull(log);
        Assert.Equal(config.Id, log.BackupConfigId);
        Assert.Equal(BackupStatus.Completed, log.Status);
        Assert.Equal("test_backup_file.zip", log.FilePath);
        Assert.Equal(1024 * 1024, log.FileSize);
        Assert.NotNull(log.StartTime);
        Assert.NotNull(log.EndTime);

        clientHost.Dispose();
    }

    [Fact]
    public async Task RetentionPolicyWorkflow_ShouldCleanupOldBackups()
    {
        // Arrange
        var serverHost = CreateServerHost();
        await serverHost.Services.InitializeDatabaseAsync();

        var retentionService = serverHost.Services.GetRequiredService<IRetentionPolicyService>();
        var retentionRepo = serverHost.Services.GetRequiredService<IRetentionPolicyRepository>();

        // Create test backup files
        var backupDir = Path.Combine(_testStoragePath, "retention_test");
        Directory.CreateDirectory(backupDir);

        var oldFile1 = Path.Combine(backupDir, "old_backup_1.zip");
        var oldFile2 = Path.Combine(backupDir, "old_backup_2.zip");
        var newFile = Path.Combine(backupDir, "new_backup.zip");

        await File.WriteAllTextAsync(oldFile1, "Old backup 1");
        await File.WriteAllTextAsync(oldFile2, "Old backup 2");
        await File.WriteAllTextAsync(newFile, "New backup");

        // Set old file timestamps
        File.SetCreationTime(oldFile1, DateTime.Now.AddDays(-10));
        File.SetCreationTime(oldFile2, DateTime.Now.AddDays(-8));

        // Create retention policy (keep files for 7 days)
        var policy = new RetentionPolicy
        {
            Name = "Test Retention Policy",
            MaxAgeDays = 7,
            IsEnabled = true
        };

        await retentionRepo.AddAsync(policy);

        // Act
        await retentionService.ApplyRetentionPolicyAsync(policy);

        // Assert
        Assert.False(File.Exists(oldFile1), "Old file 1 should be deleted");
        Assert.False(File.Exists(oldFile2), "Old file 2 should be deleted");
        Assert.True(File.Exists(newFile), "New file should be kept");

        serverHost.Dispose();
    }

    private IHost CreateClientHost()
    {
        return Host.CreateDefaultBuilder()
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
    }

    private IHost CreateServerHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath.Replace("workflow", "server"));
                services.AddSharedServices(connectionString);
                services.AddServerServices(_testStoragePath, useSecureReceiver: false);
                
                services.AddLogging(builder => 
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Warning);
                });
            })
            .Build();
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
            
            var serverDbPath = _testDatabasePath.Replace("workflow", "server");
            if (File.Exists(serverDbPath))
                File.Delete(serverDbPath);
            
            if (Directory.Exists(_testStoragePath))
                Directory.Delete(_testStoragePath, true);
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }
}