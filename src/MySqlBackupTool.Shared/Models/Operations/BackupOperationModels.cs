using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// 表示一个备份操作记录
/// 包含备份操作的完整信息和状态
/// </summary>
public class BackupOperation
{
    /// <summary>
    /// 备份操作的唯一标识符
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// 数据库名称，必填项
    /// </summary>
    [Required]
    public string DatabaseName { get; set; } = string.Empty;
    
    /// <summary>
    /// 备份文件路径，必填项
    /// </summary>
    [Required]
    public string BackupPath { get; set; } = string.Empty;
    
    /// <summary>
    /// 备份开始时间
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// 备份结束时间（可选）
    /// </summary>
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// 备份状态
    /// </summary>
    public BackupStatus Status { get; set; } = BackupStatus.Queued;
    
    /// <summary>
    /// 备份文件大小（字节）
    /// </summary>
    public long FileSize { get; set; }
    
    /// <summary>
    /// 压缩比率
    /// </summary>
    public double CompressionRatio { get; set; }
    
    /// <summary>
    /// 错误消息（如果备份失败）
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 备份类型
    /// </summary>
    public BackupType BackupType { get; set; } = BackupType.Full;
    
    /// <summary>
    /// 校验和哈希值
    /// </summary>
    public string? ChecksumHash { get; set; }
    
    // 计算属性
    
    /// <summary>
    /// 备份持续时间
    /// </summary>
    public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;
    
    /// <summary>
    /// 备份是否已完成
    /// </summary>
    public bool IsCompleted => Status == BackupStatus.Completed;
    
    /// <summary>
    /// 是否有错误
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
}

/// <summary>
/// 备份操作的类型
/// 定义不同的备份策略
/// </summary>
public enum BackupType
{
    /// <summary>
    /// 完整备份 - 备份所有数据
    /// </summary>
    Full,
    
    /// <summary>
    /// 增量备份 - 只备份自上次备份以来的更改
    /// </summary>
    Incremental,
    
    /// <summary>
    /// 差异备份 - 备份自上次完整备份以来的更改
    /// </summary>
    Differential,
    
    /// <summary>
    /// 事务日志备份 - 只备份事务日志
    /// </summary>
    Transaction
}

/// <summary>
/// 备份操作的结果
/// 包含操作完成后的详细信息
/// </summary>
public class BackupResult
{
    /// <summary>
    /// 操作唯一标识符
    /// </summary>
    public Guid OperationId { get; set; }
    
    /// <summary>
    /// 操作是否成功
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// 错误消息（如果操作失败）
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 备份文件路径
    /// </summary>
    public string? BackupFilePath { get; set; }
    
    /// <summary>
    /// 备份文件大小（字节）
    /// </summary>
    public long FileSize { get; set; }
    
    /// <summary>
    /// 操作持续时间
    /// </summary>
    public TimeSpan Duration { get; set; }
    
    /// <summary>
    /// 压缩比率
    /// </summary>
    public double CompressionRatio { get; set; }
    
    /// <summary>
    /// 校验和哈希值
    /// </summary>
    public string? ChecksumHash { get; set; }
    
    /// <summary>
    /// 操作完成时间
    /// </summary>
    public DateTime CompletedAt { get; set; }
    
    /// <summary>
    /// 警告信息列表
    /// </summary>
    public IEnumerable<string> Warnings { get; set; } = new List<string>();
}

/// <summary>
/// 备份操作的进度信息
/// 提供实时的操作进度反馈
/// </summary>
public class BackupProgressInfo
{
    /// <summary>
    /// 操作唯一标识符
    /// </summary>
    public Guid OperationId { get; set; }
    
    /// <summary>
    /// 完成百分比（0-100）
    /// </summary>
    public int PercentComplete { get; set; }
    
    /// <summary>
    /// 当前执行步骤描述
    /// </summary>
    public string CurrentStep { get; set; } = string.Empty;
    
    /// <summary>
    /// 已处理的字节数
    /// </summary>
    public long BytesProcessed { get; set; }
    
    /// <summary>
    /// 总字节数
    /// </summary>
    public long TotalBytes { get; set; }
    
    /// <summary>
    /// 已用时间
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }
    
    /// <summary>
    /// 预计剩余时间
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    
    /// <summary>
    /// 状态消息
    /// </summary>
    public string? StatusMessage { get; set; }
}

/// <summary>
/// 备份配置的验证结果
/// 包含配置验证的详细信息
/// </summary>
public class BackupValidationResult
{
    /// <summary>
    /// 配置是否有效
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// 错误信息列表
    /// </summary>
    public IEnumerable<string> Errors { get; set; } = new List<string>();
    
    /// <summary>
    /// 警告信息列表
    /// </summary>
    public IEnumerable<string> Warnings { get; set; } = new List<string>();
    
    /// <summary>
    /// 建议信息列表
    /// </summary>
    public IEnumerable<string> Recommendations { get; set; } = new List<string>();
}