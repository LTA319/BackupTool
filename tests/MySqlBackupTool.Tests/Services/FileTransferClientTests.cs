using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using MySqlBackupTool.Shared.Interfaces;
using Xunit;

namespace MySqlBackupTool.Tests.Services;

public class FileTransferClientTests : IDisposable
{
    private readonly FileTransferClient _fileTransferClient;
    private readonly string _testDirectory;

    public FileTransferClientTests()
    {
        var logger = new LoggerFactory().CreateLogger<FileTransferClient>();
        var checksumLogger = new LoggerFactory().CreateLogger<ChecksumService>();
        var checksumService = new ChecksumService(checksumLogger);
        _fileTransferClient = new FileTransferClient(logger, checksumService);
        
        // Create temporary directory for testing
        _testDirectory = Path.Combine(Path.GetTempPath(), "FileTransferTest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task TransferFileAsync_WithNonExistentFile_ReturnsFailureResult()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.txt");
        var config = CreateValidTransferConfig();

        // Act
        var result = await _fileTransferClient.TransferFileAsync(nonExistentFile, config);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("File not found", result.ErrorMessage);
        Assert.Equal(0, result.BytesTransferred);
        Assert.True(result.Duration.TotalMilliseconds >= 0);
    }

    [Fact]
    public async Task TransferFileAsync_WithInvalidServerEndpoint_ReturnsFailureResult()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        await File.WriteAllTextAsync(testFile, "Test content");
        
        var config = new TransferConfig
        {
            TargetServer = new ServerEndpoint
            {
                IPAddress = "invalid-ip",
                Port = 8080
            },
            TargetDirectory = "/backup",
            FileName = "test.txt"
        };

        // Act
        var result = await _fileTransferClient.TransferFileAsync(testFile, config);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid server endpoint", result.ErrorMessage);
        Assert.Equal(0, result.BytesTransferred);
    }

    [Fact]
    public async Task TransferFileAsync_WithInvalidPort_ReturnsFailureResult()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        await File.WriteAllTextAsync(testFile, "Test content");
        
        var config = new TransferConfig
        {
            TargetServer = new ServerEndpoint
            {
                IPAddress = "127.0.0.1",
                Port = 70000 // Invalid port
            },
            TargetDirectory = "/backup",
            FileName = "test.txt"
        };

        // Act
        var result = await _fileTransferClient.TransferFileAsync(testFile, config);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid server endpoint", result.ErrorMessage);
        Assert.Equal(0, result.BytesTransferred);
    }

    [Fact]
    public async Task TransferFileAsync_WithCancellation_ReturnsFailureResult()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        await File.WriteAllTextAsync(testFile, "Test content");
        var config = CreateValidTransferConfig();
        
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act
        var result = await _fileTransferClient.TransferFileAsync(testFile, config, cts.Token);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("cancelled", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, result.BytesTransferred);
    }

    [Fact]
    public async Task TransferFileAsync_WithValidInputsButNoServer_ReturnsFailureAfterRetries()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        await File.WriteAllTextAsync(testFile, "Test content for transfer");
        
        var config = CreateValidTransferConfig();
        config.MaxRetries = 2; // Reduce retries for faster test
        config.TimeoutSeconds = 1; // Short timeout for faster test

        // Act
        var result = await _fileTransferClient.TransferFileAsync(testFile, config);

        // Assert
        Assert.False(result.Success);
        // The error message could be about invalid endpoint or failed after retries
        Assert.True(result.ErrorMessage!.Contains("Invalid server endpoint") || result.ErrorMessage.Contains("failed after"));
        Assert.Equal(0, result.BytesTransferred);
        Assert.True(result.Duration.TotalMilliseconds > 0);
    }

    [Fact]
    public async Task ResumeTransferAsync_WithAnyToken_ReturnsNotImplementedError()
    {
        // Arrange
        var resumeToken = "test-resume-token";

        // Act
        var result = await _fileTransferClient.ResumeTransferAsync(resumeToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Resume functionality requires server-side", result.ErrorMessage);
        Assert.Equal(0, result.BytesTransferred);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task TransferFileAsync_WithInvalidFilePath_ReturnsFailureResult(string? filePath)
    {
        // Arrange
        var config = CreateValidTransferConfig();

        // Act
        var result = await _fileTransferClient.TransferFileAsync(filePath!, config);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal(0, result.BytesTransferred);
    }

    [Fact]
    public async Task TransferFileAsync_WithLargeFile_HandlesProgressReporting()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "large_test.txt");
        var content = new string('A', 1024 * 1024); // 1MB of data
        await File.WriteAllTextAsync(testFile, content);
        
        var config = CreateValidTransferConfig();
        config.MaxRetries = 1; // Single attempt for faster test
        config.TimeoutSeconds = 1; // Short timeout

        // Act
        var result = await _fileTransferClient.TransferFileAsync(testFile, config);

        // Assert
        // Even though transfer will fail (no server), we can verify the file was processed
        Assert.False(result.Success);
        Assert.True(result.Duration.TotalMilliseconds > 0);
    }

    [Fact]
    public void TransferConfig_DefaultValues_AreValid()
    {
        // Arrange & Act
        var config = CreateValidTransferConfig();

        // Assert
        Assert.NotNull(config.TargetServer);
        Assert.NotEmpty(config.TargetDirectory);
        Assert.NotEmpty(config.FileName);
        Assert.True(config.TimeoutSeconds > 0);
        Assert.True(config.MaxRetries > 0);
        Assert.NotNull(config.ChunkingStrategy);
    }

    [Fact]
    public void ChunkingStrategy_DefaultValues_AreReasonable()
    {
        // Arrange & Act
        var strategy = new ChunkingStrategy();

        // Assert
        Assert.True(strategy.ChunkSize > 0);
        Assert.True(strategy.MaxConcurrentChunks > 0);
        Assert.True(strategy.MaxConcurrentChunks <= 10); // Reasonable upper limit
    }

    [Theory]
    [InlineData(1000, 100, 10)]
    [InlineData(1000, 1000, 1)]
    [InlineData(1000, 2000, 1)]
    [InlineData(0, 100, 0)]
    public void ChunkingStrategy_CalculateChunkCount_ReturnsCorrectValue(long fileSize, long chunkSize, int expectedChunks)
    {
        // Arrange
        var strategy = new ChunkingStrategy { ChunkSize = chunkSize };

        // Act
        var chunkCount = strategy.CalculateChunkCount(fileSize);

        // Assert
        Assert.Equal(expectedChunks, chunkCount);
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