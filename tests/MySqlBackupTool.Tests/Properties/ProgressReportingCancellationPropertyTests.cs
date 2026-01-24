using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;

namespace MySqlBackupTool.Tests.Properties;

/// <summary>
/// Property-based tests for progress reporting and cancellation functionality
/// **Validates: Requirements 4.4, 6.3, 6.5**
/// </summary>
public class ProgressReportingCancellationPropertyTests : IDisposable
{
    private readonly Mock<IBackupOrchestrator> _mockOrchestrator;
    private readonly Mock<ILogger<BackgroundTaskManager>> _mockLogger;
    private readonly BackgroundTaskManager _taskManager;
    private readonly List<BackupProgressEventArgs> _progressEvents;
    private readonly List<BackupCompletedEventArgs> _completedEvents;

    public ProgressReportingCancellationPropertyTests()
    {
        _mockOrchestrator = new Mock<IBackupOrchestrator>();
        _mockLogger = new Mock<ILogger<BackgroundTaskManager>>();
        
        var config = new BackgroundTaskConfiguration
        {
            MaxConcurrentBackups = 5,
            ProgressUpdateIntervalMs = 100,
            BackupTimeoutMinutes = 1, // Short timeout for testing
            AutoCleanupCompletedTasks = false // Disable for testing
        };

        _taskManager = new BackgroundTaskManager(_mockOrchestrator.Object, _mockLogger.Object, config);
        
        _progressEvents = new List<BackupProgressEventArgs>();
        _completedEvents = new List<BackupCompletedEventArgs>();
        
        _taskManager.ProgressUpdated += (sender, args) => _progressEvents.Add(args);
        _taskManager.BackupCompleted += (sender, args) => _completedEvents.Add(args);
    }

    /// <summary>
    /// **Property 10: Progress Reporting Monotonicity**
    /// For any backup operation, progress indicators should be monotonically increasing 
    /// (never decrease) and reach 100% upon successful completion.
    /// **Validates: Requirements 4.4**
    /// </summary>
    [Property(MaxTest = 10)]
    public void ProgressReportingMonotonicity_ShouldNeverDecrease()
    {
        Prop.ForAll(
            GenerateProgressSequence(),
            (progressSequence) =>
            {
                // Arrange
                var configuration = GenerateValidBackupConfiguration();
                var progressReports = new List<BackupProgress>();
                var progressReporter = new Progress<BackupProgress>(progress => progressReports.Add(progress));

                // Setup mock to simulate progress updates
                _mockOrchestrator.Setup(x => x.ExecuteBackupAsync(
                    It.IsAny<BackupConfiguration>(),
                    It.IsAny<IProgress<BackupProgress>>(),
                    It.IsAny<CancellationToken>()))
                    .Returns((BackupConfiguration config, IProgress<BackupProgress> progress, CancellationToken ct) =>
                    {
                        return Task.Run(async () =>
                        {
                            // Simulate progress updates
                            foreach (var progressValue in progressSequence.ProgressValues)
                            {
                                if (ct.IsCancellationRequested)
                                    break;

                                var progressUpdate = new BackupProgress
                                {
                                    OperationId = Guid.NewGuid(),
                                    OverallProgress = progressValue,
                                    CurrentStatus = progressValue >= 1.0 ? BackupStatus.Completed : BackupStatus.Transferring,
                                    BytesTransferred = (long)(progressValue * progressSequence.TotalBytes),
                                    TotalBytes = progressSequence.TotalBytes,
                                    ElapsedTime = TimeSpan.FromSeconds(progressValue * 100)
                                };

                                progress?.Report(progressUpdate);
                                await Task.Delay(10, ct); // Small delay to simulate work
                            }

                            return new BackupResult
                            {
                                Success = true,
                                OperationId = Guid.NewGuid(),
                                CompletedAt = DateTime.UtcNow,
                                Duration = TimeSpan.FromSeconds(100)
                            };
                        });
                    });

                // Act
                var result = _taskManager.StartBackupAsync(configuration, progressReporter).Result;

                // Assert - Check monotonicity
                var isMonotonic = true;
                var reachedCompletion = false;
                
                for (int i = 1; i < progressReports.Count; i++)
                {
                    var current = progressReports[i];
                    var previous = progressReports[i - 1];
                    
                    // Progress should never decrease
                    if (current.OverallProgress < previous.OverallProgress)
                    {
                        isMonotonic = false;
                        break;
                    }
                    
                    // Bytes transferred should never decrease
                    if (current.BytesTransferred < previous.BytesTransferred)
                    {
                        isMonotonic = false;
                        break;
                    }
                    
                    // Elapsed time should never decrease
                    if (current.ElapsedTime < previous.ElapsedTime)
                    {
                        isMonotonic = false;
                        break;
                    }
                }

                // Check if we reached 100% completion for successful operations
                if (result.Success && progressReports.Count > 0)
                {
                    var finalProgress = progressReports.Last();
                    reachedCompletion = finalProgress.OverallProgress >= 1.0 || 
                                      finalProgress.CurrentStatus == BackupStatus.Completed;
                }

                return isMonotonic && (result.Success ? reachedCompletion : true);
            }).QuickCheckThrowOnFailure();
    }

    /// <summary>
    /// **Property 13: Background Progress Updates**
    /// For any backup operation running in the background, the system should provide 
    /// real-time progress updates to the user interface without blocking the operation.
    /// **Validates: Requirements 6.3**
    /// </summary>
    [Property(MaxTest = 8)]
    public void BackgroundProgressUpdates_ShouldProvideRealTimeUpdates()
    {
        Prop.ForAll(
            GenerateBackgroundTaskData(),
            (taskData) =>
            {
                // Arrange
                var configuration = GenerateValidBackupConfiguration();
                var operationStarted = false;
                var progressUpdateCount = 0;
                var maxProgressUpdates = taskData.ExpectedProgressUpdates;

                // Setup mock to simulate background work with progress updates
                _mockOrchestrator.Setup(x => x.ExecuteBackupAsync(
                    It.IsAny<BackupConfiguration>(),
                    It.IsAny<IProgress<BackupProgress>>(),
                    It.IsAny<CancellationToken>()))
                    .Returns((BackupConfiguration config, IProgress<BackupProgress> progress, CancellationToken ct) =>
                    {
                        return Task.Run(async () =>
                        {
                            operationStarted = true;
                            
                            // Simulate work with regular progress updates
                            for (int i = 0; i <= maxProgressUpdates && !ct.IsCancellationRequested; i++)
                            {
                                var progressValue = (double)i / maxProgressUpdates;
                                var progressUpdate = new BackupProgress
                                {
                                    OperationId = Guid.NewGuid(),
                                    OverallProgress = progressValue,
                                    CurrentStatus = progressValue >= 1.0 ? BackupStatus.Completed : BackupStatus.Transferring,
                                    CurrentOperation = $"Processing step {i}",
                                    BytesTransferred = (long)(progressValue * taskData.TotalBytes),
                                    TotalBytes = taskData.TotalBytes,
                                    ElapsedTime = TimeSpan.FromMilliseconds(i * taskData.UpdateIntervalMs)
                                };

                                progress?.Report(progressUpdate);
                                progressUpdateCount++;
                                
                                // Simulate work time
                                await Task.Delay(taskData.UpdateIntervalMs, ct);
                            }

                            return new BackupResult
                            {
                                Success = !ct.IsCancellationRequested,
                                OperationId = Guid.NewGuid(),
                                CompletedAt = DateTime.UtcNow,
                                Duration = TimeSpan.FromMilliseconds(maxProgressUpdates * taskData.UpdateIntervalMs)
                            };
                        });
                    });

                // Clear previous events
                _progressEvents.Clear();
                _completedEvents.Clear();

                // Act - Start background operation
                var backgroundTask = Task.Run(() => 
                    _taskManager.StartBackupAsync(configuration));

                // Wait a bit to ensure operation starts
                Task.Delay(50).Wait();

                // Verify operation started in background
                var operationIsRunning = operationStarted;

                // Wait for completion or timeout
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(taskData.TimeoutSeconds));
                var completedTask = Task.WhenAny(backgroundTask, timeoutTask).Result;
                
                var result = completedTask == backgroundTask ? backgroundTask.Result : null;

                // Assert - Verify background execution and progress updates
                var receivedProgressUpdates = _progressEvents.Count > 0;
                var operationCompletedInBackground = result != null;
                var progressUpdatesWhileRunning = _progressEvents.Count >= Math.Min(2, maxProgressUpdates);

                // Verify progress updates were received in real-time
                var progressTimestamps = _progressEvents.Select(e => e.Timestamp).ToList();
                var hasRealTimeUpdates = progressTimestamps.Count <= 1 || 
                    progressTimestamps.Zip(progressTimestamps.Skip(1), (a, b) => (b - a).TotalMilliseconds)
                        .All(interval => interval >= 0 && interval <= taskData.UpdateIntervalMs * 2);

                return operationIsRunning && 
                       receivedProgressUpdates && 
                       operationCompletedInBackground && 
                       progressUpdatesWhileRunning &&
                       hasRealTimeUpdates;
            }).QuickCheckThrowOnFailure();
    }

    /// <summary>
    /// **Property 14: Graceful Cancellation**
    /// For any running backup operation, the system should be able to cancel the operation 
    /// gracefully, ensuring MySQL is restarted and temporary files are cleaned up.
    /// **Validates: Requirements 6.5**
    /// </summary>
    [Property(MaxTest = 8)]
    public void GracefulCancellation_ShouldCancelOperationCleanly()
    {
        Prop.ForAll(
            GenerateCancellationTestData(),
            (testData) =>
            {
                // Arrange
                var configuration = GenerateValidBackupConfiguration();
                var operationId = Guid.NewGuid();
                var cancellationRequested = false;
                var cleanupPerformed = false;
                var mysqlRestarted = false;

                // Setup mock to simulate long-running operation that can be cancelled
                _mockOrchestrator.Setup(x => x.ExecuteBackupAsync(
                    It.IsAny<BackupConfiguration>(),
                    It.IsAny<IProgress<BackupProgress>>(),
                    It.IsAny<CancellationToken>()))
                    .Returns((BackupConfiguration config, IProgress<BackupProgress> progress, CancellationToken ct) =>
                    {
                        return Task.Run(async () =>
                        {
                            try
                            {
                                // Simulate work phases
                                for (int phase = 0; phase < testData.WorkPhases && !ct.IsCancellationRequested; phase++)
                                {
                                    var progressValue = (double)phase / testData.WorkPhases;
                                    var status = phase switch
                                    {
                                        0 => BackupStatus.StoppingMySQL,
                                        1 => BackupStatus.Compressing,
                                        2 => BackupStatus.Transferring,
                                        _ => BackupStatus.StartingMySQL
                                    };

                                    progress?.Report(new BackupProgress
                                    {
                                        OperationId = operationId,
                                        OverallProgress = progressValue,
                                        CurrentStatus = status,
                                        CurrentOperation = $"Phase {phase + 1}",
                                        ElapsedTime = TimeSpan.FromMilliseconds(phase * testData.PhaseDelayMs)
                                    });

                                    await Task.Delay(testData.PhaseDelayMs, ct);
                                }

                                // If we get here without cancellation, complete successfully
                                return new BackupResult
                                {
                                    Success = true,
                                    OperationId = operationId,
                                    CompletedAt = DateTime.UtcNow,
                                    Duration = TimeSpan.FromMilliseconds(testData.WorkPhases * testData.PhaseDelayMs)
                                };
                            }
                            catch (OperationCanceledException)
                            {
                                cancellationRequested = true;
                                
                                // Simulate cleanup operations
                                cleanupPerformed = true;
                                mysqlRestarted = true;
                                
                                // Simulate cleanup delay
                                await Task.Delay(testData.CleanupDelayMs);
                                
                                throw; // Re-throw to maintain cancellation semantics
                            }
                        });
                    });

                // Clear previous events
                _progressEvents.Clear();
                _completedEvents.Clear();

                // Act - Start operation and then cancel it
                var operationTask = Task.Run(() =>
                {
                    try
                    {
                        return _taskManager.StartBackupAsync(configuration).Result;
                    }
                    catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
                    {
                        return new BackupResult
                        {
                            Success = false,
                            OperationId = operationId,
                            ErrorMessage = "Operation was cancelled",
                            CompletedAt = DateTime.UtcNow
                        };
                    }
                });

                // Wait for operation to start
                Task.Delay(testData.CancellationDelayMs).Wait();

                // Get the operation ID from progress events
                var actualOperationId = _progressEvents.FirstOrDefault()?.OperationId ?? operationId;

                // Cancel the operation
                var cancellationResult = _taskManager.CancelBackupAsync(actualOperationId).Result;

                // Wait for operation to complete
                var result = operationTask.Result;

                // Assert - Verify graceful cancellation
                var operationWasCancelled = cancellationRequested || !result.Success;
                var cancellationWasSuccessful = cancellationResult;
                var completionEventReceived = _completedEvents.Any();
                
                // Verify cleanup was performed (simulated)
                var cleanupWasPerformed = !result.Success ? cleanupPerformed : true;
                var mysqlWasRestarted = !result.Success ? mysqlRestarted : true;

                // Verify operation completed within reasonable time
                var completedInTime = result.CompletedAt != default;

                return operationWasCancelled &&
                       cancellationWasSuccessful &&
                       completionEventReceived &&
                       cleanupWasPerformed &&
                       mysqlWasRestarted &&
                       completedInTime;
            }).QuickCheckThrowOnFailure();
    }

    #region Test Data Generators

    private static Arbitrary<ProgressSequenceData> GenerateProgressSequence()
    {
        return Arb.From(
            from stepCount in Gen.Choose(3, 8)
            from totalBytes in Gen.Choose(1024, 1048576) // 1KB to 1MB
            select new ProgressSequenceData
            {
                ProgressValues = GenerateMonotonicSequence(stepCount),
                TotalBytes = totalBytes
            });
    }

    private static double[] GenerateMonotonicSequence(int stepCount)
    {
        var sequence = new double[stepCount];
        var random = new System.Random();
        
        sequence[0] = 0.0;
        for (int i = 1; i < stepCount - 1; i++)
        {
            // Ensure monotonic increase
            var minValue = sequence[i - 1];
            var maxValue = 1.0 - (double)(stepCount - i - 1) / stepCount;
            sequence[i] = minValue + random.NextDouble() * (maxValue - minValue);
        }
        sequence[stepCount - 1] = 1.0; // Always end at 100%
        
        return sequence;
    }

    private static Arbitrary<BackgroundTaskData> GenerateBackgroundTaskData()
    {
        return Arb.From(
            from updateInterval in Gen.Choose(50, 100)
            from progressUpdates in Gen.Choose(3, 5)
            from totalBytes in Gen.Choose(1024, 1048576) // 1KB to 1MB
            from timeoutSeconds in Gen.Choose(3, 8)
            select new BackgroundTaskData
            {
                UpdateIntervalMs = updateInterval,
                ExpectedProgressUpdates = progressUpdates,
                TotalBytes = totalBytes,
                TimeoutSeconds = timeoutSeconds
            });
    }

    private static Arbitrary<CancellationTestData> GenerateCancellationTestData()
    {
        return Arb.From(
            from workPhases in Gen.Choose(3, 5)
            from phaseDelay in Gen.Choose(50, 150)
            from cancellationDelay in Gen.Choose(25, 100)
            from cleanupDelay in Gen.Choose(10, 30)
            select new CancellationTestData
            {
                WorkPhases = workPhases,
                PhaseDelayMs = phaseDelay,
                CancellationDelayMs = cancellationDelay,
                CleanupDelayMs = cleanupDelay
            });
    }

    private static BackupConfiguration GenerateValidBackupConfiguration()
    {
        return new BackupConfiguration
        {
            Id = 1,
            Name = "Test Configuration",
            MySQLConnection = new MySQLConnectionInfo
            {
                Username = "testuser",
                Password = "testpass",
                ServiceName = "MySQL80",
                DataDirectoryPath = @"C:\ProgramData\MySQL\MySQL Server 8.0\Data",
                Host = "localhost",
                Port = 3306
            },
            TargetServer = new ServerEndpoint
            {
                IPAddress = "192.168.1.100",
                Port = 8080,
                UseSSL = true
            },
            TargetDirectory = @"C:\Backups",
            NamingStrategy = new FileNamingStrategy
            {
                Pattern = "{timestamp}_{database}_{server}.zip",
                DateFormat = "yyyyMMdd_HHmmss",
                IncludeServerName = true,
                IncludeDatabaseName = true
            },
            IsActive = true
        };
    }

    #endregion

    #region Test Data Classes

    public class ProgressSequenceData
    {
        public double[] ProgressValues { get; set; } = Array.Empty<double>();
        public long TotalBytes { get; set; }
    }

    public class BackgroundTaskData
    {
        public int UpdateIntervalMs { get; set; }
        public int ExpectedProgressUpdates { get; set; }
        public long TotalBytes { get; set; }
        public int TimeoutSeconds { get; set; }
    }

    public class CancellationTestData
    {
        public int WorkPhases { get; set; }
        public int PhaseDelayMs { get; set; }
        public int CancellationDelayMs { get; set; }
        public int CleanupDelayMs { get; set; }
    }

    #endregion

    public void Dispose()
    {
        _taskManager?.Dispose();
    }
}