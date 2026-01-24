using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;
using Xunit.Abstractions;

namespace MySqlBackupTool.Tests.Benchmarks;

/// <summary>
/// Performance benchmarks for memory usage patterns
/// </summary>
public class MemoryUsageBenchmarks : IDisposable
{
    private readonly IBenchmarkRunner _benchmarkRunner;
    private readonly IMemoryProfiler _memoryProfiler;
    private readonly CompressionService _compressionService;
    private readonly EncryptionService _encryptionService;
    private readonly string _testDirectory;
    private readonly ITestOutputHelper _output;

    public MemoryUsageBenchmarks(ITestOutputHelper output)
    {
        _output = output;
        
        var loggerFactory = new LoggerFactory();
        var benchmarkLogger = loggerFactory.CreateLogger<BenchmarkRunner>();
        var memoryProfilerLogger = loggerFactory.CreateLogger<MemoryProfiler>();
        var compressionLogger = loggerFactory.CreateLogger<CompressionService>();
        var encryptionLogger = loggerFactory.CreateLogger<EncryptionService>();
        
        _memoryProfiler = new MemoryProfiler(memoryProfilerLogger);
        _compressionService = new CompressionService(compressionLogger, _memoryProfiler);
        _encryptionService = new EncryptionService(encryptionLogger);
        _benchmarkRunner = new BenchmarkRunner(benchmarkLogger, _memoryProfiler);
        
        _testDirectory = Path.Combine(Path.GetTempPath(), "MemoryBenchmarks_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task MemoryBenchmark_LargeFileProcessing_StaysWithinLimits()
    {
        // Arrange
        var testFile = await CreateTestFileAsync(200 * 1024 * 1024); // 200MB file
        var config = new BenchmarkConfig
        {
            WarmupIterations = 1,
            BenchmarkIterations = 3,
            MaxExecutionTime = TimeSpan.FromMinutes(10),
            CollectMemoryMetrics = true,
            ForceGarbageCollection = true
        };

        var memoryThresholds = new PerformanceThresholds
        {
            MaxMemoryUsageMB = 512, // Max 512MB for large file processing
        };

        // Act - Large file compression benchmark
        var result = await _benchmarkRunner.RunBenchmarkAsync(
            "LargeFileMemoryUsage",
            "MemoryUsage",
            async (cancellationToken) =>
            {
                var compressedFile = Path.Combine(_testDirectory, $"compressed_{Path.GetFileName(testFile)}.gz");
                
                // Process large file with compression (memory-intensive operation)
                await _compressionService.CompressFileAsync(testFile, compressedFile, cancellationToken);
                
                return new FileInfo(testFile).Length;
            },
            config);

        // Assert
        Assert.True(result.Success, $"Benchmark failed: {result.ErrorMessage}");
        
        var violations = _benchmarkRunner.ValidatePerformance(result, memoryThresholds);
        
        _output.WriteLine($"Large File Memory Usage Benchmark Results:");
        _output.WriteLine($"Duration: {result.GetFormattedDuration()}");
        _output.WriteLine($"Peak Memory: {result.GetFormattedPeakMemory()}");
        _output.WriteLine($"Average Memory: {FormatBytes(result.AverageMemoryUsage)}");
        _output.WriteLine($"Memory Growth: {FormatBytes(result.MemoryGrowth)}");
        _output.WriteLine($"File Size: {result.GetFormattedBytesProcessed()}");
        
        if (violations.Any())
        {
            _output.WriteLine("Memory Violations:");
            foreach (var violation in violations)
            {
                _output.WriteLine($"- {violation}");
            }
        }

        // Memory assertions
        Assert.True(result.PeakMemoryUsage < memoryThresholds.MaxMemoryUsageMB * 1024 * 1024,
            $"Peak memory usage {result.GetFormattedPeakMemory()} exceeds threshold {memoryThresholds.MaxMemoryUsageMB} MB");
        
        // Memory growth should be reasonable (not more than 25% of file size for streaming operations)
        var maxReasonableGrowth = result.BytesProcessed / 4;
        Assert.True(result.MemoryGrowth < maxReasonableGrowth,
            $"Memory growth {FormatBytes(result.MemoryGrowth)} is excessive for streaming operation (max reasonable: {FormatBytes(maxReasonableGrowth)})");
    }

    [Fact]
    public async Task MemoryBenchmark_ConcurrentOperations_ManagesMemoryEfficiently()
    {
        // Arrange
        var testFiles = await CreateTestFilesAsync(new[] { 25 * 1024 * 1024, 25 * 1024 * 1024, 25 * 1024 * 1024 }); // 3 x 25MB files
        
        var config = new BenchmarkConfig
        {
            WarmupIterations = 1,
            BenchmarkIterations = 2,
            MaxExecutionTime = TimeSpan.FromMinutes(10),
            CollectMemoryMetrics = true
        };

        // Sequential processing benchmark
        var sequentialResult = await _benchmarkRunner.RunBenchmarkAsync(
            "SequentialMemoryUsage",
            "MemoryUsage",
            async (cancellationToken) =>
            {
                long totalBytes = 0;
                foreach (var file in testFiles)
                {
                    var compressedFile = Path.Combine(_testDirectory, $"seq_compressed_{Path.GetFileName(file)}.gz");
                    await _compressionService.CompressFileAsync(file, compressedFile, cancellationToken);
                    totalBytes += new FileInfo(file).Length;
                    
                    // Force GC between files to measure individual impact
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
                return totalBytes;
            },
            config);

        // Concurrent processing benchmark
        var concurrentResult = await _benchmarkRunner.RunBenchmarkAsync(
            "ConcurrentMemoryUsage",
            "MemoryUsage",
            async (cancellationToken) =>
            {
                var tasks = testFiles.Select(async (file, index) =>
                {
                    var compressedFile = Path.Combine(_testDirectory, $"con_compressed_{index}_{Path.GetFileName(file)}.gz");
                    await _compressionService.CompressFileAsync(file, compressedFile, cancellationToken);
                    return new FileInfo(file).Length;
                });
                
                var results = await Task.WhenAll(tasks);
                return results.Sum();
            },
            config);

        // Assert
        Assert.True(sequentialResult.Success, $"Sequential processing failed: {sequentialResult.ErrorMessage}");
        Assert.True(concurrentResult.Success, $"Concurrent processing failed: {concurrentResult.ErrorMessage}");
        
        _output.WriteLine("Sequential vs Concurrent Memory Usage:");
        _output.WriteLine($"Sequential - Peak Memory: {sequentialResult.GetFormattedPeakMemory()}, Duration: {sequentialResult.GetFormattedDuration()}");
        _output.WriteLine($"Concurrent - Peak Memory: {concurrentResult.GetFormattedPeakMemory()}, Duration: {concurrentResult.GetFormattedDuration()}");
        
        var memoryRatio = (double)concurrentResult.PeakMemoryUsage / sequentialResult.PeakMemoryUsage;
        _output.WriteLine($"Memory Usage Ratio (Concurrent/Sequential): {memoryRatio:F2}x");

        // Concurrent should use more memory but not excessively (less than 3x)
        Assert.True(memoryRatio < 3.0,
            $"Concurrent memory usage {memoryRatio:F2}x is excessive compared to sequential");
        
        // But concurrent should be faster
        Assert.True(concurrentResult.Duration < sequentialResult.Duration * 1.2, // Allow 20% margin
            "Concurrent processing should not be significantly slower than sequential");
    }

    [Fact]
    public async Task MemoryBenchmark_GarbageCollectionPressure_MaintainsPerformance()
    {
        // Arrange
        var testFile = await CreateTestFileAsync(50 * 1024 * 1024); // 50MB
        
        var config = new BenchmarkConfig
        {
            WarmupIterations = 1,
            BenchmarkIterations = 5,
            MaxExecutionTime = TimeSpan.FromMinutes(5),
            CollectMemoryMetrics = true,
            ForceGarbageCollection = false // Don't force GC to see natural pressure
        };

        // Act - Multiple operations to create GC pressure
        var result = await _benchmarkRunner.RunBenchmarkAsync(
            "GCPressureTest",
            "MemoryUsage",
            async (cancellationToken) =>
            {
                long totalBytes = 0;
                
                // Perform multiple memory-intensive operations
                for (int i = 0; i < 3; i++)
                {
                    var compressedFile = Path.Combine(_testDirectory, $"gc_test_{i}_{Path.GetFileName(testFile)}.gz");
                    var encryptedFile = Path.Combine(_testDirectory, $"gc_test_{i}_{Path.GetFileName(testFile)}.enc");
                    
                    // Compress
                    await _compressionService.CompressFileAsync(testFile, compressedFile, cancellationToken);
                    
                    // Encrypt the compressed file
                    await _encryptionService.EncryptAsync(compressedFile, encryptedFile, "TestPassword123!", cancellationToken);
                    
                    totalBytes += new FileInfo(testFile).Length;
                    
                    // Create some temporary objects to increase GC pressure
                    var tempData = new byte[1024 * 1024]; // 1MB temporary allocation
                    Array.Fill(tempData, (byte)i);
                    tempData = null; // Make eligible for GC
                }
                
                return totalBytes;
            },
            config);

        // Assert
        Assert.True(result.Success, $"Benchmark failed: {result.ErrorMessage}");
        
        _output.WriteLine($"GC Pressure Test Results:");
        _output.WriteLine($"Duration: {result.GetFormattedDuration()}");
        _output.WriteLine($"Peak Memory: {result.GetFormattedPeakMemory()}");
        _output.WriteLine($"Memory Growth: {FormatBytes(result.MemoryGrowth)}");
        
        // Get detailed memory profile if available
        if (result.AdditionalMetrics.TryGetValue("MemoryProfile", out var profileObj) && profileObj is MemoryProfile profile)
        {
            _output.WriteLine($"GC Collections: Gen0={profile.Statistics.Gen0Collections}, Gen1={profile.Statistics.Gen1Collections}, Gen2={profile.Statistics.Gen2Collections}");
            _output.WriteLine($"GC Pressure: {profile.Statistics.GCPressure:F1} collections/min");
            
            // GC pressure should be reasonable (not excessive)
            Assert.True(profile.Statistics.GCPressure < 30, // Less than 30 collections per minute
                $"GC pressure {profile.Statistics.GCPressure:F1} collections/min is excessive");
        }

        // Memory growth should stabilize (not grow indefinitely)
        Assert.True(result.MemoryGrowth < result.BytesProcessed,
            $"Memory growth {FormatBytes(result.MemoryGrowth)} suggests potential memory leak");
    }

    [Fact]
    public async Task MemoryBenchmark_StreamingVsBuffering_ComparesMemoryEfficiency()
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

        // Streaming approach benchmark
        var streamingResult = await _benchmarkRunner.RunBenchmarkAsync(
            "StreamingApproach",
            "MemoryUsage",
            async (cancellationToken) =>
            {
                var outputFile = Path.Combine(_testDirectory, $"streaming_{Path.GetFileName(testFile)}");
                
                // Stream processing with small buffer
                using var input = new FileStream(testFile, FileMode.Open, FileAccess.Read);
                using var output = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
                
                var buffer = new byte[64 * 1024]; // 64KB buffer
                int bytesRead;
                long totalBytes = 0;
                
                while ((bytesRead = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    // Simulate some processing
                    for (int i = 0; i < bytesRead; i++)
                    {
                        buffer[i] = (byte)(buffer[i] ^ 0xFF); // Simple XOR transformation
                    }
                    
                    await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalBytes += bytesRead;
                }
                
                return totalBytes;
            },
            config);

        // Buffering approach benchmark
        var bufferingResult = await _benchmarkRunner.RunBenchmarkAsync(
            "BufferingApproach",
            "MemoryUsage",
            async (cancellationToken) =>
            {
                var outputFile = Path.Combine(_testDirectory, $"buffering_{Path.GetFileName(testFile)}");
                
                // Load entire file into memory for processing
                var fileData = await File.ReadAllBytesAsync(testFile, cancellationToken);
                
                // Process in memory
                for (int i = 0; i < fileData.Length; i++)
                {
                    fileData[i] = (byte)(fileData[i] ^ 0xFF); // Simple XOR transformation
                }
                
                // Write processed data
                await File.WriteAllBytesAsync(outputFile, fileData, cancellationToken);
                
                return fileData.Length;
            },
            config);

        // Assert
        Assert.True(streamingResult.Success, $"Streaming approach failed: {streamingResult.ErrorMessage}");
        Assert.True(bufferingResult.Success, $"Buffering approach failed: {bufferingResult.ErrorMessage}");
        
        _output.WriteLine("Streaming vs Buffering Memory Usage:");
        _output.WriteLine($"Streaming - Peak Memory: {streamingResult.GetFormattedPeakMemory()}, Duration: {streamingResult.GetFormattedDuration()}");
        _output.WriteLine($"Buffering - Peak Memory: {bufferingResult.GetFormattedPeakMemory()}, Duration: {bufferingResult.GetFormattedDuration()}");
        
        var memoryRatio = (double)bufferingResult.PeakMemoryUsage / streamingResult.PeakMemoryUsage;
        var speedRatio = streamingResult.Duration.TotalMilliseconds / bufferingResult.Duration.TotalMilliseconds;
        
        _output.WriteLine($"Memory Usage Ratio (Buffering/Streaming): {memoryRatio:F2}x");
        _output.WriteLine($"Speed Ratio (Streaming/Buffering): {speedRatio:F2}x");

        // Buffering should use significantly more memory
        Assert.True(memoryRatio > 2.0,
            $"Buffering approach should use significantly more memory than streaming, got {memoryRatio:F2}x");
        
        // Streaming should use reasonable memory (less than 10MB for 100MB file)
        Assert.True(streamingResult.PeakMemoryUsage < 10 * 1024 * 1024,
            $"Streaming approach uses too much memory: {streamingResult.GetFormattedPeakMemory()}");
    }

    [Fact]
    public async Task MemoryBenchmark_MemoryLeakDetection_ValidatesStability()
    {
        // Arrange
        var testFile = await CreateTestFileAsync(10 * 1024 * 1024); // 10MB
        
        var config = new BenchmarkConfig
        {
            WarmupIterations = 2,
            BenchmarkIterations = 10, // More iterations to detect leaks
            MaxExecutionTime = TimeSpan.FromMinutes(10),
            CollectMemoryMetrics = true,
            ForceGarbageCollection = true // Force GC to detect leaks
        };

        // Act - Repeated operations to detect memory leaks
        var results = new List<BenchmarkResult>();
        
        for (int iteration = 0; iteration < 5; iteration++)
        {
            var result = await _benchmarkRunner.RunBenchmarkAsync(
                $"MemoryLeakTest_Iteration_{iteration + 1}",
                "MemoryUsage",
                async (cancellationToken) =>
                {
                    var compressedFile = Path.Combine(_testDirectory, $"leak_test_{iteration}_{Path.GetFileName(testFile)}.gz");
                    
                    // Perform operation that might leak memory
                    await _compressionService.CompressFileAsync(testFile, compressedFile, cancellationToken);
                    
                    // Clean up output file to avoid disk space issues
                    if (File.Exists(compressedFile))
                    {
                        File.Delete(compressedFile);
                    }
                    
                    return new FileInfo(testFile).Length;
                },
                config);
            
            results.Add(result);
            
            // Small delay between iterations
            await Task.Delay(100);
        }

        // Assert
        Assert.True(results.All(r => r.Success), "All memory leak test iterations should succeed");
        
        _output.WriteLine("Memory Leak Detection Results:");
        for (int i = 0; i < results.Count; i++)
        {
            _output.WriteLine($"Iteration {i + 1}: Peak Memory = {results[i].GetFormattedPeakMemory()}, Growth = {FormatBytes(results[i].MemoryGrowth)}");
        }

        // Analyze memory growth trend
        var peakMemories = results.Select(r => r.PeakMemoryUsage).ToList();
        var firstHalf = peakMemories.Take(peakMemories.Count / 2).Average();
        var secondHalf = peakMemories.Skip(peakMemories.Count / 2).Average();
        var memoryTrend = (secondHalf - firstHalf) / firstHalf;
        
        _output.WriteLine($"Memory Trend: {memoryTrend:P1} increase from first half to second half");

        // Memory should not grow significantly over iterations (less than 20% increase)
        Assert.True(memoryTrend < 0.2,
            $"Memory usage increased by {memoryTrend:P1} over iterations, suggesting a potential memory leak");
        
        // Peak memory should be consistent (coefficient of variation < 0.3)
        var avgPeakMemory = peakMemories.Average();
        var stdDevPeakMemory = Math.Sqrt(peakMemories.Select(m => Math.Pow(m - avgPeakMemory, 2)).Average());
        var coefficientOfVariation = stdDevPeakMemory / avgPeakMemory;
        
        Assert.True(coefficientOfVariation < 0.3,
            $"Memory usage is too variable (CV={coefficientOfVariation:F2}), indicating instability");
    }

    /// <summary>
    /// Creates a test file with specified size
    /// </summary>
    private async Task<string> CreateTestFileAsync(int sizeBytes)
    {
        var fileName = Path.Combine(_testDirectory, $"memory_test_{sizeBytes}.dat");
        
        // Create file with mixed content for realistic memory testing
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
        _memoryProfiler?.Dispose();
    }
}