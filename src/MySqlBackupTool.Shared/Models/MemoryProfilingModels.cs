using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// 备份操作的完整内存分析配置文件
/// Complete memory profile for a backup operation
/// </summary>
public class MemoryProfile
{
    /// <summary>
    /// 操作标识符
    /// Operation identifier
    /// </summary>
    public string OperationId { get; set; } = string.Empty;
    
    /// <summary>
    /// 操作类型
    /// Operation type
    /// </summary>
    public string OperationType { get; set; } = string.Empty;
    
    /// <summary>
    /// 开始时间
    /// Start time
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// 结束时间
    /// End time
    /// </summary>
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// 持续时间
    /// Duration
    /// </summary>
    public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? DateTime.UtcNow.Subtract(StartTime);
    
    /// <summary>
    /// 内存快照列表
    /// List of memory snapshots
    /// </summary>
    public List<MemorySnapshot> Snapshots { get; set; } = new();
    
    /// <summary>
    /// 内存统计信息
    /// Memory statistics
    /// </summary>
    public MemoryStatistics Statistics { get; set; } = new();
    
    /// <summary>
    /// 垃圾回收事件列表
    /// List of garbage collection events
    /// </summary>
    public List<GarbageCollectionEvent> GCEvents { get; set; } = new();
    
    /// <summary>
    /// 获取操作期间的峰值内存使用量
    /// Gets the peak memory usage during the operation
    /// </summary>
    public long PeakMemoryUsage => Snapshots.Any() ? Snapshots.Max(s => s.WorkingSet) : 0;
    
    /// <summary>
    /// 获取操作开始时的内存使用量
    /// Gets the memory usage at the start of the operation
    /// </summary>
    public long InitialMemoryUsage => Snapshots.FirstOrDefault()?.WorkingSet ?? 0;
    
    /// <summary>
    /// 获取操作结束时的内存使用量
    /// Gets the memory usage at the end of the operation
    /// </summary>
    public long FinalMemoryUsage => Snapshots.LastOrDefault()?.WorkingSet ?? 0;
    
    /// <summary>
    /// 获取操作期间分配的总内存量
    /// Gets the total memory allocated during the operation
    /// </summary>
    public long TotalMemoryAllocated => FinalMemoryUsage - InitialMemoryUsage;
}

/// <summary>
/// 特定时间点的内存快照
/// Memory snapshot at a specific point in time
/// </summary>
public class MemorySnapshot
{
    /// <summary>
    /// 时间戳，默认为当前UTC时间
    /// Timestamp, defaults to current UTC time
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 阶段名称
    /// Phase name
    /// </summary>
    public string Phase { get; set; } = string.Empty;
    
    /// <summary>
    /// 附加信息
    /// Additional information
    /// </summary>
    public string? AdditionalInfo { get; set; }
    
    /// <summary>
    /// 进程内存指标 - 工作集大小
    /// Process memory metrics - Working set size
    /// </summary>
    public long WorkingSet { get; set; }
    
    /// <summary>
    /// 私有内存大小
    /// Private memory size
    /// </summary>
    public long PrivateMemorySize { get; set; }
    
    /// <summary>
    /// 虚拟内存大小
    /// Virtual memory size
    /// </summary>
    public long VirtualMemorySize { get; set; }
    
    /// <summary>
    /// 分页内存大小
    /// Paged memory size
    /// </summary>
    public long PagedMemorySize { get; set; }
    
    /// <summary>
    /// 非分页系统内存大小
    /// Non-paged system memory size
    /// </summary>
    public long NonPagedSystemMemorySize { get; set; }
    
    /// <summary>
    /// 分页系统内存大小
    /// Paged system memory size
    /// </summary>
    public long PagedSystemMemorySize { get; set; }
    
    /// <summary>
    /// .NET GC指标 - 第0代垃圾回收次数
    /// .NET GC metrics - Generation 0 collections
    /// </summary>
    public long Gen0Collections { get; set; }
    
    /// <summary>
    /// 第1代垃圾回收次数
    /// Generation 1 collections
    /// </summary>
    public long Gen1Collections { get; set; }
    
    /// <summary>
    /// 第2代垃圾回收次数
    /// Generation 2 collections
    /// </summary>
    public long Gen2Collections { get; set; }
    
    /// <summary>
    /// 总内存使用量
    /// Total memory usage
    /// </summary>
    public long TotalMemory { get; set; }
    
    /// <summary>
    /// 第0代堆大小
    /// Generation 0 heap size
    /// </summary>
    public long Gen0HeapSize { get; set; }
    
    /// <summary>
    /// 第1代堆大小
    /// Generation 1 heap size
    /// </summary>
    public long Gen1HeapSize { get; set; }
    
    /// <summary>
    /// 第2代堆大小
    /// Generation 2 heap size
    /// </summary>
    public long Gen2HeapSize { get; set; }
    
    /// <summary>
    /// 大对象堆大小
    /// Large object heap size
    /// </summary>
    public long LargeObjectHeapSize { get; set; }
    
    /// <summary>
    /// 系统内存指标 - 可用物理内存
    /// System memory metrics - Available physical memory
    /// </summary>
    public long AvailablePhysicalMemory { get; set; }
    
    /// <summary>
    /// 总物理内存
    /// Total physical memory
    /// </summary>
    public long TotalPhysicalMemory { get; set; }
    
    /// <summary>
    /// 内存使用百分比
    /// Memory usage percentage
    /// </summary>
    public double MemoryUsagePercentage => TotalPhysicalMemory > 0 ? 
        (double)(TotalPhysicalMemory - AvailablePhysicalMemory) / TotalPhysicalMemory * 100 : 0;
    
    /// <summary>
    /// 获取格式化的内存大小字符串
    /// Gets formatted memory size strings
    /// </summary>
    public string GetFormattedWorkingSet() => FormatBytes(WorkingSet);
    public string GetFormattedPrivateMemory() => FormatBytes(PrivateMemorySize);
    public string GetFormattedVirtualMemory() => FormatBytes(VirtualMemorySize);
    public string GetFormattedTotalMemory() => FormatBytes(TotalMemory);
    
    /// <summary>
    /// 将字节数格式化为可读的大小字符串
    /// Formats bytes into a human-readable size string
    /// </summary>
    /// <param name="bytes">字节数 / Number of bytes</param>
    /// <returns>格式化的大小字符串 / Formatted size string</returns>
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
/// Statistical analysis of memory usage during an operation
/// </summary>
public class MemoryStatistics
{
    public long PeakWorkingSet { get; set; }
    public long AverageWorkingSet { get; set; }
    public long MinimumWorkingSet { get; set; }
    public long MemoryGrowth { get; set; }
    public double MemoryGrowthRate { get; set; } // bytes per second
    
    public long PeakManagedMemory { get; set; }
    public long AverageManagedMemory { get; set; }
    public long MinimumManagedMemory { get; set; }
    public long ManagedMemoryGrowth { get; set; }
    
    public int TotalGCCollections { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public double GCPressure { get; set; } // collections per minute
    
    public TimeSpan ProfileDuration { get; set; }
    public int SnapshotCount { get; set; }
    public TimeSpan AverageSnapshotInterval { get; set; }
    
    /// <summary>
    /// Gets formatted memory statistics
    /// </summary>
    public string GetFormattedPeakWorkingSet() => FormatBytes(PeakWorkingSet);
    public string GetFormattedAverageWorkingSet() => FormatBytes(AverageWorkingSet);
    public string GetFormattedMemoryGrowth() => FormatBytes(MemoryGrowth);
    public string GetFormattedPeakManagedMemory() => FormatBytes(PeakManagedMemory);
    
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
/// Garbage collection event during profiling
/// </summary>
public class GarbageCollectionEvent
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int Generation { get; set; }
    public bool WasForced { get; set; }
    public long MemoryBeforeCollection { get; set; }
    public long MemoryAfterCollection { get; set; }
    public long MemoryFreed => MemoryBeforeCollection - MemoryAfterCollection;
    public TimeSpan Duration { get; set; }
    public string Reason { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets formatted memory amounts
    /// </summary>
    public string GetFormattedMemoryFreed() => FormatBytes(MemoryFreed);
    public string GetFormattedMemoryBefore() => FormatBytes(MemoryBeforeCollection);
    public string GetFormattedMemoryAfter() => FormatBytes(MemoryAfterCollection);
    
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
/// Memory optimization recommendation
/// </summary>
public class MemoryRecommendation
{
    public MemoryRecommendationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public MemoryRecommendationPriority Priority { get; set; }
    public string Phase { get; set; } = string.Empty;
    public long EstimatedMemorySavings { get; set; }
    public string ActionRequired { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets formatted memory savings
    /// </summary>
    public string GetFormattedMemorySavings() => FormatBytes(EstimatedMemorySavings);
    
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
/// Types of memory recommendations
/// </summary>
public enum MemoryRecommendationType
{
    ReduceBufferSize,
    IncreaseGCFrequency,
    OptimizeChunkSize,
    ReduceParallelism,
    StreamingOptimization,
    CacheOptimization,
    MemoryLeakDetection,
    GCTuning
}

/// <summary>
/// Priority levels for memory recommendations
/// </summary>
public enum MemoryRecommendationPriority
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Configuration for memory profiling
/// </summary>
public class MemoryProfilingConfig
{
    /// <summary>
    /// Interval between automatic memory snapshots
    /// </summary>
    public TimeSpan SnapshotInterval { get; set; } = TimeSpan.FromSeconds(5);
    
    /// <summary>
    /// Whether to automatically take snapshots at regular intervals
    /// </summary>
    public bool AutomaticSnapshots { get; set; } = true;
    
    /// <summary>
    /// Whether to force garbage collection during profiling for more accurate measurements
    /// </summary>
    public bool ForceGCDuringProfiling { get; set; } = false;
    
    /// <summary>
    /// Maximum number of snapshots to keep in memory
    /// </summary>
    public int MaxSnapshots { get; set; } = 1000;
    
    /// <summary>
    /// Whether to collect detailed GC information
    /// </summary>
    public bool CollectGCDetails { get; set; } = true;
    
    /// <summary>
    /// Whether to collect system memory information
    /// </summary>
    public bool CollectSystemMemoryInfo { get; set; } = true;
    
    /// <summary>
    /// Memory threshold (in bytes) above which warnings are generated
    /// </summary>
    public long MemoryWarningThreshold { get; set; } = 1024 * 1024 * 1024; // 1 GB
    
    /// <summary>
    /// Memory threshold (in bytes) above which critical alerts are generated
    /// </summary>
    public long MemoryCriticalThreshold { get; set; } = 2L * 1024 * 1024 * 1024; // 2 GB
}