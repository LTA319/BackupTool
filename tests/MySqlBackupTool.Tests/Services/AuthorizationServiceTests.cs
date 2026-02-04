using Microsoft.Extensions.Logging;
using Moq;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;

namespace MySqlBackupTool.Tests.Services;

public class AuthorizationServiceTests
{
    private readonly Mock<ILogger<AuthorizationService>> _mockLogger;
    private readonly Mock<ICredentialStorage> _mockCredentialStorage;
    private readonly Mock<IAuthenticationAuditService> _mockAuditService;
    private readonly AuthorizationService _authorizationService;

    public AuthorizationServiceTests()
    {
        _mockLogger = new Mock<ILogger<AuthorizationService>>();
        _mockCredentialStorage = new Mock<ICredentialStorage>();
        _mockAuditService = new Mock<IAuthenticationAuditService>();

        _authorizationService = new AuthorizationService(
            _mockLogger.Object,
            _mockCredentialStorage.Object,
            _mockAuditService.Object);
    }

    [Fact]
    public async Task IsAuthorizedAsync_WithSystemAdminPermission_ReturnsTrue()
    {
        // Arrange
        var context = new AuthorizationContext
        {
            ClientId = "admin-client",
            Permissions = new List<string> { BackupPermissions.SystemAdmin }
        };

        // Act
        var result = await _authorizationService.IsAuthorizedAsync(context, "upload_backup");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsAuthorizedAsync_WithRequiredPermission_ReturnsTrue()
    {
        // Arrange
        var context = new AuthorizationContext
        {
            ClientId = "backup-client",
            Permissions = new List<string> { BackupPermissions.UploadBackup }
        };

        // Act
        var result = await _authorizationService.IsAuthorizedAsync(context, "upload_backup");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsAuthorizedAsync_WithoutRequiredPermission_ReturnsFalse()
    {
        // Arrange
        var context = new AuthorizationContext
        {
            ClientId = "limited-client",
            Permissions = new List<string> { BackupPermissions.ViewLogs }
        };

        // Act
        var result = await _authorizationService.IsAuthorizedAsync(context, "upload_backup");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsAuthorizedAsync_WithUnknownOperation_ReturnsFalse()
    {
        // Arrange
        var context = new AuthorizationContext
        {
            ClientId = "test-client",
            Permissions = new List<string> { BackupPermissions.SystemAdmin }
        };

        // Act
        var result = await _authorizationService.IsAuthorizedAsync(context, "unknown_operation");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HasPermissionAsync_WithSystemAdmin_ReturnsTrue()
    {
        // Arrange
        var context = new AuthorizationContext
        {
            ClientId = "admin-client",
            Permissions = new List<string> { BackupPermissions.SystemAdmin }
        };

        // Act
        var result = await _authorizationService.HasPermissionAsync(context, BackupPermissions.UploadBackup);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasPermissionAsync_WithSpecificPermission_ReturnsTrue()
    {
        // Arrange
        var context = new AuthorizationContext
        {
            ClientId = "backup-client",
            Permissions = new List<string> { BackupPermissions.UploadBackup, BackupPermissions.ListBackups }
        };

        // Act
        var result = await _authorizationService.HasPermissionAsync(context, BackupPermissions.UploadBackup);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasPermissionAsync_WithoutPermission_ReturnsFalse()
    {
        // Arrange
        var context = new AuthorizationContext
        {
            ClientId = "limited-client",
            Permissions = new List<string> { BackupPermissions.ViewLogs }
        };

        // Act
        var result = await _authorizationService.HasPermissionAsync(context, BackupPermissions.UploadBackup);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetClientPermissionsAsync_WithExistingClient_ReturnsPermissions()
    {
        // Arrange
        var clientId = "test-client";
        var expectedPermissions = new List<string> { BackupPermissions.UploadBackup, BackupPermissions.ListBackups };
        
        var credentials = new ClientCredentials
        {
            ClientId = clientId,
            Permissions = expectedPermissions
        };

        _mockCredentialStorage.Setup(x => x.GetCredentialsAsync(clientId))
            .ReturnsAsync(credentials);

        // Act
        var result = await _authorizationService.GetClientPermissionsAsync(clientId);

        // Assert
        Assert.Equal(expectedPermissions, result);
    }

    [Fact]
    public async Task GetClientPermissionsAsync_WithNonExistentClient_ReturnsEmptyList()
    {
        // Arrange
        var clientId = "non-existent-client";

        _mockCredentialStorage.Setup(x => x.GetCredentialsAsync(clientId))
            .ReturnsAsync((ClientCredentials?)null);

        // Act
        var result = await _authorizationService.GetClientPermissionsAsync(clientId);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task LogAuthorizationAttemptAsync_WithValidContext_LogsAttempt()
    {
        // Arrange
        var context = new AuthorizationContext
        {
            ClientId = "test-client",
            Permissions = new List<string> { BackupPermissions.UploadBackup },
            RequestTime = DateTime.Now
        };

        _mockAuditService.Setup(x => x.LogAuthenticationEventAsync(It.IsAny<AuthenticationAuditLog>()))
            .Returns(Task.CompletedTask);

        // Act
        await _authorizationService.LogAuthorizationAttemptAsync(context, "upload_backup", true, "Success");

        // Assert
        _mockAuditService.Verify(x => x.LogAuthenticationEventAsync(It.Is<AuthenticationAuditLog>(log => 
            log.ClientId == "test-client" && 
            log.Operation == AuthenticationOperation.PermissionCheck &&
            log.Outcome == AuthenticationOutcome.Success)), Times.Once);
    }

    [Theory]
    [InlineData("upload_backup", true)]
    [InlineData("download_backup", true)]
    [InlineData("unknown_operation", false)]
    public void IsOperationSupported_WithVariousOperations_ReturnsExpectedResult(string operation, bool expected)
    {
        // Act
        var result = _authorizationService.IsOperationSupported(operation);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetRequiredPermissions_WithKnownOperation_ReturnsPermissions()
    {
        // Act
        var result = _authorizationService.GetRequiredPermissions("upload_backup");

        // Assert
        Assert.Contains(BackupPermissions.UploadBackup, result);
    }

    [Fact]
    public void GetRequiredPermissions_WithUnknownOperation_ReturnsEmptyArray()
    {
        // Act
        var result = _authorizationService.GetRequiredPermissions("unknown_operation");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void RegisterOperation_WithValidParameters_RegistersOperation()
    {
        // Arrange
        var operation = "custom_operation";
        var permissions = new[] { BackupPermissions.SystemAdmin };

        // Act
        _authorizationService.RegisterOperation(operation, permissions);

        // Assert
        Assert.True(_authorizationService.IsOperationSupported(operation));
        Assert.Equal(permissions, _authorizationService.GetRequiredPermissions(operation));
    }

    [Fact]
    public void UnregisterOperation_WithExistingOperation_RemovesOperation()
    {
        // Arrange
        var operation = "test_operation";
        var permissions = new[] { BackupPermissions.SystemAdmin };
        _authorizationService.RegisterOperation(operation, permissions);

        // Act
        var result = _authorizationService.UnregisterOperation(operation);

        // Assert
        Assert.True(result);
        Assert.False(_authorizationService.IsOperationSupported(operation));
    }

    [Fact]
    public void GetAllOperations_ReturnsAllRegisteredOperations()
    {
        // Act
        var operations = _authorizationService.GetAllOperations();

        // Assert
        Assert.NotEmpty(operations);
        Assert.Contains("upload_backup", operations.Keys);
        Assert.Contains("download_backup", operations.Keys);
    }
}