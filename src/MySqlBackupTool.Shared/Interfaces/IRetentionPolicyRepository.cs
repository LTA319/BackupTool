using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// RetentionPolicy实体的存储库接口 / Repository interface for RetentionPolicy entities
/// 提供保留策略的存储、查询、管理和执行功能
/// Provides storage, querying, management and execution functionality for retention policies
/// </summary>
public interface IRetentionPolicyRepository : IRepository<RetentionPolicy>
{
    /// <summary>
    /// 获取所有已启用的保留策略 / Gets all enabled retention policies
    /// 返回当前处于启用状态的所有保留策略
    /// Returns all retention policies currently in enabled state
    /// </summary>
    Task<IEnumerable<RetentionPolicy>> GetEnabledPoliciesAsync();

    /// <summary>
    /// 获取默认保留策略 / Gets the default retention policy
    /// 返回系统设置的默认保留策略，如果未设置则返回null
    /// Returns system-configured default retention policy, or null if not set
    /// </summary>
    Task<RetentionPolicy?> GetDefaultPolicyAsync();

    /// <summary>
    /// 将策略设置为默认策略 / Sets a policy as the default policy
    /// 将指定ID的策略设置为系统默认策略，同时取消其他策略的默认状态
    /// Sets policy with specified ID as system default, while removing default status from other policies
    /// </summary>
    /// <param name="id">策略ID / Policy ID</param>
    /// <returns>如果设置成功返回true，否则返回false / True if setting succeeds, false otherwise</returns>
    Task<bool> SetAsDefaultAsync(int id);

    /// <summary>
    /// 根据名称获取策略 / Gets a policy by name
    /// 通过策略名称查找对应的保留策略
    /// Finds corresponding retention policy by policy name
    /// </summary>
    /// <param name="name">策略名称 / Policy name</param>
    /// <returns>保留策略实体，如果未找到则返回null / Retention policy entity or null if not found</returns>
    Task<RetentionPolicy?> GetByNameAsync(string name);

    /// <summary>
    /// 检查策略名称是否唯一（排除指定ID） / Checks if a policy name is unique (excluding the specified ID)
    /// 验证策略名称的唯一性，通常用于创建或更新策略时的验证
    /// Validates uniqueness of policy name, typically used for validation when creating or updating policies
    /// </summary>
    /// <param name="name">策略名称 / Policy name</param>
    /// <param name="excludeId">要排除的策略ID，默认为0 / Policy ID to exclude, default is 0</param>
    /// <returns>如果名称唯一返回true，否则返回false / True if name is unique, false otherwise</returns>
    Task<bool> IsNameUniqueAsync(string name, int excludeId = 0);

    /// <summary>
    /// 启用保留策略 / Enables a retention policy
    /// 将指定ID的保留策略设置为启用状态
    /// Sets retention policy with specified ID to enabled state
    /// </summary>
    /// <param name="id">策略ID / Policy ID</param>
    /// <returns>如果启用成功返回true，否则返回false / True if enabling succeeds, false otherwise</returns>
    Task<bool> EnablePolicyAsync(int id);

    /// <summary>
    /// 禁用保留策略 / Disables a retention policy
    /// 将指定ID的保留策略设置为禁用状态
    /// Sets retention policy with specified ID to disabled state
    /// </summary>
    /// <param name="id">策略ID / Policy ID</param>
    /// <returns>如果禁用成功返回true，否则返回false / True if disabling succeeds, false otherwise</returns>
    Task<bool> DisablePolicyAsync(int id);

    /// <summary>
    /// 应用保留策略清理旧备份 / Applies retention policies to clean up old backups
    /// 执行所有启用的保留策略，删除符合条件的旧备份文件
    /// Executes all enabled retention policies to delete old backup files that meet criteria
    /// </summary>
    /// <returns>保留策略执行结果 / Retention policy execution result</returns>
    Task<RetentionResult> ApplyRetentionPoliciesAsync();
}

/// <summary>
/// 应用保留策略的结果 / Result of applying retention policies
/// 包含删除的文件数量、释放的空间、错误信息等统计数据
/// Contains statistics such as number of deleted files, freed space, error information, etc.
/// </summary>
public class RetentionResult
{
    /// <summary>
    /// 删除的文件数量 / Number of files deleted
    /// </summary>
    public int FilesDeleted { get; set; }
    
    /// <summary>
    /// 释放的字节数 / Number of bytes freed
    /// </summary>
    public long BytesFreed { get; set; }
    
    /// <summary>
    /// 已删除文件的路径列表 / List of deleted file paths
    /// </summary>
    public List<string> DeletedFiles { get; set; } = new();
    
    /// <summary>
    /// 执行过程中的错误信息列表 / List of error messages during execution
    /// </summary>
    public List<string> Errors { get; set; } = new();
    
    /// <summary>
    /// 执行耗时 / Execution duration
    /// </summary>
    public TimeSpan Duration { get; set; }
}