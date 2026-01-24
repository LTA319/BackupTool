using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MySqlBackupTool.Shared.Data;
using MySqlBackupTool.Shared.Data.Repositories;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using System.Net;
using System.Net.Sockets;

namespace MySqlBackupTool.Tests.Properties;

/// <summary>
/// Property-based tests for network communication functionality
/// **Validates: Requirements 1.2, 1.4, 1.5, 8.1, 8.2, 8.5**
/// </summary>
public class NetworkCommunicationPropertyTests : IDisposable
{
    private readonly List<IFileReceiver> _fileReceivers;
    private readonly List<int> _usedPorts;
    private readonly System.Random _random;

    public NetworkCommunicationPropertyTests()
    {
        _fileReceivers = new List<IFileReceiver>();
        _usedPorts = new List<int>();
        _random = new System.Random();
    }

    /// <summary>
    /// Property 2: Network Communication Establishment
    /// For any valid client-server endpoint pair with network connectivity, 
    /// the Backup Client should be able to establish secure communication with the File Receiver Server.
    /// **Validates: Requirements 1.2, 1.5, 8.1, 8.2**
    /// </summary>
    [Property(MaxTest = 10)]
    public bool NetworkCommunicationEstablishmentProperty()
    {
        try
        {
            // Arrange - Create file receiver with available port
            var port = GetAvailablePort();
            var fileReceiver = CreateFileReceiver();
            
            // Act - Start server and test connection
            fileReceiver.StartListeningAsync(port).Wait(TimeSpan.FromSeconds(5));
            
            // Test connection establishment
            var connectionEstablished = TestTcpConnection("127.0.0.1", port);
            
            // Clean up
            fileReceiver.StopListeningAsync().Wait(TimeSpan.FromSeconds(5));
            
            // Assert - Verify connection was established
            return connectionEstablished;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Network communication establishment test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Property 3: Concurrent Client Support
    /// For any set of multiple Backup Client instances, the File Receiver Server 
    /// should be able to handle concurrent connections from all clients simultaneously.
    /// **Validates: Requirements 1.4, 8.5**
    /// </summary>
    [Property(MaxTest = 5)]
    public bool ConcurrentClientSupportProperty()
    {
        try
        {
            // Arrange - Create file receiver
            var port = GetAvailablePort();
            var fileReceiver = CreateFileReceiver();
            
            // Start server
            fileReceiver.StartListeningAsync(port).Wait(TimeSpan.FromSeconds(5));
            
            // Act - Test multiple concurrent connections
            var concurrentConnections = Math.Min(5, Environment.ProcessorCount); // Limit based on system
            var connectionTasks = new List<Task<bool>>();
            
            for (int i = 0; i < concurrentConnections; i++)
            {
                var task = Task.Run(() => TestTcpConnection("127.0.0.1", port));
                connectionTasks.Add(task);
            }
            
            // Wait for all connections to complete
            Task.WaitAll(connectionTasks.ToArray(), TimeSpan.FromSeconds(10));
            
            // Clean up
            fileReceiver.StopListeningAsync().Wait(TimeSpan.FromSeconds(5));
            
            // Assert - Verify all connections succeeded
            var allConnectionsSucceeded = connectionTasks.All(t => t.IsCompletedSuccessfully && t.Result);
            
            return allConnectionsSucceeded;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Concurrent client support test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Property test for server start/stop reliability
    /// Server should be able to start and stop multiple times without issues
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Property(MaxTest = 5)]
    public bool ServerStartStopReliabilityProperty()
    {
        try
        {
            // Arrange
            var port = GetAvailablePort();
            var fileReceiver = CreateFileReceiver();
            
            // Act - Start and stop server multiple times
            for (int i = 0; i < 3; i++)
            {
                // Start server
                fileReceiver.StartListeningAsync(port).Wait(TimeSpan.FromSeconds(5));
                
                // Verify it's listening
                var isListening = TestTcpConnection("127.0.0.1", port);
                if (!isListening)
                    return false;
                
                // Stop server
                fileReceiver.StopListeningAsync().Wait(TimeSpan.FromSeconds(5));
                
                // Brief delay between cycles
                Thread.Sleep(100);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Server start/stop reliability test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Property test for port availability validation
    /// Server should handle port conflicts gracefully
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Property(MaxTest = 5)]
    public bool PortAvailabilityValidationProperty()
    {
        try
        {
            // Arrange - Create two file receivers for the same port
            var port = GetAvailablePort();
            var fileReceiver1 = CreateFileReceiver();
            var fileReceiver2 = CreateFileReceiver();
            
            // Act - Start first server
            fileReceiver1.StartListeningAsync(port).Wait(TimeSpan.FromSeconds(5));
            
            // Try to start second server on same port
            var secondServerFailed = false;
            try
            {
                fileReceiver2.StartListeningAsync(port).Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                secondServerFailed = true;
            }
            
            // Clean up
            fileReceiver1.StopListeningAsync().Wait(TimeSpan.FromSeconds(5));
            try
            {
                fileReceiver2.StopListeningAsync().Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Expected if second server never started
            }
            
            // Assert - Second server should fail to start or handle gracefully
            return secondServerFailed || TestTcpConnection("127.0.0.1", port);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Port availability validation test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Property test for connection timeout handling
    /// Server should handle client timeouts gracefully
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Property(MaxTest = 5)]
    public bool ConnectionTimeoutHandlingProperty()
    {
        try
        {
            // Arrange
            var port = GetAvailablePort();
            var fileReceiver = CreateFileReceiver();
            
            // Start server
            fileReceiver.StartListeningAsync(port).Wait(TimeSpan.FromSeconds(5));
            
            // Act - Create connection but don't send data (simulate timeout)
            using var client = new TcpClient();
            client.Connect("127.0.0.1", port);
            
            // Wait briefly then disconnect without proper protocol
            Thread.Sleep(100);
            client.Close();
            
            // Server should still be responsive
            var serverStillResponsive = TestTcpConnection("127.0.0.1", port);
            
            // Clean up
            fileReceiver.StopListeningAsync().Wait(TimeSpan.FromSeconds(5));
            
            return serverStillResponsive;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection timeout handling test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Creates a file receiver instance for testing
    /// </summary>
    private IFileReceiver CreateFileReceiver()
    {
        var logger = new LoggerFactory().CreateLogger<FileReceiver>();
        var storageLogger = new LoggerFactory().CreateLogger<StorageManager>();
        var chunkLogger = new LoggerFactory().CreateLogger<ChunkManager>();
        var checksumLogger = new LoggerFactory().CreateLogger<ChecksumService>();
        
        var storageManager = new StorageManager(storageLogger);
        var checksumService = new ChecksumService(checksumLogger);
        
        // Setup in-memory database for resume token repository
        var options = new DbContextOptionsBuilder<BackupDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        var dbContext = new BackupDbContext(options);
        dbContext.Database.EnsureCreated();
        
        var repoLogger = new LoggerFactory().CreateLogger<ResumeTokenRepository>();
        var resumeTokenRepository = new ResumeTokenRepository(dbContext, repoLogger);
        
        var chunkManager = new ChunkManager(chunkLogger, checksumService, resumeTokenRepository);
        
        // Create mock authentication services for testing
        var mockAuthService = new Mock<IAuthenticationService>();
        var mockAuthzService = new Mock<IAuthorizationService>();
        
        // Setup mock to allow all operations for testing
        mockAuthService.Setup(x => x.GetAuthorizationContextAsync(It.IsAny<string>()))
            .ReturnsAsync(new AuthorizationContext 
            { 
                ClientId = "test-client", 
                Permissions = new List<string> { BackupPermissions.UploadBackup } 
            });
        
        mockAuthzService.Setup(x => x.IsAuthorizedAsync(It.IsAny<AuthorizationContext>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        
        var fileReceiver = new FileReceiver(logger, storageManager, chunkManager, checksumService, 
            mockAuthService.Object, mockAuthzService.Object);
        
        _fileReceivers.Add(fileReceiver);
        return fileReceiver;
    }

    /// <summary>
    /// Gets an available port for testing
    /// </summary>
    private int GetAvailablePort()
    {
        var attempts = 0;
        while (attempts < 10)
        {
            var port = _random.Next(49152, 65535); // Use dynamic port range
            
            if (_usedPorts.Contains(port))
            {
                attempts++;
                continue;
            }
            
            try
            {
                using var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                
                _usedPorts.Add(port);
                return port;
            }
            catch
            {
                attempts++;
            }
        }
        
        throw new InvalidOperationException("Could not find available port for testing");
    }

    /// <summary>
    /// Tests TCP connection to a server
    /// </summary>
    private static bool TestTcpConnection(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            
            if (connectTask.Wait(TimeSpan.FromSeconds(2)))
            {
                return client.Connected;
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        // Clean up all file receivers
        foreach (var receiver in _fileReceivers)
        {
            try
            {
                receiver.StopListeningAsync().Wait(TimeSpan.FromSeconds(2));
                if (receiver is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        
        _fileReceivers.Clear();
        _usedPorts.Clear();
    }
}