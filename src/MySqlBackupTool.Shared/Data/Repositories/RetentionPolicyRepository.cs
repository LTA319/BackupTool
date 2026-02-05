using Microsoft.EntityFrameworkCore;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Data.Repositories;

/// <summary>
/// 保留策略实体的存储库实现
/// Repository implementation for RetentionPolicy entities
/// </summary>
public class RetentionPolicyRepository : Repository<RetentionPolicy>, IRetentionPolicyRepository
{
    public RetentionPolicyRepository(BackupDbContext context) : base(context)
    {
    }

    /// <summary>
    /// 获取所有启用的保留策略
    /// Gets all enabled retention policies
    /// </summary>
    public async Task<IEnumerable<RetentionPolicy>> GetEnabledPoliciesAsync()
    {
        return await _dbSet
            .Where(rp => rp.IsEnabled)
            .OrderBy(rp => rp.Name)
            .ToListAsync();
    }

    /// <summary>
    /// 获取默认保留策略
    /// Gets the default retention policy
    /// </summary>
    public async Task<RetentionPolicy?> GetDefaultPolicyAsync()
    {
        // For now, return the first enabled policy
        // In a more complex implementation, you might have a separate flag for default policy
        return await _dbSet
            .Where(rp => rp.IsEnabled)
            .OrderBy(rp => rp.CreatedAt)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// 设置为默认策略
    /// Sets a policy as default
    /// </summary>
    public async Task<bool> SetAsDefaultAsync(int id)
    {
        var policy = await GetByIdAsync(id);
        if (policy == null)
            return false;

        // Enable this policy if it's not already enabled
        if (!policy.IsEnabled)
        {
            policy.IsEnabled = true;
            await UpdateAsync(policy);
            await SaveChangesAsync();
        }

        return true;
    }

    /// <summary>
    /// 根据名称获取保留策略
    /// Gets a retention policy by name
    /// </summary>
    public async Task<RetentionPolicy?> GetByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return await _dbSet.FirstOrDefaultAsync(rp => rp.Name == name);
    }

    /// <summary>
    /// 检查名称是否唯一
    /// Checks if a name is unique
    /// </summary>
    public async Task<bool> IsNameUniqueAsync(string name, int excludeId = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return !await _dbSet.AnyAsync(rp => rp.Name == name && rp.Id != excludeId);
    }

    /// <summary>
    /// 启用策略
    /// Enables a policy
    /// </summary>
    public async Task<bool> EnablePolicyAsync(int id)
    {
        var policy = await GetByIdAsync(id);
        if (policy == null)
            return false;

        policy.IsEnabled = true;
        await UpdateAsync(policy);
        await SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 禁用策略
    /// Disables a policy
    /// </summary>
    public async Task<bool> DisablePolicyAsync(int id)
    {
        var policy = await GetByIdAsync(id);
        if (policy == null)
            return false;

        policy.IsEnabled = false;
        await UpdateAsync(policy);
        await SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 应用保留策略
    /// Applies retention policies
    /// </summary>
    public async Task<RetentionResult> ApplyRetentionPoliciesAsync()
    {
        var result = new RetentionResult();
        var startTime = DateTime.Now;

        try
        {
            var enabledPolicies = await GetEnabledPoliciesAsync();
            if (!enabledPolicies.Any())
            {
                result.Duration = DateTime.Now - startTime;
                return result;
            }

            // Get all backup logs with file information
            var backupLogs = await _context.BackupLogs
                .Where(bl => !string.IsNullOrEmpty(bl.FilePath) && bl.FileSize.HasValue)
                .OrderByDescending(bl => bl.StartTime)
                .ToListAsync();

            var filesToDelete = new List<BackupLog>();

            foreach (var policy in enabledPolicies)
            {
                var currentBackupCount = backupLogs.Count;
                var currentStorageUsed = backupLogs.Sum(bl => bl.FileSize ?? 0);

                foreach (var backupLog in backupLogs)
                {
                    if (filesToDelete.Contains(backupLog))
                        continue;

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
            }

            // Delete the files and update the database
            foreach (var backupLog in filesToDelete.Distinct())
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
                    }

                    // Remove the backup log entry
                    _context.BackupLogs.Remove(backupLog);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error deleting {backupLog.FilePath}: {ex.Message}");
                }
            }

            if (result.FilesDeleted > 0)
            {
                await SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error applying retention policies: {ex.Message}");
        }

        result.Duration = DateTime.Now - startTime;
        return result;
    }

    public override async Task<RetentionPolicy> AddAsync(RetentionPolicy entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        // Ensure CreatedAt is set
        if (entity.CreatedAt == default)
            entity.CreatedAt = DateTime.Now;

        return await base.AddAsync(entity);
    }

    public override async Task<RetentionPolicy> UpdateAsync(RetentionPolicy entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        // Don't update CreatedAt on updates
        var existingEntity = await _dbSet.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entity.Id);
        if (existingEntity != null)
        {
            entity.CreatedAt = existingEntity.CreatedAt;
        }

        return await base.UpdateAsync(entity);
    }
}