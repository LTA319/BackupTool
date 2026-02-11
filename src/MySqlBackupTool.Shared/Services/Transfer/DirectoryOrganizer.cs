using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 处理备份文件目录组织策略的服务 / Handles directory organization strategies for backup files
/// </summary>
public class DirectoryOrganizer
{
    private readonly ILogger<DirectoryOrganizer> _logger;

    /// <summary>
    /// 初始化目录组织器 / Initialize directory organizer
    /// </summary>
    /// <param name="logger">日志记录器 / Logger instance</param>
    public DirectoryOrganizer(ILogger<DirectoryOrganizer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 基于组织策略创建目录结构 / Creates a directory structure based on the organization strategy
    /// </summary>
    /// <param name="basePath">基础存储路径 / Base storage path</param>
    /// <param name="metadata">备份元数据 / Backup metadata</param>
    /// <param name="strategy">组织策略 / Organization strategy</param>
    /// <returns>完整目录路径 / Full directory path</returns>
    /// <exception cref="Exception">创建目录结构失败时抛出 / Thrown when directory structure creation fails</exception>
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
            
            // 确保目录存在 / Ensure directory exists
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
    /// 创建服务器优先，然后基于日期的目录结构 / Creates server-first, then date-based directory structure
    /// 示例：/Backups/ServerName/2024/01-Jan/ / Example: /Backups/ServerName/2024/01-Jan/
    /// </summary>
    /// <param name="metadata">备份元数据 / Backup metadata</param>
    /// <param name="strategy">组织策略 / Organization strategy</param>
    /// <returns>路径组件列表 / List of path components</returns>
    private List<string> CreateServerDateBasedPath(BackupMetadata metadata, DirectoryOrganizationStrategy strategy)
    {
        var components = new List<string>();

        // 服务器目录 / Server directory
        components.Add(SanitizeDirectoryName(metadata.ServerName));

        // 基于粒度的日期组件 / Date components based on granularity
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

        // 如果指定则添加数据库目录 / Database directory if specified
        if (strategy.IncludeDatabaseDirectory && !string.IsNullOrWhiteSpace(metadata.DatabaseName))
        {
            components.Add(SanitizeDirectoryName(metadata.DatabaseName));
        }

        return components;
    }

    /// <summary>
    /// 创建日期优先，然后基于服务器的目录结构 / Creates date-first, then server-based directory structure
    /// 示例：/Backups/2024/01-Jan/ServerName/ / Example: /Backups/2024/01-Jan/ServerName/
    /// </summary>
    /// <param name="metadata">备份元数据 / Backup metadata</param>
    /// <param name="strategy">组织策略 / Organization strategy</param>
    /// <returns>路径组件列表 / List of path components</returns>
    private List<string> CreateDateServerBasedPath(BackupMetadata metadata, DirectoryOrganizationStrategy strategy)
    {
        var components = new List<string>();

        // 日期组件优先 / Date components first
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

        // 服务器目录 / Server directory
        components.Add(SanitizeDirectoryName(metadata.ServerName));

        // 如果指定则添加数据库目录 / Database directory if specified
        if (strategy.IncludeDatabaseDirectory && !string.IsNullOrWhiteSpace(metadata.DatabaseName))
        {
            components.Add(SanitizeDirectoryName(metadata.DatabaseName));
        }

        return components;
    }

    /// <summary>
    /// 创建扁平的基于服务器的目录结构 / Creates flat server-based directory structure
    /// 示例：/Backups/ServerName/ / Example: /Backups/ServerName/
    /// </summary>
    /// <param name="metadata">备份元数据 / Backup metadata</param>
    /// <param name="strategy">组织策略 / Organization strategy</param>
    /// <returns>路径组件列表 / List of path components</returns>
    private List<string> CreateFlatServerBasedPath(BackupMetadata metadata, DirectoryOrganizationStrategy strategy)
    {
        var components = new List<string>
        {
            SanitizeDirectoryName(metadata.ServerName)
        };

        // 如果指定则添加数据库目录 / Database directory if specified
        if (strategy.IncludeDatabaseDirectory && !string.IsNullOrWhiteSpace(metadata.DatabaseName))
        {
            components.Add(SanitizeDirectoryName(metadata.DatabaseName));
        }

        return components;
    }

    /// <summary>
    /// 基于模式创建自定义目录结构 / Creates custom directory structure based on pattern
    /// </summary>
    /// <param name="metadata">备份元数据 / Backup metadata</param>
    /// <param name="strategy">组织策略 / Organization strategy</param>
    /// <returns>路径组件列表 / List of path components</returns>
    private List<string> CreateCustomPath(BackupMetadata metadata, DirectoryOrganizationStrategy strategy)
    {
        if (string.IsNullOrWhiteSpace(strategy.CustomPattern))
        {
            return CreateServerDateBasedPath(metadata, strategy);
        }

        var pattern = strategy.CustomPattern;
        
        // 替换占位符 / Replace placeholders
        pattern = pattern.Replace("{server}", SanitizeDirectoryName(metadata.ServerName));
        pattern = pattern.Replace("{database}", SanitizeDirectoryName(metadata.DatabaseName));
        pattern = pattern.Replace("{year}", metadata.BackupTime.Year.ToString());
        pattern = pattern.Replace("{month}", metadata.BackupTime.ToString("MM"));
        pattern = pattern.Replace("{monthname}", metadata.BackupTime.ToString("MMM"));
        pattern = pattern.Replace("{day}", metadata.BackupTime.ToString("dd"));
        pattern = pattern.Replace("{hour}", metadata.BackupTime.ToString("HH"));
        pattern = pattern.Replace("{type}", SanitizeDirectoryName(metadata.BackupType));

        // 按路径分隔符分割并清理 / Split by path separators and clean up
        var components = pattern.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(c => SanitizeDirectoryName(c))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();

        return components;
    }

    /// <summary>
    /// 验证目录组织策略 / Validates a directory organization strategy
    /// </summary>
    /// <param name="strategy">要验证的策略 / Strategy to validate</param>
    /// <returns>验证结果和错误列表 / Validation result and error list</returns>
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
                // 验证自定义模式 / Validate custom pattern
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

        // 验证日期粒度是否合理 / Validate date granularity makes sense
        if (strategy.DateGranularity == DateGranularity.Hour && strategy.Type == OrganizationType.FlatServerBased)
        {
            errors.Add("Hour granularity is not recommended with flat server-based organization");
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// 通过移除无效字符来清理目录名称 / Sanitizes a directory name by removing invalid characters
    /// </summary>
    /// <param name="name">要清理的名称 / Name to sanitize</param>
    /// <returns>清理后的目录名称 / Sanitized directory name</returns>
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
/// 组织备份目录的策略 / Strategy for organizing backup directories
/// </summary>
public class DirectoryOrganizationStrategy
{
    /// <summary>
    /// 组织类型 / Organization type
    /// </summary>
    public OrganizationType Type { get; set; } = OrganizationType.ServerDateBased;
    
    /// <summary>
    /// 日期粒度 / Date granularity
    /// </summary>
    public DateGranularity DateGranularity { get; set; } = DateGranularity.Month;
    
    /// <summary>
    /// 是否包含数据库目录 / Whether to include database directory
    /// </summary>
    public bool IncludeDatabaseDirectory { get; set; } = false;
    
    /// <summary>
    /// 自定义模式 / Custom pattern
    /// </summary>
    public string CustomPattern { get; set; } = string.Empty;

    /// <summary>
    /// 获取组织策略的描述 / Gets a description of the organization strategy
    /// </summary>
    /// <returns>策略描述 / Strategy description</returns>
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
/// 目录组织的类型 / Types of directory organization
/// </summary>
public enum OrganizationType
{
    /// <summary>
    /// 服务器-日期基础 / Server-date based
    /// </summary>
    ServerDateBased,
    
    /// <summary>
    /// 日期-服务器基础 / Date-server based
    /// </summary>
    DateServerBased,
    
    /// <summary>
    /// 扁平服务器基础 / Flat server based
    /// </summary>
    FlatServerBased,
    
    /// <summary>
    /// 自定义 / Custom
    /// </summary>
    Custom
}

/// <summary>
/// 目录组织的日期粒度 / Date granularity for directory organization
/// </summary>
public enum DateGranularity
{
    /// <summary>
    /// 年 / Year
    /// </summary>
    Year,
    
    /// <summary>
    /// 月 / Month
    /// </summary>
    Month,
    
    /// <summary>
    /// 日 / Day
    /// </summary>
    Day,
    
    /// <summary>
    /// 小时 / Hour
    /// </summary>
    Hour
}