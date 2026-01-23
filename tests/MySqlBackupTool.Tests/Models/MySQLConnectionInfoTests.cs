using System.ComponentModel.DataAnnotations;
using MySqlBackupTool.Shared.Models;
using Xunit;

namespace MySqlBackupTool.Tests.Models;

public class MySQLConnectionInfoTests
{
    [Fact]
    public void MySQLConnectionInfo_ValidConnection_PassesValidation()
    {
        // Arrange
        var connectionInfo = new MySQLConnectionInfo
        {
            Username = "testuser",
            Password = "testpass",
            ServiceName = "mysql",
            DataDirectoryPath = @"C:\temp",
            Host = "localhost",
            Port = 3306
        };

        // Act
        var validationResults = ValidateModel(connectionInfo);

        // Assert
        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData("", "Username is required")]
    [InlineData("user'name", "Username contains invalid characters")]
    [InlineData("user\"name", "Username contains invalid characters")]
    [InlineData("user\\name", "Username contains invalid characters")]
    [InlineData("validuser", null)] // Should pass
    public void MySQLConnectionInfo_Username_ValidationTests(string username, string? expectedError)
    {
        // Arrange
        var connectionInfo = CreateValidConnectionInfo();
        connectionInfo.Username = username;

        // Act
        var validationResults = ValidateModel(connectionInfo);

        // Assert
        if (expectedError != null)
        {
            Assert.Contains(validationResults, vr => vr.ErrorMessage == expectedError);
        }
        else
        {
            Assert.DoesNotContain(validationResults, vr => vr.MemberNames.Contains(nameof(MySQLConnectionInfo.Username)));
        }
    }

    [Theory]
    [InlineData("", "Password is required")]
    [InlineData("validpass", null)] // Should pass
    public void MySQLConnectionInfo_Password_ValidationTests(string password, string? expectedError)
    {
        // Arrange
        var connectionInfo = CreateValidConnectionInfo();
        connectionInfo.Password = password;

        // Act
        var validationResults = ValidateModel(connectionInfo);

        // Assert
        if (expectedError != null)
        {
            Assert.Contains(validationResults, vr => vr.ErrorMessage == expectedError);
        }
        else
        {
            Assert.DoesNotContain(validationResults, vr => vr.MemberNames.Contains(nameof(MySQLConnectionInfo.Password)));
        }
    }

    [Theory]
    [InlineData("", "Service name is required")]
    [InlineData("mysql@service", "Service name can only contain letters, numbers, hyphens, and underscores")]
    [InlineData("mysql-service", null)] // Should pass
    [InlineData("mysql_service", null)] // Should pass
    [InlineData("mysql80", null)] // Should pass
    public void MySQLConnectionInfo_ServiceName_ValidationTests(string serviceName, string? expectedError)
    {
        // Arrange
        var connectionInfo = CreateValidConnectionInfo();
        connectionInfo.ServiceName = serviceName;

        // Act
        var validationResults = ValidateModel(connectionInfo);

        // Assert
        if (expectedError != null)
        {
            Assert.Contains(validationResults, vr => vr.ErrorMessage == expectedError);
        }
        else
        {
            Assert.DoesNotContain(validationResults, vr => vr.MemberNames.Contains(nameof(MySQLConnectionInfo.ServiceName)));
        }
    }

    [Theory]
    [InlineData(0, "Port must be between 1 and 65535")]
    [InlineData(65536, "Port must be between 1 and 65535")]
    [InlineData(3306, null)] // Should pass
    [InlineData(1, null)] // Should pass
    [InlineData(65535, null)] // Should pass
    public void MySQLConnectionInfo_Port_ValidationTests(int port, string? expectedError)
    {
        // Arrange
        var connectionInfo = CreateValidConnectionInfo();
        connectionInfo.Port = port;

        // Act
        var validationResults = ValidateModel(connectionInfo);

        // Assert
        if (expectedError != null)
        {
            Assert.Contains(validationResults, vr => vr.ErrorMessage == expectedError);
        }
        else
        {
            Assert.DoesNotContain(validationResults, vr => vr.MemberNames.Contains(nameof(MySQLConnectionInfo.Port)));
        }
    }

    [Theory]
    [InlineData("", "Host is required")]
    [InlineData("local host", "Host cannot contain whitespace characters")]
    [InlineData("local\thost", "Host cannot contain whitespace characters")]
    [InlineData("local\nhost", "Host cannot contain whitespace characters")]
    [InlineData(".localhost", "Host format is invalid")]
    [InlineData("localhost.", "Host format is invalid")]
    [InlineData("local..host", "Host format is invalid")]
    [InlineData("localhost", null)] // Should pass
    [InlineData("192.168.1.1", null)] // Should pass
    public void MySQLConnectionInfo_Host_ValidationTests(string host, string? expectedError)
    {
        // Arrange
        var connectionInfo = CreateValidConnectionInfo();
        connectionInfo.Host = host;

        // Act
        var validationResults = ValidateModel(connectionInfo);

        // Assert
        if (expectedError != null)
        {
            Assert.Contains(validationResults, vr => vr.ErrorMessage == expectedError);
        }
        else
        {
            Assert.DoesNotContain(validationResults, vr => vr.MemberNames.Contains(nameof(MySQLConnectionInfo.Host)));
        }
    }

    [Fact]
    public void MySQLConnectionInfo_CustomValidation_InvalidDataDirectoryPath_ReturnsError()
    {
        // Arrange
        var connectionInfo = CreateValidConnectionInfo();
        connectionInfo.DataDirectoryPath = "C:\\path\\with\\null\0character";

        // Act
        var validationResults = ValidateModel(connectionInfo);

        // Assert
        Assert.Contains(validationResults, vr => 
            vr.ErrorMessage!.Contains("Invalid data directory path format") && 
            vr.MemberNames.Contains(nameof(MySQLConnectionInfo.DataDirectoryPath)));
    }

    [Fact]
    public void MySQLConnectionInfo_GetConnectionString_ReturnsCorrectFormat()
    {
        // Arrange
        var connectionInfo = new MySQLConnectionInfo
        {
            Username = "testuser",
            Password = "testpass",
            Host = "localhost",
            Port = 3306
        };

        // Act
        var connectionString = connectionInfo.GetConnectionString();

        // Assert
        Assert.Equal("Server=localhost;Port=3306;Database=mysql;Uid=testuser;Pwd=testpass;ConnectionTimeout=30;", connectionString);
    }

    [Fact]
    public async Task MySQLConnectionInfo_ValidateConnectionAsync_InvalidConnection_ReturnsFalse()
    {
        // Arrange
        var connectionInfo = new MySQLConnectionInfo
        {
            Username = "invaliduser",
            Password = "invalidpass",
            Host = "nonexistenthost",
            Port = 3306,
            ServiceName = "mysql",
            DataDirectoryPath = @"C:\temp"
        };

        // Act
        var (isValid, errors) = await connectionInfo.ValidateConnectionAsync();

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("MySQL connection failed"));
    }

    [Fact]
    public async Task MySQLConnectionInfo_TestServiceAccessibilityAsync_InvalidHost_ReturnsFalse()
    {
        // Arrange
        var connectionInfo = new MySQLConnectionInfo
        {
            Username = "testuser",
            Password = "testpass",
            Host = "nonexistenthost.invalid",
            Port = 3306,
            ServiceName = "mysql",
            DataDirectoryPath = @"C:\temp"
        };

        // Act
        var isAccessible = await connectionInfo.TestServiceAccessibilityAsync(1); // 1 second timeout

        // Assert
        Assert.False(isAccessible);
    }

    private static MySQLConnectionInfo CreateValidConnectionInfo()
    {
        // Create a temporary directory for testing
        var tempDir = Path.Combine(Path.GetTempPath(), "MySqlBackupToolTest");
        Directory.CreateDirectory(tempDir);
        
        return new MySQLConnectionInfo
        {
            Username = "testuser",
            Password = "testpass",
            ServiceName = "mysql",
            DataDirectoryPath = tempDir,
            Host = "localhost",
            Port = 3306
        };
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }
}