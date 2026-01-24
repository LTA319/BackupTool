using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Interface for running performance benchmarks
/// </summary>
public interface IBenchmarkRunner
{
    /// <summary>
    /// Runs a single benchmark test
    /// </summary>
    /// <param name="benchmarkName">Name of the benchmark</param>
    /// <param name="operationType">Type of operation being benchmarked</param>
    /// <param name="testAction">Action to benchmark</param>
    /// <param name="config">Benchmark configuration</param>
    /// <returns>Benchmark result</returns>
    Task<BenchmarkResult> RunBenchmarkAsync(string benchmarkName, string operationType, 
        Func<CancellationToken, Task<long>> testAction, BenchmarkConfig? config = null);

    /// <summary>
    /// Runs a benchmark suite with multiple iterations
    /// </summary>
    /// <param name="suiteName">Name of the benchmark suite</param>
    /// <param name="benchmarks">Dictionary of benchmark name to test action</param>
    /// <param name="config">Benchmark configuration</param>
    /// <returns>Complete benchmark suite results</returns>
    Task<BenchmarkSuite> RunBenchmarkSuiteAsync(string suiteName, 
        Dictionary<string, Func<CancellationToken, Task<long>>> benchmarks, BenchmarkConfig? config = null);

    /// <summary>
    /// Compares two benchmark results
    /// </summary>
    /// <param name="baseline">Baseline benchmark result</param>
    /// <param name="comparison">Comparison benchmark result</param>
    /// <returns>Benchmark comparison analysis</returns>
    BenchmarkComparison CompareBenchmarks(BenchmarkResult baseline, BenchmarkResult comparison);

    /// <summary>
    /// Validates benchmark results against performance thresholds
    /// </summary>
    /// <param name="result">Benchmark result to validate</param>
    /// <param name="thresholds">Performance thresholds</param>
    /// <returns>List of threshold violations</returns>
    List<string> ValidatePerformance(BenchmarkResult result, PerformanceThresholds thresholds);

    /// <summary>
    /// Generates a performance report from benchmark results
    /// </summary>
    /// <param name="suite">Benchmark suite results</param>
    /// <param name="format">Report format (HTML, JSON, CSV)</param>
    /// <returns>Formatted performance report</returns>
    Task<string> GenerateReportAsync(BenchmarkSuite suite, string format = "HTML");
}