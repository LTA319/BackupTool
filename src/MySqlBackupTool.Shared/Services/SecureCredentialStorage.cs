using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 使用基于文件的加密存储的安全凭据存储实现 / Secure credential storage implementation using file-based encrypted storage
/// 提供客户端凭据的安全存储、检索、更新和删除功能 / Provides secure storage, retrieval, update and deletion of client credentials
/// </summary>
public class SecureCredentialStorage : ICredentialStorage, ISecureCredentialStorage
{
    private readonly ILogger<SecureCredentialStorage> _logger;
    private readonly CredentialStorageConfig _config;
    private readonly object _lockObject = new();
    private readonly Dictionary<string, ClientCredentials> _credentialsCache = new();
    private DateTime _lastCacheUpdate = DateTime.MinValue;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 初始化安全凭据存储 / Initialize secure credential storage
    /// </summary>
    /// <param name="logger">日志记录器 / Logger instance</param>
    /// <param name="config">凭据存储配置 / Credential storage configuration</param>
    /// <exception cref="ArgumentNullException">当config为null时抛出 / Thrown when config is null</exception>
    public SecureCredentialStorage(ILogger<SecureCredentialStorage> logger, CredentialStorageConfig config)
    {
        _logger = logger;
        _config = config ?? throw new ArgumentNullException(nameof(config));
        
        ValidateConfiguration();
        EnsureCredentialsDirectoryExists();
    }

    /// <summary>
    /// 安全存储客户端凭据 / Stores client credentials securely
    /// 对密钥进行哈希处理并使用AES加密存储到文件 / Hashes the secret and stores to file using AES encryption
    /// </summary>
    /// <param name="credentials">要存储的客户端凭据 / Client credentials to store</param>
    /// <returns>存储成功返回true，失败返回false / Returns true if stored successfully, false if failed</returns>
    /// <exception cref="ArgumentNullException">当credentials为null时抛出 / Thrown when credentials is null</exception>
    public async Task<bool> StoreCredentialsAsync(ClientCredentials credentials)
    {
        if (credentials == null)
            throw new ArgumentNullException(nameof(credentials));

        try
        {
            lock (_lockObject)
            {
                // 存储前对密钥进行哈希处理 / Hash the secret before storing
                var hashedCredentials = new ClientCredentials
                {
                    ClientId = credentials.ClientId,
                    ClientSecret = credentials.HashSecret(), // 存储哈希版本 / Store hashed version
                    ClientName = credentials.ClientName,
                    Permissions = new List<string>(credentials.Permissions),
                    IsActive = credentials.IsActive,
                    CreatedAt = credentials.CreatedAt,
                    ExpiresAt = credentials.ExpiresAt
                };

                // 加载现有凭据 / Load existing credentials
                var allCredentials = LoadCredentialsFromFile();
                
                // 添加或更新凭据 / Add or update credentials
                allCredentials[credentials.ClientId] = hashedCredentials;
                
                // 保存到文件 / Save to file
                try
                {
                    SaveCredentialsToFile(allCredentials);
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "Critical error saving credentials to file for client {ClientId}", credentials.ClientId);
                    return false;
                }
                
                // 更新缓存 / Update cache
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
    /// 根据客户端ID检索客户端凭据 / Retrieves client credentials by client ID
    /// 首先检查缓存，然后从加密文件加载 / Checks cache first, then loads from encrypted file
    /// </summary>
    /// <param name="clientId">客户端ID / Client ID</param>
    /// <returns>客户端凭据或null（如果未找到） / Client credentials or null if not found</returns>
    public async Task<ClientCredentials?> GetCredentialsAsync(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return null;

        try
        {
            lock (_lockObject)
            {
                // 首先检查缓存 / Check cache first
                if (IsCacheValid() && _credentialsCache.TryGetValue(clientId, out var cachedCredentials))
                {
                    return cachedCredentials;
                }

                // 从文件加载 / Load from file
                var allCredentials = LoadCredentialsFromFile();
                
                if (allCredentials.TryGetValue(clientId, out var credentials))
                {
                    // 更新缓存 / Update cache
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
    /// 更新客户端凭据 / Updates client credentials
    /// 保留原始创建时间并更新其他字段 / Preserves original creation time and updates other fields
    /// </summary>
    /// <param name="credentials">要更新的客户端凭据 / Client credentials to update</param>
    /// <returns>更新成功返回true，失败返回false / Returns true if updated successfully, false if failed</returns>
    /// <exception cref="ArgumentNullException">当credentials为null时抛出 / Thrown when credentials is null</exception>
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

                // 存储前对密钥进行哈希处理 / Hash the secret before storing
                var hashedCredentials = new ClientCredentials
                {
                    ClientId = credentials.ClientId,
                    ClientSecret = credentials.HashSecret(), // 存储哈希版本 / Store hashed version
                    ClientName = credentials.ClientName,
                    Permissions = new List<string>(credentials.Permissions),
                    IsActive = credentials.IsActive,
                    CreatedAt = allCredentials[credentials.ClientId].CreatedAt, // 保留原始创建时间 / Preserve original creation time
                    ExpiresAt = credentials.ExpiresAt
                };

                allCredentials[credentials.ClientId] = hashedCredentials;
                SaveCredentialsToFile(allCredentials);
                
                // 更新缓存 / Update cache
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
    /// 删除客户端凭据 / Deletes client credentials
    /// </summary>
    /// <param name="clientId">要删除的客户端ID / Client ID to delete</param>
    /// <returns>删除成功返回true，失败返回false / Returns true if deleted successfully, false if failed</returns>
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
                
                // 从缓存中移除 / Remove from cache
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
    /// 列出所有客户端ID / Lists all client IDs
    /// </summary>
    /// <returns>客户端ID列表 / List of client IDs</returns>
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
    /// 验证凭据存储的完整性 / Validates the integrity of the credential storage
    /// 检查文件是否可以正确解密和读取 / Checks if file can be properly decrypted and read
    /// </summary>
    /// <returns>验证通过返回true，失败返回false / Returns true if validation passes, false if failed</returns>
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

                // 尝试加载和解密文件 / Try to load and decrypt the file
                var credentials = LoadCredentialsFromFile();
                
                // 验证每个凭据条目 / Validate each credential entry
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
    /// Ensures that default client credentials exist in the database
    /// Creates default credentials if they don't exist, preserves existing ones
    /// </summary>
    /// <returns>True if default credentials exist or were created successfully</returns>
    public async Task<bool> EnsureDefaultCredentialsExistAsync()
    {
        const string defaultClientId = "default-client";
        const string defaultClientSecret = "default-secret-2024";

        try
        {
            // Check if default credentials already exist
            var existingCredentials = await GetCredentialsAsync(defaultClientId);
            if (existingCredentials != null)
            {
                _logger.LogDebug("Default credentials already exist for client {ClientId}", defaultClientId);
                return true;
            }

            // Create default credentials
            var defaultCredentials = new ClientCredentials
            {
                ClientId = defaultClientId,
                ClientSecret = defaultClientSecret,
                ClientName = "Default Client",
                Permissions = new List<string> { "backup.upload", "backup.list" },
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await StoreCredentialsAsync(defaultCredentials);
            if (result)
            {
                _logger.LogInformation("Successfully created default credentials for client {ClientId}", defaultClientId);
            }
            else
            {
                _logger.LogError("Failed to create default credentials for client {ClientId}", defaultClientId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring default credentials exist");
            return false;
        }
    }

    /// <summary>
    /// Retrieves the default client credentials
    /// </summary>
    /// <returns>Default client credentials or null if not found</returns>
    public async Task<ClientCredentials?> GetDefaultCredentialsAsync()
    {
        const string defaultClientId = "default-client";
        
        try
        {
            return await GetCredentialsAsync(defaultClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving default credentials");
            return null;
        }
    }

    /// <summary>
    /// Retrieves client credentials by client ID
    /// </summary>
    /// <param name="clientId">Client identifier</param>
    /// <returns>Client credentials or null if not found</returns>
    public async Task<ClientCredentials?> GetCredentialsByClientIdAsync(string clientId)
    {
        return await GetCredentialsAsync(clientId);
    }

    /// <summary>
    /// Validates client credentials against stored values
    /// </summary>
    /// <param name="clientId">Client identifier</param>
    /// <param name="clientSecret">Client secret to validate</param>
    /// <returns>True if credentials are valid, false otherwise</returns>
    public async Task<bool> ValidateCredentialsAsync(string clientId, string clientSecret)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            _logger.LogWarning("Validation failed: ClientId or ClientSecret is null or empty");
            return false;
        }

        try
        {
            var storedCredentials = await GetCredentialsAsync(clientId);
            if (storedCredentials == null)
            {
                _logger.LogWarning("Validation failed: No credentials found for client {ClientId}", clientId);
                return false;
            }

            if (!storedCredentials.IsActive)
            {
                _logger.LogWarning("Validation failed: Client {ClientId} is not active", clientId);
                return false;
            }

            if (storedCredentials.IsExpired)
            {
                _logger.LogWarning("Validation failed: Credentials for client {ClientId} have expired", clientId);
                return false;
            }

            // The stored secret is already hashed, so we need to hash the provided secret and compare
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(clientSecret));
            var providedHash = Convert.ToBase64String(hashedBytes);
            
            var isValid = string.Equals(providedHash, storedCredentials.ClientSecret, StringComparison.Ordinal);
            
            if (isValid)
            {
                _logger.LogDebug("Credentials validated successfully for client {ClientId}", clientId);
            }
            else
            {
                _logger.LogWarning("Validation failed: Invalid secret for client {ClientId}", clientId);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating credentials for client {ClientId}", clientId);
            return false;
        }
    }

    /// <summary>
    /// Checks if credentials exist for the specified client ID
    /// </summary>
    /// <param name="clientId">Client identifier</param>
    /// <returns>True if credentials exist, false otherwise</returns>
    public async Task<bool> CredentialsExistAsync(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return false;

        try
        {
            var credentials = await GetCredentialsAsync(clientId);
            return credentials != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if credentials exist for client {ClientId}", clientId);
            return false;
        }
    }

    /// <summary>
    /// 验证配置 / Validates the configuration
    /// 检查必需的配置参数是否有效 / Checks if required configuration parameters are valid
    /// </summary>
    /// <exception cref="ArgumentException">当配置无效时抛出 / Thrown when configuration is invalid</exception>
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
    /// 确保凭据目录存在 / Ensures the credentials directory exists
    /// 如果目录不存在则创建它 / Creates the directory if it doesn't exist
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
    /// 检查缓存是否仍然有效 / Checks if the cache is still valid
    /// </summary>
    /// <returns>缓存有效返回true，否则返回false / Returns true if cache is valid, false otherwise</returns>
    private bool IsCacheValid()
    {
        return DateTime.UtcNow - _lastCacheUpdate < _cacheExpiry;
    }

    /// <summary>
    /// 从加密文件加载凭据 / Loads credentials from the encrypted file
    /// 解密文件内容并反序列化为凭据字典 / Decrypts file content and deserializes to credentials dictionary
    /// </summary>
    /// <returns>凭据字典 / Dictionary of credentials</returns>
    /// <exception cref="InvalidOperationException">当加载失败时抛出 / Thrown when loading fails</exception>
    private Dictionary<string, ClientCredentials> LoadCredentialsFromFile()
    {
        if (!File.Exists(_config.CredentialsFilePath))
        {
            _logger.LogDebug("Credentials file does not exist, returning empty dictionary");
            return new Dictionary<string, ClientCredentials>();
        }

        try
        {
            var fileInfo = new FileInfo(_config.CredentialsFilePath);
            if (fileInfo.Length == 0)
            {
                _logger.LogDebug("Credentials file is empty, returning empty dictionary");
                return new Dictionary<string, ClientCredentials>();
            }

            var encryptedData = File.ReadAllBytes(_config.CredentialsFilePath);
            if (encryptedData.Length == 0)
            {
                _logger.LogDebug("Credentials file contains no data, returning empty dictionary");
                return new Dictionary<string, ClientCredentials>();
            }

            var decryptedData = DecryptData(encryptedData);
            var json = Encoding.UTF8.GetString(decryptedData);
            
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogDebug("Decrypted credentials data is empty, returning empty dictionary");
                return new Dictionary<string, ClientCredentials>();
            }
            
            var credentials = JsonSerializer.Deserialize<Dictionary<string, ClientCredentials>>(json);
            
            // 更新缓存 / Update cache
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
    /// 将凭据保存到加密文件 / Saves credentials to the encrypted file
    /// 序列化凭据字典并加密保存到文件 / Serializes credentials dictionary and saves encrypted to file
    /// </summary>
    /// <param name="credentials">要保存的凭据字典 / Dictionary of credentials to save</param>
    /// <exception cref="InvalidOperationException">当保存失败时抛出 / Thrown when saving fails</exception>
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
            
            // 先写入临时文件，然后移动以避免损坏 / Write to temporary file first, then move to avoid corruption
            var tempFile = _config.CredentialsFilePath + ".tmp";
            
            // 确保临时文件不存在 / Ensure temp file doesn't exist
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
            
            File.WriteAllBytes(tempFile, encryptedData);
            
            // 原子移动 / Atomic move - handle Windows file locking issues
            if (File.Exists(_config.CredentialsFilePath))
            {
                // On Windows, we need to delete the target file first
                File.Delete(_config.CredentialsFilePath);
            }
            
            // Move temp file to final location
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
    /// 使用AES加密数据 / Encrypts data using AES encryption
    /// 生成随机IV并将其添加到加密数据的开头 / Generates random IV and prepends it to encrypted data
    /// </summary>
    /// <param name="data">要加密的数据 / Data to encrypt</param>
    /// <returns>加密后的数据 / Encrypted data</returns>
    private byte[] EncryptData(byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = DeriveKey(_config.EncryptionKey);
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        using var msEncrypt = new MemoryStream();
        
        // 首先写入IV / Write IV first
        msEncrypt.Write(aes.IV, 0, aes.IV.Length);
        
        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        {
            csEncrypt.Write(data, 0, data.Length);
        }

        var encryptedData = msEncrypt.ToArray();
        
        // 注意：为了跨平台兼容性，禁用了Windows DPAPI / Note: Windows DPAPI is disabled for cross-platform compatibility
        return encryptedData;
    }

    /// <summary>
    /// 使用AES解密数据 / Decrypts data using AES decryption
    /// 从数据开头提取IV并用于解密 / Extracts IV from beginning of data and uses it for decryption
    /// </summary>
    /// <param name="encryptedData">要解密的加密数据 / Encrypted data to decrypt</param>
    /// <returns>解密后的数据 / Decrypted data</returns>
    private byte[] DecryptData(byte[] encryptedData)
    {
        // 注意：为了跨平台兼容性，禁用了Windows DPAPI / Note: Windows DPAPI is disabled for cross-platform compatibility

        using var aes = Aes.Create();
        aes.Key = DeriveKey(_config.EncryptionKey);

        // 从数据开头提取IV / Extract IV from the beginning of the data
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
    /// 从加密密钥字符串派生256位密钥 / Derives a 256-bit key from the encryption key string
    /// 使用SHA256哈希算法生成固定长度的密钥 / Uses SHA256 hash algorithm to generate fixed-length key
    /// </summary>
    /// <param name="key">加密密钥字符串 / Encryption key string</param>
    /// <returns>派生的256位密钥 / Derived 256-bit key</returns>
    private byte[] DeriveKey(string key)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
    }
}