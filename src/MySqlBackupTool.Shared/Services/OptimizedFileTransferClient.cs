using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 具有自适应性能调优的优化文件传输客户端 / Optimized file transfer client with adaptive performance tuning
/// 根据文件大小和网络条件自动优化传输配置 / Automatically optimizes transfer configuration based on file size and network conditions
/// </summary>
public class OptimizedFileTransferClient : IFileTransferClient
{
    private readonly ILogger<OptimizedFileTransferClient> _logger;
    private readonly IFileTransferClient _baseClient;
    private readonly IMemoryProfiler? _memoryProfiler;

    /// <summary>
    /// 初始化优化文件传输客户端 / Initialize optimized file transfer client
    /// </summary>
    /// <param name="logger">日志记录器 / Logger instance</param>
    /// <param name="baseClient">基础文件传输客户端 / Base file transfer client</param>
    /// <param name="memoryProfiler">内存分析器（可选） / Memory profiler (optional)</param>
    /// <exception cref="ArgumentNullException">当logger或baseClient为null时抛出 / Thrown when logger or baseClient is null</exception>
    public OptimizedFileTransferClient(
        ILogger<OptimizedFileTransferClient> logger,
        IFileTransferClient baseClient,
        IMemoryProfiler? memoryProfiler = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _baseClient = baseClient ?? throw new ArgumentNullException(nameof(baseClient));
        _memoryProfiler = memoryProfiler;
    }

    /// <summary>
    /// 基于文件大小和网络条件进行自适应优化的文件传输 / Transfers a file with adaptive optimization based on file size and network conditions
    /// 自动调整分块大小、并发数和超时时间以获得最佳性能 / Automatically adjusts chunk size, concurrency and timeout for optimal performance
    /// </summary>
    /// <param name="filePath">要传输的文件路径 / Path of file to transfer</param>
    /// <param name="config">传输配置 / Transfer configuration</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>传输结果 / Transfer result</returns>
    public async Task<TransferResult> TransferFileAsync(string filePath, TransferConfig config, CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow;
        
        _memoryProfiler?.StartProfiling(operationId, "OptimizedFileTransfer");
        _memoryProfiler?.RecordSnapshot(operationId, "Start", $"Starting optimized transfer: {filePath}");

        try
        {
            // 获取文件信息用于优化决策 / Get file info for optimization decisions
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                return new TransferResult
                {
                    Success = false,
                    ErrorMessage = $"File not found: {filePath}",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            _logger.LogInformation("Starting optimized file transfer for {FilePath} ({FileSize} bytes) to {Server}:{Port}",
                filePath, fileInfo.Length, config.TargetServer.IPAddress, config.TargetServer.Port);

            // 根据文件大小优化传输配置 / Optimize transfer configuration based on file size
            var optimizedConfig = OptimizeTransferConfig(config, fileInfo.Length);
            _memoryProfiler?.RecordSnapshot(operationId, "ConfigOptimized", 
                $"Optimized config: ChunkSize={optimizedConfig.ChunkingStrategy.ChunkSize}, MaxConcurrent={optimizedConfig.ChunkingStrategy.MaxConcurrentChunks}");

            // 使用优化设置执行传输 / Perform the transfer with optimized settings
            var result = await _baseClient.TransferFileAsync(filePath, optimizedConfig, cancellationToken);
            
            // 计算和记录性能指标 / Calculate and log performance metrics
            var duration = DateTime.UtcNow - startTime;
            var throughputMBps = result.BytesTransferred > 0 && duration.TotalSeconds > 0 
                ? (result.BytesTransferred / (1024.0 * 1024.0)) / duration.TotalSeconds 
                : 0;

            _logger.LogInformation("Optimized file transfer completed: Success={Success}, Throughput={Throughput:F2} MB/s, Duration={Duration}ms",
                result.Success, throughputMBps, duration.TotalMilliseconds);

            _memoryProfiler?.RecordSnapshot(operationId, "Complete", 
                $"Transfer completed: Success={result.Success}, Throughput={throughputMBps:F2} MB/s");

            // 获取内存分析和建议 / Get memory profile and recommendations
            var profile = _memoryProfiler?.StopProfiling(operationId);
            if (profile != null)
            {
                var recommendations = _memoryProfiler?.GetRecommendations(profile);
                if (recommendations?.Any() == true)
                {
                    _logger.LogInformation("Performance recommendations for optimized transfer:");
                    foreach (var rec in recommendations)
                    {
                        _logger.LogInformation("- {Priority}: {Title} - {Description}", 
                            rec.Priority, rec.Title, rec.Description);
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Optimized file transfer failed for {FilePath} after {Duration}ms", filePath, duration.TotalMilliseconds);
            
            _memoryProfiler?.RecordSnapshot(operationId, "Error", $"Transfer failed: {ex.Message}");
            _memoryProfiler?.StopProfiling(operationId);

            return new TransferResult
            {
                Success = false,
                ErrorMessage = $"Optimized transfer failed: {ex.Message}",
                Duration = duration
            };
        }
    }

    /// <summary>
    /// 使用优化恢复中断的文件传输 / Resumes an interrupted file transfer with optimization
    /// </summary>
    /// <param name="resumeToken">恢复令牌 / Resume token</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>传输结果 / Transfer result</returns>
    public async Task<TransferResult> ResumeTransferAsync(string resumeToken, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resuming optimized file transfer with token {ResumeToken}", resumeToken);
        return await _baseClient.ResumeTransferAsync(resumeToken, cancellationToken);
    }

    /// <summary>
    /// 使用完整上下文和优化恢复中断的文件传输 / Resumes an interrupted file transfer with full context and optimization
    /// 重新应用基于文件大小的优化配置 / Re-applies optimization configuration based on file size
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
            _logger.LogInformation("Resuming optimized file transfer with token {ResumeToken} for file {FilePath}", resumeToken, filePath);

            // 获取文件信息用于优化决策 / Get file info for optimization decisions
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                return new TransferResult
                {
                    Success = false,
                    ErrorMessage = $"File not found for resume: {filePath}",
                    Duration = TimeSpan.Zero
                };
            }

            // 为恢复优化传输配置 / Optimize transfer configuration for resume
            var optimizedConfig = OptimizeTransferConfig(config, fileInfo.Length);
            
            return await _baseClient.ResumeTransferAsync(resumeToken, filePath, optimizedConfig, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Optimized resume transfer failed for {FilePath}", filePath);
            return new TransferResult
            {
                Success = false,
                ErrorMessage = $"Optimized resume failed: {ex.Message}",
                Duration = TimeSpan.Zero
            };
        }
    }

    /// <summary>
    /// 根据文件大小和系统能力优化传输配置 / Optimizes transfer configuration based on file size and system capabilities
    /// 自动调整分块策略、超时时间和并发设置 / Automatically adjusts chunking strategy, timeout and concurrency settings
    /// </summary>
    /// <param name="originalConfig">原始传输配置 / Original transfer configuration</param>
    /// <param name="fileSize">文件大小（字节） / File size in bytes</param>
    /// <returns>优化后的传输配置 / Optimized transfer configuration</returns>
    private TransferConfig OptimizeTransferConfig(TransferConfig originalConfig, long fileSize)
    {
        var optimizedConfig = new TransferConfig
        {
            TargetServer = originalConfig.TargetServer,
            TargetDirectory = originalConfig.TargetDirectory,
            FileName = originalConfig.FileName,
            TimeoutSeconds = OptimizeTimeout(originalConfig.TimeoutSeconds, fileSize),
            MaxRetries = originalConfig.MaxRetries,
            ChunkingStrategy = ChunkingStrategy.GetOptimizedStrategy(fileSize)
        };

        _logger.LogDebug("Optimized transfer config for {FileSize} bytes: ChunkSize={ChunkSize}, MaxConcurrent={MaxConcurrent}, Timeout={Timeout}s",
            fileSize, optimizedConfig.ChunkingStrategy.ChunkSize, optimizedConfig.ChunkingStrategy.MaxConcurrentChunks, optimizedConfig.TimeoutSeconds);

        return optimizedConfig;
    }

    /// <summary>
    /// 根据文件大小优化超时时间 / Optimizes timeout based on file size
    /// 为大文件提供更长的超时时间以避免传输中断 / Provides longer timeout for large files to avoid transfer interruption
    /// </summary>
    /// <param name="originalTimeout">原始超时时间（秒） / Original timeout in seconds</param>
    /// <param name="fileSize">文件大小（字节） / File size in bytes</param>
    /// <returns>优化后的超时时间（秒） / Optimized timeout in seconds</returns>
    private int OptimizeTimeout(int originalTimeout, long fileSize)
    {
        // 基础超时时间加上基于文件大小的额外时间 / Base timeout plus additional time based on file size
        var baseTimeout = Math.Max(originalTimeout, 60); // 至少1分钟 / At least 1 minute
        
        // 每100MB文件大小增加1分钟 / Add 1 minute per 100MB of file size
        var additionalTimeout = (int)(fileSize / (100 * 1024 * 1024)) * 60;
        
        // 对于非常大的文件，最多30分钟 / Cap at 30 minutes for very large files
        return Math.Min(baseTimeout + additionalTimeout, 30 * 60);
    }
}