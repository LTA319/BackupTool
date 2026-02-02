using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Data.Repositories;

/// <summary>
/// Repository for managing resume tokens
/// </summary>
public class ResumeTokenRepository : Repository<ResumeToken>, IResumeTokenRepository
{
    private readonly ILogger<ResumeTokenRepository> _logger;

    public ResumeTokenRepository(BackupDbContext context, ILogger<ResumeTokenRepository> logger)
        : base(context)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets a resume token by its token value
    /// </summary>
    public async Task<ResumeToken?> GetByTokenAsync(string token)
    {
        try
        {
            return await _context.Set<ResumeToken>()
                .Include(rt => rt.CompletedChunks)
                .Include(rt => rt.BackupLog)
                .FirstOrDefaultAsync(rt => rt.Token == token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting resume token by token {Token}", token);
            throw;
        }
    }

    /// <summary>
    /// Gets a resume token by transfer ID
    /// </summary>
    public async Task<ResumeToken?> GetByTransferIdAsync(string transferId)
    {
        try
        {
            return await _context.Set<ResumeToken>()
                .Include(rt => rt.CompletedChunks)
                .Include(rt => rt.BackupLog)
                .FirstOrDefaultAsync(rt => rt.TransferId == transferId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting resume token by transfer ID {TransferId}", transferId);
            throw;
        }
    }

    /// <summary>
    /// Gets all active (incomplete) resume tokens
    /// </summary>
    public async Task<List<ResumeToken>> GetActiveTokensAsync()
    {
        try
        {
            return await _context.Set<ResumeToken>()
                .Include(rt => rt.CompletedChunks)
                .Where(rt => !rt.IsCompleted)
                .OrderBy(rt => rt.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active resume tokens");
            throw;
        }
    }

    /// <summary>
    /// Gets resume tokens older than the specified age
    /// </summary>
    public async Task<List<ResumeToken>> GetExpiredTokensAsync(TimeSpan maxAge)
    {
        try
        {
            var cutoffDate = DateTime.Now - maxAge;
            return await _context.Set<ResumeToken>()
                .Include(rt => rt.CompletedChunks)
                .Where(rt => rt.LastActivity < cutoffDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expired resume tokens");
            throw;
        }
    }

    /// <summary>
    /// Marks a resume token as completed
    /// </summary>
    public async Task MarkCompletedAsync(string token)
    {
        try
        {
            var resumeToken = await _context.Set<ResumeToken>()
                .FirstOrDefaultAsync(rt => rt.Token == token);

            if (resumeToken != null)
            {
                resumeToken.IsCompleted = true;
                resumeToken.LastActivity = DateTime.Now;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Marked resume token {Token} as completed", token);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking resume token {Token} as completed", token);
            throw;
        }
    }

    /// <summary>
    /// Updates the last activity time for a resume token
    /// </summary>
    public async Task UpdateLastActivityAsync(string token)
    {
        try
        {
            var resumeToken = await _context.Set<ResumeToken>()
                .FirstOrDefaultAsync(rt => rt.Token == token);

            if (resumeToken != null)
            {
                resumeToken.LastActivity = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating last activity for resume token {Token}", token);
            throw;
        }
    }

    /// <summary>
    /// Adds a completed chunk to a resume token
    /// </summary>
    public async Task AddCompletedChunkAsync(string token, int chunkIndex, long chunkSize, string? chunkChecksum = null)
    {
        try
        {
            var resumeToken = await _context.Set<ResumeToken>()
                .Include(rt => rt.CompletedChunks)
                .FirstOrDefaultAsync(rt => rt.Token == token);

            if (resumeToken != null)
            {
                // Check if chunk already exists
                var existingChunk = resumeToken.CompletedChunks
                    .FirstOrDefault(c => c.ChunkIndex == chunkIndex);

                if (existingChunk == null)
                {
                    var chunk = new ResumeChunk
                    {
                        ResumeTokenId = resumeToken.Id,
                        ChunkIndex = chunkIndex,
                        ChunkSize = chunkSize,
                        ChunkChecksum = chunkChecksum,
                        CompletedAt = DateTime.Now
                    };

                    resumeToken.CompletedChunks.Add(chunk);
                    resumeToken.LastActivity = DateTime.Now;
                    await _context.SaveChangesAsync();

                    _logger.LogDebug("Added completed chunk {ChunkIndex} to resume token {Token}", 
                        chunkIndex, token);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding completed chunk {ChunkIndex} to resume token {Token}", 
                chunkIndex, token);
            throw;
        }
    }

    /// <summary>
    /// Gets completed chunks for a resume token
    /// </summary>
    public async Task<List<int>> GetCompletedChunksAsync(string token)
    {
        try
        {
            var resumeToken = await _context.Set<ResumeToken>()
                .Include(rt => rt.CompletedChunks)
                .FirstOrDefaultAsync(rt => rt.Token == token);

            return resumeToken?.CompletedChunks
                .Select(c => c.ChunkIndex)
                .OrderBy(i => i)
                .ToList() ?? new List<int>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting completed chunks for resume token {Token}", token);
            throw;
        }
    }

    /// <summary>
    /// Cleans up completed resume tokens and their associated data
    /// </summary>
    public async Task<int> CleanupCompletedTokensAsync(TimeSpan maxAge)
    {
        try
        {
            var cutoffDate = DateTime.Now - maxAge;
            var tokensToDelete = await _context.Set<ResumeToken>()
                .Include(rt => rt.CompletedChunks)
                .Where(rt => rt.IsCompleted && rt.LastActivity < cutoffDate)
                .ToListAsync();

            if (tokensToDelete.Count > 0)
            {
                _context.Set<ResumeToken>().RemoveRange(tokensToDelete);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Cleaned up {Count} completed resume tokens", tokensToDelete.Count);
            }

            return tokensToDelete.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up completed resume tokens");
            throw;
        }
    }
}