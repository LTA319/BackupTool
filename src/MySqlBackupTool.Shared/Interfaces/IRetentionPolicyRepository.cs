using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Repository interface for RetentionPolicy entities
/// </summary>
public interface IRetentionPolicyRepository : IRepository<RetentionPolicy>
{
    /// <summary>
    /// Gets all enabled retention policies
    /// </summary>
    Task<IEnumerable<RetentionPolicy>> GetEnabledPoliciesAsync();

    /// <summary>
    /// Gets the default retention policy
    /// </summary>
    Task<RetentionPolicy?> GetDefaultPolicyAsync();

    /// <summary>
    /// Sets a policy as the default policy
    /// </summary>
    Task<bool> SetAsDefaultAsync(int id);

    /// <summary>
    /// Gets a policy by name
    /// </summary>
    Task<RetentionPolicy?> GetByNameAsync(string name);

    /// <summary>
    /// Checks if a policy name is unique (excluding the specified ID)
    /// </summary>
    Task<bool> IsNameUniqueAsync(string name, int excludeId = 0);

    /// <summary>
    /// Enables a retention policy
    /// </summary>
    Task<bool> EnablePolicyAsync(int id);

    /// <summary>
    /// Disables a retention policy
    /// </summary>
    Task<bool> DisablePolicyAsync(int id);

    /// <summary>
    /// Applies retention policies to clean up old backups
    /// </summary>
    Task<RetentionResult> ApplyRetentionPoliciesAsync();
}

/// <summary>
/// Result of applying retention policies
/// </summary>
public class RetentionResult
{
    public int FilesDeleted { get; set; }
    public long BytesFreed { get; set; }
    public List<string> DeletedFiles { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public TimeSpan Duration { get; set; }
}