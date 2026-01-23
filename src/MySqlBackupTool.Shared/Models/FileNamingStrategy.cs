using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// Strategy for naming backup files
/// </summary>
public class FileNamingStrategy : IValidatableObject
{
    [Required(ErrorMessage = "File naming pattern is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Pattern must be between 1 and 200 characters")]
    public string Pattern { get; set; } = "{timestamp}_{database}_{server}.zip";

    [Required(ErrorMessage = "Date format is required")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Date format must be between 1 and 50 characters")]
    public string DateFormat { get; set; } = "yyyyMMdd_HHmmss";

    public bool IncludeServerName { get; set; } = true;

    public bool IncludeDatabaseName { get; set; } = true;

    /// <summary>
    /// Generates a filename based on the strategy and provided parameters
    /// </summary>
    /// <param name="serverName">Name of the server</param>
    /// <param name="databaseName">Name of the database</param>
    /// <param name="timestamp">Timestamp for the backup</param>
    /// <returns>Generated filename</returns>
    public string GenerateFileName(string serverName, string databaseName, DateTime timestamp)
    {
        var fileName = Pattern;
        
        // Replace timestamp first
        fileName = fileName.Replace("{timestamp}", timestamp.ToString(DateFormat));
        
        // Handle server name
        if (IncludeServerName)
        {
            if (!string.IsNullOrWhiteSpace(serverName))
            {
                fileName = fileName.Replace("{server}", SanitizeFileName(serverName));
            }
            else
            {
                fileName = fileName.Replace("{server}", "unknown");
            }
        }
        else
        {
            // Remove the server placeholder and any adjacent separators
            fileName = RemovePlaceholderAndSeparators(fileName, "{server}");
        }
        
        // Handle database name
        if (IncludeDatabaseName)
        {
            if (!string.IsNullOrWhiteSpace(databaseName))
            {
                fileName = fileName.Replace("{database}", SanitizeFileName(databaseName));
            }
            else
            {
                fileName = fileName.Replace("{database}", "unknown");
            }
        }
        else
        {
            // Remove the database placeholder and any adjacent separators
            fileName = RemovePlaceholderAndSeparators(fileName, "{database}");
        }

        // Final cleanup
        fileName = CleanupFileName(fileName);
        
        return fileName;
    }

    /// <summary>
    /// Removes a placeholder and any adjacent separators to avoid double separators
    /// </summary>
    private static string RemovePlaceholderAndSeparators(string input, string placeholder)
    {
        // Try different patterns: _placeholder, placeholder_, _placeholder_
        var patterns = new[]
        {
            $"_{placeholder}_", // Middle placeholder with separators on both sides
            $"_{placeholder}",  // Placeholder with leading separator
            $"{placeholder}_",  // Placeholder with trailing separator
            placeholder         // Just the placeholder
        };

        foreach (var pattern in patterns)
        {
            if (input.Contains(pattern))
            {
                // For middle pattern, replace with single separator
                if (pattern == $"_{placeholder}_")
                {
                    input = input.Replace(pattern, "_");
                }
                else
                {
                    input = input.Replace(pattern, "");
                }
                break;
            }
        }

        return input;
    }

    /// <summary>
    /// Cleans up the filename by removing double separators and trimming
    /// </summary>
    private static string CleanupFileName(string fileName)
    {
        // Remove double underscores
        while (fileName.Contains("__"))
        {
            fileName = fileName.Replace("__", "_");
        }
        
        // Remove double hyphens
        while (fileName.Contains("--"))
        {
            fileName = fileName.Replace("--", "-");
        }
        
        // Trim separators from start and end
        fileName = fileName.Trim('_', '-', ' ', '.');
        
        return fileName;
    }

    /// <summary>
    /// Sanitizes a string to be safe for use in filenames
    /// </summary>
    private static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "unknown";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(input.Where(c => !invalidChars.Contains(c)).ToArray());
        
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }

    /// <summary>
    /// Performs custom validation logic for the file naming strategy
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();

        // Validate date format
        if (!string.IsNullOrWhiteSpace(DateFormat))
        {
            try
            {
                var testDate = DateTime.Now;
                var formatted = testDate.ToString(DateFormat);
                
                // Additional validation - check if the format contains valid format specifiers
                var validFormatChars = new[] { 'y', 'M', 'd', 'H', 'h', 'm', 's', 'f', 'F', 't', 'z', 'K' };
                var hasValidFormatChar = DateFormat.Any(c => validFormatChars.Contains(c));
                
                if (!hasValidFormatChar)
                {
                    results.Add(new ValidationResult(
                        $"Invalid date format: '{DateFormat}' - must contain valid date/time format specifiers",
                        new[] { nameof(DateFormat) }));
                }
            }
            catch (FormatException)
            {
                results.Add(new ValidationResult(
                    $"Invalid date format: '{DateFormat}'",
                    new[] { nameof(DateFormat) }));
            }
        }

        // Validate pattern contains valid placeholders
        if (!string.IsNullOrWhiteSpace(Pattern))
        {
            var validPlaceholders = new[] { "{timestamp}", "{server}", "{database}" };
            var hasValidPlaceholder = validPlaceholders.Any(p => Pattern.Contains(p));
            
            if (!hasValidPlaceholder)
            {
                results.Add(new ValidationResult(
                    "Pattern must contain at least one valid placeholder: {timestamp}, {server}, or {database}",
                    new[] { nameof(Pattern) }));
            }

            // Check for invalid filename characters in the pattern (excluding placeholders)
            var patternWithoutPlaceholders = Pattern;
            foreach (var placeholder in validPlaceholders)
            {
                patternWithoutPlaceholders = patternWithoutPlaceholders.Replace(placeholder, "X");
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var foundInvalidChars = patternWithoutPlaceholders.Where(c => invalidChars.Contains(c)).ToList();
            
            if (foundInvalidChars.Any())
            {
                results.Add(new ValidationResult(
                    $"Pattern contains invalid filename characters: {string.Join(", ", foundInvalidChars.Distinct())}",
                    new[] { nameof(Pattern) }));
            }

            // Validate pattern doesn't result in empty filename
            if (Pattern.Trim().Replace("{timestamp}", "").Replace("{server}", "").Replace("{database}", "").Trim('_', '-', ' ').Length == 0)
            {
                results.Add(new ValidationResult(
                    "Pattern would result in empty or invalid filename",
                    new[] { nameof(Pattern) }));
            }
        }

        // Validate consistency between pattern and include flags
        if (!string.IsNullOrWhiteSpace(Pattern))
        {
            if (Pattern.Contains("{server}") && !IncludeServerName)
            {
                results.Add(new ValidationResult(
                    "Pattern contains {server} placeholder but IncludeServerName is false",
                    new[] { nameof(Pattern), nameof(IncludeServerName) }));
            }

            if (Pattern.Contains("{database}") && !IncludeDatabaseName)
            {
                results.Add(new ValidationResult(
                    "Pattern contains {database} placeholder but IncludeDatabaseName is false",
                    new[] { nameof(Pattern), nameof(IncludeDatabaseName) }));
            }
        }

        return results;
    }

    /// <summary>
    /// Validates the naming strategy configuration
    /// </summary>
    /// <returns>Tuple indicating if strategy is valid and any error messages</returns>
    public (bool IsValid, List<string> Errors) ValidateStrategy()
    {
        var errors = new List<string>();

        // Test the date format
        try
        {
            var testDate = DateTime.Now;
            var formatted = testDate.ToString(DateFormat);
            
            // Additional validation - check if the format contains valid format specifiers
            var validFormatChars = new[] { 'y', 'M', 'd', 'H', 'h', 'm', 's', 'f', 'F', 't', 'z', 'K' };
            var hasValidFormatChar = DateFormat.Any(c => validFormatChars.Contains(c));
            
            if (!hasValidFormatChar)
            {
                errors.Add($"Invalid date format '{DateFormat}': must contain valid date/time format specifiers");
            }
        }
        catch (FormatException ex)
        {
            errors.Add($"Invalid date format '{DateFormat}': {ex.Message}");
        }

        // Test filename generation with sample data
        try
        {
            var testFileName = GenerateFileName("TestServer", "TestDB", DateTime.Now);
            if (string.IsNullOrWhiteSpace(testFileName))
            {
                errors.Add("Pattern generates empty filename");
            }
            else if (testFileName.Length > 255)
            {
                errors.Add($"Generated filename is too long ({testFileName.Length} characters, max 255)");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Error generating filename: {ex.Message}");
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// Tests filename uniqueness by generating multiple filenames with slight time differences
    /// </summary>
    /// <param name="serverName">Server name to test with</param>
    /// <param name="databaseName">Database name to test with</param>
    /// <param name="count">Number of filenames to generate for uniqueness test</param>
    /// <returns>True if all generated filenames are unique, false otherwise</returns>
    public bool TestUniqueness(string serverName, string databaseName, int count = 10)
    {
        var filenames = new HashSet<string>();
        var baseTime = DateTime.Now;

        for (int i = 0; i < count; i++)
        {
            var testTime = baseTime.AddSeconds(i);
            var filename = GenerateFileName(serverName, databaseName, testTime);
            
            if (!filenames.Add(filename))
            {
                return false; // Duplicate found
            }
        }

        return true;
    }
}