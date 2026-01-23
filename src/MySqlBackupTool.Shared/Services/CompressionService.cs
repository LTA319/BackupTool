using System.IO.Compression;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using Microsoft.Extensions.Logging;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Service for compressing directories and managing temporary files
/// </summary>
public class CompressionService : ICompressionService
{
    private readonly ILogger<CompressionService> _logger;

    public CompressionService(ILogger<CompressionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Compresses a directory into a zip file with progress reporting
    /// </summary>
    /// <param name="sourcePath">Path to the directory to compress</param>
    /// <param name="targetPath">Path where the compressed file should be created</param>
    /// <param name="progress">Progress reporter for compression operations</param>
    /// <returns>Path to the created compressed file</returns>
    public async Task<string> CompressDirectoryAsync(string sourcePath, string targetPath, IProgress<CompressionProgress>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path cannot be null or empty", nameof(sourcePath));
        
        if (string.IsNullOrWhiteSpace(targetPath))
            throw new ArgumentException("Target path cannot be null or empty", nameof(targetPath));

        if (!Directory.Exists(sourcePath))
            throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");

        _logger.LogInformation("Starting compression of directory {SourcePath} to {TargetPath}", sourcePath, targetPath);

        try
        {
            // Ensure target directory exists
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // Get all files for progress calculation
            var allFiles = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
            var totalFiles = allFiles.Length;
            var totalBytes = allFiles.Sum(file => new FileInfo(file).Length);

            _logger.LogDebug("Found {TotalFiles} files totaling {TotalBytes} bytes", totalFiles, totalBytes);

            var compressionProgress = new CompressionProgress
            {
                TotalFiles = totalFiles,
                TotalBytes = totalBytes
            };

            progress?.Report(compressionProgress);

            // Create the zip file
            using var archive = ZipFile.Open(targetPath, ZipArchiveMode.Create);
            
            long processedBytes = 0;
            int processedFiles = 0;

            foreach (var filePath in allFiles)
            {
                var relativePath = Path.GetRelativePath(sourcePath, filePath);
                compressionProgress.CurrentFile = relativePath;
                
                _logger.LogDebug("Compressing file: {RelativePath}", relativePath);

                // Add file to archive
                var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
                
                using var entryStream = entry.Open();
                using var fileStream = File.OpenRead(filePath);
                
                await fileStream.CopyToAsync(entryStream);
                
                var fileInfo = new FileInfo(filePath);
                processedBytes += fileInfo.Length;
                processedFiles++;

                // Update progress
                compressionProgress.ProcessedBytes = processedBytes;
                compressionProgress.ProcessedFiles = processedFiles;
                compressionProgress.Progress = totalBytes > 0 ? (double)processedBytes / totalBytes : 1.0;

                progress?.Report(compressionProgress);
            }

            _logger.LogInformation("Successfully compressed {ProcessedFiles} files ({ProcessedBytes} bytes) to {TargetPath}", 
                processedFiles, processedBytes, targetPath);

            return targetPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compress directory {SourcePath} to {TargetPath}", sourcePath, targetPath);
            
            // Clean up partial file if it exists
            if (File.Exists(targetPath))
            {
                try
                {
                    File.Delete(targetPath);
                    _logger.LogDebug("Cleaned up partial compression file: {TargetPath}", targetPath);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Failed to clean up partial compression file: {TargetPath}", targetPath);
                }
            }
            
            throw;
        }
    }

    /// <summary>
    /// Cleans up temporary files created during compression
    /// </summary>
    /// <param name="filePath">Path to the file to clean up</param>
    public async Task CleanupAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.LogWarning("Cleanup called with null or empty file path");
            return;
        }

        if (!File.Exists(filePath))
        {
            _logger.LogDebug("File does not exist, no cleanup needed: {FilePath}", filePath);
            return;
        }

        try
        {
            _logger.LogDebug("Cleaning up file: {FilePath}", filePath);
            
            // Use Task.Run to make the synchronous File.Delete operation async
            await Task.Run(() => File.Delete(filePath));
            
            _logger.LogInformation("Successfully cleaned up file: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clean up file: {FilePath}", filePath);
            throw;
        }
    }
}