using System.IO.Compression;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using Microsoft.Extensions.Logging;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Service for compressing directories and managing temporary files with optimized streaming
/// </summary>
public class CompressionService : ICompressionService
{
    private readonly ILogger<CompressionService> _logger;
    private readonly IMemoryProfiler? _memoryProfiler;
    
    // Optimized streaming configuration
    private const int DefaultBufferSize = 1024 * 1024; // 1MB buffer for better I/O performance
    private const int LargeFileBufferSize = 4 * 1024 * 1024; // 4MB buffer for files > 100MB
    private const long LargeFileThreshold = 100 * 1024 * 1024; // 100MB threshold
    private const int MemoryPressureThreshold = 100; // Files processed before GC check
    private static readonly int MaxConcurrentFiles = Environment.ProcessorCount; // Parallel processing limit

    public CompressionService(ILogger<CompressionService> logger, IMemoryProfiler? memoryProfiler = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryProfiler = memoryProfiler;
    }

    /// <summary>
    /// Compresses a directory into a zip file with progress reporting and optimized streaming
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

        var operationId = $"compress-{Path.GetFileName(sourcePath)}-{Guid.NewGuid():N}";
        _memoryProfiler?.StartProfiling(operationId, "Compression");
        _memoryProfiler?.RecordSnapshot(operationId, "Start", $"Starting optimized compression: {sourcePath} -> {targetPath}");

        _logger.LogInformation("Starting optimized compression of directory {SourcePath} to {TargetPath}", sourcePath, targetPath);

        try
        {
            // Ensure target directory exists
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // Get all files for progress calculation and optimization planning
            _memoryProfiler?.RecordSnapshot(operationId, "FileDiscovery", "Discovering files to compress");
            var allFiles = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
            var totalFiles = allFiles.Length;
            var totalBytes = allFiles.Sum(file => new FileInfo(file).Length);

            _logger.LogDebug("Found {TotalFiles} files totaling {TotalBytes} bytes", totalFiles, totalBytes);
            _memoryProfiler?.RecordSnapshot(operationId, "FileDiscoveryComplete", $"Found {totalFiles} files, {totalBytes} bytes");

            // Analyze files for optimization strategy
            var largeFiles = allFiles.Where(f => new FileInfo(f).Length > LargeFileThreshold).ToList();
            var smallFiles = allFiles.Where(f => new FileInfo(f).Length <= LargeFileThreshold).ToList();
            
            _logger.LogDebug("Optimization analysis: {LargeFiles} large files (>{LargeFileThreshold} bytes), {SmallFiles} small files", 
                largeFiles.Count, LargeFileThreshold, smallFiles.Count);

            var compressionProgress = new CompressionProgress
            {
                TotalFiles = totalFiles,
                TotalBytes = totalBytes
            };

            progress?.Report(compressionProgress);

            // Create the zip file with optimized settings
            _memoryProfiler?.RecordSnapshot(operationId, "CreateArchive", "Creating optimized ZIP archive");
            using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, DefaultBufferSize);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, false);
            
            long processedBytes = 0;
            int processedFiles = 0;

            // Process large files first with optimized streaming
            foreach (var filePath in largeFiles)
            {
                var result = await ProcessLargeFileAsync(archive, sourcePath, filePath, operationId);
                processedBytes += result.BytesProcessed;
                processedFiles++;

                // Update progress
                compressionProgress.CurrentFile = result.RelativePath;
                compressionProgress.ProcessedBytes = processedBytes;
                compressionProgress.ProcessedFiles = processedFiles;
                compressionProgress.Progress = totalBytes > 0 ? (double)processedBytes / totalBytes : 1.0;

                progress?.Report(compressionProgress);

                // Memory management for large files
                if (processedFiles % 10 == 0) // More frequent GC for large files
                {
                    _memoryProfiler?.RecordSnapshot(operationId, "LargeFileProgress", 
                        $"Processed {processedFiles} large files, {processedBytes} bytes");
                    
                    // Force GC to manage memory pressure from large file operations
                    _memoryProfiler?.ForceGarbageCollection(operationId);
                }
            }

            // Process small files with batch optimization
            await ProcessSmallFilesBatchAsync(archive, sourcePath, smallFiles, compressionProgress, 
                progress, operationId, processedBytes, processedFiles, totalBytes);

            _memoryProfiler?.RecordSnapshot(operationId, "CompressionComplete", "Compression completed, finalizing archive");

            _logger.LogInformation("Successfully compressed {ProcessedFiles} files ({ProcessedBytes} bytes) to {TargetPath} using optimized streaming", 
                processedFiles + smallFiles.Count, processedBytes + smallFiles.Sum(f => new FileInfo(f).Length), targetPath);

            _memoryProfiler?.RecordSnapshot(operationId, "Success", "Optimized compression operation completed successfully");
            
            // Get memory profile and recommendations
            var profile = _memoryProfiler?.StopProfiling(operationId);
            if (profile != null)
            {
                var recommendations = _memoryProfiler?.GetRecommendations(profile);
                if (recommendations?.Any() == true)
                {
                    _logger.LogInformation("Memory profiling recommendations for optimized compression operation:");
                    foreach (var rec in recommendations)
                    {
                        _logger.LogInformation("- {Priority}: {Title} - {Description}", 
                            rec.Priority, rec.Title, rec.Description);
                    }
                }
            }

            return targetPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compress directory {SourcePath} to {TargetPath}", sourcePath, targetPath);
            _memoryProfiler?.RecordSnapshot(operationId, "Exception", $"Compression failed: {ex.Message}");
            
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
            
            _memoryProfiler?.StopProfiling(operationId);
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

    /// <summary>
    /// Processes a large file with optimized streaming and buffer management
    /// </summary>
    private async Task<(string RelativePath, long BytesProcessed)> ProcessLargeFileAsync(
        ZipArchive archive, string sourcePath, string filePath, string operationId)
    {
        var relativePath = Path.GetRelativePath(sourcePath, filePath);
        var fileInfo = new FileInfo(filePath);
        
        _logger.LogDebug("Processing large file: {RelativePath} ({FileSize} bytes)", relativePath, fileInfo.Length);
        _memoryProfiler?.RecordSnapshot(operationId, "LargeFileStart", $"Starting large file: {relativePath}");

        // Create entry with optimal compression for large files
        var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
        
        // Use larger buffer for large files to improve I/O performance
        var bufferSize = LargeFileBufferSize;
        var buffer = new byte[bufferSize];
        
        using var entryStream = entry.Open();
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize);
        
        long totalBytesRead = 0;
        int bytesRead;
        
        // Optimized streaming with manual buffer management
        while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await entryStream.WriteAsync(buffer, 0, bytesRead);
            totalBytesRead += bytesRead;
            
            // Periodic memory pressure check for very large files
            if (totalBytesRead % (50 * 1024 * 1024) == 0) // Every 50MB
            {
                _memoryProfiler?.RecordSnapshot(operationId, "LargeFileProgress", 
                    $"Processed {totalBytesRead}/{fileInfo.Length} bytes of {relativePath}");
            }
        }
        
        _memoryProfiler?.RecordSnapshot(operationId, "LargeFileComplete", $"Completed large file: {relativePath}");
        
        return (relativePath, fileInfo.Length);
    }

    /// <summary>
    /// Processes small files in optimized batches to reduce overhead
    /// </summary>
    private async Task ProcessSmallFilesBatchAsync(
        ZipArchive archive, 
        string sourcePath, 
        List<string> smallFiles, 
        CompressionProgress compressionProgress,
        IProgress<CompressionProgress>? progress,
        string operationId,
        long initialProcessedBytes,
        int initialProcessedFiles,
        long totalBytes)
    {
        _logger.LogDebug("Processing {SmallFileCount} small files in optimized batches", smallFiles.Count);
        _memoryProfiler?.RecordSnapshot(operationId, "SmallFilesBatchStart", $"Starting batch processing of {smallFiles.Count} small files");

        long processedBytes = initialProcessedBytes;
        int processedFiles = initialProcessedFiles;
        
        // Process small files with standard buffer size
        var buffer = new byte[DefaultBufferSize];
        
        foreach (var filePath in smallFiles)
        {
            var relativePath = Path.GetRelativePath(sourcePath, filePath);
            compressionProgress.CurrentFile = relativePath;
            
            _logger.LogDebug("Compressing small file: {RelativePath}", relativePath);

            // Add file to archive with optimal compression
            var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
            
            using var entryStream = entry.Open();
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, DefaultBufferSize);
            
            // Use optimized copying with our buffer
            await CopyStreamOptimizedAsync(fileStream, entryStream, buffer);
            
            var fileInfo = new FileInfo(filePath);
            processedBytes += fileInfo.Length;
            processedFiles++;

            // Update progress
            compressionProgress.ProcessedBytes = processedBytes;
            compressionProgress.ProcessedFiles = processedFiles;
            compressionProgress.Progress = totalBytes > 0 ? (double)processedBytes / totalBytes : 1.0;

            progress?.Report(compressionProgress);

            // Memory management - check every batch of files
            if (processedFiles % MemoryPressureThreshold == 0)
            {
                _memoryProfiler?.RecordSnapshot(operationId, "SmallFilesBatchProgress", 
                    $"Processed {processedFiles} files, {processedBytes} bytes");
                
                // Periodic GC to prevent memory buildup
                if (processedFiles % (MemoryPressureThreshold * 2) == 0)
                {
                    _memoryProfiler?.ForceGarbageCollection(operationId);
                }
            }
        }
        
        _memoryProfiler?.RecordSnapshot(operationId, "SmallFilesBatchComplete", $"Completed batch processing of {smallFiles.Count} small files");
    }

    /// <summary>
    /// Optimized stream copying with reusable buffer to reduce allocations
    /// </summary>
    private static async Task CopyStreamOptimizedAsync(Stream source, Stream destination, byte[] buffer)
    {
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await destination.WriteAsync(buffer, 0, bytesRead);
        }
    }
}