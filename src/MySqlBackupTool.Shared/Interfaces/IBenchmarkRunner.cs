using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 运行性能基准测试的接口
/// Interface for running performance benchmarks
/// </summary>
public interface IBenchmarkRunner
{
    /// <summary>
    /// 运行单个基准测试
    /// Runs a single benchmark test
    /// </summary>
    /// <param name="benchmarkName">基准测试名称 / Name of the benchmark</param>
    /// <param name="operationType">被测试的操作类型 / Type of operation being benchmarked</param>
    /// <param name="testAction">要测试的操作 / Action to benchmark</param>
    /// <param name="config">基准测试配置 / Benchmark configuration</param>
    /// <returns>基准测试结果 / Benchmark result</returns>
    Task<BenchmarkResult> RunBenchmarkAsync(string benchmarkName, string operationType, 
        Func<CancellationToken, Task<long>> testAction, BenchmarkConfig? config = null);

    /// <summary>
    /// 运行包含多次迭代的基准测试套件
    /// Runs a benchmark suite with multiple iterations
    /// </summary>
    /// <param name="suiteName">基准测试套件名称 / Name of the benchmark suite</param>
    /// <param name="benchmarks">基准测试名称到测试操作的字典 / Dictionary of benchmark name to test action</param>
    /// <param name="config">基准测试配置 / Benchmark configuration</param>
    /// <returns>完整的基准测试套件结果 / Complete benchmark suite results</returns>
    Task<BenchmarkSuite> RunBenchmarkSuiteAsync(string suiteName, 
        Dictionary<string, Func<CancellationToken, Task<long>>> benchmarks, BenchmarkConfig? config = null);

    /// <summary>
    /// 比较两个基准测试结果
    /// Compares two benchmark results
    /// </summary>
    /// <param name="baseline">基线基准测试结果 / Baseline benchmark result</param>
    /// <param name="comparison">比较基准测试结果 / Comparison benchmark result</param>
    /// <returns>基准测试比较分析 / Benchmark comparison analysis</returns>
    BenchmarkComparison CompareBenchmarks(BenchmarkResult baseline, BenchmarkResult comparison);

    /// <summary>
    /// 根据性能阈值验证基准测试结果
    /// Validates benchmark results against performance thresholds
    /// </summary>
    /// <param name="result">要验证的基准测试结果 / Benchmark result to validate</param>
    /// <param name="thresholds">性能阈值 / Performance thresholds</param>
    /// <returns>阈值违规列表 / List of threshold violations</returns>
    List<string> ValidatePerformance(BenchmarkResult result, PerformanceThresholds thresholds);

    /// <summary>
    /// 从基准测试结果生成性能报告
    /// Generates a performance report from benchmark results
    /// </summary>
    /// <param name="suite">基准测试套件结果 / Benchmark suite results</param>
    /// <param name="format">报告格式（HTML、JSON、CSV）/ Report format (HTML, JSON, CSV)</param>
    /// <returns>格式化的性能报告 / Formatted performance report</returns>
    Task<string> GenerateReportAsync(BenchmarkSuite suite, string format = "HTML");
}