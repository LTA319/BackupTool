using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// BackupConfiguration实体的存储库接口
/// Repository interface for BackupConfiguration entities
/// </summary>
public interface IBackupConfigurationRepository : IRepository<BackupConfiguration>
{
    /// <summary>
    /// 获取所有活动的备份配置
    /// Gets all active backup configurations
    /// </summary>
    Task<IEnumerable<BackupConfiguration>> GetActiveConfigurationsAsync();

    /// <summary>
    /// 根据名称获取配置
    /// Gets a configuration by name
    /// </summary>
    Task<BackupConfiguration?> GetByNameAsync(string name);

    /// <summary>
    /// 检查配置名称是否唯一（排除指定的ID）
    /// Checks if a configuration name is unique (excluding the specified ID)
    /// </summary>
    Task<bool> IsNameUniqueAsync(string name, int excludeId = 0);

    /// <summary>
    /// 激活配置（将IsActive设置为true）
    /// Activates a configuration (sets IsActive to true)
    /// </summary>
    Task<bool> ActivateConfigurationAsync(int id);

    /// <summary>
    /// 停用配置（将IsActive设置为false）
    /// Deactivates a configuration (sets IsActive to false)
    /// </summary>
    Task<bool> DeactivateConfigurationAsync(int id);

    /// <summary>
    /// 获取针对特定服务器端点的配置
    /// Gets configurations that target a specific server endpoint
    /// </summary>
    Task<IEnumerable<BackupConfiguration>> GetByTargetServerAsync(string ipAddress, int port);

    /// <summary>
    /// 验证并保存配置，包含连接参数验证
    /// Validates and saves a configuration with connection parameter validation
    /// </summary>
    Task<(bool Success, List<string> Errors, BackupConfiguration? Configuration)> ValidateAndSaveAsync(BackupConfiguration configuration);

    /// <summary>
    /// 验证并保存配置，可选择是否进行连接参数验证
    /// Validates and saves a configuration with optional connection parameter validation
    /// </summary>
    Task<(bool Success, List<string> Errors, BackupConfiguration? Configuration)> ValidateAndSaveAsync(BackupConfiguration configuration, bool validateConnections);

    /// <summary>
    /// 更新配置的身份验证凭据
    /// Updates the authentication credentials for a configuration
    /// </summary>
    Task<(bool Success, List<string> Errors)> UpdateCredentialsAsync(int configurationId, string clientId, string clientSecret);
}