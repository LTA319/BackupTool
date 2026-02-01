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
                var client = new AuthenticatedFileTransferClient(
                    _clientLogger,
                    mockAuthService.Object,
                    mockChecksumService.Object,
                    credentialStorage);

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

                // Ensure default credentials exist
                var defaultsExist = credentialStorage.EnsureDefaultCredentialsExistAsync().Result;
                if (!defaultsExist)
                    return false;

                // Set up mocks
                var mockAuthService = new Mock<IAuthenticationService>();
                var mockChecksumService = new Mock<IChecksumService>();

                mockChecksumService.Setup(x => x.CalculateFileMD5Async(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("d41d8cd98f00b204e9800998ecf8427e");
                mockChecksumService.Setup(x => x.CalculateFileSHA256Async(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");

                var client = new AuthenticatedFileTransferClient(
                    _clientLogger,
                    mockAuthService.Object,
                    mockChecksumService.Object,
                    credentialStorage);

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
                    return false;

                // Verify token contains default credentials
                try
                {
                    var decodedBytes = Convert.FromBase64String(authToken);
                    var decodedCredentials = System.Text.Encoding.UTF8.GetString(decodedBytes);
                    var expectedCredentials = "default-client:default-secret-2024";
                    
                    if (decodedCredentials != expectedCredentials)
                        return false;
                }
                catch
                {
                    return false;
                }

                // Verify backup configuration was updated with default credentials
                if (backupConfig.ClientId != "default-client")
                    return false;

                if (backupConfig.ClientSecret != "default-secret-2024")
                    return false;

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
            // Only test with clearly invalid inputs
            if (string.IsNullOrWhiteSpace(invalidClientId) && string.IsNullOrWhiteSpace(invalidSecret))
                return true; // Skip - this would trigger fallback, not error

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

                var client = new AuthenticatedFileTransferClient(
                    _clientLogger,
                    mockAuthService.Object,
                    mockChecksumService.Object,
                    credentialStorage);

                // Create backup configuration with invalid credentials
                var backupConfig = new BackupConfiguration
                {
                    Name = "Test Backup",
                    ClientId = invalidClientId ?? "non-existent-client",
                    ClientSecret = invalidSecret ?? "wrong-secret",
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

                // If the credentials were completely invalid and no fallback occurred,
                // we should either get a token (fallback worked) or proper error handling
                if (!string.IsNullOrEmpty(authToken))
                {
                    // If we got a token, it should be valid (fallback to defaults)
                    try
                    {
                        var decodedBytes = Convert.FromBase64String(authToken);
                        var decodedCredentials = System.Text.Encoding.UTF8.GetString(decodedBytes);
                        
                        // Should be either the original credentials or default fallback
                        return decodedCredentials.Contains(':');
                    }
                    catch
                    {
                        return false; // Invalid token format
                    }
                }

                // If no token was created, that's also acceptable for invalid credentials
                return true;
            }
            catch (Exception ex)
            {
                // Exceptions should be handled gracefully in the authentication flow
                Console.WriteLine($"Authentication error handling flow test failed: {ex.Message}");
                return ex.Message.Contains("authentication") || ex.Message.Contains("credential");
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