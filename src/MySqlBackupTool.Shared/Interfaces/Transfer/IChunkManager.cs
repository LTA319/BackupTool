using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 在大文件传输过程中管理文件分块的接口
/// Interface for managing file chunks during large file transfers
/// </summary>
public interface IChunkManager
{
    /// <summary>
    /// 初始化新的文件传输会话
    /// Initializes a new file transfer session
    /// </summary>
    /// <param name="metadata">文件元数据 / File metadata</param>
    /// <returns>会话的传输ID / Transfer ID for the session</returns>
    Task<string> InitializeTransferAsync(FileMetadata metadata);

    /// <summary>
    /// 接收并处理文件分块
    /// Receives and processes a file chunk
    /// </summary>
    /// <param name="transferId">传输会话ID / Transfer session ID</param>
    /// <param name="chunk">要处理的分块数据 / Chunk data to process</param>
    /// <returns>分块处理结果 / Result of chunk processing</returns>
    Task<ChunkResult> ReceiveChunkAsync(string transferId, ChunkData chunk);

    /// <summary>
    /// 通过重新组装所有分块来完成文件传输
    /// Finalizes a file transfer by reassembling all chunks
    /// </summary>
    /// <param name="transferId">传输会话ID / Transfer session ID</param>
    /// <param name="targetPath">最终文件的目标路径（可选，如果未提供则使用临时目录） / Target path for the final file (optional, uses temp directory if not provided)</param>
    /// <returns>最终文件的路径 / Path to the finalized file</returns>
    Task<string> FinalizeTransferAsync(string transferId, string? targetPath = null);

    /// <summary>
    /// 获取恢复中断传输所需的信息
    /// Gets information needed to resume an interrupted transfer
    /// </summary>
    /// <param name="resumeToken">标识传输的恢复令牌 / Resume token identifying the transfer</param>
    /// <returns>恢复信息 / Resume information</returns>
    Task<ResumeInfo> GetResumeInfoAsync(string resumeToken);

    /// <summary>
    /// 为中断的传输创建恢复令牌
    /// Creates a resume token for an interrupted transfer
    /// </summary>
    /// <param name="transferId">传输会话ID / Transfer session ID</param>
    /// <returns>恢复令牌 / Resume token</returns>
    Task<string> CreateResumeTokenAsync(string transferId);

    /// <summary>
    /// 在成功完成后清理恢复令牌数据
    /// Cleans up resume token data after successful completion
    /// </summary>
    /// <param name="resumeToken">要清理的恢复令牌 / Resume token to clean up</param>
    Task CleanupResumeTokenAsync(string resumeToken);

    /// <summary>
    /// 从恢复令牌恢复传输会话
    /// Restores a transfer session from a resume token
    /// </summary>
    /// <param name="resumeToken">恢复令牌 / Resume token</param>
    /// <param name="metadata">文件元数据 / File metadata</param>
    /// <returns>恢复会话的传输ID / Transfer ID for the restored session</returns>
    Task<string> RestoreTransferAsync(string resumeToken, FileMetadata metadata);
}