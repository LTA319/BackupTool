using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Service interface for retention policy management and execution
/// </summary>
public interface IRetentionPolicyService
{
    /// <summary>
    /// Executes all enabled retention policies
    /// </summary>
    Task<RetentionExecutionResult> ExecuteRetentionPoliciesAsync();

    /// <summary>
    /// Applies a specific retention policy
    /// </summary>
    Task<RetentionExecutionResult> ApplyRetentionPolicyAsync(RetentionPolicy policy);

    /// <summary>
    /// Creates a new retention policy with validation
    /// </summary>
    Task<RetentionPolicy> CreateRetentionPolicyAsync(RetentionPolicy policy);

    /// <summary>
    /// Updates an existing retention policy
    /// </summary>
    Task<RetentionPolicy> UpdateRetentionPolicyAsync(RetentionPolicy policy);

    /// <summary>
    /// Deletes a retention policy
    /// </summary>
    Task<bool> DeleteRetentionPolicyAsync(int policyId);

    /// <summary>
    /// Gets all retention policies
    /// </summary>
    Task<IEnumerable<RetentionPolicy>> GetAllRetentionPoliciesAsync();

    /// <summary>
    /// Gets all enabled retention policies
    /// </summary>
    Task<IEnumerable<RetentionPolicy>> GetEnabledRetentionPoliciesAsync();

    /// <summary>
    /// Gets a retention policy by ID
    /// </summary>
    Task<RetentionPolicy?> GetRetentionPolicyByIdAsync(int policyId);

    /// <summary>
    /// Gets a retention policy by name
    /// </summary>
    Task<RetentionPolicy?> GetRetentionPolicyByNameAsync(string name);

    /// <summary>
    /// Enables a retention policy
    /// </summary>
    Task<bool> EnableRetentionPolicyAsync(int policyId);

    /// <summary>
    /// Disables a retention policy
    /// </summary>
    Task<bool> DisableRetentionPolicyAsync(int policyId);

    /// <summary>
    /// Gets retention policy recommendations based on current backup patterns
    /// </summary>
    Task<List<RetentionPolicy>> GetRetentionPolicyRecommendationsAsync();

    /// <summary>
    /// Validates a retention policy configuration
    /// </summary>
    Task<(bool IsValid, List<string> Errors)> ValidateRetentionPolicyAsync(RetentionPolicy policy);

    /// <summary>
    /// Estimates the impact of applying a retention policy
    /// </summary>
    Task<RetentionImpactEstimate> EstimateRetentionImpactAsync(RetentionPolicy policy);
}

/// <summary>
/// Estimated impact of applying a retention policy
/// </summary>
public class RetentionImpactEstimate
{
    public int EstimatedFilesToDelete { get; set; }
    public int EstimatedLogsToDelete { get; set; }
    public long EstimatedBytesToFree { get; set; }
    public List<string> FilesToDelete { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Gets a formatted description of the estimated impact
    /// </summary>
    public string GetImpactDescription()
    {
        var bytesStr = FormatBytes(EstimatedBytesToFree);
        return $"Will delete {EstimatedFilesToDelete} files and {EstimatedLogsToDelete} logs, freeing {bytesStr}";
    }

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