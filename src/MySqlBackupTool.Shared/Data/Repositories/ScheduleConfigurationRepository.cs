using Microsoft.EntityFrameworkCore;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Data.Repositories;

/// <summary>
/// Repository implementation for schedule configuration data access
/// </summary>
public class ScheduleConfigurationRepository : Repository<ScheduleConfiguration>, IScheduleConfigurationRepository
{
    public ScheduleConfigurationRepository(BackupDbContext context) : base(context)
    {
    }

    /// <summary>
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
    /// Override to include related entities when getting by ID
    /// </summary>
    public override async Task<ScheduleConfiguration?> GetByIdAsync(int id)
    {
        return await _context.Set<ScheduleConfiguration>()
            .Include(s => s.BackupConfiguration)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    /// <summary>
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