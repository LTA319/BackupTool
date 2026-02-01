using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Moq;
using System.Text;

namespace MySqlBackupTool.Tests.Properties;

/// <summary>
/// Property-based tests for credential validation round-trip
/// **Validates: Requirements 2.3, 5.3**
/// </summary>
public class CredentialValidationPropertyTests : IDisposable
{
    private readonly List<string> _tempFiles;
    private readonly ILogger<FileReceiver> _logger;
    private readonly ILogger<SecureCredentialStorage> _storageLogger;

    public CredentialValidationPropertyTests()
    {
        _tempFiles = new List<string>();
        var loggerFactory = new LoggerFactory();
        _logger = loggerFactory.CreateLogger<FileReceiver>();
        _storageLogger = loggerFactory.CreateLogger<SecureCredentialStorage>();
    }

    /// <summary>
    /// Property 3: Credential validation round-trip
    /// For any stored client credentials, validating them against the SecureCredentialStorage 
    /// should return true, and invalid credentials should return false.
    /// **Validates: Requirements 2.3, 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CredentialValidationRoundTripProperty()
    {
        // Generate valid client IDs and secrets
        var validClientIdGen = from id in Arb.Default.NonEmptyString().Generator
                              where !id.Get.Contains(':') && !string.IsNullOrWhiteSpace(id.Get) && id.Get.Length <= 50
                              select id.Get.Trim();

        var validClientSecretGen = from secret in Arb.Default.NonEmptyString().Generator
                                  where !string.IsNullOrWhiteSpace(secret.Get) && secret.Get.Length <= 100
                                  select secret.Get.Trim();

        return Prop.ForAll(
            Arb.From(validClientIdGen),
            Arb.From(validClientSecretGen),
            (clientId, clientSecret) =>
            {
                try
                {
                    // Arrange - Create temporary credential storage
                    var tempFile = Path.GetTempFileName();
                    _tempFiles.Add(tempFile);

                    var storageConfig = new CredentialStorageConfig
                    {
                        CredentialsFilePath = tempFile,
                        EncryptionKey = "test-encryption-key-12345678"
                    };

                    var credentialStorage = new SecureCredentialStorage(_storageLogger, storageConfig);

                    // Create client credentials
                    var credentials = new ClientCredentials
                    {
                        ClientId = clientId,
                        ClientSecret = clientSecret,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    // Store credentials
                    var storeResult = credentialStorage.StoreCredentialsAsync(credentials).Result;
                    if (!storeResult)
                        return false;

                    // Create mocked dependencies for FileReceiver
                    var mockStorageManager = new Mock<IStorageManager>();
                    var mockChunkManager = new Mock<IChunkManager>();
                    var mockChecksumService = new Mock<IChecksumService>();
                    var mockAuthService = new Mock<IAuthenticationService>();
                    var mockAuthorizationService = new Mock<IAuthorizationService>();
                    var mockAuditService = new Mock<IAuthenticationAuditService>();

                    // Create FileReceiver
                    var fileReceiver = new FileReceiver(
                        _logger,
                        mockStorageManager.Object,
                        mockChunkManager.Object,
                        mockChecksumService.Object,
                        mockAuthService.Object,
                        mockAuthorizationService.Object,
                        credentialStorage,
                        mockAuditService.Object);

                    // Act - Create valid token and validate it
                    var validToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
                    var validResult = fileReceiver.ValidateTokenAsync(validToken).Result;

                    // Assert - Valid credentials should return success
                    if (!validResult.IsSuccess || validResult.ClientId != clientId)
                        return false;

                    // Act - Create invalid token with wrong secret and validate it
                    var invalidSecret = clientSecret + "invalid";
                    var invalidToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{invalidSecret}"));
                    var invalidResult = fileReceiver.ValidateTokenAsync(invalidToken).Result;

                    // Assert - Invalid credentials should return failure
                    if (invalidResult.IsSuccess)
                        return false;

                    // Act - Create token with non-existent client ID
                    var nonExistentClientId = clientId + "nonexistent";
                    var nonExistentToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{nonExistentClientId}:{clientSecret}"));
                    var nonExistentResult = fileReceiver.ValidateTokenAsync(nonExistentToken).Result;

                    // Assert - Non-existent client should return failure
                    if (nonExistentResult.IsSuccess)
                        return false;

                    // Act - Test direct credential validation through storage
                    var directValidResult = credentialStorage.ValidateCredentialsAsync(clientId, clientSecret).Result;
                    var directInvalidResult = credentialStorage.ValidateCredentialsAsync(clientId, invalidSecret).Result;

                    // Assert - Direct validation should match token validation results
                    if (!directValidResult || directInvalidResult)
                        return false;

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Credential validation round-trip test failed: {ex.Message}");
                    return false;
                }
            }).Label("Feature: authentication-token-fix, Property 3: Credential validation round-trip");
    }

    /// <summary>
    /// Property test for malformed token handling
    /// Tests that malformed tokens are properly rejected with appropriate error messages
    /// </summary>
    [Property(MaxTest = 50)]
    public Property MalformedTokenHandlingProperty()
    {
        return Prop.ForAll<string>(malformedInput =>
        {
            try
            {
                // Skip null or empty inputs as they're handled separately
                if (string.IsNullOrEmpty(malformedInput))
                    return true;

                // Arrange - Create temporary credential storage
                var tempFile = Path.GetTempFileName();
                _tempFiles.Add(tempFile);

                var storageConfig = new CredentialStorageConfig
                {
                    CredentialsFilePath = tempFile,
                    EncryptionKey = "test-encryption-key-12345678"
                };

                var credentialStorage = new SecureCredentialStorage(_storageLogger, storageConfig);

                // Create mocked dependencies for FileReceiver
                var mockStorageManager = new Mock<IStorageManager>();
                var mockChunkManager = new Mock<IChunkManager>();
                var mockChecksumService = new Mock<IChecksumService>();
                var mockAuthService = new Mock<IAuthenticationService>();
                var mockAuthorizationService = new Mock<IAuthorizationService>();
                var mockAuditService = new Mock<IAuthenticationAuditService>();

                // Create FileReceiver
                var fileReceiver = new FileReceiver(
                    _logger,
                    mockStorageManager.Object,
                    mockChunkManager.Object,
                    mockChecksumService.Object,
                    mockAuthService.Object,
                    mockAuthorizationService.Object,
                    credentialStorage,
                    mockAuditService.Object);

                // Act - Try to validate malformed token
                var result = fileReceiver.ValidateTokenAsync(malformedInput).Result;

                // Assert - Malformed token should always return failure
                if (result.IsSuccess)
                    return false;

                // Assert - Error message should be descriptive but not expose sensitive info
                if (string.IsNullOrEmpty(result.ErrorMessage))
                    return false;

                // Assert - Error message should not contain the original malformed input
                if (result.ErrorMessage.Contains(malformedInput))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Malformed token handling test failed: {ex.Message}");
                return false;
            }
        }).Label("Feature: authentication-token-fix, Property 3: Malformed token handling");
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