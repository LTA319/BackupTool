using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;

namespace MySqlBackupTool.Tests.Integration;

/// <summary>
/// Integration tests for memory profiling during backup operations
/// </summary>
public class MemoryProfilingIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly string _tempDirectory;

    public MemoryProfilingIntegrationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"MemoryProfilingTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddSharedServices("Data Source=:memory:");

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task CompressionService_WithMemoryProfiling_ShouldProfileMemoryUsage()
    {
        // Arrange
        var memoryProfiler = _serviceProvider.GetRequiredService<IMemoryProfiler>();
        var logger = _serviceProvider.GetRequiredService<ILogger<CompressionService>>();
        var compressionService = new CompressionService(logger, memoryProfiler);

        // Create test directory with files
        var sourceDir = Path.Combine(_tempDirectory, "source");
        Directory.CreateDirectory(sourceDir);
        
        // Create test files of various sizes
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "small.txt"), "Small file content");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "medium.txt"), new string('M', 10000));
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "large.txt"), new string('L', 100000));

        var targetPath = Path.Combine(_tempDirectory, "compressed.zip");

        // Act
        var result = await compressionService.CompressDirectoryAsync(sourceDir, targetPath);

        // Assert
        Assert.Equal(targetPath, result);
        Assert.True(File.Exists(targetPath));

        // Verify file was created and has reasonable size
        var fileInfo = new FileInfo(targetPath);
        Assert.True(fileInfo.Length > 0);
        Assert.True(fileInfo.Length < 200000); // Should be compressed
    }

    [Fact]
    public void MemoryProfiler_WithLargeDataProcessing_ShouldDetectMemoryPatterns()
    {
        // Arrange
        var memoryProfiler = _serviceProvider.GetRequiredService<IMemoryProfiler>();
        var operationId = "large-data-test";

        // Act
        memoryProfiler.StartProfiling(operationId, "LargeDataProcessing");

        // Simulate memory-intensive operations
        memoryProfiler.RecordSnapshot(operationId, "Start", "Beginning large data processing");

        // Allocate some memory to simulate processing
        var largeArray = new byte[10 * 1024 * 1024]; // 10 MB
        for (int i = 0; i < largeArray.Length; i++)
        {
            largeArray[i] = (byte)(i % 256);
        }
        memoryProfiler.RecordSnapshot(operationId, "AllocatedMemory", "Allocated 10MB array");

        // Force garbage collection
        memoryProfiler.ForceGarbageCollection(operationId);
        memoryProfiler.RecordSnapshot(operationId, "AfterGC", "After forced garbage collection");

        // Process the data
        var sum = largeArray.Sum(b => (long)b);
        memoryProfiler.RecordSnapshot(operationId, "ProcessedData", $"Processed data, sum: {sum}");

        // Clear reference and force GC again
        largeArray = null!;
        memoryProfiler.ForceGarbageCollection(operationId);
        memoryProfiler.RecordSnapshot(operationId, "Cleanup", "After cleanup and GC");

        var profile = memoryProfiler.StopProfiling(operationId);

        // Assert
        Assert.NotNull(profile);
        Assert.Equal(operationId, profile.OperationId);
        Assert.True(profile.Snapshots.Count >= 5);
        Assert.True(profile.GCEvents.Count >= 2);
        Assert.True(profile.Duration.TotalMilliseconds > 0);

        // Verify statistics
        Assert.True(profile.Statistics.PeakWorkingSet > profile.Statistics.MinimumWorkingSet);
        Assert.True(profile.Statistics.SnapshotCount >= 5);
        Assert.True(profile.Statistics.TotalGCCollections >= 2);

        // Get recommendations
        var recommendations = memoryProfiler.GetRecommendations(profile);
        Assert.NotNull(recommendations);

        // Log results for manual inspection
        var logger = _serviceProvider.GetRequiredService<ILogger<MemoryProfilingIntegrationTests>>();
        logger.LogInformation("Memory Profile Results:");
        logger.LogInformation("- Operation: {OperationId}", profile.OperationId);
        logger.LogInformation("- Duration: {Duration}", profile.Duration);
        logger.LogInformation("- Peak Memory: {PeakMemory}", profile.Statistics.GetFormattedPeakWorkingSet());
        logger.LogInformation("- Memory Growth: {MemoryGrowth}", profile.Statistics.GetFormattedMemoryGrowth());
        logger.LogInformation("- GC Collections: {GCCollections}", profile.Statistics.TotalGCCollections);
        logger.LogInformation("- Snapshots: {SnapshotCount}", profile.Statistics.SnapshotCount);

        if (recommendations.Any())
        {
            logger.LogInformation("Memory Recommendations:");
            foreach (var rec in recommendations)
            {
                logger.LogInformation("- {Priority}: {Title} - {Description}", 
                    rec.Priority, rec.Title, rec.Description);
            }
        }
    }

    [Fact]
    public void MemoryProfiler_WithMultipleOperations_ShouldHandleConcurrency()
    {
        // Arrange
        var memoryProfiler = _serviceProvider.GetRequiredService<IMemoryProfiler>();
        var operation1 = "concurrent-op-1";
        var operation2 = "concurrent-op-2";

        // Act
        memoryProfiler.StartProfiling(operation1, "ConcurrentOperation1");
        memoryProfiler.StartProfiling(operation2, "ConcurrentOperation2");

        // Record snapshots for both operations
        memoryProfiler.RecordSnapshot(operation1, "Phase1", "Operation 1 Phase 1");
        memoryProfiler.RecordSnapshot(operation2, "Phase1", "Operation 2 Phase 1");

        memoryProfiler.RecordSnapshot(operation1, "Phase2", "Operation 1 Phase 2");
        memoryProfiler.RecordSnapshot(operation2, "Phase2", "Operation 2 Phase 2");

        // Stop profiling
        var profile1 = memoryProfiler.StopProfiling(operation1);
        var profile2 = memoryProfiler.StopProfiling(operation2);

        // Assert
        Assert.NotNull(profile1);
        Assert.NotNull(profile2);
        Assert.Equal(operation1, profile1.OperationId);
        Assert.Equal(operation2, profile2.OperationId);
        Assert.NotEqual(profile1.OperationId, profile2.OperationId);

        // Both should have snapshots
        Assert.True(profile1.Snapshots.Count >= 3); // Start + 2 manual + End
        Assert.True(profile2.Snapshots.Count >= 3); // Start + 2 manual + End

        // Should not interfere with each other
        Assert.Null(memoryProfiler.GetCurrentProfile(operation1));
        Assert.Null(memoryProfiler.GetCurrentProfile(operation2));
    }

    [Fact]
    public void MemoryProfiler_WithAutomaticSnapshots_ShouldTakePeriodicSnapshots()
    {
        // Arrange
        var config = new MemoryProfilingConfig
        {
            AutomaticSnapshots = true,
            SnapshotInterval = TimeSpan.FromMilliseconds(100), // Very frequent for testing
            MaxSnapshots = 50
        };
        
        var logger = _serviceProvider.GetRequiredService<ILogger<MemoryProfiler>>();
        using var profiler = new MemoryProfiler(logger, config);
        
        var operationId = "auto-snapshot-test";

        // Act
        profiler.StartProfiling(operationId, "AutoSnapshotTest");
        
        // Wait for automatic snapshots
        Thread.Sleep(500); // Wait 500ms to allow several automatic snapshots
        
        var profile = profiler.StopProfiling(operationId);

        // Assert
        Assert.NotNull(profile);
        
        // Should have more snapshots than just start and end due to automatic snapshots
        Assert.True(profile.Snapshots.Count > 2);
        
        // Should have some automatic snapshots
        Assert.Contains(profile.Snapshots, s => s.Phase == "Auto");
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }
}