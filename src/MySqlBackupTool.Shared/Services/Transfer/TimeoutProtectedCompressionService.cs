using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// ICompressionService的装饰器，为所有操作添加超时保护 / Decorator for ICompressionService that adds timeout protection to all operations
/// 使用错误恢复管理器提供超时检测和恢复机制 / Uses error recovery manager to provide timeout detection and recovery mechanisms
/// </summary>
public class TimeoutProtectedCompressionService : ICompressionService
{
    private readonly ICompressionService _innerService; // 内部压缩服务 / Inner compression service
    private readonly IErrorRecoveryManager _errorRecoveryManager; // 错误恢复管理器 / Error recovery manager
    private readonly ILogger<TimeoutProtectedCompressionService> _logger;

    /// <summary>
    /// 构造函数，初始化超时保护压缩服务 / Constructor, initializes timeout-protected compression service
    /// </summary>
    /// <param name="innerService">内部压缩服务实现 / Inner compression service implementation</param>
    /// <param name="errorRecoveryManager">错误恢复管理器 / Error recovery manager</param>
    /// <param name="logger">日志服务 / Logger service</param>
    public TimeoutProtectedCompressionService(
        ICompressionService innerService,
        IErrorRecoveryManager errorRecoveryManager,
        ILogger<TimeoutProtectedCompressionService> logger)
    {
        _innerService = innerService ?? throw new ArgumentNullException(nameof(innerService));
        _errorRecoveryManager = errorRecoveryManager ?? throw new ArgumentNullException(nameof(errorRecoveryManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 带超时保护的目录压缩操作 / Directory compression operation with timeout protection
    /// 使用错误恢复管理器执行压缩操作，提供超时检测和异常处理 / Uses error recovery manager to execute compression with timeout detection and exception handling
    /// </summary>
    /// <param name="sourcePath">源目录路径 / Source directory path</param>
    /// <param name="targetPath">目标文件路径 / Target file path</param>
    /// <param name="progress">进度报告器（可选） / Progress reporter (optional)</param>
    /// <returns>压缩后的文件路径 / Path of compressed file</returns>
    /// <exception cref="CompressionException">当压缩操作超时或失败时抛出 / Thrown when compression operation times out or fails</exception>
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

    /// <summary>
    /// 带超时保护的文件清理操作 / File cleanup operation with timeout protection
    /// 使用较短的超时时间执行清理操作，失败时不抛出异常只记录日志 / Uses shorter timeout for cleanup operation, logs errors without throwing exceptions on failure
    /// </summary>
    /// <param name="filePath">要清理的文件路径 / Path of file to cleanup</param>
    /// <returns>异步任务 / Async task</returns>
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