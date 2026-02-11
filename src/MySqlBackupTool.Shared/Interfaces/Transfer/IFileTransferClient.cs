using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 客户端文件传输操作的接口 / Interface for client-side file transfer operations
/// 提供文件传输、断点续传等功能，用于客户端向服务器传输文件
/// Provides file transfer and resume functionality for client-side file transmission to server
/// </summary>
public interface IFileTransferClient
{
    /// <summary>
    /// 将文件传输到远程服务器 / Transfers a file to a remote server
    /// 建立与服务器的连接，将指定文件传输到远程服务器，支持进度报告和错误处理
    /// Establishes connection to server, transfers specified file to remote server with progress reporting and error handling
    /// </summary>
    /// <param name="filePath">要传输的文件路径 / Path to the file to transfer</param>
    /// <param name="config">传输配置设置 / Transfer configuration settings</param>
    /// <param name="cancellationToken">操作的取消令牌 / Cancellation token for the operation</param>
    /// <returns>传输操作的结果 / Result of the transfer operation</returns>
    /// <exception cref="FileNotFoundException">当文件不存在时抛出 / Thrown when file does not exist</exception>
    /// <exception cref="UnauthorizedAccessException">当没有文件读取权限时抛出 / Thrown when file read access is denied</exception>
    /// <exception cref="NetworkException">当网络连接失败时抛出 / Thrown when network connection fails</exception>
    Task<TransferResult> TransferFileAsync(string filePath, TransferConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// 恢复中断的文件传输 / Resumes an interrupted file transfer
    /// 使用恢复令牌继续之前中断的文件传输，从中断点开始继续传输
    /// Uses resume token to continue previously interrupted file transfer, continuing from interruption point
    /// </summary>
    /// <param name="resumeToken">标识中断传输的令牌 / Token identifying the interrupted transfer</param>
    /// <param name="cancellationToken">操作的取消令牌 / Cancellation token for the operation</param>
    /// <returns>恢复传输操作的结果 / Result of the resumed transfer operation</returns>
    /// <exception cref="ArgumentException">当恢复令牌无效时抛出 / Thrown when resume token is invalid</exception>
    /// <exception cref="TransferException">当恢复传输失败时抛出 / Thrown when resume transfer fails</exception>
    Task<TransferResult> ResumeTransferAsync(string resumeToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用完整上下文恢复中断的文件传输 / Resumes an interrupted file transfer with full context
    /// 提供完整的传输上下文信息来恢复中断的传输，包括文件路径和配置信息
    /// Provides complete transfer context information to resume interrupted transfer, including file path and configuration
    /// </summary>
    /// <param name="resumeToken">标识中断传输的令牌 / Token identifying the interrupted transfer</param>
    /// <param name="filePath">要传输的文件路径 / Path to the file to transfer</param>
    /// <param name="config">传输配置设置 / Transfer configuration settings</param>
    /// <param name="cancellationToken">操作的取消令牌 / Cancellation token for the operation</param>
    /// <returns>恢复传输操作的结果 / Result of the resumed transfer operation</returns>
    /// <exception cref="ArgumentException">当参数无效时抛出 / Thrown when parameters are invalid</exception>
    /// <exception cref="FileNotFoundException">当文件不存在时抛出 / Thrown when file does not exist</exception>
    /// <exception cref="TransferException">当恢复传输失败时抛出 / Thrown when resume transfer fails</exception>
    Task<TransferResult> ResumeTransferAsync(string resumeToken, string filePath, TransferConfig config, CancellationToken cancellationToken = default);
}