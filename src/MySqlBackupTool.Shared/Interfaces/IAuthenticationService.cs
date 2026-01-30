using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 客户端身份验证操作的接口
/// Interface for client authentication operations
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// 使用凭据对客户端进行身份验证
    /// Authenticates a client using credentials
    /// </summary>
    /// <param name="request">包含客户端凭据的身份验证请求 / Authentication request with client credentials</param>
    /// <returns>如果成功则返回包含令牌的身份验证响应 / Authentication response with token if successful</returns>
    Task<AuthenticationResponse> AuthenticateAsync(AuthenticationRequest request);

    /// <summary>
    /// 验证身份验证令牌
    /// Validates an authentication token
    /// </summary>
    /// <param name="token">要验证的令牌 / Token to validate</param>
    /// <returns>如果令牌有效返回true，否则返回false / True if token is valid, false otherwise</returns>
    Task<bool> ValidateTokenAsync(string token);

    /// <summary>
    /// 获取令牌的授权上下文
    /// Gets the authorization context for a token
    /// </summary>
    /// <param name="token">身份验证令牌 / Authentication token</param>
    /// <returns>授权上下文，如果令牌无效则返回null / Authorization context or null if token is invalid</returns>
    Task<AuthorizationContext?> GetAuthorizationContextAsync(string token);

    /// <summary>
    /// 撤销身份验证令牌
    /// Revokes an authentication token
    /// </summary>
    /// <param name="token">要撤销的令牌 / Token to revoke</param>
    /// <returns>如果令牌被撤销返回true，如果未找到返回false / True if token was revoked, false if not found</returns>
    Task<bool> RevokeTokenAsync(string token);

    /// <summary>
    /// 清理过期的令牌
    /// Cleans up expired tokens
    /// </summary>
    /// <returns>清理的令牌数量 / Number of tokens cleaned up</returns>
    Task<int> CleanupExpiredTokensAsync();
}

/// <summary>
/// 授权操作的接口
/// Interface for authorization operations
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// 检查客户端是否有权执行操作
    /// Checks if a client is authorized to perform an operation
    /// </summary>
    /// <param name="context">授权上下文 / Authorization context</param>
    /// <param name="operation">请求的操作 / Operation being requested</param>
    /// <returns>如果已授权返回true，否则返回false / True if authorized, false otherwise</returns>
    Task<bool> IsAuthorizedAsync(AuthorizationContext context, string operation);

    /// <summary>
    /// 检查客户端是否具有特定权限
    /// Checks if a client has a specific permission
    /// </summary>
    /// <param name="context">授权上下文 / Authorization context</param>
    /// <param name="permission">要检查的权限 / Permission to check</param>
    /// <returns>如果客户端具有权限返回true，否则返回false / True if client has permission, false otherwise</returns>
    Task<bool> HasPermissionAsync(AuthorizationContext context, string permission);

    /// <summary>
    /// 获取客户端的所有权限
    /// Gets all permissions for a client
    /// </summary>
    /// <param name="clientId">客户端标识符 / Client identifier</param>
    /// <returns>权限列表 / List of permissions</returns>
    Task<List<string>> GetClientPermissionsAsync(string clientId);

    /// <summary>
    /// 记录授权尝试
    /// Logs an authorization attempt
    /// </summary>
    /// <param name="context">授权上下文 / Authorization context</param>
    /// <param name="operation">尝试的操作 / Operation attempted</param>
    /// <param name="success">授权是否成功 / Whether authorization was successful</param>
    /// <param name="reason">授权结果的原因 / Reason for authorization result</param>
    Task LogAuthorizationAttemptAsync(AuthorizationContext context, string operation, bool success, string? reason = null);
}

/// <summary>
/// Interface for secure credential storage
/// </summary>
public interface ICredentialStorage
{
    /// <summary>
    /// Stores client credentials securely
    /// </summary>
    /// <param name="credentials">Client credentials to store</param>
    /// <returns>True if stored successfully, false otherwise</returns>
    Task<bool> StoreCredentialsAsync(ClientCredentials credentials);

    /// <summary>
    /// Retrieves client credentials by client ID
    /// </summary>
    /// <param name="clientId">Client identifier</param>
    /// <returns>Client credentials or null if not found</returns>
    Task<ClientCredentials?> GetCredentialsAsync(string clientId);

    /// <summary>
    /// Updates client credentials
    /// </summary>
    /// <param name="credentials">Updated credentials</param>
    /// <returns>True if updated successfully, false otherwise</returns>
    Task<bool> UpdateCredentialsAsync(ClientCredentials credentials);

    /// <summary>
    /// Deletes client credentials
    /// </summary>
    /// <param name="clientId">Client identifier</param>
    /// <returns>True if deleted successfully, false if not found</returns>
    Task<bool> DeleteCredentialsAsync(string clientId);

    /// <summary>
    /// Lists all client IDs
    /// </summary>
    /// <returns>List of client identifiers</returns>
    Task<List<string>> ListClientIdsAsync();

    /// <summary>
    /// Validates the integrity of the credential storage
    /// </summary>
    /// <returns>True if storage is valid, false if corrupted</returns>
    Task<bool> ValidateStorageIntegrityAsync();
}

/// <summary>
/// Interface for authentication token management
/// </summary>
public interface ITokenManager
{
    /// <summary>
    /// Generates a new authentication token
    /// </summary>
    /// <param name="clientId">Client identifier</param>
    /// <param name="permissions">Client permissions</param>
    /// <param name="expirationHours">Token expiration in hours (default: 24)</param>
    /// <returns>Generated authentication token</returns>
    Task<AuthenticationToken> GenerateTokenAsync(string clientId, List<string> permissions, int expirationHours = 24);

    /// <summary>
    /// Validates and retrieves a token
    /// </summary>
    /// <param name="tokenId">Token identifier</param>
    /// <returns>Authentication token or null if invalid</returns>
    Task<AuthenticationToken?> GetTokenAsync(string tokenId);

    /// <summary>
    /// Updates token last used timestamp
    /// </summary>
    /// <param name="tokenId">Token identifier</param>
    /// <returns>True if updated successfully</returns>
    Task<bool> UpdateTokenUsageAsync(string tokenId);

    /// <summary>
    /// Revokes a token
    /// </summary>
    /// <param name="tokenId">Token identifier</param>
    /// <returns>True if revoked successfully</returns>
    Task<bool> RevokeTokenAsync(string tokenId);

    /// <summary>
    /// Revokes all tokens for a client
    /// </summary>
    /// <param name="clientId">Client identifier</param>
    /// <returns>Number of tokens revoked</returns>
    Task<int> RevokeClientTokensAsync(string clientId);

    /// <summary>
    /// Cleans up expired tokens
    /// </summary>
    /// <returns>Number of tokens cleaned up</returns>
    Task<int> CleanupExpiredTokensAsync();

    /// <summary>
    /// Gets all active tokens for a client
    /// </summary>
    /// <param name="clientId">Client identifier</param>
    /// <returns>List of active tokens</returns>
    Task<List<AuthenticationToken>> GetClientTokensAsync(string clientId);
}