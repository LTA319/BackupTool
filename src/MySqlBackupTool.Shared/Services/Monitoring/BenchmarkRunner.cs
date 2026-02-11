using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 运行性能基准测试的服务 / Service for running performance benchmarks
/// </summary>
public class BenchmarkRunner : IBenchmarkRunner
{
    /// <summary>
    /// 日志记录器 / Logger
    /// </summary>
    private readonly ILogger<BenchmarkRunner> _logger;
    
    /// <summary>
    /// 内存分析器，可选 / Memory profiler, optional
    /// </summary>
    private readonly IMemoryProfiler? _memoryProfiler;

    /// <summary>
    /// 初始化基准测试运行器 / Initializes the benchmark runner
    /// </summary>
    /// <param name="logger">日志记录器 / Logger</param>
    /// <param name="memoryProfiler">内存分析器，可选 / Memory profiler, optional</param>
    /// <exception cref="ArgumentNullException">当日志记录器为null时抛出 / Thrown when logger is null</exception>
    public BenchmarkRunner(ILogger<BenchmarkRunner> logger, IMemoryProfiler? memoryProfiler = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryProfiler = memoryProfiler;
    }

    /// <summary>
    /// 运行单个基准测试 / Runs a single benchmark test
    /// </summary>
    /// <param name="benchmarkName">基准测试名称 / Benchmark name</param>
    /// <param name="operationType">操作类型 / Operation type</param>
    /// <param name="testAction">测试操作函数 / Test action function</param>
    /// <param name="config">基准测试配置 / Benchmark configuration</param>
    /// <returns>基准测试结果 / Benchmark result</returns>
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
            // 预热迭代 / Warmup iterations
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

            // 如果可用，开始内存分析 / Start memory profiling if available
            var operationId = $"benchmark-{benchmarkName}-{Guid.NewGuid():N}";
            _memoryProfiler?.StartProfiling(operationId, operationType);

            // 实际基准测试运行 / Actual benchmark run
            var stopwatch = Stopwatch.StartNew();
            var process = Process.GetCurrentProcess();
            var initialMemory = process.WorkingSet64;
            var initialCpuTime = process.TotalProcessorTime;

            result.StartTime = DateTime.Now;

            using var cancellationTokenSource = new CancellationTokenSource(config.MaxExecutionTime);
            result.BytesProcessed = await testAction(cancellationTokenSource.Token);

            stopwatch.Stop();
            result.EndTime = DateTime.Now;

            // 收集性能指标 / Collect performance metrics
            process.Refresh();
            var finalMemory = process.WorkingSet64;
            var finalCpuTime = process.TotalProcessorTime;

            result.PeakMemoryUsage = finalMemory;
            result.MemoryGrowth = finalMemory - initialMemory;
            result.ThreadCount = process.Threads.Count;

            // 计算CPU使用率 / Calculate CPU usage
            var cpuUsed = (finalCpuTime - initialCpuTime).TotalMilliseconds;
            var totalMsPassed = stopwatch.ElapsedMilliseconds * Environment.ProcessorCount;
            result.CpuUsagePercent = totalMsPassed > 0 ? (cpuUsed / totalMsPassed) * 100 : 0;

            result.Success = true;

            // 停止内存分析并获取详细指标 / Stop memory profiling and get detailed metrics
            if (_memoryProfiler != null)
            {
                var memoryProfile = _memoryProfiler.StopProfiling(operationId);
                result.PeakMemoryUsage = memoryProfile.Statistics.PeakWorkingSet;
                result.AverageMemoryUsage = memoryProfile.Statistics.AverageWorkingSet;
                result.MemoryGrowth = memoryProfile.Statistics.MemoryGrowth;
                
                // 将内存建议添加为额外指标 / Add memory recommendations as additional metrics
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
            result.EndTime = DateTime.Now;
            
            _logger.LogWarning("Benchmark timed out: {BenchmarkName}", benchmarkName);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = DateTime.Now;
            
            _logger.LogError(ex, "Benchmark failed: {BenchmarkName}", benchmarkName);
        }

        return result;
    }

    /// <summary>
    /// 运行包含多次迭代的基准测试套件 / Runs a benchmark suite with multiple iterations
    /// </summary>
    /// <param name="suiteName">套件名称 / Suite name</param>
    /// <param name="benchmarks">基准测试字典 / Benchmarks dictionary</param>
    /// <param name="config">基准测试配置 / Benchmark configuration</param>
    /// <returns>基准测试套件结果 / Benchmark suite result</returns>
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
            ExecutionTime = DateTime.Now
        };

        var overallStopwatch = Stopwatch.StartNew();

        try
        {
            foreach (var benchmark in benchmarks)
            {
                _logger.LogInformation("Running benchmark: {BenchmarkName}", benchmark.Key);

                // 运行多次迭代以获得统计显著性 / Run multiple iterations for statistical significance
                for (int iteration = 0; iteration < config.BenchmarkIterations; iteration++)
                {
                    var result = await RunBenchmarkAsync(
                        $"{benchmark.Key}_Iteration_{iteration + 1}", 
                        benchmark.Key, 
                        benchmark.Value, 
                        config);

                    suite.Results.Add(result);

                    // 在迭代之间添加小延迟以允许系统稳定 / Add small delay between iterations to allow system to stabilize
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
    /// 比较两个基准测试结果 / Compares two benchmark results
    /// </summary>
    /// <param name="baseline">基线结果 / Baseline result</param>
    /// <param name="comparison">比较结果 / Comparison result</param>
    /// <returns>基准测试比较结果 / Benchmark comparison result</returns>
    public BenchmarkComparison CompareBenchmarks(BenchmarkResult baseline, BenchmarkResult comparison)
    {
        return new BenchmarkComparison
        {
            BaselineResult = baseline,
            ComparisonResult = comparison
        };
    }

    /// <summary>
    /// 根据性能阈值验证基准测试结果 / Validates benchmark results against performance thresholds
    /// </summary>
    /// <param name="result">基准测试结果 / Benchmark result</param>
    /// <param name="thresholds">性能阈值 / Performance thresholds</param>
    /// <returns>验证错误列表 / List of validation errors</returns>
    public List<string> ValidatePerformance(BenchmarkResult result, PerformanceThresholds thresholds)
    {
        return thresholds.ValidateResult(result);
    }

    /// <summary>
    /// 从基准测试结果生成性能报告 / Generates a performance report from benchmark results
    /// </summary>
    /// <param name="suite">基准测试套件 / Benchmark suite</param>
    /// <param name="format">报告格式 / Report format</param>
    /// <returns>生成的报告字符串 / Generated report string</returns>
    /// <exception cref="ArgumentException">当报告格式不支持时抛出 / Thrown when report format is not supported</exception>
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
    /// 获取当前系统环境信息 / Gets current system environment information
    /// </summary>
    /// <returns>基准测试环境信息 / Benchmark environment information</returns>
    private BenchmarkEnvironment GetEnvironmentInfo()
    {
        var environment = new BenchmarkEnvironment();

        try
        {
            // 获取内存信息 / Get memory information
            var gcInfo = GC.GetGCMemoryInfo();
            environment.TotalPhysicalMemory = gcInfo.TotalAvailableMemoryBytes;
            environment.AvailablePhysicalMemory = Math.Max(0, gcInfo.TotalAvailableMemoryBytes - GC.GetTotalMemory(false));

            // 检测调试版本与发布版本 / Detect debug vs release build
            environment.IsDebugBuild = IsDebugBuild();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect complete environment information");
        }

        return environment;
    }

    /// <summary>
    /// 检测是否在调试版本中运行 / Detects if running in debug build
    /// </summary>
    /// <returns>是否为调试版本 / Whether it's a debug build</returns>
    private static bool IsDebugBuild()
    {
        #if DEBUG
        return true;
        #else
        return false;
        #endif
    }

    /// <summary>
    /// 生成HTML性能报告 / Generates HTML performance report
    /// </summary>
    /// <param name="suite">基准测试套件 / Benchmark suite</param>
    /// <returns>HTML格式的报告 / Report in HTML format</returns>
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

        // 标题 / Header
        html.AppendLine("<div class='header'>");
        html.AppendLine($"<h1>Performance Benchmark Report</h1>");
        html.AppendLine($"<h2>{suite.SuiteName}</h2>");
        html.AppendLine($"<p><strong>Execution Time:</strong> {suite.ExecutionTime:yyyy-MM-dd HH:mm:ss}</p>");
        html.AppendLine($"<p><strong>Total Duration:</strong> {suite.TotalExecutionTime}</p>");
        html.AppendLine($"<p><strong>Total Results:</strong> {suite.Results.Count}</p>");
        html.AppendLine("</div>");

        // 环境信息 / Environment Information
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

        // 按操作类型汇总 / Summary by operation type
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

        // 详细结果表 / Detailed results table
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

        // 失败的测试 / Failed tests
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
    /// 生成JSON性能报告 / Generates JSON performance report
    /// </summary>
    /// <param name="suite">基准测试套件 / Benchmark suite</param>
    /// <returns>JSON格式的报告 / Report in JSON format</returns>
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
    /// 生成CSV性能报告 / Generates CSV performance report
    /// </summary>
    /// <param name="suite">基准测试套件 / Benchmark suite</param>
    /// <returns>CSV格式的报告 / Report in CSV format</returns>
    private string GenerateCsvReport(BenchmarkSuite suite)
    {
        var csv = new StringBuilder();
        
        // 标题行 / Header
        csv.AppendLine("BenchmarkName,OperationType,StartTime,Duration(ms),ThroughputMBps,BytesProcessed,PeakMemoryUsage,AverageMemoryUsage,CpuUsagePercent,Success,ErrorMessage");

        // 数据行 / Data rows
        foreach (var result in suite.Results)
        {
            csv.AppendLine($"{EscapeCsv(result.BenchmarkName)},{EscapeCsv(result.OperationType)},{result.StartTime:yyyy-MM-dd HH:mm:ss},{result.Duration.TotalMilliseconds:F0},{result.ThroughputMBps:F2},{result.BytesProcessed},{result.PeakMemoryUsage},{result.AverageMemoryUsage},{result.CpuUsagePercent:F1},{result.Success},{EscapeCsv(result.ErrorMessage ?? "")}");
        }

        return csv.ToString();
    }

    /// <summary>
    /// 转义CSV字段值 / Escapes CSV field values
    /// </summary>
    /// <param name="value">要转义的值 / Value to escape</param>
    /// <returns>转义后的值 / Escaped value</returns>
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