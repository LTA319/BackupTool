using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Repository interface for BackupConfiguration entities
/// </summary>
public interface IBackupConfigurationRepository : IRepository<BackupConfiguration>
{
    /// <summary>
    /// Gets all active backup configurations
    /// </summary>
    Task<IEnumerable<BackupConfiguration>> GetActiveConfigurationsAsync();

    /// <summary>
    /// Gets a configuration by name
    /// </summary>
    Task<BackupConfiguration?> GetByNameAsync(string name);

    /// <summary>
    /// Checks if a configuration name is unique (excluding the specified ID)
    /// </summary>
    Task<bool> IsNameUniqueAsync(string name, int excludeId = 0);

    /// <summary>
    /// Activates a configuration (sets IsActive to true)
    /// </summary>
    Task<bool> ActivateConfigurationAsync(int id);

    /// <summary>
    /// Deactivates a configuration (sets IsActive to false)
    /// </summary>
    Task<bool> DeactivateConfigurationAsync(int id);

    /// <summary>
    /// Gets configurations that target a specific server endpoint
    /// </summary>
    Task<IEnumerable<BackupConfiguration>> GetByTargetServerAsync(string ipAddress, int port);

    /// <summary>
    /// Validates and saves a configuration with connection parameter validation
    /// </summary>
    Task<(bool Success, List<string> Errors, BackupConfiguration? Configuration)> ValidateAndSaveAsync(BackupConfiguration configuration);

    /// <summary>
    /// Validates and saves a configuration with optional connection parameter validation
    /// </summary>
    Task<(bool Success, List<string> Errors, BackupConfiguration? Configuration)> ValidateAndSaveAsync(BackupConfiguration configuration, bool validateConnections);
}