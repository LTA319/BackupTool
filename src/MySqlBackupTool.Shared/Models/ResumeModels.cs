using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// Database entity for storing resume token information
/// </summary>
public class ResumeToken
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Token { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string TransferId { get; set; } = string.Empty;

    public int? BackupLogId { get; set; }

    [Required]
    [StringLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Range(0, long.MaxValue)]
    public long FileSize { get; set; }

    [StringLength(32)]
    public string? ChecksumMD5 { get; set; }

    [StringLength(64)]
    public string? ChecksumSHA256 { get; set; }

    [StringLength(500)]
    public string? TempDirectory { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime LastActivity { get; set; } = DateTime.UtcNow;

    public bool IsCompleted { get; set; } = false;

    // Navigation property
    public BackupLog? BackupLog { get; set; }

    // Collection of completed chunks
    public List<ResumeChunk> CompletedChunks { get; set; } = new();
}

/// <summary>
/// Database entity for tracking completed chunks in a resume session
/// </summary>
public class ResumeChunk
{
    public int Id { get; set; }

    public int ResumeTokenId { get; set; }

    [Range(0, int.MaxValue)]
    public int ChunkIndex { get; set; }

    [Range(0, long.MaxValue)]
    public long ChunkSize { get; set; }

    [StringLength(32)]
    public string? ChunkChecksum { get; set; }

    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ResumeToken? ResumeToken { get; set; }
}

/// <summary>
/// Transfer state information for persistence
/// </summary>
public class TransferState
{
    [Required]
    public string TransferId { get; set; } = string.Empty;

    [Required]
    public FileMetadata Metadata { get; set; } = new();

    public List<int> CompletedChunks { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime LastActivity { get; set; } = DateTime.UtcNow;

    [StringLength(500)]
    public string? TempDirectory { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets the last completed chunk index
    /// </summary>
    public int LastCompletedChunk => CompletedChunks.Count > 0 ? CompletedChunks.Max() : -1;

    /// <summary>
    /// Gets the total number of completed chunks
    /// </summary>
    public int CompletedChunkCount => CompletedChunks.Count;

    /// <summary>
    /// Calculates the completion percentage
    /// </summary>
    public double CalculateProgress(ChunkingStrategy strategy)
    {
        var totalChunks = strategy.CalculateChunkCount(Metadata.FileSize);
        return totalChunks > 0 ? (double)CompletedChunkCount / totalChunks : 0.0;
    }
}