using System.Text.Json.Serialization;

namespace MySqlBackupTool.Shared.Models
{
    /// <summary>
    /// Metadata for encrypted files
    /// </summary>
    public class EncryptionMetadata
    {
        /// <summary>
        /// Encryption algorithm used
        /// </summary>
        public string Algorithm { get; set; } = "AES-256-CBC";
        
        /// <summary>
        /// Key derivation function used
        /// </summary>
        public string KeyDerivation { get; set; } = "PBKDF2";
        
        /// <summary>
        /// Number of iterations for key derivation
        /// </summary>
        public int Iterations { get; set; } = 100000;
        
        /// <summary>
        /// Salt used for key derivation (Base64 encoded)
        /// </summary>
        public string Salt { get; set; } = string.Empty;
        
        /// <summary>
        /// Initialization vector (Base64 encoded)
        /// </summary>
        public string IV { get; set; } = string.Empty;
        
        /// <summary>
        /// Timestamp when the file was encrypted
        /// </summary>
        public DateTime EncryptedAt { get; set; }
        
        /// <summary>
        /// Original file size before encryption
        /// </summary>
        public long OriginalSize { get; set; }
        
        /// <summary>
        /// Checksum of the original file (SHA256)
        /// </summary>
        public string OriginalChecksum { get; set; } = string.Empty;
        
        /// <summary>
        /// Version of the encryption format
        /// </summary>
        public int Version { get; set; } = 1;
        
        /// <summary>
        /// Optional description or notes
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Configuration for encryption operations
    /// </summary>
    public class EncryptionConfig
    {
        /// <summary>
        /// Password for encryption/decryption
        /// </summary>
        public string Password { get; set; } = string.Empty;
        
        /// <summary>
        /// Key size in bits (default: 256)
        /// </summary>
        public int KeySize { get; set; } = 256;
        
        /// <summary>
        /// Number of iterations for PBKDF2 (default: 100000)
        /// </summary>
        public int Iterations { get; set; } = 100000;
        
        /// <summary>
        /// Whether to securely delete the original file after encryption
        /// </summary>
        public bool SecureDelete { get; set; } = false;
        
        /// <summary>
        /// Buffer size for streaming operations (default: 64KB)
        /// </summary>
        public int BufferSize { get; set; } = 65536;
        
        /// <summary>
        /// Whether to compress the file before encryption
        /// </summary>
        public bool CompressBeforeEncryption { get; set; } = false;
    }
    
    /// <summary>
    /// Result of an encryption operation
    /// </summary>
    public class EncryptionResult
    {
        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Path to the output file
        /// </summary>
        public string OutputPath { get; set; } = string.Empty;
        
        /// <summary>
        /// Encryption metadata
        /// </summary>
        public EncryptionMetadata? Metadata { get; set; }
        
        /// <summary>
        /// Error message if operation failed
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Duration of the operation
        /// </summary>
        public TimeSpan Duration { get; set; }
        
        /// <summary>
        /// Size of the encrypted file
        /// </summary>
        public long EncryptedSize { get; set; }
    }
    
    /// <summary>
    /// Progress information for encryption/decryption operations
    /// </summary>
    public class EncryptionProgress
    {
        /// <summary>
        /// Percentage completed (0-100)
        /// </summary>
        public double PercentComplete { get; set; }
        
        /// <summary>
        /// Bytes processed so far
        /// </summary>
        public long BytesProcessed { get; set; }
        
        /// <summary>
        /// Total bytes to process
        /// </summary>
        public long TotalBytes { get; set; }
        
        /// <summary>
        /// Current operation (Encrypting, Decrypting, Validating, etc.)
        /// </summary>
        public string CurrentOperation { get; set; } = string.Empty;
        
        /// <summary>
        /// Estimated time remaining
        /// </summary>
        public TimeSpan? EstimatedTimeRemaining { get; set; }
        
        /// <summary>
        /// Processing speed in bytes per second
        /// </summary>
        public double BytesPerSecond { get; set; }
    }
}