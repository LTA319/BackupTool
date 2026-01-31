using System.IO.Compression;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using Microsoft.Extensions.Logging;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 压缩目录和管理临时文件的服务，具有优化的流处理功能 / Service for compressing directories and managing temporary files with optimized streaming
/// </summary>
public class CompressionService : ICompressionService
{
    /// <summary>
    /// 日志记录器 / Logger
    /// </summary>
    private readonly ILogger<CompressionService> _logger;
    
    /// <summary>
    /// 内存分析器，可选 / Memory profiler, optional
    /// </summary>
    private readonly IMemoryProfiler? _memoryProfiler;
    
    // 优化的流配置 / Optimized streaming configuration
    private const int DefaultBufferSize = 1024 * 1024; // 1MB缓冲区，提高I/O性能 / 1MB buffer for better I/O performance
    private const int LargeFileBufferSize = 4 * 1024 * 1024; // 4MB缓冲区用于>100MB的文件 / 4MB buffer for files > 100MB
    private const long LargeFileThreshold = 100 * 1024 * 1024; // 100MB阈值 / 100MB threshold
    // 内存管理配置 / Memory management configuration
    private const int MemoryPressureThreshold = 100; // GC检查前处理的文件数 / Files processed before GC check
    private const int LargeFileGCInterval = 10; // 大文件的GC检查间隔 / GC check interval for large files
    private const int SmallFileGCInterval = 200; // 小文件的GC检查间隔 / GC check interval for small files (MemoryPressureThreshold * 2)
    private const int ProgressReportingDivisor = 20; // 每1/20总块数报告进度 / Report progress every 1/20th of total chunks
    private const int PeriodicFlushMultiplier = 10; // 每10个缓冲区大小刷新一次 / Flush every 10 buffer sizes
    private const int ChunkRetryDelayBaseMs = 100; // 块重试的基础延迟 / Base delay for chunk retry
    private const long PeriodicMemoryCheckInterval = 50 * 1024 * 1024; // 大文件每50MB检查一次 / Every 50MB for large files
    private static readonly int MaxConcurrentFiles = Environment.ProcessorCount; // 并行处理限制 / Parallel processing limit

    /// <summary>
    /// 初始化压缩服务 / Initializes the compression service
    /// </summary>
    /// <param name="logger">日志记录器 / Logger</param>
    /// <param name="memoryProfiler">内存分析器，可选 / Memory profiler, optional</param>
    /// <exception cref="ArgumentNullException">当日志记录器为null时抛出 / Thrown when logger is null</exception>
    public CompressionService(ILogger<CompressionService> logger, IMemoryProfiler? memoryProfiler = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryProfiler = memoryProfiler;
    }

    /// <summary>
    /// 将目录压缩为zip文件，支持进度报告和优化流处理 / Compresses a directory into a zip file with progress reporting and optimized streaming
    /// </summary>
    /// <param name="sourcePath">要压缩的目录路径 / Path to the directory to compress</param>
    /// <param name="targetPath">压缩文件的创建路径 / Path where the compressed file should be created</param>
    /// <param name="progress">压缩操作的进度报告器 / Progress reporter for compression operations</param>
    /// <returns>创建的压缩文件路径 / Path to the created compressed file</returns>
    /// <exception cref="ArgumentException">当源路径或目标路径为空时抛出 / Thrown when source or target path is empty</exception>
    /// <exception cref="DirectoryNotFoundException">当源目录不存在时抛出 / Thrown when source directory is not found</exception>
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
            // 确保目标目录存在 / Ensure target directory exists
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // 获取所有文件用于进度计算和优化规划 / Get all files for progress calculation and optimization planning
            _memoryProfiler?.RecordSnapshot(operationId, "FileDiscovery", "Discovering files to compress");
            var allFiles = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
            var totalFiles = allFiles.Length;
            var totalBytes = allFiles.Sum(file => new FileInfo(file).Length);

            _logger.LogDebug("Found {TotalFiles} files totaling {TotalBytes} bytes", totalFiles, totalBytes);
            _memoryProfiler?.RecordSnapshot(operationId, "FileDiscoveryComplete", $"Found {totalFiles} files, {totalBytes} bytes");

            // 分析文件以制定优化策略 / Analyze files for optimization strategy
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

            // 使用优化设置创建zip文件 / Create the zip file with optimized settings
            _memoryProfiler?.RecordSnapshot(operationId, "CreateArchive", "Creating optimized ZIP archive");
            using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, DefaultBufferSize);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, false);
            
            long processedBytes = 0;
            int processedFiles = 0;

            // 首先处理大文件，使用优化流处理 / Process large files first with optimized streaming
            foreach (var filePath in largeFiles)
            {
                var result = await ProcessLargeFileAsync(archive, sourcePath, filePath, operationId);
                processedBytes += result.BytesProcessed;
                processedFiles++;

                // 更新进度 / Update progress
                compressionProgress.CurrentFile = result.RelativePath;
                compressionProgress.ProcessedBytes = processedBytes;
                compressionProgress.ProcessedFiles = processedFiles;
                compressionProgress.Progress = totalBytes > 0 ? (double)processedBytes / totalBytes : 1.0;

                progress?.Report(compressionProgress);

                // 大文件的内存管理 / Memory management for large files
                if (processedFiles % LargeFileGCInterval == 0) // 大文件更频繁的GC / More frequent GC for large files
                {
                    _memoryProfiler?.RecordSnapshot(operationId, "LargeFileProgress", 
                        $"Processed {processedFiles} large files, {processedBytes} bytes");
                    
                    // 强制GC以管理大文件操作的内存压力 / Force GC to manage memory pressure from large file operations
                    _memoryProfiler?.ForceGarbageCollection(operationId);
                }
            }

            // 使用批处理优化处理小文件 / Process small files with batch optimization
            await ProcessSmallFilesBatchAsync(archive, sourcePath, smallFiles, compressionProgress, 
                progress, operationId, processedBytes, processedFiles, totalBytes);

            _memoryProfiler?.RecordSnapshot(operationId, "CompressionComplete", "Compression completed, finalizing archive");

            _logger.LogInformation("Successfully compressed {ProcessedFiles} files ({ProcessedBytes} bytes) to {TargetPath} using optimized streaming", 
                processedFiles + smallFiles.Count, processedBytes + smallFiles.Sum(f => new FileInfo(f).Length), targetPath);

            _memoryProfiler?.RecordSnapshot(operationId, "Success", "Optimized compression operation completed successfully");
            
            // 获取内存配置文件和建议 / Get memory profile and recommendations
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
            
            // 如果存在部分文件则清理 / Clean up partial file if it exists
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
    /// 清理压缩过程中创建的临时文件 / Cleans up temporary files created during compression
    /// </summary>
    /// <param name="filePath">要清理的文件路径 / Path to the file to clean up</param>
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
            
            // 使用Task.Run使同步的File.Delete操作异步化 / Use Task.Run to make the synchronous File.Delete operation async
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
    /// 使用优化流处理和缓冲区管理处理大文件 / Processes a large file with optimized streaming and buffer management
    /// </summary>
    /// <param name="archive">ZIP归档 / ZIP archive</param>
    /// <param name="sourcePath">源路径 / Source path</param>
    /// <param name="filePath">文件路径 / File path</param>
    /// <param name="operationId">操作ID / Operation ID</param>
    /// <returns>相对路径和处理的字节数 / Relative path and bytes processed</returns>
    private async Task<(string RelativePath, long BytesProcessed)> ProcessLargeFileAsync(
        ZipArchive archive, string sourcePath, string filePath, string operationId)
    {
        var relativePath = Path.GetRelativePath(sourcePath, filePath);
        var fileInfo = new FileInfo(filePath);
        
        _logger.LogDebug("Processing large file: {RelativePath} ({FileSize} bytes)", relativePath, fileInfo.Length);
        _memoryProfiler?.RecordSnapshot(operationId, "LargeFileStart", $"Starting large file: {relativePath}");

        // 为大文件创建具有最佳压缩的条目 / Create entry with optimal compression for large files
        var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
        
        // 为大文件使用更大的缓冲区以提高I/O性能 / Use larger buffer for large files to improve I/O performance
        var bufferSize = LargeFileBufferSize;
        var buffer = new byte[bufferSize];
        
        using var entryStream = entry.Open();
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize);
        
        long totalBytesRead = 0;
        int bytesRead;
        
        // 使用手动缓冲区管理的优化流处理 / Optimized streaming with manual buffer management
        while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await entryStream.WriteAsync(buffer, 0, bytesRead);
            totalBytesRead += bytesRead;
            
            // 对非常大的文件进行定期内存压力检查 / Periodic memory pressure check for very large files
            if (totalBytesRead % PeriodicMemoryCheckInterval == 0) // 每50MB / Every 50MB
            {
                _memoryProfiler?.RecordSnapshot(operationId, "LargeFileProgress", 
                    $"Processed {totalBytesRead}/{fileInfo.Length} bytes of {relativePath}");
            }
        }
        
        _memoryProfiler?.RecordSnapshot(operationId, "LargeFileComplete", $"Completed large file: {relativePath}");
        
        return (relativePath, fileInfo.Length);
    }

    /// <summary>
    /// 以优化批次处理小文件以减少开销 / Processes small files in optimized batches to reduce overhead
    /// </summary>
    /// <param name="archive">ZIP归档 / ZIP archive</param>
    /// <param name="sourcePath">源路径 / Source path</param>
    /// <param name="smallFiles">小文件列表 / List of small files</param>
    /// <param name="compressionProgress">压缩进度 / Compression progress</param>
    /// <param name="progress">进度报告器 / Progress reporter</param>
    /// <param name="operationId">操作ID / Operation ID</param>
    /// <param name="initialProcessedBytes">初始处理字节数 / Initial processed bytes</param>
    /// <param name="initialProcessedFiles">初始处理文件数 / Initial processed files</param>
    /// <param name="totalBytes">总字节数 / Total bytes</param>
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
        
        // 使用标准缓冲区大小处理小文件 / Process small files with standard buffer size
        var buffer = new byte[DefaultBufferSize];
        
        foreach (var filePath in smallFiles)
        {
            var relativePath = Path.GetRelativePath(sourcePath, filePath);
            compressionProgress.CurrentFile = relativePath;
            
            _logger.LogDebug("Compressing small file: {RelativePath}", relativePath);

            // 使用最佳压缩将文件添加到归档 / Add file to archive with optimal compression
            var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
            
            using var entryStream = entry.Open();
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, DefaultBufferSize);
            
            // 使用我们的缓冲区进行优化复制 / Use optimized copying with our buffer
            await CopyStreamOptimizedAsync(fileStream, entryStream, buffer);
            
            var fileInfo = new FileInfo(filePath);
            processedBytes += fileInfo.Length;
            processedFiles++;

            // 更新进度 / Update progress
            compressionProgress.ProcessedBytes = processedBytes;
            compressionProgress.ProcessedFiles = processedFiles;
            compressionProgress.Progress = totalBytes > 0 ? (double)processedBytes / totalBytes : 1.0;

            progress?.Report(compressionProgress);

            // 内存管理 - 每批文件检查一次 / Memory management - check every batch of files
            if (processedFiles % MemoryPressureThreshold == 0)
            {
                _memoryProfiler?.RecordSnapshot(operationId, "SmallFilesBatchProgress", 
                    $"Processed {processedFiles} files, {processedBytes} bytes");
                
                // 定期GC以防止内存积累 / Periodic GC to prevent memory buildup
                if (processedFiles % SmallFileGCInterval == 0)
                {
                    _memoryProfiler?.ForceGarbageCollection(operationId);
                }
            }
        }
        
        _memoryProfiler?.RecordSnapshot(operationId, "SmallFilesBatchComplete", $"Completed batch processing of {smallFiles.Count} small files");
    }

    /// <summary>
    /// 使用可重用缓冲区的优化流复制以减少分配 / Optimized stream copying with reusable buffer to reduce allocations
    /// </summary>
    /// <param name="source">源流 / Source stream</param>
    /// <param name="destination">目标流 / Destination stream</param>
    /// <param name="buffer">缓冲区 / Buffer</param>
    private static async Task CopyStreamOptimizedAsync(Stream source, Stream destination, byte[] buffer)
    {
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await destination.WriteAsync(buffer, 0, bytesRead);
        }
    }
}