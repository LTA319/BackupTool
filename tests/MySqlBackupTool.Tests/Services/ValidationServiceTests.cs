using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.IO.Compression;

namespace MySqlBackupTool.Tests.Services
{
    public class ValidationServiceTests : IDisposable
    {
        // Test constants
        private const int Sha256HexLength = 64;
        private const int Md5HexLength = 32;
        private const int Sha1HexLength = 40;
        private const int Sha512HexLength = 128;
        private const int LargeFileSize = 1024 * 1024; // 1MB
        private const int MaxValidationTimeSeconds = 10;
        private const int MaxConfidenceScore = 100;
        
        private readonly IValidationService _validationService;
        private readonly IEncryptionService _encryptionService;
        private readonly string _testDirectory;
        private readonly ServiceProvider _serviceProvider;
        
        public ValidationServiceTests()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            services.AddSharedServices("Data Source=:memory:");
            
            _serviceProvider = services.BuildServiceProvider();
            _validationService = _serviceProvider.GetRequiredService<IValidationService>();
            _encryptionService = _serviceProvider.GetRequiredService<IEncryptionService>();
            _testDirectory = Path.Combine(Path.GetTempPath(), "MySqlBackupTool.Tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
        }
        
        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
            _serviceProvider?.Dispose();
        }
        
        [Fact]
        public async Task CalculateChecksumAsync_WithValidFile_ShouldReturnCorrectChecksum()
        {
            // Arrange
            var testContent = "This is a test file for checksum calculation.";
            var filePath = Path.Combine(_testDirectory, "test.txt");
            await File.WriteAllTextAsync(filePath, testContent);
            
            // Act
            var checksum = await _validationService.CalculateChecksumAsync(filePath, ChecksumAlgorithm.SHA256);
            
            // Assert
            Assert.NotNull(checksum);
            Assert.NotEmpty(checksum);
            Assert.Equal(Sha256HexLength, checksum.Length); // SHA256 produces 64 hex characters
            
            // Verify consistency
            var checksum2 = await _validationService.CalculateChecksumAsync(filePath, ChecksumAlgorithm.SHA256);
            Assert.Equal(checksum, checksum2);
        }
        
        [Fact]
        public async Task CalculateChecksumAsync_WithDifferentAlgorithms_ShouldReturnDifferentLengths()
        {
            // Arrange
            var testContent = "Test content for different algorithms";
            var filePath = Path.Combine(_testDirectory, "test.txt");
            await File.WriteAllTextAsync(filePath, testContent);
            
            // Act
            var md5 = await _validationService.CalculateChecksumAsync(filePath, ChecksumAlgorithm.MD5);
            var sha1 = await _validationService.CalculateChecksumAsync(filePath, ChecksumAlgorithm.SHA1);
            var sha256 = await _validationService.CalculateChecksumAsync(filePath, ChecksumAlgorithm.SHA256);
            var sha512 = await _validationService.CalculateChecksumAsync(filePath, ChecksumAlgorithm.SHA512);
            
            // Assert
            Assert.Equal(Md5HexLength, md5.Length);   // MD5: 32 hex chars
            Assert.Equal(Sha1HexLength, sha1.Length);  // SHA1: 40 hex chars
            Assert.Equal(Sha256HexLength, sha256.Length); // SHA256: 64 hex chars
            Assert.Equal(Sha512HexLength, sha512.Length); // SHA512: 128 hex chars
            
            // All should be different
            Assert.NotEqual(md5, sha1);
            Assert.NotEqual(sha1, sha256);
            Assert.NotEqual(sha256, sha512);
        }
        
        [Fact]
        public async Task ValidateIntegrityAsync_WithMatchingChecksum_ShouldReturnTrue()
        {
            // Arrange
            var testContent = "Test content for integrity validation";
            var filePath = Path.Combine(_testDirectory, "test.txt");
            await File.WriteAllTextAsync(filePath, testContent);
            
            var expectedChecksum = await _validationService.CalculateChecksumAsync(filePath, ChecksumAlgorithm.SHA256);
            
            // Act
            var isValid = await _validationService.ValidateIntegrityAsync(filePath, expectedChecksum, ChecksumAlgorithm.SHA256);
            
            // Assert
            Assert.True(isValid);
        }
        
        [Fact]
        public async Task ValidateIntegrityAsync_WithNonMatchingChecksum_ShouldReturnFalse()
        {
            // Arrange
            var testContent = "Test content for integrity validation";
            var filePath = Path.Combine(_testDirectory, "test.txt");
            await File.WriteAllTextAsync(filePath, testContent);
            
            var wrongChecksum = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
            
            // Act
            var isValid = await _validationService.ValidateIntegrityAsync(filePath, wrongChecksum, ChecksumAlgorithm.SHA256);
            
            // Assert
            Assert.False(isValid);
        }
        
        [Fact]
        public async Task ValidateBackupAsync_WithValidFile_ShouldReturnValidResult()
        {
            // Arrange
            var testContent = "This is a valid backup file content.";
            var filePath = Path.Combine(_testDirectory, "backup.sql");
            await File.WriteAllTextAsync(filePath, testContent);
            
            // Act
            var result = await _validationService.ValidateBackupAsync(filePath);
            
            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid);
            Assert.Equal(filePath, result.FilePath);
            Assert.Equal(testContent.Length, result.FileSize);
            Assert.NotEmpty(result.Checksum);
            Assert.Equal(ChecksumAlgorithm.SHA256, result.Algorithm);
            Assert.True(result.ValidationDuration.TotalMilliseconds > 0);
        }
        
        [Fact]
        public async Task ValidateBackupAsync_WithEmptyFile_ShouldReturnInvalidWithCriticalIssue()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "empty.sql");
            await File.WriteAllTextAsync(filePath, "");
            
            // Act
            var result = await _validationService.ValidateBackupAsync(filePath);
            
            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsValid);
            Assert.Equal(0, result.FileSize);
            Assert.Contains(result.Issues, i => i.Severity == ValidationSeverity.Critical && i.Description.Contains("empty"));
        }
        
        [Fact]
        public async Task ValidateBackupAsync_WithSmallFile_ShouldReturnValidWithWarning()
        {
            // Arrange
            var testContent = "Small"; // Less than 1KB
            var filePath = Path.Combine(_testDirectory, "small.sql");
            await File.WriteAllTextAsync(filePath, testContent);
            
            // Act
            var result = await _validationService.ValidateBackupAsync(filePath);
            
            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsValid); // Should be valid despite warning
            Assert.Contains(result.Issues, i => i.Severity == ValidationSeverity.Warning && i.Description.Contains("small"));
        }
        
        [Fact]
        public async Task ValidateCompressionAsync_WithValidGzipFile_ShouldReturnTrue()
        {
            // Arrange
            var testContent = "This is test content for compression validation.";
            var originalPath = Path.Combine(_testDirectory, "test.txt");
            var compressedPath = Path.Combine(_testDirectory, "test.gz");
            
            await File.WriteAllTextAsync(originalPath, testContent);
            
            // Create a valid gzip file
            using (var originalStream = new FileStream(originalPath, FileMode.Open, FileAccess.Read))
            using (var compressedStream = new FileStream(compressedPath, FileMode.Create, FileAccess.Write))
            using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Compress))
            {
                await originalStream.CopyToAsync(gzipStream);
            }
            
            // Act
            var isValid = await _validationService.ValidateCompressionAsync(compressedPath);
            
            // Assert
            Assert.True(isValid);
        }
        
        [Fact]
        public async Task ValidateCompressionAsync_WithNonCompressedFile_ShouldReturnTrue()
        {
            // Arrange
            var testContent = "This is not a compressed file.";
            var filePath = Path.Combine(_testDirectory, "test.txt");
            await File.WriteAllTextAsync(filePath, testContent);
            
            // Act
            var isValid = await _validationService.ValidateCompressionAsync(filePath);
            
            // Assert
            Assert.True(isValid); // Non-compressed files are considered valid
        }
        
        [Fact]
        public async Task ValidateCompressionAsync_WithCorruptedGzipFile_ShouldReturnFalse()
        {
            // Arrange
            var corruptedPath = Path.Combine(_testDirectory, "corrupted.gz");
            
            // Create a file with gzip header but corrupted data that will cause decompression to fail
            var corruptedData = new byte[] { 
                0x1f, 0x8b, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, // Valid gzip header
                0x00, 0x03, // Extra flags and OS
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // Invalid compressed data
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
            };
            await File.WriteAllBytesAsync(corruptedPath, corruptedData);
            
            // Act
            var isValid = await _validationService.ValidateCompressionAsync(corruptedPath);
            
            // Assert
            Assert.False(isValid);
        }
        
        [Fact]
        public async Task ValidateEncryptionAsync_WithValidEncryptedFile_ShouldReturnTrue()
        {
            // Arrange
            var testContent = "This is test content for encryption validation.";
            var originalPath = Path.Combine(_testDirectory, "test.txt");
            var encryptedPath = Path.Combine(_testDirectory, "test.encrypted");
            var password = "TestPassword123!";
            
            await File.WriteAllTextAsync(originalPath, testContent);
            await _encryptionService.EncryptAsync(originalPath, encryptedPath, password);
            
            // Act
            var isValid = await _validationService.ValidateEncryptionAsync(encryptedPath, password);
            
            // Assert
            Assert.True(isValid);
        }
        
        [Fact]
        public async Task ValidateEncryptionAsync_WithWrongPassword_ShouldReturnFalse()
        {
            // Arrange
            var testContent = "This is test content for encryption validation.";
            var originalPath = Path.Combine(_testDirectory, "test.txt");
            var encryptedPath = Path.Combine(_testDirectory, "test.encrypted");
            var correctPassword = "TestPassword123!";
            var wrongPassword = "WrongPassword456!";
            
            await File.WriteAllTextAsync(originalPath, testContent);
            await _encryptionService.EncryptAsync(originalPath, encryptedPath, correctPassword);
            
            // Act
            var isValid = await _validationService.ValidateEncryptionAsync(encryptedPath, wrongPassword);
            
            // Assert
            Assert.False(isValid);
        }
        
        [Fact]
        public async Task ValidateEncryptionAsync_WithNonEncryptedFile_ShouldReturnTrue()
        {
            // Arrange
            var testContent = "This is not an encrypted file.";
            var filePath = Path.Combine(_testDirectory, "test.txt");
            await File.WriteAllTextAsync(filePath, testContent);
            
            // Act
            var isValid = await _validationService.ValidateEncryptionAsync(filePath, "anypassword");
            
            // Assert
            Assert.True(isValid); // Non-encrypted files are considered valid
        }
        
        [Fact]
        public async Task GenerateReportAsync_WithValidFile_ShouldReturnComprehensiveReport()
        {
            // Arrange
            var testContent = "This is a comprehensive test file for report generation.";
            var filePath = Path.Combine(_testDirectory, "backup.sql");
            await File.WriteAllTextAsync(filePath, testContent);
            
            // Act
            var report = await _validationService.GenerateReportAsync(filePath);
            
            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.ValidationResult);
            Assert.NotNull(report.FileInfo);
            Assert.NotNull(report.Summary);
            
            // File info should be populated
            Assert.Equal(filePath, report.FileInfo.FullPath);
            Assert.Equal("backup.sql", report.FileInfo.FileName);
            Assert.Equal(".sql", report.FileInfo.Extension);
            Assert.Equal(testContent.Length, report.FileInfo.SizeBytes);
            
            // Summary should be populated
            Assert.True(report.Summary.ConfidenceScore >= 0 && report.Summary.ConfidenceScore <= 100);
            Assert.NotEmpty(report.Summary.Message);
            
            // Report metadata
            Assert.True(report.GeneratedAt > DateTime.MinValue);
            Assert.NotEmpty(report.ValidatorVersion);
        }
        
        [Fact]
        public async Task GenerateReportAsync_WithEncryptedFile_ShouldIncludeEncryptionResults()
        {
            // Arrange
            var testContent = "This is test content for encrypted report generation.";
            var originalPath = Path.Combine(_testDirectory, "test.txt");
            var encryptedPath = Path.Combine(_testDirectory, "backup.encrypted");
            var password = "TestPassword123!";
            
            await File.WriteAllTextAsync(originalPath, testContent);
            await _encryptionService.EncryptAsync(originalPath, encryptedPath, password);
            
            // Act
            var report = await _validationService.GenerateReportAsync(encryptedPath);
            
            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.EncryptionResult);
            Assert.True(report.EncryptionResult.IsEncrypted);
            Assert.NotEmpty(report.EncryptionResult.EncryptionAlgorithm);
            Assert.NotNull(report.EncryptionResult.Metadata);
        }
        
        [Fact]
        public async Task GenerateReportAsync_WithCompressedFile_ShouldIncludeCompressionResults()
        {
            // Arrange
            var testContent = "This is test content for compressed report generation.";
            var originalPath = Path.Combine(_testDirectory, "test.txt");
            var compressedPath = Path.Combine(_testDirectory, "backup.gz");
            
            await File.WriteAllTextAsync(originalPath, testContent);
            
            // Create a valid gzip file
            using (var originalStream = new FileStream(originalPath, FileMode.Open, FileAccess.Read))
            using (var compressedStream = new FileStream(compressedPath, FileMode.Create, FileAccess.Write))
            using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Compress))
            {
                await originalStream.CopyToAsync(gzipStream);
            }
            
            // Act
            var report = await _validationService.GenerateReportAsync(compressedPath);
            
            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.CompressionResult);
            Assert.True(report.CompressionResult.IsCompressed);
            Assert.Equal("GZip", report.CompressionResult.CompressionFormat);
            Assert.True(report.CompressionResult.CanDecompress);
        }
        
        [Fact]
        public async Task CalculateChecksumAsync_WithNullPath_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _validationService.CalculateChecksumAsync(null!));
        }
        
        [Fact]
        public async Task CalculateChecksumAsync_WithEmptyPath_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _validationService.CalculateChecksumAsync(""));
        }
        
        [Fact]
        public async Task CalculateChecksumAsync_WithNonExistentFile_ShouldThrowFileNotFoundException()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.txt");
            
            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => _validationService.CalculateChecksumAsync(nonExistentPath));
        }
        
        [Fact]
        public async Task ValidateIntegrityAsync_WithNullPath_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _validationService.ValidateIntegrityAsync(null!, "checksum"));
        }
        
        [Fact]
        public async Task ValidateIntegrityAsync_WithNullChecksum_ShouldThrowArgumentException()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "test.txt");
            await File.WriteAllTextAsync(filePath, "test");
            
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _validationService.ValidateIntegrityAsync(filePath, null!));
        }
        
        [Fact]
        public async Task ValidateBackupAsync_WithNullPath_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _validationService.ValidateBackupAsync(null!));
        }
        
        [Fact]
        public async Task ValidateBackupAsync_WithNonExistentFile_ShouldThrowFileNotFoundException()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.txt");
            
            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => _validationService.ValidateBackupAsync(nonExistentPath));
        }
        
        [Fact]
        public async Task CalculateChecksumAsync_WithLargeFile_ShouldHandleEfficiently()
        {
            // Arrange
            var largeContent = new string('A', LargeFileSize); // 1MB of data
            var filePath = Path.Combine(_testDirectory, "large_test.txt");
            await File.WriteAllTextAsync(filePath, largeContent);
            
            // Act
            var startTime = DateTime.Now;
            var checksum = await _validationService.CalculateChecksumAsync(filePath, ChecksumAlgorithm.SHA256);
            var duration = DateTime.Now - startTime;
            
            // Assert
            Assert.NotNull(checksum);
            Assert.Equal(Sha256HexLength, checksum.Length);
            Assert.True(duration.TotalSeconds < MaxValidationTimeSeconds); // Should complete within 10 seconds for 1MB file
        }
        
        [Fact]
        public async Task ValidateBackupAsync_WithOldFile_ShouldIncludeAgeWarning()
        {
            // Arrange
            var testContent = "Old backup file content";
            var filePath = Path.Combine(_testDirectory, "old_backup.sql");
            await File.WriteAllTextAsync(filePath, testContent);
            
            // Modify file creation time to be very old
            var oldDate = DateTime.Now.AddYears(-2);
            File.SetCreationTime(filePath, oldDate);
            
            // Act
            var result = await _validationService.ValidateBackupAsync(filePath);
            
            // Assert
            Assert.NotNull(result);
            Assert.Contains(result.Issues, i => i.Description.Contains("days old"));
        }
        
        [Fact]
        public async Task GenerateReportAsync_ShouldIncludeValidationSummary()
        {
            // Arrange
            var testContent = "Test content for summary validation";
            var filePath = Path.Combine(_testDirectory, "test.sql");
            await File.WriteAllTextAsync(filePath, testContent);
            
            // Act
            var report = await _validationService.GenerateReportAsync(filePath);
            
            // Assert
            Assert.NotNull(report.Summary);
            Assert.True(report.Summary.ConfidenceScore >= 0);
            Assert.True(report.Summary.ConfidenceScore <= MaxConfidenceScore);
            Assert.NotEmpty(report.Summary.Message);
            
            // Should have some status
            Assert.True(Enum.IsDefined(typeof(ValidationStatus), report.Summary.Status));
        }
        
        [Fact]
        public async Task ValidateBackupAsync_WithReadOnlyFile_ShouldIncludeInfoIssue()
        {
            // Arrange
            var testContent = "Read-only backup file content";
            var filePath = Path.Combine(_testDirectory, "readonly.sql");
            await File.WriteAllTextAsync(filePath, testContent);
            
            // Make file read-only
            var fileInfo = new System.IO.FileInfo(filePath);
            fileInfo.IsReadOnly = true;
            
            // Act
            var result = await _validationService.ValidateBackupAsync(filePath);
            
            // Assert
            Assert.NotNull(result);
            Assert.Contains(result.Issues, i => i.Description.Contains("read-only"));
            
            // Clean up - remove read-only attribute
            fileInfo.IsReadOnly = false;
        }
    }
}