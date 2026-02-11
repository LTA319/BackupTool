using System.ComponentModel.DataAnnotations;
using MySql.Data.MySqlClient;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// MySQL数据库连接信息
/// 包含连接MySQL数据库所需的所有配置参数和验证逻辑
/// </summary>
public class MySQLConnectionInfo : IValidatableObject
{
    /// <summary>
    /// MySQL用户名，必填项
    /// 长度限制：1-100个字符
    /// </summary>
    [Required(ErrorMessage = "Username is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Username must be between 1 and 100 characters")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// MySQL密码，必填项
    /// 长度限制：1-100个字符
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Password must be between 1 and 100 characters")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// MySQL服务名称，必填项
    /// 长度限制：1-100个字符，只能包含字母、数字、连字符和下划线
    /// </summary>
    [Required(ErrorMessage = "Service name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Service name must be between 1 and 100 characters")]
    [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "Service name can only contain letters, numbers, hyphens, and underscores")]
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// MySQL数据目录路径，必填项
    /// 长度限制：1-500个字符
    /// </summary>
    [Required(ErrorMessage = "Data directory path is required")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Data directory path must be between 1 and 500 characters")]
    public string DataDirectoryPath { get; set; } = string.Empty;

    /// <summary>
    /// MySQL服务器端口号
    /// 范围：1-65535，默认值：3306
    /// </summary>
    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    public int Port { get; set; } = 3306;

    /// <summary>
    /// MySQL服务器主机地址，必填项
    /// 长度限制：1-255个字符，默认值：localhost
    /// </summary>
    [Required(ErrorMessage = "Host is required")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Host must be between 1 and 255 characters")]
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// 获取此MySQL连接的连接字符串
    /// </summary>
    /// <returns>格式化的MySQL连接字符串</returns>
    public string GetConnectionString()
    {
        return $"Server={Host};Port={Port};Database=mysql;Uid={Username};Pwd={Password};ConnectionTimeout=30;";
    }

    /// <summary>
    /// 对MySQL连接信息执行自定义验证逻辑
    /// 验证主机格式、数据目录路径和用户名的有效性
    /// </summary>
    /// <param name="validationContext">验证上下文</param>
    /// <returns>验证结果集合</returns>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();

        // 验证主机格式（基本检查常见的无效字符）
        if (!string.IsNullOrWhiteSpace(Host))
        {
            if (Host.Contains(" ") || Host.Contains("\t") || Host.Contains("\n"))
            {
                results.Add(new ValidationResult(
                    "Host cannot contain whitespace characters",
                    new[] { nameof(Host) }));
            }

            // 检查有效的主机名/IP格式（基本验证）
            if (Host.StartsWith(".") || Host.EndsWith(".") || Host.Contains(".."))
            {
                results.Add(new ValidationResult(
                    "Host format is invalid",
                    new[] { nameof(Host) }));
            }
        }

        // 验证数据目录路径格式
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

        // 验证用户名不包含无效字符
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
    /// 通过尝试连接来验证MySQL连接
    /// </summary>
    /// <param name="timeoutSeconds">连接超时时间（秒），默认：30</param>
    /// <returns>包含连接是否有效和错误消息的元组</returns>
    public async Task<(bool IsValid, List<string> Errors)> ValidateConnectionAsync(int timeoutSeconds = 30)
    {
        var errors = new List<string>();

        try
        {
            var connectionString = $"Server={Host};Port={Port};Database=mysql;Uid={Username};Pwd={Password};ConnectionTimeout={timeoutSeconds};";
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            
            // 测试基本查询以确保连接功能正常
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
    /// 测试MySQL服务是否可访问和响应
    /// </summary>
    /// <param name="timeoutSeconds">连接超时时间（秒）</param>
    /// <returns>如果服务可访问返回true，否则返回false</returns>
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