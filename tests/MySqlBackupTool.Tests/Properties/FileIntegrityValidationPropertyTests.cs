using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using System.Security.Cryptography;
using System.Text;

namespace MySqlBackupTool.Tests.Properties;

/// <summary>
/// Property-based tests for file integrity validation functionality
/// **Property 9: File Integrity Validation**
/// **Validates: Requirements 4.5, 8.4**
/// </summary>
public class FileIntegrityValidationPropertyTests : IDisposable
{
    private readonly ChecksumService _checksumService;
    private readonly List<string> _tempFiles;

    public FileIntegrityValidationPropertyTests()
    {
        var logger = new LoggerFactory().CreateLogger<ChecksumService>();
        _checksumService = new ChecksumService(logger);
        _tempFiles = new List<string>();
    }

    /// <summary>
    /// Property 9: File Integrity Validation
    /// For any file transfer (chunked or whole), the system should validate file integrity using checksums 
    /// and detect any corruption or modification.
    /// **Validates: Requirements 4.5, 8.4**
    /// </summary>
    [Property(MaxTest = 20)]
    public bool FileIntegrityValidationProperty()
    {
        try
        {
            // Generate test parameters
            var fileSize = Gen.Choose(1, 10 * 1024 * 1024).Sample(0, 1).First(); // 1B to 10MB
            
            // Arrange - Create test file with known content
            var originalFile = CreateTestFile(fileSize);
            
            // Calculate original checksums
            var (originalMD5, originalSHA256) = _checksumService.CalculateFileChecksumsAsync(originalFile).Result;
            
            // Act & Assert - Test various integrity validation scenarios
            
            // 1. Validate identical file should pass
            var identicalValidation = _checksumService.ValidateFileIntegrityAsync(
                originalFile, originalMD5, originalSHA256).Result;
            
            if (!identicalValidation)
            {
                Console.WriteLine("Identical file validation failed");
                return false;
            }
            
            // 2. Create a copy and validate it should pass
            var copyFile = CreateFileCopy(originalFile);
            var copyValidation = _checksumService.ValidateFileIntegrityAsync(
                copyFile, originalMD5, originalSHA256).Result;
            
            if (!copyValidation)
            {
                Console.WriteLine("File copy validation failed");
                return false;
            }
            
            // 3. Create a corrupted file and validate it should fail
            var corruptedFile = CreateCorruptedFile(originalFile);
            var corruptedValidation = _checksumService.ValidateFileIntegrityAsync(
                corruptedFile, originalMD5, originalSHA256).Result;
            
            if (corruptedValidation)
            {
                Console.WriteLine("Corrupted file validation should have failed but passed");
                return false;
            }
            
            // 4. Test with wrong checksums should fail
            var wrongMD5 = "00000000000000000000000000000000";
            var wrongSHA256 = "0000000000000000000000000000000000000000000000000000000000000000";
            
            var wrongChecksumValidation = _checksumService.ValidateFileIntegrityAsync(
                originalFile, wrongMD5, wrongSHA256).Result;
            
            if (wrongChecksumValidation)
            {
                Console.WriteLine("Wrong checksum validation should have failed but passed");
                return false;
            }
            
            // 5. Test with only MD5 checksum
            var md5OnlyValidation = _checksumService.ValidateFileIntegrityAsync(
                originalFile, originalMD5, null).Result;
            
            if (!md5OnlyValidation)
            {
                Console.WriteLine("MD5-only validation failed");
                return false;
            }
            
            // 6. Test with only SHA256 checksum
            var sha256OnlyValidation = _checksumService.ValidateFileIntegrityAsync(
                originalFile, null, originalSHA256).Result;
            
            if (!sha256OnlyValidation)
            {
                Console.WriteLine("SHA256-only validation failed");
                return false;
            }
            
            // 7. Test with no checksums should pass (no validation requested)
            var noChecksumValidation = _checksumService.ValidateFileIntegrityAsync(
                originalFile, null, null).Result;
            
            if (!noChecksumValidation)
            {
                Console.WriteLine("No checksum validation should have passed");
                return false;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"File integrity validation property test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Property test for chunk integrity validation
    /// Chunk-level integrity checking should detect any corruption in individual chunks
    /// **Validates: Requirements 4.5, 8.4**
    /// </summary>
    [Property(MaxTest = 15)]
    public bool ChunkIntegrityValidationProperty()
    {
        try
        {
            // Generate test parameters
            var chunkSize = Gen.Choose(256, 64 * 1024).Sample(0, 1).First(); // 256B to 64KB
            
            // Arrange - Create test chunk data
            var originalChunkData = CreateRandomBytes(chunkSize);
            var originalChecksum = _checksumService.CalculateMD5(originalChunkData);
            
            // Act & Assert - Test various chunk validation scenarios
            
            // 1. Validate identical chunk should pass
            var identicalValidation = _checksumService.ValidateChunkIntegrity(originalChunkData, originalChecksum);
            
            if (!identicalValidation)
            {
                Console.WriteLine("Identical chunk validation failed");
                return false;
            }
            
            // 2. Create a copy and validate it should pass
            var copyChunkData = new byte[originalChunkData.Length];
            Array.Copy(originalChunkData, copyChunkData, originalChunkData.Length);
            
            var copyValidation = _checksumService.ValidateChunkIntegrity(copyChunkData, originalChecksum);
            
            if (!copyValidation)
            {
                Console.WriteLine("Chunk copy validation failed");
                return false;
            }
            
            // 3. Create corrupted chunk and validate it should fail (if chunk is large enough to corrupt)
            if (originalChunkData.Length > 1)
            {
                var corruptedChunkData = new byte[originalChunkData.Length];
                Array.Copy(originalChunkData, corruptedChunkData, originalChunkData.Length);
                
                // Corrupt one byte
                corruptedChunkData[corruptedChunkData.Length / 2] = (byte)(corruptedChunkData[corruptedChunkData.Length / 2] ^ 0xFF);
                
                var corruptedValidation = _checksumService.ValidateChunkIntegrity(corruptedChunkData, originalChecksum);
                
                if (corruptedValidation)
                {
                    Console.WriteLine("Corrupted chunk validation should have failed but passed");
                    return false;
                }
            }
            
            // 4. Test with wrong checksum should fail
            var wrongChecksum = "00000000000000000000000000000000";
            var wrongChecksumValidation = _checksumService.ValidateChunkIntegrity(originalChunkData, wrongChecksum);
            
            if (wrongChecksumValidation)
            {
                Console.WriteLine("Wrong checksum validation should have failed but passed");
                return false;
            }
            
            // 5. Test with empty checksum should pass (no validation requested)
            var emptyChecksumValidation = _checksumService.ValidateChunkIntegrity(originalChunkData, "");
            
            if (!emptyChecksumValidation)
            {
                Console.WriteLine("Empty checksum validation should have passed");
                return false;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Chunk integrity validation property test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Property test for checksum calculation consistency
    /// The same file should always produce the same checksums
    /// **Validates: Requirements 4.5, 8.4**
    /// </summary>
    [Property(MaxTest = 10)]
    public bool ChecksumCalculationConsistencyProperty()
    {
        try
        {
            // Generate test parameters
            var fileSize = Gen.Choose(1, 1024 * 1024).Sample(0, 1).First(); // 1B to 1MB
            
            // Arrange - Create test file
            var testFile = CreateTestFile(fileSize);
            
            // Act - Calculate checksums multiple times
            var (md5_1, sha256_1) = _checksumService.CalculateFileChecksumsAsync(testFile).Result;
            var (md5_2, sha256_2) = _checksumService.CalculateFileChecksumsAsync(testFile).Result;
            var md5_3 = _checksumService.CalculateFileMD5Async(testFile).Result;
            var sha256_3 = _checksumService.CalculateFileSHA256Async(testFile).Result;
            
            // Assert - All calculations should be consistent
            var md5Consistent = md5_1 == md5_2 && md5_2 == md5_3;
            var sha256Consistent = sha256_1 == sha256_2 && sha256_2 == sha256_3;
            
            // Verify checksums are not empty and have correct length
            var md5Valid = !string.IsNullOrEmpty(md5_1) && md5_1.Length == 32;
            var sha256Valid = !string.IsNullOrEmpty(sha256_1) && sha256_1.Length == 64;
            
            // Verify checksums are lowercase hex
            var md5Format = md5_1.All(c => char.IsDigit(c) || (c >= 'a' && c <= 'f'));
            var sha256Format = sha256_1.All(c => char.IsDigit(c) || (c >= 'a' && c <= 'f'));
            
            return md5Consistent && sha256Consistent && md5Valid && sha256Valid && md5Format && sha256Format;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Checksum calculation consistency test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Property test for byte array checksum calculation
    /// Byte array checksums should be consistent and match file checksums for the same data
    /// **Validates: Requirements 4.5, 8.4**
    /// </summary>
    [Property(MaxTest = 15)]
    public bool ByteArrayChecksumConsistencyProperty()
    {
        try
        {
            // Generate test parameters
            var dataSize = Gen.Choose(1, 1024 * 1024).Sample(0, 1).First(); // 1B to 1MB
            
            // Arrange - Create test data
            var testData = CreateRandomBytes(dataSize);
            
            // Create a temporary file with the same data
            var testFile = CreateTestFileFromBytes(testData);
            
            // Act - Calculate checksums using both methods
            var byteArrayMD5 = _checksumService.CalculateMD5(testData);
            var byteArraySHA256 = _checksumService.CalculateSHA256(testData);
            
            var (fileMD5, fileSHA256) = _checksumService.CalculateFileChecksumsAsync(testFile).Result;
            
            // Assert - Byte array and file checksums should match
            var md5Match = byteArrayMD5 == fileMD5;
            var sha256Match = byteArraySHA256 == fileSHA256;
            
            // Verify checksums are valid format
            var md5Valid = !string.IsNullOrEmpty(byteArrayMD5) && byteArrayMD5.Length == 32;
            var sha256Valid = !string.IsNullOrEmpty(byteArraySHA256) && byteArraySHA256.Length == 64;
            
            return md5Match && sha256Match && md5Valid && sha256Valid;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Byte array checksum consistency test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Creates a test file with random content
    /// </summary>
    private string CreateTestFile(long size)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"IntegrityTest_{Guid.NewGuid():N}.dat");
        _tempFiles.Add(tempFile);

        using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write);
        var random = new System.Random(42); // Fixed seed for reproducibility
        var buffer = new byte[Math.Min(8192, size)];
        long written = 0;

        while (written < size)
        {
            var bytesToWrite = (int)Math.Min(buffer.Length, size - written);
            random.NextBytes(buffer);
            fileStream.Write(buffer, 0, bytesToWrite);
            written += bytesToWrite;
        }

        return tempFile;
    }

    /// <summary>
    /// Creates a test file from byte array
    /// </summary>
    private string CreateTestFileFromBytes(byte[] data)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"IntegrityTest_{Guid.NewGuid():N}.dat");
        _tempFiles.Add(tempFile);

        File.WriteAllBytes(tempFile, data);
        return tempFile;
    }

    /// <summary>
    /// Creates a copy of an existing file
    /// </summary>
    private string CreateFileCopy(string originalFile)
    {
        var copyFile = Path.Combine(Path.GetTempPath(), $"IntegrityTestCopy_{Guid.NewGuid():N}.dat");
        _tempFiles.Add(copyFile);

        File.Copy(originalFile, copyFile);
        return copyFile;
    }

    /// <summary>
    /// Creates a corrupted version of an existing file
    /// </summary>
    private string CreateCorruptedFile(string originalFile)
    {
        var corruptedFile = Path.Combine(Path.GetTempPath(), $"IntegrityTestCorrupted_{Guid.NewGuid():N}.dat");
        _tempFiles.Add(corruptedFile);

        // Copy the original file
        File.Copy(originalFile, corruptedFile);

        // Corrupt the file by modifying some bytes
        var fileInfo = new FileInfo(corruptedFile);
        if (fileInfo.Length > 0)
        {
            using var fileStream = new FileStream(corruptedFile, FileMode.Open, FileAccess.ReadWrite);
            
            // Corrupt bytes at different positions
            var positions = new[] { 0, fileInfo.Length / 4, fileInfo.Length / 2, fileInfo.Length * 3 / 4, fileInfo.Length - 1 };
            
            foreach (var pos in positions)
            {
                if (pos < fileInfo.Length)
                {
                    fileStream.Seek(pos, SeekOrigin.Begin);
                    var originalByte = fileStream.ReadByte();
                    if (originalByte != -1)
                    {
                        fileStream.Seek(pos, SeekOrigin.Begin);
                        fileStream.WriteByte((byte)(originalByte ^ 0xFF)); // Flip all bits
                    }
                }
            }
        }

        return corruptedFile;
    }

    /// <summary>
    /// Creates random byte array
    /// </summary>
    private byte[] CreateRandomBytes(int size)
    {
        var random = new System.Random(42); // Fixed seed for reproducibility
        var bytes = new byte[size];
        random.NextBytes(bytes);
        return bytes;
    }

    public void Dispose()
    {
        // Clean up temporary files
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}