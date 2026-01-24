using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces
{
    /// <summary>
    /// Interface for backup file validation and integrity checking services
    /// </summary>
    public interface IValidationService
    {
        /// <summary>
        /// Validates a backup file for integrity and completeness
        /// </summary>
        /// <param name="filePath">Path to the backup file to validate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Validation result with details</returns>
        Task<FileValidationResult> ValidateBackupAsync(string filePath, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Validates file integrity using checksum comparison
        /// </summary>
        /// <param name="filePath">Path to the file to validate</param>
        /// <param name="expectedChecksum">Expected checksum value</param>
        /// <param name="algorithm">Checksum algorithm to use</param>
        /// <returns>True if checksums match, false otherwise</returns>
        Task<bool> ValidateIntegrityAsync(string filePath, string expectedChecksum, ChecksumAlgorithm algorithm = ChecksumAlgorithm.SHA256);
        
        /// <summary>
        /// Calculates checksum for a file
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="algorithm">Checksum algorithm to use</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Calculated checksum as hex string</returns>
        Task<string> CalculateChecksumAsync(string filePath, ChecksumAlgorithm algorithm = ChecksumAlgorithm.SHA256, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Generates a comprehensive validation report for a backup file
        /// </summary>
        /// <param name="filePath">Path to the backup file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Detailed validation report</returns>
        Task<ValidationReport> GenerateReportAsync(string filePath, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Validates that a backup file can be successfully decompressed
        /// </summary>
        /// <param name="filePath">Path to the compressed backup file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if file can be decompressed, false otherwise</returns>
        Task<bool> ValidateCompressionAsync(string filePath, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Validates that an encrypted backup file can be decrypted with the provided password
        /// </summary>
        /// <param name="filePath">Path to the encrypted backup file</param>
        /// <param name="password">Password for decryption</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if file can be decrypted, false otherwise</returns>
        Task<bool> ValidateEncryptionAsync(string filePath, string password, CancellationToken cancellationToken = default);
    }
}