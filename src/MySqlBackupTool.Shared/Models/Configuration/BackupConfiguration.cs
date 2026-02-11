using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// 备份操作的配置设置
/// 包含备份所需的所有配置信息，如MySQL连接、目标服务器、文件命名策略等
/// </summary>
public class BackupConfiguration : IValidatableObject
{
    /// <summary>
    /// 配置ID，主键
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 配置名称，必须唯一
    /// </summary>
    [Required(ErrorMessage = "Configuration name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Configuration name must be between 1 and 100 characters")]
    [RegularExpression(@"^[a-zA-Z0-9\s\-_]+$", ErrorMessage = "Configuration name can only contain letters, numbers, spaces, hyphens, and underscores")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// MySQL连接信息
    /// </summary>
    [Required(ErrorMessage = "MySQL connection information is required")]
    public MySQLConnectionInfo MySQLConnection { get; set; } = new();

    /// <summary>
    /// MySQL数据目录路径
    /// </summary>
    [Required(ErrorMessage = "Data directory path is required")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Data directory path must be between 1 and 500 characters")]
    public string DataDirectoryPath { get; set; } = string.Empty;

    /// <summary>
    /// MySQL服务名称
    /// </summary>
    [Required(ErrorMessage = "MySQL service name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Service name must be between 1 and 100 characters")]
    [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "Service name can only contain letters, numbers, hyphens, and underscores")]
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// 目标服务器端点信息
    /// </summary>
    [Required(ErrorMessage = "Target server endpoint is required")]
    public ServerEndpoint TargetServer { get; set; } = new();

    /// <summary>
    /// 目标目录路径
    /// </summary>
    [Required(ErrorMessage = "Target directory is required")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Target directory must be between 1 and 500 characters")]
    public string TargetDirectory { get; set; } = string.Empty;

    /// <summary>
    /// 文件命名策略
    /// </summary>
    [Required(ErrorMessage = "File naming strategy is required")]
    public FileNamingStrategy NamingStrategy { get; set; } = new();

    /// <summary>
    /// 配置是否处于活跃状态
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 配置创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 客户端标识符，用于身份验证
    /// </summary>
    [Required(ErrorMessage = "Client ID is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Client ID must be between 1 and 100 characters")]
    public string ClientId { get; set; } = "default-client";

    /// <summary>
    /// 客户端密钥，用于身份验证
    /// </summary>
    [Required(ErrorMessage = "Client Secret is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Client Secret must be between 1 and 200 characters")]
    public string ClientSecret { get; set; } = "default-secret-2024";

    /// <summary>
    /// 对备份配置执行自定义验证逻辑
    /// </summary>
    /// <param name="validationContext">验证上下文</param>
    /// <returns>验证结果集合</returns>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();

        // 验证数据目录路径是否存在且可访问
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
                    // 检查目录是否可读
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

        // 验证目标目录路径格式
        if (!string.IsNullOrWhiteSpace(TargetDirectory))
        {
            try
            {
                // 检查路径格式是否有效
                Path.GetFullPath(TargetDirectory);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                results.Add(new ValidationResult(
                    $"Invalid target directory path format: {ex.Message}",
                    new[] { nameof(TargetDirectory) }));
            }
        }

        // 验证配置名称是否唯一（通常在仓储层完成）
        // 现在我们为此验证添加一个占位符
        if (string.IsNullOrWhiteSpace(Name?.Trim()))
        {
            results.Add(new ValidationResult(
                "Configuration name cannot be empty or whitespace only",
                new[] { nameof(Name) }));
        }

        return results;
    }

    /// <summary>
    /// 验证此配置的所有连接参数
    /// </summary>
    /// <returns>包含验证状态和错误列表的元组</returns>
    public async Task<(bool IsValid, List<string> Errors)> ValidateConnectionParametersAsync()
    {
        var errors = new List<string>();

        // 验证MySQL连接
        if (MySQLConnection != null)
        {
            var (isValid, mysqlErrors) = await MySQLConnection.ValidateConnectionAsync();
            if (!isValid)
            {
                errors.AddRange(mysqlErrors.Select(e => $"MySQL Connection: {e}"));
            }
        }

        // 验证目标服务器端点
        if (TargetServer != null)
        {
            var (isValid, serverErrors) = TargetServer.ValidateEndpoint();
            if (!isValid)
            {
                errors.AddRange(serverErrors.Select(e => $"Target Server: {e}"));
            }
        }

        // 验证文件命名策略
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

    /// <summary>
    /// 验证此配置是否具有有效的身份验证凭据
    /// </summary>
    /// <returns>如果凭据有效则返回true，否则返回false</returns>
    public bool HasValidCredentials()
    {
        return !string.IsNullOrWhiteSpace(ClientId) && 
               !string.IsNullOrWhiteSpace(ClientSecret);
    }
}