using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;
using System.Diagnostics;

namespace MySqlBackupTool.Tests.Services;

/// <summary>
/// Tests for compression streaming optimizations
/// </summary>
public class CompressionStreamingOptimizationTests : IDisposable
{
    private readonly CompressionService _compressionService;
    private readonly string _testDirectory;
    private readonly string _tempDirectory;

    public CompressionStreamingOptimizationTests()
    {
        var logger = new LoggerFactory().CreateLogger<CompressionService>();
        _compressionService = new CompressionService(logger);
        
        // Create temporary directories for testing
        _testDirectory = Path.Combine(Path.GetTempPath(), "CompressionOptTest_" + Guid.NewGuid().ToString("N")[..8]);
        _tempDirectory = Path.Combine(Path.GetTempPath(), "CompressionOptTemp_" + Guid.NewGuid().ToString("N")[..8]);
        
        Directory.CreateDirectory(_testDirectory);
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task CompressDirectoryAsync_WithLargeFiles_UsesOptimizedStreaming()
    {
        // Arrange - Create a large file (10MB) to test large file optimization
        var largeFile = Path.Combine(_testDirectory, "large_file.txt");
        var zipPath = Path.Combine(_tempDirectory, "large_test.zip");
        
        // Create a 10MB file with compressible content (repeated pattern)
        var content = new byte[10 * 1024 * 1024]; // 10MB
        var pattern = "This is a test pattern that should compress well. "u8.ToArray();
        for (int i = 0; i < content.Length; i++)
        {
            content[i] = pattern[i % pattern.Length];
        }
        await File.WriteAllBytesAsync(largeFile, content);

        var progressReports = new List<CompressionProgress>();
        var progress = new Progress<CompressionProgress>(p => progressReports.Add(new CompressionProgress
        {
            Progress = p.Progress,
            CurrentFile = p.CurrentFile,
            ProcessedBytes = p.ProcessedBytes,
            TotalBytes = p.TotalBytes,
            ProcessedFiles = p.ProcessedFiles,
            TotalFiles = p.TotalFiles
        }));

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _compressionService.CompressDirectoryAsync(_testDirectory, zipPath, progress);
        
        stopwatch.Stop();

        // Assert
        Assert.Equal(zipPath, result);
        Assert.True(File.Exists(zipPath));
        Assert.True(new FileInfo(zipPath).Length > 0);
        Assert.NotEmpty(progressReports);
        
        // Verify progress reporting works with optimized streaming
        Assert.Contains(progressReports, p => p.Progress > 0.0);
        Assert.Contains(progressReports, p => p.Progress <= 1.0);
        Assert.Contains(progressReports, p => p.ProcessedFiles > 0);
        Assert.Contains(progressReports, p => p.ProcessedBytes > 0);
        
        // Log performance metrics
        var compressionRatio = (double)new FileInfo(zipPath).Length / content.Length;
        Assert.True(compressionRatio < 1.0, "Compression should reduce file size");
        
        // Performance should be reasonable for 10MB file
        Assert.True(stopwatch.ElapsedMilliseconds < 30000, "Compression should complete within 30 seconds");
    }

    [Fact]
    public async Task CompressDirectoryAsync_WithManySmallFiles_UsesBatchOptimization()
    {
        // Arrange - Create many small files to test batch optimization
        var zipPath = Path.Combine(_tempDirectory, "batch_test.zip");
        
        // Create 100 small files (1KB each)
        var fileCount = 100;
        var fileSize = 1024; // 1KB
        var totalSize = 0L;
        
        for (int i = 0; i < fileCount; i++)
        {
            var fileName = Path.Combine(_testDirectory, $"small_file_{i:D3}.txt");
            // Use compressible content instead of random
            var content = System.Text.Encoding.UTF8.GetBytes($"Small file {i} content. ".PadRight(fileSize, 'X'));
            await File.WriteAllBytesAsync(fileName, content);
            totalSize += content.Length;
        }

        var progressReports = new List<CompressionProgress>();
        var progress = new Progress<CompressionProgress>(p => progressReports.Add(new CompressionProgress
        {
            Progress = p.Progress,
            CurrentFile = p.CurrentFile,
            ProcessedBytes = p.ProcessedBytes,
            TotalBytes = p.TotalBytes,
            ProcessedFiles = p.ProcessedFiles,
            TotalFiles = p.TotalFiles
        }));

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _compressionService.CompressDirectoryAsync(_testDirectory, zipPath, progress);
        
        stopwatch.Stop();

        // Assert
        Assert.Equal(zipPath, result);
        Assert.True(File.Exists(zipPath));
        Assert.True(new FileInfo(zipPath).Length > 0);
        Assert.NotEmpty(progressReports);
        
        // Verify all files were processed
        var finalProgress = progressReports.Last();
        Assert.Equal(fileCount, finalProgress.ProcessedFiles);
        Assert.Equal(fileCount, finalProgress.TotalFiles);
        Assert.Equal(totalSize, finalProgress.ProcessedBytes);
        Assert.Equal(totalSize, finalProgress.TotalBytes);
        Assert.Equal(1.0, finalProgress.Progress, 2); // Within 2 decimal places
        
        // Performance should be good for many small files
        Assert.True(stopwatch.ElapsedMilliseconds < 10000, "Batch compression should complete within 10 seconds");
    }

    [Fact]
    public async Task CompressDirectoryAsync_WithMixedFileSizes_UsesHybridOptimization()
    {
        // Arrange - Create a mix of large and small files
        var zipPath = Path.Combine(_tempDirectory, "mixed_test.zip");
        
        // Create 1 large file (5MB) with compressible content
        var largeFile = Path.Combine(_testDirectory, "large.bin");
        var largeContent = new byte[5 * 1024 * 1024];
        var largePattern = "Large file pattern for compression testing. "u8.ToArray();
        for (int i = 0; i < largeContent.Length; i++)
        {
            largeContent[i] = largePattern[i % largePattern.Length];
        }
        await File.WriteAllBytesAsync(largeFile, largeContent);
        
        // Create 50 small files (10KB each) with compressible content
        var smallFileCount = 50;
        var smallFileSize = 10 * 1024;
        var totalSmallSize = 0L;
        
        for (int i = 0; i < smallFileCount; i++)
        {
            var fileName = Path.Combine(_testDirectory, $"small_{i:D2}.txt");
            var content = System.Text.Encoding.UTF8.GetBytes($"Small file {i} with compressible content. ".PadRight(smallFileSize, 'Y'));
            await File.WriteAllBytesAsync(fileName, content);
            totalSmallSize += content.Length;
        }

        var totalExpectedSize = largeContent.Length + totalSmallSize;
        var totalExpectedFiles = 1 + smallFileCount;

        var progressReports = new List<CompressionProgress>();
        var progress = new Progress<CompressionProgress>(p => progressReports.Add(new CompressionProgress
        {
            Progress = p.Progress,
            CurrentFile = p.CurrentFile,
            ProcessedBytes = p.ProcessedBytes,
            TotalBytes = p.TotalBytes,
            ProcessedFiles = p.ProcessedFiles,
            TotalFiles = p.TotalFiles
        }));

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _compressionService.CompressDirectoryAsync(_testDirectory, zipPath, progress);
        
        stopwatch.Stop();

        // Assert
        Assert.Equal(zipPath, result);
        Assert.True(File.Exists(zipPath));
        Assert.True(new FileInfo(zipPath).Length > 0);
        Assert.NotEmpty(progressReports);
        
        // Verify all files were processed correctly
        var finalProgress = progressReports.Last();
        Assert.Equal(totalExpectedFiles, finalProgress.ProcessedFiles);
        Assert.Equal(totalExpectedFiles, finalProgress.TotalFiles);
        Assert.Equal(totalExpectedSize, finalProgress.ProcessedBytes);
        Assert.Equal(totalExpectedSize, finalProgress.TotalBytes);
        Assert.Equal(1.0, finalProgress.Progress, 2);
        
        // Verify progress was reported throughout the operation
        Assert.True(progressReports.Count > 1, "Should have multiple progress reports");
        Assert.True(progressReports.Any(p => p.Progress > 0.0 && p.Progress < 1.0), 
            "Should have intermediate progress reports");
        
        // Performance should be reasonable for mixed file sizes
        Assert.True(stopwatch.ElapsedMilliseconds < 20000, "Mixed compression should complete within 20 seconds");
    }

    public void Dispose()
    {
        // Clean up test directories
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
        
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}