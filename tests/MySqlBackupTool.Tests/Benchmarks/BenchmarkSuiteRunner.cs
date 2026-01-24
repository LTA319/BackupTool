using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;
using Xunit.Abstractions;

namespace MySqlBackupTool.Tests.Benchmarks;

/// <summary>
/// Comprehensive benchmark suite runner for all performance tests
/// </summary>
public class BenchmarkSuiteRunner : IDisposable
{
    private readonly IBenchmarkRunner _benchmarkRunner;
    private readonly IMemoryProfiler _memoryProfiler;
    private readonly CompressionService _compressionService;
    private readonly EncryptionService _encryptionService;
    private readonly FileTransferClient _fileTransferClient;
    private readonly string _testDirectory;
    private readonly string _reportDirectory;
    private readonly ITestOutputHelper _output;

    public BenchmarkSuiteRunner(ITestOutputHelper output)
    {
        _output = output;
        
        var loggerFactory = new LoggerFactory();
        var benchmarkLogger = loggerFactory.CreateLogger<BenchmarkRunner>();
        var memoryProfilerLogger = loggerFactory.CreateLogger<MemoryProfiler>();
        var compressionLogger = loggerFactory.CreateLogger<CompressionService>();
        var encryptionLogger = loggerFactory.CreateLogger<EncryptionService>();
        var transferLogger = loggerFactory.CreateLogger<FileTransferClient>();
        var checksumLogger = loggerFactory.CreateLogger<ChecksumService>();
        
        _memoryProfiler = new MemoryProfiler(memoryProfilerLogger);
        var checksumService = new ChecksumService(checksumLogger);
        
        _compressionService = new CompressionService(compressionLogger, _memoryProfiler);
        _encryptionService = new EncryptionService(encryptionLogger);
        _fileTransferClient = new FileTransferClient(transferLogger, checksumService, _memoryProfiler);
        _benchmarkRunner = new BenchmarkRunner(benchmarkLogger, _memoryProfiler);
        
        _testDirectory = Path.Combine(Path.GetTempPath(), "BenchmarkSuite_" + Guid.NewGuid().ToString("N")[..8]);
        _reportDirectory = Path.Combine(_testDirectory, "Reports");
        Directory.CreateDirectory(_testDirectory);
        Directory.CreateDirectory(_reportDirectory);
    }

    [Fact]
    public async Task RunCompleteBenchmarkSuite_AllOperations_GeneratesComprehensiveReport()
    {
        // Arrange
        var config = new BenchmarkConfig
        {
            WarmupIterations = 1,
            BenchmarkIterations = 3,
            MaxExecutionTime = TimeSpan.FromMinutes(30),
            CollectMemoryMetrics = true,
            CollectSystemMetrics = true,
            TestFileSizes = new List<long>
            {
                1024 * 1024,      // 1 MB
                10 * 1024 * 1024, // 10 MB
                50 * 1024 * 1024  // 50 MB
            }
        };

        var thresholds = new PerformanceThresholds
        {
            MinThroughputMBps = 5.0,
            MaxDurationForSmallFiles = TimeSpan.FromSeconds(30),
            MaxDurationForLargeFiles = TimeSpan.FromMinutes(5),
            MaxMemoryUsageMB = 1024,
            MaxCpuUsagePercent = 90.0,
            MinCompressionRatio = 0.1,
            MinSuccessRate = 90.0
        };

        // Create test files
        var testFiles = new Dictionary<string, string>();
        foreach (var size in config.TestFileSizes)
        {
            var file = await CreateTestFileAsync((int)size);
            testFiles[FormatBytes(size)] = file;
        }

        // Act - Run comprehensive benchmark suite
        var allBenchmarks = new Dictionary<string, Func<CancellationToken, Task<long>>>();

        // Compression benchmarks
        foreach (var kvp in testFiles)
        {
            var sizeLabel = kvp.Key;
            var file = kvp.Value;
            
            allBenchmarks[$"Compression_{sizeLabel}"] = async (cancellationToken) =>
            {
                var outputFile = Path.Combine(_testDirectory, $"compressed_{sizeLabel}_{Path.GetFileName(file)}.gz");
                await _compressionService.CompressFileAsync(file, outputFile, cancellationToken);
                return new FileInfo(file).Length;
            };
        }

        // Encryption benchmarks
        foreach (var kvp in testFiles)
        {
            var sizeLabel = kvp.Key;
            var file = kvp.Value;
            
            allBenchmarks[$"Encryption_{sizeLabel}"] = async (cancellationToken) =>
            {
                var outputFile = Path.Combine(_testDirectory, $"encrypted_{sizeLabel}_{Path.GetFileName(file)}.enc");
                await _encryptionService.EncryptAsync(file, outputFile, "BenchmarkPassword123!", cancellationToken);
                return new FileInfo(file).Length;
            };
        }

        // File transfer benchmarks (local copy simulation)
        foreach (var kvp in testFiles)
        {
            var sizeLabel = kvp.Key;
            var file = kvp.Value;
            
            allBenchmarks[$"FileTransfer_{sizeLabel}"] = async (cancellationToken) =>
            {
                var outputFile = Path.Combine(_testDirectory, $"transferred_{sizeLabel}_{Path.GetFileName(file)}");
                
                using var source = new FileStream(file, FileMode.Open, FileAccess.Read);
                using var destination = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
                
                await source.CopyToAsync(destination, cancellationToken);
                return new FileInfo(file).Length;
            };
        }

        // Combined operation benchmarks
        foreach (var kvp in testFiles.Where(f => f.Key != "50.0 MB")) // Skip largest for combined operations
        {
            var sizeLabel = kvp.Key;
            var file = kvp.Value;
            
            allBenchmarks[$"CompressAndEncrypt_{sizeLabel}"] = async (cancellationToken) =>
            {
                var compressedFile = Path.Combine(_testDirectory, $"combined_compressed_{sizeLabel}_{Path.GetFileName(file)}.gz");
                var encryptedFile = Path.Combine(_testDirectory, $"combined_encrypted_{sizeLabel}_{Path.GetFileName(file)}.enc");
                
                // Compress first
                await _compressionService.CompressFileAsync(file, compressedFile, cancellationToken);
                
                // Then encrypt
                await _encryptionService.EncryptAsync(compressedFile, encryptedFile, "BenchmarkPassword123!", cancellationToken);
                
                return new FileInfo(file).Length;
            };
        }

        // Run the complete benchmark suite
        var suite = await _benchmarkRunner.RunBenchmarkSuiteAsync("CompleteBenchmarkSuite", allBenchmarks, config);

        // Assert
        Assert.True(suite.Results.Any(), "Benchmark suite should produce results");
        
        var successfulResults = suite.Results.Where(r => r.Success).ToList();
        var failedResults = suite.Results.Where(r => !r.Success).ToList();
        
        _output.WriteLine($"Benchmark Suite Results:");
        _output.WriteLine($"Total Tests: {suite.Results.Count}");
        _output.WriteLine($"Successful: {successfulResults.Count}");
        _output.WriteLine($"Failed: {failedResults.Count}");
        _output.WriteLine($"Success Rate: {(double)successfulResults.Count / suite.Results.Count * 100:F1}%");
        _output.WriteLine($"Total Execution Time: {suite.TotalExecutionTime}");

        // Validate performance thresholds
        var allViolations = new List<string>();
        foreach (var result in successfulResults)
        {
            var violations = _benchmarkRunner.ValidatePerformance(result, thresholds);
            allViolations.AddRange(violations.Select(v => $"{result.BenchmarkName}: {v}"));
        }

        if (allViolations.Any())
        {
            _output.WriteLine("\nPerformance Threshold Violations:");
            foreach (var violation in allViolations)
            {
                _output.WriteLine($"- {violation}");
            }
        }

        // Generate reports
        await GenerateAllReportsAsync(suite);

        // Performance assertions
        var successRate = (double)successfulResults.Count / suite.Results.Count * 100;
        Assert.True(successRate >= thresholds.MinSuccessRate, 
            $"Success rate {successRate:F1}% is below minimum {thresholds.MinSuccessRate}%");

        // At least 80% of successful tests should meet performance thresholds
        var testsWithViolations = successfulResults.Count(r => _benchmarkRunner.ValidatePerformance(r, thresholds).Any());
        var thresholdComplianceRate = (double)(successfulResults.Count - testsWithViolations) / successfulResults.Count * 100;
        
        Assert.True(thresholdComplianceRate >= 80.0,
            $"Only {thresholdComplianceRate:F1}% of tests meet performance thresholds (expected >= 80%)");

        _output.WriteLine($"\nPerformance Threshold Compliance: {thresholdComplianceRate:F1}%");
    }

    [Fact]
    public async Task RunScalabilityBenchmarks_DifferentFileSizes_ShowsLinearScaling()
    {
        // Arrange
        var fileSizes = new[] { 1024 * 1024, 5 * 1024 * 1024, 25 * 1024 * 1024 }; // 1MB, 5MB, 25MB
        var testFiles = new List<string>();
        
        foreach (var size in fileSizes)
        {
            var file = await CreateTestFileAsync(size);
            testFiles.Add(file);
        }

        var config = new BenchmarkConfig
        {
            WarmupIterations = 1,
            BenchmarkIterations = 3,
            MaxExecutionTime = TimeSpan.FromMinutes(15)
        };

        // Act - Test compression scalability
        var scalabilityResults = new List<BenchmarkResult>();
        
        for (int i = 0; i < testFiles.Count; i++)
        {
            var file = testFiles[i];
            var size = fileSizes[i];
            
            var result = await _benchmarkRunner.RunBenchmarkAsync(
                $"Scalability_Compression_{FormatBytes(size)}",
                "Compression",
                async (cancellationToken) =>
                {
                    var outputFile = Path.Combine(_testDirectory, $"scalability_{i}_{Path.GetFileName(file)}.gz");
                    await _compressionService.CompressFileAsync(file, outputFile, cancellationToken);
                    return new FileInfo(file).Length;
                },
                config);
            
            scalabilityResults.Add(result);
        }

        // Assert
        Assert.True(scalabilityResults.All(r => r.Success), "All scalability tests should succeed");
        
        _output.WriteLine("Scalability Analysis:");
        for (int i = 0; i < scalabilityResults.Count; i++)
        {
            var result = scalabilityResults[i];
            _output.WriteLine($"{FormatBytes(fileSizes[i])}: {result.GetFormattedDuration()}, {result.GetFormattedThroughput()}");
        }

        // Analyze scaling characteristics
        var throughputs = scalabilityResults.Select(r => r.ThroughputMBps).ToList();
        var durations = scalabilityResults.Select(r => r.Duration.TotalSeconds).ToList();
        var sizes = fileSizes.Select(s => s / (1024.0 * 1024.0)).ToList(); // Convert to MB

        // Calculate scaling efficiency (throughput should not degrade significantly)
        var minThroughput = throughputs.Min();
        var maxThroughput = throughputs.Max();
        var throughputVariance = (maxThroughput - minThroughput) / maxThroughput;
        
        _output.WriteLine($"Throughput Variance: {throughputVariance:P1}");
        
        // Duration should scale roughly linearly with file size
        var durationPerMB = durations.Zip(sizes, (d, s) => d / s).ToList();
        var avgDurationPerMB = durationPerMB.Average();
        var durationVariance = durationPerMB.Select(d => Math.Abs(d - avgDurationPerMB) / avgDurationPerMB).Average();
        
        _output.WriteLine($"Duration Linearity Variance: {durationVariance:P1}");

        // Assertions for good scaling
        Assert.True(throughputVariance < 0.5, // Less than 50% throughput variance
            $"Throughput variance {throughputVariance:P1} indicates poor scaling");
        
        Assert.True(durationVariance < 0.3, // Less than 30% variance in duration per MB
            $"Duration variance {durationVariance:P1} indicates non-linear scaling");
    }

    [Fact]
    public async Task RunPerformanceRegressionTest_CompareWithBaseline_DetectsRegressions()
    {
        // Arrange
        var testFile = await CreateTestFileAsync(10 * 1024 * 1024); // 10MB
        
        var config = new BenchmarkConfig
        {
            WarmupIterations = 2,
            BenchmarkIterations = 5,
            MaxExecutionTime = TimeSpan.FromMinutes(5)
        };

        // Simulate baseline performance (could be loaded from previous test runs)
        var baselineResult = await _benchmarkRunner.RunBenchmarkAsync(
            "BaselineCompression",
            "Compression",
            async (cancellationToken) =>
            {
                var outputFile = Path.Combine(_testDirectory, $"baseline_{Path.GetFileName(testFile)}.gz");
                await _compressionService.CompressFileAsync(testFile, outputFile, cancellationToken);
                return new FileInfo(testFile).Length;
            },
            config);

        // Current performance test
        var currentResult = await _benchmarkRunner.RunBenchmarkAsync(
            "CurrentCompression",
            "Compression",
            async (cancellationToken) =>
            {
                var outputFile = Path.Combine(_testDirectory, $"current_{Path.GetFileName(testFile)}.gz");
                await _compressionService.CompressFileAsync(testFile, outputFile, cancellationToken);
                return new FileInfo(testFile).Length;
            },
            config);

        // Act - Compare performance
        var comparison = _benchmarkRunner.CompareBenchmarks(baselineResult, currentResult);

        // Assert
        Assert.True(baselineResult.Success, $"Baseline test failed: {baselineResult.ErrorMessage}");
        Assert.True(currentResult.Success, $"Current test failed: {currentResult.ErrorMessage}");
        
        _output.WriteLine("Performance Regression Analysis:");
        _output.WriteLine($"Baseline: {baselineResult.GetFormattedDuration()}, {baselineResult.GetFormattedThroughput()}");
        _output.WriteLine($"Current:  {currentResult.GetFormattedDuration()}, {currentResult.GetFormattedThroughput()}");
        _output.WriteLine($"Duration Change: {comparison.GetFormattedDurationImprovement()}");
        _output.WriteLine($"Throughput Change: {comparison.GetFormattedThroughputImprovement()}");
        _output.WriteLine($"Memory Change: {comparison.GetFormattedMemoryImprovement()}");

        // Performance regression thresholds
        var maxAcceptableRegression = -20.0; // 20% performance degradation is concerning
        
        Assert.True(comparison.DurationImprovement > maxAcceptableRegression,
            $"Performance regression detected: {comparison.GetFormattedDurationImprovement()} duration change");
        
        Assert.True(comparison.ThroughputImprovement > maxAcceptableRegression,
            $"Throughput regression detected: {comparison.GetFormattedThroughputImprovement()} throughput change");
    }

    /// <summary>
    /// Generates all report formats for the benchmark suite
    /// </summary>
    private async Task GenerateAllReportsAsync(BenchmarkSuite suite)
    {
        try
        {
            // Generate HTML report
            var htmlReport = await _benchmarkRunner.GenerateReportAsync(suite, "HTML");
            var htmlPath = Path.Combine(_reportDirectory, "benchmark_report.html");
            await File.WriteAllTextAsync(htmlPath, htmlReport);
            _output.WriteLine($"HTML report generated: {htmlPath}");

            // Generate JSON report
            var jsonReport = await _benchmarkRunner.GenerateReportAsync(suite, "JSON");
            var jsonPath = Path.Combine(_reportDirectory, "benchmark_report.json");
            await File.WriteAllTextAsync(jsonPath, jsonReport);
            _output.WriteLine($"JSON report generated: {jsonPath}");

            // Generate CSV report
            var csvReport = await _benchmarkRunner.GenerateReportAsync(suite, "CSV");
            var csvPath = Path.Combine(_reportDirectory, "benchmark_report.csv");
            await File.WriteAllTextAsync(csvPath, csvReport);
            _output.WriteLine($"CSV report generated: {csvPath}");

            // Generate summary report
            await GenerateSummaryReportAsync(suite);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Failed to generate reports: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a summary report with key insights
    /// </summary>
    private async Task GenerateSummaryReportAsync(BenchmarkSuite suite)
    {
        var summary = new System.Text.StringBuilder();
        
        summary.AppendLine("# MySQL Backup Tool - Performance Benchmark Summary");
        summary.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        summary.AppendLine();
        
        summary.AppendLine("## Overall Results");
        summary.AppendLine($"- Total Tests: {suite.Results.Count}");
        summary.AppendLine($"- Successful: {suite.Results.Count(r => r.Success)}");
        summary.AppendLine($"- Failed: {suite.Results.Count(r => !r.Success)}");
        summary.AppendLine($"- Success Rate: {(double)suite.Results.Count(r => r.Success) / suite.Results.Count * 100:F1}%");
        summary.AppendLine($"- Total Execution Time: {suite.TotalExecutionTime}");
        summary.AppendLine();

        // Performance by operation type
        var operationTypes = suite.Results.Select(r => r.OperationType).Distinct().ToList();
        
        summary.AppendLine("## Performance by Operation Type");
        foreach (var operationType in operationTypes)
        {
            var opSummary = suite.GetSummary(operationType);
            summary.AppendLine($"### {operationType}");
            summary.AppendLine($"- Tests: {opSummary.TestCount}");
            summary.AppendLine($"- Success Rate: {opSummary.SuccessRate:F1}%");
            summary.AppendLine($"- Average Duration: {opSummary.GetFormattedAverageDuration()}");
            summary.AppendLine($"- Average Throughput: {opSummary.GetFormattedAverageThroughput()}");
            summary.AppendLine($"- Peak Memory: {opSummary.GetFormattedPeakMemory()}");
            summary.AppendLine();
        }

        // Top performers
        var topThroughput = suite.Results.Where(r => r.Success && r.ThroughputMBps > 0)
            .OrderByDescending(r => r.ThroughputMBps)
            .Take(3)
            .ToList();

        if (topThroughput.Any())
        {
            summary.AppendLine("## Top Throughput Performers");
            foreach (var result in topThroughput)
            {
                summary.AppendLine($"- {result.BenchmarkName}: {result.GetFormattedThroughput()}");
            }
            summary.AppendLine();
        }

        // Memory efficiency
        var memoryEfficient = suite.Results.Where(r => r.Success && r.BytesProcessed > 0)
            .OrderBy(r => (double)r.PeakMemoryUsage / r.BytesProcessed)
            .Take(3)
            .ToList();

        if (memoryEfficient.Any())
        {
            summary.AppendLine("## Most Memory Efficient");
            foreach (var result in memoryEfficient)
            {
                var efficiency = (double)result.PeakMemoryUsage / result.BytesProcessed;
                summary.AppendLine($"- {result.BenchmarkName}: {efficiency:F3} memory/data ratio");
            }
            summary.AppendLine();
        }

        // Environment info
        summary.AppendLine("## Test Environment");
        summary.AppendLine($"- Machine: {suite.Environment.MachineName}");
        summary.AppendLine($"- OS: {suite.Environment.OperatingSystem}");
        summary.AppendLine($"- Processors: {suite.Environment.ProcessorCount}");
        summary.AppendLine($"- Total Memory: {suite.Environment.GetFormattedTotalMemory()}");
        summary.AppendLine($"- .NET Version: {suite.Environment.DotNetVersion}");
        summary.AppendLine($"- Build Type: {(suite.Environment.IsDebugBuild ? "Debug" : "Release")}");

        var summaryPath = Path.Combine(_reportDirectory, "benchmark_summary.md");
        await File.WriteAllTextAsync(summaryPath, summary.ToString());
        _output.WriteLine($"Summary report generated: {summaryPath}");
    }

    /// <summary>
    /// Creates a test file with specified size
    /// </summary>
    private async Task<string> CreateTestFileAsync(int sizeBytes)
    {
        var fileName = Path.Combine(_testDirectory, $"benchmark_test_{sizeBytes}.dat");
        
        // Create file with mixed content for realistic benchmarking
        var random = new Random(42); // Fixed seed for reproducible results
        var buffer = new byte[Math.Min(sizeBytes, 1024 * 1024)]; // 1MB buffer max
        
        using var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
        var remaining = sizeBytes;
        
        while (remaining > 0)
        {
            var chunkSize = Math.Min(remaining, buffer.Length);
            
            // Fill with mixed pattern data
            for (int i = 0; i < chunkSize; i++)
            {
                if (i % 1000 == 0)
                {
                    // Add some random data
                    buffer[i] = (byte)random.Next(256);
                }
                else
                {
                    // Add pattern data for compression testing
                    buffer[i] = (byte)((i % 256) ^ (i / 256 % 256));
                }
            }
            
            await fileStream.WriteAsync(buffer.AsMemory(0, chunkSize));
            remaining -= chunkSize;
        }
        
        return fileName;
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
        
        _compressionService?.Dispose();
        _memoryProfiler?.Dispose();
    }
}