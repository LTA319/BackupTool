using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services
{
    /// <summary>
    /// 验证服务，用于验证备份文件并确保其完整性
    /// 提供文件校验、压缩完整性检查、加密完整性检查等功能
    /// </summary>
    public class ValidationService : IValidationService
    {
        private readonly ILoggingService _loggingService;
        private readonly IEncryptionService _encryptionService;
        private readonly ICompressionService _compressionService;
        private const int DefaultBufferSize = 65536; // 64KB
        
        /// <summary>
        /// 构造函数，初始化验证服务
        /// </summary>
        /// <param name="loggingService">日志服务</param>
        /// <param name="encryptionService">加密服务</param>
        /// <param name="compressionService">压缩服务</param>
        public ValidationService(
            ILoggingService loggingService,
            IEncryptionService encryptionService,
            ICompressionService compressionService)
        {
            _loggingService = loggingService;
            _encryptionService = encryptionService;
            _compressionService = compressionService;
        }
        
        /// <summary>
        /// 验证备份文件的完整性和完整性
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>文件验证结果</returns>
        public async Task<FileValidationResult> ValidateBackupAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");
            
            _loggingService.LogInformation($"Starting backup validation for file: {filePath}");
            
            var startTime = DateTime.UtcNow;
            var result = new FileValidationResult
            {
                FilePath = filePath,
                ValidatedAt = startTime,
                Algorithm = ChecksumAlgorithm.SHA256
            };
            
            try
            {
                var fileInfo = new System.IO.FileInfo(filePath);
                result.FileSize = fileInfo.Length;
                
                // 计算校验和
                result.Checksum = await CalculateChecksumAsync(filePath, ChecksumAlgorithm.SHA256, cancellationToken);
                
                // 执行基本文件验证
                await ValidateFileBasicsAsync(result, cancellationToken);
                
                // 检查文件是否已压缩
                if (await IsCompressedFileAsync(filePath))
                {
                    await ValidateCompressionIntegrityAsync(result, cancellationToken);
                }
                
                // 检查文件是否已加密
                if (await IsEncryptedFileAsync(filePath))
                {
                    await ValidateEncryptionIntegrityAsync(result, cancellationToken);
                }
                
                // 确定整体验证状态
                result.IsValid = result.Issues.All(i => i.Severity != ValidationSeverity.Critical && i.Severity != ValidationSeverity.Error);
                
                result.ValidationDuration = DateTime.UtcNow - startTime;
                
                var status = result.IsValid ? "passed" : "failed";
                _loggingService.LogInformation($"Backup validation {status} for file: {filePath} (Duration: {result.ValidationDuration.TotalSeconds:F2}s)");
                
                return result;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Backup validation failed: {ex.Message}");
                
                result.IsValid = false;
                result.Issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.FileSystem,
                    Severity = ValidationSeverity.Critical,
                    Description = $"Validation failed with exception: {ex.Message}",
                    SuggestedAction = "Check file accessibility and system resources"
                });
                
                result.ValidationDuration = DateTime.UtcNow - startTime;
                return result;
            }
        }
        
        /// <summary>
        /// 使用校验和比较验证文件完整性
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="expectedChecksum">期望的校验和</param>
        /// <param name="algorithm">校验和算法</param>
        /// <returns>如果文件完整性验证通过返回true，否则返回false</returns>
        public async Task<bool> ValidateIntegrityAsync(string filePath, string expectedChecksum, ChecksumAlgorithm algorithm = ChecksumAlgorithm.SHA256)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            if (string.IsNullOrEmpty(expectedChecksum))
                throw new ArgumentException("Expected checksum cannot be null or empty", nameof(expectedChecksum));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");
            
            try
            {
                var actualChecksum = await CalculateChecksumAsync(filePath, algorithm);
                var isValid = string.Equals(expectedChecksum, actualChecksum, StringComparison.OrdinalIgnoreCase);
                
                _loggingService.LogDebug($"Integrity validation for {filePath}: {(isValid ? "PASSED" : "FAILED")}");
                if (!isValid)
                {
                    _loggingService.LogWarning($"Checksum mismatch - Expected: {expectedChecksum}, Actual: {actualChecksum}");
                }
                
                return isValid;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Integrity validation failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 计算文件的校验和
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="algorithm">校验和算法</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>文件的校验和字符串</returns>
        public async Task<string> CalculateChecksumAsync(string filePath, ChecksumAlgorithm algorithm = ChecksumAlgorithm.SHA256, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");
            
            using var hashAlgorithm = CreateHashAlgorithm(algorithm);
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, DefaultBufferSize);
            
            var hash = await hashAlgorithm.ComputeHashAsync(stream, cancellationToken);
            return Convert.ToHexString(hash);
        }
        
        /// <summary>
        /// 为备份文件生成综合验证报告
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>详细的验证报告</returns>
        public async Task<ValidationReport> GenerateReportAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");
            
            _loggingService.LogInformation($"Generating validation report for file: {filePath}");
            
            var report = new ValidationReport
            {
                GeneratedAt = DateTime.UtcNow
            };
            
            try
            {
                // Basic validation
                report.ValidationResult = await ValidateBackupAsync(filePath, cancellationToken);
                
                // File information
                var fileInfo = new System.IO.FileInfo(filePath);
                report.FileInfo = new BackupFileInfo
                {
                    FullPath = fileInfo.FullName,
                    FileName = fileInfo.Name,
                    Extension = fileInfo.Extension,
                    SizeBytes = fileInfo.Length,
                    CreatedAt = fileInfo.CreationTime,
                    ModifiedAt = fileInfo.LastWriteTime,
                    IsReadOnly = fileInfo.IsReadOnly
                };
                
                // Compression validation
                if (await IsCompressedFileAsync(filePath))
                {
                    report.CompressionResult = await ValidateCompressionDetailsAsync(filePath, cancellationToken);
                }
                
                // Encryption validation
                if (await IsEncryptedFileAsync(filePath))
                {
                    report.EncryptionResult = await ValidateEncryptionDetailsAsync(filePath, cancellationToken);
                }
                
                // Database validation (basic heuristics)
                report.DatabaseResult = await ValidateDatabaseContentAsync(filePath, cancellationToken);
                
                // Generate summary
                report.Summary = GenerateValidationSummary(report);
                
                _loggingService.LogInformation($"Validation report generated successfully for file: {filePath}");
                
                return report;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Failed to generate validation report: {ex.Message}");
                
                report.ValidationResult.IsValid = false;
                report.ValidationResult.Issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.FileSystem,
                    Severity = ValidationSeverity.Critical,
                    Description = $"Report generation failed: {ex.Message}",
                    SuggestedAction = "Check file accessibility and system resources"
                });
                
                report.Summary = GenerateValidationSummary(report);
                return report;
            }
        }
        
        /// <summary>
        /// 验证备份文件是否可以成功解压缩
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>如果可以成功解压缩返回true，否则返回false</returns>
        public async Task<bool> ValidateCompressionAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");
            
            try
            {
                // Check if file is compressed
                if (!await IsCompressedFileAsync(filePath))
                {
                    _loggingService.LogDebug($"File is not compressed: {filePath}");
                    return true; // Not compressed is valid
                }
                
                // Try to read the entire compressed file to validate it
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                using var memoryStream = new MemoryStream();
                
                // Try to decompress the entire file
                await gzipStream.CopyToAsync(memoryStream, cancellationToken);
                
                _loggingService.LogDebug($"Compression validation passed for file: {filePath}");
                return true;
            }
            catch (InvalidDataException)
            {
                _loggingService.LogWarning($"Invalid compressed data in file: {filePath}");
                return false;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Compression validation failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 验证加密的备份文件是否可以使用提供的密码解密
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="password">解密密码</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>如果可以成功解密返回true，否则返回false</returns>
        public async Task<bool> ValidateEncryptionAsync(string filePath, string password, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");
            
            try
            {
                // Check if file is encrypted
                if (!await IsEncryptedFileAsync(filePath))
                {
                    _loggingService.LogDebug($"File is not encrypted: {filePath}");
                    return true; // Not encrypted is valid
                }
                
                // Use encryption service to validate password
                var isValid = await _encryptionService.ValidatePasswordAsync(filePath, password);
                
                _loggingService.LogDebug($"Encryption validation {(isValid ? "passed" : "failed")} for file: {filePath}");
                return isValid;
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Encryption validation failed: {ex.Message}");
                return false;
            }
        }
        
        #region 私有辅助方法
        
        /// <summary>
        /// 根据算法类型创建哈希算法实例
        /// </summary>
        /// <param name="algorithm">校验和算法</param>
        /// <returns>哈希算法实例</returns>
        private static HashAlgorithm CreateHashAlgorithm(ChecksumAlgorithm algorithm)
        {
            return algorithm switch
            {
                ChecksumAlgorithm.MD5 => MD5.Create(),
                ChecksumAlgorithm.SHA1 => SHA1.Create(),
                ChecksumAlgorithm.SHA256 => SHA256.Create(),
                ChecksumAlgorithm.SHA512 => SHA512.Create(),
                _ => SHA256.Create()
            };
        }
        
        /// <summary>
        /// 验证文件基本属性（大小、年龄、权限等）
        /// </summary>
        /// <param name="result">验证结果对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task ValidateFileBasicsAsync(FileValidationResult result, CancellationToken cancellationToken)
        {
            var fileInfo = new System.IO.FileInfo(result.FilePath);
            
            // Check file size
            if (fileInfo.Length == 0)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.FileSystem,
                    Severity = ValidationSeverity.Critical,
                    Description = "File is empty (0 bytes)",
                    SuggestedAction = "Verify backup process completed successfully"
                });
            }
            else if (fileInfo.Length < 1024) // Less than 1KB
            {
                result.Issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.FileSystem,
                    Severity = ValidationSeverity.Warning,
                    Description = "File is very small, may not contain complete backup data",
                    SuggestedAction = "Verify backup process and source data size"
                });
            }
            
            // Check file age
            var age = DateTime.Now - fileInfo.CreationTime;
            if (age.TotalDays > 365)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.FileSystem,
                    Severity = ValidationSeverity.Info,
                    Description = $"File is {age.TotalDays:F0} days old",
                    SuggestedAction = "Consider creating a fresh backup if this is intended for restoration"
                });
            }
            
            // Check file permissions
            if (fileInfo.IsReadOnly)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.FileSystem,
                    Severity = ValidationSeverity.Info,
                    Description = "File is read-only",
                    SuggestedAction = "Ensure file can be accessed for restoration if needed"
                });
            }
        }
        
        /// <summary>
        /// 检查文件是否为压缩文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>如果是压缩文件返回true，否则返回false</returns>
        private async Task<bool> IsCompressedFileAsync(string filePath)
        {
            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var buffer = new byte[3];
                var bytesRead = await fileStream.ReadAsync(buffer, 0, 3);
                
                // Check for GZip magic number (1f 8b)
                if (bytesRead >= 2 && buffer[0] == 0x1f && buffer[1] == 0x8b)
                {
                    return true;
                }
                
                // Check for ZIP magic number (50 4b)
                if (bytesRead >= 2 && buffer[0] == 0x50 && buffer[1] == 0x4b)
                {
                    return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 检查文件是否为加密文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>如果是加密文件返回true，否则返回false</returns>
        private async Task<bool> IsEncryptedFileAsync(string filePath)
        {
            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var buffer = new byte[8];
                var bytesRead = await fileStream.ReadAsync(buffer, 0, 8);
                
                // Check for our encryption magic header "MYSQLBAK"
                if (bytesRead == 8)
                {
                    var header = Encoding.ASCII.GetString(buffer);
                    return header == "MYSQLBAK";
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 验证压缩文件的完整性
        /// </summary>
        /// <param name="result">验证结果对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task ValidateCompressionIntegrityAsync(FileValidationResult result, CancellationToken cancellationToken)
        {
            try
            {
                var canDecompress = await ValidateCompressionAsync(result.FilePath, cancellationToken);
                if (!canDecompress)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Type = ValidationIssueType.Compression,
                        Severity = ValidationSeverity.Critical,
                        Description = "Compressed file is corrupted and cannot be decompressed",
                        SuggestedAction = "Recreate backup from source data"
                    });
                }
            }
            catch (Exception ex)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.Compression,
                    Severity = ValidationSeverity.Error,
                    Description = $"Compression validation failed: {ex.Message}",
                    SuggestedAction = "Check file integrity and compression format"
                });
            }
        }
        
        /// <summary>
        /// 验证加密文件的完整性
        /// </summary>
        /// <param name="result">验证结果对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task ValidateEncryptionIntegrityAsync(FileValidationResult result, CancellationToken cancellationToken)
        {
            try
            {
                // Get encryption metadata without password
                var metadata = await _encryptionService.GetMetadataAsync(result.FilePath);
                
                result.Metadata["EncryptionAlgorithm"] = metadata.Algorithm;
                result.Metadata["EncryptionKeyDerivation"] = metadata.KeyDerivation;
                result.Metadata["EncryptionIterations"] = metadata.Iterations;
                result.Metadata["OriginalSize"] = metadata.OriginalSize;
                
                // Basic encryption integrity checks
                if (string.IsNullOrEmpty(metadata.Salt) || string.IsNullOrEmpty(metadata.IV))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Type = ValidationIssueType.Encryption,
                        Severity = ValidationSeverity.Critical,
                        Description = "Encryption metadata is incomplete or corrupted",
                        SuggestedAction = "Recreate encrypted backup"
                    });
                }
            }
            catch (Exception ex)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.Encryption,
                    Severity = ValidationSeverity.Error,
                    Description = $"Encryption validation failed: {ex.Message}",
                    SuggestedAction = "Check file format and encryption integrity"
                });
            }
        }
        
        /// <summary>
        /// 验证压缩文件的详细信息
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>压缩验证结果</returns>
        private async Task<CompressionValidationResult> ValidateCompressionDetailsAsync(string filePath, CancellationToken cancellationToken)
        {
            var result = new CompressionValidationResult
            {
                IsCompressed = true
            };
            
            try
            {
                // Detect compression format
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var buffer = new byte[3];
                await fileStream.ReadAsync(buffer, 0, 3, cancellationToken);
                
                if (buffer[0] == 0x1f && buffer[1] == 0x8b)
                {
                    result.CompressionFormat = "GZip";
                }
                else if (buffer[0] == 0x50 && buffer[1] == 0x4b)
                {
                    result.CompressionFormat = "ZIP";
                }
                else
                {
                    result.CompressionFormat = "Unknown";
                }
                
                // Test decompression
                result.CanDecompress = await ValidateCompressionAsync(filePath, cancellationToken);
                
                // Calculate compression ratio
                var compressedSize = new System.IO.FileInfo(filePath).Length;
                result.CompressionRatio = 1.0; // Default if we can't determine original size
                
                // For GZip files, try to estimate uncompressed size
                if (result.CompressionFormat == "GZip")
                {
                    try
                    {
                        fileStream.Seek(-4, SeekOrigin.End);
                        var sizeBytes = new byte[4];
                        await fileStream.ReadAsync(sizeBytes, 0, 4, cancellationToken);
                        var uncompressedSize = BitConverter.ToUInt32(sizeBytes, 0);
                        
                        if (uncompressedSize > 0)
                        {
                            result.EstimatedUncompressedSize = uncompressedSize;
                            result.CompressionRatio = (double)compressedSize / uncompressedSize;
                        }
                    }
                    catch
                    {
                        // Ignore errors in size estimation
                    }
                }
            }
            catch (Exception ex)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.Compression,
                    Severity = ValidationSeverity.Error,
                    Description = $"Compression analysis failed: {ex.Message}",
                    SuggestedAction = "Check file format and compression integrity"
                });
            }
            
            return result;
        }
        
        /// <summary>
        /// 验证加密文件的详细信息
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>加密验证结果</returns>
        private async Task<EncryptionValidationResult> ValidateEncryptionDetailsAsync(string filePath, CancellationToken cancellationToken)
        {
            var result = new EncryptionValidationResult
            {
                IsEncrypted = true
            };
            
            try
            {
                result.Metadata = await _encryptionService.GetMetadataAsync(filePath);
                result.EncryptionAlgorithm = result.Metadata.Algorithm;
                result.CanDecrypt = false; // Cannot test without password
            }
            catch (Exception ex)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.Encryption,
                    Severity = ValidationSeverity.Error,
                    Description = $"Encryption analysis failed: {ex.Message}",
                    SuggestedAction = "Check file format and encryption integrity"
                });
            }
            
            return result;
        }
        
        /// <summary>
        /// 验证数据库内容
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>数据库验证结果</returns>
        private async Task<DatabaseValidationResult> ValidateDatabaseContentAsync(string filePath, CancellationToken cancellationToken)
        {
            var result = new DatabaseValidationResult();
            
            try
            {
                // Basic heuristic checks for database content
                // This is a simplified implementation - in practice, you'd want more sophisticated detection
                
                var fileName = Path.GetFileName(filePath).ToLowerInvariant();
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                
                // Check file name patterns
                if (fileName.Contains("mysql") || fileName.Contains("database") || fileName.Contains("backup"))
                {
                    result.IsValidDatabaseBackup = true;
                    result.DatabaseType = "MySQL";
                }
                
                // Check file extensions
                if (extension == ".sql" || extension == ".dump" || extension == ".bak")
                {
                    result.IsValidDatabaseBackup = true;
                    if (result.DatabaseType == string.Empty)
                    {
                        result.DatabaseType = "SQL";
                    }
                }
                
                // For compressed/encrypted files, we can't easily analyze content
                // without decompressing/decrypting first
                if (await IsCompressedFileAsync(filePath) || await IsEncryptedFileAsync(filePath))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Type = ValidationIssueType.Database,
                        Severity = ValidationSeverity.Info,
                        Description = "Cannot analyze database content in compressed/encrypted file",
                        SuggestedAction = "Decompress/decrypt file for detailed database analysis"
                    });
                }
            }
            catch (Exception ex)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Type = ValidationIssueType.Database,
                    Severity = ValidationSeverity.Warning,
                    Description = $"Database content analysis failed: {ex.Message}",
                    SuggestedAction = "Manual verification of database backup content may be required"
                });
            }
            
            return result;
        }
        
        /// <summary>
        /// 生成验证摘要
        /// </summary>
        /// <param name="report">验证报告</param>
        /// <returns>验证摘要</returns>
        private ValidationSummary GenerateValidationSummary(ValidationReport report)
        {
            var summary = new ValidationSummary();
            
            // Count issues by severity
            var allIssues = new List<ValidationIssue>();
            allIssues.AddRange(report.ValidationResult.Issues);
            
            if (report.CompressionResult != null)
                allIssues.AddRange(report.CompressionResult.Issues);
            
            if (report.EncryptionResult != null)
                allIssues.AddRange(report.EncryptionResult.Issues);
            
            if (report.DatabaseResult != null)
                allIssues.AddRange(report.DatabaseResult.Issues);
            
            summary.TotalIssues = allIssues.Count;
            summary.CriticalIssues = allIssues.Count(i => i.Severity == ValidationSeverity.Critical);
            summary.WarningIssues = allIssues.Count(i => i.Severity == ValidationSeverity.Warning);
            summary.InfoIssues = allIssues.Count(i => i.Severity == ValidationSeverity.Info);
            
            // Determine overall status
            if (summary.CriticalIssues > 0)
            {
                summary.Status = ValidationStatus.Failed;
                summary.Message = $"Validation failed with {summary.CriticalIssues} critical issue(s)";
            }
            else if (allIssues.Any(i => i.Severity == ValidationSeverity.Error))
            {
                summary.Status = ValidationStatus.Failed;
                summary.Message = "Validation failed with errors";
            }
            else if (summary.WarningIssues > 0)
            {
                summary.Status = ValidationStatus.PassedWithWarnings;
                summary.Message = $"Validation passed with {summary.WarningIssues} warning(s)";
            }
            else
            {
                summary.Status = ValidationStatus.Passed;
                summary.Message = "Validation passed successfully";
            }
            
            // Calculate confidence score
            summary.ConfidenceScore = CalculateConfidenceScore(summary, report, allIssues);
            
            // Generate recommendations
            summary.Recommendations = GenerateRecommendations(allIssues, report);
            
            return summary;
        }
        
        /// <summary>
        /// 计算置信度分数
        /// </summary>
        /// <param name="summary">验证摘要</param>
        /// <param name="report">验证报告</param>
        /// <param name="allIssues">所有问题列表</param>
        /// <returns>置信度分数（0-100）</returns>
        private int CalculateConfidenceScore(ValidationSummary summary, ValidationReport report, List<ValidationIssue> allIssues)
        {
            var score = 100;
            
            // Deduct points for issues
            score -= summary.CriticalIssues * 50;
            score -= allIssues.Count(i => i.Severity == ValidationSeverity.Error) * 25;
            score -= summary.WarningIssues * 10;
            score -= summary.InfoIssues * 2;
            
            // Bonus points for good practices
            if (report.EncryptionResult?.IsEncrypted == true)
                score += 10;
            
            if (report.CompressionResult?.IsCompressed == true)
                score += 5;
            
            return Math.Max(0, Math.Min(100, score));
        }
        
        /// <summary>
        /// 根据问题和报告生成建议
        /// </summary>
        /// <param name="issues">问题列表</param>
        /// <param name="report">验证报告</param>
        /// <returns>建议列表</returns>
        private List<string> GenerateRecommendations(List<ValidationIssue> issues, ValidationReport report)
        {
            var recommendations = new List<string>();
            
            if (issues.Any(i => i.Type == ValidationIssueType.Integrity))
            {
                recommendations.Add("Verify backup integrity by comparing checksums with original source");
            }
            
            if (report.EncryptionResult?.IsEncrypted != true && report.FileInfo.SizeBytes > 1024 * 1024) // > 1MB
            {
                recommendations.Add("Consider encrypting backup files for enhanced security");
            }
            
            if (report.CompressionResult?.IsCompressed != true)
            {
                recommendations.Add("Consider compressing backup files to save storage space");
            }
            
            if (issues.Any(i => i.Severity == ValidationSeverity.Critical))
            {
                recommendations.Add("Critical issues found - recreate backup from source data");
            }
            
            if (report.FileInfo.SizeBytes == 0)
            {
                recommendations.Add("Empty backup file detected - verify backup process configuration");
            }
            
            return recommendations;
        }
        
        #endregion
    }
}