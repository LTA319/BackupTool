using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services
{
    /// <summary>
    /// Service for validating backup files and ensuring their integrity
    /// </summary>
    public class ValidationService : IValidationService
    {
        private readonly ILoggingService _loggingService;
        private readonly IEncryptionService _encryptionService;
        private readonly ICompressionService _compressionService;
        private const int DefaultBufferSize = 65536; // 64KB
        
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
        /// Validates a backup file for integrity and completeness
        /// </summary>
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
                
                // Calculate checksum
                result.Checksum = await CalculateChecksumAsync(filePath, ChecksumAlgorithm.SHA256, cancellationToken);
                
                // Perform basic file validation
                await ValidateFileBasicsAsync(result, cancellationToken);
                
                // Check if file is compressed
                if (await IsCompressedFileAsync(filePath))
                {
                    await ValidateCompressionIntegrityAsync(result, cancellationToken);
                }
                
                // Check if file is encrypted
                if (await IsEncryptedFileAsync(filePath))
                {
                    await ValidateEncryptionIntegrityAsync(result, cancellationToken);
                }
                
                // Determine overall validation status
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
        /// Validates file integrity using checksum comparison
        /// </summary>
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
        /// Calculates checksum for a file
        /// </summary>
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
        /// Generates a comprehensive validation report for a backup file
        /// </summary>
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
        /// Validates that a backup file can be successfully decompressed
        /// </summary>
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
        /// Validates that an encrypted backup file can be decrypted with the provided password
        /// </summary>
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
        
        #region Private Helper Methods
        
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