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
/// Integration tests for complete backup workflows with authentication
/// Tests the end-to-end authentication flow from client to server
/// </summary>
public class AuthenticationBackupFlowIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly IHost _clientHost;
    private readonly IHost _serverHost;
    private readonly string _testDatabasePath;
    private readonly string _testStoragePath;
    private readonly int _testPort;

    private readonly string _testCredentialsPath;

    public AuthenticationBackupFlowIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _testPort = GetAvailablePort();
        _testDatabasePath = Path.Combine(Path.GetTempPath(), $"test_auth_backup_{Guid.NewGuid():N}.db");
        _testStoragePath = Path.Combine(Path.GetTempPath(), $"test_auth_storage_{Guid.NewGuid():N}");
        _testCredentialsPath = Path.Combine(Path.GetTempPath(), $"test_auth_credentials_{Guid.NewGuid():N}.dat");
        Directory.CreateDirectory(_testStoragePath);

        // Create client host with authentication services
        _clientHost = CreateClientHost();
        
        // Create server host with authentication services
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
                
                // Override the credential storage config to use shared test file
                services.AddSingleton(new CredentialStorageConfig
                {
                    CredentialsFilePath = _testCredentialsPath,
                    EncryptionKey = "MySqlBackupTool-TestKey-2024",
                    UseWindowsDPAPI = false, // Disable DPAPI for cross-platform testing
                    MaxAuthenticationAttempts = 5,
                    LockoutDurationMinutes = 15
                });
                
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
                // Use the same database file as the client for shared credentials
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(_testDatabasePath);
                services.AddSharedServices(connectionString);
                services.AddServerServices(_testStoragePath, useSecureReceiver: false); // Use non-secure for testing
                
                // Override the credential storage config to use shared test file
                services.AddSingleton(new CredentialStorageConfig
                {
                    CredentialsFilePath = _testCredentialsPath,
                    EncryptionKey = "MySqlBackupTool-TestKey-2024",
                    UseWindowsDPAPI = false, // Disable DPAPI for cross-platform testing
                    MaxAuthenticationAttempts = 5,
                    LockoutDurationMinutes = 15
                });
                
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
    public async Task AuthenticationTokenCreation_ShouldSucceed()
    {
        // Arrange
        await _clientHost.Services.InitializeDatabaseAsync();
        await _serverHost.Services.InitializeDatabaseAsync();

        // Ensure default credentials exist
        var clientCredentialStorage = _clientHost.Services.GetRequiredService<ISecureCredentialStorage>();
        await clientCredentialStorage.EnsureDefaultCredentialsExistAsync();

        // Create a backup configuration for token creation
        var config = new BackupConfiguration
        {
            ClientId = "default-client",
            ClientSecret = "default-secret-2024"
        };

        // Act - Create authentication token
        // Get the concrete AuthenticatedFileTransferClient directly from DI
        var authenticatedClient = _clientHost.Services.GetRequiredService<AuthenticatedFileTransferClient>();
        var token = await authenticatedClient.CreateAuthenticationTokenAsync(config);
        
        // Assert - Token should be created successfully
        Assert.NotNull(token);
        Assert.NotEmpty(token);
        
        // Verify token format by decoding it
        var decodedBytes = Convert.FromBase64String(token);
        var credentials = System.Text.Encoding.UTF8.GetString(decodedBytes);
        Assert.Equal("default-client:default-secret-2024", credentials);
    }

    [Fact]
    public async Task AuthenticationTokenValidation_ShouldSucceed()
    {
        // Arrange
        await _clientHost.Services.InitializeDatabaseAsync();
        await _serverHost.Services.InitializeDatabaseAsync();

        // Ensure default credentials exist in both client and server
        var clientCredentialStorage = _clientHost.Services.GetRequiredService<ISecureCredentialStorage>();
        var serverCredentialStorage = _serverHost.Services.GetRequiredService<ISecureCredentialStorage>();
        
        await clientCredentialStorage.EnsureDefaultCredentialsExistAsync();
        await serverCredentialStorage.EnsureDefaultCredentialsExistAsync();

        // Create a valid token using the client's AuthenticatedFileTransferClient
        var config = new BackupConfiguration
        {
            ClientId = "default-client",
            ClientSecret = "default-secret-2024"
        };

        var authenticatedClient = _clientHost.Services.GetRequiredService<AuthenticatedFileTransferClient>();
        var token = await authenticatedClient.CreateAuthenticationTokenAsync(config);

        // Act - Validate token on server side
        var fileReceiver = (FileReceiver)_serverHost.Services.GetRequiredService<IFileReceiver>();
        var validationResult = await fileReceiver.ValidateTokenAsync(token);

        // Assert - Token should be validated successfully
        Assert.True(validationResult.IsSuccess, $"Token validation should succeed. Error: {validationResult.ErrorMessage}");
        Assert.Equal("default-client", validationResult.ClientId);
    }

    [Fact]
    public async Task AuthenticationWithCustomCredentials_ShouldSucceed()
    {
        // Arrange
        await _clientHost.Services.InitializeDatabaseAsync();
        await _serverHost.Services.InitializeDatabaseAsync();

        // Create custom credentials in both client and server
        var clientCredentialStorage = _clientHost.Services.GetRequiredService<ISecureCredentialStorage>();
        var serverCredentialStorage = _serverHost.Services.GetRequiredService<ISecureCredentialStorage>();
        
        var customCredentials = new ClientCredentials
        {
            ClientId = "test-client",
            ClientSecret = "test-secret-123",
            IsActive = true
        };

        await clientCredentialStorage.StoreCredentialsAsync(customCredentials);
        await serverCredentialStorage.StoreCredentialsAsync(customCredentials);

        // Create token with custom credentials
        var credentials = "test-client:test-secret-123";
        var token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(credentials));

        // Act - Validate token
        var fileReceiver = (FileReceiver)_serverHost.Services.GetRequiredService<IFileReceiver>();
        var validationResult = await fileReceiver.ValidateTokenAsync(token);

        // Assert
        Assert.True(validationResult.IsSuccess, $"Custom credential validation should succeed. Error: {validationResult.ErrorMessage}");
        Assert.Equal("test-client", validationResult.ClientId);
    }

    [Fact]
    public async Task AuthenticationWithDefaultCredentialFallback_ShouldSucceed()
    {
        // Arrange
        await _clientHost.Services.InitializeDatabaseAsync();
        await _serverHost.Services.InitializeDatabaseAsync();

        // Ensure default credentials exist
        var clientCredentialStorage = _clientHost.Services.GetRequiredService<ISecureCredentialStorage>();
        await clientCredentialStorage.EnsureDefaultCredentialsExistAsync();

        // Create backup configuration WITHOUT explicit credentials (should fallback to defaults)
        var config = new BackupConfiguration
        {
            // No ClientId/ClientSecret specified - should fallback to defaults
        };

        // Act - Create token (should use default credentials)
        var authenticatedClient = _clientHost.Services.GetRequiredService<AuthenticatedFileTransferClient>();
        var token = await authenticatedClient.CreateAuthenticationTokenAsync(config);
        
        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);
        
        // Verify configuration was updated with default credentials
        Assert.Equal("default-client", config.ClientId);
        Assert.Equal("default-secret-2024", config.ClientSecret);
        
        // Verify token contains default credentials
        var decodedBytes = Convert.FromBase64String(token);
        var credentials = System.Text.Encoding.UTF8.GetString(decodedBytes);
        Assert.Equal("default-client:default-secret-2024", credentials);
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
            
            if (File.Exists(_testCredentialsPath))
                File.Delete(_testCredentialsPath);
            
            if (Directory.Exists(_testStoragePath))
                Directory.Delete(_testStoragePath, true);
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }
}