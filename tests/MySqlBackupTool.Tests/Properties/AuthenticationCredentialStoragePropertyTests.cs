using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;

namespace MySqlBackupTool.Tests.Properties;

/// <summary>
/// Property-based tests for authentication credential storage functionality
/// **Validates: Requirements 1.1, 3.1, 3.2, 3.3**
/// </summary>
public class AuthenticationCredentialStoragePropertyTests : IDisposable
{
    private readonly List<string> _tempFiles;
    private readonly ILogger<SecureCredentialStorage> _logger;

    public AuthenticationCredentialStoragePropertyTests()
    {
        _tempFiles = new List<string>();
        var loggerFactory = new LoggerFactory();
        _logger = loggerFactory.CreateLogger<SecureCredentialStorage>();
    }

    /// <summary>
    /// Property 1: System initialization ensures default credentials
    /// For any system initialization, default client credentials with ClientId="default-client" 
    /// and ClientSecret="default-secret-2024" should exist in the database after initialization, 
    /// and subsequent initializations should not overwrite existing credentials.
    /// **Validates: Requirements 1.1, 3.1, 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SystemInitializationEnsuresDefaultCredentialsProperty()
    {
        return Prop.ForAll<int>(_ =>
        {
            try
            {
                // Arrange - Create temporary storage
                var tempFile = Path.GetTempFileName();
                _tempFiles.Add(tempFile);

                var config = new CredentialStorageConfig
                {
                    CredentialsFilePath = tempFile,
                    EncryptionKey = "test-encryption-key-12345678"
                };

                var storage = new SecureCredentialStorage(_logger, config);

                // Act - Ensure default credentials exist (first time)
                var firstResult = storage.EnsureDefaultCredentialsExistAsync().Result;

                // Verify default credentials were created
                var defaultCredentials = storage.GetDefaultCredentialsAsync().Result;

                // Act - Ensure default credentials exist again (should not overwrite)
                var secondResult = storage.EnsureDefaultCredentialsExistAsync().Result;

                // Verify credentials still exist and weren't overwritten
                var defaultCredentialsAfterSecond = storage.GetDefaultCredentialsAsync().Result;

                // Assert - Both operations should succeed
                if (!firstResult || !secondResult)
                    return false;

                // Assert - Default credentials should exist with correct values
                if (defaultCredentials == null || defaultCredentialsAfterSecond == null)
                    return false;

                if (defaultCredentials.ClientId != "default-client")
                    return false;

                // Assert - Credentials should be the same after second initialization (not overwritten)
                if (defaultCredentials.ClientId != defaultCredentialsAfterSecond.ClientId)
                    return false;

                if (defaultCredentials.CreatedAt != defaultCredentialsAfterSecond.CreatedAt)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"System initialization default credentials test failed: {ex.Message}");
                return false;
            }
        }).Label("Feature: authentication-token-fix, Property 1: System initialization ensures default credentials");
    }

    /// <summary>
    /// Property 10: Credential storage interface completeness
    /// For any SecureCredentialStorage implementation, it should provide all required methods 
    /// (retrieve, create, validate, ensure defaults) and they should function correctly.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property CredentialStorageInterfaceCompletenessProperty()
    {
        return Prop.ForAll<int>(_ =>
        {
            try
            {
                // Arrange - Create temporary storage
                var tempFile = Path.GetTempFileName();
                _tempFiles.Add(tempFile);

                var config = new CredentialStorageConfig
                {
                    CredentialsFilePath = tempFile,
                    EncryptionKey = "test-encryption-key-12345678"
                };

                var storage = new SecureCredentialStorage(_logger, config);

                // Test all interface methods exist and work
                
                // 1. EnsureDefaultCredentialsExistAsync
                var ensureResult = storage.EnsureDefaultCredentialsExistAsync().Result;
                if (!ensureResult)
                    return false;

                // 2. GetDefaultCredentialsAsync
                var defaultCreds = storage.GetDefaultCredentialsAsync().Result;
                if (defaultCreds == null)
                    return false;

                // 3. GetCredentialsByClientIdAsync
                var retrievedCreds = storage.GetCredentialsByClientIdAsync("default-client").Result;
                if (retrievedCreds == null)
                    return false;

                // 4. ValidateCredentialsAsync
                var validationResult = storage.ValidateCredentialsAsync("default-client", "default-secret-2024").Result;
                if (!validationResult)
                    return false;

                // 5. CredentialsExistAsync
                var existsResult = storage.CredentialsExistAsync("default-client").Result;
                if (!existsResult)
                    return false;

                // 6. StoreCredentialsAsync (test with new credentials)
                var testCredentials = new ClientCredentials
                {
                    ClientId = "test-client-" + Guid.NewGuid().ToString("N")[..8],
                    ClientSecret = "test-secret-123",
                    ClientName = "Test Client",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                var storeResult = storage.StoreCredentialsAsync(testCredentials).Result;
                if (!storeResult)
                    return false;

                // 7. UpdateCredentialsAsync
                testCredentials.ClientName = "Updated Test Client";
                var updateResult = storage.UpdateCredentialsAsync(testCredentials).Result;
                if (!updateResult)
                    return false;

                // 8. ListClientIdsAsync
                var clientIds = storage.ListClientIdsAsync().Result;
                if (clientIds == null || clientIds.Count < 2) // Should have at least default + test client
                    return false;

                // 9. ValidateStorageIntegrityAsync
                var integrityResult = storage.ValidateStorageIntegrityAsync().Result;
                if (!integrityResult)
                    return false;

                // 10. DeleteCredentialsAsync
                var deleteResult = storage.DeleteCredentialsAsync(testCredentials.ClientId).Result;
                if (!deleteResult)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Credential storage interface completeness test failed: {ex.Message}");
                return false;
            }
        }).Label("Feature: authentication-token-fix, Property 10: Credential storage interface completeness");
    }

    /// <summary>
    /// Property test for credential validation consistency
    /// Valid credentials should always validate successfully, invalid ones should always fail
    /// </summary>
    [Property(MaxTest = 50)]
    public Property CredentialValidationConsistencyProperty()
    {
        return Prop.ForAll<int>(_ =>
        {
            try
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

                // Ensure default credentials exist
                storage.EnsureDefaultCredentialsExistAsync().Wait();

                // Act & Assert - Valid credentials should validate
                var validResult = storage.ValidateCredentialsAsync("default-client", "default-secret-2024").Result;
                if (!validResult)
                    return false;

                // Act & Assert - Invalid credentials should not validate
                var invalidResult1 = storage.ValidateCredentialsAsync("default-client", "wrong-secret").Result;
                if (invalidResult1)
                    return false;

                var invalidResult2 = storage.ValidateCredentialsAsync("non-existent-client", "any-secret").Result;
                if (invalidResult2)
                    return false;

                var invalidResult3 = storage.ValidateCredentialsAsync("", "").Result;
                if (invalidResult3)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Credential validation consistency test failed: {ex.Message}");
                return false;
            }
        }).Label("Feature: authentication-token-fix, Property: Credential validation consistency");
    }

    /// <summary>
    /// Property test for storage persistence
    /// Stored credentials should persist across storage instances
    /// </summary>
    [Property(MaxTest = 30)]
    public Property StoragePersistenceProperty()
    {
        return Prop.ForAll<int>(_ =>
        {
            try
            {
                // Arrange
                var tempFile = Path.GetTempFileName();
                _tempFiles.Add(tempFile);

                var config = new CredentialStorageConfig
                {
                    CredentialsFilePath = tempFile,
                    EncryptionKey = "test-encryption-key-12345678"
                };

                // Create first storage instance and store credentials
                var storage1 = new SecureCredentialStorage(_logger, config);
                storage1.EnsureDefaultCredentialsExistAsync().Wait();

                var testCredentials = new ClientCredentials
                {
                    ClientId = "persist-test-" + Guid.NewGuid().ToString("N")[..8],
                    ClientSecret = "persist-secret-123",
                    ClientName = "Persistence Test Client",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                var storeResult = storage1.StoreCredentialsAsync(testCredentials).Result;
                if (!storeResult)
                    return false;

                // Create second storage instance (simulating restart)
                var storage2 = new SecureCredentialStorage(_logger, config);

                // Act - Try to retrieve credentials from second instance
                var retrievedCredentials = storage2.GetCredentialsByClientIdAsync(testCredentials.ClientId).Result;

                // Assert - Credentials should be retrievable
                if (retrievedCredentials == null)
                    return false;

                if (retrievedCredentials.ClientId != testCredentials.ClientId)
                    return false;

                if (retrievedCredentials.ClientName != testCredentials.ClientName)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Storage persistence test failed: {ex.Message}");
                return false;
            }
        }).Label("Feature: authentication-token-fix, Property: Storage persistence");
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