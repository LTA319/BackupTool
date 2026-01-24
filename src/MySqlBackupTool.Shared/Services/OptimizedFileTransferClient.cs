using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Optimized file transfer client with adaptive performance tuning
/// </summary>
public class OptimizedFileTransferClient : IFileTransferClient
{
    private readonly ILogger<OptimizedFileTransferClient> _logger;
    private readonly IFileTransferClient _baseClient;
    private readonly IMemoryProfiler? _memoryProfiler;

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
    /// Transfers a file with adaptive optimization based on file size and network conditions
    /// </summary>
    public async Task<TransferResult> TransferFileAsync(string filePath, TransferConfig config, CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow;
        
        _memoryProfiler?.StartProfiling(operationId, "OptimizedFileTransfer");
        _memoryProfiler?.RecordSnapshot(operationId, "Start", $"Starting optimized transfer: {filePath}");

        try
        {
            // Get file info for optimization decisions
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

            // Optimize transfer configuration based on file size
            var optimizedConfig = OptimizeTransferConfig(config, fileInfo.Length);
            _memoryProfiler?.RecordSnapshot(operationId, "ConfigOptimized", 
                $"Optimized config: ChunkSize={optimizedConfig.ChunkingStrategy.ChunkSize}, MaxConcurrent={optimizedConfig.ChunkingStrategy.MaxConcurrentChunks}");

            // Perform the transfer with optimized settings
            var result = await _baseClient.TransferFileAsync(filePath, optimizedConfig, cancellationToken);
            
            // Calculate and log performance metrics
            var duration = DateTime.UtcNow - startTime;
            var throughputMBps = result.BytesTransferred > 0 && duration.TotalSeconds > 0 
                ? (result.BytesTransferred / (1024.0 * 1024.0)) / duration.TotalSeconds 
                : 0;

            _logger.LogInformation("Optimized file transfer completed: Success={Success}, Throughput={Throughput:F2} MB/s, Duration={Duration}ms",
                result.Success, throughputMBps, duration.TotalMilliseconds);

            _memoryProfiler?.RecordSnapshot(operationId, "Complete", 
                $"Transfer completed: Success={result.Success}, Throughput={throughputMBps:F2} MB/s");

            // Get memory profile and recommendations
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
    /// Resumes an interrupted file transfer with optimization
    /// </summary>
    public async Task<TransferResult> ResumeTransferAsync(string resumeToken, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resuming optimized file transfer with token {ResumeToken}", resumeToken);
        return await _baseClient.ResumeTransferAsync(resumeToken, cancellationToken);
    }

    /// <summary>
    /// Resumes an interrupted file transfer with full context and optimization
    /// </summary>
    public async Task<TransferResult> ResumeTransferAsync(string resumeToken, string filePath, TransferConfig config, CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid().ToString();
        
        try
        {
            _logger.LogInformation("Resuming optimized file transfer with token {ResumeToken} for file {FilePath}", resumeToken, filePath);

            // Get file info for optimization decisions
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

            // Optimize transfer configuration for resume
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
    /// Optimizes transfer configuration based on file size and system capabilities
    /// </summary>
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
    /// Optimizes timeout based on file size
    /// </summary>
    private int OptimizeTimeout(int originalTimeout, long fileSize)
    {
        // Base timeout plus additional time based on file size
        var baseTimeout = Math.Max(originalTimeout, 60); // At least 1 minute
        
        // Add 1 minute per 100MB of file size
        var additionalTimeout = (int)(fileSize / (100 * 1024 * 1024)) * 60;
        
        // Cap at 30 minutes for very large files
        return Math.Min(baseTimeout + additionalTimeout, 30 * 60);
    }
}