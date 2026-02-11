using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 安全凭据存储操作接口
/// 提供具有增强安全功能的客户端凭据管理方法
/// Interface for secure credential storage operations
/// Provides methods for managing client credentials with enhanced security features
/// </summary>
public interface ISecureCredentialStorage
{
    /// <summary>
    /// 确保数据库中存在默认客户端凭据
    /// 如果不存在则创建默认凭据，保留现有凭据
    /// Ensures that default client credentials exist in the database
    /// Creates default credentials if they don't exist, preserves existing ones
    /// </summary>
    /// <returns>如果默认凭据存在或创建成功则返回true / True if default credentials exist or were created successfully</returns>
    Task<bool> EnsureDefaultCredentialsExistAsync();

    /// <summary>
    /// 检索默认客户端凭据
    /// Retrieves the default client credentials
    /// </summary>
    /// <returns>默认客户端凭据，如果未找到则返回null / Default client credentials or null if not found</returns>
    Task<ClientCredentials?> GetDefaultCredentialsAsync();

    /// <summary>
    /// 根据客户端ID检索客户端凭据
    /// Retrieves client credentials by client ID
    /// </summary>
    /// <param name="clientId">客户端标识符 / Client identifier</param>
    /// <returns>客户端凭据，如果未找到则返回null / Client credentials or null if not found</returns>
    Task<ClientCredentials?> GetCredentialsByClientIdAsync(string clientId);

    /// <summary>
    /// 根据存储的值验证客户端凭据
    /// Validates client credentials against stored values
    /// </summary>
    /// <param name="clientId">客户端标识符 / Client identifier</param>
    /// <param name="clientSecret">要验证的客户端密钥 / Client secret to validate</param>
    /// <returns>如果凭据有效返回true，否则返回false / True if credentials are valid, false otherwise</returns>
    Task<bool> ValidateCredentialsAsync(string clientId, string clientSecret);

    /// <summary>
    /// 安全存储客户端凭据
    /// Stores client credentials securely
    /// </summary>
    /// <param name="credentials">要存储的客户端凭据 / Client credentials to store</param>
    /// <returns>如果存储成功返回true，否则返回false / True if stored successfully, false otherwise</returns>
    Task<bool> StoreCredentialsAsync(ClientCredentials credentials);

    /// <summary>
    /// 检查指定客户端ID是否存在凭据
    /// Checks if credentials exist for the specified client ID
    /// </summary>
    /// <param name="clientId">客户端标识符 / Client identifier</param>
    /// <returns>如果凭据存在返回true，否则返回false / True if credentials exist, false otherwise</returns>
    Task<bool> CredentialsExistAsync(string clientId);

    /// <summary>
    /// 更新客户端凭据
    /// Updates client credentials
    /// </summary>
    /// <param name="credentials">更新的凭据 / Updated credentials</param>
    /// <returns>如果更新成功返回true，否则返回false / True if updated successfully, false otherwise</returns>
    Task<bool> UpdateCredentialsAsync(ClientCredentials credentials);

    /// <summary>
    /// 删除客户端凭据
    /// Deletes client credentials
    /// </summary>
    /// <param name="clientId">客户端标识符 / Client identifier</param>
    /// <returns>如果删除成功返回true，如果未找到返回false / True if deleted successfully, false if not found</returns>
    Task<bool> DeleteCredentialsAsync(string clientId);

    /// <summary>
    /// 列出所有客户端ID
    /// Lists all client IDs
    /// </summary>
    /// <returns>客户端标识符列表 / List of client identifiers</returns>
    Task<List<string>> ListClientIdsAsync();

    /// <summary>
    /// 验证凭据存储的完整性
    /// Validates the integrity of the credential storage
    /// </summary>
    /// <returns>如果存储有效返回true，如果损坏返回false / True if storage is valid, false if corrupted</returns>
    Task<bool> ValidateStorageIntegrityAsync();
}