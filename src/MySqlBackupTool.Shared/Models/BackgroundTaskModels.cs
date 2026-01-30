using System;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// 备份进度更新的事件参数
/// 用于通知备份操作的进度变化
/// </summary>
public class BackupProgressEventArgs : EventArgs
{
    /// <summary>
    /// 操作唯一标识符
    /// </summary>
    public Guid OperationId { get; set; }
    
    /// <summary>
    /// 当前备份进度信息
    /// </summary>
    public BackupProgress Progress { get; set; } = new();
    
    /// <summary>
    /// 事件发生的时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 备份完成的事件参数
/// 用于通知备份操作的完成状态
/// </summary>
public class BackupCompletedEventArgs : EventArgs
{
    /// <summary>
    /// 操作唯一标识符
    /// </summary>
    public Guid OperationId { get; set; }
    
    /// <summary>
    /// 备份操作的最终结果
    /// </summary>
    public BackupResult Result { get; set; } = new();
    
    /// <summary>
    /// 备份完成的时间
    /// </summary>
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 表示一个正在运行的备份任务
/// 包含任务的完整状态和控制信息
/// </summary>
public class BackupTask
{
    /// <summary>
    /// 操作唯一标识符
    /// </summary>
    public Guid OperationId { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// 备份配置信息
    /// </summary>
    public BackupConfiguration Configuration { get; set; } = new();
    
    /// <summary>
    /// 当前进度信息
    /// </summary>
    public BackupProgress Progress { get; set; } = new();
    
    /// <summary>
    /// 用于取消任务的令牌源
    /// </summary>
    public CancellationTokenSource CancellationTokenSource { get; set; } = new();
    
    /// <summary>
    /// 异步任务对象
    /// </summary>
    public Task<BackupResult>? Task { get; set; }
    
    /// <summary>
    /// 任务开始时间
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 任务是否已完成
    /// </summary>
    public bool IsCompleted => Task?.IsCompleted ?? false;
    
    /// <summary>
    /// 任务是否已被取消
    /// </summary>
    public bool IsCancelled => CancellationTokenSource.Token.IsCancellationRequested;
}

/// <summary>
/// 后台任务管理的配置选项
/// 定义任务执行的各种参数和限制
/// </summary>
public class BackgroundTaskConfiguration
{
    /// <summary>
    /// 最大并发备份操作数量
    /// 限制同时运行的备份任务数量以控制资源使用
    /// </summary>
    public int MaxConcurrentBackups { get; set; } = 2;

    /// <summary>
    /// 进度更新间隔（毫秒）
    /// 控制进度通知的频率
    /// </summary>
    public int ProgressUpdateIntervalMs { get; set; } = 1000;

    /// <summary>
    /// 备份操作超时时间（分钟）
    /// 默认8小时，超过此时间的备份将被终止
    /// </summary>
    public int BackupTimeoutMinutes { get; set; } = 480; // 8 hours default

    /// <summary>
    /// 是否自动清理已完成的任务
    /// 启用后会自动移除完成的任务信息
    /// </summary>
    public bool AutoCleanupCompletedTasks { get; set; } = true;

    /// <summary>
    /// 已完成任务信息的保留时间（分钟）
    /// 超过此时间的完成任务信息将被清理
    /// </summary>
    public int CompletedTaskRetentionMinutes { get; set; } = 60;
}

/// <summary>
/// 后台任务执行的统计信息
/// 提供任务执行情况的详细统计数据
/// </summary>
public class BackgroundTaskStatistics
{
    /// <summary>
    /// 总启动任务数
    /// </summary>
    public int TotalTasksStarted { get; set; }
    
    /// <summary>
    /// 已完成任务数
    /// </summary>
    public int TasksCompleted { get; set; }
    
    /// <summary>
    /// 失败任务数
    /// </summary>
    public int TasksFailed { get; set; }
    
    /// <summary>
    /// 取消任务数
    /// </summary>
    public int TasksCancelled { get; set; }
    
    /// <summary>
    /// 当前正在运行的任务数
    /// </summary>
    public int CurrentlyRunning { get; set; }
    
    /// <summary>
    /// 平均执行时间
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }
    
    /// <summary>
    /// 最后一个任务的启动时间
    /// </summary>
    public DateTime LastTaskStarted { get; set; }
    
    /// <summary>
    /// 最后一个任务的完成时间
    /// </summary>
    public DateTime LastTaskCompleted { get; set; }
}