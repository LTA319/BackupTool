using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 计算和验证文件校验和的服务 / Service for calculating and validating file checksums
/// </summary>
public class ChecksumService : IChecksumService
{
    /// <summary>
    /// 日志记录器 / Logger
    /// </summary>
    private readonly ILogger<ChecksumService> _logger;

    /// <summary>
    /// 初始化校验和服务 / Initializes the checksum service
    /// </summary>
    /// <param name="logger">日志记录器 / Logger</param>
    /// <exception cref="ArgumentNullException">当日志记录器为null时抛出 / Thrown when logger is null</exception>
    public ChecksumService(ILogger<ChecksumService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    

    /// <summary>
    /// 计算文件的MD5校验和 / Calculates MD5 checksum for a file
    /// </summary>
    /// <param name="filePath">文件路径 / File path</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>MD5校验和字符串 / MD5 checksum string</returns>
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
    /// 计算文件的SHA256校验和 / Calculates SHA256 checksum for a file
    /// </summary>
    /// <param name="filePath">文件路径 / File path</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>SHA256校验和字符串 / SHA256 checksum string</returns>
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
    /// 计算文件的MD5和SHA256校验和 / Calculates both MD5 and SHA256 checksums for a file
    /// </summary>
    /// <param name="filePath">文件路径 / File path</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>MD5和SHA256校验和元组 / Tuple of MD5 and SHA256 checksums</returns>
    public async Task<(string md5, string sha256)> CalculateFileChecksumsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            using var md5 = MD5.Create();
            using var sha256 = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            
            // 读取文件一次并计算两个哈希值 / Read file once and calculate both hashes
            var buffer = new byte[64 * 1024]; // 64KB缓冲区 / 64KB buffer
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
    /// 计算字节数组的MD5校验和 / Calculates MD5 checksum for byte array
    /// </summary>
    /// <param name="data">字节数组数据 / Byte array data</param>
    /// <returns>MD5校验和字符串 / MD5 checksum string</returns>
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
    /// 计算字节数组的SHA256校验和 / Calculates SHA256 checksum for byte array
    /// </summary>
    /// <param name="data">字节数组数据 / Byte array data</param>
    /// <returns>SHA256校验和字符串 / SHA256 checksum string</returns>
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
    /// 通过比较校验和验证文件完整性 / Validates file integrity by comparing checksums
    /// </summary>
    /// <param name="filePath">文件路径 / File path</param>
    /// <param name="expectedMD5">期望的MD5校验和 / Expected MD5 checksum</param>
    /// <param name="expectedSHA256">期望的SHA256校验和 / Expected SHA256 checksum</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>是否验证通过 / Whether validation passed</returns>
    public async Task<bool> ValidateFileIntegrityAsync(string filePath, string? expectedMD5 = null, string? expectedSHA256 = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(expectedMD5) && string.IsNullOrEmpty(expectedSHA256))
            {
                _logger.LogWarning("No expected checksums provided for validation of file {FilePath}", filePath);
                return true; // 未请求验证 / No validation requested
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
    /// 通过比较MD5校验和验证块完整性 / Validates chunk integrity by comparing MD5 checksum
    /// </summary>
    /// <param name="chunkData">块数据 / Chunk data</param>
    /// <param name="expectedChecksum">期望的校验和 / Expected checksum</param>
    /// <returns>是否验证通过 / Whether validation passed</returns>
    public bool ValidateChunkIntegrity(byte[] chunkData, string expectedChecksum)
    {
        try
        {
            if (string.IsNullOrEmpty(expectedChecksum))
            {
                _logger.LogWarning("No expected checksum provided for chunk validation");
                return true; // 未请求验证 / No validation requested
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
    /// 创建包含计算校验和的文件元数据 / Creates file metadata with calculated checksums
    /// </summary>
    /// <param name="filePath">文件路径 / File path</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>包含MD5、SHA256和文件大小的元组 / Tuple containing MD5, SHA256 and file size</returns>
    /// <exception cref="FileNotFoundException">当文件不存在时抛出 / Thrown when file is not found</exception>
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