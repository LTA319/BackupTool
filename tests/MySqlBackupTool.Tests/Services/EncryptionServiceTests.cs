using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Text;

namespace MySqlBackupTool.Tests.Services
{
    public class EncryptionServiceTests : IDisposable
    {
        private readonly IEncryptionService _encryptionService;
        private readonly string _testDirectory;
        private readonly ServiceProvider _serviceProvider;
        
        public EncryptionServiceTests()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            services.AddSharedServices("Data Source=:memory:");
            
            _serviceProvider = services.BuildServiceProvider();
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
        public async Task EncryptAsync_WithValidInput_ShouldCreateEncryptedFile()
        {
            // Arrange
            var testContent = "This is a test file for encryption.";
            var inputPath = Path.Combine(_testDirectory, "test.txt");
            var outputPath = Path.Combine(_testDirectory, "test.encrypted");
            var password = "TestPassword123!";
            
            await File.WriteAllTextAsync(inputPath, testContent);
            
            // Act
            var metadata = await _encryptionService.EncryptAsync(inputPath, outputPath, password);
            
            // Assert
            Assert.True(File.Exists(outputPath));
            Assert.NotNull(metadata);
            Assert.Equal("AES-256-CBC", metadata.Algorithm);
            Assert.Equal("PBKDF2", metadata.KeyDerivation);
            Assert.Equal(testContent.Length, metadata.OriginalSize);
            Assert.False(string.IsNullOrEmpty(metadata.Salt));
            Assert.False(string.IsNullOrEmpty(metadata.IV));
            Assert.False(string.IsNullOrEmpty(metadata.OriginalChecksum));
            
            // Verify encrypted file is different from original
            var encryptedContent = await File.ReadAllBytesAsync(outputPath);
            var originalContent = Encoding.UTF8.GetBytes(testContent);
            Assert.NotEqual(originalContent, encryptedContent);
        }
        
        [Fact]
        public async Task DecryptAsync_WithCorrectPassword_ShouldRestoreOriginalFile()
        {
            // Arrange
            var testContent = "This is a test file for encryption and decryption.";
            var inputPath = Path.Combine(_testDirectory, "test.txt");
            var encryptedPath = Path.Combine(_testDirectory, "test.encrypted");
            var decryptedPath = Path.Combine(_testDirectory, "test.decrypted");
            var password = "TestPassword123!";
            
            await File.WriteAllTextAsync(inputPath, testContent);
            
            // Encrypt first
            await _encryptionService.EncryptAsync(inputPath, encryptedPath, password);
            
            // Act
            await _encryptionService.DecryptAsync(encryptedPath, decryptedPath, password);
            
            // Assert
            Assert.True(File.Exists(decryptedPath));
            var decryptedContent = await File.ReadAllTextAsync(decryptedPath);
            Assert.Equal(testContent, decryptedContent);
        }
        
        [Fact]
        public async Task DecryptAsync_WithWrongPassword_ShouldThrowException()
        {
            // Arrange
            var testContent = "This is a test file for encryption.";
            var inputPath = Path.Combine(_testDirectory, "test.txt");
            var encryptedPath = Path.Combine(_testDirectory, "test.encrypted");
            var decryptedPath = Path.Combine(_testDirectory, "test.decrypted");
            var correctPassword = "TestPassword123!";
            var wrongPassword = "WrongPassword456!";
            
            await File.WriteAllTextAsync(inputPath, testContent);
            await _encryptionService.EncryptAsync(inputPath, encryptedPath, correctPassword);
            
            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _encryptionService.DecryptAsync(encryptedPath, decryptedPath, wrongPassword));
            
            // Verify no partial file was created
            Assert.False(File.Exists(decryptedPath));
        }
        
        [Fact]
        public async Task ValidatePasswordAsync_WithCorrectPassword_ShouldReturnTrue()
        {
            // Arrange
            var testContent = "This is a test file for password validation.";
            var inputPath = Path.Combine(_testDirectory, "test.txt");
            var encryptedPath = Path.Combine(_testDirectory, "test.encrypted");
            var password = "TestPassword123!";
            
            await File.WriteAllTextAsync(inputPath, testContent);
            await _encryptionService.EncryptAsync(inputPath, encryptedPath, password);
            
            // Act
            var isValid = await _encryptionService.ValidatePasswordAsync(encryptedPath, password);
            
            // Assert
            Assert.True(isValid);
        }
        
        [Fact]
        public async Task ValidatePasswordAsync_WithWrongPassword_ShouldReturnFalse()
        {
            // Arrange
            var testContent = "This is a test file for password validation.";
            var inputPath = Path.Combine(_testDirectory, "test.txt");
            var encryptedPath = Path.Combine(_testDirectory, "test.encrypted");
            var correctPassword = "TestPassword123!";
            var wrongPassword = "WrongPassword456!";
            
            await File.WriteAllTextAsync(inputPath, testContent);
            await _encryptionService.EncryptAsync(inputPath, encryptedPath, correctPassword);
            
            // Act
            var isValid = await _encryptionService.ValidatePasswordAsync(encryptedPath, wrongPassword);
            
            // Assert
            Assert.False(isValid);
        }
        
        [Fact]
        public async Task GetMetadataAsync_ShouldReturnCorrectMetadata()
        {
            // Arrange
            var testContent = "This is a test file for metadata retrieval.";
            var inputPath = Path.Combine(_testDirectory, "test.txt");
            var encryptedPath = Path.Combine(_testDirectory, "test.encrypted");
            var password = "TestPassword123!";
            
            await File.WriteAllTextAsync(inputPath, testContent);
            var originalMetadata = await _encryptionService.EncryptAsync(inputPath, encryptedPath, password);
            
            // Act
            var retrievedMetadata = await _encryptionService.GetMetadataAsync(encryptedPath);
            
            // Assert
            Assert.NotNull(retrievedMetadata);
            Assert.Equal(originalMetadata.Algorithm, retrievedMetadata.Algorithm);
            Assert.Equal(originalMetadata.KeyDerivation, retrievedMetadata.KeyDerivation);
            Assert.Equal(originalMetadata.Iterations, retrievedMetadata.Iterations);
            Assert.Equal(originalMetadata.Salt, retrievedMetadata.Salt);
            Assert.Equal(originalMetadata.IV, retrievedMetadata.IV);
            Assert.Equal(originalMetadata.OriginalSize, retrievedMetadata.OriginalSize);
            Assert.Equal(originalMetadata.OriginalChecksum, retrievedMetadata.OriginalChecksum);
        }
        
        [Fact]
        public void GenerateSecurePassword_WithDefaultLength_ShouldReturnValidPassword()
        {
            // Act
            var password = _encryptionService.GenerateSecurePassword();
            
            // Assert
            Assert.NotNull(password);
            Assert.Equal(32, password.Length);
            Assert.True(password.Any(char.IsUpper));
            Assert.True(password.Any(char.IsLower));
            Assert.True(password.Any(char.IsDigit));
            Assert.True(password.Any(c => "!@#$%^&*".Contains(c))); // Check for special characters
        }
        
        [Fact]
        public void GenerateSecurePassword_WithCustomLength_ShouldReturnCorrectLength()
        {
            // Arrange
            var customLength = 16;
            
            // Act
            var password = _encryptionService.GenerateSecurePassword(customLength);
            
            // Assert
            Assert.NotNull(password);
            Assert.Equal(customLength, password.Length);
        }
        
        [Fact]
        public void GenerateSecurePassword_WithTooShortLength_ShouldThrowException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(
                () => _encryptionService.GenerateSecurePassword(7));
        }
        
        [Fact]
        public async Task EncryptAsync_WithLargeFile_ShouldHandleEfficiently()
        {
            // Arrange
            var largeContent = new string('A', 1024 * 1024); // 1MB of data
            var inputPath = Path.Combine(_testDirectory, "large_test.txt");
            var outputPath = Path.Combine(_testDirectory, "large_test.encrypted");
            var password = "TestPassword123!";
            
            await File.WriteAllTextAsync(inputPath, largeContent);
            
            // Act
            var startTime = DateTime.Now;
            var metadata = await _encryptionService.EncryptAsync(inputPath, outputPath, password);
            var duration = DateTime.Now - startTime;
            
            // Assert
            Assert.True(File.Exists(outputPath));
            Assert.Equal(largeContent.Length, metadata.OriginalSize);
            Assert.True(duration.TotalSeconds < 30); // Should complete within 30 seconds for 1MB file
            
            // Verify file can be decrypted
            var decryptedPath = Path.Combine(_testDirectory, "large_test.decrypted");
            await _encryptionService.DecryptAsync(outputPath, decryptedPath, password);
            
            var decryptedContent = await File.ReadAllTextAsync(decryptedPath);
            Assert.Equal(largeContent, decryptedContent);
        }
        
        [Fact]
        public async Task EncryptAsync_WithNullOrEmptyInputs_ShouldThrowArgumentException()
        {
            // Arrange
            var validPath = Path.Combine(_testDirectory, "test.txt");
            var password = "TestPassword123!";
            
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _encryptionService.EncryptAsync("", validPath, password));
            
            await Assert.ThrowsAsync<ArgumentException>(
                () => _encryptionService.EncryptAsync(validPath, "", password));
            
            await Assert.ThrowsAsync<ArgumentException>(
                () => _encryptionService.EncryptAsync(validPath, validPath, ""));
        }
        
        [Fact]
        public async Task EncryptAsync_WithNonExistentFile_ShouldThrowFileNotFoundException()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.txt");
            var outputPath = Path.Combine(_testDirectory, "output.encrypted");
            var password = "TestPassword123!";
            
            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => _encryptionService.EncryptAsync(nonExistentPath, outputPath, password));
        }
        
        [Fact]
        public async Task DecryptAsync_WithNonExistentFile_ShouldThrowFileNotFoundException()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.encrypted");
            var outputPath = Path.Combine(_testDirectory, "output.txt");
            var password = "TestPassword123!";
            
            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => _encryptionService.DecryptAsync(nonExistentPath, outputPath, password));
        }
        
        [Fact]
        public async Task GetMetadataAsync_WithInvalidFile_ShouldThrowException()
        {
            // Arrange
            var invalidPath = Path.Combine(_testDirectory, "invalid.txt");
            await File.WriteAllTextAsync(invalidPath, "This is not an encrypted file");
            
            // Act & Assert
            await Assert.ThrowsAsync<InvalidDataException>(
                () => _encryptionService.GetMetadataAsync(invalidPath));
        }
        
        [Fact]
        public async Task EncryptDecryptRoundTrip_WithSpecialCharacters_ShouldPreserveContent()
        {
            // Arrange
            var testContent = "Special chars: àáâãäåæçèéêë ñòóôõö ùúûüý 中文 🚀 \\n\\r\\t";
            var inputPath = Path.Combine(_testDirectory, "special.txt");
            var encryptedPath = Path.Combine(_testDirectory, "special.encrypted");
            var decryptedPath = Path.Combine(_testDirectory, "special.decrypted");
            var password = "TestPassword123!";
            
            await File.WriteAllTextAsync(inputPath, testContent, Encoding.UTF8);
            
            // Act
            await _encryptionService.EncryptAsync(inputPath, encryptedPath, password);
            await _encryptionService.DecryptAsync(encryptedPath, decryptedPath, password);
            
            // Assert
            var decryptedContent = await File.ReadAllTextAsync(decryptedPath, Encoding.UTF8);
            Assert.Equal(testContent, decryptedContent);
        }
        
        [Fact]
        public async Task EncryptAsync_ShouldCreateOutputDirectory()
        {
            // Arrange
            var testContent = "Test content";
            var inputPath = Path.Combine(_testDirectory, "test.txt");
            var outputDir = Path.Combine(_testDirectory, "subdir", "nested");
            var outputPath = Path.Combine(outputDir, "test.encrypted");
            var password = "TestPassword123!";
            
            await File.WriteAllTextAsync(inputPath, testContent);
            
            // Act
            await _encryptionService.EncryptAsync(inputPath, outputPath, password);
            
            // Assert
            Assert.True(Directory.Exists(outputDir));
            Assert.True(File.Exists(outputPath));
        }
    }
}