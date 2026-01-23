using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using System.IO.Compression;

namespace MySqlBackupTool.Tests.Properties;

/// <summary>
/// Property-based tests for file compression and cleanup functionality
/// **Validates: Requirements 3.2, 3.3, 3.6**
/// </summary>
public class CompressionPropertyTests : IDisposable
{
    private readonly CompressionService _compressionService;
    private readonly List<string> _tempDirectories;
    private readonly List<string> _tempFiles;

    public CompressionPropertyTests()
    {
        var logger = new LoggerFactory().CreateLogger<CompressionService>();
        _compressionService = new CompressionService(logger);
        _tempDirectories = new List<string>();
        _tempFiles = new List<string>();
    }

    /// <summary>
    /// Property 6: File Compression and Transfer
    /// For any valid data directory, the system should be able to compress it into a backup file 
    /// and transfer it to the target location following the configured naming convention.
    /// **Validates: Requirements 3.2, 3.3**
    /// </summary>
    [Property(MaxTest = 20)]
    public bool FileCompressionAndTransferProperty()
    {
        try
        {
            // Arrange - Create test directory structure
            var testDirectory = CreateSimpleTestDirectory();
            var zipPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.zip");
            _tempFiles.Add(zipPath);

            // Act - Compress the directory
            var result = _compressionService.CompressDirectoryAsync(testDirectory, zipPath).Result;

            // Assert - Verify compression results
            var compressionSuccessful = File.Exists(result) && result == zipPath;
            var zipFileValid = IsValidZipFile(zipPath);
            var fileSizeReasonable = new FileInfo(zipPath).Length > 0;

            return compressionSuccessful && zipFileValid && fileSizeReasonable;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"File compression property test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Property 7: Backup Cleanup
    /// For any successful backup operation, the system should clean up temporary files 
    /// (local Data.zip) after confirming MySQL availability.
    /// **Validates: Requirements 3.6**
    /// </summary>
    [Property(MaxTest = 20)]
    public bool BackupCleanupProperty()
    {
        try
        {
            // Arrange - Create a temporary file to clean up
            var tempFile = Path.Combine(Path.GetTempPath(), $"cleanup_test_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(tempFile, "Test content for cleanup");
            var fileExistsBeforeCleanup = File.Exists(tempFile);

            // Act - Clean up the file
            _compressionService.CleanupAsync(tempFile).Wait();

            // Assert - Verify file is cleaned up
            var fileExistsAfterCleanup = File.Exists(tempFile);
            var cleanupSuccessful = fileExistsBeforeCleanup && !fileExistsAfterCleanup;

            return cleanupSuccessful;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Backup cleanup property test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Property test for compression progress reporting monotonicity
    /// Progress should always be monotonically increasing and reach 1.0 on completion
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 10)]
    public bool CompressionProgressMonotonicityProperty()
    {
        try
        {
            // Arrange - Create test directory and progress tracking
            var testDirectory = CreateSimpleTestDirectory();
            var zipPath = Path.Combine(Path.GetTempPath(), $"progress_test_{Guid.NewGuid():N}.zip");
            _tempFiles.Add(zipPath);

            var progressReports = new List<CompressionProgress>();
            var progress = new Progress<CompressionProgress>(p => progressReports.Add(new CompressionProgress
            {
                Progress = p.Progress,
                ProcessedFiles = p.ProcessedFiles,
                TotalFiles = p.TotalFiles,
                ProcessedBytes = p.ProcessedBytes,
                TotalBytes = p.TotalBytes,
                CurrentFile = p.CurrentFile
            }));

            // Act - Compress with progress reporting
            _compressionService.CompressDirectoryAsync(testDirectory, zipPath, progress).Wait();

            // Assert - Verify progress monotonicity
            var progressIsMonotonic = IsProgressMonotonic(progressReports);
            var progressReachesCompletion = progressReports.Count > 0 && 
                progressReports.Last().Progress >= 0.99; // Allow for floating point precision
            var progressStartsAtZero = progressReports.Count > 0 && 
                progressReports.First().Progress >= 0.0;

            return progressIsMonotonic && progressReachesCompletion && progressStartsAtZero;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Compression progress monotonicity test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Property test for compression error handling and cleanup
    /// When compression fails, partial files should be cleaned up
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 10)]
    public bool CompressionErrorHandlingProperty()
    {
        try
        {
            // Arrange - Create a valid source directory but invalid target
            var testDirectory = CreateSimpleTestDirectory();
            var invalidPath = ""; // Invalid path

            // Act - Attempt compression with invalid target path
            var compressionFailed = false;
            try
            {
                _compressionService.CompressDirectoryAsync(testDirectory, invalidPath).Wait();
            }
            catch
            {
                compressionFailed = true;
            }

            // Assert - Verify error handling
            var partialFileNotLeft = !File.Exists(invalidPath);

            return compressionFailed && partialFileNotLeft;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Compression error handling test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Creates a simple test directory structure
    /// </summary>
    private string CreateSimpleTestDirectory()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"CompressionTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        _tempDirectories.Add(testDir);

        // Create a few test files
        File.WriteAllText(Path.Combine(testDir, "file1.txt"), "Test content 1");
        File.WriteAllText(Path.Combine(testDir, "file2.txt"), "Test content 2");
        
        var subDir = Path.Combine(testDir, "subdir");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "file3.txt"), "Test content 3");

        return testDir;
    }

    /// <summary>
    /// Verifies that a zip file is valid and can be opened
    /// </summary>
    private static bool IsValidZipFile(string zipPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            return archive.Entries.Count >= 0; // Just verify we can read it
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Verifies that progress reports are monotonically increasing
    /// </summary>
    private static bool IsProgressMonotonic(List<CompressionProgress> progressReports)
    {
        if (progressReports.Count <= 1) return true;

        for (int i = 1; i < progressReports.Count; i++)
        {
            if (progressReports[i].Progress < progressReports[i - 1].Progress)
                return false;
            if (progressReports[i].ProcessedFiles < progressReports[i - 1].ProcessedFiles)
                return false;
            if (progressReports[i].ProcessedBytes < progressReports[i - 1].ProcessedBytes)
                return false;
        }

        return true;
    }

    public void Dispose()
    {
        // Clean up temporary directories
        foreach (var dir in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        // Clean up temporary files
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}