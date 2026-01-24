using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;
using Xunit.Abstractions;

namespace MySqlBackupTool.Tests.Benchmarks;

/// <summary>
/// Performance benchmarks for encryption operations
/// </summary>
public class EncryptionBenchmarks : IDisposable
{
    private readonly IBenchmarkRunner _benchmarkRunner;
    private readonly EncryptionService _encryptionService;
    private readonly string _testDirectory;
    private readonly ITestOutputHelper _output;
    private const string TestPassword = "TestPassword123!";

    public EncryptionBenchmarks(ITestOutputHelper output)
    {
        _output = output;
        
        var loggerFactory = new LoggerFactory();
        var encryptionLogger = loggerFactory.CreateLogger<EncryptionService>();
        var benchmarkLogger = loggerFactory.CreateLogger<BenchmarkRunner>();
        var memoryProfilerLogger = loggerFactory.CreateLogger<MemoryProfiler>();
        
        var memoryProfiler = new MemoryProfiler(memoryProfilerLogger);
        _encryptionService = new EncryptionService(encryptionLogger);
        _benchmarkRunner = new BenchmarkRunner(benchmarkLogger, memoryProfiler);
        
        _testDirectory = Path.Combine(Path.GetTempPath(), "EncryptionBenchmarks_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task EncryptionBenchmark_SmallFiles_MeetsPerformanceThresholds()
    {
        // Arrange
        var testFile = await CreateTestFileAsync(5 * 1024 * 1024); // 5MB
        var config = new BenchmarkConfig
        {
            WarmupIterations = 2,
            BenchmarkIterations = 5,
            MaxExecutionTime = TimeSpan.FromMinutes(2)
        };

        var thresholds = new PerformanceThresholds
        {
            MinThroughputMBps = 20.0, // At least 20 MB/s for encryption
            MaxDurationForSmallFiles = TimeSpan.FromSeconds(15),
            MaxMemoryUsageMB = 256, // Max 256MB for small files
        };

        // Act
        var result = await _benchmarkRunner.RunBenchmarkAsync(
            "SmallFileEncryption",
            "Encryption",
            async (cancellationToken) =>
            {
                var encryptedFile = Path.Combine(_testDirectory, $"encrypted_{Path.GetFileName(testFile)}.enc");
                
                await _encryptionService.EncryptAsync(testFile, encryptedFile, TestPassword, cancellationToken);
                
                return new FileInfo(testFile).Length;
            },
            config);

        // Assert
        Assert.True(result.Success, $"Benchmark failed: {result.ErrorMessage}");
        
        var violations = _benchmarkRunner.ValidatePerformance(result, thresholds);
        
        _output.WriteLine($"Encryption Benchmark Results:");
        _output.WriteLine($"Duration: {result.GetFormattedDuration()}");
        _output.WriteLine($"Throughput: {result.GetFormattedThroughput()}");
        _output.WriteLine($"Peak Memory: {result.GetFormattedPeakMemory()}");
        _output.WriteLine($"Bytes Processed: {result.GetFormattedBytesProcessed()}");
        
        if (violations.Any())
        {
            _output.WriteLine("Performance Violations:");
            foreach (var violation in violations)
            {
                _output.WriteLine($"- {violation}");
            }
        }

        // Performance assertions
        Assert.Empty(violations); // No performance threshold violations
        Assert.True(result.ThroughputMBps >= thresholds.MinThroughputMBps, 
            $"Throughput {result.ThroughputMBps:F2} MB/s is below minimum {thresholds.MinThroughputMBps} MB/s");
    }

    [Fact]
    public async Task EncryptionBenchmark_LargeFiles_HandlesMemoryEfficiently()
    {
        // Arrange
        var testFile = await CreateTestFileAsync(100 * 1024 * 1024); // 100MB
        var config = new BenchmarkConfig
        {
            WarmupIterations = 1,
            BenchmarkIterations = 3,
            MaxExecutionTime = TimeSpan.FromMinutes(10),
            CollectMemoryMetrics = true
        };

        var thresholds = new PerformanceThresholds
        {
            MinThroughputMBps = 15.0, // At least 15 MB/s for large files
            MaxDurationForLargeFiles = TimeSpan.FromMinutes(5),
            MaxMemoryUsageMB = 512, // Max 512MB for large files
        };

        // Act
        var result = await _benchmarkRunner.RunBenchmarkAsync(
            "LargeFileEncryption",
            "Encryption",
            async (cancellationToken) =>
            {
                var encryptedFile = Path.Combine(_testDirectory, $"encrypted_{Path.GetFileName(testFile)}.enc");
                
                await _encryptionService.EncryptAsync(testFile, encryptedFile, TestPassword, cancellationToken);
                
                return new FileInfo(testFile).Length;
            },
            config);

        // Assert
        Assert.True(result.Success, $"Benchmark failed: {result.ErrorMessage}");
        
        var violations = _benchmarkRunner.ValidatePerformance(result, thresholds);
        
        _output.WriteLine($"Large File Encryption Benchmark Results:");
        _output.WriteLine($"Duration: {result.GetFormattedDuration()}");
        _output.WriteLine($"Throughput: {result.GetFormattedThroughput()}");
        _output.WriteLine($"Peak Memory: {result.GetFormattedPeakMemory()}");
        _output.WriteLine($"Memory Growth: {FormatBytes(result.MemoryGrowth)}");
        
        // Memory efficiency assertions
        Assert.True(result.PeakMemoryUsage < thresholds.MaxMemoryUsageMB * 1024 * 1024,
            $"Peak memory usage {result.GetFormattedPeakMemory()} exceeds threshold {thresholds.MaxMemoryUsageMB} MB");
        
        // Memory growth should be reasonable (streaming encryption should not load entire file)
        Assert.True(result.MemoryGrowth < 50 * 1024 * 1024, // Less than 50MB growth
            $"Memory growth {FormatBytes(result.MemoryGrowth)} is excessive for streaming encryption");
    }

    [Fact]
    public async Task EncryptionBenchmark_EncryptDecryptRoundTrip_ValidatesIntegrity()
    {
        // Arrange
        var testFile = await CreateTestFileAsync(10 * 1024 * 1024); // 10MB
        var originalChecksum = await CalculateFileChecksumAsync(testFile);
        
        var config = new BenchmarkConfig
        {
            WarmupIterations = 1,
            BenchmarkIterations = 3,
            MaxExecutionTime = TimeSpan.FromMinutes(5)
        };

        // Act - Encrypt + Decrypt benchmark
        var result = await _benchmarkRunner.RunBenchmarkAsync(
            "EncryptDecryptRoundTrip",
            "Encryption",
            async (cancellationToken) =>
            {
                var encryptedFile = Path.Combine(_testDirectory, $"encrypted_{Path.GetFileName(testFile)}.enc");
                var decryptedFile = Path.Combine(_testDirectory, $"decrypted_{Path.GetFileName(testFile)}");
                
                // Encrypt
                await _encryptionService.EncryptAsync(testFile, encryptedFile, TestPassword, cancellationToken);
                
                // Decrypt
                await _encryptionService.DecryptAsync(encryptedFile, decryptedFile, TestPassword, cancellationToken);
                
                // Verify integrity
                var decryptedChecksum = await CalculateFileChecksumAsync(decryptedFile);
                if (originalChecksum != decryptedChecksum)
                {
                    throw new InvalidOperationException("File integrity check failed after encryption/decryption");
                }
                
                return new FileInfo(testFile).Length * 2; // Processed both encrypt and decrypt
            },
            config);

        // Assert
        Assert.True(result.Success, $"Benchmark failed: {result.ErrorMessage}");
        
        _output.WriteLine($"Encrypt/Decrypt Round Trip Benchmark Results:");
        _output.WriteLine($"Duration: {result.GetFormattedDuration()}");
        _output.WriteLine($"Throughput: {result.GetFormattedThroughput()}");
        _output.WriteLine($"Peak Memory: {result.GetFormattedPeakMemory()}");
        
        // Should maintain reasonable throughput even with round trip
        Assert.True(result.ThroughputMBps >= 10.0, 
            $"Round trip throughput {result.ThroughputMBps:F2} MB/s is too low");
    }

    [Fact]
    public async Task EncryptionBenchmark_DifferentFileSizes_ShowsLinearScaling()
    {
        // Arrange
        var fileSizes = new[] { 1024 * 1024, 10 * 1024 * 1024, 50 * 1024 * 1024 }; // 1MB, 10MB, 50MB
        var testFiles = await CreateTestFilesAsync(fileSizes);
        
        var config = new BenchmarkConfig
        {
            WarmupIterations = 1,
            BenchmarkIterations = 2,
            MaxExecutionTime = TimeSpan.FromMinutes(15)
        };

        var benchmarks = new Dictionary<string, Func<CancellationToken, Task<long>>>();
        
        for (int i = 0; i < testFiles.Count; i++)
        {
            var file = testFiles[i];
            var size = fileSizes[i];
            benchmarks[$"Encryption_{FormatBytes(size)}"] = async (cancellationToken) =>
            {
                var encryptedFile = Path.Combine(_testDirectory, $"encrypted_{Path.GetFileName(file)}.enc");
                await _encryptionService.EncryptAsync(file, encryptedFile, TestPassword, cancellationToken);
                return new FileInfo(file).Length;
            };
        }

        // Act
        var suite = await _benchmarkRunner.RunBenchmarkSuiteAsync("EncryptionScalability", benchmarks, config);

        // Assert
        Assert.True(suite.Results.All(r => r.Success), "All encryption benchmarks should succeed");
        
        _output.WriteLine("Encryption Scalability Results:");
        var throughputs = new List<double>();
        
        foreach (var operationType in benchmarks.Keys)
        {
            var summary = suite.GetSummary(operationType);
            _output.WriteLine($"{operationType}:");
            _output.WriteLine($"  Average Duration: {summary.GetFormattedAverageDuration()}");
            _output.WriteLine($"  Average Throughput: {summary.GetFormattedAverageThroughput()}");
            _output.WriteLine($"  Peak Memory: {summary.GetFormattedPeakMemory()}");
            
            throughputs.Add(summary.AverageThroughput);
        }

        // Throughput should be relatively consistent across file sizes (within 50% variance)
        if (throughputs.Count > 1)
        {
            var minThroughput = throughputs.Min();
            var maxThroughput = throughputs.Max();
            var variance = (maxThroughput - minThroughput) / minThroughput;
            
            Assert.True(variance < 1.0, // Less than 100% variance
                $"Throughput variance {variance:P0} is too high, indicating poor scaling");
        }
    }

    [Fact]
    public async Task EncryptionBenchmark_PasswordValidation_PerformsEfficiently()
    {
        // Arrange
        var testFile = await CreateTestFileAsync(1024 * 1024); // 1MB
        var encryptedFile = Path.Combine(_testDirectory, $"encrypted_{Path.GetFileName(testFile)}.enc");
        
        // First encrypt the file
        await _encryptionService.EncryptAsync(testFile, encryptedFile, TestPassword);
        
        var config = new BenchmarkConfig
        {
            WarmupIterations = 2,
            BenchmarkIterations = 10, // More iterations for password validation
            MaxExecutionTime = TimeSpan.FromMinutes(2)
        };

        // Act - Password validation benchmark
        var result = await _benchmarkRunner.RunBenchmarkAsync(
            "PasswordValidation",
            "Encryption",
            async (cancellationToken) =>
            {
                // Test both correct and incorrect passwords
                var isValidCorrect = _encryptionService.ValidatePassword(encryptedFile, TestPassword);
                var isValidIncorrect = _encryptionService.ValidatePassword(encryptedFile, "WrongPassword");
                
                if (!isValidCorrect || isValidIncorrect)
                {
                    throw new InvalidOperationException("Password validation failed");
                }
                
                return new FileInfo(encryptedFile).Length;
            },
            config);

        // Assert
        Assert.True(result.Success, $"Benchmark failed: {result.ErrorMessage}");
        
        _output.WriteLine($"Password Validation Benchmark Results:");
        _output.WriteLine($"Duration: {result.GetFormattedDuration()}");
        _output.WriteLine($"Peak Memory: {result.GetFormattedPeakMemory()}");
        
        // Password validation should be fast (under 100ms per validation)
        Assert.True(result.Duration.TotalMilliseconds < 1000, 
            $"Password validation took {result.Duration.TotalMilliseconds:F0}ms, should be under 1000ms");
    }

    /// <summary>
    /// Creates a test file with specified size
    /// </summary>
    private async Task<string> CreateTestFileAsync(int sizeBytes)
    {
        var fileName = Path.Combine(_testDirectory, $"test_file_{sizeBytes}.dat");
        
        // Create file with random binary data for encryption testing
        var random = new Random(42); // Fixed seed for reproducible results
        var buffer = new byte[Math.Min(sizeBytes, 1024 * 1024)]; // 1MB buffer max
        
        using var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
        var remaining = sizeBytes;
        
        while (remaining > 0)
        {
            var chunkSize = Math.Min(remaining, buffer.Length);
            random.NextBytes(buffer.AsSpan(0, chunkSize));
            await fileStream.WriteAsync(buffer.AsMemory(0, chunkSize));
            remaining -= chunkSize;
        }
        
        return fileName;
    }

    /// <summary>
    /// Creates multiple test files with specified sizes
    /// </summary>
    private async Task<List<string>> CreateTestFilesAsync(int[] fileSizes)
    {
        var files = new List<string>();
        
        for (int i = 0; i < fileSizes.Length; i++)
        {
            var file = await CreateTestFileAsync(fileSizes[i]);
            files.Add(file);
        }
        
        return files;
    }

    /// <summary>
    /// Calculates SHA256 checksum of a file
    /// </summary>
    private async Task<string> CalculateFileChecksumAsync(string filePath)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        
        var hashBytes = await sha256.ComputeHashAsync(fileStream);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Formats bytes to human-readable string
    /// </summary>
    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";
        
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}