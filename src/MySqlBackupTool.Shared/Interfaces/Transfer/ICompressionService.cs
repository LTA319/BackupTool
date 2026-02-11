using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 文件压缩操作的接口 / Interface for file compression operations
/// 提供目录压缩、文件清理等功能，支持进度报告和异步操作
/// Provides directory compression, file cleanup functionality with progress reporting and async operations
/// </summary>
public interface ICompressionService
{
    /// <summary>
    /// 将目录压缩为zip文件 / Compresses a directory into a zip file
    /// 支持递归压缩目录中的所有文件和子目录，并提供压缩进度报告
    /// Supports recursive compression of all files and subdirectories with compression progress reporting
    /// </summary>
    /// <param name="sourcePath">要压缩的目录路径 / Path to the directory to compress</param>
    /// <param name="targetPath">压缩文件的创建路径 / Path where the compressed file should be created</param>
    /// <param name="progress">压缩操作的进度报告器，可选参数 / Progress reporter for compression operations, optional parameter</param>
    /// <returns>创建的压缩文件路径 / Path to the created compressed file</returns>
    /// <exception cref="DirectoryNotFoundException">当源目录不存在时抛出 / Thrown when source directory does not exist</exception>
    /// <exception cref="UnauthorizedAccessException">当没有访问权限时抛出 / Thrown when access is denied</exception>
    /// <exception cref="CompressionException">当压缩操作失败时抛出 / Thrown when compression operation fails</exception>
    Task<string> CompressDirectoryAsync(string sourcePath, string targetPath, IProgress<CompressionProgress>? progress = null);

    /// <summary>
    /// 清理压缩过程中创建的临时文件 / Cleans up temporary files created during compression
    /// 删除指定的文件，通常用于清理压缩操作产生的临时文件和中间文件
    /// Deletes the specified file, typically used to clean up temporary and intermediate files from compression operations
    /// </summary>
    /// <param name="filePath">要清理的文件路径 / Path to the file to clean up</param>
    /// <exception cref="FileNotFoundException">当文件不存在时抛出 / Thrown when file does not exist</exception>
    /// <exception cref="UnauthorizedAccessException">当没有删除权限时抛出 / Thrown when delete access is denied</exception>
    Task CleanupAsync(string filePath);
}