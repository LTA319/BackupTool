using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces
{
    /// <summary>
    /// Interface for file encryption and decryption services
    /// </summary>
    public interface IEncryptionService
    {
        /// <summary>
        /// Encrypts a file using AES-256 encryption
        /// </summary>
        /// <param name="inputPath">Path to the file to encrypt</param>
        /// <param name="outputPath">Path where the encrypted file will be saved</param>
        /// <param name="password">Password for encryption</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Encryption metadata</returns>
        Task<EncryptionMetadata> EncryptAsync(string inputPath, string outputPath, string password, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Decrypts a file that was encrypted with AES-256
        /// </summary>
        /// <param name="inputPath">Path to the encrypted file</param>
        /// <param name="outputPath">Path where the decrypted file will be saved</param>
        /// <param name="password">Password for decryption</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the async operation</returns>
        Task DecryptAsync(string inputPath, string outputPath, string password, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Validates if the provided password can decrypt the encrypted file
        /// </summary>
        /// <param name="encryptedFilePath">Path to the encrypted file</param>
        /// <param name="password">Password to validate</param>
        /// <returns>True if password is correct, false otherwise</returns>
        Task<bool> ValidatePasswordAsync(string encryptedFilePath, string password);
        
        /// <summary>
        /// Gets metadata from an encrypted file
        /// </summary>
        /// <param name="encryptedFilePath">Path to the encrypted file</param>
        /// <returns>Encryption metadata</returns>
        Task<EncryptionMetadata> GetMetadataAsync(string encryptedFilePath);
        
        /// <summary>
        /// Generates a secure random password
        /// </summary>
        /// <param name="length">Length of the password (default: 32)</param>
        /// <returns>Secure random password</returns>
        string GenerateSecurePassword(int length = 32);
    }
}