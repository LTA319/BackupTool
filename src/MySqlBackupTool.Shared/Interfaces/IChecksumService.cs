namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Interface for checksum calculation and validation services
/// </summary>
public interface IChecksumService
{
    /// <summary>
    /// Calculates MD5 checksum for a file
    /// </summary>
    Task<string> CalculateFileMD5Async(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates SHA256 checksum for a file
    /// </summary>
    Task<string> CalculateFileSHA256Async(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates both MD5 and SHA256 checksums for a file
    /// </summary>
    Task<(string md5, string sha256)> CalculateFileChecksumsAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates MD5 checksum for byte array
    /// </summary>
    string CalculateMD5(byte[] data);

    /// <summary>
    /// Calculates SHA256 checksum for byte array
    /// </summary>
    string CalculateSHA256(byte[] data);

    /// <summary>
    /// Validates file integrity by comparing checksums
    /// </summary>
    Task<bool> ValidateFileIntegrityAsync(string filePath, string? expectedMD5 = null, string? expectedSHA256 = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates chunk integrity by comparing MD5 checksum
    /// </summary>
    bool ValidateChunkIntegrity(byte[] chunkData, string expectedChecksum);

    /// <summary>
    /// Creates file metadata with calculated checksums
    /// </summary>
    Task<(string md5, string sha256, long fileSize)> CreateFileMetadataAsync(string filePath, CancellationToken cancellationToken = default);
}