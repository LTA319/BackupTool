namespace MySqlBackupTool.Shared.Models
{
    /// <summary>
    /// 文件验证操作的结果
    /// Represents the result of a file validation operation
    /// </summary>
    public class FileValidationResult
    {
        /// <summary>
        /// 验证是否通过
        /// Whether the validation passed
        /// </summary>
        public bool IsValid { get; set; }
        
        /// <summary>
        /// 被验证文件的路径
        /// Path to the validated file
        /// </summary>
        public string FilePath { get; set; } = string.Empty;
        
        /// <summary>
        /// 被验证文件的大小（字节）
        /// Size of the validated file in bytes
        /// </summary>
        public long FileSize { get; set; }
        
        /// <summary>
        /// 文件的计算校验和
        /// Calculated checksum of the file
        /// </summary>
        public string Checksum { get; set; } = string.Empty;
        
        /// <summary>
        /// 用于校验和计算的算法
        /// Algorithm used for checksum calculation
        /// </summary>
        public ChecksumAlgorithm Algorithm { get; set; }
        
        /// <summary>
        /// 执行验证的时间
        /// When the validation was performed
        /// </summary>
        public DateTime ValidatedAt { get; set; }
        
        /// <summary>
        /// 发现的验证问题列表
        /// List of validation issues found
        /// </summary>
        public List<ValidationIssue> Issues { get; set; } = new();
        
        /// <summary>
        /// 执行验证所花费的时间
        /// Time taken to perform the validation
        /// </summary>
        public TimeSpan ValidationDuration { get; set; }
        
        /// <summary>
        /// 关于验证的附加元数据
        /// Additional metadata about the validation
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
    
    /// <summary>
    /// 备份验证过程中发现的验证问题
    /// Represents a validation issue found during backup validation
    /// </summary>
    public class ValidationIssue
    {
        /// <summary>
        /// 验证问题的类型
        /// Type of validation issue
        /// </summary>
        public ValidationIssueType Type { get; set; }
        
        /// <summary>
        /// 问题的描述
        /// Description of the issue
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// 问题的严重程度
        /// Severity level of the issue
        /// </summary>
        public ValidationSeverity Severity { get; set; }
        
        /// <summary>
        /// 发现问题的位置或上下文
        /// Location or context where the issue was found
        /// </summary>
        public string Location { get; set; } = string.Empty;
        
        /// <summary>
        /// 解决问题的建议操作
        /// Suggested action to resolve the issue
        /// </summary>
        public string SuggestedAction { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Comprehensive validation report for a backup file
    /// </summary>
    public class ValidationReport
    {
        /// <summary>
        /// Overall validation result
        /// </summary>
        public FileValidationResult ValidationResult { get; set; } = new();
        
        /// <summary>
        /// File information
        /// </summary>
        public BackupFileInfo FileInfo { get; set; } = new();
        
        /// <summary>
        /// Compression validation results (if applicable)
        /// </summary>
        public CompressionValidationResult? CompressionResult { get; set; }
        
        /// <summary>
        /// Encryption validation results (if applicable)
        /// </summary>
        public EncryptionValidationResult? EncryptionResult { get; set; }
        
        /// <summary>
        /// Database-specific validation results (if applicable)
        /// </summary>
        public DatabaseValidationResult? DatabaseResult { get; set; }
        
        /// <summary>
        /// Summary of validation results
        /// </summary>
        public ValidationSummary Summary { get; set; } = new();
        
        /// <summary>
        /// When the report was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; }
        
        /// <summary>
        /// Version of the validation service that generated this report
        /// </summary>
        public string ValidatorVersion { get; set; } = "1.0.0";
    }
    
    /// <summary>
    /// File information for validation (renamed to avoid conflict with System.IO.FileInfo)
    /// </summary>
    public class BackupFileInfo
    {
        /// <summary>
        /// Full path to the file
        /// </summary>
        public string FullPath { get; set; } = string.Empty;
        
        /// <summary>
        /// File name without path
        /// </summary>
        public string FileName { get; set; } = string.Empty;
        
        /// <summary>
        /// File extension
        /// </summary>
        public string Extension { get; set; } = string.Empty;
        
        /// <summary>
        /// File size in bytes
        /// </summary>
        public long SizeBytes { get; set; }
        
        /// <summary>
        /// File creation time
        /// </summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// File last modified time
        /// </summary>
        public DateTime ModifiedAt { get; set; }
        
        /// <summary>
        /// Whether the file is read-only
        /// </summary>
        public bool IsReadOnly { get; set; }
    }
    
    /// <summary>
    /// Compression validation results
    /// </summary>
    public class CompressionValidationResult
    {
        /// <summary>
        /// Whether the file is compressed
        /// </summary>
        public bool IsCompressed { get; set; }
        
        /// <summary>
        /// Compression format detected
        /// </summary>
        public string CompressionFormat { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether the compressed file can be decompressed
        /// </summary>
        public bool CanDecompress { get; set; }
        
        /// <summary>
        /// Compression ratio (compressed size / original size)
        /// </summary>
        public double CompressionRatio { get; set; }
        
        /// <summary>
        /// Estimated uncompressed size
        /// </summary>
        public long EstimatedUncompressedSize { get; set; }
        
        /// <summary>
        /// Issues found during compression validation
        /// </summary>
        public List<ValidationIssue> Issues { get; set; } = new();
    }
    
    /// <summary>
    /// Encryption validation results
    /// </summary>
    public class EncryptionValidationResult
    {
        /// <summary>
        /// Whether the file is encrypted
        /// </summary>
        public bool IsEncrypted { get; set; }
        
        /// <summary>
        /// Encryption algorithm detected
        /// </summary>
        public string EncryptionAlgorithm { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether the file can be decrypted with provided credentials
        /// </summary>
        public bool CanDecrypt { get; set; }
        
        /// <summary>
        /// Encryption metadata (if available)
        /// </summary>
        public EncryptionMetadata? Metadata { get; set; }
        
        /// <summary>
        /// Issues found during encryption validation
        /// </summary>
        public List<ValidationIssue> Issues { get; set; } = new();
    }
    
    /// <summary>
    /// Database-specific validation results
    /// </summary>
    public class DatabaseValidationResult
    {
        /// <summary>
        /// Whether the backup appears to be a valid database backup
        /// </summary>
        public bool IsValidDatabaseBackup { get; set; }
        
        /// <summary>
        /// Database type detected (MySQL, PostgreSQL, etc.)
        /// </summary>
        public string DatabaseType { get; set; } = string.Empty;
        
        /// <summary>
        /// Database version detected (if available)
        /// </summary>
        public string DatabaseVersion { get; set; } = string.Empty;
        
        /// <summary>
        /// List of databases found in the backup
        /// </summary>
        public List<string> DatabaseNames { get; set; } = new();
        
        /// <summary>
        /// List of tables found in the backup
        /// </summary>
        public List<string> TableNames { get; set; } = new();
        
        /// <summary>
        /// Estimated number of records in the backup
        /// </summary>
        public long EstimatedRecordCount { get; set; }
        
        /// <summary>
        /// Issues found during database validation
        /// </summary>
        public List<ValidationIssue> Issues { get; set; } = new();
    }
    
    /// <summary>
    /// Summary of validation results
    /// </summary>
    public class ValidationSummary
    {
        /// <summary>
        /// Overall validation status
        /// </summary>
        public ValidationStatus Status { get; set; }
        
        /// <summary>
        /// Total number of issues found
        /// </summary>
        public int TotalIssues { get; set; }
        
        /// <summary>
        /// Number of critical issues
        /// </summary>
        public int CriticalIssues { get; set; }
        
        /// <summary>
        /// Number of warning issues
        /// </summary>
        public int WarningIssues { get; set; }
        
        /// <summary>
        /// Number of informational issues
        /// </summary>
        public int InfoIssues { get; set; }
        
        /// <summary>
        /// Overall confidence score (0-100)
        /// </summary>
        public int ConfidenceScore { get; set; }
        
        /// <summary>
        /// Brief summary message
        /// </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// Recommendations for improving backup quality
        /// </summary>
        public List<string> Recommendations { get; set; } = new();
    }
    
    /// <summary>
    /// 验证问题的类型
    /// Types of validation issues
    /// </summary>
    public enum ValidationIssueType
    {
        /// <summary>
        /// 文件系统相关问题
        /// File system related issue
        /// </summary>
        FileSystem,
        
        /// <summary>
        /// 校验和或完整性问题
        /// Checksum or integrity issue
        /// </summary>
        Integrity,
        
        /// <summary>
        /// 压缩相关问题
        /// Compression related issue
        /// </summary>
        Compression,
        
        /// <summary>
        /// 加密相关问题
        /// Encryption related issue
        /// </summary>
        Encryption,
        
        /// <summary>
        /// 数据库结构问题
        /// Database structure issue
        /// </summary>
        Database,
        
        /// <summary>
        /// 性能或大小问题
        /// Performance or size issue
        /// </summary>
        Performance,
        
        /// <summary>
        /// 配置问题
        /// Configuration issue
        /// </summary>
        Configuration,
        
        /// <summary>
        /// 安全相关问题
        /// Security related issue
        /// </summary>
        Security
    }
    
    /// <summary>
    /// 验证问题的严重程度级别
    /// Severity levels for validation issues
    /// </summary>
    public enum ValidationSeverity
    {
        /// <summary>
        /// 信息性消息
        /// Informational message
        /// </summary>
        Info,
        
        /// <summary>
        /// 应该处理的警告
        /// Warning that should be addressed
        /// </summary>
        Warning,
        
        /// <summary>
        /// 阻止正常备份使用的错误
        /// Error that prevents proper backup usage
        /// </summary>
        Error,
        
        /// <summary>
        /// 表示备份损坏的关键错误
        /// Critical error that indicates backup corruption
        /// </summary>
        Critical
    }
    
    /// <summary>
    /// 整体验证状态
    /// Overall validation status
    /// </summary>
    public enum ValidationStatus
    {
        /// <summary>
        /// 验证通过，无问题
        /// Validation passed with no issues
        /// </summary>
        Passed,
        
        /// <summary>
        /// 验证通过但有警告
        /// Validation passed with warnings
        /// </summary>
        PassedWithWarnings,
        
        /// <summary>
        /// 验证失败，有错误
        /// Validation failed with errors
        /// </summary>
        Failed,
        
        /// <summary>
        /// 验证无法完成
        /// Validation could not be completed
        /// </summary>
        Incomplete
    }
    
    /// <summary>
    /// 支持的校验和算法
    /// Supported checksum algorithms
    /// </summary>
    public enum ChecksumAlgorithm
    {
        /// <summary>
        /// MD5哈希算法（快速但安全性较低）
        /// MD5 hash algorithm (fast but less secure)
        /// </summary>
        MD5,
        
        /// <summary>
        /// SHA-1哈希算法
        /// SHA-1 hash algorithm
        /// </summary>
        SHA1,
        
        /// <summary>
        /// SHA-256哈希算法（推荐）
        /// SHA-256 hash algorithm (recommended)
        /// </summary>
        SHA256,
        
        /// <summary>
        /// SHA-512哈希算法（最安全）
        /// SHA-512 hash algorithm (most secure)
        /// </summary>
        SHA512
    }
}