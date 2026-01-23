using System.ComponentModel.DataAnnotations;
using MySql.Data.MySqlClient;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// MySQL connection information
/// </summary>
public class MySQLConnectionInfo : IValidatableObject
{
    [Required(ErrorMessage = "Username is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Username must be between 1 and 100 characters")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Password must be between 1 and 100 characters")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Service name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Service name must be between 1 and 100 characters")]
    [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "Service name can only contain letters, numbers, hyphens, and underscores")]
    public string ServiceName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Data directory path is required")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Data directory path must be between 1 and 500 characters")]
    public string DataDirectoryPath { get; set; } = string.Empty;

    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    public int Port { get; set; } = 3306;

    [Required(ErrorMessage = "Host is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Host must be between 1 and 255 characters")]
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Gets the connection string for this MySQL connection
    /// </summary>
    public string GetConnectionString()
    {
        return $"Server={Host};Port={Port};Database=mysql;Uid={Username};Pwd={Password};ConnectionTimeout=30;";
    }

    /// <summary>
    /// Performs custom validation logic for the MySQL connection
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();

        // Validate host format (basic check for common invalid characters)
        if (!string.IsNullOrWhiteSpace(Host))
        {
            if (Host.Contains(" ") || Host.Contains("\t") || Host.Contains("\n"))
            {
                results.Add(new ValidationResult(
                    "Host cannot contain whitespace characters",
                    new[] { nameof(Host) }));
            }

            // Check for valid hostname/IP format (basic validation)
            if (Host.StartsWith(".") || Host.EndsWith(".") || Host.Contains(".."))
            {
                results.Add(new ValidationResult(
                    "Host format is invalid",
                    new[] { nameof(Host) }));
            }
        }

        // Validate data directory path format
        if (!string.IsNullOrWhiteSpace(DataDirectoryPath))
        {
            try
            {
                Path.GetFullPath(DataDirectoryPath);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                results.Add(new ValidationResult(
                    $"Invalid data directory path format: {ex.Message}",
                    new[] { nameof(DataDirectoryPath) }));
            }
        }

        // Validate username doesn't contain invalid characters
        if (!string.IsNullOrWhiteSpace(Username))
        {
            var invalidChars = new[] { '\'', '"', '\\', '\0', '\n', '\r', '\t' };
            if (Username.Any(c => invalidChars.Contains(c)))
            {
                results.Add(new ValidationResult(
                    "Username contains invalid characters",
                    new[] { nameof(Username) }));
            }
        }

        return results;
    }

    /// <summary>
    /// Validates the MySQL connection by attempting to connect
    /// </summary>
    /// <param name="timeoutSeconds">Connection timeout in seconds (default: 30)</param>
    /// <returns>Tuple indicating if connection is valid and any error messages</returns>
    public async Task<(bool IsValid, List<string> Errors)> ValidateConnectionAsync(int timeoutSeconds = 30)
    {
        var errors = new List<string>();

        try
        {
            var connectionString = $"Server={Host};Port={Port};Database=mysql;Uid={Username};Pwd={Password};ConnectionTimeout={timeoutSeconds};";
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            
            // Test basic query to ensure connection is functional
            using var command = new MySqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync();
            
            return (true, errors);
        }
        catch (MySqlException ex)
        {
            errors.Add($"MySQL connection failed: {ex.Message}");
            return (false, errors);
        }
        catch (Exception ex)
        {
            errors.Add($"Connection validation failed: {ex.Message}");
            return (false, errors);
        }
    }

    /// <summary>
    /// Tests if the MySQL service is accessible and responsive
    /// </summary>
    /// <param name="timeoutSeconds">Connection timeout in seconds</param>
    /// <returns>True if service is accessible, false otherwise</returns>
    public async Task<bool> TestServiceAccessibilityAsync(int timeoutSeconds = 30)
    {
        try
        {
            var connectionString = $"Server={Host};Port={Port};Database=mysql;Uid={Username};Pwd={Password};ConnectionTimeout={timeoutSeconds};";
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            return connection.State == System.Data.ConnectionState.Open;
        }
        catch
        {
            return false;
        }
    }
}