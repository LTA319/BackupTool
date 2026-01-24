using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Service for running performance benchmarks
/// </summary>
public class BenchmarkRunner : IBenchmarkRunner
{
    private readonly ILogger<BenchmarkRunner> _logger;
    private readonly IMemoryProfiler? _memoryProfiler;

    public BenchmarkRunner(ILogger<BenchmarkRunner> logger, IMemoryProfiler? memoryProfiler = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryProfiler = memoryProfiler;
    }

    /// <summary>
    /// Runs a single benchmark test
    /// </summary>
    public async Task<BenchmarkResult> RunBenchmarkAsync(string benchmarkName, string operationType, 
        Func<CancellationToken, Task<long>> testAction, BenchmarkConfig? config = null)
    {
        config ??= new BenchmarkConfig();
        
        _logger.LogInformation("Starting benchmark: {BenchmarkName} ({OperationType})", benchmarkName, operationType);

        var result = new BenchmarkResult
        {
            BenchmarkName = benchmarkName,
            OperationType = operationType,
            TestConfiguration = JsonSerializer.Serialize(config)
        };

        try
        {
            // Warmup iterations
            _logger.LogDebug("Running {WarmupIterations} warmup iterations", config.WarmupIterations);
            for (int i = 0; i < config.WarmupIterations; i++)
            {
                if (config.ForceGarbageCollection)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }

                using var cts = new CancellationTokenSource(config.MaxExecutionTime);
                await testAction(cts.Token);
            }

            // Start memory profiling if available
            var operationId = $"benchmark-{benchmarkName}-{Guid.NewGuid():N}";
            _memoryProfiler?.StartProfiling(operationId, operationType);

            // Actual benchmark run
            var stopwatch = Stopwatch.StartNew();
            var process = Process.GetCurrentProcess();
            var initialMemory = process.WorkingSet64;
            var initialCpuTime = process.TotalProcessorTime;

            result.StartTime = DateTime.UtcNow;

            using var cancellationTokenSource = new CancellationTokenSource(config.MaxExecutionTime);
            result.BytesProcessed = await testAction(cancellationTokenSource.Token);

            stopwatch.Stop();
            result.EndTime = DateTime.UtcNow;

            // Collect performance metrics
            process.Refresh();
            var finalMemory = process.WorkingSet64;
            var finalCpuTime = process.TotalProcessorTime;

            result.PeakMemoryUsage = finalMemory;
            result.MemoryGrowth = finalMemory - initialMemory;
            result.ThreadCount = process.Threads.Count;

            // Calculate CPU usage
            var cpuUsed = (finalCpuTime - initialCpuTime).TotalMilliseconds;
            var totalMsPassed = stopwatch.ElapsedMilliseconds * Environment.ProcessorCount;
            result.CpuUsagePercent = totalMsPassed > 0 ? (cpuUsed / totalMsPassed) * 100 : 0;

            result.Success = true;

            // Stop memory profiling and get detailed metrics
            if (_memoryProfiler != null)
            {
                var memoryProfile = _memoryProfiler.StopProfiling(operationId);
                result.PeakMemoryUsage = memoryProfile.Statistics.PeakWorkingSet;
                result.AverageMemoryUsage = memoryProfile.Statistics.AverageWorkingSet;
                result.MemoryGrowth = memoryProfile.Statistics.MemoryGrowth;
                
                // Add memory recommendations as additional metrics
                var recommendations = _memoryProfiler.GetRecommendations(memoryProfile);
                result.AdditionalMetrics["MemoryRecommendations"] = recommendations.Count;
                result.AdditionalMetrics["MemoryProfile"] = memoryProfile;
            }

            _logger.LogInformation("Benchmark completed: {BenchmarkName} - Duration: {Duration}ms, Throughput: {Throughput} MB/s, Peak Memory: {Memory}",
                benchmarkName, stopwatch.ElapsedMilliseconds, result.GetFormattedThroughput(), result.GetFormattedPeakMemory());
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.ErrorMessage = "Benchmark timed out";
            result.EndTime = DateTime.UtcNow;
            
            _logger.LogWarning("Benchmark timed out: {BenchmarkName}", benchmarkName);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = DateTime.UtcNow;
            
            _logger.LogError(ex, "Benchmark failed: {BenchmarkName}", benchmarkName);
        }

        return result;
    }

    /// <summary>
    /// Runs a benchmark suite with multiple iterations
    /// </summary>
    public async Task<BenchmarkSuite> RunBenchmarkSuiteAsync(string suiteName, 
        Dictionary<string, Func<CancellationToken, Task<long>>> benchmarks, BenchmarkConfig? config = null)
    {
        config ??= new BenchmarkConfig();
        
        _logger.LogInformation("Starting benchmark suite: {SuiteName} with {BenchmarkCount} benchmarks", 
            suiteName, benchmarks.Count);

        var suite = new BenchmarkSuite
        {
            SuiteName = suiteName,
            Environment = GetEnvironmentInfo(),
            ExecutionTime = DateTime.UtcNow
        };

        var overallStopwatch = Stopwatch.StartNew();

        try
        {
            foreach (var benchmark in benchmarks)
            {
                _logger.LogInformation("Running benchmark: {BenchmarkName}", benchmark.Key);

                // Run multiple iterations for statistical significance
                for (int iteration = 0; iteration < config.BenchmarkIterations; iteration++)
                {
                    var result = await RunBenchmarkAsync(
                        $"{benchmark.Key}_Iteration_{iteration + 1}", 
                        benchmark.Key, 
                        benchmark.Value, 
                        config);

                    suite.Results.Add(result);

                    // Add small delay between iterations to allow system to stabilize
                    await Task.Delay(100);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Benchmark suite failed: {SuiteName}", suiteName);
        }
        finally
        {
            overallStopwatch.Stop();
            suite.TotalExecutionTime = overallStopwatch.Elapsed;
        }

        _logger.LogInformation("Benchmark suite completed: {SuiteName} - Total time: {TotalTime}, Results: {ResultCount}",
            suiteName, suite.TotalExecutionTime, suite.Results.Count);

        return suite;
    }

    /// <summary>
    /// Compares two benchmark results
    /// </summary>
    public BenchmarkComparison CompareBenchmarks(BenchmarkResult baseline, BenchmarkResult comparison)
    {
        return new BenchmarkComparison
        {
            BaselineResult = baseline,
            ComparisonResult = comparison
        };
    }

    /// <summary>
    /// Validates benchmark results against performance thresholds
    /// </summary>
    public List<string> ValidatePerformance(BenchmarkResult result, PerformanceThresholds thresholds)
    {
        return thresholds.ValidateResult(result);
    }

    /// <summary>
    /// Generates a performance report from benchmark results
    /// </summary>
    public async Task<string> GenerateReportAsync(BenchmarkSuite suite, string format = "HTML")
    {
        return format.ToUpperInvariant() switch
        {
            "HTML" => await GenerateHtmlReportAsync(suite),
            "JSON" => GenerateJsonReport(suite),
            "CSV" => GenerateCsvReport(suite),
            _ => throw new ArgumentException($"Unsupported report format: {format}")
        };
    }

    /// <summary>
    /// Gets current system environment information
    /// </summary>
    private BenchmarkEnvironment GetEnvironmentInfo()
    {
        var environment = new BenchmarkEnvironment();

        try
        {
            // Get memory information
            var gcInfo = GC.GetGCMemoryInfo();
            environment.TotalPhysicalMemory = gcInfo.TotalAvailableMemoryBytes;
            environment.AvailablePhysicalMemory = Math.Max(0, gcInfo.TotalAvailableMemoryBytes - GC.GetTotalMemory(false));

            // Detect debug vs release build
            environment.IsDebugBuild = IsDebugBuild();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect complete environment information");
        }

        return environment;
    }

    /// <summary>
    /// Detects if running in debug build
    /// </summary>
    private static bool IsDebugBuild()
    {
        #if DEBUG
        return true;
        #else
        return false;
        #endif
    }

    /// <summary>
    /// Generates HTML performance report
    /// </summary>
    private async Task<string> GenerateHtmlReportAsync(BenchmarkSuite suite)
    {
        var html = new StringBuilder();
        
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine($"<title>Performance Benchmark Report - {suite.SuiteName}</title>");
        html.AppendLine("<style>");
        html.AppendLine(@"
            body { font-family: Arial, sans-serif; margin: 20px; }
            .header { background-color: #f0f0f0; padding: 20px; border-radius: 5px; margin-bottom: 20px; }
            .summary { background-color: #e8f5e8; padding: 15px; border-radius: 5px; margin-bottom: 20px; }
            .warning { background-color: #fff3cd; padding: 15px; border-radius: 5px; margin-bottom: 20px; }
            .error { background-color: #f8d7da; padding: 15px; border-radius: 5px; margin-bottom: 20px; }
            table { border-collapse: collapse; width: 100%; margin-bottom: 20px; }
            th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
            th { background-color: #f2f2f2; }
            .metric { font-weight: bold; }
            .good { color: green; }
            .warning-text { color: orange; }
            .error-text { color: red; }
        ");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");

        // Header
        html.AppendLine("<div class='header'>");
        html.AppendLine($"<h1>Performance Benchmark Report</h1>");
        html.AppendLine($"<h2>{suite.SuiteName}</h2>");
        html.AppendLine($"<p><strong>Execution Time:</strong> {suite.ExecutionTime:yyyy-MM-dd HH:mm:ss}</p>");
        html.AppendLine($"<p><strong>Total Duration:</strong> {suite.TotalExecutionTime}</p>");
        html.AppendLine($"<p><strong>Total Results:</strong> {suite.Results.Count}</p>");
        html.AppendLine("</div>");

        // Environment Information
        html.AppendLine("<div class='summary'>");
        html.AppendLine("<h3>Test Environment</h3>");
        html.AppendLine($"<p><strong>Machine:</strong> {suite.Environment.MachineName}</p>");
        html.AppendLine($"<p><strong>OS:</strong> {suite.Environment.OperatingSystem}</p>");
        html.AppendLine($"<p><strong>Architecture:</strong> {suite.Environment.ProcessorArchitecture}</p>");
        html.AppendLine($"<p><strong>Processors:</strong> {suite.Environment.ProcessorCount}</p>");
        html.AppendLine($"<p><strong>Total Memory:</strong> {suite.Environment.GetFormattedTotalMemory()}</p>");
        html.AppendLine($"<p><strong>.NET Version:</strong> {suite.Environment.DotNetVersion}</p>");
        html.AppendLine($"<p><strong>Build Type:</strong> {(suite.Environment.IsDebugBuild ? "Debug" : "Release")}</p>");
        html.AppendLine("</div>");

        // Summary by operation type
        var operationTypes = suite.Results.Select(r => r.OperationType).Distinct().ToList();
        
        foreach (var operationType in operationTypes)
        {
            var summary = suite.GetSummary(operationType);
            
            html.AppendLine("<div class='summary'>");
            html.AppendLine($"<h3>{operationType} Summary</h3>");
            html.AppendLine($"<p><strong>Tests:</strong> {summary.TestCount}</p>");
            html.AppendLine($"<p><strong>Success Rate:</strong> {summary.SuccessRate:F1}%</p>");
            html.AppendLine($"<p><strong>Average Duration:</strong> {summary.GetFormattedAverageDuration()}</p>");
            html.AppendLine($"<p><strong>Fastest:</strong> {summary.FastestDuration.TotalMilliseconds:F0} ms</p>");
            html.AppendLine($"<p><strong>Slowest:</strong> {summary.SlowestDuration.TotalMilliseconds:F0} ms</p>");
            
            if (summary.AverageThroughput > 0)
            {
                html.AppendLine($"<p><strong>Average Throughput:</strong> {summary.GetFormattedAverageThroughput()}</p>");
                html.AppendLine($"<p><strong>Peak Throughput:</strong> {summary.GetFormattedPeakThroughput()}</p>");
            }
            
            html.AppendLine($"<p><strong>Average Memory:</strong> {summary.GetFormattedAverageMemory()}</p>");
            html.AppendLine($"<p><strong>Peak Memory:</strong> {summary.GetFormattedPeakMemory()}</p>");
            html.AppendLine($"<p><strong>Total Bytes Processed:</strong> {summary.GetFormattedTotalBytes()}</p>");
            html.AppendLine("</div>");
        }

        // Detailed results table
        html.AppendLine("<h3>Detailed Results</h3>");
        html.AppendLine("<table>");
        html.AppendLine("<tr>");
        html.AppendLine("<th>Benchmark</th>");
        html.AppendLine("<th>Operation</th>");
        html.AppendLine("<th>Duration</th>");
        html.AppendLine("<th>Throughput</th>");
        html.AppendLine("<th>Bytes Processed</th>");
        html.AppendLine("<th>Peak Memory</th>");
        html.AppendLine("<th>CPU Usage</th>");
        html.AppendLine("<th>Success</th>");
        html.AppendLine("</tr>");

        foreach (var result in suite.Results.OrderBy(r => r.OperationType).ThenBy(r => r.BenchmarkName))
        {
            var rowClass = result.Success ? "" : "error-text";
            html.AppendLine($"<tr class='{rowClass}'>");
            html.AppendLine($"<td>{result.BenchmarkName}</td>");
            html.AppendLine($"<td>{result.OperationType}</td>");
            html.AppendLine($"<td>{result.GetFormattedDuration()}</td>");
            html.AppendLine($"<td>{result.GetFormattedThroughput()}</td>");
            html.AppendLine($"<td>{result.GetFormattedBytesProcessed()}</td>");
            html.AppendLine($"<td>{result.GetFormattedPeakMemory()}</td>");
            html.AppendLine($"<td>{result.CpuUsagePercent:F1}%</td>");
            html.AppendLine($"<td>{(result.Success ? "✓" : "✗")}</td>");
            html.AppendLine("</tr>");
        }

        html.AppendLine("</table>");

        // Failed tests
        var failedResults = suite.Results.Where(r => !r.Success).ToList();
        if (failedResults.Any())
        {
            html.AppendLine("<div class='error'>");
            html.AppendLine("<h3>Failed Tests</h3>");
            foreach (var failed in failedResults)
            {
                html.AppendLine($"<p><strong>{failed.BenchmarkName}:</strong> {failed.ErrorMessage}</p>");
            }
            html.AppendLine("</div>");
        }

        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    /// <summary>
    /// Generates JSON performance report
    /// </summary>
    private string GenerateJsonReport(BenchmarkSuite suite)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(suite, options);
    }

    /// <summary>
    /// Generates CSV performance report
    /// </summary>
    private string GenerateCsvReport(BenchmarkSuite suite)
    {
        var csv = new StringBuilder();
        
        // Header
        csv.AppendLine("BenchmarkName,OperationType,StartTime,Duration(ms),ThroughputMBps,BytesProcessed,PeakMemoryUsage,AverageMemoryUsage,CpuUsagePercent,Success,ErrorMessage");

        // Data rows
        foreach (var result in suite.Results)
        {
            csv.AppendLine($"{EscapeCsv(result.BenchmarkName)},{EscapeCsv(result.OperationType)},{result.StartTime:yyyy-MM-dd HH:mm:ss},{result.Duration.TotalMilliseconds:F0},{result.ThroughputMBps:F2},{result.BytesProcessed},{result.PeakMemoryUsage},{result.AverageMemoryUsage},{result.CpuUsagePercent:F1},{result.Success},{EscapeCsv(result.ErrorMessage ?? "")}");
        }

        return csv.ToString();
    }

    /// <summary>
    /// Escapes CSV field values
    /// </summary>
    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}