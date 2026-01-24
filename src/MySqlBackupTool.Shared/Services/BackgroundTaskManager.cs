using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Manages background backup operations with thread-safe progress reporting and cancellation support
/// </summary>
public class BackgroundTaskManager : IBackgroundTaskManager, IDisposable
{
    private readonly IBackupOrchestrator _backupOrchestrator;
    private readonly ILogger<BackgroundTaskManager> _logger;
    private readonly BackgroundTaskConfiguration _configuration;
    private readonly ConcurrentDictionary<Guid, BackupTask> _runningTasks;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly Timer _cleanupTimer;
    private readonly BackgroundTaskStatistics _statistics;
    private readonly object _statisticsLock = new();
    private bool _disposed;

    public event EventHandler<BackupProgressEventArgs>? ProgressUpdated;
    public event EventHandler<BackupCompletedEventArgs>? BackupCompleted;

    public BackgroundTaskManager(
        IBackupOrchestrator backupOrchestrator,
        ILogger<BackgroundTaskManager> logger,
        BackgroundTaskConfiguration? configuration = null)
    {
        _backupOrchestrator = backupOrchestrator ?? throw new ArgumentNullException(nameof(backupOrchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? new BackgroundTaskConfiguration();
        
        _runningTasks = new ConcurrentDictionary<Guid, BackupTask>();
        _concurrencyLimiter = new SemaphoreSlim(_configuration.MaxConcurrentBackups, _configuration.MaxConcurrentBackups);
        _statistics = new BackgroundTaskStatistics();
        
        // Setup cleanup timer to run every minute
        _cleanupTimer = new Timer(CleanupCompletedTasks, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        
        _logger.LogInformation("BackgroundTaskManager initialized with max concurrent backups: {MaxConcurrent}", 
            _configuration.MaxConcurrentBackups);
    }

    /// <summary>
    /// Starts a backup operation in the background
    /// </summary>
    public async Task<BackupResult> StartBackupAsync(BackupConfiguration configuration, IProgress<BackupProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BackgroundTaskManager));

        var operationId = Guid.NewGuid();
        
        _logger.LogInformation("Starting background backup operation {OperationId} for configuration {ConfigurationName}", 
            operationId, configuration.Name);

        // Wait for available slot (respects concurrency limits)
        await _concurrencyLimiter.WaitAsync(cancellationToken);

        try
        {
            // Create backup task
            var backupTask = new BackupTask
            {
                OperationId = operationId,
                Configuration = configuration,
                StartedAt = DateTime.UtcNow
            };

            // Create combined cancellation token
            var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, 
                backupTask.CancellationTokenSource.Token);

            // Add timeout if configured
            if (_configuration.BackupTimeoutMinutes > 0)
            {
                combinedTokenSource.CancelAfter(TimeSpan.FromMinutes(_configuration.BackupTimeoutMinutes));
            }

            // Create progress reporter that updates the task and raises events
            var progressReporter = new Progress<BackupProgress>(progressUpdate =>
            {
                try
                {
                    // Update task progress
                    backupTask.Progress = progressUpdate;
                    backupTask.Progress.OperationId = operationId;

                    // Raise progress event
                    ProgressUpdated?.Invoke(this, new BackupProgressEventArgs
                    {
                        OperationId = operationId,
                        Progress = progressUpdate,
                        Timestamp = DateTime.UtcNow
                    });

                    // Forward to original progress reporter if provided
                    progress?.Report(progressUpdate);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reporting progress for operation {OperationId}", operationId);
                }
            });

            // Start the backup task
            backupTask.Task = Task.Run(async () =>
            {
                try
                {
                    UpdateStatistics(stats => stats.TotalTasksStarted++);
                    UpdateStatistics(stats => stats.LastTaskStarted = DateTime.UtcNow);

                    var result = await _backupOrchestrator.ExecuteBackupAsync(
                        configuration, 
                        progressReporter, 
                        combinedTokenSource.Token);

                    // Update statistics based on result
                    if (result.Success)
                    {
                        UpdateStatistics(stats => stats.TasksCompleted++);
                    }
                    else
                    {
                        UpdateStatistics(stats => stats.TasksFailed++);
                    }

                    UpdateStatistics(stats => stats.LastTaskCompleted = DateTime.UtcNow);
                    UpdateAverageExecutionTime(result.Duration);

                    // Raise completion event
                    BackupCompleted?.Invoke(this, new BackupCompletedEventArgs
                    {
                        OperationId = operationId,
                        Result = result,
                        CompletedAt = DateTime.UtcNow
                    });

                    return result;
                }
                catch (OperationCanceledException)
                {
                    UpdateStatistics(stats => stats.TasksCancelled++);
                    
                    var cancelledResult = new BackupResult
                    {
                        OperationId = operationId,
                        Success = false,
                        ErrorMessage = "Operation was cancelled",
                        CompletedAt = DateTime.UtcNow,
                        Duration = DateTime.UtcNow - backupTask.StartedAt
                    };

                    BackupCompleted?.Invoke(this, new BackupCompletedEventArgs
                    {
                        OperationId = operationId,
                        Result = cancelledResult,
                        CompletedAt = DateTime.UtcNow
                    });

                    return cancelledResult;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in background backup task {OperationId}", operationId);
                    
                    UpdateStatistics(stats => stats.TasksFailed++);
                    
                    var errorResult = new BackupResult
                    {
                        OperationId = operationId,
                        Success = false,
                        ErrorMessage = $"Unexpected error: {ex.Message}",
                        CompletedAt = DateTime.UtcNow,
                        Duration = DateTime.UtcNow - backupTask.StartedAt
                    };

                    BackupCompleted?.Invoke(this, new BackupCompletedEventArgs
                    {
                        OperationId = operationId,
                        Result = errorResult,
                        CompletedAt = DateTime.UtcNow
                    });

                    return errorResult;
                }
                finally
                {
                    // Release concurrency slot
                    _concurrencyLimiter.Release();
                    combinedTokenSource.Dispose();
                }
            }, combinedTokenSource.Token);

            // Add task to running tasks collection
            _runningTasks.TryAdd(operationId, backupTask);

            _logger.LogInformation("Background backup task {OperationId} started successfully", operationId);

            // Return the task result (this will wait for completion)
            return await backupTask.Task;
        }
        catch (Exception ex)
        {
            // Release concurrency slot if we failed to start
            _concurrencyLimiter.Release();
            
            _logger.LogError(ex, "Failed to start background backup operation {OperationId}", operationId);
            
            return new BackupResult
            {
                OperationId = operationId,
                Success = false,
                ErrorMessage = $"Failed to start backup: {ex.Message}",
                CompletedAt = DateTime.UtcNow,
                Duration = TimeSpan.Zero
            };
        }
    }

    /// <summary>
    /// Cancels a running backup operation
    /// </summary>
    public async Task<bool> CancelBackupAsync(Guid operationId)
    {
        if (_disposed)
            return false;

        _logger.LogInformation("Attempting to cancel backup operation {OperationId}", operationId);

        if (_runningTasks.TryGetValue(operationId, out var backupTask))
        {
            try
            {
                // Cancel the operation
                backupTask.CancellationTokenSource.Cancel();
                
                // Wait for the task to complete (with timeout)
                if (backupTask.Task != null)
                {
                    try
                    {
                        await backupTask.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancellation succeeds
                    }
                    catch (TimeoutException)
                    {
                        _logger.LogWarning("Timeout waiting for backup operation {OperationId} to cancel", operationId);
                    }
                }

                _logger.LogInformation("Successfully cancelled backup operation {OperationId}", operationId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling backup operation {OperationId}", operationId);
                return false;
            }
        }

        _logger.LogWarning("Backup operation {OperationId} not found for cancellation", operationId);
        return false;
    }

    /// <summary>
    /// Gets the status of a backup operation
    /// </summary>
    public async Task<BackupProgress?> GetBackupStatusAsync(Guid operationId)
    {
        if (_disposed)
            return null;

        await Task.CompletedTask; // Make method async

        if (_runningTasks.TryGetValue(operationId, out var backupTask))
        {
            return backupTask.Progress;
        }

        return null;
    }

    /// <summary>
    /// Gets all currently running backup operations
    /// </summary>
    public async Task<IEnumerable<BackupProgress>> GetRunningBackupsAsync()
    {
        if (_disposed)
            return Enumerable.Empty<BackupProgress>();

        await Task.CompletedTask; // Make method async

        return _runningTasks.Values
            .Where(task => !task.IsCompleted)
            .Select(task => task.Progress)
            .ToList();
    }

    /// <summary>
    /// Gets current statistics about background task execution
    /// </summary>
    public BackgroundTaskStatistics GetStatistics()
    {
        lock (_statisticsLock)
        {
            var stats = new BackgroundTaskStatistics
            {
                TotalTasksStarted = _statistics.TotalTasksStarted,
                TasksCompleted = _statistics.TasksCompleted,
                TasksFailed = _statistics.TasksFailed,
                TasksCancelled = _statistics.TasksCancelled,
                CurrentlyRunning = _runningTasks.Count(kvp => !kvp.Value.IsCompleted),
                AverageExecutionTime = _statistics.AverageExecutionTime,
                LastTaskStarted = _statistics.LastTaskStarted,
                LastTaskCompleted = _statistics.LastTaskCompleted
            };

            return stats;
        }
    }

    /// <summary>
    /// Updates statistics in a thread-safe manner
    /// </summary>
    private void UpdateStatistics(Action<BackgroundTaskStatistics> updateAction)
    {
        lock (_statisticsLock)
        {
            updateAction(_statistics);
        }
    }

    /// <summary>
    /// Updates the average execution time
    /// </summary>
    private void UpdateAverageExecutionTime(TimeSpan newDuration)
    {
        lock (_statisticsLock)
        {
            var totalCompleted = _statistics.TasksCompleted + _statistics.TasksFailed + _statistics.TasksCancelled;
            if (totalCompleted > 0)
            {
                var totalTicks = _statistics.AverageExecutionTime.Ticks * (totalCompleted - 1) + newDuration.Ticks;
                _statistics.AverageExecutionTime = new TimeSpan(totalTicks / totalCompleted);
            }
            else
            {
                _statistics.AverageExecutionTime = newDuration;
            }
        }
    }

    /// <summary>
    /// Cleans up completed tasks periodically
    /// </summary>
    private void CleanupCompletedTasks(object? state)
    {
        if (_disposed || !_configuration.AutoCleanupCompletedTasks)
            return;

        try
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(-_configuration.CompletedTaskRetentionMinutes);
            var tasksToRemove = new List<Guid>();

            foreach (var kvp in _runningTasks)
            {
                var task = kvp.Value;
                if (task.IsCompleted && task.StartedAt < cutoffTime)
                {
                    tasksToRemove.Add(kvp.Key);
                }
            }

            foreach (var taskId in tasksToRemove)
            {
                if (_runningTasks.TryRemove(taskId, out var removedTask))
                {
                    removedTask.CancellationTokenSource.Dispose();
                    _logger.LogDebug("Cleaned up completed task {TaskId}", taskId);
                }
            }

            if (tasksToRemove.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} completed tasks", tasksToRemove.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during task cleanup");
        }
    }

    /// <summary>
    /// Disposes resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            // Cancel all running tasks
            var cancellationTasks = new List<Task>();
            foreach (var task in _runningTasks.Values)
            {
                task.CancellationTokenSource.Cancel();
                if (task.Task != null && !task.Task.IsCompleted)
                {
                    cancellationTasks.Add(task.Task);
                }
            }

            // Wait for all tasks to complete (with timeout)
            if (cancellationTasks.Count > 0)
            {
                try
                {
                    Task.WaitAll(cancellationTasks.ToArray(), TimeSpan.FromSeconds(30));
                }
                catch (AggregateException)
                {
                    // Expected when tasks are cancelled
                }
            }

            // Dispose resources
            _cleanupTimer?.Dispose();
            _concurrencyLimiter?.Dispose();

            // Dispose all task cancellation tokens
            foreach (var task in _runningTasks.Values)
            {
                task.CancellationTokenSource.Dispose();
            }

            _runningTasks.Clear();

            _logger.LogInformation("BackgroundTaskManager disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during BackgroundTaskManager disposal");
        }
    }
}