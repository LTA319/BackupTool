using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Interface for file compression operations
/// </summary>
public interface ICompressionService
{
    /// <summary>
    /// Compresses a directory into a zip file
    /// </summary>
    /// <param name="sourcePath">Path to the directory to compress</param>
    /// <param name="targetPath">Path where the compressed file should be created</param>
    /// <param name="progress">Progress reporter for compression operations</param>
    /// <returns>Path to the created compressed file</returns>
    Task<string> CompressDirectoryAsync(string sourcePath, string targetPath, IProgress<CompressionProgress>? progress = null);

    /// <summary>
    /// Cleans up temporary files created during compression
    /// </summary>
    /// <param name="filePath">Path to the file to clean up</param>
    Task CleanupAsync(string filePath);
}