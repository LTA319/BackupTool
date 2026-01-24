using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;

namespace MySqlBackupTool.Examples;

/// <summary>
/// Example demonstrating memory profiling during backup operations
/// </summary>
public class MemoryProfilingExample
{
    public static async Task Main(string[] args)
    {
        // Setup dependency injection
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        services.AddSharedServices("Data Source=:memory:");

        using var serviceProvider = services.BuildServiceProvider();
        
        // Get services
        var memoryProfiler = serviceProvider.GetRequiredService<IMemoryProfiler>();
        var compressionService = serviceProvider.GetRequiredService<ICompressionService>();
        var logger = serviceProvider.GetRequiredService<ILogger<MemoryProfilingExample>>();

        logger.LogInformation("Starting Memory Profiling Example");

        // Example 1: Profile a compression operation
        await ProfileCompressionOperation(memoryProfiler, compressionService, logger);

        // Example 2: Profile memory-intensive operations
        await ProfileMemoryIntensiveOperation(memoryProfiler, logger);

        logger.LogInformation("Memory Profiling Example completed");
    }

    private static async Task ProfileCompressionOperation(
        IMemoryProfiler memoryProfiler, 
        ICompressionService compressionService, 
        ILogger logger)
    {
        logger.LogInformation("=== Compression Operation Memory Profiling ===");

        // Create test data
        var tempDir = Path.Combine(Path.GetTempPath(), $"MemoryProfilingExample_{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(tempDir, "source");
        var targetFile = Path.Combine(tempDir, "compressed.zip");

        try
        {
            Directory.CreateDirectory(sourceDir);

            // Create test files of various sizes
            await File.WriteAllTextAsync(Path.Combine(sourceDir, "small.txt"), "Small file content");
            await File.WriteAllTextAsync(Path.Combine(sourceDir, "medium.txt"), new string('M', 50000));
            await File.WriteAllTextAsync(Path.Combine(sourceDir, "large.txt"), new string('L', 500000));

            // The compression service will automatically profile memory usage
            // because we injected the memory profiler into it
            var result = await compressionService.CompressDirectoryAsync(sourceDir, targetFile);

            logger.LogInformation("Compression completed: {Result}", result);
            logger.LogInformation("Compressed file size: {Size} bytes", new FileInfo(result).Length);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    private static async Task ProfileMemoryIntensiveOperation(IMemoryProfiler memoryProfiler, ILogger logger)
    {
        logger.LogInformation("=== Memory Intensive Operation Profiling ===");

        var operationId = "memory-intensive-example";
        
        // Start profiling
        memoryProfiler.StartProfiling(operationId, "MemoryIntensiveExample");

        try
        {
            // Phase 1: Initial allocation
            memoryProfiler.RecordSnapshot(operationId, "Phase1-Start", "Starting memory allocation");
            
            var largeArrays = new List<byte[]>();
            for (int i = 0; i < 10; i++)
            {
                largeArrays.Add(new byte[1024 * 1024]); // 1 MB each
                if (i % 3 == 0)
                {
                    memoryProfiler.RecordSnapshot(operationId, $"Phase1-Alloc{i}", $"Allocated {i + 1} arrays");
                }
            }

            // Phase 2: Processing
            memoryProfiler.RecordSnapshot(operationId, "Phase2-Start", "Starting data processing");
            
            await Task.Run(() =>
            {
                for (int i = 0; i < largeArrays.Count; i++)
                {
                    // Simulate processing
                    for (int j = 0; j < largeArrays[i].Length; j += 1000)
                    {
                        largeArrays[i][j] = (byte)(i + j % 256);
                    }
                }
            });

            memoryProfiler.RecordSnapshot(operationId, "Phase2-Complete", "Data processing completed");

            // Phase 3: Force garbage collection
            memoryProfiler.ForceGarbageCollection(operationId);
            memoryProfiler.RecordSnapshot(operationId, "Phase3-AfterGC", "After forced garbage collection");

            // Phase 4: Cleanup
            largeArrays.Clear();
            memoryProfiler.ForceGarbageCollection(operationId);
            memoryProfiler.RecordSnapshot(operationId, "Phase4-Cleanup", "After cleanup and GC");

            // Get the final profile
            var profile = memoryProfiler.StopProfiling(operationId);

            // Display results
            DisplayMemoryProfile(profile, logger);

            // Get recommendations
            var recommendations = memoryProfiler.GetRecommendations(profile);
            DisplayRecommendations(recommendations, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during memory intensive operation");
            memoryProfiler.StopProfiling(operationId);
        }
    }

    private static void DisplayMemoryProfile(MemoryProfile profile, ILogger logger)
    {
        logger.LogInformation("=== Memory Profile Results ===");
        logger.LogInformation("Operation: {OperationId} ({OperationType})", profile.OperationId, profile.OperationType);
        logger.LogInformation("Duration: {Duration}", profile.Duration);
        logger.LogInformation("Snapshots: {SnapshotCount}", profile.Snapshots.Count);
        logger.LogInformation("GC Events: {GCEventCount}", profile.GCEvents.Count);
        
        logger.LogInformation("Memory Statistics:");
        logger.LogInformation("- Peak Working Set: {PeakMemory}", profile.Statistics.GetFormattedPeakWorkingSet());
        logger.LogInformation("- Average Working Set: {AverageMemory}", profile.Statistics.GetFormattedAverageWorkingSet());
        logger.LogInformation("- Memory Growth: {MemoryGrowth}", profile.Statistics.GetFormattedMemoryGrowth());
        logger.LogInformation("- Memory Growth Rate: {GrowthRate:F2} MB/s", profile.Statistics.MemoryGrowthRate / (1024 * 1024));
        logger.LogInformation("- Total GC Collections: {GCCollections}", profile.Statistics.TotalGCCollections);
        logger.LogInformation("- GC Pressure: {GCPressure:F1} collections/min", profile.Statistics.GCPressure);

        logger.LogInformation("Memory Snapshots:");
        foreach (var snapshot in profile.Snapshots.Take(10)) // Show first 10 snapshots
        {
            logger.LogInformation("- {Timestamp:HH:mm:ss.fff} [{Phase}]: {WorkingSet} (GC: {Gen0}/{Gen1}/{Gen2})",
                snapshot.Timestamp, snapshot.Phase, snapshot.GetFormattedWorkingSet(),
                snapshot.Gen0Collections, snapshot.Gen1Collections, snapshot.Gen2Collections);
        }

        if (profile.Snapshots.Count > 10)
        {
            logger.LogInformation("... and {Count} more snapshots", profile.Snapshots.Count - 10);
        }

        if (profile.GCEvents.Any())
        {
            logger.LogInformation("Garbage Collection Events:");
            foreach (var gcEvent in profile.GCEvents)
            {
                logger.LogInformation("- {Timestamp:HH:mm:ss.fff} Gen{Generation}: {MemoryFreed} freed in {Duration:F2}ms ({Reason})",
                    gcEvent.Timestamp, gcEvent.Generation, gcEvent.GetFormattedMemoryFreed(),
                    gcEvent.Duration.TotalMilliseconds, gcEvent.Reason);
            }
        }
    }

    private static void DisplayRecommendations(List<MemoryRecommendation> recommendations, ILogger logger)
    {
        if (!recommendations.Any())
        {
            logger.LogInformation("=== No Memory Recommendations ===");
            logger.LogInformation("Memory usage appears to be within normal parameters.");
            return;
        }

        logger.LogInformation("=== Memory Optimization Recommendations ===");
        
        var groupedRecommendations = recommendations.GroupBy(r => r.Priority).OrderByDescending(g => g.Key);
        
        foreach (var group in groupedRecommendations)
        {
            logger.LogInformation("{Priority} Priority Recommendations:", group.Key);
            
            foreach (var recommendation in group.Key)
            {
                logger.LogInformation("- {Title}", recommendation.Title);
                logger.LogInformation("  Description: {Description}", recommendation.Description);
                logger.LogInformation("  Action Required: {Action}", recommendation.ActionRequired);
                
                if (recommendation.EstimatedMemorySavings > 0)
                {
                    logger.LogInformation("  Estimated Savings: {Savings}", recommendation.GetFormattedMemorySavings());
                }
                
                if (!string.IsNullOrEmpty(recommendation.Phase))
                {
                    logger.LogInformation("  Related Phase: {Phase}", recommendation.Phase);
                }
                
                logger.LogInformation("");
            }
        }
    }
}

/// <summary>
/// Configuration for memory profiling examples
/// </summary>
public static class MemoryProfilingConfig
{
    /// <summary>
    /// Creates a configuration optimized for demonstration purposes
    /// </summary>
    public static MemoryProfilingConfig CreateDemoConfig()
    {
        return new MemoryProfilingConfig
        {
            AutomaticSnapshots = true,
            SnapshotInterval = TimeSpan.FromSeconds(2),
            MaxSnapshots = 100,
            CollectGCDetails = true,
            CollectSystemMemoryInfo = true,
            MemoryWarningThreshold = 500 * 1024 * 1024, // 500 MB
            MemoryCriticalThreshold = 1024 * 1024 * 1024 // 1 GB
        };
    }

    /// <summary>
    /// Creates a configuration optimized for production monitoring
    /// </summary>
    public static MemoryProfilingConfig CreateProductionConfig()
    {
        return new MemoryProfilingConfig
        {
            AutomaticSnapshots = true,
            SnapshotInterval = TimeSpan.FromSeconds(30),
            MaxSnapshots = 200,
            CollectGCDetails = true,
            CollectSystemMemoryInfo = false, // Disable for performance
            MemoryWarningThreshold = 2L * 1024 * 1024 * 1024, // 2 GB
            MemoryCriticalThreshold = 4L * 1024 * 1024 * 1024 // 4 GB
        };
    }
}