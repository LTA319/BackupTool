using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Repository interface for managing resume tokens
/// </summary>
public interface IResumeTokenRepository : IRepository<ResumeToken>
{
    /// <summary>
    /// Gets a resume token by its token value
    /// </summary>
    /// <param name="token">Token value</param>
    /// <returns>Resume token entity or null if not found</returns>
    Task<ResumeToken?> GetByTokenAsync(string token);

    /// <summary>
    /// Gets a resume token by transfer ID
    /// </summary>
    /// <param name="transferId">Transfer ID</param>
    /// <returns>Resume token entity or null if not found</returns>
    Task<ResumeToken?> GetByTransferIdAsync(string transferId);

    /// <summary>
    /// Gets all active (incomplete) resume tokens
    /// </summary>
    /// <returns>List of active resume tokens</returns>
    Task<List<ResumeToken>> GetActiveTokensAsync();

    /// <summary>
    /// Gets resume tokens older than the specified age
    /// </summary>
    /// <param name="maxAge">Maximum age of tokens to keep</param>
    /// <returns>List of old resume tokens</returns>
    Task<List<ResumeToken>> GetExpiredTokensAsync(TimeSpan maxAge);

    /// <summary>
    /// Marks a resume token as completed
    /// </summary>
    /// <param name="token">Token value</param>
    Task MarkCompletedAsync(string token);

    /// <summary>
    /// Updates the last activity time for a resume token
    /// </summary>
    /// <param name="token">Token value</param>
    Task UpdateLastActivityAsync(string token);

    /// <summary>
    /// Adds a completed chunk to a resume token
    /// </summary>
    /// <param name="token">Token value</param>
    /// <param name="chunkIndex">Chunk index</param>
    /// <param name="chunkSize">Chunk size</param>
    /// <param name="chunkChecksum">Chunk checksum</param>
    Task AddCompletedChunkAsync(string token, int chunkIndex, long chunkSize, string? chunkChecksum = null);

    /// <summary>
    /// Gets completed chunks for a resume token
    /// </summary>
    /// <param name="token">Token value</param>
    /// <returns>List of completed chunk indices</returns>
    Task<List<int>> GetCompletedChunksAsync(string token);

    /// <summary>
    /// Cleans up completed resume tokens and their associated data
    /// </summary>
    /// <param name="maxAge">Maximum age of completed tokens to keep</param>
    /// <returns>Number of tokens cleaned up</returns>
    Task<int> CleanupCompletedTokensAsync(TimeSpan maxAge);
}