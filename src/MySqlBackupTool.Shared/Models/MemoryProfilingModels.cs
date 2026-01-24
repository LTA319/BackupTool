using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// Complete memory profile for a backup operation
/// </summary>
public class MemoryProfile
{
    public string OperationId { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? DateTime.UtcNow.Subtract(StartTime);
    
    public List<MemorySnapshot> Snapshots { get; set; } = new();
    public MemoryStatistics Statistics { get; set; } = new();
    public List<GarbageCollectionEvent> GCEvents { get; set; } = new();
    
    /// <summary>
    /// Gets the peak memory usage during the operation
    /// </summary>
    public long PeakMemoryUsage => Snapshots.Any() ? Snapshots.Max(s => s.WorkingSet) : 0;
    
    /// <summary>
    /// Gets the memory usage at the start of the operation
    /// </summary>
    public long InitialMemoryUsage => Snapshots.FirstOrDefault()?.WorkingSet ?? 0;
    
    /// <summary>
    /// Gets the memory usage at the end of the operation
    /// </summary>
    public long FinalMemoryUsage => Snapshots.LastOrDefault()?.WorkingSet ?? 0;
    
    /// <summary>
    /// Gets the total memory allocated during the operation
    /// </summary>
    public long TotalMemoryAllocated => FinalMemoryUsage - InitialMemoryUsage;
}

/// <summary>
/// Memory snapshot at a specific point in time
/// </summary>
public class MemorySnapshot
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Phase { get; set; } = string.Empty;
    public string? AdditionalInfo { get; set; }
    
    // Process memory metrics
    public long WorkingSet { get; set; }
    public long PrivateMemorySize { get; set; }
    public long VirtualMemorySize { get; set; }
    public long PagedMemorySize { get; set; }
    public long NonPagedSystemMemorySize { get; set; }
    public long PagedSystemMemorySize { get; set; }
    
    // .NET GC metrics
    public long Gen0Collections { get; set; }
    public long Gen1Collections { get; set; }
    public long Gen2Collections { get; set; }
    public long TotalMemory { get; set; }
    public long Gen0HeapSize { get; set; }
    public long Gen1HeapSize { get; set; }
    public long Gen2HeapSize { get; set; }
    public long LargeObjectHeapSize { get; set; }
    
    // System memory metrics
    public long AvailablePhysicalMemory { get; set; }
    public long TotalPhysicalMemory { get; set; }
    public double MemoryUsagePercentage => TotalPhysicalMemory > 0 ? 
        (double)(TotalPhysicalMemory - AvailablePhysicalMemory) / TotalPhysicalMemory * 100 : 0;
    
    /// <summary>
    /// Gets formatted memory size strings
    /// </summary>
    public string GetFormattedWorkingSet() => FormatBytes(WorkingSet);
    public string GetFormattedPrivateMemory() => FormatBytes(PrivateMemorySize);
    public string GetFormattedVirtualMemory() => FormatBytes(VirtualMemorySize);
    public string GetFormattedTotalMemory() => FormatBytes(TotalMemory);
    
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