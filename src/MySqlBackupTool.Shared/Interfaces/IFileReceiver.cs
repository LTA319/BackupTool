using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Interface for server-side file reception operations
/// </summary>
public interface IFileReceiver
{
    /// <summary>
    /// Starts listening for incoming file transfer connections
    /// </summary>
    /// <param name="port">Port number to listen on</param>
    Task StartListeningAsync(int port);

    /// <summary>
    /// Stops listening for incoming connections
    /// </summary>
    Task StopListeningAsync();

    /// <summary>
    /// Receives a file from a client
    /// </summary>
    /// <param name="request">File reception request details</param>
    /// <returns>Result of the file reception operation</returns>
    Task<ReceiveResult> ReceiveFileAsync(ReceiveRequest request);
}

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
    /// <param name="policy">Retention policy to apply</param>
    Task ApplyRetentionPolicyAsync(RetentionPolicy policy);
}