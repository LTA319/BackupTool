using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 保留策略管理和执行的服务接口 / Service interface for retention policy management and execution
/// 提供保留策略的创建、更新、删除、执行和影响评估等高级服务功能
/// Provides high-level service functionality for retention policy creation, updating, deletion, execution and impact assessment
/// </summary>
public interface IRetentionPolicyService
{
    /// <summary>
    /// 执行所有已启用的保留策略 / Executes all enabled retention policies
    /// 运行系统中所有处于启用状态的保留策略，清理符合条件的旧备份
    /// Runs all enabled retention policies in the system to clean up old backups that meet criteria
    /// </summary>
    /// <returns>保留策略执行结果 / Retention policy execution result</returns>
    Task<RetentionExecutionResult> ExecuteRetentionPoliciesAsync();

    /// <summary>
    /// 应用特定的保留策略 / Applies a specific retention policy
    /// 执行指定的单个保留策略，而不是所有启用的策略
    /// Executes specified single retention policy instead of all enabled policies
    /// </summary>
    /// <param name="policy">要应用的保留策略 / Retention policy to apply</param>
    /// <returns>保留策略执行结果 / Retention policy execution result</returns>
    Task<RetentionExecutionResult> ApplyRetentionPolicyAsync(RetentionPolicy policy);

    /// <summary>
    /// 创建带有验证的新保留策略 / Creates a new retention policy with validation
    /// 创建新的保留策略，包括名称唯一性验证和配置有效性检查
    /// Creates new retention policy including name uniqueness validation and configuration validity checks
    /// </summary>
    /// <param name="policy">要创建的保留策略 / Retention policy to create</param>
    /// <returns>创建的保留策略实体 / Created retention policy entity</returns>
    Task<RetentionPolicy> CreateRetentionPolicyAsync(RetentionPolicy policy);

    /// <summary>
    /// 更新现有的保留策略 / Updates an existing retention policy
    /// 修改现有保留策略的配置，包括验证和一致性检查
    /// Modifies configuration of existing retention policy including validation and consistency checks
    /// </summary>
    /// <param name="policy">要更新的保留策略 / Retention policy to update</param>
    /// <returns>更新后的保留策略实体 / Updated retention policy entity</returns>
    Task<RetentionPolicy> UpdateRetentionPolicyAsync(RetentionPolicy policy);

    /// <summary>
    /// 删除保留策略 / Deletes a retention policy
    /// 删除指定ID的保留策略，包括相关的依赖检查
    /// Deletes retention policy with specified ID including related dependency checks
    /// </summary>
    /// <param name="policyId">策略ID / Policy ID</param>
    /// <returns>如果删除成功返回true，否则返回false / True if deletion succeeds, false otherwise</returns>
    Task<bool> DeleteRetentionPolicyAsync(int policyId);

    /// <summary>
    /// 获取所有保留策略 / Gets all retention policies
    /// 返回系统中定义的所有保留策略，包括启用和禁用的
    /// Returns all retention policies defined in the system, including enabled and disabled ones
    /// </summary>
    /// <returns>保留策略集合 / Collection of retention policies</returns>
    Task<IEnumerable<RetentionPolicy>> GetAllRetentionPoliciesAsync();

    /// <summary>
    /// 获取所有已启用的保留策略 / Gets all enabled retention policies
    /// 返回当前处于启用状态的保留策略
    /// Returns retention policies currently in enabled state
    /// </summary>
    /// <returns>已启用的保留策略集合 / Collection of enabled retention policies</returns>
    Task<IEnumerable<RetentionPolicy>> GetEnabledRetentionPoliciesAsync();

    /// <summary>
    /// 根据ID获取保留策略 / Gets a retention policy by ID
    /// 通过策略ID查找对应的保留策略
    /// Finds corresponding retention policy by policy ID
    /// </summary>
    /// <param name="policyId">策略ID / Policy ID</param>
    /// <returns>保留策略实体，如果未找到则返回null / Retention policy entity or null if not found</returns>
    Task<RetentionPolicy?> GetRetentionPolicyByIdAsync(int policyId);

    /// <summary>
    /// 根据名称获取保留策略 / Gets a retention policy by name
    /// 通过策略名称查找对应的保留策略
    /// Finds corresponding retention policy by policy name
    /// </summary>
    /// <param name="name">策略名称 / Policy name</param>
    /// <returns>保留策略实体，如果未找到则返回null / Retention policy entity or null if not found</returns>
    Task<RetentionPolicy?> GetRetentionPolicyByNameAsync(string name);

    /// <summary>
    /// 启用保留策略 / Enables a retention policy
    /// 将指定ID的保留策略设置为启用状态
    /// Sets retention policy with specified ID to enabled state
    /// </summary>
    /// <param name="policyId">策略ID / Policy ID</param>
    /// <returns>如果启用成功返回true，否则返回false / True if enabling succeeds, false otherwise</returns>
    Task<bool> EnableRetentionPolicyAsync(int policyId);

    /// <summary>
    /// 禁用保留策略 / Disables a retention policy
    /// 将指定ID的保留策略设置为禁用状态
    /// Sets retention policy with specified ID to disabled state
    /// </summary>
    /// <param name="policyId">策略ID / Policy ID</param>
    /// <returns>如果禁用成功返回true，否则返回false / True if disabling succeeds, false otherwise</returns>
    Task<bool> DisableRetentionPolicyAsync(int policyId);

    /// <summary>
    /// 基于当前备份模式获取保留策略建议 / Gets retention policy recommendations based on current backup patterns
    /// 分析现有备份数据和使用模式，提供优化的保留策略建议
    /// Analyzes existing backup data and usage patterns to provide optimized retention policy recommendations
    /// </summary>
    /// <returns>推荐的保留策略列表 / List of recommended retention policies</returns>
    Task<List<RetentionPolicy>> GetRetentionPolicyRecommendationsAsync();

    /// <summary>
    /// 验证保留策略配置 / Validates a retention policy configuration
    /// 检查保留策略配置的有效性，包括参数范围、逻辑一致性等
    /// Checks validity of retention policy configuration including parameter ranges, logical consistency, etc.
    /// </summary>
    /// <param name="policy">要验证的保留策略 / Retention policy to validate</param>
    /// <returns>验证结果和错误列表的元组 / Tuple of validation result and error list</returns>
    Task<(bool IsValid, List<string> Errors)> ValidateRetentionPolicyAsync(RetentionPolicy policy);

    /// <summary>
    /// 估算应用保留策略的影响 / Estimates the impact of applying a retention policy
    /// 预测执行指定保留策略将删除的文件数量和释放的空间
    /// Predicts number of files to be deleted and space to be freed by executing specified retention policy
    /// </summary>
    /// <param name="policy">要评估的保留策略 / Retention policy to evaluate</param>
    /// <returns>保留策略影响估算结果 / Retention policy impact estimation result</returns>
    Task<RetentionImpactEstimate> EstimateRetentionImpactAsync(RetentionPolicy policy);
}

/// <summary>
/// 应用保留策略的估算影响 / Estimated impact of applying a retention policy
/// 提供执行保留策略前的影响预测，包括文件删除数量和空间释放估算
/// Provides impact prediction before executing retention policy including file deletion count and space release estimation
/// </summary>
public class RetentionImpactEstimate
{
    /// <summary>
    /// 估算要删除的文件数量 / Estimated number of files to delete
    /// </summary>
    public int EstimatedFilesToDelete { get; set; }
    
    /// <summary>
    /// 估算要删除的日志数量 / Estimated number of logs to delete
    /// </summary>
    public int EstimatedLogsToDelete { get; set; }
    
    /// <summary>
    /// 估算要释放的字节数 / Estimated number of bytes to free
    /// </summary>
    public long EstimatedBytesToFree { get; set; }
    
    /// <summary>
    /// 要删除的文件列表 / List of files to delete
    /// </summary>
    public List<string> FilesToDelete { get; set; } = new();
    
    /// <summary>
    /// 警告信息列表 / List of warning messages
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// 获取估算影响的格式化描述 / Gets a formatted description of the estimated impact
    /// 返回易于理解的影响描述字符串
    /// Returns user-friendly impact description string
    /// </summary>
    /// <returns>影响描述字符串 / Impact description string</returns>
    public string GetImpactDescription()
    {
        var bytesStr = FormatBytes(EstimatedBytesToFree);
        return $"Will delete {EstimatedFilesToDelete} files and {EstimatedLogsToDelete} logs, freeing {bytesStr}";
    }

    /// <summary>
    /// 格式化字节数为可读的字符串 / Formats byte count to readable string
    /// </summary>
    /// <param name="bytes">字节数 / Number of bytes</param>
    /// <returns>格式化的字节字符串 / Formatted byte string</returns>
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