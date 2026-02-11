using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces
{
    /// <summary>
    /// 备份文件验证和完整性检查服务的接口 / Interface for backup file validation and integrity checking services
    /// 提供备份文件的完整性验证、校验和计算、压缩和加密验证等功能
    /// Provides backup file integrity validation, checksum calculation, compression and encryption validation functionality
    /// </summary>
    public interface IValidationService
    {
        /// <summary>
        /// 验证备份文件的完整性和完整性 / Validates a backup file for integrity and completeness
        /// 对备份文件进行全面的完整性检查，包括文件结构、数据完整性等
        /// Performs comprehensive integrity check on backup file including file structure, data integrity, etc.
        /// </summary>
        /// <param name="filePath">要验证的备份文件路径 / Path to the backup file to validate</param>
        /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
        /// <returns>包含详细信息的验证结果 / Validation result with details</returns>
        /// <exception cref="FileNotFoundException">当文件不存在时抛出 / Thrown when file does not exist</exception>
        /// <exception cref="UnauthorizedAccessException">当没有文件访问权限时抛出 / Thrown when file access is denied</exception>
        Task<FileValidationResult> ValidateBackupAsync(string filePath, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 使用校验和比较验证文件完整性 / Validates file integrity using checksum comparison
        /// 计算文件的校验和并与期望值进行比较，确保文件未被损坏或篡改
        /// Calculates file checksum and compares with expected value to ensure file is not corrupted or tampered
        /// </summary>
        /// <param name="filePath">要验证的文件路径 / Path to the file to validate</param>
        /// <param name="expectedChecksum">期望的校验和值 / Expected checksum value</param>
        /// <param name="algorithm">要使用的校验和算法 / Checksum algorithm to use</param>
        /// <returns>如果校验和匹配返回true，否则返回false / True if checksums match, false otherwise</returns>
        /// <exception cref="FileNotFoundException">当文件不存在时抛出 / Thrown when file does not exist</exception>
        /// <exception cref="ArgumentException">当校验和格式无效时抛出 / Thrown when checksum format is invalid</exception>
        Task<bool> ValidateIntegrityAsync(string filePath, string expectedChecksum, ChecksumAlgorithm algorithm = ChecksumAlgorithm.SHA256);
        
        /// <summary>
        /// 计算文件的校验和 / Calculates checksum for a file
        /// 使用指定的算法计算文件的校验和值，用于完整性验证
        /// Calculates file checksum value using specified algorithm for integrity verification
        /// </summary>
        /// <param name="filePath">文件路径 / Path to the file</param>
        /// <param name="algorithm">要使用的校验和算法 / Checksum algorithm to use</param>
        /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
        /// <returns>计算得出的十六进制校验和字符串 / Calculated checksum as hex string</returns>
        /// <exception cref="FileNotFoundException">当文件不存在时抛出 / Thrown when file does not exist</exception>
        /// <exception cref="UnauthorizedAccessException">当没有文件读取权限时抛出 / Thrown when file read access is denied</exception>
        Task<string> CalculateChecksumAsync(string filePath, ChecksumAlgorithm algorithm = ChecksumAlgorithm.SHA256, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 为备份文件生成综合验证报告 / Generates a comprehensive validation report for a backup file
        /// 创建包含文件完整性、结构分析、元数据验证等信息的详细报告
        /// Creates detailed report including file integrity, structure analysis, metadata validation and other information
        /// </summary>
        /// <param name="filePath">备份文件路径 / Path to the backup file</param>
        /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
        /// <returns>详细的验证报告 / Detailed validation report</returns>
        /// <exception cref="FileNotFoundException">当文件不存在时抛出 / Thrown when file does not exist</exception>
        /// <exception cref="UnauthorizedAccessException">当没有文件访问权限时抛出 / Thrown when file access is denied</exception>
        Task<ValidationReport> GenerateReportAsync(string filePath, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 验证备份文件是否可以成功解压缩 / Validates that a backup file can be successfully decompressed
        /// 测试压缩文件的完整性，确保可以正确解压缩而不会出现错误
        /// Tests integrity of compressed file to ensure it can be correctly decompressed without errors
        /// </summary>
        /// <param name="filePath">压缩备份文件的路径 / Path to the compressed backup file</param>
        /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
        /// <returns>如果文件可以解压缩返回true，否则返回false / True if file can be decompressed, false otherwise</returns>
        /// <exception cref="FileNotFoundException">当文件不存在时抛出 / Thrown when file does not exist</exception>
        /// <exception cref="InvalidDataException">当文件格式无效时抛出 / Thrown when file format is invalid</exception>
        Task<bool> ValidateCompressionAsync(string filePath, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 验证加密备份文件是否可以使用提供的密码解密 / Validates that an encrypted backup file can be decrypted with the provided password
        /// 测试加密文件是否可以使用指定密码正确解密，验证密码的正确性
        /// Tests if encrypted file can be correctly decrypted with specified password, validates password correctness
        /// </summary>
        /// <param name="filePath">加密备份文件的路径 / Path to the encrypted backup file</param>
        /// <param name="password">解密密码 / Password for decryption</param>
        /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
        /// <returns>如果文件可以解密返回true，否则返回false / True if file can be decrypted, false otherwise</returns>
        /// <exception cref="FileNotFoundException">当文件不存在时抛出 / Thrown when file does not exist</exception>
        /// <exception cref="ArgumentException">当密码为空或无效时抛出 / Thrown when password is empty or invalid</exception>
        Task<bool> ValidateEncryptionAsync(string filePath, string password, CancellationToken cancellationToken = default);
    }
}