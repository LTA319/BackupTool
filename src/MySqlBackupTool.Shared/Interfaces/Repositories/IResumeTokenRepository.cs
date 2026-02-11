using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 管理恢复令牌的存储库接口 / Repository interface for managing resume tokens
/// 提供恢复令牌的存储、查询、更新和清理功能，支持文件传输的断点续传
/// Provides storage, querying, updating and cleanup functionality for resume tokens, supporting file transfer resumption
/// </summary>
public interface IResumeTokenRepository : IRepository<ResumeToken>
{
    /// <summary>
    /// 根据令牌值获取恢复令牌 / Gets a resume token by its token value
    /// 通过令牌字符串查找对应的恢复令牌实体
    /// Finds corresponding resume token entity by token string
    /// </summary>
    /// <param name="token">令牌值 / Token value</param>
    /// <returns>恢复令牌实体，如果未找到则返回null / Resume token entity or null if not found</returns>
    Task<ResumeToken?> GetByTokenAsync(string token);

    /// <summary>
    /// 根据传输ID获取恢复令牌 / Gets a resume token by transfer ID
    /// 通过传输操作的唯一标识符查找对应的恢复令牌
    /// Finds corresponding resume token by unique identifier of transfer operation
    /// </summary>
    /// <param name="transferId">传输ID / Transfer ID</param>
    /// <returns>恢复令牌实体，如果未找到则返回null / Resume token entity or null if not found</returns>
    Task<ResumeToken?> GetByTransferIdAsync(string transferId);

    /// <summary>
    /// 获取所有活跃（未完成）的恢复令牌 / Gets all active (incomplete) resume tokens
    /// 返回所有正在进行中或可以继续的传输令牌
    /// Returns all ongoing or resumable transfer tokens
    /// </summary>
    /// <returns>活跃恢复令牌列表 / List of active resume tokens</returns>
    Task<List<ResumeToken>> GetActiveTokensAsync();

    /// <summary>
    /// 获取超过指定时间的恢复令牌 / Gets resume tokens older than the specified age
    /// 查找创建时间超过指定时长的令牌，通常用于清理过期令牌
    /// Finds tokens created longer than specified duration, typically used for cleaning expired tokens
    /// </summary>
    /// <param name="maxAge">令牌保留的最大时间 / Maximum age of tokens to keep</param>
    /// <returns>过期恢复令牌列表 / List of old resume tokens</returns>
    Task<List<ResumeToken>> GetExpiredTokensAsync(TimeSpan maxAge);

    /// <summary>
    /// 将恢复令牌标记为已完成 / Marks a resume token as completed
    /// 更新令牌状态为完成，表示传输操作已成功结束
    /// Updates token status to completed, indicating transfer operation has successfully ended
    /// </summary>
    /// <param name="token">令牌值 / Token value</param>
    Task MarkCompletedAsync(string token);

    /// <summary>
    /// 更新恢复令牌的最后活动时间 / Updates the last activity time for a resume token
    /// 记录令牌的最新使用时间，用于跟踪传输活动状态
    /// Records latest usage time of token for tracking transfer activity status
    /// </summary>
    /// <param name="token">令牌值 / Token value</param>
    Task UpdateLastActivityAsync(string token);

    /// <summary>
    /// 向恢复令牌添加已完成的分块 / Adds a completed chunk to a resume token
    /// 记录已成功传输的文件分块信息，包括索引、大小和校验和
    /// Records successfully transferred file chunk information including index, size and checksum
    /// </summary>
    /// <param name="token">令牌值 / Token value</param>
    /// <param name="chunkIndex">分块索引 / Chunk index</param>
    /// <param name="chunkSize">分块大小 / Chunk size</param>
    /// <param name="chunkChecksum">分块校验和，可选 / Chunk checksum, optional</param>
    Task AddCompletedChunkAsync(string token, int chunkIndex, long chunkSize, string? chunkChecksum = null);

    /// <summary>
    /// 获取恢复令牌的已完成分块 / Gets completed chunks for a resume token
    /// 返回指定令牌已成功传输的所有分块索引列表
    /// Returns list of all chunk indices successfully transferred for specified token
    /// </summary>
    /// <param name="token">令牌值 / Token value</param>
    /// <returns>已完成分块索引列表 / List of completed chunk indices</returns>
    Task<List<int>> GetCompletedChunksAsync(string token);

    /// <summary>
    /// 清理已完成的恢复令牌及其关联数据 / Cleans up completed resume tokens and their associated data
    /// 删除超过指定时间的已完成令牌，释放存储空间
    /// Deletes completed tokens older than specified time to free storage space
    /// </summary>
    /// <param name="maxAge">已完成令牌保留的最大时间 / Maximum age of completed tokens to keep</param>
    /// <returns>清理的令牌数量 / Number of tokens cleaned up</returns>
    Task<int> CleanupCompletedTokensAsync(TimeSpan maxAge);
}