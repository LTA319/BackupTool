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
/// Property-based tests for authentication error handling
/// **Validates: Requirements 4.1, 4.2, 4.3, 4.4**
/// </summary>
public class AuthenticationErrorHandlingPropertyTests : IDisposable
{
    private readonly List<string> _tempFiles;
    private readonly ILogger<FileReceiver> _logger;
    private readonly ILogger<SecureCredentialStorage> _storageLogger;

    public AuthenticationErrorHandlingPropertyTests()
    {
        _tempFiles = new List<string>();
        var loggerFactory = new LoggerFactory();
        _logger = loggerFactory.CreateLogger<FileReceiver>();
        _storageLogger = loggerFactory.CreateLogger<SecureCredentialStorage>();
    }

    /// <summary>
    /// Property 7: Authentication error handling
    /// For any authentication failure scenario (missing, invalid, or malformed credentials), 
    /// the system should log appropriate error messages without exposing sensitive information 
    /// and provide user-friendly error messages.
    /// **Validates: Requirements 4.1, 4.2, 4.3, 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AuthenticationErrorHandlingProperty()
    {
        // Generate various types of invalid tokens
        var invalidTokenGen = Gen.OneOf(
            Gen.Constant(""), // Empty token
            Gen.Constant("invalid-base64!@#"), // Invalid base64
            Gen.Constant("dGVzdA=="), // Valid base64 but wrong format (just "test")
            Gen.Constant("dGVzdDp0ZXN0OnRlc3Q="), // Valid base64 but too many colons ("test:test:test")
            from invalidData in Arb.Default.NonEmptyString().Generator
            where !string.IsNullOrWhiteSpace(invalidData.Get) && invalidData.Get.Length < 100
            select Convert.ToBase64String(Encoding.UTF8.GetBytes(invalidData.Get)) // Random invalid format
        );

        return Prop.ForAll(
            Arb.From(invalidTokenGen),
            invalidToken =>
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

                    // Create mocked dependencies for FileReceiver
                    var mockStorageManager = new Mock<IStorageManager>();
                    var mockChunkManager = new Mock<IChunkManager>();
                    var mockChecksumService = new Mock<IChecksumService>();
                    var mockAuthService = new Mock<IAuthenticationService>();
                    var mockAuthorizationService = new Mock<IAuthorizationService>();

                    // Create FileReceiver
                    var fileReceiver = new FileReceiver(
                        _logger,
                        mockStorageManager.Object,
                        mockChunkManager.Object,
                        mockChecksumService.Object,
                        mockAuthService.Object,
                        mockAuthorizationService.Object,
                        credentialStorage);

                    // Act - Try to validate invalid token
                    var result = fileReceiver.ValidateTokenAsync(invalidToken).Result;

                    // Assert - Authentication should fail
                    if (result.IsSuccess)
                        return false;

                    // Assert - Error message should be present and descriptive
                    if (string.IsNullOrEmpty(result.ErrorMessage))
                        return false;

                    // Assert - Error message should not expose the original token
                    if (!string.IsNullOrEmpty(invalidToken) && result.ErrorMessage.Contains(invalidToken))
                        return false;

                    // Assert - Error message should be user-friendly (not contain stack traces or internal details)
                    var errorMessage = result.ErrorMessage.ToLower();
                    if (errorMessage.Contains("exception") || 
                        errorMessage.Contains("stack") || 
                        errorMessage.Contains("internal") ||
                        errorMessage.Contains("system."))
                        return false;

                    // Assert - Error message should be one of the expected categories
                    var validErrorMessages = new[]
                    {
                        "authentication token is required",
                        "invalid token format",
                        "invalid credentials format",
                        "invalid credentials",
                        "token validation error"
                    };

                    if (!validErrorMessages.Any(msg => errorMessage.Contains(msg.ToLower())))
                        return false;

                    // Assert - ClientId should be null for failed authentication
                    if (!string.IsNullOrEmpty(result.ClientId))
                        return false;

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Authentication error handling test failed: {ex.Message}");
                    return false;
                }
            }).Label("Feature: authentication-token-fix, Property 7: Authentication error handling");
    }

    /// <summary>
    /// Property test for missing credentials error handling
    /// Tests that null and empty tokens are handled appropriately
    /// </summary>
    [Property(MaxTest = 20)]
    public Property MissingCredentialsErrorHandlingProperty()
    {
        var nullOrEmptyTokenGen = Gen.OneOf(
            Gen.Constant((string?)null),
            Gen.Constant(""),
            Gen.Constant("   "), // Whitespace only
            Gen.Constant("\t\n\r") // Various whitespace characters
        );

        return Prop.ForAll(
            Arb.From(nullOrEmptyTokenGen),
            token =>
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

                    // Create mocked dependencies for FileReceiver
                    var mockStorageManager = new Mock<IStorageManager>();
                    var mockChunkManager = new Mock<IChunkManager>();
                    var mockChecksumService = new Mock<IChecksumService>();
                    var mockAuthService = new Mock<IAuthenticationService>();
                    var mockAuthorizationService = new Mock<IAuthorizationService>();

                    // Create FileReceiver
                    var fileReceiver = new FileReceiver(
                        _logger,
                        mockStorageManager.Object,
                        mockChunkManager.Object,
                        mockChecksumService.Object,
                        mockAuthService.Object,
                        mockAuthorizationService.Object,
                        credentialStorage);

                    // Act - Try to validate null/empty token
                    var result = fileReceiver.ValidateTokenAsync(token!).Result;

                    // Assert - Authentication should fail
                    if (result.IsSuccess)
                        return false;

                    // Assert - Error message should indicate missing token
                    if (string.IsNullOrEmpty(result.ErrorMessage))
                        return false;

                    // Assert - Error message should be specific to missing credentials
                    if (!result.ErrorMessage.ToLower().Contains("authentication token is required"))
                        return false;

                    // Assert - ClientId should be null
                    if (!string.IsNullOrEmpty(result.ClientId))
                        return false;

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Missing credentials error handling test failed: {ex.Message}");
                    return false;
                }
            }).Label("Feature: authentication-token-fix, Property 7: Missing credentials error handling");
    }

    /// <summary>
    /// Property test for secure error logging
    /// Tests that error logging doesn't expose sensitive information
    /// </summary>
    [Property(MaxTest = 50)]
    public Property SecureErrorLoggingProperty()
    {
        // Generate tokens that might contain sensitive information
        var sensitiveTokenGen = from sensitiveData in Arb.Default.NonEmptyString().Generator
                               where !string.IsNullOrWhiteSpace(sensitiveData.Get) && sensitiveData.Get.Length < 50
                               let credentials = $"user:{sensitiveData.Get}"
                               select Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));

        return Prop.ForAll(
            Arb.From(sensitiveTokenGen),
            sensitiveToken =>
            {
                try
                {
                    // Arrange - Create temporary credential storage (without the test credentials)
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

                    // Create FileReceiver
                    var fileReceiver = new FileReceiver(
                        _logger,
                        mockStorageManager.Object,
                        mockChunkManager.Object,
                        mockChecksumService.Object,
                        mockAuthService.Object,
                        mockAuthorizationService.Object,
                        credentialStorage);

                    // Act - Try to validate token with sensitive data (should fail since credentials don't exist)
                    var result = fileReceiver.ValidateTokenAsync(sensitiveToken).Result;

                    // Assert - Authentication should fail (credentials don't exist in storage)
                    if (result.IsSuccess)
                        return false;

                    // Assert - Error message should not contain the original token
                    if (result.ErrorMessage.Contains(sensitiveToken))
                        return false;

                    // Assert - Error message should not contain decoded credentials
                    try
                    {
                        var decodedBytes = Convert.FromBase64String(sensitiveToken);
                        var decodedCredentials = Encoding.UTF8.GetString(decodedBytes);
                        if (result.ErrorMessage.Contains(decodedCredentials))
                            return false;

                        // Check individual parts of credentials
                        var parts = decodedCredentials.Split(':', 2);
                        if (parts.Length == 2)
                        {
                            if (result.ErrorMessage.Contains(parts[0]) || result.ErrorMessage.Contains(parts[1]))
                                return false;
                        }
                    }
                    catch
                    {
                        // If decoding fails, that's fine - we just want to ensure no sensitive data is exposed
                    }

                    // Assert - Error message should be generic
                    var validGenericMessages = new[]
                    {
                        "invalid credentials",
                        "authentication failed",
                        "token validation error"
                    };

                    if (!validGenericMessages.Any(msg => result.ErrorMessage.ToLower().Contains(msg)))
                        return false;

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Secure error logging test failed: {ex.Message}");
                    return false;
                }
            }).Label("Feature: authentication-token-fix, Property 7: Secure error logging");
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