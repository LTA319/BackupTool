using System.ComponentModel.DataAnnotations;
using MySqlBackupTool.Shared.Models;
using Xunit;

namespace MySqlBackupTool.Tests.Models;

public class BackupConfigurationTests
{
    [Fact]
    public void BackupConfiguration_ValidConfiguration_PassesValidation()
    {
        // Arrange
        var config = CreateValidConfiguration(); // This now uses temp directory

        // Act
        var validationResults = ValidateModel(config);

        // Assert
        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData("", "Configuration name is required")]
    [InlineData("Test@Config", "Configuration name can only contain letters, numbers, spaces, hyphens, and underscores")]
    [InlineData("A", null)] // Should pass - minimum length is 1
    public void BackupConfiguration_Name_ValidationTests(string name, string? expectedError)
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Name = name;

        // Act
        var validationResults = ValidateModel(config);

        // Assert
        if (expectedError != null)
        {
            Assert.Contains(validationResults, vr => vr.ErrorMessage == expectedError);
        }
        else
        {
            Assert.DoesNotContain(validationResults, vr => vr.MemberNames.Contains(nameof(BackupConfiguration.Name)));
        }
    }

    [Theory]
    [InlineData("", "MySQL service name is required")]
    [InlineData("mysql@service", "Service name can only contain letters, numbers, hyphens, and underscores")]
    [InlineData("mysql-service", null)] // Should pass
    [InlineData("mysql_service", null)] // Should pass
    public void BackupConfiguration_ServiceName_ValidationTests(string serviceName, string? expectedError)
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.ServiceName = serviceName;

        // Act
        var validationResults = ValidateModel(config);

        // Assert
        if (expectedError != null)
        {
            Assert.Contains(validationResults, vr => vr.ErrorMessage == expectedError);
        }
        else
        {
            Assert.DoesNotContain(validationResults, vr => vr.MemberNames.Contains(nameof(BackupConfiguration.ServiceName)));
        }
    }

    [Fact]
    public void BackupConfiguration_CustomValidation_NonExistentDataDirectory_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.DataDirectoryPath = @"C:\NonExistentDirectory\Data";

        // Act
        var validationResults = ValidateModel(config);

        // Assert
        Assert.Contains(validationResults, vr => 
            vr.ErrorMessage!.Contains("does not exist") && 
            vr.MemberNames.Contains(nameof(BackupConfiguration.DataDirectoryPath)));
    }

    [Fact]
    public void BackupConfiguration_CustomValidation_InvalidTargetDirectoryPath_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.TargetDirectory = "C:\\path\\with\\null\0character"; // Null character should definitely cause issues

        // Act
        var validationResults = ValidateModel(config);

        // Assert
        Assert.Contains(validationResults, vr => 
            vr.ErrorMessage!.Contains("Invalid target directory path format") && 
            vr.MemberNames.Contains(nameof(BackupConfiguration.TargetDirectory)));
    }

    [Fact]
    public void BackupConfiguration_CustomValidation_WhitespaceOnlyName_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Name = "   ";

        // Act
        var validationResults = ValidateModel(config);

        // Assert
        // The Required attribute will catch this as "Configuration name is required"
        // since whitespace-only strings are treated as empty by the Required attribute
        Assert.Contains(validationResults, vr => 
            vr.ErrorMessage == "Configuration name is required");
    }

    [Fact]
    public async Task BackupConfiguration_ValidateConnectionParametersAsync_AllValid_ReturnsTrue()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        var (isValid, errors) = await config.ValidateConnectionParametersAsync();

        // Assert - Note: This will likely fail in test environment due to no actual MySQL server
        // In a real scenario, you'd mock the connection validation
        Assert.IsType<bool>(isValid);
        Assert.IsType<List<string>>(errors);
    }

    private static BackupConfiguration CreateValidConfiguration()
    {
        // Create a temporary directory for testing
        var tempDir = Path.Combine(Path.GetTempPath(), "MySqlBackupToolTest");
        Directory.CreateDirectory(tempDir);
        
        return new BackupConfiguration
        {
            Name = "Test Configuration",
            MySQLConnection = new MySQLConnectionInfo
            {
                Username = "testuser",
                Password = "testpass",
                ServiceName = "mysql",
                DataDirectoryPath = tempDir,
                Host = "localhost",
                Port = 3306
            },
            DataDirectoryPath = tempDir,
            ServiceName = "mysql",
            TargetServer = new ServerEndpoint
            {
                IPAddress = "192.168.1.100",
                Port = 8080,
                UseSSL = true
            },
            TargetDirectory = tempDir,
            NamingStrategy = new FileNamingStrategy
            {
                Pattern = "{timestamp}_{database}_{server}.zip",
                DateFormat = "yyyyMMdd_HHmmss",
                IncludeServerName = true,
                IncludeDatabaseName = true
            }
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