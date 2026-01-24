using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Security.Cryptography;
using System.Text;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Utility service for managing client credentials
/// </summary>
public class ClientCredentialManager
{
    private readonly ILogger<ClientCredentialManager> _logger;
    private readonly ICredentialStorage _credentialStorage;

    public ClientCredentialManager(ILogger<ClientCredentialManager> logger, ICredentialStorage credentialStorage)
    {
        _logger = logger;
        _credentialStorage = credentialStorage ?? throw new ArgumentNullException(nameof(credentialStorage));
    }

    /// <summary>
    /// Creates a new client with default backup permissions
    /// </summary>
    public async Task<ClientCredentials> CreateClientAsync(string clientId, string? clientName = null, string[]? customPermissions = null)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID cannot be null or empty", nameof(clientId));

        try
        {
            // Check if client already exists
            var existingClient = await _credentialStorage.GetCredentialsAsync(clientId);
            if (existingClient != null)
            {
                throw new InvalidOperationException($"Client with ID '{clientId}' already exists");
            }

            // Generate secure client secret
            var clientSecret = GenerateSecureSecret();

            // Use custom permissions or default backup client permissions
            var permissions = customPermissions?.ToList() ?? BackupPermissions.DefaultClientPermissions.ToList();

            var credentials = new ClientCredentials
            {
                ClientId = clientId,
                ClientSecret = clientSecret,
                ClientName = clientName ?? clientId,
                Permissions = permissions,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var success = await _credentialStorage.StoreCredentialsAsync(credentials);
            if (!success)
            {
                throw new InvalidOperationException("Failed to store client credentials");
            }

            _logger.LogInformation("Created new client {ClientId} with permissions: {Permissions}", 
                clientId, string.Join(", ", permissions));

            // Return credentials with the original (unhashed) secret for initial setup
            return credentials;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create client {ClientId}", clientId);
            throw;
        }
    }

    /// <summary>
    /// Updates client permissions
    /// </summary>
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

            // Validate permissions
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
    /// Resets client secret
    /// </summary>
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

            // Generate new secure secret
            var newSecret = GenerateSecureSecret();
            existingCredentials.ClientSecret = newSecret;

            var success = await _credentialStorage.UpdateCredentialsAsync(existingCredentials);
            if (success)
            {
                _logger.LogInformation("Reset secret for client {ClientId}", clientId);
                return newSecret; // Return the new secret (unhashed) for client configuration
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
    /// Activates or deactivates a client
    /// </summary>
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
    /// Sets client expiration date
    /// </summary>
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
    /// Lists all clients with their basic information
    /// </summary>
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
    /// Deletes a client
    /// </summary>
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
    /// Validates client credentials
    /// </summary>
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
    /// Generates a cryptographically secure client secret
    /// </summary>
    private string GenerateSecureSecret()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32]; // 256 bits
        rng.GetBytes(bytes);
        
        // Convert to base64 and make it more readable
        var base64 = Convert.ToBase64String(bytes);
        
        // Remove padding and make URL-safe
        return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>
    /// Client information for listing purposes (without sensitive data)
    /// </summary>
    public class ClientInfo
    {
        public string ClientId { get; set; } = string.Empty;
        public string? ClientName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public List<string> Permissions { get; set; } = new();
        public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
    }
}