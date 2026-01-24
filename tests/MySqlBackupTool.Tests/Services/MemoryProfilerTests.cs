using Microsoft.Extensions.Logging;
using Moq;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;

namespace MySqlBackupTool.Tests.Services;

/// <summary>
/// Tests for the MemoryProfiler service
/// </summary>
public class MemoryProfilerTests : IDisposable
{
    private readonly Mock<ILogger<MemoryProfiler>> _mockLogger;
    private readonly MemoryProfiler _profiler;
    private readonly MemoryProfilingConfig _config;

    public MemoryProfilerTests()
    {
        _mockLogger = new Mock<ILogger<MemoryProfiler>>();
        _config = new MemoryProfilingConfig
        {
            AutomaticSnapshots = false, // Disable for testing
            SnapshotInterval = TimeSpan.FromSeconds(1),
            MaxSnapshots = 100
        };
        _profiler = new MemoryProfiler(_mockLogger.Object, _config);
    }

    [Fact]
    public void StartProfiling_WithValidParameters_ShouldCreateProfile()
    {
        // Arrange
        var operationId = "test-operation";
        var operationType = "TestOperation";

        // Act
        _profiler.StartProfiling(operationId, operationType);
        var profile = _profiler.GetCurrentProfile(operationId);

        // Assert
        Assert.NotNull(profile);
        Assert.Equal(operationId, profile.OperationId);
        Assert.Equal(operationType, profile.OperationType);
        Assert.True(profile.Snapshots.Any());
        Assert.Equal("Start", profile.Snapshots.First().Phase);
    }

    [Fact]
    public void StartProfiling_WithNullOperationId_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _profiler.StartProfiling(null!, "TestOperation"));
    }

    [Fact]
    public void StartProfiling_WithEmptyOperationType_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _profiler.StartProfiling("test-operation", ""));
    }

    [Fact]
    public void RecordSnapshot_WithValidOperation_ShouldAddSnapshot()
    {
        // Arrange
        var operationId = "test-operation";
        _profiler.StartProfiling(operationId, "TestOperation");
        var initialSnapshotCount = _profiler.GetCurrentProfile(operationId)!.Snapshots.Count;

        // Act
        _profiler.RecordSnapshot(operationId, "TestPhase", "Test info");
        var profile = _profiler.GetCurrentProfile(operationId);

        // Assert
        Assert.NotNull(profile);
        Assert.Equal(initialSnapshotCount + 1, profile.Snapshots.Count);
        
        var lastSnapshot = profile.Snapshots.Last();
        Assert.Equal("TestPhase", lastSnapshot.Phase);
        Assert.Equal("Test info", lastSnapshot.AdditionalInfo);
        Assert.True(lastSnapshot.WorkingSet > 0);
    }

    [Fact]
    public void RecordSnapshot_WithUnknownOperation_ShouldNotThrow()
    {
        // Act & Assert - Should not throw
        _profiler.RecordSnapshot("unknown-operation", "TestPhase", "Test info");
    }

    [Fact]
    public void StopProfiling_WithValidOperation_ShouldReturnCompleteProfile()
    {
        // Arrange
        var operationId = "test-operation";
        _profiler.StartProfiling(operationId, "TestOperation");
        _profiler.RecordSnapshot(operationId, "MiddlePhase", "Middle info");

        // Act
        var profile = _profiler.StopProfiling(operationId);

        // Assert
        Assert.NotNull(profile);
        Assert.Equal(operationId, profile.OperationId);
        Assert.NotNull(profile.EndTime);
        Assert.True(profile.Duration.TotalMilliseconds > 0);
        Assert.True(profile.Snapshots.Count >= 2); // Start + End snapshots
        Assert.NotNull(profile.Statistics);
        
        // Should no longer be in active profiles
        Assert.Null(_profiler.GetCurrentProfile(operationId));
    }

    [Fact]
    public void StopProfiling_WithUnknownOperation_ShouldReturnEmptyProfile()
    {
        // Act
        var profile = _profiler.StopProfiling("unknown-operation");

        // Assert
        Assert.NotNull(profile);
        Assert.Equal("unknown-operation", profile.OperationId);
        Assert.Empty(profile.Snapshots);
    }

    [Fact]
    public void ForceGarbageCollection_WithValidOperation_ShouldRecordGCEvent()
    {
        // Arrange
        var operationId = "test-operation";
        _profiler.StartProfiling(operationId, "TestOperation");

        // Act
        _profiler.ForceGarbageCollection(operationId);
        var profile = _profiler.GetCurrentProfile(operationId);

        // Assert
        Assert.NotNull(profile);
        Assert.True(profile.GCEvents.Any());
        
        var gcEvent = profile.GCEvents.First();
        Assert.True(gcEvent.WasForced);
        Assert.Equal("Manual/Forced", gcEvent.Reason);
        Assert.True(gcEvent.Duration.TotalMilliseconds >= 0);
    }

    [Fact]
    public void GetRecommendations_WithHighMemoryUsage_ShouldReturnRecommendations()
    {
        // Arrange
        var profile = new MemoryProfile
        {
            OperationId = "test-operation",
            OperationType = "TestOperation",
            Statistics = new MemoryStatistics
            {
                PeakWorkingSet = 3L * 1024 * 1024 * 1024, // 3 GB - above critical threshold
                MemoryGrowthRate = 100 * 1024 * 1024, // 100 MB/s - high growth rate
                GCPressure = 15, // High GC pressure
                ProfileDuration = TimeSpan.FromMinutes(5)
            }
        };

        // Act
        var recommendations = _profiler.GetRecommendations(profile);

        // Assert
        Assert.NotEmpty(recommendations);
        
        // Should have critical memory usage recommendation
        Assert.Contains(recommendations, r => r.Priority == MemoryRecommendationPriority.Critical);
        
        // Should have high memory growth rate recommendation
        Assert.Contains(recommendations, r => r.Type == MemoryRecommendationType.StreamingOptimization);
        
        // Should have GC pressure recommendation
        Assert.Contains(recommendations, r => r.Type == MemoryRecommendationType.IncreaseGCFrequency);
    }

    [Fact]
    public void GetRecommendations_WithNormalMemoryUsage_ShouldReturnFewRecommendations()
    {
        // Arrange
        var profile = new MemoryProfile
        {
            OperationId = "test-operation",
            OperationType = "TestOperation",
            Statistics = new MemoryStatistics
            {
                PeakWorkingSet = 100 * 1024 * 1024, // 100 MB - normal usage
                MemoryGrowthRate = 1024 * 1024, // 1 MB/s - normal growth
                GCPressure = 2, // Low GC pressure
                ProfileDuration = TimeSpan.FromMinutes(5)
            }
        };

        // Act
        var recommendations = _profiler.GetRecommendations(profile);

        // Assert
        // Should have few or no recommendations for normal usage
        Assert.True(recommendations.Count <= 1);
    }

    [Fact]
    public void MemorySnapshot_ShouldCaptureSystemMetrics()
    {
        // Arrange
        var operationId = "test-operation";
        _profiler.StartProfiling(operationId, "TestOperation");

        // Act
        _profiler.RecordSnapshot(operationId, "TestPhase");
        var profile = _profiler.GetCurrentProfile(operationId);

        // Assert
        Assert.NotNull(profile);
        var snapshot = profile.Snapshots.Last();
        
        // Should capture basic memory metrics
        Assert.True(snapshot.WorkingSet > 0);
        Assert.True(snapshot.TotalMemory > 0);
        Assert.True(snapshot.Gen0Collections >= 0);
        Assert.True(snapshot.Gen1Collections >= 0);
        Assert.True(snapshot.Gen2Collections >= 0);
        
        // Should have formatted strings
        Assert.NotEmpty(snapshot.GetFormattedWorkingSet());
        Assert.NotEmpty(snapshot.GetFormattedTotalMemory());
    }

    [Fact]
    public void MemoryStatistics_ShouldCalculateCorrectly()
    {
        // Arrange
        var operationId = "test-operation";
        _profiler.StartProfiling(operationId, "TestOperation");
        
        // Add some snapshots with delays to create measurable differences
        _profiler.RecordSnapshot(operationId, "Phase1");
        Thread.Sleep(10); // Small delay
        _profiler.RecordSnapshot(operationId, "Phase2");
        Thread.Sleep(10); // Small delay
        _profiler.RecordSnapshot(operationId, "Phase3");

        // Act
        var profile = _profiler.StopProfiling(operationId);

        // Assert
        Assert.NotNull(profile.Statistics);
        Assert.True(profile.Statistics.SnapshotCount >= 4); // Start + 3 manual + End
        Assert.True(profile.Statistics.ProfileDuration.TotalMilliseconds > 0);
        Assert.True(profile.Statistics.PeakWorkingSet > 0);
        Assert.True(profile.Statistics.AverageWorkingSet > 0);
        Assert.True(profile.Statistics.MinimumWorkingSet > 0);
        
        // Peak should be >= average >= minimum
        Assert.True(profile.Statistics.PeakWorkingSet >= profile.Statistics.AverageWorkingSet);
        Assert.True(profile.Statistics.AverageWorkingSet >= profile.Statistics.MinimumWorkingSet);
    }

    public void Dispose()
    {
        _profiler?.Dispose();
    }
}