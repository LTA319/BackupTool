using System.Text.Json.Serialization;

namespace MySqlBackupTool.Shared.Models
{
    /// <summary>
    /// 加密文件的元数据信息
    /// Metadata for encrypted files
    /// </summary>
    public class EncryptionMetadata
    {
        /// <summary>
        /// 使用的加密算法，默认为AES-256-CBC
        /// Encryption algorithm used, defaults to AES-256-CBC
        /// </summary>
        public string Algorithm { get; set; } = "AES-256-CBC";
        
        /// <summary>
        /// 使用的密钥派生函数，默认为PBKDF2
        /// Key derivation function used, defaults to PBKDF2
        /// </summary>
        public string KeyDerivation { get; set; } = "PBKDF2";
        
        /// <summary>
        /// 密钥派生的迭代次数，默认为100000次
        /// Number of iterations for key derivation, defaults to 100000
        /// </summary>
        public int Iterations { get; set; } = 100000;
        
        /// <summary>
        /// 用于密钥派生的盐值（Base64编码）
        /// Salt used for key derivation (Base64 encoded)
        /// </summary>
        public string Salt { get; set; } = string.Empty;
        
        /// <summary>
        /// 初始化向量（Base64编码）
        /// Initialization vector (Base64 encoded)
        /// </summary>
        public string IV { get; set; } = string.Empty;
        
        /// <summary>
        /// 文件加密的时间戳
        /// Timestamp when the file was encrypted
        /// </summary>
        public DateTime EncryptedAt { get; set; }
        
        /// <summary>
        /// 加密前的原始文件大小
        /// Original file size before encryption
        /// </summary>
        public long OriginalSize { get; set; }
        
        /// <summary>
        /// 原始文件的校验和（SHA256）
        /// Checksum of the original file (SHA256)
        /// </summary>
        public string OriginalChecksum { get; set; } = string.Empty;
        
        /// <summary>
        /// 加密格式的版本号，默认为1
        /// Version of the encryption format, defaults to 1
        /// </summary>
        public int Version { get; set; } = 1;
        
        /// <summary>
        /// 可选的描述或备注信息
        /// Optional description or notes
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// 加密操作的配置选项
    /// Configuration for encryption operations
    /// </summary>
    public class EncryptionConfig
    {
        /// <summary>
        /// 用于加密/解密的密码
        /// Password for encryption/decryption
        /// </summary>
        public string Password { get; set; } = string.Empty;
        
        /// <summary>
        /// 密钥长度（位），默认为256位
        /// Key size in bits, defaults to 256
        /// </summary>
        public int KeySize { get; set; } = 256;
        
        /// <summary>
        /// PBKDF2的迭代次数，默认为100000次
        /// Number of iterations for PBKDF2, defaults to 100000
        /// </summary>
        public int Iterations { get; set; } = 100000;
        
        /// <summary>
        /// 是否在加密后安全删除原始文件，默认为false
        /// Whether to securely delete the original file after encryption, defaults to false
        /// </summary>
        public bool SecureDelete { get; set; } = false;
        
        /// <summary>
        /// 流操作的缓冲区大小，默认为64KB
        /// Buffer size for streaming operations, defaults to 64KB
        /// </summary>
        public int BufferSize { get; set; } = 65536;
        
        /// <summary>
        /// 是否在加密前压缩文件，默认为false
        /// Whether to compress the file before encryption, defaults to false
        /// </summary>
        public bool CompressBeforeEncryption { get; set; } = false;
    }
    
    /// <summary>
    /// 加密操作的结果信息
    /// Result of an encryption operation
    /// </summary>
    public class EncryptionResult
    {
        /// <summary>
        /// 操作是否成功
        /// Whether the operation was successful
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// 输出文件的路径
        /// Path to the output file
        /// </summary>
        public string OutputPath { get; set; } = string.Empty;
        
        /// <summary>
        /// 加密元数据信息
        /// Encryption metadata
        /// </summary>
        public EncryptionMetadata? Metadata { get; set; }
        
        /// <summary>
        /// 操作失败时的错误消息
        /// Error message if operation failed
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// 操作持续时间
        /// Duration of the operation
        /// </summary>
        public TimeSpan Duration { get; set; }
        
        /// <summary>
        /// 加密后文件的大小
        /// Size of the encrypted file
        /// </summary>
        public long EncryptedSize { get; set; }
    }
    
    /// <summary>
    /// 加密/解密操作的进度信息
    /// Progress information for encryption/decryption operations
    /// </summary>
    public class EncryptionProgress
    {
        /// <summary>
        /// 完成百分比（0-100）
        /// Percentage completed (0-100)
        /// </summary>
        public double PercentComplete { get; set; }
        
        /// <summary>
        /// 已处理的字节数
        /// Bytes processed so far
        /// </summary>
        public long BytesProcessed { get; set; }
        
        /// <summary>
        /// 总字节数
        /// Total bytes to process
        /// </summary>
        public long TotalBytes { get; set; }
        
        /// <summary>
        /// 当前操作状态（加密中、解密中、验证中等）
        /// Current operation (Encrypting, Decrypting, Validating, etc.)
        /// </summary>
        public string CurrentOperation { get; set; } = string.Empty;
        
        /// <summary>
        /// 预计剩余时间
        /// Estimated time remaining
        /// </summary>
        public TimeSpan? EstimatedTimeRemaining { get; set; }
        
        /// <summary>
        /// 处理速度（字节/秒）
        /// Processing speed in bytes per second
        /// </summary>
        public double BytesPerSecond { get; set; }
    }
}