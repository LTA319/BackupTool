using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces
{
    /// <summary>
    /// 文件加密和解密服务的接口 / Interface for file encryption and decryption services
    /// 提供AES-256加密算法的文件加密、解密、密码验证和元数据管理功能
    /// Provides file encryption, decryption, password validation and metadata management using AES-256 encryption
    /// </summary>
    public interface IEncryptionService
    {
        /// <summary>
        /// 使用AES-256加密算法加密文件 / Encrypts a file using AES-256 encryption
        /// 将指定文件加密并保存到目标路径，返回加密元数据信息
        /// Encrypts the specified file and saves to target path, returns encryption metadata
        /// </summary>
        /// <param name="inputPath">要加密的文件路径 / Path to the file to encrypt</param>
        /// <param name="outputPath">加密文件的保存路径 / Path where the encrypted file will be saved</param>
        /// <param name="password">加密密码 / Password for encryption</param>
        /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
        /// <returns>加密元数据信息 / Encryption metadata</returns>
        /// <exception cref="FileNotFoundException">当输入文件不存在时抛出 / Thrown when input file does not exist</exception>
        /// <exception cref="UnauthorizedAccessException">当没有文件访问权限时抛出 / Thrown when file access is denied</exception>
        /// <exception cref="EncryptionException">当加密操作失败时抛出 / Thrown when encryption operation fails</exception>
        Task<EncryptionMetadata> EncryptAsync(string inputPath, string outputPath, string password, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 解密使用AES-256加密的文件 / Decrypts a file that was encrypted with AES-256
        /// 将加密文件解密并保存到目标路径，需要提供正确的解密密码
        /// Decrypts the encrypted file and saves to target path, requires correct decryption password
        /// </summary>
        /// <param name="inputPath">加密文件的路径 / Path to the encrypted file</param>
        /// <param name="outputPath">解密文件的保存路径 / Path where the decrypted file will be saved</param>
        /// <param name="password">解密密码 / Password for decryption</param>
        /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
        /// <returns>表示异步操作的任务 / Task representing the async operation</returns>
        /// <exception cref="FileNotFoundException">当加密文件不存在时抛出 / Thrown when encrypted file does not exist</exception>
        /// <exception cref="UnauthorizedAccessException">当密码错误时抛出 / Thrown when password is incorrect</exception>
        /// <exception cref="EncryptionException">当解密操作失败时抛出 / Thrown when decryption operation fails</exception>
        Task DecryptAsync(string inputPath, string outputPath, string password, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 验证提供的密码是否能够解密加密文件 / Validates if the provided password can decrypt the encrypted file
        /// 通过尝试读取文件头部信息来验证密码的正确性，不会完全解密文件
        /// Validates password correctness by attempting to read file header information without full decryption
        /// </summary>
        /// <param name="encryptedFilePath">加密文件的路径 / Path to the encrypted file</param>
        /// <param name="password">要验证的密码 / Password to validate</param>
        /// <returns>如果密码正确返回true，否则返回false / True if password is correct, false otherwise</returns>
        /// <exception cref="FileNotFoundException">当加密文件不存在时抛出 / Thrown when encrypted file does not exist</exception>
        Task<bool> ValidatePasswordAsync(string encryptedFilePath, string password);
        
        /// <summary>
        /// 从加密文件中获取元数据信息 / Gets metadata from an encrypted file
        /// 读取加密文件的头部信息，包括加密算法、创建时间、文件大小等元数据
        /// Reads header information from encrypted file including encryption algorithm, creation time, file size and other metadata
        /// </summary>
        /// <param name="encryptedFilePath">加密文件的路径 / Path to the encrypted file</param>
        /// <returns>加密元数据信息 / Encryption metadata</returns>
        /// <exception cref="FileNotFoundException">当加密文件不存在时抛出 / Thrown when encrypted file does not exist</exception>
        /// <exception cref="InvalidDataException">当文件格式无效时抛出 / Thrown when file format is invalid</exception>
        Task<EncryptionMetadata> GetMetadataAsync(string encryptedFilePath);
        
        /// <summary>
        /// 生成安全的随机密码 / Generates a secure random password
        /// 使用加密安全的随机数生成器创建指定长度的强密码，包含大小写字母、数字和特殊字符
        /// Uses cryptographically secure random number generator to create strong password of specified length with uppercase, lowercase, numbers and special characters
        /// </summary>
        /// <param name="length">密码长度，默认为32位 / Length of the password (default: 32)</param>
        /// <returns>安全的随机密码 / Secure random password</returns>
        /// <exception cref="ArgumentException">当密码长度小于8时抛出 / Thrown when password length is less than 8</exception>
        string GenerateSecurePassword(int length = 32);
    }
}