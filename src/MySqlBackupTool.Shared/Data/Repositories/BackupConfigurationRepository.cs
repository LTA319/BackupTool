using Microsoft.EntityFrameworkCore;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Data.Repositories;

/// <summary>
/// Repository implementation for BackupConfiguration entities
/// </summary>
public class BackupConfigurationRepository : Repository<BackupConfiguration>, IBackupConfigurationRepository
{
    public BackupConfigurationRepository(BackupDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<BackupConfiguration>> GetActiveConfigurationsAsync()
    {
        return await _dbSet.Where(c => c.IsActive).ToListAsync();
    }

    public async Task<BackupConfiguration?> GetByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return await _dbSet.FirstOrDefaultAsync(c => c.Name == name);
    }

    public async Task<bool> IsNameUniqueAsync(string name, int excludeId = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return !await _dbSet.AnyAsync(c => c.Name == name && c.Id != excludeId);
    }

    public async Task<bool> ActivateConfigurationAsync(int id)
    {
        var configuration = await GetByIdAsync(id);
        if (configuration == null)
            return false;

        configuration.IsActive = true;
        await UpdateAsync(configuration);
        await SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeactivateConfigurationAsync(int id)
    {
        var configuration = await GetByIdAsync(id);
        if (configuration == null)
            return false;

        configuration.IsActive = false;
        await UpdateAsync(configuration);
        await SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<BackupConfiguration>> GetByTargetServerAsync(string ipAddress, int port)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return Enumerable.Empty<BackupConfiguration>();

        // Since TargetServer is stored as JSON, we need to load all configurations
        // and filter in memory. For better performance, consider adding separate columns
        // for IP and port if this query is used frequently.
        var allConfigurations = await GetAllAsync();
        return allConfigurations.Where(c => 
            c.TargetServer.IPAddress == ipAddress && 
            c.TargetServer.Port == port);
    }

    public async Task<(bool Success, List<string> Errors, BackupConfiguration? Configuration)> ValidateAndSaveAsync(BackupConfiguration configuration)
    {
        return await ValidateAndSaveAsync(configuration, validateConnections: true);
    }

    public async Task<(bool Success, List<string> Errors, BackupConfiguration? Configuration)> ValidateAndSaveAsync(BackupConfiguration configuration, bool validateConnections)
    {
        var errors = new List<string>();

        if (configuration == null)
        {
            errors.Add("Configuration cannot be null");
            return (false, errors, null);
        }

        try
        {
            // Validate data annotations
            var validationContext = new ValidationContext(configuration);
            var validationResults = new List<ValidationResult>();
            
            if (!Validator.TryValidateObject(configuration, validationContext, validationResults, true))
            {
                errors.AddRange(validationResults.Select(vr => vr.ErrorMessage ?? "Validation error"));
            }

            // Check name uniqueness
            if (!await IsNameUniqueAsync(configuration.Name, configuration.Id))
            {
                errors.Add($"Configuration name '{configuration.Name}' is already in use");
            }

            // Validate connection parameters only if requested
            if (validateConnections)
            {
                var (isValid, connectionErrors) = await configuration.ValidateConnectionParametersAsync();
                if (!isValid)
                {
                    errors.AddRange(connectionErrors);
                }
            }

            // If there are validation errors, return them
            if (errors.Count > 0)
            {
                return (false, errors, null);
            }

            // Save the configuration
            BackupConfiguration savedConfiguration;
            if (configuration.Id == 0)
            {
                // New configuration
                configuration.CreatedAt = DateTime.UtcNow;
                savedConfiguration = await AddAsync(configuration);
            }
            else
            {
                // Update existing configuration
                savedConfiguration = await UpdateAsync(configuration);
            }

            await SaveChangesAsync();
            return (true, errors, savedConfiguration);
        }
        catch (Exception ex)
        {
            errors.Add($"Error saving configuration: {ex.Message}");
            return (false, errors, null);
        }
    }

    public override async Task<BackupConfiguration> AddAsync(BackupConfiguration entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        // Ensure CreatedAt is set
        if (entity.CreatedAt == default)
            entity.CreatedAt = DateTime.UtcNow;

        return await base.AddAsync(entity);
    }

    public override async Task<BackupConfiguration> UpdateAsync(BackupConfiguration entity)
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