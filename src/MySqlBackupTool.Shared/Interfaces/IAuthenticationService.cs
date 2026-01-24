using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Interface for client authentication operations
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Authenticates a client using credentials
    /// </summary>
    /// <param name="request">Authentication request with client credentials</param>
    /// <returns>Authentication response with token if successful</returns>
    Task<AuthenticationResponse> AuthenticateAsync(AuthenticationRequest request);

    /// <summary>
    /// Validates an authentication token
    /// </summary>
    /// <param name="token">Token to validate</param>
    /// <returns>True if token is valid, false otherwise</returns>
    Task<bool> ValidateTokenAsync(string token);

    /// <summary>
    /// Gets the authorization context for a token
    /// </summary>
    /// <param name="token">Authentication token</param>
    /// <returns>Authorization context or null if token is invalid</returns>
    Task<AuthorizationContext?> GetAuthorizationContextAsync(string token);

    /// <summary>
    /// Revokes an authentication token
    /// </summary>
    /// <param name="token">Token to revoke</param>
    /// <returns>True if token was revoked, false if not found</returns>
    Task<bool> RevokeTokenAsync(string token);

    /// <summary>
    /// Cleans up expired tokens
    /// </summary>
    /// <returns>Number of tokens cleaned up</returns>
    Task<int> CleanupExpiredTokensAsync();
}

/// <summary>
/// Interface for authorization operations
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Checks if a client is authorized to perform an operation
    /// </summary>
    /// <param name="context">Authorization context</param>
    /// <param name="operation">Operation being requested</param>
    /// <returns>True if authorized, false otherwise</returns>
    Task<bool> IsAuthorizedAsync(AuthorizationContext context, string operation);

    /// <summary>
    /// Checks if a client has a specific permission
    /// </summary>
    /// <param name="context">Authorization context</param>
    /// <param name="permission">Permission to check</param>
    /// <returns>True if client has permission, false otherwise</returns>
    Task<bool> HasPermissionAsync(AuthorizationContext context, string permission);

    /// <summary>
    /// Gets all permissions for a client
    /// </summary>
    /// <param name="clientId">Client identifier</param>
    /// <returns>List of permissions</returns>
    Task<List<string>> GetClientPermissionsAsync(string clientId);

    /// <summary>
    /// Logs an authorization attempt
    /// </summary>
    /// <param name="context">Authorization context</param>
    /// <param name="operation">Operation attempted</param>
    /// <param name="success">Whether authorization was successful</param>
    /// <param name="reason">Reason for authorization result</param>
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