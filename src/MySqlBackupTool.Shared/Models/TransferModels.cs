using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// Configuration for file transfer operations
/// </summary>
public class TransferConfig
{
    [Required]
    public ServerEndpoint TargetServer { get; set; } = new();

    [Required]
    [StringLength(500)]
    public string TargetDirectory { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string FileName { get; set; } = string.Empty;

    public ChunkingStrategy ChunkingStrategy { get; set; } = new();

    public int TimeoutSeconds { get; set; } = 300; // 5 minutes default

    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// Request for file transfer
/// </summary>
public class TransferRequest
{
    [Required]
    public string TransferId { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public FileMetadata Metadata { get; set; } = new();

    public ChunkingStrategy ChunkingStrategy { get; set; } = new();

    public bool ResumeTransfer { get; set; } = false;

    public string? ResumeToken { get; set; }

    /// <summary>
    /// Authentication token for the transfer request
    /// </summary>
    [Required]
    public string AuthenticationToken { get; set; } = string.Empty;

    /// <summary>
    /// Client ID making the request
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;
}

/// <summary>
/// Metadata about a file being transferred
/// </summary>
public class FileMetadata
{
    [Required]
    [StringLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Range(0, long.MaxValue)]
    public long FileSize { get; set; }

    [StringLength(32)]
    public string ChecksumMD5 { get; set; } = string.Empty;

    [StringLength(64)]
    public string ChecksumSHA256 { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public BackupConfiguration? SourceConfig { get; set; }
}

/// <summary>
/// Strategy for chunking large files
/// </summary>
public class ChunkingStrategy
{
    [Range(1024, long.MaxValue)] // Minimum 1KB chunks
    public long ChunkSize { get; set; } = 50 * 1024 * 1024; // 50MB default

    [Range(1, 10)]
    public int MaxConcurrentChunks { get; set; } = 4;

    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Calculates the total number of chunks needed for a file
    /// </summary>
    public int CalculateChunkCount(long fileSize)
    {
        return (int)Math.Ceiling((double)fileSize / ChunkSize);
    }
}

/// <summary>
/// Data for a single file chunk
/// </summary>
public class ChunkData
{
    [Required]
    public string TransferId { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int ChunkIndex { get; set; }

    [Required]
    public byte[] Data { get; set; } = Array.Empty<byte>();

    [StringLength(32)]
    public string ChunkChecksum { get; set; } = string.Empty;

    public bool IsLastChunk { get; set; } = false;
}

/// <summary>
/// Result of a file transfer operation
/// </summary>
public class TransferResult
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public long BytesTransferred { get; set; }

    public TimeSpan Duration { get; set; }

    public string? ResumeToken { get; set; }

    public string? ChecksumHash { get; set; }

    public double TransferRate => Duration.TotalSeconds > 0 ? BytesTransferred / Duration.TotalSeconds : 0;
}

/// <summary>
/// Response from server for transfer operations
/// </summary>
public class TransferResponse
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public string? AdditionalInfo { get; set; }
}

/// <summary>
/// Request for receiving a file
/// </summary>
public class ReceiveRequest
{
    [Required]
    public string TransferId { get; set; } = string.Empty;

    [Required]
    public FileMetadata Metadata { get; set; } = new();

    [Required]
    [StringLength(500)]
    public string TargetPath { get; set; } = string.Empty;

    /// <summary>
    /// Authentication token for the receive request
    /// </summary>
    [Required]
    public string AuthenticationToken { get; set; } = string.Empty;

    /// <summary>
    /// Client ID making the request
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;
}

/// <summary>
/// Result of a file reception operation
/// </summary>
public class ReceiveResult
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    [StringLength(500)]
    public string? FilePath { get; set; }

    public long BytesReceived { get; set; }

    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Result of processing a file chunk
/// </summary>
public class ChunkResult
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public int ChunkIndex { get; set; }

    public bool IsComplete { get; set; } = false;
}

/// <summary>
/// Information needed to resume an interrupted transfer
/// </summary>
public class ResumeInfo
{
    [Required]
    public string TransferId { get; set; } = string.Empty;

    [Required]
    public FileMetadata Metadata { get; set; } = new();

    public int LastCompletedChunk { get; set; } = -1;

    public List<int> CompletedChunks { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}