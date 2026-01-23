using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// Metadata about a backup operation
/// </summary>
public class BackupMetadata
{
    [Required]
    [StringLength(100)]
    public string ServerName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string DatabaseName { get; set; } = string.Empty;

    public DateTime BackupTime { get; set; } = DateTime.UtcNow;

    [Required]
    [StringLength(50)]
    public string BackupType { get; set; } = "Full";

    [Range(0, long.MaxValue)]
    public long EstimatedSize { get; set; }

    public BackupConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets a unique identifier for this backup
    /// </summary>
    public string GetBackupId()
    {
        return $"{ServerName}_{DatabaseName}_{BackupTime:yyyyMMdd_HHmmss}";
    }
}

/// <summary>
/// Policy for retaining backup files
/// </summary>
public class RetentionPolicy
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Maximum age of backups to retain (in days)
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? MaxAgeDays { get; set; }

    /// <summary>
    /// Maximum number of backups to retain
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? MaxCount { get; set; }

    /// <summary>
    /// Maximum storage space to use (in bytes)
    /// </summary>
    [Range(1, long.MaxValue)]
    public long? MaxStorageBytes { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Determines if a backup should be retained based on this policy
    /// </summary>
    /// <param name="backupDate">Date of the backup</param>
    /// <param name="currentBackupCount">Current number of backups</param>
    /// <param name="currentStorageUsed">Current storage usage in bytes</param>
    /// <param name="backupSize">Size of the backup in bytes</param>
    /// <returns>True if the backup should be retained, false if it should be deleted</returns>
    public bool ShouldRetainBackup(DateTime backupDate, int currentBackupCount, long currentStorageUsed, long backupSize)
    {
        if (!IsEnabled)
            return true;

        // Check age policy
        if (MaxAgeDays.HasValue)
        {
            var age = DateTime.UtcNow - backupDate;
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
    /// Gets a human-readable description of the policy
    /// </summary>
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