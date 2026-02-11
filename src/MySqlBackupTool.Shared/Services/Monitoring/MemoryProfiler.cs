using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 内存分析器，用于分析备份操作期间的内存使用情况 / Memory profiler for analyzing memory usage during backup operations
/// 提供内存快照、性能监控和资源使用统计功能 / Provides memory snapshots, performance monitoring and resource usage statistics
/// </summary>
public class MemoryProfiler : IMemoryProfiler, IDisposable
{
    private readonly ILogger<MemoryProfiler> _logger;
    private readonly MemoryProfilingConfig _config;
    private readonly ConcurrentDictionary<string, MemoryProfile> _activeProfiles;
    private readonly ConcurrentDictionary<string, Timer> _snapshotTimers;
    private readonly Process _currentProcess;
    private bool _disposed;

    /// <summary>
    /// 初始化内存分析器 / Initialize memory profiler
    /// </summary>
    /// <param name="logger">日志记录器 / Logger instance</param>
    /// <param name="config">内存分析配置（可选） / Memory profiling configuration (optional)</param>
    /// <exception cref="ArgumentNullException">当logger为null时抛出 / Thrown when logger is null</exception>
    public MemoryProfiler(ILogger<MemoryProfiler> logger, MemoryProfilingConfig? config = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? new MemoryProfilingConfig();
        _activeProfiles = new ConcurrentDictionary<string, MemoryProfile>();
        _snapshotTimers = new ConcurrentDictionary<string, Timer>();
        _currentProcess = Process.GetCurrentProcess();
        
        _logger.LogDebug("MemoryProfiler initialized with config: AutoSnapshots={AutoSnapshots}, Interval={Interval}",
            _config.AutomaticSnapshots, _config.SnapshotInterval);
    }

    /// <summary>
    /// 为备份操作开始内存分析 / Starts memory profiling for a backup operation
    /// </summary>
    /// <param name="operationId">操作ID / Operation ID</param>
    /// <param name="operationType">操作类型 / Operation type</param>
    /// <exception cref="ArgumentException">当操作ID或类型为空时抛出 / Thrown when operation ID or type is empty</exception>
    public void StartProfiling(string operationId, string operationType)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("Operation ID cannot be null or empty", nameof(operationId));
        
        if (string.IsNullOrWhiteSpace(operationType))
            throw new ArgumentException("Operation type cannot be null or empty", nameof(operationType));

        _logger.LogInformation("Starting memory profiling for operation {OperationId} of type {OperationType}", 
            operationId, operationType);

        var profile = new MemoryProfile
        {
            OperationId = operationId,
            OperationType = operationType,
            StartTime = DateTime.Now
        };

        _activeProfiles.TryAdd(operationId, profile);

        // 拍摄初始快照 / Take initial snapshot
        RecordSnapshot(operationId, "Start", "Initial memory state");

        // 如果配置了则开始自动快照 / Start automatic snapshots if configured
        if (_config.AutomaticSnapshots)
        {
            var timer = new Timer(
                callback: _ => RecordSnapshot(operationId, "Auto", "Automatic snapshot"),
                state: null,
                dueTime: _config.SnapshotInterval,
                period: _config.SnapshotInterval);

            _snapshotTimers.TryAdd(operationId, timer);
        }

        _logger.LogDebug("Memory profiling started for operation {OperationId}", operationId);
    }

    /// <summary>
    /// 在操作期间记录内存快照 / Records a memory snapshot during the operation
    /// </summary>
    /// <param name="operationId">操作ID / Operation ID</param>
    /// <param name="phase">阶段名称 / Phase name</param>
    /// <param name="additionalInfo">附加信息 / Additional information</param>
    public void RecordSnapshot(string operationId, string phase, string? additionalInfo = null)
    {
        if (!_activeProfiles.TryGetValue(operationId, out var profile))
        {
            _logger.LogWarning("Attempted to record snapshot for unknown operation {OperationId}", operationId);
            return;
        }

        try
        {
            var snapshot = CreateMemorySnapshot(phase, additionalInfo);
            
            lock (profile.Snapshots)
            {
                profile.Snapshots.Add(snapshot);
                
                // 限制快照数量以防止内存问题 / Limit snapshots to prevent memory issues
                if (profile.Snapshots.Count > _config.MaxSnapshots)
                {
                    profile.Snapshots.RemoveAt(0);
                    _logger.LogDebug("Removed oldest snapshot for operation {OperationId} (limit: {MaxSnapshots})", 
                        operationId, _config.MaxSnapshots);
                }
            }

            // 检查内存阈值 / Check memory thresholds
            CheckMemoryThresholds(operationId, snapshot);

            _logger.LogDebug("Recorded memory snapshot for operation {OperationId}, phase {Phase}: {WorkingSet}",
                operationId, phase, snapshot.GetFormattedWorkingSet());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record memory snapshot for operation {OperationId}", operationId);
        }
    }

    /// <summary>
    /// 停止分析并返回完整的内存分析结果 / Stops profiling and returns the complete memory profile
    /// </summary>
    /// <param name="operationId">操作ID / Operation ID</param>
    /// <returns>内存分析结果 / Memory profile result</returns>
    public MemoryProfile StopProfiling(string operationId)
    {
        if (!_activeProfiles.TryRemove(operationId, out var profile))
        {
            _logger.LogWarning("Attempted to stop profiling for unknown operation {OperationId}", operationId);
            return new MemoryProfile { OperationId = operationId };
        }

        _logger.LogInformation("Stopping memory profiling for operation {OperationId}", operationId);

        // 停止自动快照 / Stop automatic snapshots
        if (_snapshotTimers.TryRemove(operationId, out var timer))
        {
            timer.Dispose();
        }

        // 拍摄最终快照 / Take final snapshot
        RecordSnapshot(operationId, "End", "Final memory state");
        profile.EndTime = DateTime.Now;

        // 计算统计信息 / Calculate statistics
        profile.Statistics = CalculateStatistics(profile);

        _logger.LogInformation("Memory profiling completed for operation {OperationId}. Duration: {Duration}, Peak: {Peak}, Growth: {Growth}",
            operationId, profile.Duration, profile.Statistics.GetFormattedPeakWorkingSet(), 
            profile.Statistics.GetFormattedMemoryGrowth());

        return profile;
    }

    /// <summary>
    /// 获取正在进行的操作的当前内存分析结果 / Gets the current memory profile for an ongoing operation
    /// </summary>
    /// <param name="operationId">操作ID / Operation ID</param>
    /// <returns>当前内存分析结果或null / Current memory profile or null</returns>
    public MemoryProfile? GetCurrentProfile(string operationId)
    {
        if (_activeProfiles.TryGetValue(operationId, out var profile))
        {
            // 创建带有当前统计信息的副本 / Create a copy with current statistics
            var currentProfile = new MemoryProfile
            {
                OperationId = profile.OperationId,
                OperationType = profile.OperationType,
                StartTime = profile.StartTime,
                EndTime = null,
                Snapshots = new List<MemorySnapshot>(profile.Snapshots),
                GCEvents = new List<GarbageCollectionEvent>(profile.GCEvents)
            };
            
            currentProfile.Statistics = CalculateStatistics(currentProfile);
            return currentProfile;
        }

        return null;
    }

    /// <summary>
    /// 强制垃圾回收并记录影响 / Forces garbage collection and records the impact
    /// </summary>
    /// <param name="operationId">操作ID / Operation ID</param>
    /// <param name="generation">GC代数（可选） / GC generation (optional)</param>
    public void ForceGarbageCollection(string operationId, int? generation = null)
    {
        if (!_activeProfiles.TryGetValue(operationId, out var profile))
        {
            _logger.LogWarning("Attempted to force GC for unknown operation {OperationId}", operationId);
            return;
        }

        _logger.LogDebug("Forcing garbage collection for operation {OperationId}, generation {Generation}", 
            operationId, generation?.ToString() ?? "all");

        var beforeSnapshot = CreateMemorySnapshot("GC-Before", $"Before forced GC (gen {generation?.ToString() ?? "all"})");
        var startTime = DateTime.Now;

        try
        {
            if (generation.HasValue)
            {
                GC.Collect(generation.Value, GCCollectionMode.Forced, true);
            }
            else
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            var endTime = DateTime.Now;
            var afterSnapshot = CreateMemorySnapshot("GC-After", $"After forced GC (gen {generation?.ToString() ?? "all"})");

            var gcEvent = new GarbageCollectionEvent
            {
                Timestamp = startTime,
                Generation = generation ?? -1,
                WasForced = true,
                MemoryBeforeCollection = beforeSnapshot.TotalMemory,
                MemoryAfterCollection = afterSnapshot.TotalMemory,
                Duration = endTime - startTime,
                Reason = "Manual/Forced"
            };

            lock (profile.GCEvents)
            {
                profile.GCEvents.Add(gcEvent);
            }

            _logger.LogInformation("Forced GC completed for operation {OperationId}. Memory freed: {MemoryFreed}, Duration: {Duration}",
                operationId, gcEvent.GetFormattedMemoryFreed(), gcEvent.Duration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to force garbage collection for operation {OperationId}", operationId);
        }
    }

    /// <summary>
    /// 基于分析数据获取内存使用建议 / Gets memory usage recommendations based on profiling data
    /// </summary>
    /// <param name="profile">内存分析结果 / Memory profile</param>
    /// <returns>内存建议列表 / List of memory recommendations</returns>
    public List<MemoryRecommendation> GetRecommendations(MemoryProfile profile)
    {
        var recommendations = new List<MemoryRecommendation>();

        try
        {
            // 高内存使用建议 / High memory usage recommendation
            if (profile.Statistics.PeakWorkingSet > _config.MemoryCriticalThreshold)
            {
                recommendations.Add(new MemoryRecommendation
                {
                    Type = MemoryRecommendationType.ReduceBufferSize,
                    Title = "Critical Memory Usage Detected",
                    Description = $"Peak memory usage ({profile.Statistics.GetFormattedPeakWorkingSet()}) exceeded critical threshold ({FormatBytes(_config.MemoryCriticalThreshold)})",
                    Priority = MemoryRecommendationPriority.Critical,
                    EstimatedMemorySavings = profile.Statistics.PeakWorkingSet - _config.MemoryCriticalThreshold,
                    ActionRequired = "Reduce buffer sizes, implement streaming, or increase available memory"
                });
            }
            else if (profile.Statistics.PeakWorkingSet > _config.MemoryWarningThreshold)
            {
                recommendations.Add(new MemoryRecommendation
                {
                    Type = MemoryRecommendationType.OptimizeChunkSize,
                    Title = "High Memory Usage Warning",
                    Description = $"Peak memory usage ({profile.Statistics.GetFormattedPeakWorkingSet()}) exceeded warning threshold ({FormatBytes(_config.MemoryWarningThreshold)})",
                    Priority = MemoryRecommendationPriority.High,
                    EstimatedMemorySavings = profile.Statistics.PeakWorkingSet - _config.MemoryWarningThreshold,
                    ActionRequired = "Consider optimizing chunk sizes or implementing memory-efficient algorithms"
                });
            }

            // 高GC压力建议 / High GC pressure recommendation
            if (profile.Statistics.GCPressure > 10) // 每分钟超过10次回收 / More than 10 collections per minute
            {
                recommendations.Add(new MemoryRecommendation
                {
                    Type = MemoryRecommendationType.IncreaseGCFrequency,
                    Title = "High Garbage Collection Pressure",
                    Description = $"High GC activity detected ({profile.Statistics.GCPressure:F1} collections/min)",
                    Priority = MemoryRecommendationPriority.Medium,
                    EstimatedMemorySavings = profile.Statistics.ManagedMemoryGrowth / 4,
                    ActionRequired = "Optimize object allocation patterns or implement object pooling"
                });
            }

            // 内存增长率建议 / Memory growth rate recommendation
            if (profile.Statistics.MemoryGrowthRate > 50 * 1024 * 1024) // 每秒增长超过50MB / More than 50 MB/s growth
            {
                recommendations.Add(new MemoryRecommendation
                {
                    Type = MemoryRecommendationType.StreamingOptimization,
                    Title = "High Memory Growth Rate",
                    Description = $"Memory is growing rapidly ({FormatBytes((long)profile.Statistics.MemoryGrowthRate)}/s)",
                    Priority = MemoryRecommendationPriority.High,
                    EstimatedMemorySavings = (long)(profile.Statistics.MemoryGrowthRate * profile.Statistics.ProfileDuration.TotalSeconds / 2),
                    ActionRequired = "Implement streaming or reduce memory allocation rate"
                });
            }

            // 内存泄漏检测 / Memory leak detection
            if (profile.Statistics.MemoryGrowth > 0 && profile.Statistics.TotalGCCollections > 5)
            {
                var expectedMemoryAfterGC = profile.Statistics.MinimumWorkingSet * 1.2; // 预期20%开销 / 20% overhead expected
                if (profile.Statistics.AverageWorkingSet > expectedMemoryAfterGC)
                {
                    recommendations.Add(new MemoryRecommendation
                    {
                        Type = MemoryRecommendationType.MemoryLeakDetection,
                        Title = "Potential Memory Leak Detected",
                        Description = "Memory usage remains high despite garbage collection activity",
                        Priority = MemoryRecommendationPriority.High,
                        EstimatedMemorySavings = (long)(profile.Statistics.AverageWorkingSet - expectedMemoryAfterGC),
                        ActionRequired = "Investigate for memory leaks, unmanaged resources, or event handler leaks"
                    });
                }
            }

            _logger.LogDebug("Generated {Count} memory recommendations for operation {OperationId}", 
                recommendations.Count, profile.OperationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate memory recommendations for operation {OperationId}", profile.OperationId);
        }

        return recommendations;
    }

    /// <summary>
    /// 创建当前系统状态的内存快照 / Creates a memory snapshot with current system state
    /// </summary>
    /// <param name="phase">阶段名称 / Phase name</param>
    /// <param name="additionalInfo">附加信息 / Additional information</param>
    /// <returns>内存快照 / Memory snapshot</returns>
    private MemorySnapshot CreateMemorySnapshot(string phase, string? additionalInfo)
    {
        try
        {
            _currentProcess.Refresh();

            var snapshot = new MemorySnapshot
            {
                Timestamp = DateTime.Now,
                Phase = phase,
                AdditionalInfo = additionalInfo,
                
                // 进程内存指标 / Process memory metrics
                WorkingSet = _currentProcess.WorkingSet64,
                PrivateMemorySize = _currentProcess.PrivateMemorySize64,
                VirtualMemorySize = _currentProcess.VirtualMemorySize64,
                PagedMemorySize = _currentProcess.PagedMemorySize64,
                NonPagedSystemMemorySize = _currentProcess.NonpagedSystemMemorySize64,
                PagedSystemMemorySize = _currentProcess.PagedSystemMemorySize64,
                
                // .NET GC指标 / .NET GC metrics
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),
                TotalMemory = GC.GetTotalMemory(false)
            };

            // 如果可用则获取GC堆大小（.NET 6+） / Get GC heap sizes if available (.NET 6+)
            try
            {
                var gcInfo = GC.GetGCMemoryInfo();
                snapshot.Gen0HeapSize = gcInfo.GenerationInfo[0].SizeAfterBytes;
                snapshot.Gen1HeapSize = gcInfo.GenerationInfo[1].SizeAfterBytes;
                snapshot.Gen2HeapSize = gcInfo.GenerationInfo[2].SizeAfterBytes;
                snapshot.LargeObjectHeapSize = gcInfo.GenerationInfo[3].SizeAfterBytes;
            }
            catch
            {
                // 此.NET版本中堆大小信息不可用 / Heap size information not available in this .NET version
            }

            // 系统内存指标（如果配置） / System memory metrics (if configured)
            if (_config.CollectSystemMemoryInfo)
            {
                try
                {
                    var gcMemoryInfo = GC.GetGCMemoryInfo();
                    snapshot.TotalPhysicalMemory = gcMemoryInfo.TotalAvailableMemoryBytes;
                    
                    // For cross-platform compatibility, we'll estimate available memory
                    // This is a simplified approach - in production you might want to use platform-specific APIs
                    snapshot.AvailablePhysicalMemory = Math.Max(0, gcMemoryInfo.TotalAvailableMemoryBytes - snapshot.WorkingSet);
                }
                catch
                {
                    // System memory information not available
                    snapshot.AvailablePhysicalMemory = 0;
                    snapshot.TotalPhysicalMemory = 0;
                }
            }

            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create memory snapshot for phase {Phase}", phase);
            
            // Return minimal snapshot on error
            return new MemorySnapshot
            {
                Timestamp = DateTime.Now,
                Phase = phase,
                AdditionalInfo = $"Error: {ex.Message}",
                WorkingSet = 0,
                TotalMemory = GC.GetTotalMemory(false)
            };
        }
    }

    /// <summary>
    /// Calculates statistics from memory snapshots
    /// </summary>
    private MemoryStatistics CalculateStatistics(MemoryProfile profile)
    {
        var statistics = new MemoryStatistics();

        if (!profile.Snapshots.Any())
            return statistics;

        var snapshots = profile.Snapshots.OrderBy(s => s.Timestamp).ToList();
        var workingSets = snapshots.Select(s => s.WorkingSet).ToList();
        var managedMemory = snapshots.Select(s => s.TotalMemory).ToList();

        // Working set statistics
        statistics.PeakWorkingSet = workingSets.Max();
        statistics.AverageWorkingSet = (long)workingSets.Average();
        statistics.MinimumWorkingSet = workingSets.Min();
        statistics.MemoryGrowth = workingSets.Last() - workingSets.First();

        // Managed memory statistics
        statistics.PeakManagedMemory = managedMemory.Max();
        statistics.AverageManagedMemory = (long)managedMemory.Average();
        statistics.MinimumManagedMemory = managedMemory.Min();
        statistics.ManagedMemoryGrowth = managedMemory.Last() - managedMemory.First();

        // Time-based calculations
        statistics.ProfileDuration = profile.Duration;
        statistics.SnapshotCount = snapshots.Count;
        
        if (snapshots.Count > 1)
        {
            var totalInterval = snapshots.Last().Timestamp - snapshots.First().Timestamp;
            statistics.AverageSnapshotInterval = TimeSpan.FromTicks(totalInterval.Ticks / (snapshots.Count - 1));
            
            // Memory growth rate (bytes per second)
            if (totalInterval.TotalSeconds > 0)
            {
                statistics.MemoryGrowthRate = statistics.MemoryGrowth / totalInterval.TotalSeconds;
            }
        }

        // GC statistics
        if (snapshots.Count > 1)
        {
            var firstSnapshot = snapshots.First();
            var lastSnapshot = snapshots.Last();
            
            statistics.Gen0Collections = (int)(lastSnapshot.Gen0Collections - firstSnapshot.Gen0Collections);
            statistics.Gen1Collections = (int)(lastSnapshot.Gen1Collections - firstSnapshot.Gen1Collections);
            statistics.Gen2Collections = (int)(lastSnapshot.Gen2Collections - firstSnapshot.Gen2Collections);
            statistics.TotalGCCollections = statistics.Gen0Collections + statistics.Gen1Collections + statistics.Gen2Collections;
            
            // GC pressure (collections per minute)
            if (statistics.ProfileDuration.TotalMinutes > 0)
            {
                statistics.GCPressure = statistics.TotalGCCollections / statistics.ProfileDuration.TotalMinutes;
            }
        }

        return statistics;
    }

    /// <summary>
    /// Checks memory thresholds and logs warnings
    /// </summary>
    private void CheckMemoryThresholds(string operationId, MemorySnapshot snapshot)
    {
        if (snapshot.WorkingSet > _config.MemoryCriticalThreshold)
        {
            _logger.LogCritical("CRITICAL: Memory usage for operation {OperationId} exceeded critical threshold: {Usage} > {Threshold}",
                operationId, snapshot.GetFormattedWorkingSet(), FormatBytes(_config.MemoryCriticalThreshold));
        }
        else if (snapshot.WorkingSet > _config.MemoryWarningThreshold)
        {
            _logger.LogWarning("WARNING: Memory usage for operation {OperationId} exceeded warning threshold: {Usage} > {Threshold}",
                operationId, snapshot.GetFormattedWorkingSet(), FormatBytes(_config.MemoryWarningThreshold));
        }
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

    /// <summary>
    /// Disposes resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _logger.LogDebug("Disposing MemoryProfiler");

        // Stop all active profiling sessions
        foreach (var operationId in _activeProfiles.Keys.ToList())
        {
            try
            {
                StopProfiling(operationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping profiling for operation {OperationId} during disposal", operationId);
            }
        }

        // Dispose all timers
        foreach (var timer in _snapshotTimers.Values)
        {
            try
            {
                timer.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing snapshot timer");
            }
        }

        _snapshotTimers.Clear();
        _activeProfiles.Clear();
        _currentProcess?.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}