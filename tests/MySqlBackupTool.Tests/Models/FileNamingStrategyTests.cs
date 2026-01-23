using System.ComponentModel.DataAnnotations;
using MySqlBackupTool.Shared.Models;
using Xunit;

namespace MySqlBackupTool.Tests.Models;

public class FileNamingStrategyTests
{
    [Fact]
    public void FileNamingStrategy_ValidStrategy_PassesValidation()
    {
        // Arrange
        var strategy = new FileNamingStrategy
        {
            Pattern = "{timestamp}_{database}_{server}.zip",
            DateFormat = "yyyyMMdd_HHmmss",
            IncludeServerName = true,
            IncludeDatabaseName = true
        };

        // Act
        var validationResults = ValidateModel(strategy);

        // Assert
        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData("", "File naming pattern is required")]
    [InlineData("{timestamp}.zip", null)] // Should pass - has valid placeholder
    [InlineData("backup.zip", "Pattern must contain at least one valid placeholder: {timestamp}, {server}, or {database}")]
    [InlineData("{invalid}.zip", "Pattern must contain at least one valid placeholder: {timestamp}, {server}, or {database}")]
    public void FileNamingStrategy_Pattern_ValidationTests(string pattern, string? expectedError)
    {
        // Arrange
        var strategy = CreateValidStrategy();
        strategy.Pattern = pattern;

        // Act
        var validationResults = ValidateModel(strategy);

        // Assert
        if (expectedError != null)
        {
            Assert.Contains(validationResults, vr => vr.ErrorMessage == expectedError);
        }
        else
        {
            Assert.DoesNotContain(validationResults, vr => vr.MemberNames.Contains(nameof(FileNamingStrategy.Pattern)));
        }
    }

    [Theory]
    [InlineData("", "Date format is required")]
    [InlineData("yyyyMMdd", null)] // Should pass
    [InlineData("yyyy-MM-dd HH:mm:ss", null)] // Should pass
    [InlineData("abc", "Invalid date format: 'abc' - must contain valid date/time format specifiers")]
    public void FileNamingStrategy_DateFormat_ValidationTests(string dateFormat, string? expectedErrorContains)
    {
        // Arrange
        var strategy = CreateValidStrategy();
        strategy.DateFormat = dateFormat;

        // Act
        var validationResults = ValidateModel(strategy);

        // Assert
        if (expectedErrorContains != null)
        {
            Assert.Contains(validationResults, vr => vr.ErrorMessage!.Contains(expectedErrorContains));
        }
        else
        {
            Assert.DoesNotContain(validationResults, vr => vr.MemberNames.Contains(nameof(FileNamingStrategy.DateFormat)));
        }
    }

    [Fact]
    public void FileNamingStrategy_CustomValidation_InvalidFilenameCharacters_ReturnsError()
    {
        // Arrange
        var strategy = CreateValidStrategy();
        strategy.Pattern = "{timestamp}|{database}.zip"; // Contains invalid character |

        // Act
        var validationResults = ValidateModel(strategy);

        // Assert
        Assert.Contains(validationResults, vr => 
            vr.ErrorMessage!.Contains("Pattern contains invalid filename characters") && 
            vr.MemberNames.Contains(nameof(FileNamingStrategy.Pattern)));
    }

    [Fact]
    public void FileNamingStrategy_CustomValidation_EmptyPatternResult_ReturnsError()
    {
        // Arrange
        var strategy = CreateValidStrategy();
        strategy.Pattern = "___"; // Would result in empty filename after cleanup

        // Act
        var validationResults = ValidateModel(strategy);

        // Assert
        Assert.Contains(validationResults, vr => 
            vr.ErrorMessage!.Contains("Pattern would result in empty or invalid filename") && 
            vr.MemberNames.Contains(nameof(FileNamingStrategy.Pattern)));
    }

    [Fact]
    public void FileNamingStrategy_CustomValidation_InconsistentServerSettings_ReturnsError()
    {
        // Arrange
        var strategy = CreateValidStrategy();
        strategy.Pattern = "{timestamp}_{server}.zip";
        strategy.IncludeServerName = false; // Inconsistent with pattern

        // Act
        var validationResults = ValidateModel(strategy);

        // Assert
        Assert.Contains(validationResults, vr => 
            vr.ErrorMessage!.Contains("Pattern contains {server} placeholder but IncludeServerName is false"));
    }

    [Fact]
    public void FileNamingStrategy_CustomValidation_InconsistentDatabaseSettings_ReturnsError()
    {
        // Arrange
        var strategy = CreateValidStrategy();
        strategy.Pattern = "{timestamp}_{database}.zip";
        strategy.IncludeDatabaseName = false; // Inconsistent with pattern

        // Act
        var validationResults = ValidateModel(strategy);

        // Assert
        Assert.Contains(validationResults, vr => 
            vr.ErrorMessage!.Contains("Pattern contains {database} placeholder but IncludeDatabaseName is false"));
    }

    [Fact]
    public void FileNamingStrategy_GenerateFileName_ValidInputs_ReturnsCorrectFormat()
    {
        // Arrange
        var strategy = new FileNamingStrategy
        {
            Pattern = "{timestamp}_{database}_{server}.zip",
            DateFormat = "yyyyMMdd_HHmmss",
            IncludeServerName = true,
            IncludeDatabaseName = true
        };
        var timestamp = new DateTime(2024, 1, 15, 14, 30, 45);

        // Act
        var filename = strategy.GenerateFileName("TestServer", "TestDB", timestamp);

        // Assert
        Assert.Equal("20240115_143045_TestDB_TestServer.zip", filename);
    }

    [Fact]
    public void FileNamingStrategy_GenerateFileName_ExcludeServerName_ReturnsCorrectFormat()
    {
        // Arrange
        var strategy = new FileNamingStrategy
        {
            Pattern = "{timestamp}_{database}_{server}.zip",
            DateFormat = "yyyyMMdd_HHmmss",
            IncludeServerName = false,
            IncludeDatabaseName = true
        };
        var timestamp = new DateTime(2024, 1, 15, 14, 30, 45);

        // Act
        var filename = strategy.GenerateFileName("TestServer", "TestDB", timestamp);

        // Assert
        Assert.Equal("20240115_143045_TestDB.zip", filename);
    }

    [Fact]
    public void FileNamingStrategy_GenerateFileName_ExcludeDatabaseName_ReturnsCorrectFormat()
    {
        // Arrange
        var strategy = new FileNamingStrategy
        {
            Pattern = "{timestamp}_{database}_{server}.zip",
            DateFormat = "yyyyMMdd_HHmmss",
            IncludeServerName = true,
            IncludeDatabaseName = false
        };
        var timestamp = new DateTime(2024, 1, 15, 14, 30, 45);

        // Act
        var filename = strategy.GenerateFileName("TestServer", "TestDB", timestamp);

        // Assert
        Assert.Equal("20240115_143045_TestServer.zip", filename);
    }

    [Fact]
    public void FileNamingStrategy_GenerateFileName_InvalidCharactersInNames_SanitizesCorrectly()
    {
        // Arrange
        var strategy = new FileNamingStrategy
        {
            Pattern = "{timestamp}_{database}_{server}.zip",
            DateFormat = "yyyyMMdd_HHmmss",
            IncludeServerName = true,
            IncludeDatabaseName = true
        };
        var timestamp = new DateTime(2024, 1, 15, 14, 30, 45);

        // Act
        var filename = strategy.GenerateFileName("Test|Server", "Test<DB>", timestamp);

        // Assert
        Assert.Equal("20240115_143045_TestDB_TestServer.zip", filename);
    }

    [Fact]
    public void FileNamingStrategy_GenerateFileName_EmptyNames_UsesUnknown()
    {
        // Arrange
        var strategy = new FileNamingStrategy
        {
            Pattern = "{timestamp}_{database}_{server}.zip",
            DateFormat = "yyyyMMdd_HHmmss",
            IncludeServerName = true,
            IncludeDatabaseName = true
        };
        var timestamp = new DateTime(2024, 1, 15, 14, 30, 45);

        // Act
        var filename = strategy.GenerateFileName("", "", timestamp);

        // Assert
        Assert.Equal("20240115_143045_unknown_unknown.zip", filename);
    }

    [Fact]
    public void FileNamingStrategy_ValidateStrategy_ValidStrategy_ReturnsTrue()
    {
        // Arrange
        var strategy = CreateValidStrategy();

        // Act
        var (isValid, errors) = strategy.ValidateStrategy();

        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void FileNamingStrategy_ValidateStrategy_InvalidDateFormat_ReturnsFalse()
    {
        // Arrange
        var strategy = CreateValidStrategy();
        strategy.DateFormat = "abc";

        // Act
        var (isValid, errors) = strategy.ValidateStrategy();

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("Invalid date format 'abc': must contain valid date/time format specifiers"));
    }

    [Fact]
    public void FileNamingStrategy_TestUniqueness_DifferentTimestamps_ReturnsTrue()
    {
        // Arrange
        var strategy = new FileNamingStrategy
        {
            Pattern = "{timestamp}_{database}_{server}.zip",
            DateFormat = "yyyyMMdd_HHmmss",
            IncludeServerName = true,
            IncludeDatabaseName = true
        };

        // Act
        var isUnique = strategy.TestUniqueness("TestServer", "TestDB", 5);

        // Assert
        Assert.True(isUnique);
    }

    [Fact]
    public void FileNamingStrategy_TestUniqueness_SameTimestamp_ReturnsFalse()
    {
        // Arrange
        var strategy = new FileNamingStrategy
        {
            Pattern = "{database}_{server}.zip", // No timestamp, will generate duplicates
            DateFormat = "yyyyMMdd_HHmmss",
            IncludeServerName = true,
            IncludeDatabaseName = true
        };

        // Act
        var isUnique = strategy.TestUniqueness("TestServer", "TestDB", 2);

        // Assert
        Assert.False(isUnique);
    }

    private static FileNamingStrategy CreateValidStrategy()
    {
        return new FileNamingStrategy
        {
            Pattern = "{timestamp}_{database}_{server}.zip",
            DateFormat = "yyyyMMdd_HHmmss",
            IncludeServerName = true,
            IncludeDatabaseName = true
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