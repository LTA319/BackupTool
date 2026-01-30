using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 编排完整备份工作流程的接口
/// Interface for orchestrating the complete backup workflow
/// </summary>
public interface IBackupOrchestrator
{
    /// <summary>
    /// 执行完整的备份工作流程
    /// Executes the complete backup workflow
    /// </summary>
    /// <param name="configuration">备份配置 / Backup configuration</param>
    /// <param name="progress">进度报告器 / Progress reporter</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>备份结果 / Backup result</returns>
    Task<BackupResult> ExecuteBackupAsync(BackupConfiguration configuration, IProgress<BackupProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 在执行前验证备份配置
    /// Validates a backup configuration before execution
    /// </summary>
    /// <param name="configuration">要验证的配置 / Configuration to validate</param>
    /// <returns>验证结果 / Validation result</returns>
    Task<BackupValidationResult> ValidateConfigurationAsync(BackupConfiguration configuration);
}