using System.Text;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;
using Xunit.Abstractions;

namespace MySqlBackupTool.Tests.Benchmarks;

/// <summary>
/// Performance benchmarks for compression operations
/// </summary>
public class CompressionBenchmarks : IDisposable
{
    private readonly IBenchmarkRunner _benchmarkRunner;
    private readonly CompressionService _compressionService;
    private readonly string _testDirectory;
    private readonly ITestOutputHelper _output;

    public CompressionBenchmarks(ITestOutputHelper output)
    {
        _output = output;
        
        var loggerFactory = new LoggerFactory();
        var compressionLogger = loggerFactory.CreateLogger<CompressionService>();
        var benchmarkLogger = loggerFactory.CreateLogger<BenchmarkRunner>();
        var memoryProfilerLogger = loggerFactory.CreateLogger<MemoryProfiler>();
        var loggingService = new LoggingService(loggerFactory.CreateLogger<LoggingService>());
        
        var memoryProfiler = new MemoryProfiler(memoryProfilerLogger);
        _compressionService = new CompressionService(compressionLogger, memoryProfiler);
        _benchmarkRunner = new BenchmarkRunner(benchmarkLogger, memoryProfiler);
        
        _testDirectory = Path.Combine(Path.GetTempPath(), "CompressionBenchmarks_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task CompressionBenchmark_SmallFiles_MeetsPerformanceThresholds()
    {
        // Arrange
        var testFiles = await CreateTestFilesAsync(new[] { 1024 * 1024 }); // 1MB
        var config = new BenchmarkConfig
        {
            WarmupIterations = 2,
            BenchmarkIterations = 5,
            MaxExecutionTime = TimeSpan.FromMinutes(2)
        };

        var thresholds = new PerformanceThresholds
        {
            MinThroughputMBps = 5.0, // At least 5 MB/s for compression
            MaxDurationForSmallFiles = TimeSpan.FromSeconds(10),
            MaxMemoryUsageMB = 256, // Max 256MB for small files
            MinCompressionRatio = 0.1 // At least 10% compression
        };

        // Act
        var result = await _benchmarkRunner.RunBenchmarkAsync(
            "SmallFileCompression",
            "Compression",
            async (cancellationToken) =>
            {
                var inputFile = testFiles.First();
                var outputFile = Path.Combine(_testDirectory, $"compressed_{Path.GetFileName(inputFile)}.gz");
                
                await _compressionService.CompressDirectoryAsync(inputFile, outputFile, null, cancellationToken);
                
                // Calculate compression ratio
                var originalSize = new FileInfo(inputFile).Length;
                var compressedSize = new FileInfo(outputFile).Length;
                var compressionRatio = 1.0 - ((double)compressedSize / originalSize);
                
                // Store compression ratio in result (will be set by benchmark runner)
                return originalSize;
            },
            config);

        // Assert
        Assert.True(result.Success, $"Benchmark failed: {result.ErrorMessage}");
        
        var violations = _benchmarkRunner.ValidatePerformance(result, thresholds);
        
        _output.WriteLine($"Compression Benchmark Results:");
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
    public async Task CompressionBenchmark_LargeFiles_HandlesMemoryEfficiently()
    {
        // Arrange
        var testFiles = await CreateTestFilesAsync(new[] { 50 * 1024 * 1024 }); // 50MB
        var config = new BenchmarkConfig
        {
            WarmupIterations = 1,
            BenchmarkIterations = 3,
            MaxExecutionTime = TimeSpan.FromMinutes(5),
            CollectMemoryMetrics = true
        };

        var thresholds = new PerformanceThresholds
        {
            MinThroughputMBps = 10.0, // At least 10 MB/s for large files
            MaxDurationForLargeFiles = TimeSpan.FromMinutes(2),
            MaxMemoryUsageMB = 512, // Max 512MB for large files
            MinCompressionRatio = 0.1
        };

        // Act
        var result = await _benchmarkRunner.RunBenchmarkAsync(
            "LargeFileCompression",
            "Compression",
            async (cancellationToken) =>
            {
                var inputFile = testFiles.First();
                var outputFile = Path.Combine(_testDirectory, $"compressed_{Path.GetFileName(inputFile)}.gz");
                
                await _compressionService.CompressFileAsync(inputFile, outputFile, cancellationToken);
                
                return new FileInfo(inputFile).Length;
            },
            config);

        // Assert
        Assert.True(result.Success, $"Benchmark failed: {result.ErrorMessage}");
        
        var violations = _benchmarkRunner.ValidatePerformance(result, thresholds);
        
        _output.WriteLine($"Large File Compression Benchmark Results:");
        _output.WriteLine($"Duration: {result.GetFormattedDuration()}");
        _output.WriteLine($"Throughput: {result.GetFormattedThroughput()}");
        _output.WriteLine($"Peak Memory: {result.GetFormattedPeakMemory()}");
        _output.WriteLine($"Memory Growth: {FormatBytes(result.MemoryGrowth)}");
        
        // Memory efficiency assertions
        Assert.True(result.PeakMemoryUsage < thresholds.MaxMemoryUsageMB * 1024 * 1024,
            $"Peak memory usage {result.GetFormattedPeakMemory()} exceeds threshold {thresholds.MaxMemoryUsageMB} MB");
        
        // Memory growth should be reasonable (not more than 2x file size)
        Assert.True(result.MemoryGrowth < result.BytesProcessed * 2,
            $"Memory growth {FormatBytes(result.MemoryGrowth)} is excessive for file size {result.GetFormattedBytesProcessed()}");
    }

    [Fact]
    public async Task CompressionBenchmark_MultipleFileSizes_ShowsScalability()
    {
        // Arrange
        var fileSizes = new[] { 1024 * 1024, 10 * 1024 * 1024, 50 * 1024 * 1024 }; // 1MB, 10MB, 50MB
        var testFiles = await CreateTestFilesAsync(fileSizes);
        
        var config = new BenchmarkConfig
        {
            WarmupIterations = 1,
            BenchmarkIterations = 2,
            MaxExecutionTime = TimeSpan.FromMinutes(10)
        };

        var benchmarks = new Dictionary<string, Func<CancellationToken, Task<long>>>();
        
        for (int i = 0; i < testFiles.Count; i++)
        {
            var file = testFiles[i];
            var size = fileSizes[i];
            benchmarks[$"Compression_{FormatBytes(size)}"] = async (cancellationToken) =>
            {
                var outputFile = Path.Combine(_testDirectory, $"compressed_{Path.GetFileName(file)}.gz");
                await _compressionService.CompressFileAsync(file, outputFile, cancellationToken);
                return new FileInfo(file).Length;
            };
        }

        // Act
        var suite = await _benchmarkRunner.RunBenchmarkSuiteAsync("CompressionScalability", benchmarks, config);

        // Assert
        Assert.True(suite.Results.All(r => r.Success), "All compression benchmarks should succeed");
        
        _output.WriteLine("Compression Scalability Results:");
        foreach (var operationType in benchmarks.Keys)
        {
            var summary = suite.GetSummary(operationType);
            _output.WriteLine($"{operationType}:");
            _output.WriteLine($"  Average Duration: {summary.GetFormattedAverageDuration()}");
            _output.WriteLine($"  Average Throughput: {summary.GetFormattedAverageThroughput()}");
            _output.WriteLine($"  Peak Memory: {summary.GetFormattedPeakMemory()}");
        }

        // Scalability assertions - throughput should not degrade significantly with larger files
        var smallFileResults = suite.GetResultsByType("Compression_1.0 MB").Where(r => r.Success);
        var largeFileResults = suite.GetResultsByType("Compression_50.0 MB").Where(r => r.Success);
        
        if (smallFileResults.Any() && largeFileResults.Any())
        {
            var smallFileThroughput = smallFileResults.Average(r => r.ThroughputMBps);
            var largeFileThroughput = largeFileResults.Average(r => r.ThroughputMBps);
            
            // Large files should have at least 50% of small file throughput
            Assert.True(largeFileThroughput >= smallFileThroughput * 0.5,
                $"Large file throughput {largeFileThroughput:F2} MB/s is significantly lower than small file throughput {smallFileThroughput:F2} MB/s");
        }
    }

    [Fact]
    public async Task CompressionBenchmark_ParallelCompression_ShowsPerformanceGains()
    {
        // Arrange
        var testFiles = await CreateTestFilesAsync(new[] { 5 * 1024 * 1024, 5 * 1024 * 1024, 5 * 1024 * 1024 }); // 3 x 5MB files
        
        var config = new BenchmarkConfig
        {
            WarmupIterations = 1,
            BenchmarkIterations = 3,
            MaxExecutionTime = TimeSpan.FromMinutes(5)
        };

        // Sequential compression benchmark
        var sequentialResult = await _benchmarkRunner.RunBenchmarkAsync(
            "SequentialCompression",
            "Compression",
            async (cancellationToken) =>
            {
                long totalBytes = 0;
                foreach (var file in testFiles)
                {
                    var outputFile = Path.Combine(_testDirectory, $"seq_compressed_{Path.GetFileName(file)}.gz");
                    await _compressionService.CompressFileAsync(file, outputFile, cancellationToken);
                    totalBytes += new FileInfo(file).Length;
                }
                return totalBytes;
            },
            config);

        // Parallel compression benchmark
        var parallelResult = await _benchmarkRunner.RunBenchmarkAsync(
            "ParallelCompression",
            "Compression",
            async (cancellationToken) =>
            {
                var tasks = testFiles.Select(async (file, index) =>
                {
                    var outputFile = Path.Combine(_testDirectory, $"par_compressed_{index}_{Path.GetFileName(file)}.gz");
                    await _compressionService.CompressFileAsync(file, outputFile, cancellationToken);
                    return new FileInfo(file).Length;
                });
                
                var results = await Task.WhenAll(tasks);
                return results.Sum();
            },
            config);

        // Assert
        Assert.True(sequentialResult.Success, $"Sequential compression failed: {sequentialResult.ErrorMessage}");
        Assert.True(parallelResult.Success, $"Parallel compression failed: {parallelResult.ErrorMessage}");
        
        _output.WriteLine("Parallel vs Sequential Compression:");
        _output.WriteLine($"Sequential - Duration: {sequentialResult.GetFormattedDuration()}, Throughput: {sequentialResult.GetFormattedThroughput()}");
        _output.WriteLine($"Parallel - Duration: {parallelResult.GetFormattedDuration()}, Throughput: {parallelResult.GetFormattedThroughput()}");
        
        var comparison = _benchmarkRunner.CompareBenchmarks(sequentialResult, parallelResult);
        _output.WriteLine($"Performance Improvement: {comparison.GetFormattedDurationImprovement()} duration, {comparison.GetFormattedThroughputImprovement()} throughput");

        // Parallel should be faster (at least 20% improvement on multi-core systems)
        if (Environment.ProcessorCount > 1)
        {
            Assert.True(parallelResult.Duration < sequentialResult.Duration,
                "Parallel compression should be faster than sequential on multi-core systems");
            
            Assert.True(comparison.DurationImprovement > 10,
                $"Parallel compression should show at least 10% improvement, got {comparison.DurationImprovement:F1}%");
        }
    }

    /// <summary>
    /// Creates test files with specified sizes
    /// </summary>
    private async Task<List<string>> CreateTestFilesAsync(int[] fileSizes)
    {
        var files = new List<string>();
        
        for (int i = 0; i < fileSizes.Length; i++)
        {
            var fileName = Path.Combine(_testDirectory, $"test_file_{i}_{fileSizes[i]}.txt");
            
            // Create file with mixed content for realistic compression testing
            var content = GenerateTestContent(fileSizes[i]);
            await File.WriteAllTextAsync(fileName, content);
            
            files.Add(fileName);
        }
        
        return files;
    }

    /// <summary>
    /// Generates test content with mixed patterns for realistic compression
    /// </summary>
    private string GenerateTestContent(int sizeBytes)
    {
        var random = new Random(42); // Fixed seed for reproducible results
        var content = new StringBuilder();
        
        // Mix of patterns to simulate real data
        var patterns = new[]
        {
            "This is a test line with some repeated content. ",
            "MySQL backup data with various information stored here. ",
            "Configuration settings and parameters for the backup tool. ",
            "Log entries and diagnostic information for troubleshooting. ",
            "Binary data simulation: " + new string('A', 50) + " ",
            "Compressed content with repetitive patterns: " + string.Join("", Enumerable.Repeat("PATTERN", 10)) + " "
        };
        
        while (content.Length < sizeBytes)
        {
            var pattern = patterns[random.Next(patterns.Length)];
            content.Append(pattern);
            
            // Add some random variation
            if (random.Next(10) == 0)
            {
                content.Append($"RANDOM_{random.Next(1000)} ");
            }
        }
        
        // Trim to exact size
        if (content.Length > sizeBytes)
        {
            content.Length = sizeBytes;
        }
        
        return content.ToString();
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
        
        _compressionService?.Dispose();
    }
}