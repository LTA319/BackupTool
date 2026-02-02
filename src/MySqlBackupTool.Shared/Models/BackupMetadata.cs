using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// 备份操作的元数据信息
/// Metadata about a backup operation
/// </summary>
public class BackupMetadata
{
    /// <summary>
    /// 服务器名称，最大长度100字符
    /// Server name, maximum 100 characters
    /// </summary>
    [Required]
    [StringLength(100)]
    public string ServerName { get; set; } = string.Empty;

    /// <summary>
    /// 数据库名称，最大长度100字符
    /// Database name, maximum 100 characters
    /// </summary>
    [Required]
    [StringLength(100)]
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// 备份时间，默认为当前UTC时间
    /// Backup time, defaults to current UTC time
    /// </summary>
    public DateTime BackupTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 备份类型，默认为"Full"（完整备份），最大长度50字符
    /// Backup type, defaults to "Full", maximum 50 characters
    /// </summary>
    [Required]
    [StringLength(50)]
    public string BackupType { get; set; } = "Full";

    /// <summary>
    /// 预估的备份文件大小（字节），必须为非负数
    /// Estimated backup file size in bytes, must be non-negative
    /// </summary>
    [Range(0, long.MaxValue)]
    public long EstimatedSize { get; set; }

    /// <summary>
    /// 关联的备份配置信息
    /// Associated backup configuration
    /// </summary>
    public BackupConfiguration? Configuration { get; set; }

    /// <summary>
    /// 获取此备份的唯一标识符，格式为：服务器名_数据库名_时间戳
    /// Gets a unique identifier for this backup in format: ServerName_DatabaseName_Timestamp
    /// </summary>
    /// <returns>备份的唯一标识符 / Unique backup identifier</returns>
    public string GetBackupId()
    {
        return $"{ServerName}_{DatabaseName}_{BackupTime:yyyyMMdd_HHmmss}";
    }
}

/// <summary>
/// 备份文件保留策略配置
/// Policy for retaining backup files
/// </summary>
public class RetentionPolicy
{
    /// <summary>
    /// 策略的唯一标识符
    /// Unique identifier for the policy
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 策略名称，最大长度100字符
    /// Policy name, maximum 100 characters
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 策略描述，最大长度500字符
    /// Policy description, maximum 500 characters
    /// </summary>
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 备份文件的最大保留天数，null表示无限制
    /// Maximum age of backups to retain (in days), null means no limit
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? MaxAgeDays { get; set; }

    /// <summary>
    /// 最大保留的备份文件数量，null表示无限制
    /// Maximum number of backups to retain, null means no limit
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? MaxCount { get; set; }

    /// <summary>
    /// 最大存储空间使用量（字节），null表示无限制
    /// Maximum storage space to use (in bytes), null means no limit
    /// </summary>
    [Range(1, long.MaxValue)]
    public long? MaxStorageBytes { get; set; }

    /// <summary>
    /// 策略是否启用，默认为true
    /// Whether the policy is enabled, defaults to true
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 策略创建时间，默认为当前UTC时间
    /// Policy creation time, defaults to current UTC time
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 根据此策略判断备份是否应该被保留
    /// Determines if a backup should be retained based on this policy
    /// </summary>
    /// <param name="backupDate">备份日期 / Date of the backup</param>
    /// <param name="currentBackupCount">当前备份数量 / Current number of backups</param>
    /// <param name="currentStorageUsed">当前存储使用量（字节）/ Current storage usage in bytes</param>
    /// <param name="backupSize">备份文件大小（字节）/ Size of the backup in bytes</param>
    /// <returns>如果应该保留备份返回true，如果应该删除返回false / True if the backup should be retained, false if it should be deleted</returns>
    public bool ShouldRetainBackup(DateTime backupDate, int currentBackupCount, long currentStorageUsed, long backupSize)
    {
        if (!IsEnabled)
            return true;

        // Check age policy
        if (MaxAgeDays.HasValue)
        {
            var age = DateTime.Now - backupDate;
            if (age.TotalDays > MaxAgeDays.Value)
                return false;
        }

        // Check count policy
        if (MaxCount.HasValue && currentBackupCount > MaxCount.Value)
            return false;

        // Check storage policy
        if (MaxStorageBytes.HasValue && (currentStorageUsed + backupSize) > MaxStorageBytes.Value)
            return false;

        return true;
    }

    /// <summary>
    /// 获取策略的可读描述信息
    /// Gets a human-readable description of the policy
    /// </summary>
    /// <returns>策略描述字符串 / Policy description string</returns>
    public string GetPolicyDescription()
    {
        var parts = new List<string>();

        if (MaxAgeDays.HasValue)
            parts.Add($"Keep for {MaxAgeDays.Value} days");

        if (MaxCount.HasValue)
            parts.Add($"Keep max {MaxCount.Value} backups");

        if (MaxStorageBytes.HasValue)
        {
            var sizeStr = FormatBytes(MaxStorageBytes.Value);
            parts.Add($"Use max {sizeStr} storage");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "No restrictions";
    }

    /// <summary>
    /// 将字节数格式化为可读的大小字符串
    /// Formats bytes into a human-readable size string
    /// </summary>
    /// <param name="bytes">字节数 / Number of bytes</param>
    /// <returns>格式化的大小字符串 / Formatted size string</returns>
    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}