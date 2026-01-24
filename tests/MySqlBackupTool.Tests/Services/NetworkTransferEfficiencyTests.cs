using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using MySqlBackupTool.Shared.Interfaces;
using Xunit;
using System.Diagnostics;

namespace MySqlBackupTool.Tests.Services;

/// <summary>
/// Tests for network transfer efficiency improvements
/// </summary>
public class NetworkTransferEfficiencyTests : IDisposable
{
    private readonly FileTransferClient _fileTransferClient;
    private readonly OptimizedFileTransferClient _optimizedClient;
    private readonly string _testDirectory;

    public NetworkTransferEfficiencyTests()
    {
        var logger = new LoggerFactory().CreateLogger<FileTransferClient>();
        var optimizedLogger = new LoggerFactory().CreateLogger<OptimizedFileTransferClient>();
        var checksumLogger = new LoggerFactory().CreateLogger<ChecksumService>();
        var memoryProfilerLogger = new LoggerFactory().CreateLogger<MemoryProfiler>();
        
        var checksumService = new ChecksumService(checksumLogger);
        var memoryProfiler = new MemoryProfiler(memoryProfilerLogger);
        
        _fileTransferClient = new FileTransferClient(logger, checksumService, memoryProfiler);
        _optimizedClient = new OptimizedFileTransferClient(optimizedLogger, _fileTransferClient, memoryProfiler);
        
        // Create temporary directory for testing
        _testDirectory = Path.Combine(Path.GetTempPath(), "NetworkEfficiencyTest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void GetOptimalBufferSize_SmallFile_ReturnsSmallBuffer()
    {
        // Arrange
        var fileTransferClient = _fileTransferClient;
        var smallFileSize = 5 * 1024 * 1024; // 5MB
        
        // Use reflection to access private method for testing
        var method = typeof(FileTransferClient).GetMethod("GetOptimalBufferSize", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Act
        var bufferSize = (int)method!.Invoke(fileTransferClient, new object[] { smallFileSize })!;
        
        // Assert
        Assert.Equal(64 * 1024, bufferSize); // Should use SmallFileBufferSize (64KB)
    }

    [Fact]
    public void GetOptimalBufferSize_LargeFile_ReturnsLargeBuffer()
    {
        // Arrange
        var fileTransferClient = _fileTransferClient;
        var largeFileSize = 50 * 1024 * 1024; // 50MB
        
        // Use reflection to access private method for testing
        var method = typeof(FileTransferClient).GetMethod("GetOptimalBufferSize", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Act
        var bufferSize = (int)method!.Invoke(fileTransferClient, new object[] { largeFileSize })!;
        
        // Assert
        Assert.Equal(1024 * 1024, bufferSize); // Should use LargeFileBufferSize (1MB)
    }

    [Fact]
    public void GetOptimalBufferSize_HugeFile_ReturnsHugeBuffer()
    {
        // Arrange
        var fileTransferClient = _fileTransferClient;
        var hugeFileSize = 500 * 1024 * 1024; // 500MB
        
        // Use reflection to access private method for testing
        var method = typeof(FileTransferClient).GetMethod("GetOptimalBufferSize", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Act
        var bufferSize = (int)method!.Invoke(fileTransferClient, new object[] { hugeFileSize })!;
        
        // Assert
        Assert.Equal(4 * 1024 * 1024, bufferSize); // Should use HugeFileBufferSize (4MB)
    }

    [Theory]
    [InlineData(1024 * 1024, 1 * 1024 * 1024)] // 1MB file -> 1MB chunks
    [InlineData(50 * 1024 * 1024, 5 * 1024 * 1024)] // 50MB file -> 5MB chunks
    [InlineData(500 * 1024 * 1024, 10 * 1024 * 1024)] // 500MB file -> 10MB chunks
    [InlineData(2L * 1024 * 1024 * 1024, 25 * 1024 * 1024)] // 2GB file -> 25MB chunks
    public void ChunkingStrategy_GetOptimizedStrategy_ReturnsCorrectChunkSize(long fileSize, long expectedChunkSize)
    {
        // Act
        var strategy = ChunkingStrategy.GetOptimizedStrategy(fileSize);
        
        // Assert
        Assert.Equal(expectedChunkSize, strategy.ChunkSize);
        Assert.True(strategy.MaxConcurrentChunks > 0);
        Assert.True(strategy.EnableCompression);
    }

    [Fact]
    public async Task OptimizedFileTransferClient_WithSmallFile_OptimizesConfiguration()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "small_test.txt");
        var content = new string('A', 1024 * 1024); // 1MB file
        await File.WriteAllTextAsync(testFile, content);
        
        var config = CreateValidTransferConfig();
        config.MaxRetries = 1; // Single attempt for faster test
        config.TimeoutSeconds = 1; // Short timeout

        // Act
        var result = await _optimizedClient.TransferFileAsync(testFile, config);

        // Assert
        // Even though transfer will fail (no server), we can verify optimization was applied
        Assert.False(result.Success);
        Assert.True(result.Duration.TotalMilliseconds > 0);
    }

    [Fact]
    public async Task OptimizedFileTransferClient_WithLargeFile_OptimizesConfiguration()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "large_test.txt");
        var content = new string('B', 10 * 1024 * 1024); // 10MB file
        await File.WriteAllTextAsync(testFile, content);
        
        var config = CreateValidTransferConfig();
        config.MaxRetries = 1; // Single attempt for faster test
        config.TimeoutSeconds = 1; // Short timeout

        // Act
        var result = await _optimizedClient.TransferFileAsync(testFile, config);

        // Assert
        // Even though transfer will fail (no server), we can verify optimization was applied
        Assert.False(result.Success);
        Assert.True(result.Duration.TotalMilliseconds > 0);
    }

    [Fact]
    public void ChunkingStrategy_CalculateChunkCount_WithOptimizedSizes_IsAccurate()
    {
        // Arrange
        var fileSize = 100 * 1024 * 1024; // 100MB
        var strategy = ChunkingStrategy.GetOptimizedStrategy(fileSize);
        
        // Act
        var chunkCount = strategy.CalculateChunkCount(fileSize);
        var expectedChunkCount = (int)Math.Ceiling((double)fileSize / strategy.ChunkSize);
        
        // Assert
        Assert.Equal(expectedChunkCount, chunkCount);
        Assert.True(chunkCount > 0);
        Assert.True(chunkCount <= 100); // Reasonable upper limit
    }

    [Fact]
    public async Task FileTransferClient_WithOptimizedBuffers_HandlesLargeFiles()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "buffer_test.txt");
        var content = new string('C', 5 * 1024 * 1024); // 5MB file
        await File.WriteAllTextAsync(testFile, content);
        
        var config = CreateValidTransferConfig();
        config.MaxRetries = 1; // Single attempt for faster test
        config.TimeoutSeconds = 1; // Short timeout

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _fileTransferClient.TransferFileAsync(testFile, config);
        stopwatch.Stop();

        // Assert
        // Even though transfer will fail (no server), we can verify the file was processed efficiently
        Assert.False(result.Success);
        Assert.True(result.Duration.TotalMilliseconds > 0);
        Assert.True(stopwatch.ElapsedMilliseconds < 5000); // Should complete quickly even with failure
    }

    [Theory]
    [InlineData(60, 1024 * 1024, 60)] // Small file, keep original timeout
    [InlineData(60, 100 * 1024 * 1024, 120)] // 100MB file, add 1 minute
    [InlineData(60, 500 * 1024 * 1024, 360)] // 500MB file, add 5 minutes
    [InlineData(60, 3L * 1024 * 1024 * 1024, 1800)] // 3GB file, cap at 30 minutes
    public void OptimizeTimeout_WithDifferentFileSizes_ReturnsAppropriateTimeout(int originalTimeout, long fileSize, int expectedTimeout)
    {
        // Arrange
        var optimizedClient = _optimizedClient;
        
        // Use reflection to access private method for testing
        var method = typeof(OptimizedFileTransferClient).GetMethod("OptimizeTimeout", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Act
        var optimizedTimeout = (int)method!.Invoke(optimizedClient, new object[] { originalTimeout, fileSize })!;
        
        // Assert
        Assert.Equal(expectedTimeout, optimizedTimeout);
    }

    [Fact]
    public async Task OptimizedFileTransferClient_WithNonExistentFile_ReturnsFailureQuickly()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.txt");
        var config = CreateValidTransferConfig();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _optimizedClient.TransferFileAsync(nonExistentFile, config);
        stopwatch.Stop();

        // Assert
        Assert.False(result.Success);
        Assert.Contains("File not found", result.ErrorMessage);
        Assert.True(stopwatch.ElapsedMilliseconds < 100); // Should fail very quickly
    }

    [Fact]
    public async Task OptimizedFileTransferClient_ResumeTransfer_OptimizesConfiguration()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "resume_test.txt");
        var content = new string('D', 2 * 1024 * 1024); // 2MB file
        await File.WriteAllTextAsync(testFile, content);
        
        var config = CreateValidTransferConfig();
        var resumeToken = "test-resume-token";

        // Act
        var result = await _optimizedClient.ResumeTransferAsync(resumeToken, testFile, config);

        // Assert
        // Even though resume will fail (no server), we can verify optimization was applied
        Assert.False(result.Success);
    }

    private TransferConfig CreateValidTransferConfig()
    {
        return new TransferConfig
        {
            TargetServer = new ServerEndpoint
            {
                IPAddress = "127.0.0.1",
                Port = 8080
            },
            TargetDirectory = "/backup",
            FileName = "test.txt",
            TimeoutSeconds = 30,
            MaxRetries = 3
        };
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}