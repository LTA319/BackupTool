using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;

namespace MySqlBackupTool.Tests.Services;

/// <summary>
/// Unit tests for SecureCredentialStorage service
/// </summary>
public class SecureCredentialStorageTests : IDisposable
{
    private readonly List<string> _tempFiles;
    private readonly ILogger<SecureCredentialStorage> _logger;

    public SecureCredentialStorageTests()
    {
        _tempFiles = new List<string>();
        var loggerFactory = new LoggerFactory();
        _logger = loggerFactory.CreateLogger<SecureCredentialStorage>();
    }

    [Fact]
    public async Task EnsureDefaultCredentialsExistAsync_ShouldCreateDefaultCredentials()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        _tempFiles.Add(tempFile);

        var config = new CredentialStorageConfig
        {
            CredentialsFilePath = tempFile,
            EncryptionKey = "test-encryption-key-12345678"
        };

        var storage = new SecureCredentialStorage(_logger, config);

        // Act
        var result = await storage.EnsureDefaultCredentialsExistAsync();

        // Assert
        Assert.True(result);
        
        var defaultCredentials = await storage.GetDefaultCredentialsAsync();
        Assert.NotNull(defaultCredentials);
        Assert.Equal("default-client", defaultCredentials.ClientId);
        Assert.True(await storage.CredentialsExistAsync("default-client"));
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithValidCredentials_ShouldReturnTrue()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        _tempFiles.Add(tempFile);

        var config = new CredentialStorageConfig
        {
            CredentialsFilePath = tempFile,
            EncryptionKey = "test-encryption-key-12345678"
        };

        var storage = new SecureCredentialStorage(_logger, config);
        await storage.EnsureDefaultCredentialsExistAsync();

        // Act
        var result = await storage.ValidateCredentialsAsync("default-client", "default-secret-2024");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithInvalidCredentials_ShouldReturnFalse()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        _tempFiles.Add(tempFile);

        var config = new CredentialStorageConfig
        {
            CredentialsFilePath = tempFile,
            EncryptionKey = "test-encryption-key-12345678"
        };

        var storage = new SecureCredentialStorage(_logger, config);
        await storage.EnsureDefaultCredentialsExistAsync();

        // Act
        var result = await storage.ValidateCredentialsAsync("default-client", "wrong-secret");

        // Assert
        Assert.False(result);
    }

    public void Dispose()
    {
        // Clean up temporary files
        foreach (var tempFile in _tempFiles)
        {
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        _tempFiles.Clear();
    }
}