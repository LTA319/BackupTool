using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Interface for secure credential storage operations
/// Provides methods for managing client credentials with enhanced security features
/// </summary>
public interface ISecureCredentialStorage
{
    /// <summary>
    /// Ensures that default client credentials exist in the database
    /// Creates default credentials if they don't exist, preserves existing ones
    /// </summary>
    /// <returns>True if default credentials exist or were created successfully</returns>
    Task<bool> EnsureDefaultCredentialsExistAsync();

    /// <summary>
    /// Retrieves the default client credentials
    /// </summary>
    /// <returns>Default client credentials or null if not found</returns>
    Task<ClientCredentials?> GetDefaultCredentialsAsync();

    /// <summary>
    /// Retrieves client credentials by client ID
    /// </summary>
    /// <param name="clientId">Client identifier</param>
    /// <returns>Client credentials or null if not found</returns>
    Task<ClientCredentials?> GetCredentialsByClientIdAsync(string clientId);

    /// <summary>
    /// Validates client credentials against stored values
    /// </summary>
    /// <param name="clientId">Client identifier</param>
    /// <param name="clientSecret">Client secret to validate</param>
    /// <returns>True if credentials are valid, false otherwise</returns>
    Task<bool> ValidateCredentialsAsync(string clientId, string clientSecret);

    /// <summary>
    /// Stores client credentials securely
    /// </summary>
    /// <param name="credentials">Client credentials to store</param>
    /// <returns>True if stored successfully, false otherwise</returns>
    Task<bool> StoreCredentialsAsync(ClientCredentials credentials);

    /// <summary>
    /// Checks if credentials exist for the specified client ID
    /// </summary>
    /// <param name="clientId">Client identifier</param>
    /// <returns>True if credentials exist, false otherwise</returns>
    Task<bool> CredentialsExistAsync(string clientId);

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