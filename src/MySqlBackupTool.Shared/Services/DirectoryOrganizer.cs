using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Handles directory organization strategies for backup files
/// </summary>
public class DirectoryOrganizer
{
    private readonly ILogger<DirectoryOrganizer> _logger;

    public DirectoryOrganizer(ILogger<DirectoryOrganizer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates a directory structure based on the organization strategy
    /// </summary>
    /// <param name="basePath">Base storage path</param>
    /// <param name="metadata">Backup metadata</param>
    /// <param name="strategy">Organization strategy</param>
    /// <returns>Full directory path</returns>
    public string CreateDirectoryStructure(string basePath, BackupMetadata metadata, DirectoryOrganizationStrategy strategy)
    {
        try
        {
            var pathComponents = new List<string> { basePath };

            switch (strategy.Type)
            {
                case OrganizationType.ServerDateBased:
                    pathComponents.AddRange(CreateServerDateBasedPath(metadata, strategy));
                    break;

                case OrganizationType.DateServerBased:
                    pathComponents.AddRange(CreateDateServerBasedPath(metadata, strategy));
                    break;

                case OrganizationType.FlatServerBased:
                    pathComponents.AddRange(CreateFlatServerBasedPath(metadata, strategy));
                    break;

                case OrganizationType.Custom:
                    pathComponents.AddRange(CreateCustomPath(metadata, strategy));
                    break;

                default:
                    pathComponents.AddRange(CreateServerDateBasedPath(metadata, strategy));
                    break;
            }

            var fullPath = Path.Combine(pathComponents.ToArray());
            
            // Ensure directory exists
            Directory.CreateDirectory(fullPath);
            
            _logger.LogDebug("Created directory structure: {Path}", fullPath);
            return fullPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating directory structure for server {ServerName}", metadata.ServerName);
            throw;
        }
    }

    /// <summary>
    /// Creates server-first, then date-based directory structure
    /// Example: /Backups/ServerName/2024/01-Jan/
    /// </summary>
    private List<string> CreateServerDateBasedPath(BackupMetadata metadata, DirectoryOrganizationStrategy strategy)
    {
        var components = new List<string>();

        // Server directory
        components.Add(SanitizeDirectoryName(metadata.ServerName));

        // Date components based on granularity
        switch (strategy.DateGranularity)
        {
            case DateGranularity.Year:
                components.Add(metadata.BackupTime.Year.ToString());
                break;

            case DateGranularity.Month:
                components.Add(metadata.BackupTime.Year.ToString());
                components.Add(metadata.BackupTime.ToString("MM-MMM"));
                break;

            case DateGranularity.Day:
                components.Add(metadata.BackupTime.Year.ToString());
                components.Add(metadata.BackupTime.ToString("MM-MMM"));
                components.Add(metadata.BackupTime.ToString("dd"));
                break;

            case DateGranularity.Hour:
                components.Add(metadata.BackupTime.Year.ToString());
                components.Add(metadata.BackupTime.ToString("MM-MMM"));
                components.Add(metadata.BackupTime.ToString("dd"));
                components.Add(metadata.BackupTime.ToString("HH"));
                break;
        }

        // Database directory if specified
        if (strategy.IncludeDatabaseDirectory && !string.IsNullOrWhiteSpace(metadata.DatabaseName))
        {
            components.Add(SanitizeDirectoryName(metadata.DatabaseName));
        }

        return components;
    }

    /// <summary>
    /// Creates date-first, then server-based directory structure
    /// Example: /Backups/2024/01-Jan/ServerName/
    /// </summary>
    private List<string> CreateDateServerBasedPath(BackupMetadata metadata, DirectoryOrganizationStrategy strategy)
    {
        var components = new List<string>();

        // Date components first
        switch (strategy.DateGranularity)
        {
            case DateGranularity.Year:
                components.Add(metadata.BackupTime.Year.ToString());
                break;

            case DateGranularity.Month:
                components.Add(metadata.BackupTime.Year.ToString());
                components.Add(metadata.BackupTime.ToString("MM-MMM"));
                break;

            case DateGranularity.Day:
                components.Add(metadata.BackupTime.Year.ToString());
                components.Add(metadata.BackupTime.ToString("MM-MMM"));
                components.Add(metadata.BackupTime.ToString("dd"));
                break;

            case DateGranularity.Hour:
                components.Add(metadata.BackupTime.Year.ToString());
                components.Add(metadata.BackupTime.ToString("MM-MMM"));
                components.Add(metadata.BackupTime.ToString("dd"));
                components.Add(metadata.BackupTime.ToString("HH"));
                break;
        }

        // Server directory
        components.Add(SanitizeDirectoryName(metadata.ServerName));

        // Database directory if specified
        if (strategy.IncludeDatabaseDirectory && !string.IsNullOrWhiteSpace(metadata.DatabaseName))
        {
            components.Add(SanitizeDirectoryName(metadata.DatabaseName));
        }

        return components;
    }

    /// <summary>
    /// Creates flat server-based directory structure
    /// Example: /Backups/ServerName/
    /// </summary>
    private List<string> CreateFlatServerBasedPath(BackupMetadata metadata, DirectoryOrganizationStrategy strategy)
    {
        var components = new List<string>
        {
            SanitizeDirectoryName(metadata.ServerName)
        };

        // Database directory if specified
        if (strategy.IncludeDatabaseDirectory && !string.IsNullOrWhiteSpace(metadata.DatabaseName))
        {
            components.Add(SanitizeDirectoryName(metadata.DatabaseName));
        }

        return components;
    }

    /// <summary>
    /// Creates custom directory structure based on pattern
    /// </summary>
    private List<string> CreateCustomPath(BackupMetadata metadata, DirectoryOrganizationStrategy strategy)
    {
        if (string.IsNullOrWhiteSpace(strategy.CustomPattern))
        {
            return CreateServerDateBasedPath(metadata, strategy);
        }

        var pattern = strategy.CustomPattern;
        
        // Replace placeholders
        pattern = pattern.Replace("{server}", SanitizeDirectoryName(metadata.ServerName));
        pattern = pattern.Replace("{database}", SanitizeDirectoryName(metadata.DatabaseName));
        pattern = pattern.Replace("{year}", metadata.BackupTime.Year.ToString());
        pattern = pattern.Replace("{month}", metadata.BackupTime.ToString("MM"));
        pattern = pattern.Replace("{monthname}", metadata.BackupTime.ToString("MMM"));
        pattern = pattern.Replace("{day}", metadata.BackupTime.ToString("dd"));
        pattern = pattern.Replace("{hour}", metadata.BackupTime.ToString("HH"));
        pattern = pattern.Replace("{type}", SanitizeDirectoryName(metadata.BackupType));

        // Split by path separators and clean up
        var components = pattern.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(c => SanitizeDirectoryName(c))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();

        return components;
    }

    /// <summary>
    /// Validates a directory organization strategy
    /// </summary>
    /// <param name="strategy">Strategy to validate</param>
    /// <returns>Validation result</returns>
    public (bool IsValid, List<string> Errors) ValidateStrategy(DirectoryOrganizationStrategy strategy)
    {
        var errors = new List<string>();

        if (strategy.Type == OrganizationType.Custom)
        {
            if (string.IsNullOrWhiteSpace(strategy.CustomPattern))
            {
                errors.Add("Custom pattern is required when using custom organization type");
            }
            else
            {
                // Validate custom pattern
                var testMetadata = new BackupMetadata
                {
                    ServerName = "TestServer",
                    DatabaseName = "TestDB",
                    BackupTime = DateTime.Now,
                    BackupType = "Full"
                };

                try
                {
                    var testPath = CreateCustomPath(testMetadata, strategy);
                    if (testPath.Count == 0)
                    {
                        errors.Add("Custom pattern results in empty directory structure");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Invalid custom pattern: {ex.Message}");
                }
            }
        }

        // Validate date granularity makes sense
        if (strategy.DateGranularity == DateGranularity.Hour && strategy.Type == OrganizationType.FlatServerBased)
        {
            errors.Add("Hour granularity is not recommended with flat server-based organization");
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// Sanitizes a directory name by removing invalid characters
    /// </summary>
    private static string SanitizeDirectoryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Unknown";
        }

        var invalidChars = Path.GetInvalidPathChars().Concat(Path.GetInvalidFileNameChars()).ToArray();
        var sanitized = new string(name.Where(c => !invalidChars.Contains(c)).ToArray());
        
        return string.IsNullOrWhiteSpace(sanitized) ? "Unknown" : sanitized.Trim();
    }
}

/// <summary>
/// Strategy for organizing backup directories
/// </summary>
public class DirectoryOrganizationStrategy
{
    public OrganizationType Type { get; set; } = OrganizationType.ServerDateBased;
    public DateGranularity DateGranularity { get; set; } = DateGranularity.Month;
    public bool IncludeDatabaseDirectory { get; set; } = false;
    public string CustomPattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets a description of the organization strategy
    /// </summary>
    public string GetDescription()
    {
        return Type switch
        {
            OrganizationType.ServerDateBased => $"Server/{GetDateGranularityDescription()}{(IncludeDatabaseDirectory ? "/Database" : "")}",
            OrganizationType.DateServerBased => $"{GetDateGranularityDescription()}/Server{(IncludeDatabaseDirectory ? "/Database" : "")}",
            OrganizationType.FlatServerBased => $"Server{(IncludeDatabaseDirectory ? "/Database" : "")}",
            OrganizationType.Custom => $"Custom: {CustomPattern}",
            _ => "Unknown"
        };
    }

    private string GetDateGranularityDescription()
    {
        return DateGranularity switch
        {
            DateGranularity.Year => "Year",
            DateGranularity.Month => "Year/Month",
            DateGranularity.Day => "Year/Month/Day",
            DateGranularity.Hour => "Year/Month/Day/Hour",
            _ => "Date"
        };
    }
}

/// <summary>
/// Types of directory organization
/// </summary>
public enum OrganizationType
{
    ServerDateBased,
    DateServerBased,
    FlatServerBased,
    Custom
}

/// <summary>
/// Date granularity for directory organization
/// </summary>
public enum DateGranularity
{
    Year,
    Month,
    Day,
    Hour
}