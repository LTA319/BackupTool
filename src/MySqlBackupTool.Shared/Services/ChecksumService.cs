using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Service for calculating and validating file checksums
/// </summary>
public class ChecksumService : IChecksumService
{
    private readonly ILogger<ChecksumService> _logger;

    public ChecksumService(ILogger<ChecksumService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Calculates MD5 checksum for a file
    /// </summary>
    public async Task<string> CalculateFileMD5Async(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            using var md5 = MD5.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            
            var hash = await md5.ComputeHashAsync(stream, cancellationToken);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating MD5 checksum for file {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Calculates SHA256 checksum for a file
    /// </summary>
    public async Task<string> CalculateFileSHA256Async(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            using var sha256 = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            
            var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating SHA256 checksum for file {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Calculates both MD5 and SHA256 checksums for a file
    /// </summary>
    public async Task<(string md5, string sha256)> CalculateFileChecksumsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            using var md5 = MD5.Create();
            using var sha256 = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            
            // Read file once and calculate both hashes
            var buffer = new byte[64 * 1024]; // 64KB buffer
            int bytesRead;
            
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                md5.TransformBlock(buffer, 0, bytesRead, null, 0);
                sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
            }
            
            md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            
            var md5Hash = Convert.ToHexString(md5.Hash!).ToLowerInvariant();
            var sha256Hash = Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
            
            return (md5Hash, sha256Hash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating checksums for file {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Calculates MD5 checksum for byte array
    /// </summary>
    public string CalculateMD5(byte[] data)
    {
        try
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(data);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating MD5 checksum for byte array of length {Length}", data.Length);
            throw;
        }
    }

    /// <summary>
    /// Calculates SHA256 checksum for byte array
    /// </summary>
    public string CalculateSHA256(byte[] data)
    {
        try
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(data);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating SHA256 checksum for byte array of length {Length}", data.Length);
            throw;
        }
    }

    /// <summary>
    /// Validates file integrity by comparing checksums
    /// </summary>
    public async Task<bool> ValidateFileIntegrityAsync(string filePath, string? expectedMD5 = null, string? expectedSHA256 = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(expectedMD5) && string.IsNullOrEmpty(expectedSHA256))
            {
                _logger.LogWarning("No expected checksums provided for validation of file {FilePath}", filePath);
                return true; // No validation requested
            }

            if (!File.Exists(filePath))
            {
                _logger.LogError("File not found for integrity validation: {FilePath}", filePath);
                return false;
            }

            var (actualMD5, actualSHA256) = await CalculateFileChecksumsAsync(filePath, cancellationToken);

            bool isValid = true;

            if (!string.IsNullOrEmpty(expectedMD5))
            {
                if (!string.Equals(expectedMD5, actualMD5, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError("MD5 checksum mismatch for file {FilePath}. Expected: {Expected}, Actual: {Actual}", 
                        filePath, expectedMD5, actualMD5);
                    isValid = false;
                }
                else
                {
                    _logger.LogDebug("MD5 checksum validation passed for file {FilePath}", filePath);
                }
            }

            if (!string.IsNullOrEmpty(expectedSHA256))
            {
                if (!string.Equals(expectedSHA256, actualSHA256, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError("SHA256 checksum mismatch for file {FilePath}. Expected: {Expected}, Actual: {Actual}", 
                        filePath, expectedSHA256, actualSHA256);
                    isValid = false;
                }
                else
                {
                    _logger.LogDebug("SHA256 checksum validation passed for file {FilePath}", filePath);
                }
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating file integrity for {FilePath}", filePath);
            return false;
        }
    }

    /// <summary>
    /// Validates chunk integrity by comparing MD5 checksum
    /// </summary>
    public bool ValidateChunkIntegrity(byte[] chunkData, string expectedChecksum)
    {
        try
        {
            if (string.IsNullOrEmpty(expectedChecksum))
            {
                _logger.LogWarning("No expected checksum provided for chunk validation");
                return true; // No validation requested
            }

            var actualChecksum = CalculateMD5(chunkData);
            
            if (!string.Equals(expectedChecksum, actualChecksum, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Chunk checksum mismatch. Expected: {Expected}, Actual: {Actual}", 
                    expectedChecksum, actualChecksum);
                return false;
            }

            _logger.LogDebug("Chunk checksum validation passed");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating chunk integrity");
            return false;
        }
    }

    /// <summary>
    /// Creates file metadata with calculated checksums
    /// </summary>
    public async Task<(string md5, string sha256, long fileSize)> CreateFileMetadataAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            var fileInfo = new FileInfo(filePath);
            var (md5, sha256) = await CalculateFileChecksumsAsync(filePath, cancellationToken);

            _logger.LogDebug("Created file metadata for {FilePath}: Size={Size}, MD5={MD5}, SHA256={SHA256}", 
                filePath, fileInfo.Length, md5, sha256);

            return (md5, sha256, fileInfo.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating file metadata for {FilePath}", filePath);
            throw;
        }
    }
}