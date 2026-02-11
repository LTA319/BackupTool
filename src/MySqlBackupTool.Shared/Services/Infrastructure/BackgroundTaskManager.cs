using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 后台任务管理器，管理后台备份操作，支持线程安全的进度报告和取消操作 / Manages background backup operations with thread-safe progress reporting and cancellation support
/// </summary>
public class BackgroundTaskManager : IBackgroundTaskManager, IDisposable
{
    /// <summary>
    /// 备份编排器，用于执行实际的备份操作 / Backup orchestrator for executing actual backup operations
    /// </summary>
    private readonly IBackupOrchestrator _backupOrchestrator;
    
    /// <summary>
    /// 日志记录器 / Logger for recording operations
    /// </summary>
    private readonly ILogger<BackgroundTaskManager> _logger;
    
    /// <summary>
    /// 后台任务配置 / Background task configuration
    /// </summary>
    private readonly BackgroundTaskConfiguration _configuration;
    
    /// <summary>
    /// 正在运行的任务集合，线程安全 / Collection of running tasks, thread-safe
    /// </summary>
    private readonly ConcurrentDictionary<Guid, BackupTask> _runningTasks;
    
    /// <summary>
    /// 并发限制器，控制同时运行的备份任务数量 / Concurrency limiter to control the number of simultaneous backup tasks
    /// </summary>
    private readonly SemaphoreSlim _concurrencyLimiter;
    
    /// <summary>
    /// 清理定时器，定期清理已完成的任务 / Cleanup timer for periodically cleaning up completed tasks
    /// </summary>
    private readonly Timer _cleanupTimer;
    
    /// <summary>
    /// 后台任务统计信息 / Background task statistics
    /// </summary>
    private readonly BackgroundTaskStatistics _statistics;
    
    /// <summary>
    /// 统计信息锁对象，确保线程安全 / Statistics lock object for thread safety
    /// </summary>
    private readonly object _statisticsLock = new();
    
    /// <summary>
    /// 是否已释放资源 / Whether resources have been disposed
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 进度更新事件，当备份进度发生变化时触发 / Progress update event, triggered when backup progress changes
    /// </summary>
    public event EventHandler<BackupProgressEventArgs>? ProgressUpdated;
    
    /// <summary>
    /// 备份完成事件，当备份操作完成时触发 / Backup completed event, triggered when backup operation completes
    /// </summary>
    public event EventHandler<BackupCompletedEventArgs>? BackupCompleted;

    /// <summary>
    /// 初始化后台任务管理器 / Initializes the background task manager
    /// </summary>
    /// <param name="backupOrchestrator">备份编排器实例 / Backup orchestrator instance</param>
    /// <param name="logger">日志记录器实例 / Logger instance</param>
    /// <param name="configuration">后台任务配置，可选 / Background task configuration, optional</param>
    /// <exception cref="ArgumentNullException">当必需参数为null时抛出 / Thrown when required parameters are null</exception>
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
        
        // 设置清理定时器，每分钟运行一次 / Setup cleanup timer to run every minute
        _cleanupTimer = new Timer(CleanupCompletedTasks, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        
        _logger.LogInformation("BackgroundTaskManager initialized with max concurrent backups: {MaxConcurrent}", 
            _configuration.MaxConcurrentBackups);
    }

    /// <summary>
    /// 在后台启动备份操作 / Starts a backup operation in the background
    /// </summary>
    /// <param name="configuration">备份配置 / Backup configuration</param>
    /// <param name="progress">进度报告器，可选 / Progress reporter, optional</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>备份结果 / Backup result</returns>
    /// <exception cref="ObjectDisposedException">当对象已释放时抛出 / Thrown when object is disposed</exception>
    public async Task<BackupResult> StartBackupAsync(BackupConfiguration configuration, IProgress<BackupProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BackgroundTaskManager));

        var operationId = Guid.NewGuid();
        
        _logger.LogInformation("Starting background backup operation {OperationId} for configuration {ConfigurationName}", 
            operationId, configuration.Name);

        // 等待可用槽位（遵循并发限制）/ Wait for available slot (respects concurrency limits)
        await _concurrencyLimiter.WaitAsync(cancellationToken);

        try
        {
            // 创建备份任务 / Create backup task
            var backupTask = new BackupTask
            {
                OperationId = operationId,
                Configuration = configuration,
                StartedAt = DateTime.Now
            };

            // 创建组合取消令牌 / Create combined cancellation token
            var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, 
                backupTask.CancellationTokenSource.Token);

            // 如果配置了超时时间，则添加超时 / Add timeout if configured
            if (_configuration.BackupTimeoutMinutes > 0)
            {
                combinedTokenSource.CancelAfter(TimeSpan.FromMinutes(_configuration.BackupTimeoutMinutes));
            }

            // 创建进度报告器，更新任务并触发事件 / Create progress reporter that updates the task and raises events
            var progressReporter = new Progress<BackupProgress>(progressUpdate =>
            {
                try
                {
                    // 更新任务进度 / Update task progress
                    backupTask.Progress = progressUpdate;
                    backupTask.Progress.OperationId = operationId;

                    // 触发进度事件 / Raise progress event
                    ProgressUpdated?.Invoke(this, new BackupProgressEventArgs
                    {
                        OperationId = operationId,
                        Progress = progressUpdate,
                        Timestamp = DateTime.Now
                    });

                    // 转发到原始进度报告器（如果提供）/ Forward to original progress reporter if provided
                    progress?.Report(progressUpdate);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reporting progress for operation {OperationId}", operationId);
                }
            });

            // 启动备份任务 / Start the backup task
            backupTask.Task = Task.Run(async () =>
            {
                try
                {
                    UpdateStatistics(stats => stats.TotalTasksStarted++);
                    UpdateStatistics(stats => stats.LastTaskStarted = DateTime.Now);

                    var result = await _backupOrchestrator.ExecuteBackupAsync(
                        configuration, 
                        progressReporter, 
                        combinedTokenSource.Token);

                    // 根据结果更新统计信息 / Update statistics based on result
                    if (result.Success)
                    {
                        UpdateStatistics(stats => stats.TasksCompleted++);
                    }
                    else
                    {
                        UpdateStatistics(stats => stats.TasksFailed++);
                    }

                    UpdateStatistics(stats => stats.LastTaskCompleted = DateTime.Now);
                    UpdateAverageExecutionTime(result.Duration);

                    // 触发完成事件 / Raise completion event
                    BackupCompleted?.Invoke(this, new BackupCompletedEventArgs
                    {
                        OperationId = operationId,
                        Result = result,
                        CompletedAt = DateTime.Now
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
                        CompletedAt = DateTime.Now,
                        Duration = DateTime.Now - backupTask.StartedAt
                    };

                    BackupCompleted?.Invoke(this, new BackupCompletedEventArgs
                    {
                        OperationId = operationId,
                        Result = cancelledResult,
                        CompletedAt = DateTime.Now
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
                        CompletedAt = DateTime.Now,
                        Duration = DateTime.Now - backupTask.StartedAt
                    };

                    BackupCompleted?.Invoke(this, new BackupCompletedEventArgs
                    {
                        OperationId = operationId,
                        Result = errorResult,
                        CompletedAt = DateTime.Now
                    });

                    return errorResult;
                }
                finally
                {
                    // 释放并发槽位 / Release concurrency slot
                    _concurrencyLimiter.Release();
                    combinedTokenSource.Dispose();
                }
            }, combinedTokenSource.Token);

            // 将任务添加到正在运行的任务集合 / Add task to running tasks collection
            _runningTasks.TryAdd(operationId, backupTask);

            _logger.LogInformation("Background backup task {OperationId} started successfully", operationId);

            // 返回任务结果（这将等待完成）/ Return the task result (this will wait for completion)
            return await backupTask.Task;
        }
        catch (Exception ex)
        {
            // 如果启动失败，释放并发槽位 / Release concurrency slot if we failed to start
            _concurrencyLimiter.Release();
            
            _logger.LogError(ex, "Failed to start background backup operation {OperationId}", operationId);
            
            return new BackupResult
            {
                OperationId = operationId,
                Success = false,
                ErrorMessage = $"Failed to start backup: {ex.Message}",
                CompletedAt = DateTime.Now,
                Duration = TimeSpan.Zero
            };
        }
    }

    /// <summary>
    /// 取消正在运行的备份操作 / Cancels a running backup operation
    /// </summary>
    /// <param name="operationId">操作ID / Operation ID</param>
    /// <returns>是否成功取消 / Whether cancellation was successful</returns>
    public async Task<bool> CancelBackupAsync(Guid operationId)
    {
        if (_disposed)
            return false;

        _logger.LogInformation("Attempting to cancel backup operation {OperationId}", operationId);

        if (_runningTasks.TryGetValue(operationId, out var backupTask))
        {
            try
            {
                // 取消操作 / Cancel the operation
                backupTask.CancellationTokenSource.Cancel();
                
                // 等待任务完成（带超时）/ Wait for the task to complete (with timeout)
                if (backupTask.Task != null)
                {
                    try
                    {
                        await backupTask.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    }
                    catch (OperationCanceledException)
                    {
                        // 取消成功时的预期异常 / Expected when cancellation succeeds
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
    /// 获取备份操作的状态 / Gets the status of a backup operation
    /// </summary>
    /// <param name="operationId">操作ID / Operation ID</param>
    /// <returns>备份进度信息，如果未找到则返回null / Backup progress information, or null if not found</returns>
    public async Task<BackupProgress?> GetBackupStatusAsync(Guid operationId)
    {
        if (_disposed)
            return null;

        await Task.CompletedTask; // 使方法异步 / Make method async

        if (_runningTasks.TryGetValue(operationId, out var backupTask))
        {
            return backupTask.Progress;
        }

        return null;
    }

    /// <summary>
    /// 获取所有当前正在运行的备份操作 / Gets all currently running backup operations
    /// </summary>
    /// <returns>正在运行的备份操作进度列表 / List of running backup operation progress</returns>
    public async Task<IEnumerable<BackupProgress>> GetRunningBackupsAsync()
    {
        if (_disposed)
            return Enumerable.Empty<BackupProgress>();

        await Task.CompletedTask; // 使方法异步 / Make method async

        return _runningTasks.Values
            .Where(task => !task.IsCompleted)
            .Select(task => task.Progress)
            .ToList();
    }

    /// <summary>
    /// 获取后台任务执行的当前统计信息 / Gets current statistics about background task execution
    /// </summary>
    /// <returns>后台任务统计信息 / Background task statistics</returns>
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
    /// 以线程安全的方式更新统计信息 / Updates statistics in a thread-safe manner
    /// </summary>
    /// <param name="updateAction">更新操作 / Update action</param>
    private void UpdateStatistics(Action<BackgroundTaskStatistics> updateAction)
    {
        lock (_statisticsLock)
        {
            updateAction(_statistics);
        }
    }

    /// <summary>
    /// 更新平均执行时间 / Updates the average execution time
    /// </summary>
    /// <param name="newDuration">新的执行时间 / New execution duration</param>
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
    /// 定期清理已完成的任务 / Cleans up completed tasks periodically
    /// </summary>
    /// <param name="state">定时器状态参数 / Timer state parameter</param>
    private void CleanupCompletedTasks(object? state)
    {
        if (_disposed || !_configuration.AutoCleanupCompletedTasks)
            return;

        try
        {
            var cutoffTime = DateTime.Now.AddMinutes(-_configuration.CompletedTaskRetentionMinutes);
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
    /// 释放资源 / Disposes resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            // 取消所有正在运行的任务 / Cancel all running tasks
            var cancellationTasks = new List<Task>();
            foreach (var task in _runningTasks.Values)
            {
                task.CancellationTokenSource.Cancel();
                if (task.Task != null && !task.Task.IsCompleted)
                {
                    cancellationTasks.Add(task.Task);
                }
            }

            // 等待所有任务完成（带超时）/ Wait for all tasks to complete (with timeout)
            if (cancellationTasks.Count > 0)
            {
                try
                {
                    Task.WaitAll(cancellationTasks.ToArray(), TimeSpan.FromSeconds(30));
                }
                catch (AggregateException)
                {
                    // 任务被取消时的预期异常 / Expected when tasks are cancelled
                }
            }

            // 释放资源 / Dispose resources
            _cleanupTimer?.Dispose();
            _concurrencyLimiter?.Dispose();

            // 释放所有任务取消令牌 / Dispose all task cancellation tokens
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