using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// IFileTransferClient的装饰器，为所有操作添加超时保护 / Decorator for IFileTransferClient that adds timeout protection to all operations
/// 使用错误恢复管理器提供文件传输的超时检测和恢复机制 / Uses error recovery manager to provide timeout detection and recovery mechanisms for file transfers
/// </summary>
public class TimeoutProtectedFileTransferClient : IFileTransferClient, IFileTransferService
{
    private readonly IFileTransferClient _innerClient; // 内部文件传输客户端 / Inner file transfer client
    private readonly IErrorRecoveryManager _errorRecoveryManager; // 错误恢复管理器 / Error recovery manager
    private readonly ILogger<TimeoutProtectedFileTransferClient> _logger;

    /// <summary>
    /// 构造函数，初始化超时保护文件传输客户端 / Constructor, initializes timeout-protected file transfer client
    /// </summary>
    /// <param name="innerClient">内部文件传输客户端实现 / Inner file transfer client implementation</param>
    /// <param name="errorRecoveryManager">错误恢复管理器 / Error recovery manager</param>
    /// <param name="logger">日志服务 / Logger service</param>
    public TimeoutProtectedFileTransferClient(
        IFileTransferClient innerClient,
        IErrorRecoveryManager errorRecoveryManager,
        ILogger<TimeoutProtectedFileTransferClient> logger)
    {
        _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
        _errorRecoveryManager = errorRecoveryManager ?? throw new ArgumentNullException(nameof(errorRecoveryManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 带超时保护的文件传输操作 / File transfer operation with timeout protection
    /// 使用错误恢复管理器执行文件传输，提供超时检测和错误处理 / Uses error recovery manager to execute file transfer with timeout detection and error handling
    /// </summary>
    /// <param name="filePath">要传输的文件路径 / Path of file to transfer</param>
    /// <param name="config">传输配置 / Transfer configuration</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>传输结果 / Transfer result</returns>
    public async Task<TransferResult> TransferFileAsync(string filePath, TransferConfig config, CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid().ToString();
        
        try
        {
            _logger.LogDebug("Starting timeout-protected file transfer for {FilePath}", filePath);

            return await _errorRecoveryManager.ExecuteWithTimeoutAsync(
                async (ct) => await _innerClient.TransferFileAsync(filePath, config, ct),
                _errorRecoveryManager.Configuration.TransferTimeout,
                "File Transfer",
                operationId,
                cancellationToken);
        }
        catch (OperationTimeoutException ex)
        {
            _logger.LogError(ex, "File transfer timed out for {FilePath}", filePath);
            
            var transferException = new TransferException(operationId, filePath, 
                $"File transfer timed out after {ex.ActualDuration.TotalSeconds:F1} seconds", ex)
            {
                RemoteEndpoint = $"{config.TargetServer.IPAddress}:{config.TargetServer.Port}"
            };
            
            var recoveryResult = await _errorRecoveryManager.HandleTransferFailureAsync(transferException, cancellationToken);
            
            var result = new TransferResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                BytesTransferred = 0
            };

            if (recoveryResult.Success && recoveryResult.StrategyUsed == RecoveryStrategy.Retry)
            {
                result.ErrorMessage += " - Transfer can be resumed";
            }
            
            return result;
        }
        catch (Exception ex) when (!(ex is TransferException))
        {
            _logger.LogError(ex, "Unexpected error during timeout-protected file transfer for {FilePath}", filePath);
            
            var transferException = new TransferException(operationId, filePath, 
                "Unexpected error during file transfer", ex)
            {
                RemoteEndpoint = $"{config.TargetServer.IPAddress}:{config.TargetServer.Port}"
            };
            
            await _errorRecoveryManager.HandleTransferFailureAsync(transferException, cancellationToken);
            
            return new TransferResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                BytesTransferred = 0
            };
        }
    }

    /// <summary>
    /// 带超时保护的传输恢复操作 / Transfer resume operation with timeout protection
    /// 使用恢复令牌恢复中断的传输，提供超时检测 / Resumes interrupted transfer using resume token with timeout detection
    /// </summary>
    /// <param name="resumeToken">恢复令牌 / Resume token</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>传输结果 / Transfer result</returns>
    public async Task<TransferResult> ResumeTransferAsync(string resumeToken, CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid().ToString();
        
        try
        {
            _logger.LogDebug("Starting timeout-protected resume transfer with token {ResumeToken}", resumeToken);

            return await _errorRecoveryManager.ExecuteWithTimeoutAsync(
                async (ct) => await _innerClient.ResumeTransferAsync(resumeToken, ct),
                _errorRecoveryManager.Configuration.TransferTimeout,
                "Resume Transfer",
                operationId,
                cancellationToken);
        }
        catch (OperationTimeoutException ex)
        {
            _logger.LogError(ex, "Resume transfer timed out for token {ResumeToken}", resumeToken);
            
            var transferException = new TransferException(operationId, "Unknown", 
                $"Resume transfer timed out after {ex.ActualDuration.TotalSeconds:F1} seconds", ex)
            {
                ResumeToken = resumeToken
            };
            
            await _errorRecoveryManager.HandleTransferFailureAsync(transferException, cancellationToken);
            
            return new TransferResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                BytesTransferred = 0
            };
        }
        catch (Exception ex) when (!(ex is TransferException))
        {
            _logger.LogError(ex, "Unexpected error during timeout-protected resume transfer for token {ResumeToken}", resumeToken);
            
            var transferException = new TransferException(operationId, "Unknown", 
                "Unexpected error during resume transfer", ex)
            {
                ResumeToken = resumeToken
            };
            
            await _errorRecoveryManager.HandleTransferFailureAsync(transferException, cancellationToken);
            
            return new TransferResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                BytesTransferred = 0
            };
        }
    }

    /// <summary>
    /// 带超时保护的上下文传输恢复操作 / Transfer resume operation with context and timeout protection
    /// 使用完整上下文恢复中断的传输，提供超时检测和错误处理 / Resumes interrupted transfer with full context, provides timeout detection and error handling
    /// </summary>
    /// <param name="resumeToken">恢复令牌 / Resume token</param>
    /// <param name="filePath">文件路径 / File path</param>
    /// <param name="config">传输配置 / Transfer configuration</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>传输结果 / Transfer result</returns>
    public async Task<TransferResult> ResumeTransferAsync(string resumeToken, string filePath, TransferConfig config, CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid().ToString();
        
        try
        {
            _logger.LogDebug("Starting timeout-protected resume transfer with token {ResumeToken} for file {FilePath}", 
                resumeToken, filePath);

            return await _errorRecoveryManager.ExecuteWithTimeoutAsync(
                async (ct) => await _innerClient.ResumeTransferAsync(resumeToken, filePath, config, ct),
                _errorRecoveryManager.Configuration.TransferTimeout,
                "Resume Transfer with Context",
                operationId,
                cancellationToken);
        }
        catch (OperationTimeoutException ex)
        {
            _logger.LogError(ex, "Resume transfer with context timed out for token {ResumeToken}, file {FilePath}", 
                resumeToken, filePath);
            
            var transferException = new TransferException(operationId, filePath, 
                $"Resume transfer timed out after {ex.ActualDuration.TotalSeconds:F1} seconds", ex)
            {
                ResumeToken = resumeToken,
                RemoteEndpoint = $"{config.TargetServer.IPAddress}:{config.TargetServer.Port}"
            };
            
            await _errorRecoveryManager.HandleTransferFailureAsync(transferException, cancellationToken);
            
            return new TransferResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                BytesTransferred = 0
            };
        }
        catch (Exception ex) when (!(ex is TransferException))
        {
            _logger.LogError(ex, "Unexpected error during timeout-protected resume transfer for token {ResumeToken}, file {FilePath}", 
                resumeToken, filePath);
            
            var transferException = new TransferException(operationId, filePath, 
                "Unexpected error during resume transfer", ex)
            {
                ResumeToken = resumeToken,
                RemoteEndpoint = $"{config.TargetServer.IPAddress}:{config.TargetServer.Port}"
            };
            
            await _errorRecoveryManager.HandleTransferFailureAsync(transferException, cancellationToken);
            
            return new TransferResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                BytesTransferred = 0
            };
        }
    }
}