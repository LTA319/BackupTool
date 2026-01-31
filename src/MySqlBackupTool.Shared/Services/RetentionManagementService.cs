using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 管理保留策略和自动清理的服务 / Service for managing retention policies and automatic cleanup
/// 提供备份文件和日志的自动清理、策略管理和影响评估功能 / Provides automatic cleanup of backup files and logs, policy management and impact assessment
/// </summary>
public class RetentionManagementService : IRetentionPolicyService
{
    private readonly IRetentionPolicyRepository _retentionPolicyRepository;
    private readonly IBackupLogRepository _backupLogRepository;
    private readonly ILogger<RetentionManagementService> _logger;
    private readonly RetentionPolicyValidator _validator;

    /// <summary>
    /// 初始化保留管理服务 / Initialize retention management service
    /// </summary>
    /// <param name="retentionPolicyRepository">保留策略仓储 / Retention policy repository</param>
    /// <param name="backupLogRepository">备份日志仓储 / Backup log repository</param>
    /// <param name="logger">日志记录器 / Logger instance</param>
    /// <exception cref="ArgumentNullException">当任何参数为null时抛出 / Thrown when any parameter is null</exception>
    public RetentionManagementService(
        IRetentionPolicyRepository retentionPolicyRepository,
        IBackupLogRepository backupLogRepository,
        ILogger<RetentionManagementService> logger)
    {
        _retentionPolicyRepository = retentionPolicyRepository ?? throw new ArgumentNullException(nameof(retentionPolicyRepository));
        _backupLogRepository = backupLogRepository ?? throw new ArgumentNullException(nameof(backupLogRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _validator = new RetentionPolicyValidator();
    }

    /// <summary>
    /// 执行所有启用的保留策略 / Executes all enabled retention policies
    /// 自动清理过期的备份文件和日志记录 / Automatically cleans up expired backup files and log records
    /// </summary>
    /// <returns>保留策略执行结果 / Retention policy execution result</returns>
    public async Task<RetentionExecutionResult> ExecuteRetentionPoliciesAsync()
    {
        _logger.LogInformation("Starting retention policy execution");

        var result = new RetentionExecutionResult
        {
            ExecutedAt = DateTime.UtcNow
        };

        var startTime = DateTime.UtcNow;

        try
        {
            // 获取所有启用的保留策略 / Get all enabled retention policies
            var enabledPolicies = await _retentionPolicyRepository.GetEnabledPoliciesAsync();
            var policiesList = enabledPolicies.ToList();

            if (!policiesList.Any())
            {
                _logger.LogInformation("No enabled retention policies found");
                result.Duration = DateTime.UtcNow - startTime;
                return result;
            }

            result.AppliedPolicies = policiesList;
            _logger.LogInformation("Found {PolicyCount} enabled retention policies", policiesList.Count);

            // 为每个策略执行日志清理 / Execute log cleanup for each policy
            foreach (var policy in policiesList)
            {
                try
                {
                    _logger.LogInformation("Applying retention policy: {PolicyName}", policy.Name);

                    var policyResult = await ApplyRetentionPolicyAsync(policy);
                    
                    result.LogsDeleted += policyResult.LogsDeleted;
                    result.FilesDeleted += policyResult.FilesDeleted;
                    result.BytesFreed += policyResult.BytesFreed;
                    result.DeletedFiles.AddRange(policyResult.DeletedFiles);

                    if (policyResult.Errors.Any())
                    {
                        result.Errors.AddRange(policyResult.Errors.Select(e => $"Policy '{policy.Name}': {e}"));
                    }

                    _logger.LogInformation("Policy '{PolicyName}' completed: {LogsDeleted} logs, {FilesDeleted} files, {BytesFreed} bytes freed",
                        policy.Name, policyResult.LogsDeleted, policyResult.FilesDeleted, policyResult.BytesFreed);
                }
                catch (Exception ex)
                {
                    var errorMessage = $"Error applying retention policy '{policy.Name}': {ex.Message}";
                    result.Errors.Add(errorMessage);
                    _logger.LogError(ex, "Error applying retention policy {PolicyName}", policy.Name);
                }
            }

            result.Duration = DateTime.UtcNow - startTime;

            if (result.Success)
            {
                _logger.LogInformation("Retention policy execution completed successfully: {LogsDeleted} logs, {FilesDeleted} files, {BytesFreed} bytes freed",
                    result.LogsDeleted, result.FilesDeleted, result.BytesFreed);
            }
            else
            {
                _logger.LogWarning("Retention policy execution completed with {ErrorCount} errors", result.Errors.Count);
            }

            return result;
        }
        catch (Exception ex)
        {
            result.Duration = DateTime.UtcNow - startTime;
            result.Errors.Add($"Critical error during retention execution: {ex.Message}");
            _logger.LogError(ex, "Critical error during retention policy execution");
            return result;
        }
    }

    /// <summary>
    /// 应用指定的保留策略 / Applies a specific retention policy
    /// 根据策略配置清理过期的备份文件和日志 / Cleans up expired backup files and logs based on policy configuration
    /// </summary>
    /// <param name="policy">要应用的保留策略 / Retention policy to apply</param>
    /// <returns>保留策略执行结果 / Retention policy execution result</returns>
    /// <exception cref="ArgumentNullException">当policy为null时抛出 / Thrown when policy is null</exception>
    public async Task<RetentionExecutionResult> ApplyRetentionPolicyAsync(RetentionPolicy policy)
    {
        if (policy == null)
            throw new ArgumentNullException(nameof(policy));

        if (!policy.IsEnabled)
        {
            _logger.LogWarning("Attempted to apply disabled retention policy: {PolicyName}", policy.Name);
            return new RetentionExecutionResult();
        }

        _logger.LogDebug("Applying retention policy: {PolicyName} - {PolicyDescription}", 
            policy.Name, policy.GetPolicyDescription());

        var result = new RetentionExecutionResult
        {
            ExecutedAt = DateTime.UtcNow,
            AppliedPolicies = new List<RetentionPolicy> { policy }
        };

        var startTime = DateTime.UtcNow;

        try
        {
            // 根据策略应用日志清理 / Apply log cleanup based on policy
            var logsDeleted = 0;

            if (policy.MaxAgeDays.HasValue)
            {
                logsDeleted += await _backupLogRepository.CleanupOldLogsAsync(policy.MaxAgeDays.Value, policy.MaxCount);
            }
            else if (policy.MaxCount.HasValue)
            {
                // 如果只指定了最大数量，使用一个非常大的年龄（实际上无限制） / If only max count is specified, use a very large age (effectively unlimited)
                logsDeleted += await _backupLogRepository.CleanupOldLogsAsync(int.MaxValue, policy.MaxCount.Value);
            }

            result.LogsDeleted = logsDeleted;

            // 根据策略应用文件清理 / Apply file cleanup based on policy
            var fileCleanupResult = await ApplyFileRetentionPolicyAsync(policy);
            result.FilesDeleted = fileCleanupResult.FilesDeleted;
            result.BytesFreed = fileCleanupResult.BytesFreed;
            result.DeletedFiles = fileCleanupResult.DeletedFiles;
            result.Errors.AddRange(fileCleanupResult.Errors);

            result.Duration = DateTime.UtcNow - startTime;

            _logger.LogDebug("Retention policy '{PolicyName}' applied: {LogsDeleted} logs, {FilesDeleted} files deleted",
                policy.Name, result.LogsDeleted, result.FilesDeleted);

            return result;
        }
        catch (Exception ex)
        {
            result.Duration = DateTime.UtcNow - startTime;
            result.Errors.Add($"Error applying retention policy: {ex.Message}");
            _logger.LogError(ex, "Error applying retention policy {PolicyName}", policy.Name);
            return result;
        }
    }

    /// <summary>
    /// 对备份文件应用文件保留策略 / Applies file retention policy to backup files
    /// 根据策略规则删除不需要保留的备份文件 / Deletes backup files that don't need to be retained based on policy rules
    /// </summary>
    /// <param name="policy">保留策略 / Retention policy</param>
    /// <returns>保留结果 / Retention result</returns>
    private async Task<RetentionResult> ApplyFileRetentionPolicyAsync(RetentionPolicy policy)
    {
        var result = new RetentionResult();

        try
        {
            // 获取所有带有文件信息的备份日志 / Get all backup logs with file information
            var allBackupLogs = await _backupLogRepository.GetAllAsync();
            var backupLogsWithFiles = allBackupLogs
                .Where(bl => !string.IsNullOrEmpty(bl.FilePath) && bl.FileSize.HasValue)
                .OrderByDescending(bl => bl.StartTime)
                .ToList();

            if (!backupLogsWithFiles.Any())
            {
                _logger.LogDebug("No backup files found for retention policy application");
                return result;
            }

            var filesToDelete = new List<BackupLog>();
            var currentBackupCount = backupLogsWithFiles.Count;
            var currentStorageUsed = backupLogsWithFiles.Sum(bl => bl.FileSize ?? 0);

            // 应用保留逻辑 / Apply retention logic
            foreach (var backupLog in backupLogsWithFiles)
            {
                var shouldRetain = policy.ShouldRetainBackup(
                    backupLog.StartTime,
                    currentBackupCount,
                    currentStorageUsed,
                    backupLog.FileSize ?? 0);

                if (!shouldRetain)
                {
                    filesToDelete.Add(backupLog);
                    currentBackupCount--;
                    currentStorageUsed -= backupLog.FileSize ?? 0;
                }
            }

            // 删除文件 / Delete the files
            foreach (var backupLog in filesToDelete)
            {
                try
                {
                    if (!string.IsNullOrEmpty(backupLog.FilePath) && File.Exists(backupLog.FilePath))
                    {
                        var fileInfo = new FileInfo(backupLog.FilePath);
                        var fileSize = fileInfo.Length;
                        
                        File.Delete(backupLog.FilePath);
                        
                        result.FilesDeleted++;
                        result.BytesFreed += fileSize;
                        result.DeletedFiles.Add(backupLog.FilePath);

                        _logger.LogDebug("Deleted backup file: {FilePath} ({FileSize} bytes)", 
                            backupLog.FilePath, fileSize);
                    }
                }
                catch (Exception ex)
                {
                    var errorMessage = $"Error deleting file {backupLog.FilePath}: {ex.Message}";
                    result.Errors.Add(errorMessage);
                    _logger.LogWarning(ex, "Error deleting backup file {FilePath}", backupLog.FilePath);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error applying file retention policy: {ex.Message}");
            _logger.LogError(ex, "Error applying file retention policy");
            return result;
        }
    }

    /// <summary>
    /// 创建新的保留策略并进行验证 / Creates a new retention policy with validation
    /// 验证策略配置并检查名称唯一性 / Validates policy configuration and checks name uniqueness
    /// </summary>
    /// <param name="policy">要创建的保留策略 / Retention policy to create</param>
    /// <returns>创建的保留策略 / Created retention policy</returns>
    /// <exception cref="ArgumentNullException">当policy为null时抛出 / Thrown when policy is null</exception>
    /// <exception cref="InvalidOperationException">当策略名称已存在时抛出 / Thrown when policy name already exists</exception>
    public async Task<RetentionPolicy> CreateRetentionPolicyAsync(RetentionPolicy policy)
    {
        if (policy == null)
            throw new ArgumentNullException(nameof(policy));

        _logger.LogInformation("Creating new retention policy: {PolicyName}", policy.Name);

        // 验证策略 / Validate policy
        await ValidateRetentionPolicyInternalAsync(policy);

        // 检查名称唯一性 / Check name uniqueness
        var isUnique = await _retentionPolicyRepository.IsNameUniqueAsync(policy.Name);
        if (!isUnique)
        {
            throw new InvalidOperationException($"A retention policy with the name '{policy.Name}' already exists");
        }

        // 设置创建时间 / Set creation time
        policy.CreatedAt = DateTime.UtcNow;

        var result = await _retentionPolicyRepository.AddAsync(policy);
        await _retentionPolicyRepository.SaveChangesAsync();

        _logger.LogInformation("Created retention policy: {PolicyName} (ID: {PolicyId})", 
            result.Name, result.Id);

        return result;
    }

    /// <summary>
    /// 更新现有的保留策略 / Updates an existing retention policy
    /// 验证策略配置并检查名称唯一性（排除当前策略） / Validates policy configuration and checks name uniqueness (excluding current policy)
    /// </summary>
    /// <param name="policy">要更新的保留策略 / Retention policy to update</param>
    /// <returns>更新后的保留策略 / Updated retention policy</returns>
    /// <exception cref="ArgumentNullException">当policy为null时抛出 / Thrown when policy is null</exception>
    /// <exception cref="InvalidOperationException">当策略名称已存在时抛出 / Thrown when policy name already exists</exception>
    public async Task<RetentionPolicy> UpdateRetentionPolicyAsync(RetentionPolicy policy)
    {
        if (policy == null)
            throw new ArgumentNullException(nameof(policy));

        _logger.LogInformation("Updating retention policy: {PolicyName} (ID: {PolicyId})", 
            policy.Name, policy.Id);

        // 验证策略 / Validate policy
        await ValidateRetentionPolicyInternalAsync(policy);

        // 检查名称唯一性（排除当前策略） / Check name uniqueness (excluding current policy)
        var isUnique = await _retentionPolicyRepository.IsNameUniqueAsync(policy.Name, policy.Id);
        if (!isUnique)
        {
            throw new InvalidOperationException($"A retention policy with the name '{policy.Name}' already exists");
        }

        var result = await _retentionPolicyRepository.UpdateAsync(policy);
        await _retentionPolicyRepository.SaveChangesAsync();

        _logger.LogInformation("Updated retention policy: {PolicyName} (ID: {PolicyId})", 
            result.Name, result.Id);

        return result;
    }

    /// <summary>
    /// 删除保留策略 / Deletes a retention policy
    /// </summary>
    /// <param name="policyId">策略ID / Policy ID</param>
    /// <returns>删除成功返回true，失败返回false / Returns true if deleted successfully, false if failed</returns>
    public async Task<bool> DeleteRetentionPolicyAsync(int policyId)
    {
        _logger.LogInformation("Deleting retention policy with ID: {PolicyId}", policyId);

        try
        {
            var policy = await _retentionPolicyRepository.GetByIdAsync(policyId);
            if (policy == null)
            {
                _logger.LogWarning("Retention policy with ID {PolicyId} not found", policyId);
                return false;
            }

            await _retentionPolicyRepository.DeleteAsync(policyId);
            await _retentionPolicyRepository.SaveChangesAsync();

            _logger.LogInformation("Deleted retention policy: {PolicyName} (ID: {PolicyId})", 
                policy.Name, policyId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting retention policy with ID {PolicyId}", policyId);
            return false;
        }
    }

    /// <summary>
    /// 获取所有保留策略 / Gets all retention policies
    /// </summary>
    /// <returns>保留策略集合 / Collection of retention policies</returns>
    public async Task<IEnumerable<RetentionPolicy>> GetAllRetentionPoliciesAsync()
    {
        return await _retentionPolicyRepository.GetAllAsync();
    }

    /// <summary>
    /// 获取所有启用的保留策略 / Gets all enabled retention policies
    /// </summary>
    /// <returns>启用的保留策略集合 / Collection of enabled retention policies</returns>
    public async Task<IEnumerable<RetentionPolicy>> GetEnabledRetentionPoliciesAsync()
    {
        return await _retentionPolicyRepository.GetEnabledPoliciesAsync();
    }

    /// <summary>
    /// 根据ID获取保留策略 / Gets a retention policy by ID
    /// </summary>
    /// <param name="policyId">策略ID / Policy ID</param>
    /// <returns>保留策略或null（如果未找到） / Retention policy or null if not found</returns>
    public async Task<RetentionPolicy?> GetRetentionPolicyByIdAsync(int policyId)
    {
        return await _retentionPolicyRepository.GetByIdAsync(policyId);
    }

    /// <summary>
    /// 根据名称获取保留策略 / Gets a retention policy by name
    /// </summary>
    /// <param name="name">策略名称 / Policy name</param>
    /// <returns>保留策略或null（如果未找到） / Retention policy or null if not found</returns>
    public async Task<RetentionPolicy?> GetRetentionPolicyByNameAsync(string name)
    {
        return await _retentionPolicyRepository.GetByNameAsync(name);
    }

    /// <summary>
    /// 启用保留策略 / Enables a retention policy
    /// </summary>
    /// <param name="policyId">策略ID / Policy ID</param>
    /// <returns>启用成功返回true，失败返回false / Returns true if enabled successfully, false if failed</returns>
    public async Task<bool> EnableRetentionPolicyAsync(int policyId)
    {
        _logger.LogInformation("Enabling retention policy with ID: {PolicyId}", policyId);
        return await _retentionPolicyRepository.EnablePolicyAsync(policyId);
    }

    /// <summary>
    /// 禁用保留策略 / Disables a retention policy
    /// </summary>
    /// <param name="policyId">策略ID / Policy ID</param>
    /// <returns>禁用成功返回true，失败返回false / Returns true if disabled successfully, false if failed</returns>
    public async Task<bool> DisableRetentionPolicyAsync(int policyId)
    {
        _logger.LogInformation("Disabling retention policy with ID: {PolicyId}", policyId);
        return await _retentionPolicyRepository.DisablePolicyAsync(policyId);
    }

    /// <summary>
    /// 验证保留策略配置 / Validates a retention policy configuration
    /// </summary>
    /// <param name="policy">要验证的保留策略 / Retention policy to validate</param>
    /// <returns>验证结果和错误列表 / Validation result and error list</returns>
    public async Task<(bool IsValid, List<string> Errors)> ValidateRetentionPolicyAsync(RetentionPolicy policy)
    {
        var validationResult = _validator.ValidatePolicy(policy);
        
        // 为了向后兼容，将警告添加到错误中 / Add warnings to errors for backward compatibility
        var allErrors = new List<string>();
        allErrors.AddRange(validationResult.Errors);
        allErrors.AddRange(validationResult.Warnings.Select(w => $"Warning: {w}"));
        
        return (validationResult.IsValid, allErrors);
    }

    /// <summary>
    /// 估算应用保留策略的影响 / Estimates the impact of applying a retention policy
    /// 预测将删除的文件数量和释放的存储空间 / Predicts the number of files to be deleted and storage space to be freed
    /// </summary>
    /// <param name="policy">要评估的保留策略 / Retention policy to evaluate</param>
    /// <returns>保留影响估算结果 / Retention impact estimate result</returns>
    public async Task<RetentionImpactEstimate> EstimateRetentionImpactAsync(RetentionPolicy policy)
    {
        var estimate = new RetentionImpactEstimate();

        try
        {
            // 获取所有带有文件信息的备份日志 / Get all backup logs with file information
            var allBackupLogs = await _backupLogRepository.GetAllAsync();
            var backupLogsWithFiles = allBackupLogs
                .Where(bl => !string.IsNullOrEmpty(bl.FilePath) && bl.FileSize.HasValue)
                .OrderByDescending(bl => bl.StartTime)
                .ToList();

            if (!backupLogsWithFiles.Any())
            {
                return estimate;
            }

            var filesToDelete = new List<BackupLog>();
            var currentBackupCount = backupLogsWithFiles.Count;
            var currentStorageUsed = backupLogsWithFiles.Sum(bl => bl.FileSize ?? 0);

            // 应用保留逻辑来估算影响 / Apply retention logic to estimate impact
            foreach (var backupLog in backupLogsWithFiles)
            {
                var shouldRetain = policy.ShouldRetainBackup(
                    backupLog.StartTime,
                    currentBackupCount,
                    currentStorageUsed,
                    backupLog.FileSize ?? 0);

                if (!shouldRetain)
                {
                    filesToDelete.Add(backupLog);
                    currentBackupCount--;
                    currentStorageUsed -= backupLog.FileSize ?? 0;
                }
            }

            estimate.EstimatedFilesToDelete = filesToDelete.Count;
            estimate.EstimatedLogsToDelete = filesToDelete.Count;
            estimate.EstimatedBytesToFree = filesToDelete.Sum(bl => bl.FileSize ?? 0);
            estimate.FilesToDelete = filesToDelete
                .Where(bl => !string.IsNullOrEmpty(bl.FilePath))
                .Select(bl => bl.FilePath!)
                .ToList();

            // 为大量删除添加警告 / Add warnings for large deletions
            if (estimate.EstimatedFilesToDelete > 10)
            {
                estimate.Warnings.Add($"This policy will delete {estimate.EstimatedFilesToDelete} backup files");
            }

            if (estimate.EstimatedBytesToFree > 10L * 1024 * 1024 * 1024) // 10GB
            {
                var sizeStr = FormatBytes(estimate.EstimatedBytesToFree);
                estimate.Warnings.Add($"This policy will free {sizeStr} of storage space");
            }

            return estimate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error estimating retention policy impact");
            estimate.Warnings.Add($"Error estimating impact: {ex.Message}");
            return estimate;
        }
    }

    /// <summary>
    /// 保留策略的内部验证方法 / Internal validation method for retention policies
    /// </summary>
    /// <param name="policy">要验证的保留策略 / Retention policy to validate</param>
    /// <exception cref="ArgumentException">当策略验证失败时抛出 / Thrown when policy validation fails</exception>
    private async Task ValidateRetentionPolicyInternalAsync(RetentionPolicy policy)
    {
        var validationResult = _validator.ValidatePolicy(policy);
        
        if (!validationResult.IsValid)
        {
            throw new ArgumentException($"Retention policy validation failed: {string.Join(", ", validationResult.Errors)}");
        }
        
        // 记录警告 / Log warnings
        foreach (var warning in validationResult.Warnings)
        {
            _logger.LogWarning("Retention policy validation warning: {Warning}", warning);
        }
    }

    /// <summary>
    /// 根据当前备份模式获取保留策略建议 / Gets retention policy recommendations based on current backup patterns
    /// 分析备份历史并生成适合的保留策略建议 / Analyzes backup history and generates suitable retention policy recommendations
    /// </summary>
    /// <returns>保留策略建议列表 / List of retention policy recommendations</returns>
    public async Task<List<RetentionPolicy>> GetRetentionPolicyRecommendationsAsync()
    {
        _logger.LogInformation("Generating retention policy recommendations");

        var recommendations = new List<RetentionPolicy>();

        try
        {
            // 分析当前备份模式 / Analyze current backup patterns
            var recentLogs = await _backupLogRepository.GetByDateRangeAsync(
                DateTime.UtcNow.AddDays(-90), DateTime.UtcNow);

            var backupLogsList = recentLogs.ToList();
            
            if (!backupLogsList.Any())
            {
                // 如果没有备份历史，返回默认建议 / Default recommendations if no backup history
                recommendations.AddRange(GetDefaultRetentionPolicies());
                return recommendations;
            }

            var totalBackups = backupLogsList.Count;
            var averageBackupsPerDay = totalBackups / 90.0;
            var totalStorage = backupLogsList.Where(bl => bl.FileSize.HasValue).Sum(bl => bl.FileSize!.Value);
            var averageBackupSize = backupLogsList.Where(bl => bl.FileSize.HasValue).Any() 
                ? backupLogsList.Where(bl => bl.FileSize.HasValue).Average(bl => bl.FileSize!.Value)
                : 0;

            // 保守策略（保留更多备份） / Conservative policy (keep more backups)
            recommendations.Add(new RetentionPolicy
            {
                Name = "Conservative (Recommended)",
                Description = "Keeps backups for 90 days or up to 100 backups, whichever is more restrictive",
                MaxAgeDays = 90,
                MaxCount = Math.Max(100, (int)(averageBackupsPerDay * 90)),
                IsEnabled = false
            });

            // 平衡策略 / Balanced policy
            recommendations.Add(new RetentionPolicy
            {
                Name = "Balanced",
                Description = "Keeps backups for 30 days or up to 50 backups",
                MaxAgeDays = 30,
                MaxCount = Math.Max(50, (int)(averageBackupsPerDay * 30)),
                IsEnabled = false
            });

            // 激进策略（保留较少备份） / Aggressive policy (keep fewer backups)
            recommendations.Add(new RetentionPolicy
            {
                Name = "Aggressive",
                Description = "Keeps backups for 7 days or up to 20 backups",
                MaxAgeDays = 7,
                MaxCount = Math.Max(20, (int)(averageBackupsPerDay * 7)),
                IsEnabled = false
            });

            // 基于存储的策略 / Storage-based policy
            if (totalStorage > 0)
            {
                var recommendedStorageLimit = (long)(totalStorage * 1.5); // 50%缓冲 / 50% buffer
                recommendations.Add(new RetentionPolicy
                {
                    Name = "Storage-Based",
                    Description = $"Limits storage usage to approximately {FormatBytes(recommendedStorageLimit)}",
                    MaxStorageBytes = recommendedStorageLimit,
                    IsEnabled = false
                });
            }

            _logger.LogInformation("Generated {RecommendationCount} retention policy recommendations", 
                recommendations.Count);

            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating retention policy recommendations");
            return GetDefaultRetentionPolicies();
        }
    }

    /// <summary>
    /// 获取默认保留策略 / Gets default retention policies
    /// </summary>
    /// <returns>默认保留策略列表 / List of default retention policies</returns>
    private List<RetentionPolicy> GetDefaultRetentionPolicies()
    {
        return new List<RetentionPolicy>
        {
            new RetentionPolicy
            {
                Name = "Default - 30 Days",
                Description = "Keep backups for 30 days",
                MaxAgeDays = 30,
                IsEnabled = false
            },
            new RetentionPolicy
            {
                Name = "Default - 50 Backups",
                Description = "Keep the most recent 50 backups",
                MaxCount = 50,
                IsEnabled = false
            }
        };
    }

    /// <summary>
    /// 格式化字节数为人类可读的字符串 / Formats bytes to human-readable string
    /// </summary>
    /// <param name="bytes">字节数 / Number of bytes</param>
    /// <returns>格式化的字符串 / Formatted string</returns>
    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";
        
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}