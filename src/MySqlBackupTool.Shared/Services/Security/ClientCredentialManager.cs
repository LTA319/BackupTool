using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Security.Cryptography;
using System.Text;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 管理客户端凭据的实用服务 / Utility service for managing client credentials
/// </summary>
public class ClientCredentialManager
{
    /// <summary>
    /// 日志记录器 / Logger
    /// </summary>
    private readonly ILogger<ClientCredentialManager> _logger;
    
    /// <summary>
    /// 凭据存储接口 / Credential storage interface
    /// </summary>
    private readonly ICredentialStorage _credentialStorage;

    /// <summary>
    /// 初始化客户端凭据管理器 / Initializes the client credential manager
    /// </summary>
    /// <param name="logger">日志记录器 / Logger</param>
    /// <param name="credentialStorage">凭据存储服务 / Credential storage service</param>
    /// <exception cref="ArgumentNullException">当凭据存储服务为null时抛出 / Thrown when credential storage service is null</exception>
    public ClientCredentialManager(ILogger<ClientCredentialManager> logger, ICredentialStorage credentialStorage)
    {
        _logger = logger;
        _credentialStorage = credentialStorage ?? throw new ArgumentNullException(nameof(credentialStorage));
    }

    /// <summary>
    /// 创建具有默认备份权限的新客户端 / Creates a new client with default backup permissions
    /// </summary>
    /// <param name="clientId">客户端ID / Client ID</param>
    /// <param name="clientName">客户端名称，可选 / Client name, optional</param>
    /// <param name="customPermissions">自定义权限，可选 / Custom permissions, optional</param>
    /// <returns>客户端凭据 / Client credentials</returns>
    /// <exception cref="ArgumentException">当客户端ID为空时抛出 / Thrown when client ID is empty</exception>
    /// <exception cref="InvalidOperationException">当客户端已存在或存储失败时抛出 / Thrown when client already exists or storage fails</exception>
    public async Task<ClientCredentials> CreateClientAsync(string clientId, string? clientName = null, string[]? customPermissions = null)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID cannot be null or empty", nameof(clientId));

        try
        {
            // 检查客户端是否已存在 / Check if client already exists
            var existingClient = await _credentialStorage.GetCredentialsAsync(clientId);
            if (existingClient != null)
            {
                throw new InvalidOperationException($"Client with ID '{clientId}' already exists");
            }

            // 生成安全的客户端密钥 / Generate secure client secret
            var clientSecret = GenerateSecureSecret();

            // 使用自定义权限或默认备份客户端权限 / Use custom permissions or default backup client permissions
            var permissions = customPermissions?.ToList() ?? BackupPermissions.DefaultClientPermissions.ToList();

            var credentials = new ClientCredentials
            {
                ClientId = clientId,
                ClientSecret = clientSecret,
                ClientName = clientName ?? clientId,
                Permissions = permissions,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            var success = await _credentialStorage.StoreCredentialsAsync(credentials);
            if (!success)
            {
                throw new InvalidOperationException("Failed to store client credentials");
            }

            _logger.LogInformation("Created new client {ClientId} with permissions: {Permissions}", 
                clientId, string.Join(", ", permissions));

            // 返回带有原始（未哈希）密钥的凭据用于初始设置 / Return credentials with the original (unhashed) secret for initial setup
            return credentials;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create client {ClientId}", clientId);
            throw;
        }
    }

    /// <summary>
    /// 更新客户端权限 / Updates client permissions
    /// </summary>
    /// <param name="clientId">客户端ID / Client ID</param>
    /// <param name="permissions">权限数组 / Permissions array</param>
    /// <returns>是否更新成功 / Whether update was successful</returns>
    /// <exception cref="ArgumentException">当客户端ID为空或权限无效时抛出 / Thrown when client ID is empty or permissions are invalid</exception>
    public async Task<bool> UpdateClientPermissionsAsync(string clientId, string[] permissions)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID cannot be null or empty", nameof(clientId));

        if (permissions == null || permissions.Length == 0)
            throw new ArgumentException("At least one permission is required", nameof(permissions));

        try
        {
            var existingCredentials = await _credentialStorage.GetCredentialsAsync(clientId);
            if (existingCredentials == null)
            {
                _logger.LogWarning("Attempted to update permissions for non-existent client {ClientId}", clientId);
                return false;
            }

            // 验证权限 / Validate permissions
            var invalidPermissions = permissions.Where(p => !BackupPermissions.AllPermissions.Contains(p)).ToList();
            if (invalidPermissions.Any())
            {
                throw new ArgumentException($"Invalid permissions: {string.Join(", ", invalidPermissions)}");
            }

            existingCredentials.Permissions = permissions.ToList();

            var success = await _credentialStorage.UpdateCredentialsAsync(existingCredentials);
            if (success)
            {
                _logger.LogInformation("Updated permissions for client {ClientId}: {Permissions}", 
                    clientId, string.Join(", ", permissions));
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update permissions for client {ClientId}", clientId);
            return false;
        }
    }

    /// <summary>
    /// 重置客户端密钥 / Resets client secret
    /// </summary>
    /// <param name="clientId">客户端ID / Client ID</param>
    /// <returns>新的客户端密钥，如果失败则返回null / New client secret, or null if failed</returns>
    /// <exception cref="ArgumentException">当客户端ID为空时抛出 / Thrown when client ID is empty</exception>
    public async Task<string?> ResetClientSecretAsync(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID cannot be null or empty", nameof(clientId));

        try
        {
            var existingCredentials = await _credentialStorage.GetCredentialsAsync(clientId);
            if (existingCredentials == null)
            {
                _logger.LogWarning("Attempted to reset secret for non-existent client {ClientId}", clientId);
                return null;
            }

            // 生成新的安全密钥 / Generate new secure secret
            var newSecret = GenerateSecureSecret();
            existingCredentials.ClientSecret = newSecret;

            var success = await _credentialStorage.UpdateCredentialsAsync(existingCredentials);
            if (success)
            {
                _logger.LogInformation("Reset secret for client {ClientId}", clientId);
                return newSecret; // 返回新密钥（未哈希）用于客户端配置 / Return the new secret (unhashed) for client configuration
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset secret for client {ClientId}", clientId);
            return null;
        }
    }

    /// <summary>
    /// 激活或停用客户端 / Activates or deactivates a client
    /// </summary>
    /// <param name="clientId">客户端ID / Client ID</param>
    /// <param name="isActive">是否激活 / Whether to activate</param>
    /// <returns>是否操作成功 / Whether operation was successful</returns>
    /// <exception cref="ArgumentException">当客户端ID为空时抛出 / Thrown when client ID is empty</exception>
    public async Task<bool> SetClientActiveStatusAsync(string clientId, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID cannot be null or empty", nameof(clientId));

        try
        {
            var existingCredentials = await _credentialStorage.GetCredentialsAsync(clientId);
            if (existingCredentials == null)
            {
                _logger.LogWarning("Attempted to change status for non-existent client {ClientId}", clientId);
                return false;
            }

            existingCredentials.IsActive = isActive;

            var success = await _credentialStorage.UpdateCredentialsAsync(existingCredentials);
            if (success)
            {
                _logger.LogInformation("Set client {ClientId} active status to {IsActive}", clientId, isActive);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set active status for client {ClientId}", clientId);
            return false;
        }
    }

    /// <summary>
    /// 设置客户端过期日期 / Sets client expiration date
    /// </summary>
    /// <param name="clientId">客户端ID / Client ID</param>
    /// <param name="expiresAt">过期时间 / Expiration time</param>
    /// <returns>是否设置成功 / Whether setting was successful</returns>
    /// <exception cref="ArgumentException">当客户端ID为空时抛出 / Thrown when client ID is empty</exception>
    public async Task<bool> SetClientExpirationAsync(string clientId, DateTime? expiresAt)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID cannot be null or empty", nameof(clientId));

        try
        {
            var existingCredentials = await _credentialStorage.GetCredentialsAsync(clientId);
            if (existingCredentials == null)
            {
                _logger.LogWarning("Attempted to set expiration for non-existent client {ClientId}", clientId);
                return false;
            }

            existingCredentials.ExpiresAt = expiresAt;

            var success = await _credentialStorage.UpdateCredentialsAsync(existingCredentials);
            if (success)
            {
                _logger.LogInformation("Set client {ClientId} expiration to {ExpiresAt}", clientId, expiresAt);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set expiration for client {ClientId}", clientId);
            return false;
        }
    }

    /// <summary>
    /// 列出所有客户端及其基本信息 / Lists all clients with their basic information
    /// </summary>
    /// <returns>客户端信息列表 / List of client information</returns>
    public async Task<List<ClientInfo>> ListClientsAsync()
    {
        try
        {
            var clientIds = await _credentialStorage.ListClientIdsAsync();
            var clients = new List<ClientInfo>();

            foreach (var clientId in clientIds)
            {
                var credentials = await _credentialStorage.GetCredentialsAsync(clientId);
                if (credentials != null)
                {
                    clients.Add(new ClientInfo
                    {
                        ClientId = credentials.ClientId,
                        ClientName = credentials.ClientName,
                        IsActive = credentials.IsActive,
                        CreatedAt = credentials.CreatedAt,
                        ExpiresAt = credentials.ExpiresAt,
                        Permissions = new List<string>(credentials.Permissions)
                    });
                }
            }

            return clients.OrderBy(c => c.ClientId).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list clients");
            return new List<ClientInfo>();
        }
    }

    /// <summary>
    /// 删除客户端 / Deletes a client
    /// </summary>
    /// <param name="clientId">客户端ID / Client ID</param>
    /// <returns>是否删除成功 / Whether deletion was successful</returns>
    /// <exception cref="ArgumentException">当客户端ID为空时抛出 / Thrown when client ID is empty</exception>
    public async Task<bool> DeleteClientAsync(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID cannot be null or empty", nameof(clientId));

        try
        {
            var success = await _credentialStorage.DeleteCredentialsAsync(clientId);
            if (success)
            {
                _logger.LogInformation("Deleted client {ClientId}", clientId);
            }
            else
            {
                _logger.LogWarning("Attempted to delete non-existent client {ClientId}", clientId);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete client {ClientId}", clientId);
            return false;
        }
    }

    /// <summary>
    /// 验证客户端凭据 / Validates client credentials
    /// </summary>
    /// <param name="clientId">客户端ID / Client ID</param>
    /// <param name="clientSecret">客户端密钥 / Client secret</param>
    /// <returns>是否验证通过 / Whether validation passed</returns>
    public async Task<bool> ValidateClientAsync(string clientId, string clientSecret)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return false;

        try
        {
            var storedCredentials = await _credentialStorage.GetCredentialsAsync(clientId);
            if (storedCredentials == null || !storedCredentials.IsActive || storedCredentials.IsExpired)
            {
                return false;
            }

            return storedCredentials.VerifySecret(clientSecret, storedCredentials.ClientSecret);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating client {ClientId}", clientId);
            return false;
        }
    }

    /// <summary>
    /// 生成加密安全的客户端密钥 / Generates a cryptographically secure client secret
    /// </summary>
    /// <returns>安全的客户端密钥 / Secure client secret</returns>
    private string GenerateSecureSecret()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32]; // 256位 / 256 bits
        rng.GetBytes(bytes);
        
        // 转换为base64并使其更易读 / Convert to base64 and make it more readable
        var base64 = Convert.ToBase64String(bytes);
        
        // 移除填充并使其URL安全 / Remove padding and make URL-safe
        return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>
    /// 用于列表显示的客户端信息（不包含敏感数据）/ Client information for listing purposes (without sensitive data)
    /// </summary>
    public class ClientInfo
    {
        /// <summary>
        /// 客户端ID / Client ID
        /// </summary>
        public string ClientId { get; set; } = string.Empty;
        
        /// <summary>
        /// 客户端名称 / Client name
        /// </summary>
        public string? ClientName { get; set; }
        
        /// <summary>
        /// 是否激活 / Whether active
        /// </summary>
        public bool IsActive { get; set; }
        
        /// <summary>
        /// 创建时间 / Creation time
        /// </summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// 过期时间 / Expiration time
        /// </summary>
        public DateTime? ExpiresAt { get; set; }
        
        /// <summary>
        /// 权限列表 / Permissions list
        /// </summary>
        public List<string> Permissions { get; set; } = new();
        
        /// <summary>
        /// 是否已过期 / Whether expired
        /// </summary>
        public bool IsExpired => ExpiresAt.HasValue && DateTime.Now > ExpiresAt.Value;
    }
}