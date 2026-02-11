using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 保留策略验证器服务 / Service for validating retention policies and their configurations
/// 提供保留策略配置验证、策略冲突检查、安全性评估等功能 / Provides retention policy configuration validation, conflict checking, and safety assessment
/// </summary>
public class RetentionPolicyValidator
{
    /// <summary>
    /// 验证保留策略配置 / Validates a retention policy configuration
    /// 检查策略名称、描述、保留条件、范围值和逻辑一致性 / Checks policy name, description, retention criteria, range values, and logical consistency
    /// </summary>
    /// <param name="policy">要验证的保留策略 / The retention policy to validate</param>
    /// <returns>验证结果，包含错误和警告信息 / Validation result containing errors and warnings</returns>
    public ValidationResult ValidatePolicy(RetentionPolicy policy)
    {
        var result = new ValidationResult();

        if (policy == null)
        {
            result.AddError("Retention policy cannot be null");
            return result;
        }

        // Validate name
        ValidateName(policy.Name, result);

        // Validate description
        ValidateDescription(policy.Description, result);

        // Validate retention criteria
        ValidateRetentionCriteria(policy, result);

        // Validate ranges
        ValidateRanges(policy, result);

        // Validate logical consistency
        ValidateLogicalConsistency(policy, result);

        return result;
    }

    /// <summary>
    /// 验证多个策略是否存在冲突 / Validates multiple policies for conflicts
    /// 检查重复的策略名称和策略间的潜在冲突 / Checks for duplicate policy names and potential conflicts between policies
    /// </summary>
    /// <param name="policies">要验证的策略集合 / The collection of policies to validate</param>
    /// <returns>验证结果，包含冲突信息 / Validation result containing conflict information</returns>
    public ValidationResult ValidatePolicySet(IEnumerable<RetentionPolicy> policies)
    {
        var result = new ValidationResult();
        var policiesList = policies.ToList();

        // Check for duplicate names
        var duplicateNames = policiesList
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicateName in duplicateNames)
        {
            result.AddError($"Duplicate policy name found: '{duplicateName}'");
        }

        // Check for conflicting policies
        ValidatePolicyConflicts(policiesList, result);

        return result;
    }

    /// <summary>
    /// 验证策略应用的安全性（不会删除过多数据） / Validates if a policy is safe to apply (won't delete too much data)
    /// 检查策略是否会删除超过90%的备份或存储空间，以及是否设置了过于激进的保留期限 / Checks if policy would delete more than 90% of backups or storage, and if retention period is too aggressive
    /// </summary>
    /// <param name="policy">要验证的保留策略 / The retention policy to validate</param>
    /// <param name="currentStorageUsed">当前存储使用量（字节） / Current storage usage in bytes</param>
    /// <param name="currentBackupCount">当前备份文件数量 / Current number of backup files</param>
    /// <returns>安全性验证结果 / Safety validation result</returns>
    public ValidationResult ValidatePolicySafety(RetentionPolicy policy, long currentStorageUsed, int currentBackupCount)
    {
        var result = new ValidationResult();

        // Check if policy would delete more than 90% of backups
        if (policy.MaxCount.HasValue && currentBackupCount > 0)
        {
            var retentionRatio = (double)policy.MaxCount.Value / currentBackupCount;
            if (retentionRatio < 0.1) // Would keep less than 10%
            {
                result.AddWarning($"Policy would delete {100 - (retentionRatio * 100):F1}% of existing backups");
            }
        }

        // Check if policy would delete more than 90% of storage
        if (policy.MaxStorageBytes.HasValue && currentStorageUsed > 0)
        {
            var storageRatio = (double)policy.MaxStorageBytes.Value / currentStorageUsed;
            if (storageRatio < 0.1) // Would keep less than 10%
            {
                var currentStr = FormatBytes(currentStorageUsed);
                var limitStr = FormatBytes(policy.MaxStorageBytes.Value);
                result.AddWarning($"Policy would reduce storage from {currentStr} to {limitStr}");
            }
        }

        // Check for very aggressive age policies
        if (policy.MaxAgeDays.HasValue && policy.MaxAgeDays.Value < 7)
        {
            result.AddWarning($"Policy has very short retention period: {policy.MaxAgeDays.Value} days");
        }

        return result;
    }

    /// <summary>
    /// 验证策略名称的有效性 / Validates the validity of policy name
    /// 检查名称是否为空、长度是否超限、是否包含无效字符等 / Checks if name is empty, exceeds length limit, contains invalid characters, etc.
    /// </summary>
    /// <param name="name">策略名称 / Policy name</param>
    /// <param name="result">验证结果对象 / Validation result object</param>
    private void ValidateName(string? name, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            result.AddError("Policy name is required");
            return;
        }

        if (name.Length > 100)
        {
            result.AddError("Policy name cannot exceed 100 characters");
        }

        if (name.Trim() != name)
        {
            result.AddWarning("Policy name has leading or trailing whitespace");
        }

        // Check for invalid characters
        var invalidChars = new[] { '<', '>', ':', '"', '|', '?', '*', '\\', '/' };
        if (name.Any(c => invalidChars.Contains(c)))
        {
            result.AddError("Policy name contains invalid characters");
        }
    }

    /// <summary>
    /// 验证策略描述的有效性 / Validates the validity of policy description
    /// 检查描述长度是否超过限制 / Checks if description length exceeds limit
    /// </summary>
    /// <param name="description">策略描述 / Policy description</param>
    /// <param name="result">验证结果对象 / Validation result object</param>
    private void ValidateDescription(string? description, ValidationResult result)
    {
        if (!string.IsNullOrEmpty(description) && description.Length > 500)
        {
            result.AddError("Policy description cannot exceed 500 characters");
        }
    }

    /// <summary>
    /// 验证保留条件是否已设置 / Validates retention criteria are set
    /// 确保至少设置了一个保留条件（最大天数、最大数量或最大存储空间） / Ensures at least one retention criteria is set (MaxAgeDays, MaxCount, or MaxStorageBytes)
    /// </summary>
    /// <param name="policy">保留策略 / Retention policy</param>
    /// <param name="result">验证结果对象 / Validation result object</param>
    private void ValidateRetentionCriteria(RetentionPolicy policy, ValidationResult result)
    {
        var hasCriteria = policy.MaxAgeDays.HasValue || 
                         policy.MaxCount.HasValue || 
                         policy.MaxStorageBytes.HasValue;

        if (!hasCriteria)
        {
            result.AddError("At least one retention criteria (MaxAgeDays, MaxCount, or MaxStorageBytes) must be specified");
        }
    }

    /// <summary>
    /// 验证数值范围的合理性 / Validates the reasonableness of value ranges
    /// 检查最大天数、最大数量、最大存储空间是否在合理范围内 / Checks if MaxAgeDays, MaxCount, MaxStorageBytes are within reasonable ranges
    /// </summary>
    /// <param name="policy">保留策略 / Retention policy</param>
    /// <param name="result">验证结果对象 / Validation result object</param>
    private void ValidateRanges(RetentionPolicy policy, ValidationResult result)
    {
        if (policy.MaxAgeDays.HasValue && policy.MaxAgeDays.Value < 1)
        {
            result.AddError("MaxAgeDays must be at least 1");
        }

        if (policy.MaxCount.HasValue && policy.MaxCount.Value < 1)
        {
            result.AddError("MaxCount must be at least 1");
        }

        if (policy.MaxStorageBytes.HasValue && policy.MaxStorageBytes.Value < 1)
        {
            result.AddError("MaxStorageBytes must be at least 1");
        }

        // Check for unreasonably large values
        if (policy.MaxAgeDays.HasValue && policy.MaxAgeDays.Value > 3650) // 10 years
        {
            result.AddWarning("MaxAgeDays is very large (over 10 years)");
        }

        if (policy.MaxCount.HasValue && policy.MaxCount.Value > 10000)
        {
            result.AddWarning("MaxCount is very large (over 10,000 backups)");
        }

        if (policy.MaxStorageBytes.HasValue && policy.MaxStorageBytes.Value > 100L * 1024 * 1024 * 1024 * 1024) // 100TB
        {
            result.AddWarning("MaxStorageBytes is very large (over 100TB)");
        }
    }

    /// <summary>
    /// 验证策略的逻辑一致性 / Validates logical consistency of the policy
    /// 检查年龄和数量策略之间是否可能存在冲突 / Checks if age and count policies might conflict with each other
    /// </summary>
    /// <param name="policy">保留策略 / Retention policy</param>
    /// <param name="result">验证结果对象 / Validation result object</param>
    private void ValidateLogicalConsistency(RetentionPolicy policy, ValidationResult result)
    {
        // Check if age and count policies might conflict
        if (policy.MaxAgeDays.HasValue && policy.MaxCount.HasValue)
        {
            // If someone backs up daily, MaxAgeDays should be >= MaxCount
            if (policy.MaxAgeDays.Value < policy.MaxCount.Value)
            {
                result.AddWarning($"MaxAgeDays ({policy.MaxAgeDays.Value}) is less than MaxCount ({policy.MaxCount.Value}). " +
                                "This might cause unexpected behavior with daily backups.");
            }
        }
    }

    /// <summary>
    /// 验证策略集合中的冲突 / Validates conflicts in policy set
    /// 检查多个启用的策略是否可能产生意外的交互 / Checks if multiple enabled policies might interact unexpectedly
    /// </summary>
    /// <param name="policies">策略列表 / List of policies</param>
    /// <param name="result">验证结果对象 / Validation result object</param>
    private void ValidatePolicyConflicts(List<RetentionPolicy> policies, ValidationResult result)
    {
        var enabledPolicies = policies.Where(p => p.IsEnabled).ToList();

        if (enabledPolicies.Count > 1)
        {
            // Check if multiple enabled policies might conflict
            var hasAgePolicies = enabledPolicies.Any(p => p.MaxAgeDays.HasValue);
            var hasCountPolicies = enabledPolicies.Any(p => p.MaxCount.HasValue);
            var hasStoragePolicies = enabledPolicies.Any(p => p.MaxStorageBytes.HasValue);

            if (hasAgePolicies && hasCountPolicies)
            {
                result.AddWarning("Multiple enabled policies with different criteria types may interact unexpectedly");
            }
        }
    }

    /// <summary>
    /// 格式化字节数为可读的字符串 / Formats bytes to readable string
    /// 将字节数转换为带单位的可读格式（B, KB, MB, GB, TB） / Converts bytes to readable format with units (B, KB, MB, GB, TB)
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

/// <summary>
/// 验证操作的结果 / Result of a validation operation
/// 包含验证过程中发现的错误和警告信息 / Contains errors and warnings found during validation
/// </summary>
public class ValidationResult
{
    /// <summary>错误列表 / List of errors</summary>
    public List<string> Errors { get; } = new();
    
    /// <summary>警告列表 / List of warnings</summary>
    public List<string> Warnings { get; } = new();
    
    /// <summary>是否验证通过（无错误） / Whether validation passed (no errors)</summary>
    public bool IsValid => Errors.Count == 0;
    
    /// <summary>是否有警告 / Whether there are warnings</summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>
    /// 添加错误信息 / Add error message
    /// </summary>
    /// <param name="error">错误信息 / Error message</param>
    public void AddError(string error)
    {
        Errors.Add(error);
    }

    /// <summary>
    /// 添加警告信息 / Add warning message
    /// </summary>
    /// <param name="warning">警告信息 / Warning message</param>
    public void AddWarning(string warning)
    {
        Warnings.Add(warning);
    }

    /// <summary>
    /// 获取验证结果摘要 / Get validation result summary
    /// </summary>
    /// <returns>摘要字符串 / Summary string</returns>
    public string GetSummary()
    {
        if (IsValid && !HasWarnings)
            return "Validation passed";
        
        var parts = new List<string>();
        
        if (Errors.Count > 0)
            parts.Add($"{Errors.Count} error(s)");
            
        if (Warnings.Count > 0)
            parts.Add($"{Warnings.Count} warning(s)");
            
        return string.Join(", ", parts);
    }
}