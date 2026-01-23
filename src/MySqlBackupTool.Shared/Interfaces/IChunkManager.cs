using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Interface for managing file chunks during large file transfers
/// </summary>
public interface IChunkManager
{
    /// <summary>
    /// Initializes a new file transfer session
    /// </summary>
    /// <param name="metadata">File metadata</param>
    /// <returns>Transfer ID for the session</returns>
    Task<string> InitializeTransferAsync(FileMetadata metadata);

    /// <summary>
    /// Receives and processes a file chunk
    /// </summary>
    /// <param name="transferId">Transfer session ID</param>
    /// <param name="chunk">Chunk data to process</param>
    /// <returns>Result of chunk processing</returns>
    Task<ChunkResult> ReceiveChunkAsync(string transferId, ChunkData chunk);

    /// <summary>
    /// Finalizes a file transfer by reassembling all chunks
    /// </summary>
    /// <param name="transferId">Transfer session ID</param>
    /// <returns>Path to the finalized file</returns>
    Task<string> FinalizeTransferAsync(string transferId);

    /// <summary>
    /// Gets information needed to resume an interrupted transfer
    /// </summary>
    /// <param name="resumeToken">Resume token identifying the transfer</param>
    /// <returns>Resume information</returns>
    Task<ResumeInfo> GetResumeInfoAsync(string resumeToken);

    /// <summary>
    /// Creates a resume token for an interrupted transfer
    /// </summary>
    /// <param name="transferId">Transfer session ID</param>
    /// <returns>Resume token</returns>
    Task<string> CreateResumeTokenAsync(string transferId);

    /// <summary>
    /// Cleans up resume token data after successful completion
    /// </summary>
    /// <param name="resumeToken">Resume token to clean up</param>
    Task CleanupResumeTokenAsync(string resumeToken);

    /// <summary>
    /// Restores a transfer session from a resume token
    /// </summary>
    /// <param name="resumeToken">Resume token</param>
    /// <param name="metadata">File metadata</param>
    /// <returns>Transfer ID for the restored session</returns>
    Task<string> RestoreTransferAsync(string resumeToken, FileMetadata metadata);
}