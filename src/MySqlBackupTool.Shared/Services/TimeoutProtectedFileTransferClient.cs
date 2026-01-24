using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Decorator for IFileTransferClient that adds timeout protection to all operations
/// </summary>
public class TimeoutProtectedFileTransferClient : IFileTransferClient, IFileTransferService
{
    private readonly IFileTransferClient _innerClient;
    private readonly IErrorRecoveryManager _errorRecoveryManager;
    private readonly ILogger<TimeoutProtectedFileTransferClient> _logger;

    public TimeoutProtectedFileTransferClient(
        IFileTransferClient innerClient,
        IErrorRecoveryManager errorRecoveryManager,
        ILogger<TimeoutProtectedFileTransferClient> logger)
    {
        _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
        _errorRecoveryManager = errorRecoveryManager ?? throw new ArgumentNullException(nameof(errorRecoveryManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

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