using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Interface for client-side file transfer operations
/// </summary>
public interface IFileTransferClient
{
    /// <summary>
    /// Transfers a file to a remote server
    /// </summary>
    /// <param name="filePath">Path to the file to transfer</param>
    /// <param name="config">Transfer configuration settings</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result of the transfer operation</returns>
    Task<TransferResult> TransferFileAsync(string filePath, TransferConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes an interrupted file transfer
    /// </summary>
    /// <param name="resumeToken">Token identifying the interrupted transfer</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result of the resumed transfer operation</returns>
    Task<TransferResult> ResumeTransferAsync(string resumeToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes an interrupted file transfer with full context
    /// </summary>
    /// <param name="resumeToken">Token identifying the interrupted transfer</param>
    /// <param name="filePath">Path to the file to transfer</param>
    /// <param name="config">Transfer configuration settings</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Result of the resumed transfer operation</returns>
    Task<TransferResult> ResumeTransferAsync(string resumeToken, string filePath, TransferConfig config, CancellationToken cancellationToken = default);
}