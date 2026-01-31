using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services
{
    /// <summary>
    /// 使用AES-256加密和解密文件的服务 / Service for encrypting and decrypting files using AES-256 encryption
    /// </summary>
    public class EncryptionService : IEncryptionService
    {
        private readonly ILoggingService _loggingService;
        private const int DefaultBufferSize = 65536; // 64KB 缓冲区大小 / 64KB buffer size
        private const int SaltSize = 32; // 256位盐值 / 256 bits salt
        private const int IVSize = 16; // AES的128位初始化向量 / 128 bits IV for AES
        private const int DefaultIterations = 100000; // 默认PBKDF2迭代次数 / Default PBKDF2 iterations
        
        /// <summary>
        /// 初始化加密服务 / Initialize encryption service
        /// </summary>
        /// <param name="loggingService">日志服务 / Logging service</param>
        public EncryptionService(ILoggingService loggingService)
        {
            _loggingService = loggingService;
        }
        
        /// <summary>
        /// 使用AES-256加密文件 / Encrypts a file using AES-256 encryption
        /// </summary>
        /// <param name="inputPath">输入文件路径 / Input file path</param>
        /// <param name="outputPath">输出文件路径 / Output file path</param>
        /// <param name="password">加密密码 / Encryption password</param>
        /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
        /// <returns>加密元数据 / Encryption metadata</returns>
        /// <exception cref="ArgumentException">参数无效时抛出 / Thrown when arguments are invalid</exception>
        /// <exception cref="FileNotFoundException">输入文件不存在时抛出 / Thrown when input file not found</exception>
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
            
            // 生成盐值和初始化向量 / Generate salt and IV
            var salt = GenerateRandomBytes(SaltSize);
            var iv = GenerateRandomBytes(IVSize);
            
            // 计算原始文件校验和 / Calculate original file checksum
            var originalChecksum = await CalculateFileChecksumAsync(inputPath, cancellationToken);
            
            // 创建元数据 / Create metadata
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
                // 从密码派生密钥 / Derive key from password
                var key = DeriveKey(password, salt, DefaultIterations);
                
                try
                {
                    // 如果输出目录不存在则创建 / Create output directory if it doesn't exist
                    var outputDir = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                    {
                        Directory.CreateDirectory(outputDir);
                    }
                    
                    // 加密文件 / Encrypt the file
                    using var inputStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                    
                    // 写入元数据头 / Write metadata header
                    await WriteMetadataHeaderAsync(outputStream, metadata, cancellationToken);
                    
                    // 加密文件内容 / Encrypt file content
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
                        
                        // 定期报告进度 / Report progress periodically
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
                    // 为了安全清除内存中的密钥 / Clear the key from memory for security
                    Array.Clear(key, 0, key.Length);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Encryption failed: {ex.Message}");
                
                // 清理部分输出文件 / Clean up partial output file
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
        /// 解密使用AES-256加密的文件 / Decrypts a file that was encrypted with AES-256
        /// </summary>
        /// <param name="inputPath">加密文件路径 / Encrypted file path</param>
        /// <param name="outputPath">解密输出路径 / Decrypted output path</param>
        /// <param name="password">解密密码 / Decryption password</param>
        /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
        /// <exception cref="ArgumentException">参数无效时抛出 / Thrown when arguments are invalid</exception>
        /// <exception cref="FileNotFoundException">输入文件不存在时抛出 / Thrown when input file not found</exception>
        /// <exception cref="UnauthorizedAccessException">密码错误时抛出 / Thrown when password is incorrect</exception>
        /// <exception cref="InvalidDataException">文件损坏时抛出 / Thrown when file is corrupted</exception>
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
                
                // 读取元数据头 / Read metadata header
                var metadata = await ReadMetadataHeaderAsync(inputStream, cancellationToken);
                
                // 从密码派生密钥 / Derive key from password
                var salt = Convert.FromBase64String(metadata.Salt);
                var iv = Convert.FromBase64String(metadata.IV);
                var key = DeriveKey(password, salt, metadata.Iterations);
                
                try
                {
                    // 如果输出目录不存在则创建 / Create output directory if it doesn't exist
                    var outputDir = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                    {
                        Directory.CreateDirectory(outputDir);
                    }
                    
                    // 解密文件 / Decrypt the file
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
                            
                            // 定期报告进度 / Report progress periodically
                            if (totalBytesWritten % (DefaultBufferSize * 10) == 0)
                            {
                                var progress = metadata.OriginalSize > 0 ? (double)totalBytesWritten / metadata.OriginalSize * 100 : 0;
                                _loggingService.LogDebug($"Decryption progress: {progress:F1}%");
                            }
                        }
                    } // 确保输出流在校验和计算前被释放 / Ensure output stream is disposed before checksum calculation
                    
                    // 如果可用则验证解密文件校验和 / Verify decrypted file checksum if available
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
                    // 为了安全清除内存中的密钥 / Clear the key from memory for security
                    Array.Clear(key, 0, key.Length);
                }
            }
            catch (CryptographicException ex)
            {
                _loggingService.LogError($"Decryption failed - likely incorrect password: {ex.Message}");
                
                // 清理部分输出文件 / Clean up partial output file
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
                
                // 清理部分输出文件 / Clean up partial output file
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
        /// 验证提供的密码是否可以解密加密文件 / Validates if the provided password can decrypt the encrypted file
        /// </summary>
        /// <param name="encryptedFilePath">加密文件路径 / Encrypted file path</param>
        /// <param name="password">要验证的密码 / Password to validate</param>
        /// <returns>密码是否正确 / Whether password is correct</returns>
        /// <exception cref="ArgumentException">参数无效时抛出 / Thrown when arguments are invalid</exception>
        /// <exception cref="FileNotFoundException">文件不存在时抛出 / Thrown when file not found</exception>
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
                
                // 读取元数据头 / Read metadata header
                var metadata = await ReadMetadataHeaderAsync(inputStream, CancellationToken.None);
                
                // 从密码派生密钥 / Derive key from password
                var salt = Convert.FromBase64String(metadata.Salt);
                var iv = Convert.FromBase64String(metadata.IV);
                var key = DeriveKey(password, salt, metadata.Iterations);
                
                try
                {
                    // 尝试解密一小部分来验证密码 / Try to decrypt a small portion to validate password
                    using var aes = Aes.Create();
                    aes.Key = key;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    
                    using var decryptor = aes.CreateDecryptor();
                    using var cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read);
                    
                    // 尝试读取小缓冲区 - 如果密码错误，这将抛出异常 / Try to read a small buffer - if password is wrong, this will throw
                    var buffer = new byte[1024];
                    var bytesRead = await cryptoStream.ReadAsync(buffer, 0, buffer.Length);
                    
                    // 如果我们可以读取一些数据而不出现异常，密码可能是正确的 / If we can read some data without exception, password is likely correct
                    // 但让我们也尝试读取更多一点来确保 / But let's also try to read a bit more to be sure
                    if (bytesRead > 0)
                    {
                        var secondBuffer = new byte[1024];
                        await cryptoStream.ReadAsync(secondBuffer, 0, secondBuffer.Length);
                    }
                    
                    return true;
                }
                finally
                {
                    // 为了安全清除内存中的密钥 / Clear the key from memory for security
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
        /// 从加密文件获取元数据 / Gets metadata from an encrypted file
        /// </summary>
        /// <param name="encryptedFilePath">加密文件路径 / Encrypted file path</param>
        /// <returns>加密元数据 / Encryption metadata</returns>
        /// <exception cref="ArgumentException">参数无效时抛出 / Thrown when arguments are invalid</exception>
        /// <exception cref="FileNotFoundException">文件不存在时抛出 / Thrown when file not found</exception>
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
        /// 生成安全的随机密码 / Generates a secure random password
        /// </summary>
        /// <param name="length">密码长度（最少8个字符） / Password length (minimum 8 characters)</param>
        /// <returns>生成的安全密码 / Generated secure password</returns>
        /// <exception cref="ArgumentException">长度小于8时抛出 / Thrown when length is less than 8</exception>
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
            
            // 确保每个必需类别至少有一个字符 / Ensure at least one character from each required category
            result.Append(upperChars[random[0] % upperChars.Length]);
            result.Append(lowerChars[random[1] % lowerChars.Length]);
            result.Append(digitChars[random[2] % digitChars.Length]);
            result.Append(specialChars[random[3] % specialChars.Length]);
            
            // 用所有类别的随机字符填充其余部分 / Fill the rest with random characters from all categories
            for (int i = 4; i < length; i++)
            {
                result.Append(allChars[random[i] % allChars.Length]);
            }
            
            // 打乱结果以避免可预测的模式 / Shuffle the result to avoid predictable patterns
            var chars = result.ToString().ToCharArray();
            for (int i = chars.Length - 1; i > 0; i--)
            {
                int j = random[i] % (i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }
            
            return new string(chars);
        }
        
        #region 私有辅助方法 / Private Helper Methods
        
        /// <summary>
        /// 生成指定大小的随机字节 / Generate random bytes of specified size
        /// </summary>
        /// <param name="size">字节数 / Number of bytes</param>
        /// <returns>随机字节数组 / Random byte array</returns>
        private static byte[] GenerateRandomBytes(int size)
        {
            var bytes = new byte[size];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return bytes;
        }
        
        /// <summary>
        /// 从密码和盐值派生加密密钥 / Derive encryption key from password and salt
        /// </summary>
        /// <param name="password">密码 / Password</param>
        /// <param name="salt">盐值 / Salt</param>
        /// <param name="iterations">迭代次数 / Iterations</param>
        /// <returns>派生的密钥 / Derived key</returns>
        private static byte[] DeriveKey(string password, byte[] salt, int iterations)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(32); // AES-256需要256位 / 256 bits for AES-256
        }
        
        /// <summary>
        /// 计算文件的SHA256校验和 / Calculate SHA256 checksum of file
        /// </summary>
        /// <param name="filePath">文件路径 / File path</param>
        /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
        /// <returns>十六进制校验和字符串 / Hexadecimal checksum string</returns>
        private async Task<string> CalculateFileChecksumAsync(string filePath, CancellationToken cancellationToken)
        {
            using var sha256 = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            
            var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
            return Convert.ToHexString(hash);
        }
        
        /// <summary>
        /// 将加密元数据头写入输出流 / Write encryption metadata header to output stream
        /// </summary>
        /// <param name="outputStream">输出流 / Output stream</param>
        /// <param name="metadata">加密元数据 / Encryption metadata</param>
        /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
        private async Task WriteMetadataHeaderAsync(Stream outputStream, EncryptionMetadata metadata, CancellationToken cancellationToken)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var metadataJson = JsonSerializer.Serialize(metadata, jsonOptions);
            var metadataBytes = Encoding.UTF8.GetBytes(metadataJson);
            
            // 写入魔术头以标识加密文件 / Write magic header to identify encrypted files
            var magicHeader = Encoding.ASCII.GetBytes("MYSQLBAK");
            await outputStream.WriteAsync(magicHeader, 0, magicHeader.Length, cancellationToken);
            
            // 写入元数据长度（4字节） / Write metadata length (4 bytes)
            var lengthBytes = BitConverter.GetBytes(metadataBytes.Length);
            await outputStream.WriteAsync(lengthBytes, 0, lengthBytes.Length, cancellationToken);
            
            // 写入元数据 / Write metadata
            await outputStream.WriteAsync(metadataBytes, 0, metadataBytes.Length, cancellationToken);
        }
        
        /// <summary>
        /// 从输入流读取加密元数据头 / Read encryption metadata header from input stream
        /// </summary>
        /// <param name="inputStream">输入流 / Input stream</param>
        /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
        /// <returns>加密元数据 / Encryption metadata</returns>
        /// <exception cref="InvalidDataException">文件格式无效时抛出 / Thrown when file format is invalid</exception>
        private async Task<EncryptionMetadata> ReadMetadataHeaderAsync(Stream inputStream, CancellationToken cancellationToken)
        {
            // 读取魔术头 / Read magic header
            var magicHeader = new byte[8];
            var bytesRead = await inputStream.ReadAsync(magicHeader, 0, magicHeader.Length, cancellationToken);
            if (bytesRead != 8 || Encoding.ASCII.GetString(magicHeader) != "MYSQLBAK")
            {
                throw new InvalidDataException("File is not a valid encrypted backup file");
            }
            
            // 读取元数据长度 / Read metadata length
            var lengthBytes = new byte[4];
            bytesRead = await inputStream.ReadAsync(lengthBytes, 0, lengthBytes.Length, cancellationToken);
            if (bytesRead != 4)
            {
                throw new InvalidDataException("Invalid encrypted file format - cannot read metadata length");
            }
            
            var metadataLength = BitConverter.ToInt32(lengthBytes, 0);
            if (metadataLength <= 0 || metadataLength > 1024 * 1024) // 元数据最大1MB / Max 1MB for metadata
            {
                throw new InvalidDataException("Invalid metadata length in encrypted file");
            }
            
            // 读取元数据 / Read metadata
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