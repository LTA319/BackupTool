namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 校验和计算和验证服务的接口
/// Interface for checksum calculation and validation services
/// </summary>
public interface IChecksumService
{
    /// <summary>
    /// 计算文件的MD5校验和
    /// Calculates MD5 checksum for a file
    /// </summary>
    Task<string> CalculateFileMD5Async(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 计算文件的SHA256校验和
    /// Calculates SHA256 checksum for a file
    /// </summary>
    Task<string> CalculateFileSHA256Async(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 计算文件的MD5和SHA256校验和
    /// Calculates both MD5 and SHA256 checksums for a file
    /// </summary>
    Task<(string md5, string sha256)> CalculateFileChecksumsAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 计算字节数组的MD5校验和
    /// Calculates MD5 checksum for byte array
    /// </summary>
    string CalculateMD5(byte[] data);

    /// <summary>
    /// 计算字节数组的SHA256校验和
    /// Calculates SHA256 checksum for byte array
    /// </summary>
    string CalculateSHA256(byte[] data);

    /// <summary>
    /// 通过比较校验和验证文件完整性
    /// Validates file integrity by comparing checksums
    /// </summary>
    Task<bool> ValidateFileIntegrityAsync(string filePath, string? expectedMD5 = null, string? expectedSHA256 = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 通过比较MD5校验和验证分块完整性
    /// Validates chunk integrity by comparing MD5 checksum
    /// </summary>
    bool ValidateChunkIntegrity(byte[] chunkData, string expectedChecksum);

    /// <summary>
    /// 创建包含计算校验和的文件元数据
    /// Creates file metadata with calculated checksums
    /// </summary>
    Task<(string md5, string sha256, long fileSize)> CreateFileMetadataAsync(string filePath, CancellationToken cancellationToken = default);
}