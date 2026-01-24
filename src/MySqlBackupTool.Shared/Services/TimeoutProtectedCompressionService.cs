using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Decorator for ICompressionService that adds timeout protection to all operations
/// </summary>
public class TimeoutProtectedCompressionService : ICompressionService
{
    private readonly ICompressionService _innerService;
    private readonly IErrorRecoveryManager _errorRecoveryManager;
    private readonly ILogger<TimeoutProtectedCompressionService> _logger;

    public TimeoutProtectedCompressionService(
        ICompressionService innerService,
        IErrorRecoveryManager errorRecoveryManager,
        ILogger<TimeoutProtectedCompressionService> logger)
    {
        _innerService = innerService ?? throw new ArgumentNullException(nameof(innerService));
        _errorRecoveryManager = errorRecoveryManager ?? throw new ArgumentNullException(nameof(errorRecoveryManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> CompressDirectoryAsync(string sourcePath, string targetPath, IProgress<CompressionProgress>? progress = null)
    {
        var operationId = Guid.NewGuid().ToString();
        
        try
        {
            _logger.LogDebug("Starting timeout-protected compression operation from {SourcePath} to {TargetPath}", 
                sourcePath, targetPath);

            return await _errorRecoveryManager.ExecuteWithTimeoutAsync(
                async (cancellationToken) => await _innerService.CompressDirectoryAsync(sourcePath, targetPath, progress),
                _errorRecoveryManager.Configuration.CompressionTimeout,
                "Compression",
                operationId);
        }
        catch (OperationTimeoutException ex)
        {
            _logger.LogError(ex, "Compression operation timed out for source {SourcePath}", sourcePath);
            
            var compressionException = new CompressionException(operationId, sourcePath, 
                $"Compression operation timed out after {ex.ActualDuration.TotalSeconds:F1} seconds", ex)
            {
                TargetPath = targetPath
            };
            
            var recoveryResult = await _errorRecoveryManager.HandleCompressionFailureAsync(compressionException);
            
            if (!recoveryResult.Success)
            {
                _logger.LogError("Recovery failed for compression timeout: {Message}", recoveryResult.Message);
            }
            
            throw compressionException;
        }
        catch (Exception ex) when (!(ex is CompressionException))
        {
            _logger.LogError(ex, "Unexpected error during timeout-protected compression for source {SourcePath}", sourcePath);
            
            var compressionException = new CompressionException(operationId, sourcePath, 
                "Unexpected error during compression operation", ex)
            {
                TargetPath = targetPath
            };
            
            await _errorRecoveryManager.HandleCompressionFailureAsync(compressionException);
            throw compressionException;
        }
    }

    public async Task CleanupAsync(string filePath)
    {
        var operationId = Guid.NewGuid().ToString();
        
        try
        {
            _logger.LogDebug("Starting timeout-protected cleanup operation for file {FilePath}", filePath);

            // Cleanup operations should be relatively quick, use a shorter timeout
            var cleanupTimeout = TimeSpan.FromMinutes(5);

            await _errorRecoveryManager.ExecuteWithTimeoutAsync(
                async (cancellationToken) => await _innerService.CleanupAsync(filePath),
                cleanupTimeout,
                "Cleanup",
                operationId);
        }
        catch (OperationTimeoutException ex)
        {
            _logger.LogError(ex, "Cleanup operation timed out for file {FilePath}", filePath);
            
            // For cleanup timeouts, we don't want to throw - just log the issue
            _logger.LogWarning("Cleanup timeout for {FilePath} - file may need manual removal", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during timeout-protected cleanup for file {FilePath}", filePath);
            
            // For cleanup errors, we don't want to throw - just log the issue
            _logger.LogWarning("Cleanup failed for {FilePath} - file may need manual removal", filePath);
        }
    }
}