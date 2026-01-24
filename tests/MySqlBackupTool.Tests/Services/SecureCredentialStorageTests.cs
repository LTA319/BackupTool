using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;

namespace MySqlBackupTool.Tests.Services;

public class SecureCredentialStorageTests : IDisposable
{
    private readonly SecureCredentialStorage _credentialStorage;
    private readonly string _testCredentialsFile;
    private readonly CredentialStorageConfig _config;

    public SecureCredentialStorageTests()
    {
        _testCredentialsFile = Path.Combine(Path.GetTempPath(), $"test-credentials-{Guid.NewGuid()}.dat");
        
        _config = new CredentialStorageConfig
        {
            CredentialsFilePath = _testCredentialsFile,
            EncryptionKey = "test-encryption-key-that-is-long-enough-for-security",
            UseWindowsDPAPI = false, // Disable for cross-platform testing
            MaxAuthenticationAttempts = 5,
            LockoutDurationMinutes = 15
        };

        var logger = new LoggerFactory().CreateLogger<SecureCredentialStorage>();
        _credentialStorage = new SecureCredentialStorage(logger, _config);
    }

    [Fact]
    public async Task StoreCredentialsAsync_WithValidCredentials_ReturnsTrue()
    {
        // Arrange
        var credentials = new ClientCredentials
        {
            ClientId = "test-client",
            ClientSecret = "test-secret-123",
            ClientName = "Test Client",
            Permissions = new List<string> { BackupPermissions.UploadBackup },
            IsActive = true
        };

        // Act
        var result = await _credentialStorage.StoreCredentialsAsync(credentials);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetCredentialsAsync_WithExistingClient_ReturnsCredentials()
    {
        // Arrange
        var originalCredentials = new ClientCredentials
        {
            ClientId = "test-client",
            ClientSecret = "test-secret-123",
            ClientName = "Test Client",
            Permissions = new List<string> { BackupPermissions.UploadBackup, BackupPermissions.ListBackups },
            IsActive = true
        };

        await _credentialStorage.StoreCredentialsAsync(originalCredentials);

        // Act
        var retrievedCredentials = await _credentialStorage.GetCredentialsAsync("test-client");

        // Assert
        Assert.NotNull(retrievedCredentials);
        Assert.Equal(originalCredentials.ClientId, retrievedCredentials.ClientId);
        Assert.Equal(originalCredentials.ClientName, retrievedCredentials.ClientName);
        Assert.Equal(originalCredentials.Permissions, retrievedCredentials.Permissions);
        Assert.Equal(originalCredentials.IsActive, retrievedCredentials.IsActive);
        
        // Note: ClientSecret should be hashed, so we verify using the verification method
        Assert.True(originalCredentials.VerifySecret("test-secret-123", retrievedCredentials.ClientSecret));
    }

    [Fact]
    public async Task GetCredentialsAsync_WithNonExistentClient_ReturnsNull()
    {
        // Act
        var result = await _credentialStorage.GetCredentialsAsync("non-existent-client");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateCredentialsAsync_WithExistingClient_ReturnsTrue()
    {
        // Arrange
        var originalCredentials = new ClientCredentials
        {
            ClientId = "test-client",
            ClientSecret = "original-secret",
            ClientName = "Original Name",
            Permissions = new List<string> { BackupPermissions.UploadBackup },
            IsActive = true
        };

        await _credentialStorage.StoreCredentialsAsync(originalCredentials);

        var updatedCredentials = new ClientCredentials
        {
            ClientId = "test-client",
            ClientSecret = "updated-secret",
            ClientName = "Updated Name",
            Permissions = new List<string> { BackupPermissions.UploadBackup, BackupPermissions.ListBackups },
            IsActive = false
        };

        // Act
        var result = await _credentialStorage.UpdateCredentialsAsync(updatedCredentials);

        // Assert
        Assert.True(result);

        // Verify the update
        var retrievedCredentials = await _credentialStorage.GetCredentialsAsync("test-client");
        Assert.NotNull(retrievedCredentials);
        Assert.Equal(updatedCredentials.ClientName, retrievedCredentials.ClientName);
        Assert.Equal(updatedCredentials.Permissions, retrievedCredentials.Permissions);
        Assert.Equal(updatedCredentials.IsActive, retrievedCredentials.IsActive);
        Assert.True(updatedCredentials.VerifySecret("updated-secret", retrievedCredentials.ClientSecret));
    }

    [Fact]
    public async Task UpdateCredentialsAsync_WithNonExistentClient_ReturnsFalse()
    {
        // Arrange
        var credentials = new ClientCredentials
        {
            ClientId = "non-existent-client",
            ClientSecret = "test-secret",
            ClientName = "Test Client",
            Permissions = new List<string> { BackupPermissions.UploadBackup },
            IsActive = true
        };

        // Act
        var result = await _credentialStorage.UpdateCredentialsAsync(credentials);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteCredentialsAsync_WithExistingClient_ReturnsTrue()
    {
        // Arrange
        var credentials = new ClientCredentials
        {
            ClientId = "test-client",
            ClientSecret = "test-secret",
            ClientName = "Test Client",
            Permissions = new List<string> { BackupPermissions.UploadBackup },
            IsActive = true
        };

        await _credentialStorage.StoreCredentialsAsync(credentials);

        // Act
        var result = await _credentialStorage.DeleteCredentialsAsync("test-client");

        // Assert
        Assert.True(result);

        // Verify deletion
        var retrievedCredentials = await _credentialStorage.GetCredentialsAsync("test-client");
        Assert.Null(retrievedCredentials);
    }

    [Fact]
    public async Task DeleteCredentialsAsync_WithNonExistentClient_ReturnsFalse()
    {
        // Act
        var result = await _credentialStorage.DeleteCredentialsAsync("non-existent-client");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ListClientIdsAsync_WithMultipleClients_ReturnsAllClientIds()
    {
        // Arrange
        var client1 = new ClientCredentials
        {
            ClientId = "client-1",
            ClientSecret = "secret-1",
            Permissions = new List<string> { BackupPermissions.UploadBackup }
        };

        var client2 = new ClientCredentials
        {
            ClientId = "client-2",
            ClientSecret = "secret-2",
            Permissions = new List<string> { BackupPermissions.ListBackups }
        };

        await _credentialStorage.StoreCredentialsAsync(client1);
        await _credentialStorage.StoreCredentialsAsync(client2);

        // Act
        var clientIds = await _credentialStorage.ListClientIdsAsync();

        // Assert
        Assert.Contains("client-1", clientIds);
        Assert.Contains("client-2", clientIds);
        Assert.Equal(2, clientIds.Count);
    }

    [Fact]
    public async Task ValidateStorageIntegrityAsync_WithValidStorage_ReturnsTrue()
    {
        // Arrange
        var credentials = new ClientCredentials
        {
            ClientId = "test-client",
            ClientSecret = "test-secret",
            ClientName = "Test Client",
            Permissions = new List<string> { BackupPermissions.UploadBackup },
            IsActive = true
        };

        await _credentialStorage.StoreCredentialsAsync(credentials);

        // Act
        var result = await _credentialStorage.ValidateStorageIntegrityAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateStorageIntegrityAsync_WithEmptyStorage_ReturnsTrue()
    {
        // Act
        var result = await _credentialStorage.ValidateStorageIntegrityAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task StoreAndRetrieveMultipleClients_MaintainsDataIntegrity()
    {
        // Arrange
        var clients = new List<ClientCredentials>
        {
            new ClientCredentials
            {
                ClientId = "client-1",
                ClientSecret = "secret-1",
                ClientName = "Client One",
                Permissions = new List<string> { BackupPermissions.UploadBackup },
                IsActive = true
            },
            new ClientCredentials
            {
                ClientId = "client-2",
                ClientSecret = "secret-2",
                ClientName = "Client Two",
                Permissions = new List<string> { BackupPermissions.DownloadBackup, BackupPermissions.ListBackups },
                IsActive = false
            },
            new ClientCredentials
            {
                ClientId = "client-3",
                ClientSecret = "secret-3",
                ClientName = "Client Three",
                Permissions = new List<string> { BackupPermissions.SystemAdmin },
                IsActive = true,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            }
        };

        // Act - Store all clients
        foreach (var client in clients)
        {
            var storeResult = await _credentialStorage.StoreCredentialsAsync(client);
            Assert.True(storeResult);
        }

        // Assert - Retrieve and verify all clients
        foreach (var originalClient in clients)
        {
            var retrievedClient = await _credentialStorage.GetCredentialsAsync(originalClient.ClientId);
            
            Assert.NotNull(retrievedClient);
            Assert.Equal(originalClient.ClientId, retrievedClient.ClientId);
            Assert.Equal(originalClient.ClientName, retrievedClient.ClientName);
            Assert.Equal(originalClient.Permissions, retrievedClient.Permissions);
            Assert.Equal(originalClient.IsActive, retrievedClient.IsActive);
            Assert.Equal(originalClient.ExpiresAt, retrievedClient.ExpiresAt);
            
            // Verify password hashing
            Assert.True(originalClient.VerifySecret(originalClient.ClientSecret, retrievedClient.ClientSecret));
        }

        // Verify client list
        var clientIds = await _credentialStorage.ListClientIdsAsync();
        Assert.Equal(3, clientIds.Count);
        Assert.Contains("client-1", clientIds);
        Assert.Contains("client-2", clientIds);
        Assert.Contains("client-3", clientIds);
    }

    public void Dispose()
    {
        // Clean up test file
        if (File.Exists(_testCredentialsFile))
        {
            try
            {
                File.Delete(_testCredentialsFile);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}