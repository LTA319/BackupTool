using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Service for managing retention policies and automatic cleanup
/// </summary>
public class RetentionManagementService : IRetentionPolicyService
{
    private readonly IRetentionPolicyRepository _retentionPolicyRepository;
    private readonly IBackupLogRepository _backupLogRepository;
    private readonly ILogger<RetentionManagementService> _logger;
    private readonly RetentionPolicyValidator _validator;

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
    /// Executes all enabled retention policies
    /// </summary>
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
            // Get all enabled retention policies
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

            // Execute log cleanup for each policy
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
    /// Applies a specific retention policy
    /// </summary>
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
            // Apply log cleanup based on policy
            var logsDeleted = 0;

            if (policy.MaxAgeDays.HasValue)
            {
                logsDeleted += await _backupLogRepository.CleanupOldLogsAsync(policy.MaxAgeDays.Value, policy.MaxCount);
            }
            else if (policy.MaxCount.HasValue)
            {
                // If only max count is specified, use a very large age (effectively unlimited)
                logsDeleted += await _backupLogRepository.CleanupOldLogsAsync(int.MaxValue, policy.MaxCount.Value);
            }

            result.LogsDeleted = logsDeleted;

            // Apply file cleanup based on policy
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
    /// Applies file retention policy to backup files
    /// </summary>
    private async Task<RetentionResult> ApplyFileRetentionPolicyAsync(RetentionPolicy policy)
    {
        var result = new RetentionResult();

        try
        {
            // Get all backup logs with file information
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

            // Apply retention logic
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

            // Delete the files
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
    /// Creates a new retention policy with validation
    /// </summary>
    public async Task<RetentionPolicy> CreateRetentionPolicyAsync(RetentionPolicy policy)
    {
        if (policy == null)
            throw new ArgumentNullException(nameof(policy));

        _logger.LogInformation("Creating new retention policy: {PolicyName}", policy.Name);

        // Validate policy
        await ValidateRetentionPolicyInternalAsync(policy);

        // Check name uniqueness
        var isUnique = await _retentionPolicyRepository.IsNameUniqueAsync(policy.Name);
        if (!isUnique)
        {
            throw new InvalidOperationException($"A retention policy with the name '{policy.Name}' already exists");
        }

        // Set creation time
        policy.CreatedAt = DateTime.UtcNow;

        var result = await _retentionPolicyRepository.AddAsync(policy);
        await _retentionPolicyRepository.SaveChangesAsync();

        _logger.LogInformation("Created retention policy: {PolicyName} (ID: {PolicyId})", 
            result.Name, result.Id);

        return result;
    }

    /// <summary>
    /// Updates an existing retention policy
    /// </summary>
    public async Task<RetentionPolicy> UpdateRetentionPolicyAsync(RetentionPolicy policy)
    {
        if (policy == null)
            throw new ArgumentNullException(nameof(policy));

        _logger.LogInformation("Updating retention policy: {PolicyName} (ID: {PolicyId})", 
            policy.Name, policy.Id);

        // Validate policy
        await ValidateRetentionPolicyInternalAsync(policy);

        // Check name uniqueness (excluding current policy)
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
    /// Deletes a retention policy
    /// </summary>
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
    /// Gets all retention policies
    /// </summary>
    public async Task<IEnumerable<RetentionPolicy>> GetAllRetentionPoliciesAsync()
    {
        return await _retentionPolicyRepository.GetAllAsync();
    }

    /// <summary>
    /// Gets all enabled retention policies
    /// </summary>
    public async Task<IEnumerable<RetentionPolicy>> GetEnabledRetentionPoliciesAsync()
    {
        return await _retentionPolicyRepository.GetEnabledPoliciesAsync();
    }

    /// <summary>
    /// Gets a retention policy by ID
    /// </summary>
    public async Task<RetentionPolicy?> GetRetentionPolicyByIdAsync(int policyId)
    {
        return await _retentionPolicyRepository.GetByIdAsync(policyId);
    }

    /// <summary>
    /// Gets a retention policy by name
    /// </summary>
    public async Task<RetentionPolicy?> GetRetentionPolicyByNameAsync(string name)
    {
        return await _retentionPolicyRepository.GetByNameAsync(name);
    }

    /// <summary>
    /// Enables a retention policy
    /// </summary>
    public async Task<bool> EnableRetentionPolicyAsync(int policyId)
    {
        _logger.LogInformation("Enabling retention policy with ID: {PolicyId}", policyId);
        return await _retentionPolicyRepository.EnablePolicyAsync(policyId);
    }

    /// <summary>
    /// Disables a retention policy
    /// </summary>
    public async Task<bool> DisableRetentionPolicyAsync(int policyId)
    {
        _logger.LogInformation("Disabling retention policy with ID: {PolicyId}", policyId);
        return await _retentionPolicyRepository.DisablePolicyAsync(policyId);
    }

    /// <summary>
    /// Validates a retention policy configuration
    /// </summary>
    public async Task<(bool IsValid, List<string> Errors)> ValidateRetentionPolicyAsync(RetentionPolicy policy)
    {
        var validationResult = _validator.ValidatePolicy(policy);
        
        // Add warnings to errors for backward compatibility
        var allErrors = new List<string>();
        allErrors.AddRange(validationResult.Errors);
        allErrors.AddRange(validationResult.Warnings.Select(w => $"Warning: {w}"));
        
        return (validationResult.IsValid, allErrors);
    }

    /// <summary>
    /// Estimates the impact of applying a retention policy
    /// </summary>
    public async Task<RetentionImpactEstimate> EstimateRetentionImpactAsync(RetentionPolicy policy)
    {
        var estimate = new RetentionImpactEstimate();

        try
        {
            // Get all backup logs with file information
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

            // Apply retention logic to estimate impact
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

            // Add warnings for large deletions
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
    /// Internal validation method for retention policies
    /// </summary>
    private async Task ValidateRetentionPolicyInternalAsync(RetentionPolicy policy)
    {
        var validationResult = _validator.ValidatePolicy(policy);
        
        if (!validationResult.IsValid)
        {
            throw new ArgumentException($"Retention policy validation failed: {string.Join(", ", validationResult.Errors)}");
        }
        
        // Log warnings
        foreach (var warning in validationResult.Warnings)
        {
            _logger.LogWarning("Retention policy validation warning: {Warning}", warning);
        }
    }

    /// <summary>
    /// Gets retention policy recommendations based on current backup patterns
    /// </summary>
    public async Task<List<RetentionPolicy>> GetRetentionPolicyRecommendationsAsync()
    {
        _logger.LogInformation("Generating retention policy recommendations");

        var recommendations = new List<RetentionPolicy>();

        try
        {
            // Analyze current backup patterns
            var recentLogs = await _backupLogRepository.GetByDateRangeAsync(
                DateTime.UtcNow.AddDays(-90), DateTime.UtcNow);

            var backupLogsList = recentLogs.ToList();
            
            if (!backupLogsList.Any())
            {
                // Default recommendations if no backup history
                recommendations.AddRange(GetDefaultRetentionPolicies());
                return recommendations;
            }

            var totalBackups = backupLogsList.Count;
            var averageBackupsPerDay = totalBackups / 90.0;
            var totalStorage = backupLogsList.Where(bl => bl.FileSize.HasValue).Sum(bl => bl.FileSize!.Value);
            var averageBackupSize = backupLogsList.Where(bl => bl.FileSize.HasValue).Any() 
                ? backupLogsList.Where(bl => bl.FileSize.HasValue).Average(bl => bl.FileSize!.Value)
                : 0;

            // Conservative policy (keep more backups)
            recommendations.Add(new RetentionPolicy
            {
                Name = "Conservative (Recommended)",
                Description = "Keeps backups for 90 days or up to 100 backups, whichever is more restrictive",
                MaxAgeDays = 90,
                MaxCount = Math.Max(100, (int)(averageBackupsPerDay * 90)),
                IsEnabled = false
            });

            // Balanced policy
            recommendations.Add(new RetentionPolicy
            {
                Name = "Balanced",
                Description = "Keeps backups for 30 days or up to 50 backups",
                MaxAgeDays = 30,
                MaxCount = Math.Max(50, (int)(averageBackupsPerDay * 30)),
                IsEnabled = false
            });

            // Aggressive policy (keep fewer backups)
            recommendations.Add(new RetentionPolicy
            {
                Name = "Aggressive",
                Description = "Keeps backups for 7 days or up to 20 backups",
                MaxAgeDays = 7,
                MaxCount = Math.Max(20, (int)(averageBackupsPerDay * 7)),
                IsEnabled = false
            });

            // Storage-based policy
            if (totalStorage > 0)
            {
                var recommendedStorageLimit = (long)(totalStorage * 1.5); // 50% buffer
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