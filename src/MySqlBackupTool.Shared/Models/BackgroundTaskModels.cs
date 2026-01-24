using System;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// Event arguments for backup progress updates
/// </summary>
public class BackupProgressEventArgs : EventArgs
{
    public Guid OperationId { get; set; }
    public BackupProgress Progress { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event arguments for backup completion
/// </summary>
public class BackupCompletedEventArgs : EventArgs
{
    public Guid OperationId { get; set; }
    public BackupResult Result { get; set; } = new();
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a running backup task
/// </summary>
public class BackupTask
{
    public Guid OperationId { get; set; } = Guid.NewGuid();
    public BackupConfiguration Configuration { get; set; } = new();
    public BackupProgress Progress { get; set; } = new();
    public CancellationTokenSource CancellationTokenSource { get; set; } = new();
    public Task<BackupResult>? Task { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public bool IsCompleted => Task?.IsCompleted ?? false;
    public bool IsCancelled => CancellationTokenSource.Token.IsCancellationRequested;
}

/// <summary>
/// Configuration for background task management
/// </summary>
public class BackgroundTaskConfiguration
{
    /// <summary>
    /// Maximum number of concurrent backup operations
    /// </summary>
    public int MaxConcurrentBackups { get; set; } = 2;

    /// <summary>
    /// Progress update interval in milliseconds
    /// </summary>
    public int ProgressUpdateIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Timeout for backup operations in minutes
    /// </summary>
    public int BackupTimeoutMinutes { get; set; } = 480; // 8 hours default

    /// <summary>
    /// Whether to automatically clean up completed tasks
    /// </summary>
    public bool AutoCleanupCompletedTasks { get; set; } = true;

    /// <summary>
    /// How long to keep completed task information in minutes
    /// </summary>
    public int CompletedTaskRetentionMinutes { get; set; } = 60;
}

/// <summary>
/// Statistics about background task execution
/// </summary>
public class BackgroundTaskStatistics
{
    public int TotalTasksStarted { get; set; }
    public int TasksCompleted { get; set; }
    public int TasksFailed { get; set; }
    public int TasksCancelled { get; set; }
    public int CurrentlyRunning { get; set; }
    public TimeSpan AverageExecutionTime { get; set; }
    public DateTime LastTaskStarted { get; set; }
    public DateTime LastTaskCompleted { get; set; }
}