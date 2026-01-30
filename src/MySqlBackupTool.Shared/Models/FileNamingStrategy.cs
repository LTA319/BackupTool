using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// 备份文件命名策略
/// 定义如何根据配置参数生成备份文件名
/// </summary>
public class FileNamingStrategy : IValidatableObject
{
    /// <summary>
    /// 文件命名模式，必填项
    /// 支持占位符：{timestamp}、{database}、{server}
    /// 长度限制：1-200个字符
    /// </summary>
    [Required(ErrorMessage = "File naming pattern is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Pattern must be between 1 and 200 characters")]
    public string Pattern { get; set; } = "{timestamp}_{database}_{server}.zip";

    /// <summary>
    /// 日期格式字符串，必填项
    /// 用于格式化{timestamp}占位符
    /// 长度限制：1-50个字符
    /// </summary>
    [Required(ErrorMessage = "Date format is required")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Date format must be between 1 and 50 characters")]
    public string DateFormat { get; set; } = "yyyyMMdd_HHmmss";

    /// <summary>
    /// 是否在文件名中包含服务器名称
    /// 控制{server}占位符的处理
    /// </summary>
    public bool IncludeServerName { get; set; } = true;

    /// <summary>
    /// 是否在文件名中包含数据库名称
    /// 控制{database}占位符的处理
    /// </summary>
    public bool IncludeDatabaseName { get; set; } = true;

    /// <summary>
    /// 根据策略和提供的参数生成文件名
    /// </summary>
    /// <param name="serverName">服务器名称</param>
    /// <param name="databaseName">数据库名称</param>
    /// <param name="timestamp">备份时间戳</param>
    /// <returns>生成的文件名</returns>
    public string GenerateFileName(string serverName, string databaseName, DateTime timestamp)
    {
        var fileName = Pattern;
        
        // 首先替换时间戳
        fileName = fileName.Replace("{timestamp}", timestamp.ToString(DateFormat));
        
        // 处理服务器名称
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
            // 移除服务器占位符和相邻的分隔符
            fileName = RemovePlaceholderAndSeparators(fileName, "{server}");
        }
        
        // 处理数据库名称
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
            // 移除数据库占位符和相邻的分隔符
            fileName = RemovePlaceholderAndSeparators(fileName, "{database}");
        }

        // 最终清理
        fileName = CleanupFileName(fileName);
        
        return fileName;
    }

    /// <summary>
    /// 移除占位符和相邻的分隔符以避免双重分隔符
    /// </summary>
    /// <param name="input">输入字符串</param>
    /// <param name="placeholder">要移除的占位符</param>
    /// <returns>处理后的字符串</returns>
    private static string RemovePlaceholderAndSeparators(string input, string placeholder)
    {
        // 尝试不同的模式：_placeholder、placeholder_、_placeholder_
        var patterns = new[]
        {
            $"_{placeholder}_", // 两边都有分隔符的中间占位符
            $"_{placeholder}",  // 有前导分隔符的占位符
            $"{placeholder}_",  // 有尾随分隔符的占位符
            placeholder         // 只有占位符
        };

        foreach (var pattern in patterns)
        {
            if (input.Contains(pattern))
            {
                // 对于中间模式，替换为单个分隔符
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
    /// 通过移除双重分隔符和修剪来清理文件名
    /// </summary>
    /// <param name="fileName">要清理的文件名</param>
    /// <returns>清理后的文件名</returns>
    private static string CleanupFileName(string fileName)
    {
        // 移除双下划线
        while (fileName.Contains("__"))
        {
            fileName = fileName.Replace("__", "_");
        }
        
        // 移除双连字符
        while (fileName.Contains("--"))
        {
            fileName = fileName.Replace("--", "-");
        }
        
        // 从开头和结尾修剪分隔符
        fileName = fileName.Trim('_', '-', ' ', '.');
        
        return fileName;
    }

    /// <summary>
    /// 清理字符串使其适合用于文件名
    /// </summary>
    /// <param name="input">输入字符串</param>
    /// <returns>清理后的安全文件名字符串</returns>
    private static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "unknown";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(input.Where(c => !invalidChars.Contains(c)).ToArray());
        
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }

    /// <summary>
    /// 对文件命名策略执行自定义验证逻辑
    /// </summary>
    /// <param name="validationContext">验证上下文</param>
    /// <returns>验证结果集合</returns>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();

        // 验证日期格式
        if (!string.IsNullOrWhiteSpace(DateFormat))
        {
            try
            {
                var testDate = DateTime.Now;
                var formatted = testDate.ToString(DateFormat);
                
                // 附加验证 - 检查格式是否包含有效的格式说明符
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

        // 验证模式包含有效的占位符
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

            // 检查模式中的无效文件名字符（排除占位符）
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

            // 验证模式不会导致空文件名
            if (Pattern.Trim().Replace("{timestamp}", "").Replace("{server}", "").Replace("{database}", "").Trim('_', '-', ' ').Length == 0)
            {
                results.Add(new ValidationResult(
                    "Pattern would result in empty or invalid filename",
                    new[] { nameof(Pattern) }));
            }
        }

        // 验证模式和包含标志之间的一致性
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
    /// 验证命名策略配置
    /// </summary>
    /// <returns>包含策略是否有效和错误消息的元组</returns>
    public (bool IsValid, List<string> Errors) ValidateStrategy()
    {
        var errors = new List<string>();

        // 测试日期格式
        try
        {
            var testDate = DateTime.Now;
            var formatted = testDate.ToString(DateFormat);
            
            // 附加验证 - 检查格式是否包含有效的格式说明符
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

        // 使用示例数据测试文件名生成
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
    /// 通过生成多个具有轻微时间差异的文件名来测试文件名唯一性
    /// </summary>
    /// <param name="serverName">用于测试的服务器名称</param>
    /// <param name="databaseName">用于测试的数据库名称</param>
    /// <param name="count">生成用于唯一性测试的文件名数量</param>
    /// <returns>如果所有生成的文件名都是唯一的返回true，否则返回false</returns>
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
                return false; // 发现重复
            }
        }

        return true;
    }
}