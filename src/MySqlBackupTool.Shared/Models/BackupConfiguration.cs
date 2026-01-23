using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// Configuration settings for a backup operation
/// </summary>
public class BackupConfiguration : IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Configuration name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Configuration name must be between 1 and 100 characters")]
    [RegularExpression(@"^[a-zA-Z0-9\s\-_]+$", ErrorMessage = "Configuration name can only contain letters, numbers, spaces, hyphens, and underscores")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "MySQL connection information is required")]
    public MySQLConnectionInfo MySQLConnection { get; set; } = new();

    [Required(ErrorMessage = "Data directory path is required")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Data directory path must be between 1 and 500 characters")]
    public string DataDirectoryPath { get; set; } = string.Empty;

    [Required(ErrorMessage = "MySQL service name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Service name must be between 1 and 100 characters")]
    [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "Service name can only contain letters, numbers, hyphens, and underscores")]
    public string ServiceName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Target server endpoint is required")]
    public ServerEndpoint TargetServer { get; set; } = new();

    [Required(ErrorMessage = "Target directory is required")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Target directory must be between 1 and 500 characters")]
    public string TargetDirectory { get; set; } = string.Empty;

    [Required(ErrorMessage = "File naming strategy is required")]
    public FileNamingStrategy NamingStrategy { get; set; } = new();

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Performs custom validation logic for the backup configuration
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();

        // Validate data directory path exists and is accessible
        if (!string.IsNullOrWhiteSpace(DataDirectoryPath))
        {
            try
            {
                if (!Directory.Exists(DataDirectoryPath))
                {
                    results.Add(new ValidationResult(
                        $"Data directory path '{DataDirectoryPath}' does not exist",
                        new[] { nameof(DataDirectoryPath) }));
                }
                else
                {
                    // Check if directory is readable
                    try
                    {
                        Directory.GetFiles(DataDirectoryPath);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        results.Add(new ValidationResult(
                            $"Access denied to data directory path '{DataDirectoryPath}'",
                            new[] { nameof(DataDirectoryPath) }));
                    }
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                results.Add(new ValidationResult(
                    $"Invalid data directory path format: {ex.Message}",
                    new[] { nameof(DataDirectoryPath) }));
            }
        }

        // Validate target directory path format
        if (!string.IsNullOrWhiteSpace(TargetDirectory))
        {
            try
            {
                // Check if path format is valid
                Path.GetFullPath(TargetDirectory);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                results.Add(new ValidationResult(
                    $"Invalid target directory path format: {ex.Message}",
                    new[] { nameof(TargetDirectory) }));
            }
        }

        // Validate that configuration name is unique (this would typically be done at the repository level)
        // For now, we'll add a placeholder for this validation
        if (string.IsNullOrWhiteSpace(Name?.Trim()))
        {
            results.Add(new ValidationResult(
                "Configuration name cannot be empty or whitespace only",
                new[] { nameof(Name) }));
        }

        return results;
    }

    /// <summary>
    /// Validates all connection parameters for this configuration
    /// </summary>
    /// <returns>True if all parameters are valid, false otherwise</returns>
    public async Task<(bool IsValid, List<string> Errors)> ValidateConnectionParametersAsync()
    {
        var errors = new List<string>();

        // Validate MySQL connection
        if (MySQLConnection != null)
        {
            var (isValid, mysqlErrors) = await MySQLConnection.ValidateConnectionAsync();
            if (!isValid)
            {
                errors.AddRange(mysqlErrors.Select(e => $"MySQL Connection: {e}"));
            }
        }

        // Validate target server endpoint
        if (TargetServer != null)
        {
            var (isValid, serverErrors) = TargetServer.ValidateEndpoint();
            if (!isValid)
            {
                errors.AddRange(serverErrors.Select(e => $"Target Server: {e}"));
            }
        }

        // Validate file naming strategy
        if (NamingStrategy != null)
        {
            var (isValid, namingErrors) = NamingStrategy.ValidateStrategy();
            if (!isValid)
            {
                errors.AddRange(namingErrors.Select(e => $"Naming Strategy: {e}"));
            }
        }

        return (errors.Count == 0, errors);
    }
}