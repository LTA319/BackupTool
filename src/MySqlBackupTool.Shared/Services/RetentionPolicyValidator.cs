using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Service for validating retention policies and their configurations
/// </summary>
public class RetentionPolicyValidator
{
    /// <summary>
    /// Validates a retention policy configuration
    /// </summary>
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
    /// Validates multiple policies for conflicts
    /// </summary>
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
    /// Validates if a policy is safe to apply (won't delete too much data)
    /// </summary>
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

    private void ValidateDescription(string? description, ValidationResult result)
    {
        if (!string.IsNullOrEmpty(description) && description.Length > 500)
        {
            result.AddError("Policy description cannot exceed 500 characters");
        }
    }

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
/// Result of a validation operation
/// </summary>
public class ValidationResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    
    public bool IsValid => Errors.Count == 0;
    public bool HasWarnings => Warnings.Count > 0;

    public void AddError(string error)
    {
        Errors.Add(error);
    }

    public void AddWarning(string warning)
    {
        Warnings.Add(warning);
    }

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