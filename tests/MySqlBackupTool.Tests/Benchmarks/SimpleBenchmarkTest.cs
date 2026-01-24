using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;
using Xunit.Abstractions;

namespace MySqlBackupTool.Tests.Benchmarks;

/// <summary>
/// Simple benchmark test to verify the benchmarking infrastructure works
/// </summary>
public class SimpleBenchmarkTest : IDisposable
{
    private readonly IBenchmarkRunner _benchmarkRunner;
    private readonly string _testDirectory;
    private readonly ITestOutputHelper _output;

    public SimpleBenchmarkTest(ITestOutputHelper output)
    {
        _output = output;
        
        var loggerFactory = new LoggerFactory();
        var benchmarkLogger = loggerFactory.CreateLogger<BenchmarkRunner>();
        var memoryProfilerLogger = loggerFactory.CreateLogger<MemoryProfiler>();
        
        var memoryProfiler = new MemoryProfiler(memoryProfilerLogger);
        _benchmarkRunner = new BenchmarkRunner(benchmarkLogger, memoryProfiler);
        
        _testDirectory = Path.Combine(Path.GetTempPath(), "SimpleBenchmark_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task SimpleBenchmark_FileOperations_MeasuresPerformance()
    {
        // Arrange
        var config = new BenchmarkConfig
        {
            WarmupIterations = 1,
            BenchmarkIterations = 3,
            MaxExecutionTime = TimeSpan.FromMinutes(1)
        };

        // Act - Simple file I/O benchmark
        var result = await _benchmarkRunner.RunBenchmarkAsync(
            "SimpleFileIO",
            "FileOperations",
            async (cancellationToken) =>
            {
                var testFile = Path.Combine(_testDirectory, "test_file.txt");
                var content = new string('A', 1024 * 1024); // 1MB of data
                
                // Write file
                await File.WriteAllTextAsync(testFile, content, cancellationToken);
                
                // Read file
                var readContent = await File.ReadAllTextAsync(testFile, cancellationToken);
                
                // Verify content
                if (readContent.Length != content.Length)
                {
                    throw new InvalidOperationException("File content mismatch");
                }
                
                return content.Length;
            },
            config);

        // Assert
        Assert.True(result.Success, $"Benchmark failed: {result.ErrorMessage}");
        Assert.True(result.BytesProcessed > 0, "Should have processed some bytes");
        Assert.True(result.Duration.TotalMilliseconds > 0, "Should have measurable duration");
        
        _output.WriteLine($"Simple File I/O Benchmark Results:");
        _output.WriteLine($"Duration: {result.GetFormattedDuration()}");
        _output.WriteLine($"Throughput: {result.GetFormattedThroughput()}");
        _output.WriteLine($"Peak Memory: {result.GetFormattedPeakMemory()}");
        _output.WriteLine($"Bytes Processed: {result.GetFormattedBytesProcessed()}");
        
        // Basic performance assertions
        Assert.True(result.ThroughputMBps > 1.0, "Should achieve at least 1 MB/s throughput");
        Assert.True(result.Duration.TotalSeconds < 30, "Should complete within 30 seconds");
    }

    [Fact]
    public async Task SimpleBenchmark_MemoryAllocation_TracksMemoryUsage()
    {
        // Arrange
        var config = new BenchmarkConfig
        {
            WarmupIterations = 1,
            BenchmarkIterations = 2,
            MaxExecutionTime = TimeSpan.FromMinutes(1),
            CollectMemoryMetrics = true
        };

        // Act - Memory allocation benchmark
        var result = await _benchmarkRunner.RunBenchmarkAsync(
            "MemoryAllocation",
            "Memory",
            async (cancellationToken) =>
            {
                var allocations = new List<byte[]>();
                long totalBytes = 0;
                
                // Allocate memory in chunks
                for (int i = 0; i < 10; i++)
                {
                    var chunk = new byte[1024 * 1024]; // 1MB chunks
                    Array.Fill(chunk, (byte)(i % 256));
                    allocations.Add(chunk);
                    totalBytes += chunk.Length;
                    
                    // Small delay to allow memory tracking
                    await Task.Delay(10, cancellationToken);
                }
                
                // Process the allocated memory
                long checksum = 0;
                foreach (var chunk in allocations)
                {
                    checksum += chunk.Sum(b => (long)b);
                }
                
                // Verify we did some work
                if (checksum == 0)
                {
                    throw new InvalidOperationException("No work was performed");
                }
                
                return totalBytes;
            },
            config);

        // Assert
        Assert.True(result.Success, $"Benchmark failed: {result.ErrorMessage}");
        Assert.True(result.PeakMemoryUsage > 0, "Should track peak memory usage");
        Assert.True(result.BytesProcessed > 0, "Should have processed some bytes");
        
        _output.WriteLine($"Memory Allocation Benchmark Results:");
        _output.WriteLine($"Duration: {result.GetFormattedDuration()}");
        _output.WriteLine($"Peak Memory: {result.GetFormattedPeakMemory()}");
        _output.WriteLine($"Memory Growth: {FormatBytes(result.MemoryGrowth)}");
        _output.WriteLine($"Bytes Processed: {result.GetFormattedBytesProcessed()}");
        
        // Memory should be tracked
        Assert.True(result.PeakMemoryUsage >= result.BytesProcessed / 2, 
            "Peak memory should be at least half the allocated data size");
    }

    [Fact]
    public async Task SimpleBenchmark_BenchmarkSuite_RunsMultipleTests()
    {
        // Arrange
        var config = new BenchmarkConfig
        {
            WarmupIterations = 1,
            BenchmarkIterations = 2,
            MaxExecutionTime = TimeSpan.FromMinutes(2)
        };

        var benchmarks = new Dictionary<string, Func<CancellationToken, Task<long>>>
        {
            ["FastOperation"] = async (cancellationToken) =>
            {
                await Task.Delay(10, cancellationToken); // 10ms operation
                return 1024; // 1KB of "work"
            },
            
            ["SlowOperation"] = async (cancellationToken) =>
            {
                await Task.Delay(100, cancellationToken); // 100ms operation
                return 10240; // 10KB of "work"
            }
        };

        // Act
        var suite = await _benchmarkRunner.RunBenchmarkSuiteAsync("SimpleSuite", benchmarks, config);

        // Assert
        Assert.True(suite.Results.Count > 0, "Suite should produce results");
        Assert.True(suite.Results.All(r => r.Success), "All benchmarks should succeed");
        
        var fastResults = suite.GetResultsByType("FastOperation");
        var slowResults = suite.GetResultsByType("SlowOperation");
        
        Assert.True(fastResults.Any(), "Should have fast operation results");
        Assert.True(slowResults.Any(), "Should have slow operation results");
        
        _output.WriteLine($"Benchmark Suite Results:");
        _output.WriteLine($"Total Tests: {suite.Results.Count}");
        _output.WriteLine($"Total Duration: {suite.TotalExecutionTime}");
        
        foreach (var operationType in benchmarks.Keys)
        {
            var summary = suite.GetSummary(operationType);
            _output.WriteLine($"{operationType}: {summary.GetFormattedAverageDuration()}, {summary.GetFormattedAverageThroughput()}");
        }
        
        // Performance comparison
        var avgFastDuration = fastResults.Average(r => r.Duration.TotalMilliseconds);
        var avgSlowDuration = slowResults.Average(r => r.Duration.TotalMilliseconds);
        
        Assert.True(avgSlowDuration > avgFastDuration, 
            "Slow operation should take longer than fast operation");
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
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }
}