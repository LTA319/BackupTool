using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 文件传输服务操作的接口 / Interface for file transfer service operations
/// 提供高级文件传输服务，包括文件传输、断点续传等功能的统一服务接口
/// Provides high-level file transfer services, including unified service interface for file transfer and resume functionality
/// </summary>
public interface IFileTransferService
{
    /// <summary>
    /// 将文件传输到远程服务器 / Transfers a file to a remote server
    /// 通过服务层封装的文件传输功能，提供更高级的传输管理和错误处理
    /// Provides higher-level transfer management and error handling through service layer encapsulated file transfer functionality
    /// </summary>
    /// <param name="filePath">要传输的文件路径 / Path to the file to transfer</param>
    /// <param name="config">传输配置设置 / Transfer configuration settings</param>
    /// <param name="cancellationToken">操作的取消令牌 / Cancellation token for the operation</param>
    /// <returns>传输操作的结果 / Result of the transfer operation</returns>
    /// <exception cref="FileNotFoundException">当文件不存在时抛出 / Thrown when file does not exist</exception>
    /// <exception cref="UnauthorizedAccessException">当没有文件访问权限时抛出 / Thrown when file access is denied</exception>
    /// <exception cref="TransferException">当传输操作失败时抛出 / Thrown when transfer operation fails</exception>
    Task<TransferResult> TransferFileAsync(string filePath, TransferConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// 恢复中断的文件传输 / Resumes an interrupted file transfer
    /// 使用恢复令牌恢复之前中断的文件传输，提供服务级别的恢复管理
    /// Uses resume token to resume previously interrupted file transfer, provides service-level resume management
    /// </summary>
    /// <param name="resumeToken">标识中断传输的令牌 / Token identifying the interrupted transfer</param>
    /// <param name="cancellationToken">操作的取消令牌 / Cancellation token for the operation</param>
    /// <returns>恢复传输操作的结果 / Result of the resumed transfer operation</returns>
    /// <exception cref="ArgumentException">当恢复令牌无效时抛出 / Thrown when resume token is invalid</exception>
    /// <exception cref="TransferException">当恢复传输失败时抛出 / Thrown when resume transfer fails</exception>
    Task<TransferResult> ResumeTransferAsync(string resumeToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用完整上下文恢复中断的文件传输 / Resumes an interrupted file transfer with full context
    /// 提供完整的传输上下文来恢复中断的传输，包括文件路径、配置和服务级别的状态管理
    /// Provides complete transfer context to resume interrupted transfer, including file path, configuration and service-level state management
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