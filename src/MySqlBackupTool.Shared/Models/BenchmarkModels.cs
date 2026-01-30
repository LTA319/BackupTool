using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// 特定操作的性能基准测试结果
/// 包含详细的性能指标和系统资源使用情况
/// </summary>
public class BenchmarkResult
{
    /// <summary>
    /// 基准测试名称
    /// </summary>
    public string BenchmarkName { get; set; } = string.Empty;
    
    /// <summary>
    /// 操作类型
    /// </summary>
    public string OperationType { get; set; } = string.Empty;
    
    /// <summary>
    /// 测试开始时间
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// 测试结束时间
    /// </summary>
    public DateTime EndTime { get; set; }
    
    /// <summary>
    /// 测试持续时间
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;
    
    // 性能指标
    
    /// <summary>
    /// 处理的字节数
    /// </summary>
    public long BytesProcessed { get; set; }
    
    /// <summary>
    /// 吞吐量（MB/秒）
    /// 根据处理字节数和持续时间计算
    /// </summary>
    public double ThroughputMBps => BytesProcessed > 0 && Duration.TotalSeconds > 0 
        ? (BytesProcessed / (1024.0 * 1024.0)) / Duration.TotalSeconds 
        : 0;
    
    // 内存指标
    
    /// <summary>
    /// 峰值内存使用量（字节）
    /// </summary>
    public long PeakMemoryUsage { get; set; }
    
    /// <summary>
    /// 平均内存使用量（字节）
    /// </summary>
    public long AverageMemoryUsage { get; set; }
    
    /// <summary>
    /// 内存增长量（字节）
    /// </summary>
    public long MemoryGrowth { get; set; }
    
    // 系统指标
    
    /// <summary>
    /// CPU使用率百分比
    /// </summary>
    public double CpuUsagePercent { get; set; }
    
    /// <summary>
    /// 线程数量
    /// </summary>
    public int ThreadCount { get; set; }
    
    /// <summary>
    /// 磁盘IO字节数
    /// </summary>
    public long DiskIOBytes { get; set; }
    
    /// <summary>
    /// 网络IO字节数
    /// </summary>
    public long NetworkIOBytes { get; set; }
    
    // 质量指标
    
    /// <summary>
    /// 测试是否成功
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// 错误消息（如果测试失败）
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 压缩比率
    /// </summary>
    public double CompressionRatio { get; set; }
    
    /// <summary>
    /// 校验和验证结果
    /// </summary>
    public string ChecksumValidation { get; set; } = string.Empty;
    
    // 附加上下文
    
    /// <summary>
    /// 附加指标字典
    /// 可以存储任何额外的测试指标
    /// </summary>
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
    
    /// <summary>
    /// 测试环境描述
    /// </summary>
    public string Environment { get; set; } = string.Empty;
    
    /// <summary>
    /// 测试配置描述
    /// </summary>
    public string TestConfiguration { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets formatted performance metrics
    /// </summary>
    public string GetFormattedThroughput() => $"{ThroughputMBps:F2} MB/s";
    public string GetFormattedDuration() => Duration.TotalMilliseconds < 1000 
        ? $"{Duration.TotalMilliseconds:F0} ms" 
        : $"{Duration.TotalSeconds:F1} s";
    public string GetFormattedBytesProcessed() => FormatBytes(BytesProcessed);
    public string GetFormattedPeakMemory() => FormatBytes(PeakMemoryUsage);
    
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
}

/// <summary>
/// Collection of benchmark results for comparison and analysis
/// </summary>
public class BenchmarkSuite
{
    public string SuiteName { get; set; } = string.Empty;
    public DateTime ExecutionTime { get; set; } = DateTime.UtcNow;
    public List<BenchmarkResult> Results { get; set; } = new();
    public BenchmarkEnvironment Environment { get; set; } = new();
    public TimeSpan TotalExecutionTime { get; set; }
    
    /// <summary>
    /// Gets benchmark results by operation type
    /// </summary>
    public IEnumerable<BenchmarkResult> GetResultsByType(string operationType)
    {
        return Results.Where(r => r.OperationType.Equals(operationType, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Gets the fastest result for a specific operation type
    /// </summary>
    public BenchmarkResult? GetFastestResult(string operationType)
    {
        return GetResultsByType(operationType)
            .Where(r => r.Success)
            .OrderBy(r => r.Duration)
            .FirstOrDefault();
    }
    
    /// <summary>
    /// Gets the highest throughput result for a specific operation type
    /// </summary>
    public BenchmarkResult? GetHighestThroughputResult(string operationType)
    {
        return GetResultsByType(operationType)
            .Where(r => r.Success && r.ThroughputMBps > 0)
            .OrderByDescending(r => r.ThroughputMBps)
            .FirstOrDefault();
    }
    
    /// <summary>
    /// Gets average performance metrics for an operation type
    /// </summary>
    public BenchmarkSummary GetSummary(string operationType)
    {
        var results = GetResultsByType(operationType).Where(r => r.Success).ToList();
        
        if (!results.Any())
            return new BenchmarkSummary { OperationType = operationType };
        
        return new BenchmarkSummary
        {
            OperationType = operationType,
            TestCount = results.Count,
            AverageDuration = TimeSpan.FromTicks((long)results.Average(r => r.Duration.Ticks)),
            FastestDuration = results.Min(r => r.Duration),
            SlowestDuration = results.Max(r => r.Duration),
            AverageThroughput = results.Where(r => r.ThroughputMBps > 0).DefaultIfEmpty().Average(r => r?.ThroughputMBps ?? 0),
            PeakThroughput = results.Where(r => r.ThroughputMBps > 0).DefaultIfEmpty().Max(r => r?.ThroughputMBps ?? 0),
            AverageMemoryUsage = (long)results.Average(r => r.AverageMemoryUsage),
            PeakMemoryUsage = results.Max(r => r.PeakMemoryUsage),
            TotalBytesProcessed = results.Sum(r => r.BytesProcessed),
            SuccessRate = (double)results.Count / Results.Count(r => r.OperationType.Equals(operationType, StringComparison.OrdinalIgnoreCase)) * 100
        };
    }
}

/// <summary>
/// Summary statistics for a benchmark operation type
/// </summary>
public class BenchmarkSummary
{
    public string OperationType { get; set; } = string.Empty;
    public int TestCount { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public TimeSpan FastestDuration { get; set; }
    public TimeSpan SlowestDuration { get; set; }
    public double AverageThroughput { get; set; }
    public double PeakThroughput { get; set; }
    public long AverageMemoryUsage { get; set; }
    public long PeakMemoryUsage { get; set; }
    public long TotalBytesProcessed { get; set; }
    public double SuccessRate { get; set; }
    
    /// <summary>
    /// Gets formatted summary metrics
    /// </summary>
    public string GetFormattedAverageDuration() => AverageDuration.TotalMilliseconds < 1000 
        ? $"{AverageDuration.TotalMilliseconds:F0} ms" 
        : $"{AverageDuration.TotalSeconds:F1} s";
    
    public string GetFormattedAverageThroughput() => $"{AverageThroughput:F2} MB/s";
    public string GetFormattedPeakThroughput() => $"{PeakThroughput:F2} MB/s";
    public string GetFormattedAverageMemory() => FormatBytes(AverageMemoryUsage);
    public string GetFormattedPeakMemory() => FormatBytes(PeakMemoryUsage);
    public string GetFormattedTotalBytes() => FormatBytes(TotalBytesProcessed);
    
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
}

/// <summary>
/// System environment information for benchmark context
/// </summary>
public class BenchmarkEnvironment
{
    public string MachineName { get; set; } = Environment.MachineName;
    public string OperatingSystem { get; set; } = Environment.OSVersion.ToString();
    public string ProcessorArchitecture { get; set; } = Environment.Is64BitProcess ? "x64" : "x86";
    public int ProcessorCount { get; set; } = Environment.ProcessorCount;
    public long TotalPhysicalMemory { get; set; }
    public long AvailablePhysicalMemory { get; set; }
    public string DotNetVersion { get; set; } = Environment.Version.ToString();
    public string WorkingDirectory { get; set; } = Environment.CurrentDirectory;
    public bool IsDebugBuild { get; set; }
    
    /// <summary>
    /// Gets formatted environment information
    /// </summary>
    public string GetFormattedTotalMemory() => FormatBytes(TotalPhysicalMemory);
    public string GetFormattedAvailableMemory() => FormatBytes(AvailablePhysicalMemory);
    
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
}

/// <summary>
/// Configuration for benchmark execution
/// </summary>
public class BenchmarkConfig
{
    public int WarmupIterations { get; set; } = 3;
    public int BenchmarkIterations { get; set; } = 10;
    public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.FromMinutes(30);
    public bool CollectMemoryMetrics { get; set; } = true;
    public bool CollectSystemMetrics { get; set; } = true;
    public bool ForceGarbageCollection { get; set; } = true;
    public List<long> TestFileSizes { get; set; } = new() 
    { 
        1024 * 1024,           // 1 MB
        10 * 1024 * 1024,      // 10 MB
        100 * 1024 * 1024,     // 100 MB
        500 * 1024 * 1024      // 500 MB
    };
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}

/// <summary>
/// Benchmark comparison between two results
/// </summary>
public class BenchmarkComparison
{
    public BenchmarkResult BaselineResult { get; set; } = new();
    public BenchmarkResult ComparisonResult { get; set; } = new();
    
    public double DurationImprovement => BaselineResult.Duration.TotalMilliseconds > 0 
        ? (BaselineResult.Duration.TotalMilliseconds - ComparisonResult.Duration.TotalMilliseconds) / BaselineResult.Duration.TotalMilliseconds * 100
        : 0;
    
    public double ThroughputImprovement => BaselineResult.ThroughputMBps > 0 
        ? (ComparisonResult.ThroughputMBps - BaselineResult.ThroughputMBps) / BaselineResult.ThroughputMBps * 100
        : 0;
    
    public double MemoryImprovement => BaselineResult.PeakMemoryUsage > 0 
        ? (BaselineResult.PeakMemoryUsage - ComparisonResult.PeakMemoryUsage) / (double)BaselineResult.PeakMemoryUsage * 100
        : 0;
    
    public string GetFormattedDurationImprovement() => $"{DurationImprovement:+0.0;-0.0;0.0}%";
    public string GetFormattedThroughputImprovement() => $"{ThroughputImprovement:+0.0;-0.0;0.0}%";
    public string GetFormattedMemoryImprovement() => $"{MemoryImprovement:+0.0;-0.0;0.0}%";
}

/// <summary>
/// Performance threshold definitions for benchmark validation
/// </summary>
public class PerformanceThresholds
{
    public double MinThroughputMBps { get; set; } = 10.0; // Minimum acceptable throughput
    public TimeSpan MaxDurationForSmallFiles { get; set; } = TimeSpan.FromSeconds(5); // Max time for files < 10MB
    public TimeSpan MaxDurationForLargeFiles { get; set; } = TimeSpan.FromMinutes(5); // Max time for files > 100MB
    public long MaxMemoryUsageMB { get; set; } = 1024; // Max memory usage in MB
    public double MaxCpuUsagePercent { get; set; } = 80.0; // Max CPU usage percentage
    public double MinCompressionRatio { get; set; } = 0.3; // Minimum compression ratio (30% reduction)
    public double MinSuccessRate { get; set; } = 95.0; // Minimum success rate percentage
    
    /// <summary>
    /// Validates a benchmark result against thresholds
    /// </summary>
    public List<string> ValidateResult(BenchmarkResult result)
    {
        var violations = new List<string>();
        
        if (result.ThroughputMBps > 0 && result.ThroughputMBps < MinThroughputMBps)
        {
            violations.Add($"Throughput {result.GetFormattedThroughput()} is below minimum {MinThroughputMBps:F1} MB/s");
        }
        
        var maxDuration = result.BytesProcessed < 10 * 1024 * 1024 ? MaxDurationForSmallFiles : MaxDurationForLargeFiles;
        if (result.Duration > maxDuration)
        {
            violations.Add($"Duration {result.GetFormattedDuration()} exceeds maximum {maxDuration.TotalSeconds:F1}s");
        }
        
        if (result.PeakMemoryUsage > MaxMemoryUsageMB * 1024 * 1024)
        {
            violations.Add($"Peak memory {result.GetFormattedPeakMemory()} exceeds maximum {MaxMemoryUsageMB} MB");
        }
        
        if (result.CpuUsagePercent > MaxCpuUsagePercent)
        {
            violations.Add($"CPU usage {result.CpuUsagePercent:F1}% exceeds maximum {MaxCpuUsagePercent:F1}%");
        }
        
        if (result.CompressionRatio > 0 && result.CompressionRatio < MinCompressionRatio)
        {
            violations.Add($"Compression ratio {result.CompressionRatio:F1}% is below minimum {MinCompressionRatio:F1}%");
        }
        
        return violations;
    }
}