using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Interface for managing backup file storage
/// </summary>
public interface IStorageManager
{
    /// <summary>
    /// Creates a storage path for a backup file
    /// </summary>
    /// <param name="metadata">Metadata about the backup</param>
    /// <returns>Path where the backup should be stored</returns>
    Task<string> CreateBackupPathAsync(BackupMetadata metadata);

    /// <summary>
    /// Validates that sufficient storage space is available
    /// </summary>
    /// <param name="requiredSpace">Amount of space required in bytes</param>
    /// <returns>True if sufficient space is available, false otherwise</returns>
    Task<bool> ValidateStorageSpaceAsync(long requiredSpace);

    /// <summary>
    /// Applies retention policies to manage old backup files
    /// </summary>
    /// <param name="retentionPolicy">The retention policy to apply</param>
    /// <param name="backupDirectory">Directory containing backup files</param>
    /// <returns>Number of files cleaned up</returns>
    Task<int> ApplyRetentionPolicyAsync(RetentionPolicy retentionPolicy, string backupDirectory);

    /// <summary>
    /// Gets available storage space in bytes
    /// </summary>
    /// <param name="path">Path to check storage space for</param>
    /// <returns>Available space in bytes</returns>
    Task<long> GetAvailableSpaceAsync(string path);

    /// <summary>
    /// Ensures the storage directory exists and is accessible
    /// </summary>
    /// <param name="path">Directory path to ensure</param>
    /// <returns>True if directory is accessible, false otherwise</returns>
    Task<bool> EnsureDirectoryAsync(string path);
}