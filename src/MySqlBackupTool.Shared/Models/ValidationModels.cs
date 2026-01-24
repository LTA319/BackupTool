namespace MySqlBackupTool.Shared.Models
{
    /// <summary>
    /// Represents the result of a file validation operation
    /// </summary>
    public class FileValidationResult
    {
        /// <summary>
        /// Whether the validation passed
        /// </summary>
        public bool IsValid { get; set; }
        
        /// <summary>
        /// Path to the validated file
        /// </summary>
        public string FilePath { get; set; } = string.Empty;
        
        /// <summary>
        /// Size of the validated file in bytes
        /// </summary>
        public long FileSize { get; set; }
        
        /// <summary>
        /// Calculated checksum of the file
        /// </summary>
        public string Checksum { get; set; } = string.Empty;
        
        /// <summary>
        /// Algorithm used for checksum calculation
        /// </summary>
        public ChecksumAlgorithm Algorithm { get; set; }
        
        /// <summary>
        /// When the validation was performed
        /// </summary>
        public DateTime ValidatedAt { get; set; }
        
        /// <summary>
        /// List of validation issues found
        /// </summary>
        public List<ValidationIssue> Issues { get; set; } = new();
        
        /// <summary>
        /// Time taken to perform the validation
        /// </summary>
        public TimeSpan ValidationDuration { get; set; }
        
        /// <summary>
        /// Additional metadata about the validation
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
    
    /// <summary>
    /// Represents a validation issue found during backup validation
    /// </summary>
    public class ValidationIssue
    {
        /// <summary>
        /// Type of validation issue
        /// </summary>
        public ValidationIssueType Type { get; set; }
        
        /// <summary>
        /// Description of the issue
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Severity level of the issue
        /// </summary>
        public ValidationSeverity Severity { get; set; }
        
        /// <summary>
        /// Location or context where the issue was found
        /// </summary>
        public string Location { get; set; } = string.Empty;
        
        /// <summary>
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
    /// Types of validation issues
    /// </summary>
    public enum ValidationIssueType
    {
        /// <summary>
        /// File system related issue
        /// </summary>
        FileSystem,
        
        /// <summary>
        /// Checksum or integrity issue
        /// </summary>
        Integrity,
        
        /// <summary>
        /// Compression related issue
        /// </summary>
        Compression,
        
        /// <summary>
        /// Encryption related issue
        /// </summary>
        Encryption,
        
        /// <summary>
        /// Database structure issue
        /// </summary>
        Database,
        
        /// <summary>
        /// Performance or size issue
        /// </summary>
        Performance,
        
        /// <summary>
        /// Configuration issue
        /// </summary>
        Configuration,
        
        /// <summary>
        /// Security related issue
        /// </summary>
        Security
    }
    
    /// <summary>
    /// Severity levels for validation issues
    /// </summary>
    public enum ValidationSeverity
    {
        /// <summary>
        /// Informational message
        /// </summary>
        Info,
        
        /// <summary>
        /// Warning that should be addressed
        /// </summary>
        Warning,
        
        /// <summary>
        /// Error that prevents proper backup usage
        /// </summary>
        Error,
        
        /// <summary>
        /// Critical error that indicates backup corruption
        /// </summary>
        Critical
    }
    
    /// <summary>
    /// Overall validation status
    /// </summary>
    public enum ValidationStatus
    {
        /// <summary>
        /// Validation passed with no issues
        /// </summary>
        Passed,
        
        /// <summary>
        /// Validation passed with warnings
        /// </summary>
        PassedWithWarnings,
        
        /// <summary>
        /// Validation failed with errors
        /// </summary>
        Failed,
        
        /// <summary>
        /// Validation could not be completed
        /// </summary>
        Incomplete
    }
    
    /// <summary>
    /// Supported checksum algorithms
    /// </summary>
    public enum ChecksumAlgorithm
    {
        /// <summary>
        /// MD5 hash algorithm (fast but less secure)
        /// </summary>
        MD5,
        
        /// <summary>
        /// SHA-1 hash algorithm
        /// </summary>
        SHA1,
        
        /// <summary>
        /// SHA-256 hash algorithm (recommended)
        /// </summary>
        SHA256,
        
        /// <summary>
        /// SHA-512 hash algorithm (most secure)
        /// </summary>
        SHA512
    }
}