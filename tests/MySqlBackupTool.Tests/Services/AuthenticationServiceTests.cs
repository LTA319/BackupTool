using Microsoft.Extensions.Logging;
using Moq;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;

namespace MySqlBackupTool.Tests.Services;

public class AuthenticationServiceTests
{
    private readonly Mock<ILogger<AuthenticationService>> _mockLogger;
    private readonly Mock<ICredentialStorage> _mockCredentialStorage;
    private readonly Mock<ITokenManager> _mockTokenManager;
    private readonly CredentialStorageConfig _config;
    private readonly AuthenticationService _authenticationService;

    public AuthenticationServiceTests()
    {
        _mockLogger = new Mock<ILogger<AuthenticationService>>();
        _mockCredentialStorage = new Mock<ICredentialStorage>();
        _mockTokenManager = new Mock<ITokenManager>();
        _config = new CredentialStorageConfig
        {
            CredentialsFilePath = "test-credentials.dat",
            EncryptionKey = "test-encryption-key-12345",
            MaxAuthenticationAttempts = 5,
            LockoutDurationMinutes = 15
        };

        _authenticationService = new AuthenticationService(
            _mockLogger.Object,
            _mockCredentialStorage.Object,
            _mockTokenManager.Object,
            _config);
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidCredentials_ReturnsSuccessfulResponse()
    {
        // Arrange
        var clientId = "test-client";
        var clientSecret = "test-secret";
        var hashedSecret = new ClientCredentials { ClientSecret = clientSecret }.HashSecret();

        var storedCredentials = new ClientCredentials
        {
            ClientId = clientId,
            ClientSecret = hashedSecret,
            IsActive = true,
            Permissions = new List<string> { BackupPermissions.UploadBackup }
        };

        var expectedToken = new AuthenticationToken
        {
            TokenId = "test-token",
            ClientId = clientId,
            ExpiresAt = DateTime.Now.AddHours(24),
            Permissions = storedCredentials.Permissions
        };

        _mockCredentialStorage.Setup(x => x.GetCredentialsAsync(clientId))
            .ReturnsAsync(storedCredentials);

        _mockTokenManager.Setup(x => x.GenerateTokenAsync(clientId, storedCredentials.Permissions, 24))
            .ReturnsAsync(expectedToken);

        var request = new AuthenticationRequest
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            Timestamp = DateTime.Now
        };

        // Act
        var result = await _authenticationService.AuthenticateAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedToken.TokenId, result.Token);
        Assert.Equal(expectedToken.ExpiresAt, result.TokenExpiresAt);
        Assert.Equal(storedCredentials.Permissions, result.Permissions);
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidCredentials_ReturnsFailureResponse()
    {
        // Arrange
        var clientId = "test-client";
        var clientSecret = "wrong-secret";

        _mockCredentialStorage.Setup(x => x.GetCredentialsAsync(clientId))
            .ReturnsAsync((ClientCredentials?)null);

        var request = new AuthenticationRequest
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            Timestamp = DateTime.Now
        };

        // Act
        var result = await _authenticationService.AuthenticateAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Invalid client credentials", result.ErrorMessage);
        Assert.Null(result.Token);
    }

    [Fact]
    public async Task AuthenticateAsync_WithInactiveClient_ReturnsFailureResponse()
    {
        // Arrange
        var clientId = "test-client";
        var clientSecret = "test-secret";
        var hashedSecret = new ClientCredentials { ClientSecret = clientSecret }.HashSecret();

        var storedCredentials = new ClientCredentials
        {
            ClientId = clientId,
            ClientSecret = hashedSecret,
            IsActive = false, // Inactive client
            Permissions = new List<string> { BackupPermissions.UploadBackup }
        };

        _mockCredentialStorage.Setup(x => x.GetCredentialsAsync(clientId))
            .ReturnsAsync(storedCredentials);

        var request = new AuthenticationRequest
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            Timestamp = DateTime.Now
        };

        // Act
        var result = await _authenticationService.AuthenticateAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Client account is inactive", result.ErrorMessage);
    }

    [Fact]
    public async Task AuthenticateAsync_WithExpiredClient_ReturnsFailureResponse()
    {
        // Arrange
        var clientId = "test-client";
        var clientSecret = "test-secret";
        var hashedSecret = new ClientCredentials { ClientSecret = clientSecret }.HashSecret();

        var storedCredentials = new ClientCredentials
        {
            ClientId = clientId,
            ClientSecret = hashedSecret,
            IsActive = true,
            ExpiresAt = DateTime.Now.AddDays(-1), // Expired yesterday
            Permissions = new List<string> { BackupPermissions.UploadBackup }
        };

        _mockCredentialStorage.Setup(x => x.GetCredentialsAsync(clientId))
            .ReturnsAsync(storedCredentials);

        var request = new AuthenticationRequest
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            Timestamp = DateTime.Now
        };

        // Act
        var result = await _authenticationService.AuthenticateAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Client credentials have expired", result.ErrorMessage);
    }

    [Fact]
    public async Task AuthenticateAsync_WithOldTimestamp_ReturnsFailureResponse()
    {
        // Arrange
        var clientId = "test-client";
        var clientSecret = "test-secret";

        var request = new AuthenticationRequest
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            Timestamp = DateTime.Now.AddMinutes(-10) // 10 minutes old
        };

        // Act
        var result = await _authenticationService.AuthenticateAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Invalid request timestamp", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateTokenAsync_WithValidToken_ReturnsTrue()
    {
        // Arrange
        var tokenId = "valid-token";
        var validToken = new AuthenticationToken
        {
            TokenId = tokenId,
            ClientId = "test-client",
            IsActive = true,
            ExpiresAt = DateTime.Now.AddHours(1)
        };

        _mockTokenManager.Setup(x => x.GetTokenAsync(tokenId))
            .ReturnsAsync(validToken);

        _mockTokenManager.Setup(x => x.UpdateTokenUsageAsync(tokenId))
            .ReturnsAsync(true);

        // Act
        var result = await _authenticationService.ValidateTokenAsync(tokenId);

        // Assert
        Assert.True(result);
        _mockTokenManager.Verify(x => x.UpdateTokenUsageAsync(tokenId), Times.Once);
    }

    [Fact]
    public async Task ValidateTokenAsync_WithInvalidToken_ReturnsFalse()
    {
        // Arrange
        var tokenId = "invalid-token";

        _mockTokenManager.Setup(x => x.GetTokenAsync(tokenId))
            .ReturnsAsync((AuthenticationToken?)null);

        // Act
        var result = await _authenticationService.ValidateTokenAsync(tokenId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetAuthorizationContextAsync_WithValidToken_ReturnsContext()
    {
        // Arrange
        var tokenId = "valid-token";
        var validToken = new AuthenticationToken
        {
            TokenId = tokenId,
            ClientId = "test-client",
            IsActive = true,
            ExpiresAt = DateTime.Now.AddHours(1),
            Permissions = new List<string> { BackupPermissions.UploadBackup }
        };

        _mockTokenManager.Setup(x => x.GetTokenAsync(tokenId))
            .ReturnsAsync(validToken);

        _mockTokenManager.Setup(x => x.UpdateTokenUsageAsync(tokenId))
            .ReturnsAsync(true);

        // Act
        var result = await _authenticationService.GetAuthorizationContextAsync(tokenId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(validToken.ClientId, result.ClientId);
        Assert.Equal(validToken.Permissions, result.Permissions);
    }

    [Fact]
    public async Task RevokeTokenAsync_WithValidToken_ReturnsTrue()
    {
        // Arrange
        var tokenId = "valid-token";

        _mockTokenManager.Setup(x => x.RevokeTokenAsync(tokenId))
            .ReturnsAsync(true);

        // Act
        var result = await _authenticationService.RevokeTokenAsync(tokenId);

        // Assert
        Assert.True(result);
        _mockTokenManager.Verify(x => x.RevokeTokenAsync(tokenId), Times.Once);
    }
}