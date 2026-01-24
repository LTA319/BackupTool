using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services
{
    /// <summary>
    /// Service for encrypting and decrypting files using AES-256 encryption
    /// </summary>
    public class EncryptionService : IEncryptionService
    {
        private readonly ILoggingService _loggingService;
        private const int DefaultBufferSize = 65536; // 64KB
        private const int SaltSize = 32; // 256 bits
        private const int IVSize = 16; // 128 bits for AES
        private const int DefaultIterations = 100000;
        
        public EncryptionService(ILoggingService loggingService)
        {
            _loggingService = loggingService;
        }
        
        /// <summary>
        /// Encrypts a file using AES-256 encryption
        /// </summary>
        public async Task<EncryptionMetadata> EncryptAsync(string inputPath, string outputPath, string password, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(inputPath))
                throw new ArgumentException("Input path cannot be null or empty", nameof(inputPath));
            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));
            if (!File.Exists(inputPath))
                throw new FileNotFoundException($"Input file not found: {inputPath}");
            
            _loggingService.LogInformation($"Starting encryption of file: {inputPath}");
            
            var startTime = DateTime.UtcNow;
            var fileInfo = new FileInfo(inputPath);
            var originalSize = fileInfo.Length;
            
            // Generate salt and IV
            var salt = GenerateRandomBytes(SaltSize);
            var iv = GenerateRandomBytes(IVSize);
            
            // Calculate original file checksum
            var originalChecksum = await CalculateFileChecksumAsync(inputPath, cancellationToken);
            
            // Create metadata
            var metadata = new EncryptionMetadata
            {
                Algorithm = "AES-256-CBC",
                KeyDerivation = "PBKDF2",
                Iterations = DefaultIterations,
                Salt = Convert.ToBase64String(salt),
                IV = Convert.ToBase64String(iv),
                EncryptedAt = startTime,
                OriginalSize = originalSize,
                OriginalChecksum = originalChecksum,
                Version = 1
            };
            
            try
            {
                // Derive key from password
                var key = DeriveKey(password, salt, DefaultIterations);
                
                try
                {
                    // Create output directory if it doesn't exist
                    var outputDir = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                    {
                        Directory.CreateDirectory(outputDir);
                    }
                    
                    // Encrypt the file
                    using var inputStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                    
                    // Write metadata header
                    await WriteMetadataHeaderAsync(outputStream, metadata, cancellationToken);
                    
                    // Encrypt file content
                    using var aes = Aes.Create();
                    aes.Key = key;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    
                    using var encryptor = aes.CreateEncryptor();
                    using var cryptoStream = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write);
                    
                    var buffer = new byte[DefaultBufferSize];
                    long totalBytesRead = 0;
                    int bytesRead;
                    
                    while ((bytesRead = await inputStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await cryptoStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        totalBytesRead += bytesRead;
                        
                        // Report progress periodically
                        if (totalBytesRead % (DefaultBufferSize * 10) == 0)
                        {
                            var progress = (double)totalBytesRead / originalSize * 100;
                            _loggingService.LogDebug($"Encryption progress: {progress:F1}%");
                        }
                    }
                    
                    await cryptoStream.FlushFinalBlockAsync();
                    
                    var duration = DateTime.UtcNow - startTime;
                    _loggingService.LogInformation($"File encrypted successfully in {duration.TotalSeconds:F2} seconds. Output: {outputPath}");
                    
                    return metadata;
                }
                finally
                {
                    // Clear the key from memory for security
                    Array.Clear(key, 0, key.Length);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Encryption failed: {ex.Message}");
                
                // Clean up partial output file
                if (File.Exists(outputPath))
                {
                    try
                    {
                        File.Delete(outputPath);
                    }
                    catch (Exception deleteEx)
                    {
                        _loggingService.LogWarning($"Failed to delete partial output file: {deleteEx.Message}");
                    }
                }
                
                throw;
            }
        }
        
        /// <summary>
        /// Decrypts a file that was encrypted with AES-256
        /// </summary>
        public async Task DecryptAsync(string inputPath, string outputPath, string password, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(inputPath))
                throw new ArgumentException("Input path cannot be null or empty", nameof(inputPath));
            if (string.IsNullOrEmpty(outputPath))
                throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));
            if (!File.Exists(inputPath))
                throw new FileNotFoundException($"Input file not found: {inputPath}");
            
            _loggingService.LogInformation($"Starting decryption of file: {inputPath}");
            
            var startTime = DateTime.UtcNow;
            
            try
            {
                using var inputStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                
                // Read metadata header
                var metadata = await ReadMetadataHeaderAsync(inputStream, cancellationToken);
                
                // Derive key from password
                var salt = Convert.FromBase64String(metadata.Salt);
                var iv = Convert.FromBase64String(metadata.IV);
                var key = DeriveKey(password, salt, metadata.Iterations);
                
                try
                {
                    // Create output directory if it doesn't exist
                    var outputDir = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                    {
                        Directory.CreateDirectory(outputDir);
                    }
                    
                    // Decrypt the file
                    using (var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                    {
                        using var aes = Aes.Create();
                        aes.Key = key;
                        aes.IV = iv;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;
                        
                        using var decryptor = aes.CreateDecryptor();
                        using var cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read);
                        
                        var buffer = new byte[DefaultBufferSize];
                        long totalBytesWritten = 0;
                        int bytesRead;
                        
                        while ((bytesRead = await cryptoStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                        {
                            await outputStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                            totalBytesWritten += bytesRead;
                            
                            // Report progress periodically
                            if (totalBytesWritten % (DefaultBufferSize * 10) == 0)
                            {
                                var progress = metadata.OriginalSize > 0 ? (double)totalBytesWritten / metadata.OriginalSize * 100 : 0;
                                _loggingService.LogDebug($"Decryption progress: {progress:F1}%");
                            }
                        }
                    } // Ensure output stream is disposed before checksum calculation
                    
                    // Verify decrypted file checksum if available
                    if (!string.IsNullOrEmpty(metadata.OriginalChecksum))
                    {
                        _loggingService.LogDebug("Verifying decrypted file integrity...");
                        var decryptedChecksum = await CalculateFileChecksumAsync(outputPath, cancellationToken);
                        
                        if (!string.Equals(metadata.OriginalChecksum, decryptedChecksum, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException("Decrypted file checksum does not match original. File may be corrupted or password is incorrect.");
                        }
                        
                        _loggingService.LogDebug("File integrity verification passed");
                    }
                    
                    var duration = DateTime.UtcNow - startTime;
                    _loggingService.LogInformation($"File decrypted successfully in {duration.TotalSeconds:F2} seconds. Output: {outputPath}");
                }
                finally
                {
                    // Clear the key from memory for security
                    Array.Clear(key, 0, key.Length);
                }
            }
            catch (CryptographicException ex)
            {
                _loggingService.LogError($"Decryption failed - likely incorrect password: {ex.Message}");
                
                // Clean up partial output file
                if (File.Exists(outputPath))
                {
                    try
                    {
                        File.Delete(outputPath);
                    }
                    catch (Exception deleteEx)
                    {
                        _loggingService.LogWarning($"Failed to delete partial output file: {deleteEx.Message}");
                    }
                }
                
                throw new UnauthorizedAccessException("Decryption failed. Please check your password.", ex);
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Decryption failed: {ex.Message}");
                
                // Clean up partial output file
                if (File.Exists(outputPath))
                {
                    try
                    {
                        File.Delete(outputPath);
                    }
                    catch (Exception deleteEx)
                    {
                        _loggingService.LogWarning($"Failed to delete partial output file: {deleteEx.Message}");
                    }
                }
                
                throw;
            }
        }
        
        /// <summary>
        /// Validates if the provided password can decrypt the encrypted file
        /// </summary>
        public async Task<bool> ValidatePasswordAsync(string encryptedFilePath, string password)
        {
            if (string.IsNullOrEmpty(encryptedFilePath))
                throw new ArgumentException("Encrypted file path cannot be null or empty", nameof(encryptedFilePath));
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));
            if (!File.Exists(encryptedFilePath))
                throw new FileNotFoundException($"Encrypted file not found: {encryptedFilePath}");
            
            try
            {
                using var inputStream = new FileStream(encryptedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                
                // Read metadata header
                var metadata = await ReadMetadataHeaderAsync(inputStream, CancellationToken.None);
                
                // Derive key from password
                var salt = Convert.FromBase64String(metadata.Salt);
                var iv = Convert.FromBase64String(metadata.IV);
                var key = DeriveKey(password, salt, metadata.Iterations);
                
                try
                {
                    // Try to decrypt a small portion to validate password
                    using var aes = Aes.Create();
                    aes.Key = key;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    
                    using var decryptor = aes.CreateDecryptor();
                    using var cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read);
                    
                    // Try to read a small buffer - if password is wrong, this will throw
                    var buffer = new byte[1024];
                    var bytesRead = await cryptoStream.ReadAsync(buffer, 0, buffer.Length);
                    
                    // If we can read some data without exception, password is likely correct
                    // But let's also try to read a bit more to be sure
                    if (bytesRead > 0)
                    {
                        var secondBuffer = new byte[1024];
                        await cryptoStream.ReadAsync(secondBuffer, 0, secondBuffer.Length);
                    }
                    
                    return true;
                }
                finally
                {
                    // Clear the key from memory for security
                    Array.Clear(key, 0, key.Length);
                }
            }
            catch (CryptographicException)
            {
                return false;
            }
            catch (InvalidDataException)
            {
                return false;
            }
            catch (Exception ex)
            {
                _loggingService.LogWarning($"Password validation failed with unexpected error: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Gets metadata from an encrypted file
        /// </summary>
        public async Task<EncryptionMetadata> GetMetadataAsync(string encryptedFilePath)
        {
            if (string.IsNullOrEmpty(encryptedFilePath))
                throw new ArgumentException("Encrypted file path cannot be null or empty", nameof(encryptedFilePath));
            if (!File.Exists(encryptedFilePath))
                throw new FileNotFoundException($"Encrypted file not found: {encryptedFilePath}");
            
            using var inputStream = new FileStream(encryptedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await ReadMetadataHeaderAsync(inputStream, CancellationToken.None);
        }
        
        /// <summary>
        /// Generates a secure random password
        /// </summary>
        public string GenerateSecurePassword(int length = 32)
        {
            if (length < 8)
                throw new ArgumentException("Password length must be at least 8 characters", nameof(length));
            
            const string upperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lowerChars = "abcdefghijklmnopqrstuvwxyz";
            const string digitChars = "0123456789";
            const string specialChars = "!@#$%^&*";
            const string allChars = upperChars + lowerChars + digitChars + specialChars;
            
            var random = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(random);
            }
            
            var result = new StringBuilder(length);
            
            // Ensure at least one character from each required category
            result.Append(upperChars[random[0] % upperChars.Length]);
            result.Append(lowerChars[random[1] % lowerChars.Length]);
            result.Append(digitChars[random[2] % digitChars.Length]);
            result.Append(specialChars[random[3] % specialChars.Length]);
            
            // Fill the rest with random characters from all categories
            for (int i = 4; i < length; i++)
            {
                result.Append(allChars[random[i] % allChars.Length]);
            }
            
            // Shuffle the result to avoid predictable patterns
            var chars = result.ToString().ToCharArray();
            for (int i = chars.Length - 1; i > 0; i--)
            {
                int j = random[i] % (i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }
            
            return new string(chars);
        }
        
        #region Private Helper Methods
        
        private static byte[] GenerateRandomBytes(int size)
        {
            var bytes = new byte[size];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return bytes;
        }
        
        private static byte[] DeriveKey(string password, byte[] salt, int iterations)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(32); // 256 bits for AES-256
        }
        
        private async Task<string> CalculateFileChecksumAsync(string filePath, CancellationToken cancellationToken)
        {
            using var sha256 = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            
            var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
            return Convert.ToHexString(hash);
        }
        
        private async Task WriteMetadataHeaderAsync(Stream outputStream, EncryptionMetadata metadata, CancellationToken cancellationToken)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var metadataJson = JsonSerializer.Serialize(metadata, jsonOptions);
            var metadataBytes = Encoding.UTF8.GetBytes(metadataJson);
            
            // Write magic header to identify encrypted files
            var magicHeader = Encoding.ASCII.GetBytes("MYSQLBAK");
            await outputStream.WriteAsync(magicHeader, 0, magicHeader.Length, cancellationToken);
            
            // Write metadata length (4 bytes)
            var lengthBytes = BitConverter.GetBytes(metadataBytes.Length);
            await outputStream.WriteAsync(lengthBytes, 0, lengthBytes.Length, cancellationToken);
            
            // Write metadata
            await outputStream.WriteAsync(metadataBytes, 0, metadataBytes.Length, cancellationToken);
        }
        
        private async Task<EncryptionMetadata> ReadMetadataHeaderAsync(Stream inputStream, CancellationToken cancellationToken)
        {
            // Read magic header
            var magicHeader = new byte[8];
            var bytesRead = await inputStream.ReadAsync(magicHeader, 0, magicHeader.Length, cancellationToken);
            if (bytesRead != 8 || Encoding.ASCII.GetString(magicHeader) != "MYSQLBAK")
            {
                throw new InvalidDataException("File is not a valid encrypted backup file");
            }
            
            // Read metadata length
            var lengthBytes = new byte[4];
            bytesRead = await inputStream.ReadAsync(lengthBytes, 0, lengthBytes.Length, cancellationToken);
            if (bytesRead != 4)
            {
                throw new InvalidDataException("Invalid encrypted file format - cannot read metadata length");
            }
            
            var metadataLength = BitConverter.ToInt32(lengthBytes, 0);
            if (metadataLength <= 0 || metadataLength > 1024 * 1024) // Max 1MB for metadata
            {
                throw new InvalidDataException("Invalid metadata length in encrypted file");
            }
            
            // Read metadata
            var metadataBytes = new byte[metadataLength];
            bytesRead = await inputStream.ReadAsync(metadataBytes, 0, metadataBytes.Length, cancellationToken);
            if (bytesRead != metadataLength)
            {
                throw new InvalidDataException("Invalid encrypted file format - cannot read metadata");
            }
            
            var metadataJson = Encoding.UTF8.GetString(metadataBytes);
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var metadata = JsonSerializer.Deserialize<EncryptionMetadata>(metadataJson, jsonOptions);
            if (metadata == null)
            {
                throw new InvalidDataException("Failed to deserialize encryption metadata");
            }
            
            return metadata;
        }
        
        #endregion
    }
}