using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MySqlBackupTool.Client.Forms;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Reflection;
using Xunit;

namespace MySqlBackupTool.Tests.Forms;

/// <summary>
/// Unit tests for BackupMonitorForm error handling functionality
/// Tests user-friendly error message display for authentication failures
/// **Validates: Requirements 4.4**
/// </summary>
public class BackupMonitorFormTests
{
    /// <summary>
    /// Creates a BackupMonitorForm instance for testing with mocked dependencies
    /// </summary>
    /// <returns>BackupMonitorForm instance</returns>
    private BackupMonitorForm CreateFormForTesting()
    {
        // Create service collection and add required services
        var services = new ServiceCollection();
        
        // Add mocked logger
        var mockLogger = new Mock<ILogger<BackupMonitorForm>>();
        services.AddSingleton(mockLogger.Object);
        
        // Add mocked repositories
        var mockConfigRepository = new Mock<IBackupConfigurationRepository>();
        mockConfigRepository.Setup(r => r.GetActiveConfigurationsAsync())
            .ReturnsAsync(new List<BackupConfiguration>());
        services.AddSingleton(mockConfigRepository.Object);
        
        var mockLogRepository = new Mock<IBackupLogRepository>();
        mockLogRepository.Setup(r => r.GetRunningBackupsAsync())
            .ReturnsAsync(new List<BackupLog>());
        services.AddSingleton(mockLogRepository.Object);
        
        // Add optional backup orchestrator
        var mockBackupOrchestrator = new Mock<IBackupOrchestrator>();
        services.AddSingleton(mockBackupOrchestrator.Object);
        
        var serviceProvider = services.BuildServiceProvider();
        return new BackupMonitorForm(serviceProvider);
    }

    [Fact]
    public void GetUserFriendlyErrorMessage_AuthenticationTokenFailure_ReturnsUserFriendlyMessage()
    {
        // Arrange
        using var form = CreateFormForTesting();
        var exception = new Exception("Failed to obtain authentication token");

        // Act
        var result = InvokePrivateMethod<string>(form, "GetUserFriendlyErrorMessage", exception);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("身份验证失败：无法获取身份验证令牌", result);
        Assert.Contains("客户端凭据配置不正确", result);
        Assert.Contains("检查备份配置中的客户端ID和密钥设置", result);
        Assert.Contains("重新启动应用程序以重新初始化默认凭据", result);
    }

    [Fact]
    public void GetUserFriendlyErrorMessage_InvalidCredentials_ReturnsUserFriendlyMessage()
    {
        // Arrange
        using var form = CreateFormForTesting();
        var exception = new Exception("Invalid credentials provided");

        // Act
        var result = InvokePrivateMethod<string>(form, "GetUserFriendlyErrorMessage", exception);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("身份验证失败：提供的凭据无效", result);
        Assert.Contains("客户端ID或密钥不正确", result);
        Assert.Contains("验证备份配置中的客户端凭据", result);
        Assert.Contains("联系系统管理员检查服务器端凭据设置", result);
    }

    [Fact]
    public void GetUserFriendlyErrorMessage_MalformedToken_ReturnsUserFriendlyMessage()
    {
        // Arrange
        using var form = CreateFormForTesting();
        var exception = new Exception("Authentication token is malformed");

        // Act
        var result = InvokePrivateMethod<string>(form, "GetUserFriendlyErrorMessage", exception);

        // Assert
        Assert.NotNull(result);
        // The message "Authentication token is malformed" contains "authentication token" 
        // so it matches the token failure pattern, not the malformed pattern
        Assert.Contains("身份验证失败：无法获取身份验证令牌", result);
        Assert.Contains("客户端凭据配置不正确", result);
        Assert.Contains("检查备份配置中的客户端ID和密钥设置", result);
    }

    [Fact]
    public void GetUserFriendlyErrorMessage_ActualMalformedToken_ReturnsUserFriendlyMessage()
    {
        // Arrange
        using var form = CreateFormForTesting();
        var exception = new Exception("Token format is malformed");

        // Act
        var result = InvokePrivateMethod<string>(form, "GetUserFriendlyErrorMessage", exception);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("身份验证失败：令牌格式错误", result);
        Assert.Contains("凭据编码过程中出现问题", result);
        Assert.Contains("重新启动客户端应用程序", result);
        Assert.Contains("检查客户端和服务器是否为相同版本", result);
    }

    [Fact]
    public void GetUserFriendlyErrorMessage_ConnectionError_ReturnsUserFriendlyMessage()
    {
        // Arrange
        using var form = CreateFormForTesting();
        var exception = new Exception("Connection timeout occurred");

        // Act
        var result = InvokePrivateMethod<string>(form, "GetUserFriendlyErrorMessage", exception);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("连接错误：无法连接到备份服务器", result);
        Assert.Contains("服务器未运行或不可访问", result);
        Assert.Contains("确认备份服务器正在运行", result);
        Assert.Contains("检查网络连接和服务器地址配置", result);
    }

    [Fact]
    public void GetUserFriendlyErrorMessage_PermissionError_ReturnsUserFriendlyMessage()
    {
        // Arrange
        using var form = CreateFormForTesting();
        var exception = new Exception("Access denied to backup directory");

        // Act
        var result = InvokePrivateMethod<string>(form, "GetUserFriendlyErrorMessage", exception);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("权限错误：没有执行此操作的权限", result);
        Assert.Contains("客户端权限不足", result);
        Assert.Contains("联系系统管理员检查客户端权限", result);
        Assert.Contains("尝试以管理员身份运行应用程序", result);
    }

    [Fact]
    public void GetUserFriendlyErrorMessage_GenericError_ReturnsUserFriendlyMessage()
    {
        // Arrange
        using var form = CreateFormForTesting();
        var exception = new Exception("Some unexpected error occurred");

        // Act
        var result = InvokePrivateMethod<string>(form, "GetUserFriendlyErrorMessage", exception);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("操作失败：Some unexpected error occurred", result);
        Assert.Contains("检查应用程序日志获取详细信息", result);
        Assert.Contains("确认所有服务正常运行", result);
        Assert.Contains("联系技术支持获取帮助", result);
    }

    [Fact]
    public void GetUserFriendlyBackupErrorMessage_AuthenticationTokenFailure_ReturnsUserFriendlyMessage()
    {
        // Arrange
        using var form = CreateFormForTesting();
        var errorMessage = "Failed to obtain authentication token";

        // Act
        var result = InvokePrivateMethod<string>(form, "GetUserFriendlyBackupErrorMessage", errorMessage);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("身份验证失败：无法获取身份验证令牌", result);
        Assert.Contains("客户端凭据配置不正确", result);
        Assert.Contains("检查备份配置中的客户端ID和密钥设置", result);
    }

    [Fact]
    public void GetUserFriendlyBackupErrorMessage_InvalidCredentials_ReturnsUserFriendlyMessage()
    {
        // Arrange
        using var form = CreateFormForTesting();
        var errorMessage = "Authentication failed - invalid credentials";

        // Act
        var result = InvokePrivateMethod<string>(form, "GetUserFriendlyBackupErrorMessage", errorMessage);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("身份验证失败：提供的凭据无效", result);
        Assert.Contains("客户端ID或密钥不正确", result);
        Assert.Contains("验证备份配置中的客户端凭据", result);
    }

    [Fact]
    public void GetUserFriendlyBackupErrorMessage_EmptyMessage_ReturnsDefaultMessage()
    {
        // Arrange
        using var form = CreateFormForTesting();
        string? errorMessage = null;

        // Act
        var result = InvokePrivateMethod<string>(form, "GetUserFriendlyBackupErrorMessage", errorMessage);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("备份操作失败，但未提供具体错误信息", result);
        Assert.Contains("检查应用程序日志获取详细信息", result);
        Assert.Contains("确认备份服务器正常运行", result);
    }

    [Fact]
    public void GetUserFriendlyBackupErrorMessage_GenericError_ReturnsUserFriendlyMessage()
    {
        // Arrange
        using var form = CreateFormForTesting();
        var errorMessage = "Database connection failed";

        // Act
        var result = InvokePrivateMethod<string>(form, "GetUserFriendlyBackupErrorMessage", errorMessage);

        // Assert
        Assert.NotNull(result);
        // The message "Database connection failed" contains "connection" 
        // so it matches the connection error pattern, not the generic pattern
        Assert.Contains("连接错误：无法连接到备份服务器", result);
        Assert.Contains("服务器未运行或不可访问", result);
        Assert.Contains("确认备份服务器正在运行", result);
    }

    [Fact]
    public void GetUserFriendlyBackupErrorMessage_ActualGenericError_ReturnsUserFriendlyMessage()
    {
        // Arrange
        using var form = CreateFormForTesting();
        var errorMessage = "Some unexpected system error occurred";

        // Act
        var result = InvokePrivateMethod<string>(form, "GetUserFriendlyBackupErrorMessage", errorMessage);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("备份失败：Some unexpected system error occurred", result);
        Assert.Contains("检查应用程序日志获取详细信息", result);
        Assert.Contains("确认所有服务正常运行", result);
    }

    [Theory]
    [InlineData("备份失败，Failed to obtain authentication token")]
    [InlineData("AUTHENTICATION TOKEN ERROR")]
    [InlineData("failed to obtain authentication token")]
    public void GetUserFriendlyErrorMessage_ChineseAndEnglishAuthErrors_ReturnsConsistentMessage(string errorMessage)
    {
        // Arrange
        using var form = CreateFormForTesting();
        var exception = new Exception(errorMessage);

        // Act
        var result = InvokePrivateMethod<string>(form, "GetUserFriendlyErrorMessage", exception);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("身份验证失败：无法获取身份验证令牌", result);
        Assert.Contains("建议解决方案", result);
    }

    [Theory]
    [InlineData("Invalid credentials")]
    [InlineData("AUTHENTICATION FAILED")]
    [InlineData("凭据无效")]
    [InlineData("身份验证失败")]
    public void GetUserFriendlyErrorMessage_VariousCredentialErrors_ReturnsConsistentMessage(string errorMessage)
    {
        // Arrange
        using var form = CreateFormForTesting();
        var exception = new Exception(errorMessage);

        // Act
        var result = InvokePrivateMethod<string>(form, "GetUserFriendlyErrorMessage", exception);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("身份验证失败：提供的凭据无效", result);
        Assert.Contains("建议解决方案", result);
    }

    /// <summary>
    /// Helper method to invoke private methods using reflection for testing
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="obj">Object instance</param>
    /// <param name="methodName">Method name</param>
    /// <param name="parameters">Method parameters</param>
    /// <returns>Method result</returns>
    private T InvokePrivateMethod<T>(object obj, string methodName, params object?[] parameters)
    {
        var type = obj.GetType();
        var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        
        Assert.NotNull(method);
        
        var result = method.Invoke(obj, parameters);
        return (T)result!;
    }
}