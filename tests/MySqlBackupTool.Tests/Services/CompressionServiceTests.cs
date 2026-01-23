using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;

namespace MySqlBackupTool.Tests.Services;

public class CompressionServiceTests : IDisposable
{
    private readonly CompressionService _compressionService;
    private readonly string _testDirectory;
    private readonly string _tempDirectory;

    public CompressionServiceTests()
    {
        var logger = new LoggerFactory().CreateLogger<CompressionService>();
        _compressionService = new CompressionService(logger);
        
        // Create temporary directories for testing
        _testDirectory = Path.Combine(Path.GetTempPath(), "CompressionTest_" + Guid.NewGuid().ToString("N")[..8]);
        _tempDirectory = Path.Combine(Path.GetTempPath(), "CompressionTemp_" + Guid.NewGuid().ToString("N")[..8]);
        
        Directory.CreateDirectory(_testDirectory);
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task CompressDirectoryAsync_WithValidDirectory_CreatesZipFile()
    {
        // Arrange
        var testFile1 = Path.Combine(_testDirectory, "test1.txt");
        var testFile2 = Path.Combine(_testDirectory, "subdir", "test2.txt");
        var zipPath = Path.Combine(_tempDirectory, "test.zip");

        Directory.CreateDirectory(Path.GetDirectoryName(testFile2)!);
        await File.WriteAllTextAsync(testFile1, "Test content 1");
        await File.WriteAllTextAsync(testFile2, "Test content 2");

        // Act
        var result = await _compressionService.CompressDirectoryAsync(_testDirectory, zipPath);

        // Assert
        Assert.Equal(zipPath, result);
        Assert.True(File.Exists(zipPath));
        Assert.True(new FileInfo(zipPath).Length > 0);
    }

    [Fact]
    public async Task CompressDirectoryAsync_WithProgressReporting_ReportsProgress()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        var zipPath = Path.Combine(_tempDirectory, "test.zip");
        await File.WriteAllTextAsync(testFile, "Test content");

        var progressReports = new List<CompressionProgress>();
        var progress = new Progress<CompressionProgress>(p => progressReports.Add(p));

        // Act
        await _compressionService.CompressDirectoryAsync(_testDirectory, zipPath, progress);

        // Assert
        Assert.NotEmpty(progressReports);
        Assert.True(progressReports.Any(p => p.Progress >= 0.0));
        Assert.True(progressReports.Any(p => p.Progress <= 1.0));
        Assert.Contains(progressReports, p => p.ProcessedFiles > 0);
    }

    [Fact]
    public async Task CompressDirectoryAsync_WithNullSourcePath_ThrowsArgumentException()
    {
        // Arrange
        var zipPath = Path.Combine(_tempDirectory, "test.zip");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _compressionService.CompressDirectoryAsync(null!, zipPath));
    }

    [Fact]
    public async Task CompressDirectoryAsync_WithNonExistentDirectory_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDirectory, "nonexistent");
        var zipPath = Path.Combine(_tempDirectory, "test.zip");

        // Act & Assert
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => 
            _compressionService.CompressDirectoryAsync(nonExistentPath, zipPath));
    }

    [Fact]
    public async Task CleanupAsync_WithExistingFile_DeletesFile()
    {
        // Arrange
        var testFile = Path.Combine(_tempDirectory, "cleanup_test.txt");
        await File.WriteAllTextAsync(testFile, "Test content");
        Assert.True(File.Exists(testFile));

        // Act
        await _compressionService.CleanupAsync(testFile);

        // Assert
        Assert.False(File.Exists(testFile));
    }

    [Fact]
    public async Task CleanupAsync_WithNonExistentFile_DoesNotThrow()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_tempDirectory, "nonexistent.txt");

        // Act & Assert (should not throw)
        await _compressionService.CleanupAsync(nonExistentFile);
    }

    [Fact]
    public async Task CleanupAsync_WithNullPath_DoesNotThrow()
    {
        // Act & Assert (should not throw)
        await _compressionService.CleanupAsync(null!);
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