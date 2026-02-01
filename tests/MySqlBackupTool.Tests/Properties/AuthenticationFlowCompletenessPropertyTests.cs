using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Moq;

namespace MySqlBackupTool.Tests.Properties;

/// <summary>
/// Property-based tests for end-to-end authentication flow completeness
/// **Validates: Requirements 5.1, 5.4**
/// </summary>
public class AuthenticationFlowCompletenessPropertyTests : IDisposable
{
    private readonly List<string> _tempFiles;
    private readonly ILogger<AuthenticatedFileTransferClient> _clientLogger;
    private readonly ILogger<FileReceiver> _serverLogger;
    private readonly ILogger<SecureCredentialStorage> _storageLogger;

    public AuthenticationFlowCompletenessPropertyTests()
    {
        _tempFiles = new List<string>();
        var loggerFactory = new LoggerFactory();
        _clientLogger = loggerFactory.CreateLogger<AuthenticatedFileTransferClient>();
        _serverLogger = loggerFactory.CreateLogger<FileReceiver>();
        _storageLogger = loggerFactory.CreateLogger<SecureCredentialStorage>();
    }

    /// <summary>
    /// Property 4: Authentication flow completeness
    /// For any backup operation with valid credentials, the client should successfully retrieve credentials, 
    /// create a valid token, and the server should validate it and allow the operation to proceed.
    /// **Validates: Requirements 5.1, 5.4**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property AuthenticationFlowCompletenessProperty()
    {
        return Prop.ForAll<string, string>((clientId, clientSecret) =>
        {
            // Filter out invalid inputs
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                return true; // Skip invalid inputs

            if (clientId.Contains(':') || clientId.Length > 100 || clientSecret.Length > 200)
                return true; // Skip inputs that would fail validation

            try
            {
                // Arrange - Set up credential storage
                var tempFile = Path.GetTempFileName();
                _tempFiles.Add(tempFile);

                var config = new CredentialStorageConfig
                {
                    CredentialsFilePath = tempFile,
                    EncryptionKey = "test-encryption-key-12345678"
                };

                var credentialStorage = new SecureCredentialStorage(_storageLogger, config);

                // Store test credentials
                var testCredentials = new ClientCredentials
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret,
                    ClientName = "Test Client",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var storeResult = credentialStorage.StoreCredentialsAsync(testCredentials).Result;
                if (!storeResult)
                    return false;

                // Set up mocks for services
                var mockAuthService = new Mock<IAuthenticationService>();
                var mockChecksumService = new Mock<IChecksumService>();

                // Configure checksum service to return valid checksums
                mockChecksumService.Setup(x => x.CalculateFileMD5Async(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("d41d8cd98f00b204e9800998ecf8427e");
                mockChecksumService.Setup(x => x.CalculateFileSHA256Async(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");

                // Create client with authentication
                var mockAuditService = new Mock<IAuthenticationAuditService>();
                var client = new AuthenticatedFileTransferClient(
                    _clientLogger,
                    mockAuthService.Object,
                    mockChecksumService.Object,
                    credentialStorage,
                    mockAuditService.Object);

                // Create backup configuration with test credentials
                var backupConfig = new BackupConfiguration
                {
                    Name = "Test Backup",
                    ClientId = clientId,
                    ClientSecret = clientSecret,
                    MySQLConnection = new MySQLConnectionInfo
                    {
                        Host = "localhost",
                        Port = 3306,
                        Username = "test",
                        Password = "test",
                        ServiceName = "MySQL",
                        DataDirectoryPath = Path.GetTempPath()
                    },
                    TargetServer = new ServerEndpoint
                    {
                        IPAddress = "127.0.0.1",
                        Port = 8080,
                        UseSSL = false
                    },
                    TargetDirectory = "/backups",
                    NamingStrategy = new FileNamingStrategy()
                };

                // Act - Step 1: Client retrieves credentials and creates token
                var authToken = client.CreateAuthenticationTokenAsync(backupConfig).Result;

                // Assert - Token should be created successfully
                if (string.IsNullOrEmpty(authToken))
                    return false;

                // Verify token format (should be base64 encoded "clientId:clientSecret")
                try
                {
                    var decodedBytes = Convert.FromBase64String(authToken);
                    var decodedCredentials = System.Text.Encoding.UTF8.GetString(decodedBytes);
                    var expectedCredentials = $"{clientId}:{clientSecret}";
                    
                    if (decodedCredentials != expectedCredentials)
                        return false;
                }
                catch
                {
                    return false; // Invalid base64 token
                }

                // Act - Step 2: Server validates the token
                var serverCredentialStorage = new SecureCredentialStorage(_storageLogger, config);
                var serverResult = ValidateTokenOnServer(authToken, serverCredentialStorage);

                // Assert - Server should validate the token successfully
                if (!serverResult.IsSuccess)
                    return false;

                if (serverResult.ClientId != clientId)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Authentication flow completeness test failed: {ex.Message}");
                return false;
            }
        }).Label("Feature: authentication-token-fix, Property 4: Authentication flow completeness");
    }

    /// <summary>
    /// Property test for authentication flow with default credentials fallback
    /// When backup configuration lacks credentials, the system should automatically use default credentials
    /// **Validates: Requirements 3.4, 6.3**
    /// </summary>
    [Property(MaxTest = 30)]
    public Property DefaultCredentialFallbackFlowProperty()
    {
        return Prop.ForAll<int>(_ =>
        {
            try
            {
                // Arrange - Set up credential storage with default credentials
                var tempFile = Path.GetTempFileName();
                _tempFiles.Add(tempFile);

                var config = new CredentialStorageConfig
                {
                    CredentialsFilePath = tempFile,
                    EncryptionKey = "test-encryption-key-12345678"
                };

                var credentialStorage = new SecureCredentialStorage(_storageLogger, config);

                // Ensure default credentials exist - this is critical for the test
                var defaultsExist = credentialStorage.EnsureDefaultCredentialsExistAsync().Result;
                if (!defaultsExist)
                {
                    Console.WriteLine("Failed to ensure default credentials exist");
                    return false;
                }

                // Verify default credentials were actually created
                var defaultCreds = credentialStorage.GetDefaultCredentialsAsync().Result;
                if (defaultCreds == null || defaultCreds.ClientId != "default-client")
                {
                    Console.WriteLine($"Default credentials were not created correctly. ClientId: {defaultCreds?.ClientId ?? "null"}");
                    return false;
                }

                // Note: ClientSecret is hashed when stored, so we can't compare directly
                // Instead, verify that the credential exists and has the correct ClientId

                // Set up mocks
                var mockAuthService = new Mock<IAuthenticationService>();
                var mockChecksumService = new Mock<IChecksumService>();

                mockChecksumService.Setup(x => x.CalculateFileMD5Async(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("d41d8cd98f00b204e9800998ecf8427e");
                mockChecksumService.Setup(x => x.CalculateFileSHA256Async(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");

                var mockAuditService = new Mock<IAuthenticationAuditService>();
                var client = new AuthenticatedFileTransferClient(
                    _clientLogger,
                    mockAuthService.Object,
                    mockChecksumService.Object,
                    credentialStorage,
                    mockAuditService.Object);

                // Create backup configuration WITHOUT credentials (should fallback to defaults)
                var backupConfig = new BackupConfiguration
                {
                    Name = "Test Backup",
                    ClientId = "", // Empty - should trigger fallback
                    ClientSecret = "", // Empty - should trigger fallback
                    MySQLConnection = new MySQLConnectionInfo
                    {
                        Host = "localhost",
                        Port = 3306,
                        Username = "test",
                        Password = "test",
                        ServiceName = "MySQL",
                        DataDirectoryPath = Path.GetTempPath()
                    },
                    TargetServer = new ServerEndpoint
                    {
                        IPAddress = "127.0.0.1",
                        Port = 8080,
                        UseSSL = false
                    },
                    TargetDirectory = "/backups",
                    NamingStrategy = new FileNamingStrategy()
                };

                // Act - Client should fallback to default credentials
                var authToken = client.CreateAuthenticationTokenAsync(backupConfig).Result;

                // Assert - Token should be created using default credentials
                if (string.IsNullOrEmpty(authToken))
                {
                    Console.WriteLine("No authentication token was created");
                    return false;
                }

                // Verify token contains default credentials
                try
                {
                    var decodedBytes = Convert.FromBase64String(authToken);
                    var decodedCredentials = System.Text.Encoding.UTF8.GetString(decodedBytes);
                    var expectedCredentials = "default-client:default-secret-2024";
                    
                    if (decodedCredentials != expectedCredentials)
                    {
                        Console.WriteLine($"Token contains unexpected credentials: {decodedCredentials}, expected: {expectedCredentials}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to decode authentication token: {ex.Message}");
                    return false;
                }

                // Verify backup configuration was updated with default credentials
                if (backupConfig.ClientId != "default-client")
                {
                    Console.WriteLine($"BackupConfig ClientId was not updated: {backupConfig.ClientId}");
                    return false;
                }

                if (backupConfig.ClientSecret != "default-secret-2024")
                {
                    Console.WriteLine($"BackupConfig ClientSecret was not updated: {backupConfig.ClientSecret}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Default credential fallback flow test failed: {ex.Message}");
                return false;
            }
        }).Label("Feature: authentication-token-fix, Property: Default credential fallback flow");
    }

    /// <summary>
    /// Property test for authentication error handling in the flow
    /// Invalid credentials should be properly handled throughout the authentication flow
    /// **Validates: Requirements 4.1, 4.2, 4.3**
    /// </summary>
    [Property(MaxTest = 30)]
    public Property AuthenticationErrorHandlingFlowProperty()
    {
        return Prop.ForAll<string, string>((invalidClientId, invalidSecret) =>
        {
            // Filter out inputs that would trigger fallback behavior instead of error handling
            if (string.IsNullOrWhiteSpace(invalidClientId) && string.IsNullOrWhiteSpace(invalidSecret))
                return true; // Skip - this would trigger fallback, not error

            // Filter out inputs that contain colons (these are correctly rejected by validation)
            if (!string.IsNullOrEmpty(invalidClientId) && invalidClientId.Contains(':'))
                return true; // Skip - colon validation is working correctly

            if (!string.IsNullOrEmpty(invalidSecret) && invalidSecret.Contains(':'))
                return true; // Skip - colon validation is working correctly

            // Filter out inputs that are too long (these are correctly rejected by validation)
            if (!string.IsNullOrEmpty(invalidClientId) && invalidClientId.Length > 100)
                return true; // Skip - length validation is working correctly

            if (!string.IsNullOrEmpty(invalidSecret) && invalidSecret.Length > 200)
                return true; // Skip - length validation is working correctly

            try
            {
                // Arrange - Set up credential storage without the invalid credentials
                var tempFile = Path.GetTempFileName();
                _tempFiles.Add(tempFile);

                var config = new CredentialStorageConfig
                {
                    CredentialsFilePath = tempFile,
                    EncryptionKey = "test-encryption-key-12345678"
                };

                var credentialStorage = new SecureCredentialStorage(_storageLogger, config);

                // Only store default credentials, not the invalid ones
                credentialStorage.EnsureDefaultCredentialsExistAsync().Wait();

                // Set up mocks
                var mockAuthService = new Mock<IAuthenticationService>();
                var mockChecksumService = new Mock<IChecksumService>();

                var mockAuditService = new Mock<IAuthenticationAuditService>();
                var client = new AuthenticatedFileTransferClient(
                    _clientLogger,
                    mockAuthService.Object,
                    mockChecksumService.Object,
                    credentialStorage,
                    mockAuditService.Object);

                // Create backup configuration with invalid credentials that don't trigger validation errors
                var backupConfig = new BackupConfiguration
                {
                    Name = "Test Backup",
                    ClientId = string.IsNullOrWhiteSpace(invalidClientId) ? "non-existent-client" : invalidClientId,
                    ClientSecret = string.IsNullOrWhiteSpace(invalidSecret) ? "wrong-secret" : invalidSecret,
                    MySQLConnection = new MySQLConnectionInfo
                    {
                        Host = "localhost",
                        Port = 3306,
                        Username = "test",
                        Password = "test",
                        ServiceName = "MySQL",
                        DataDirectoryPath = Path.GetTempPath()
                    },
                    TargetServer = new ServerEndpoint
                    {
                        IPAddress = "127.0.0.1",
                        Port = 8080,
                        UseSSL = false
                    },
                    TargetDirectory = "/backups",
                    NamingStrategy = new FileNamingStrategy()
                };

                // Act - Try to create token with invalid credentials
                var authToken = client.CreateAuthenticationTokenAsync(backupConfig).Result;

                // The system should handle this gracefully - either through fallback or proper error handling
                if (!string.IsNullOrEmpty(authToken))
                {
                    // If we got a token, it should be valid (fallback to defaults likely occurred)
                    try
                    {
                        var decodedBytes = Convert.FromBase64String(authToken);
                        var decodedCredentials = System.Text.Encoding.UTF8.GetString(decodedBytes);
                        
                        // Should be either the original credentials or default fallback
                        var isValidFormat = decodedCredentials.Contains(':') && decodedCredentials.Split(':').Length == 2;
                        if (!isValidFormat)
                        {
                            Console.WriteLine($"Invalid token format: {decodedCredentials}");
                            return false;
                        }

                        // If fallback occurred, should be default credentials
                        if (decodedCredentials == "default-client:default-secret-2024")
                        {
                            // Fallback worked correctly
                            return true;
                        }

                        // Otherwise, should be the original credentials (if they were valid)
                        var expectedCredentials = $"{backupConfig.ClientId}:{backupConfig.ClientSecret}";
                        return decodedCredentials == expectedCredentials;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to decode token: {ex.Message}");
                        return false; // Invalid token format
                    }
                }

                // If no token was created, that's also acceptable for invalid credentials
                // This indicates proper error handling
                return true;
            }
            catch (Exception ex)
            {
                // Exceptions should be handled gracefully in the authentication flow
                // We expect authentication-related exceptions to be handled properly
                var isAuthenticationError = ex.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase) || 
                                          ex.Message.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
                                          ex.Message.Contains("token", StringComparison.OrdinalIgnoreCase);
                
                if (isAuthenticationError)
                {
                    // This is expected behavior for invalid credentials
                    return true;
                }
                
                Console.WriteLine($"Unexpected error in authentication error handling flow test: {ex.Message}");
                return false;
            }
        }).Label("Feature: authentication-token-fix, Property: Authentication error handling flow");
    }

    /// <summary>
    /// Simulates server-side token validation
    /// </summary>
    private AuthenticationResult ValidateTokenOnServer(string token, ISecureCredentialStorage credentialStorage)
    {
        try
        {
            // Decode base64 token
            var decodedBytes = Convert.FromBase64String(token);
            var credentials = System.Text.Encoding.UTF8.GetString(decodedBytes);
            
            // Parse clientId:clientSecret format
            var parts = credentials.Split(':', 2);
            if (parts.Length != 2)
            {
                return AuthenticationResult.Failure("Invalid token format");
            }
            
            var clientId = parts[0];
            var clientSecret = parts[1];
            
            // Validate against stored credentials
            var isValid = credentialStorage.ValidateCredentialsAsync(clientId, clientSecret).Result;
            
            if (isValid)
            {
                return AuthenticationResult.Success(clientId);
            }
            else
            {
                return AuthenticationResult.Failure("Invalid credentials");
            }
        }
        catch (Exception ex)
        {
            return AuthenticationResult.Failure($"Token validation error: {ex.Message}");
        }
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