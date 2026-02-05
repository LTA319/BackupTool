using Microsoft.EntityFrameworkCore;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Data.Repositories;

/// <summary>
/// 调度配置数据访问的存储库实现
/// Repository implementation for schedule configuration data access
/// </summary>
public class ScheduleConfigurationRepository : Repository<ScheduleConfiguration>, IScheduleConfigurationRepository
{
    public ScheduleConfigurationRepository(BackupDbContext context) : base(context)
    {
    }


    /// <summary>
    /// 获取特定备份配置的所有调度配置
    /// Gets all schedule configurations for a specific backup configuration
    /// </summary>
    public async Task<List<ScheduleConfiguration>> GetByBackupConfigIdAsync(int backupConfigId)
    {
        return await _context.Set<ScheduleConfiguration>()
            .Where(s => s.BackupConfigId == backupConfigId)
            .Include(s => s.BackupConfiguration)
            .OrderBy(s => s.ScheduleType)
            .ThenBy(s => s.ScheduleTime)
            .ToListAsync();
    }

    /// <summary>
    /// 获取所有启用的调度配置
    /// Gets all enabled schedule configurations
    /// </summary>
    public async Task<List<ScheduleConfiguration>> GetEnabledSchedulesAsync()
    {
        return await _context.Set<ScheduleConfiguration>()
            .Where(s => s.IsEnabled)
            .Include(s => s.BackupConfiguration)
            .OrderBy(s => s.NextExecution)
            .ToListAsync();
    }

    /// <summary>
    /// 获取到期需要执行的调度配置
    /// Gets schedule configurations that are due for execution
    /// </summary>
    public async Task<List<ScheduleConfiguration>> GetDueSchedulesAsync(DateTime currentTime)
    {
        return await _context.Set<ScheduleConfiguration>()
            .Where(s => s.IsEnabled && 
                       s.NextExecution.HasValue && 
                       s.NextExecution.Value <= currentTime)
            .Include(s => s.BackupConfiguration)
            .OrderBy(s => s.NextExecution)
            .ToListAsync();
    }

    /// <summary>
    /// 更新调度的最后执行时间
    /// Updates the last executed time for a schedule
    /// </summary>
    public async Task UpdateLastExecutedAsync(int scheduleId, DateTime executedTime)
    {
        var schedule = await _context.Set<ScheduleConfiguration>()
            .FirstOrDefaultAsync(s => s.Id == scheduleId);

        if (schedule != null)
        {
            schedule.LastExecuted = executedTime;
            schedule.NextExecution = schedule.CalculateNextExecution();
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// 更新调度的下次执行时间
    /// Updates the next execution time for a schedule
    /// </summary>
    public async Task UpdateNextExecutionAsync(int scheduleId, DateTime? nextExecutionTime)
    {
        var schedule = await _context.Set<ScheduleConfiguration>()
            .FirstOrDefaultAsync(s => s.Id == scheduleId);

        if (schedule != null)
        {
            schedule.NextExecution = nextExecutionTime;
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// 启用或禁用调度
    /// Enables or disables a schedule
    /// </summary>
    public async Task SetEnabledAsync(int scheduleId, bool enabled)
    {
        var schedule = await _context.Set<ScheduleConfiguration>()
            .FirstOrDefaultAsync(s => s.Id == scheduleId);

        if (schedule != null)
        {
            schedule.IsEnabled = enabled;
            
            // Recalculate next execution time when enabling
            if (enabled)
            {
                schedule.NextExecution = schedule.CalculateNextExecution();
            }
            else
            {
                schedule.NextExecution = null;
            }
            
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// 重写以在按ID获取时包含相关实体
    /// Override to include related entities when getting by ID
    /// </summary>
    public override async Task<ScheduleConfiguration?> GetByIdAsync(int id)
    {
        return await _context.Set<ScheduleConfiguration>()
            .Include(s => s.BackupConfiguration)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    /// <summary>
    /// 重写以在获取所有时包含相关实体
    /// Override to include related entities when getting all
    /// </summary>
    public override async Task<IEnumerable<ScheduleConfiguration>> GetAllAsync()
    {
        return await _context.Set<ScheduleConfiguration>()
            .Include(s => s.BackupConfiguration)
            .OrderBy(s => s.BackupConfigId)
            .ThenBy(s => s.ScheduleType)
            .ThenBy(s => s.ScheduleTime)
            .ToListAsync();
    }
}