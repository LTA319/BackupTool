using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Secure credential storage implementation using file-based encrypted storage
/// </summary>
public class SecureCredentialStorage : ICredentialStorage
{
    private readonly ILogger<SecureCredentialStorage> _logger;
    private readonly CredentialStorageConfig _config;
    private readonly object _lockObject = new();
    private readonly Dictionary<string, ClientCredentials> _credentialsCache = new();
    private DateTime _lastCacheUpdate = DateTime.MinValue;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);

    public SecureCredentialStorage(ILogger<SecureCredentialStorage> logger, CredentialStorageConfig config)
    {
        _logger = logger;
        _config = config ?? throw new ArgumentNullException(nameof(config));
        
        ValidateConfiguration();
        EnsureCredentialsDirectoryExists();
    }

    /// <summary>
    /// Stores client credentials securely
    /// </summary>
    public async Task<bool> StoreCredentialsAsync(ClientCredentials credentials)
    {
        if (credentials == null)
            throw new ArgumentNullException(nameof(credentials));

        try
        {
            lock (_lockObject)
            {
                // Hash the secret before storing
                var hashedCredentials = new ClientCredentials
                {
                    ClientId = credentials.ClientId,
                    ClientSecret = credentials.HashSecret(), // Store hashed version
                    ClientName = credentials.ClientName,
                    Permissions = new List<string>(credentials.Permissions),
                    IsActive = credentials.IsActive,
                    CreatedAt = credentials.CreatedAt,
                    ExpiresAt = credentials.ExpiresAt
                };

                // Load existing credentials
                var allCredentials = LoadCredentialsFromFile();
                
                // Add or update credentials
                allCredentials[credentials.ClientId] = hashedCredentials;
                
                // Save to file
                SaveCredentialsToFile(allCredentials);
                
                // Update cache
                _credentialsCache[credentials.ClientId] = hashedCredentials;
                _lastCacheUpdate = DateTime.UtcNow;
            }

            _logger.LogInformation("Successfully stored credentials for client {ClientId}", credentials.ClientId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store credentials for client {ClientId}", credentials.ClientId);
            return false;
        }
    }

    /// <summary>
    /// Retrieves client credentials by client ID
    /// </summary>
    public async Task<ClientCredentials?> GetCredentialsAsync(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return null;

        try
        {
            lock (_lockObject)
            {
                // Check cache first
                if (IsCacheValid() && _credentialsCache.TryGetValue(clientId, out var cachedCredentials))
                {
                    return cachedCredentials;
                }

                // Load from file
                var allCredentials = LoadCredentialsFromFile();
                
                if (allCredentials.TryGetValue(clientId, out var credentials))
                {
                    // Update cache
                    _credentialsCache[clientId] = credentials;
                    return credentials;
                }

                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve credentials for client {ClientId}", clientId);
            return null;
        }
    }

    /// <summary>
    /// Updates client credentials
    /// </summary>
    public async Task<bool> UpdateCredentialsAsync(ClientCredentials credentials)
    {
        if (credentials == null)
            throw new ArgumentNullException(nameof(credentials));

        try
        {
            lock (_lockObject)
            {
                var allCredentials = LoadCredentialsFromFile();
                
                if (!allCredentials.ContainsKey(credentials.ClientId))
                {
                    _logger.LogWarning("Attempted to update non-existent client {ClientId}", credentials.ClientId);
                    return false;
                }

                // Hash the secret before storing
                var hashedCredentials = new ClientCredentials
                {
                    ClientId = credentials.ClientId,
                    ClientSecret = credentials.HashSecret(), // Store hashed version
                    ClientName = credentials.ClientName,
                    Permissions = new List<string>(credentials.Permissions),
                    IsActive = credentials.IsActive,
                    CreatedAt = allCredentials[credentials.ClientId].CreatedAt, // Preserve original creation time
                    ExpiresAt = credentials.ExpiresAt
                };

                allCredentials[credentials.ClientId] = hashedCredentials;
                SaveCredentialsToFile(allCredentials);
                
                // Update cache
                _credentialsCache[credentials.ClientId] = hashedCredentials;
                _lastCacheUpdate = DateTime.UtcNow;
            }

            _logger.LogInformation("Successfully updated credentials for client {ClientId}", credentials.ClientId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update credentials for client {ClientId}", credentials.ClientId);
            return false;
        }
    }

    /// <summary>
    /// Deletes client credentials
    /// </summary>
    public async Task<bool> DeleteCredentialsAsync(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return false;

        try
        {
            lock (_lockObject)
            {
                var allCredentials = LoadCredentialsFromFile();
                
                if (!allCredentials.Remove(clientId))
                {
                    _logger.LogWarning("Attempted to delete non-existent client {ClientId}", clientId);
                    return false;
                }

                SaveCredentialsToFile(allCredentials);
                
                // Remove from cache
                _credentialsCache.Remove(clientId);
            }

            _logger.LogInformation("Successfully deleted credentials for client {ClientId}", clientId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete credentials for client {ClientId}", clientId);
            return false;
        }
    }

    /// <summary>
    /// Lists all client IDs
    /// </summary>
    public async Task<List<string>> ListClientIdsAsync()
    {
        try
        {
            lock (_lockObject)
            {
                var allCredentials = LoadCredentialsFromFile();
                return allCredentials.Keys.ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list client IDs");
            return new List<string>();
        }
    }

    /// <summary>
    /// Validates the integrity of the credential storage
    /// </summary>
    public async Task<bool> ValidateStorageIntegrityAsync()
    {
        try
        {
            lock (_lockObject)
            {
                if (!File.Exists(_config.CredentialsFilePath))
                {
                    _logger.LogInformation("Credentials file does not exist, storage is valid (empty)");
                    return true;
                }

                // Try to load and decrypt the file
                var credentials = LoadCredentialsFromFile();
                
                // Validate each credential entry
                foreach (var kvp in credentials)
                {
                    var clientId = kvp.Key;
                    var creds = kvp.Value;
                    
                    if (string.IsNullOrWhiteSpace(creds.ClientId) || 
                        string.IsNullOrWhiteSpace(creds.ClientSecret) ||
                        creds.ClientId != clientId)
                    {
                        _logger.LogError("Invalid credential entry found for client {ClientId}", clientId);
                        return false;
                    }
                }

                _logger.LogDebug("Credential storage integrity validation passed");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Credential storage integrity validation failed");
            return false;
        }
    }

    /// <summary>
    /// Validates the configuration
    /// </summary>
    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_config.CredentialsFilePath))
            throw new ArgumentException("Credentials file path is required", nameof(_config.CredentialsFilePath));

        if (string.IsNullOrWhiteSpace(_config.EncryptionKey))
            throw new ArgumentException("Encryption key is required", nameof(_config.EncryptionKey));

        if (_config.EncryptionKey.Length < 16)
            throw new ArgumentException("Encryption key must be at least 16 characters", nameof(_config.EncryptionKey));
    }

    /// <summary>
    /// Ensures the credentials directory exists
    /// </summary>
    private void EnsureCredentialsDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_config.CredentialsFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger.LogInformation("Created credentials directory: {Directory}", directory);
        }
    }

    /// <summary>
    /// Checks if the cache is still valid
    /// </summary>
    private bool IsCacheValid()
    {
        return DateTime.UtcNow - _lastCacheUpdate < _cacheExpiry;
    }

    /// <summary>
    /// Loads credentials from the encrypted file
    /// </summary>
    private Dictionary<string, ClientCredentials> LoadCredentialsFromFile()
    {
        if (!File.Exists(_config.CredentialsFilePath))
        {
            _logger.LogDebug("Credentials file does not exist, returning empty dictionary");
            return new Dictionary<string, ClientCredentials>();
        }

        try
        {
            var encryptedData = File.ReadAllBytes(_config.CredentialsFilePath);
            var decryptedData = DecryptData(encryptedData);
            var json = Encoding.UTF8.GetString(decryptedData);
            
            var credentials = JsonSerializer.Deserialize<Dictionary<string, ClientCredentials>>(json);
            
            // Update cache
            _credentialsCache.Clear();
            if (credentials != null)
            {
                foreach (var kvp in credentials)
                {
                    _credentialsCache[kvp.Key] = kvp.Value;
                }
            }
            _lastCacheUpdate = DateTime.UtcNow;
            
            return credentials ?? new Dictionary<string, ClientCredentials>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load credentials from file");
            throw new InvalidOperationException("Failed to load credentials from storage", ex);
        }
    }

    /// <summary>
    /// Saves credentials to the encrypted file
    /// </summary>
    private void SaveCredentialsToFile(Dictionary<string, ClientCredentials> credentials)
    {
        try
        {
            var json = JsonSerializer.Serialize(credentials, new JsonSerializerOptions 
            { 
                WriteIndented = false 
            });
            
            var data = Encoding.UTF8.GetBytes(json);
            var encryptedData = EncryptData(data);
            
            // Write to temporary file first, then move to avoid corruption
            var tempFile = _config.CredentialsFilePath + ".tmp";
            File.WriteAllBytes(tempFile, encryptedData);
            
            // Atomic move
            if (File.Exists(_config.CredentialsFilePath))
            {
                File.Delete(_config.CredentialsFilePath);
            }
            File.Move(tempFile, _config.CredentialsFilePath);
            
            _logger.LogDebug("Successfully saved credentials to file");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save credentials to file");
            throw new InvalidOperationException("Failed to save credentials to storage", ex);
        }
    }

    /// <summary>
    /// Encrypts data using AES encryption
    /// </summary>
    private byte[] EncryptData(byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = DeriveKey(_config.EncryptionKey);
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        using var msEncrypt = new MemoryStream();
        
        // Write IV first
        msEncrypt.Write(aes.IV, 0, aes.IV.Length);
        
        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        {
            csEncrypt.Write(data, 0, data.Length);
        }

        var encryptedData = msEncrypt.ToArray();
        
        // Apply additional Windows DPAPI encryption if configured and available
        // Note: DPAPI is disabled in this implementation for cross-platform compatibility
        // if (_config.UseWindowsDPAPI && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        // {
        //     try
        //     {
        //         encryptedData = System.Security.Cryptography.ProtectedData.Protect(encryptedData, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
        //         _logger.LogDebug("Applied Windows DPAPI protection to encrypted data");
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogWarning(ex, "Failed to apply Windows DPAPI protection, using AES only");
        //     }
        // }

        return encryptedData;
    }

    /// <summary>
    /// Decrypts data using AES decryption
    /// </summary>
    private byte[] DecryptData(byte[] encryptedData)
    {
        // Remove Windows DPAPI protection if it was applied
        // Note: DPAPI is disabled in this implementation for cross-platform compatibility
        // if (_config.UseWindowsDPAPI && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        // {
        //     try
        //     {
        //         encryptedData = System.Security.Cryptography.ProtectedData.Unprotect(encryptedData, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
        //         _logger.LogDebug("Removed Windows DPAPI protection from encrypted data");
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogWarning(ex, "Failed to remove Windows DPAPI protection, assuming AES-only encryption");
        //     }
        // }

        using var aes = Aes.Create();
        aes.Key = DeriveKey(_config.EncryptionKey);

        // Extract IV from the beginning of the data
        var iv = new byte[aes.IV.Length];
        Array.Copy(encryptedData, 0, iv, 0, iv.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var msDecrypt = new MemoryStream(encryptedData, iv.Length, encryptedData.Length - iv.Length);
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var msResult = new MemoryStream();
        
        csDecrypt.CopyTo(msResult);
        return msResult.ToArray();
    }

    /// <summary>
    /// Derives a 256-bit key from the encryption key string
    /// </summary>
    private byte[] DeriveKey(string key)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
    }
}