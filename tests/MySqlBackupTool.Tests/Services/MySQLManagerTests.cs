using Microsoft.Extensions.Logging;
using Moq;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;

namespace MySqlBackupTool.Tests.Services;

/// <summary>
/// Unit tests for MySQLManager service
/// Tests the IBackupService and IMySQLManager implementations
/// </summary>
public class MySQLManagerTests
{
    private readonly Mock<ILogger<MySQLManager>> _mockLogger;
    private readonly MySQLManager _mysqlManager;

    public MySQLManagerTests()
    {
        _mockLogger = new Mock<ILogger<MySQLManager>>();
        _mysqlManager = new MySQLManager(_mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithValidLogger_ShouldCreateInstance()
    {
        // Arrange & Act
        var manager = new MySQLManager(_mockLogger.Object);

        // Assert
        Assert.NotNull(manager);
        Assert.IsAssignableFrom<IMySQLManager>(manager);
        Assert.IsAssignableFrom<IBackupService>(manager);
    }

    [Fact]
    public async Task StopInstanceAsync_WithNullServiceName_ShouldReturnFalse()
    {
        // Arrange
        string? serviceName = null;

        // Act
        var result = await _mysqlManager.StopInstanceAsync(serviceName!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task StopInstanceAsync_WithEmptyServiceName_ShouldReturnFalse()
    {
        // Arrange
        var serviceName = string.Empty;

        // Act
        var result = await _mysqlManager.StopInstanceAsync(serviceName);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task StartInstanceAsync_WithNullServiceName_ShouldReturnFalse()
    {
        // Arrange
        string? serviceName = null;

        // Act
        var result = await _mysqlManager.StartInstanceAsync(serviceName!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task StartInstanceAsync_WithEmptyServiceName_ShouldReturnFalse()
    {
        // Arrange
        var serviceName = string.Empty;

        // Act
        var result = await _mysqlManager.StartInstanceAsync(serviceName);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task VerifyInstanceAvailabilityAsync_WithNullConnectionInfo_ShouldReturnFalse()
    {
        // Arrange
        MySQLConnectionInfo? connectionInfo = null;

        // Act
        var result = await _mysqlManager.VerifyInstanceAvailabilityAsync(connectionInfo!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task VerifyInstanceAvailabilityAsync_WithInvalidConnectionInfo_ShouldReturnFalse()
    {
        // Arrange
        var connectionInfo = new MySQLConnectionInfo
        {
            ServiceName = "",
            Username = "",
            Password = "",
            DataDirectoryPath = ""
        };

        // Act
        var result = await _mysqlManager.VerifyInstanceAvailabilityAsync(connectionInfo);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StopInstanceAsync_WithInvalidServiceName_ShouldReturnFalse(string serviceName)
    {
        // Act
        var result = await _mysqlManager.StopInstanceAsync(serviceName);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task StopInstanceAsync_WithNullServiceName_ShouldReturnFalse_Theory()
    {
        // Act
        var result = await _mysqlManager.StopInstanceAsync(null!);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StartInstanceAsync_WithInvalidServiceName_ShouldReturnFalse(string serviceName)
    {
        // Act
        var result = await _mysqlManager.StartInstanceAsync(serviceName);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task StartInstanceAsync_WithNullServiceName_ShouldReturnFalse_Theory()
    {
        // Act
        var result = await _mysqlManager.StartInstanceAsync(null!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task StopInstanceAsync_WithValidServiceName_ShouldHandleGracefully()
    {
        // Arrange
        var serviceName = "MySQL80";

        // Act & Assert - Should not throw exception
        var result = await _mysqlManager.StopInstanceAsync(serviceName);
        
        // Result may be true or false depending on system state, but should not throw
        Assert.IsType<bool>(result);
    }

    [Fact]
    public async Task StartInstanceAsync_WithValidServiceName_ShouldHandleGracefully()
    {
        // Arrange
        var serviceName = "MySQL80";

        // Act & Assert - Should not throw exception
        var result = await _mysqlManager.StartInstanceAsync(serviceName);
        
        // Result may be true or false depending on system state, but should not throw
        Assert.IsType<bool>(result);
    }

    [Fact]
    public async Task VerifyInstanceAvailabilityAsync_WithValidConnectionInfo_ShouldHandleGracefully()
    {
        // Arrange
        var connectionInfo = new MySQLConnectionInfo
        {
            ServiceName = "MySQL80",
            Username = "root",
            Password = "password",
            DataDirectoryPath = @"C:\ProgramData\MySQL\MySQL Server 8.0\Data"
        };

        // Act & Assert - Should not throw exception
        var result = await _mysqlManager.VerifyInstanceAvailabilityAsync(connectionInfo);
        
        // Result may be true or false depending on system state, but should not throw
        Assert.IsType<bool>(result);
    }
}