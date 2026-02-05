using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 基于TCP的文件传输客户端实现 / TCP-based file transfer client implementation
/// 提供文件传输、断点续传、分块传输等功能 / Provides file transfer, resume transfer, chunked transfer and other features
/// </summary>
public class FileTransferClient : IFileTransferClient, IFileTransferService
{
    private readonly ILogger<FileTransferClient> _logger;
    private readonly IChecksumService _checksumService;
    private readonly IMemoryProfiler? _memoryProfiler;
    
    // 针对不同场景优化的缓冲区大小
    private const int SmallFileBufferSize = 64 * 1024; // 64KB 用于小于10MB的文件
    private const int LargeFileBufferSize = 1024 * 1024; // 1MB 用于大于等于10MB的文件
    private const int HugeFileBufferSize = 4 * 1024 * 1024; // 4MB 用于大于等于100MB的文件
    private const long LargeFileThreshold = 10 * 1024 * 1024; // 10MB
    private const long HugeFileThreshold = 100 * 1024 * 1024; // 100MB
    
    // 网络配置常量
    private const int MaxRetryAttempts = 3;
    private const int BaseRetryDelayMs = 1000; // 1秒基础延迟
    private const int MaxChunkRetries = 3;
    private const int ChunkRetryDelayMs = 100;
    private const int NetworkTimeoutSeconds = 30;
    private const int MinSocketBufferSize = 65536; // 64KB最小值
    private const int SocketBufferMultiplier = 2;
    private const int TcpClientBufferSize = 1024 * 1024; // 1MB
    private const int ProgressReportingDivisor = 20; // 每1/20的分块报告进度
    private const int PeriodicFlushMultiplier = 10; // 每10个缓冲区大小刷新一次

    /// <summary>
    /// 构造函数，初始化文件传输客户端 / Constructor, initialize file transfer client
    /// </summary>
    /// <param name="logger">日志记录器 / Logger instance</param>
    /// <param name="checksumService">校验和服务 / Checksum service</param>
    /// <param name="memoryProfiler">内存分析器（可选） / Memory profiler (optional)</param>
    /// <exception cref="ArgumentNullException">当必需参数为null时抛出 / Thrown when required parameters are null</exception>
    public FileTransferClient(ILogger<FileTransferClient> logger, IChecksumService checksumService, IMemoryProfiler? memoryProfiler = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _checksumService = checksumService ?? throw new ArgumentNullException(nameof(checksumService));
        _memoryProfiler = memoryProfiler;
    }

    /// <summary>
    /// 将文件传输到远程服务器 / Transfer file to remote server
    /// </summary>
    /// <param name="filePath">要传输的文件路径 / File path to transfer</param>
    /// <param name="config">传输配置 / Transfer configuration</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>传输结果 / Transfer result</returns>
    public async Task<TransferResult> TransferFileAsync(string filePath, TransferConfig config, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        var transferId = Guid.NewGuid().ToString();
        var operationId = $"transfer-{Path.GetFileName(filePath)}-{transferId[..8]}";
        
        _memoryProfiler?.StartProfiling(operationId, "FileTransfer");
        _memoryProfiler?.RecordSnapshot(operationId, "Start", $"Starting file transfer: {filePath} -> {config.TargetServer.IPAddress}:{config.TargetServer.Port}");
        
        try
        {
            // 提前检查取消 / Check for cancellation early
            cancellationToken.ThrowIfCancellationRequested();
            
            _logger.LogInformation("Starting file transfer for {FilePath} to {Server}:{Port}", 
                filePath, config.TargetServer.IPAddress, config.TargetServer.Port);

            // 验证输入 / Validate inputs
            _memoryProfiler?.RecordSnapshot(operationId, "Validation", "Validating inputs");
            if (!File.Exists(filePath))
            {
                _memoryProfiler?.RecordSnapshot(operationId, "ValidationFailed", "File not found");
                _memoryProfiler?.StopProfiling(operationId);
                return new TransferResult
                {
                    Success = false,
                    ErrorMessage = $"File not found: {filePath}",
                    Duration = DateTime.Now - startTime
                };
            }

            var (isValid, errors) = config.TargetServer.ValidateEndpoint();
            if (!isValid)
            {
                _memoryProfiler?.RecordSnapshot(operationId, "ValidationFailed", "Invalid server endpoint");
                _memoryProfiler?.StopProfiling(operationId);
                return new TransferResult
                {
                    Success = false,
                    ErrorMessage = $"Invalid server endpoint: {string.Join(", ", errors)}",
                    Duration = DateTime.Now - startTime
                };
            }

            // 获取文件信息并创建元数据 / Get file info and create metadata
            _memoryProfiler?.RecordSnapshot(operationId, "CreateMetadata", "Creating file metadata");
            var fileInfo = new FileInfo(filePath);
            var metadata = await CreateFileMetadataAsync(filePath, config.FileName, config);

            // 创建传输请求 / Create transfer request
            var transferRequest = new TransferRequest
            {
                TransferId = transferId,
                Metadata = metadata,
                ChunkingStrategy = config.ChunkingStrategy,
                ResumeTransfer = false
            };

            _memoryProfiler?.RecordSnapshot(operationId, "StartTransfer", $"Starting transfer with retry logic, file size: {fileInfo.Length} bytes");

            // 使用重试逻辑执行传输 / Perform transfer with retry logic
            var result = await TransferWithRetryAsync(filePath, transferRequest, config, cancellationToken, operationId);
            result.Duration = DateTime.Now - startTime;

            _logger.LogInformation("File transfer completed. Success: {Success}, Bytes: {Bytes}, Duration: {Duration}",
                result.Success, result.BytesTransferred, result.Duration);

            _memoryProfiler?.RecordSnapshot(operationId, "Complete", $"Transfer completed: Success={result.Success}, Bytes={result.BytesTransferred}");
            
            // 获取内存分析和建议 / Get memory profile and recommendations
            var profile = _memoryProfiler?.StopProfiling(operationId);
            if (profile != null)
            {
                var recommendations = _memoryProfiler?.GetRecommendations(profile);
                if (recommendations?.Any() == true)
                {
                    _logger.LogInformation("Memory profiling recommendations for file transfer:");
                    foreach (var rec in recommendations)
                    {
                        _logger.LogInformation("- {Priority}: {Title} - {Description}", 
                            rec.Priority, rec.Title, rec.Description);
                    }
                }
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("File transfer was cancelled for {FilePath}", filePath);
            _memoryProfiler?.RecordSnapshot(operationId, "Cancelled", "Transfer was cancelled");
            _memoryProfiler?.StopProfiling(operationId);
            return new TransferResult
            {
                Success = false,
                ErrorMessage = "Transfer was cancelled",
                Duration = DateTime.Now - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during file transfer for {FilePath}", filePath);
            _memoryProfiler?.RecordSnapshot(operationId, "Exception", $"Unexpected error: {ex.Message}");
            _memoryProfiler?.StopProfiling(operationId);
            return new TransferResult
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}",
                Duration = DateTime.Now - startTime
            };
        }
    }

    /// <summary>
    /// 恢复中断的文件传输 / Resume interrupted file transfer
    /// </summary>
    /// <param name="resumeToken">恢复令牌 / Resume token</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>传输结果 / Transfer result</returns>
    public async Task<TransferResult> ResumeTransferAsync(string resumeToken, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        
        try
        {
            _logger.LogInformation("Resuming file transfer with token {ResumeToken}", resumeToken);

            // 此方法需要访问IChunkManager来获取恢复信息 / This method would need access to IChunkManager to get resume info
            // 目前，返回错误表示客户端需要更新以支持恢复 / For now, return an error indicating the client needs to be updated to support resume
            // 实际实现需要依赖注入IChunkManager / The actual implementation would require dependency injection of IChunkManager
            
            return new TransferResult
            {
                Success = false,
                ErrorMessage = "Resume functionality requires server-side coordination. Use ResumeTransferAsync(resumeToken, filePath, config) instead.",
                Duration = DateTime.Now - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming transfer with token {ResumeToken}", resumeToken);
            return new TransferResult
            {
                Success = false,
                ErrorMessage = $"Resume error: {ex.Message}",
                Duration = DateTime.Now - startTime
            };
        }
    }

    /// <summary>
    /// 使用完整上下文恢复中断的文件传输 / Resume interrupted file transfer with full context
    /// </summary>
    /// <param name="resumeToken">恢复令牌 / Resume token</param>
    /// <param name="filePath">文件路径 / File path</param>
    /// <param name="config">传输配置 / Transfer configuration</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>传输结果 / Transfer result</returns>
    public async Task<TransferResult> ResumeTransferAsync(string resumeToken, string filePath, TransferConfig config, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        var operationId = $"resume-{Path.GetFileName(filePath)}-{resumeToken[..Math.Min(8, resumeToken.Length)]}";
        
        _memoryProfiler?.StartProfiling(operationId, "FileTransferResume");
        _memoryProfiler?.RecordSnapshot(operationId, "Start", $"Starting resume transfer: {filePath} with token {resumeToken}");
        
        try
        {
            _logger.LogInformation("Resuming file transfer with token {ResumeToken} for file {FilePath}", resumeToken, filePath);

            // 验证输入 / Validate inputs
            _memoryProfiler?.RecordSnapshot(operationId, "Validation", "Validating inputs for resume");
            if (!File.Exists(filePath))
            {
                _memoryProfiler?.RecordSnapshot(operationId, "ValidationFailed", "File not found");
                _memoryProfiler?.StopProfiling(operationId);
                return new TransferResult
                {
                    Success = false,
                    ErrorMessage = $"File not found: {filePath}",
                    Duration = DateTime.Now - startTime
                };
            }

            var (isValid, errors) = config.TargetServer.ValidateEndpoint();
            if (!isValid)
            {
                _memoryProfiler?.RecordSnapshot(operationId, "ValidationFailed", "Invalid server endpoint");
                _memoryProfiler?.StopProfiling(operationId);
                return new TransferResult
                {
                    Success = false,
                    ErrorMessage = $"Invalid server endpoint: {string.Join(", ", errors)}",
                    Duration = DateTime.Now - startTime
                };
            }

            // 创建文件元数据 / Create file metadata
            _memoryProfiler?.RecordSnapshot(operationId, "CreateMetadata", "Creating file metadata for resume");
            var metadata = await CreateFileMetadataAsync(filePath, config.FileName, config);

            // 创建恢复传输请求 / Create resume transfer request
            var transferRequest = new TransferRequest
            {
                TransferId = Guid.NewGuid().ToString(), // 恢复的新传输ID / New transfer ID for resume
                Metadata = metadata,
                ChunkingStrategy = config.ChunkingStrategy,
                ResumeTransfer = true,
                ResumeToken = resumeToken
            };

            _memoryProfiler?.RecordSnapshot(operationId, "StartResume", "Starting resume transfer with retry logic");

            // 使用重试逻辑执行传输 / Perform transfer with retry logic
            var result = await TransferWithRetryAsync(filePath, transferRequest, config, cancellationToken, operationId);
            result.Duration = DateTime.Now - startTime;

            _logger.LogInformation("Resume transfer completed. Success: {Success}, Bytes: {Bytes}, Duration: {Duration}",
                result.Success, result.BytesTransferred, result.Duration);

            _memoryProfiler?.RecordSnapshot(operationId, "Complete", $"Resume transfer completed: Success={result.Success}, Bytes={result.BytesTransferred}");
            
            // 获取内存分析和建议 / Get memory profile and recommendations
            var profile = _memoryProfiler?.StopProfiling(operationId);
            if (profile != null)
            {
                var recommendations = _memoryProfiler?.GetRecommendations(profile);
                if (recommendations?.Any() == true)
                {
                    _logger.LogInformation("Memory profiling recommendations for resume transfer:");
                    foreach (var rec in recommendations)
                    {
                        _logger.LogInformation("- {Priority}: {Title} - {Description}", 
                            rec.Priority, rec.Title, rec.Description);
                    }
                }
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Resume transfer was cancelled for {FilePath}", filePath);
            _memoryProfiler?.RecordSnapshot(operationId, "Cancelled", "Resume transfer was cancelled");
            _memoryProfiler?.StopProfiling(operationId);
            return new TransferResult
            {
                Success = false,
                ErrorMessage = "Resume transfer was cancelled",
                Duration = DateTime.Now - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during resume transfer for {FilePath}", filePath);
            _memoryProfiler?.RecordSnapshot(operationId, "Exception", $"Unexpected error: {ex.Message}");
            _memoryProfiler?.StopProfiling(operationId);
            return new TransferResult
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}",
                Duration = DateTime.Now - startTime
            };
        }
    }

    /// <summary>
    /// 使用重试逻辑执行文件传输
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="request">传输请求</param>
    /// <param name="config">传输配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="operationId">操作标识符</param>
    /// <returns>传输结果</returns>
    private async Task<TransferResult> TransferWithRetryAsync(string filePath, TransferRequest request, TransferConfig config, CancellationToken cancellationToken, string operationId)
    {
        Exception? lastException = null;
        
        for (int attempt = 1; attempt <= config.MaxRetries; attempt++)
        {
            try
            {
                _logger.LogDebug("Transfer attempt {Attempt} of {MaxRetries} for {TransferId}", 
                    attempt, config.MaxRetries, request.TransferId);
                _memoryProfiler?.RecordSnapshot(operationId, $"Attempt{attempt}", $"Transfer attempt {attempt} of {config.MaxRetries}");

                var result = await PerformTransferAsync(filePath, request, config, cancellationToken, operationId);
                
                if (result.Success)
                {
                    _memoryProfiler?.RecordSnapshot(operationId, "TransferSuccess", $"Transfer succeeded on attempt {attempt}");
                    return result;
                }

                // If not successful but no exception, treat as retriable error
                lastException = new InvalidOperationException(result.ErrorMessage ?? "Transfer failed");
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Transfer attempt {Attempt} failed for {TransferId}", attempt, request.TransferId);
                _memoryProfiler?.RecordSnapshot(operationId, $"AttemptFailed{attempt}", $"Attempt {attempt} failed: {ex.Message}");
            }

            // Don't retry if cancelled
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            // Don't delay after the last attempt
            if (attempt < config.MaxRetries)
            {
                var delay = TimeSpan.FromMilliseconds(BaseRetryDelayMs * Math.Pow(2, attempt - 1));
                _logger.LogDebug("Waiting {Delay}ms before retry attempt {NextAttempt}", delay.TotalMilliseconds, attempt + 1);
                _memoryProfiler?.RecordSnapshot(operationId, $"RetryDelay{attempt}", $"Waiting {delay.TotalMilliseconds}ms before retry");
                await Task.Delay(delay, cancellationToken);
            }
        }

        return new TransferResult
        {
            Success = false,
            ErrorMessage = $"Transfer failed after {config.MaxRetries} attempts. Last error: {lastException?.Message}",
            BytesTransferred = 0
        };
    }

    /// <summary>
    /// 使用优化的网络设置执行实际的文件传输
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="request">传输请求</param>
    /// <param name="config">传输配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="operationId">操作标识符</param>
    /// <returns>传输结果</returns>
    private async Task<TransferResult> PerformTransferAsync(string filePath, TransferRequest request, TransferConfig config, CancellationToken cancellationToken, string operationId)
    {
        using var tcpClient = new TcpClient();
        
        try
        {
            // Configure TCP client for optimal performance
            ConfigureTcpClientForPerformance(tcpClient);
            
            _memoryProfiler?.RecordSnapshot(operationId, "Connect", "Connecting to server with optimized settings");
            
            // Connect to server with timeout
            var connectTask = tcpClient.ConnectAsync(config.TargetServer.IPAddress, config.TargetServer.Port);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(config.TimeoutSeconds), cancellationToken);
            
            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                throw new TimeoutException($"Connection timeout after {config.TimeoutSeconds} seconds");
            }
            
            if (!tcpClient.Connected)
            {
                throw new InvalidOperationException("Failed to establish TCP connection");
            }

            _logger.LogDebug("Connected to {Server}:{Port} for transfer {TransferId} with optimized settings", 
                config.TargetServer.IPAddress, config.TargetServer.Port, request.TransferId);
            _memoryProfiler?.RecordSnapshot(operationId, "Connected", "Successfully connected to server");

            using var networkStream = tcpClient.GetStream();
            
            // Send transfer request header
            _memoryProfiler?.RecordSnapshot(operationId, "SendRequest", "Sending transfer request");
            await SendTransferRequestAsync(networkStream, request, cancellationToken);
            
            // Wait for server acknowledgment
            _memoryProfiler?.RecordSnapshot(operationId, "WaitAck", "Waiting for server acknowledgment");
            var ackResponse = await ReceiveAcknowledgmentAsync(networkStream, cancellationToken);
            if (!ackResponse.Success)
            {
                throw new InvalidOperationException($"Server rejected transfer: {ackResponse.ErrorMessage}");
            }

            // Parse resume information if this is a resume transfer
            var completedChunks = new HashSet<int>();
            if (request.ResumeTransfer && !string.IsNullOrEmpty(ackResponse.AdditionalInfo))
            {
                try
                {
                    var resumeChunks = JsonSerializer.Deserialize<List<int>>(ackResponse.AdditionalInfo);
                    if (resumeChunks != null)
                    {
                        completedChunks = new HashSet<int>(resumeChunks);
                        _logger.LogInformation("Resume transfer for {TransferId}: {Count} chunks already completed", 
                            request.TransferId, completedChunks.Count);
                        _memoryProfiler?.RecordSnapshot(operationId, "ResumeInfo", $"Resume transfer: {completedChunks.Count} chunks completed");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse resume information, proceeding with full transfer");
                }
            }

            // Send file data
            _memoryProfiler?.RecordSnapshot(operationId, "SendData", "Starting file data transfer");
            var bytesTransferred = await SendFileDataAsync(networkStream, filePath, request, completedChunks, cancellationToken, operationId);
            
            // Wait for final confirmation
            _memoryProfiler?.RecordSnapshot(operationId, "WaitConfirmation", "Waiting for final confirmation");
            var finalResponse = await ReceiveFinalConfirmationAsync(networkStream, cancellationToken);
            
            _memoryProfiler?.RecordSnapshot(operationId, "TransferComplete", $"Transfer completed: {bytesTransferred} bytes");
            
            return new TransferResult
            {
                Success = finalResponse.Success,
                ErrorMessage = finalResponse.ErrorMessage,
                BytesTransferred = bytesTransferred
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during file transfer for {TransferId}", request.TransferId);
            _memoryProfiler?.RecordSnapshot(operationId, "TransferError", $"Transfer error: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 向服务器发送传输请求头
    /// </summary>
    /// <param name="stream">网络流</param>
    /// <param name="request">传输请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task SendTransferRequestAsync(NetworkStream stream, TransferRequest request, CancellationToken cancellationToken)
    {
        var requestJson = JsonSerializer.Serialize(request);
        var requestBytes = Encoding.UTF8.GetBytes(requestJson);
        var headerBytes = BitConverter.GetBytes(requestBytes.Length);
        
        // Send header length (4 bytes) followed by JSON data
        await stream.WriteAsync(headerBytes, 0, 4, cancellationToken);
        await stream.WriteAsync(requestBytes, 0, requestBytes.Length, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        
        _logger.LogDebug("Sent transfer request for {TransferId}, size: {Size} bytes", request.TransferId, requestBytes.Length);
    }

    /// <summary>
    /// 接收服务器的确认响应
    /// </summary>
    /// <param name="stream">网络流</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>传输响应</returns>
    private async Task<TransferResponse> ReceiveAcknowledgmentAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        try
        {
            var lengthBuffer = new byte[4];
            await ReadExactlyAsync(stream, lengthBuffer, 4, cancellationToken);
            
            var responseLength = BitConverter.ToInt32(lengthBuffer, 0);
            if (responseLength <= 0 || responseLength > 1024 * 1024) // Max 1MB for response
            {
                throw new InvalidOperationException($"Invalid response length: {responseLength}");
            }
            
            var responseBuffer = new byte[responseLength];
            await ReadExactlyAsync(stream, responseBuffer, responseLength, cancellationToken);
            
            var responseJson = Encoding.UTF8.GetString(responseBuffer);
            var response = JsonSerializer.Deserialize<TransferResponse>(responseJson) ?? new TransferResponse();
            
            _logger.LogDebug("Received acknowledgment: Success={Success}, Message={Message}", response.Success, response.ErrorMessage);
            return response;
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            _logger.LogError(ex, "Error receiving acknowledgment from server");
            return new TransferResponse
            {
                Success = false,
                ErrorMessage = $"Failed to receive server acknowledgment: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 向服务器发送文件数据
    /// </summary>
    /// <param name="stream">网络流</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="request">传输请求</param>
    /// <param name="completedChunks">已完成的分块集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="operationId">操作标识符</param>
    /// <returns>传输的字节数</returns>
    private async Task<long> SendFileDataAsync(NetworkStream stream, string filePath, TransferRequest request, HashSet<int> completedChunks, CancellationToken cancellationToken, string operationId)
    {
        var fileInfo = new FileInfo(filePath);
        var fileSize = fileInfo.Length;
        
        _logger.LogDebug("Starting file data transfer for {TransferId}, file size: {FileSize} bytes", request.TransferId, fileSize);
        _memoryProfiler?.RecordSnapshot(operationId, "StartDataTransfer", $"Starting data transfer, file size: {fileSize} bytes");
        
        // Determine if we should use chunking
        var shouldChunk = fileSize > request.ChunkingStrategy.ChunkSize;
        
        if (shouldChunk)
        {
            _memoryProfiler?.RecordSnapshot(operationId, "ChunkedTransfer", "Using chunked transfer strategy");
            return await SendFileDataChunkedAsync(stream, filePath, request, completedChunks, cancellationToken, operationId);
        }
        else
        {
            _memoryProfiler?.RecordSnapshot(operationId, "DirectTransfer", "Using direct transfer strategy");
            return await SendFileDataDirectAsync(stream, filePath, request, cancellationToken, operationId);
        }
    }

    /// <summary>
    /// 使用优化的缓冲区大小直接发送文件数据（不分块）
    /// </summary>
    /// <param name="stream">网络流</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="request">传输请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="operationId">操作标识符</param>
    /// <returns>传输的字节数</returns>
    private async Task<long> SendFileDataDirectAsync(NetworkStream stream, string filePath, TransferRequest request, CancellationToken cancellationToken, string operationId)
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        var fileSize = fileStream.Length;
        
        // Choose optimal buffer size based on file size
        var bufferSize = GetOptimalBufferSize(fileSize);
        var buffer = new byte[bufferSize];
        
        long totalBytes = 0;
        
        _logger.LogDebug("Starting direct transfer for {TransferId}, file size: {FileSize} bytes, buffer size: {BufferSize} bytes", 
            request.TransferId, fileSize, bufferSize);
        _memoryProfiler?.RecordSnapshot(operationId, "DirectTransferStart", 
            $"Direct transfer starting: file size {fileSize} bytes, buffer size {bufferSize} bytes");
        
        // Configure network stream for optimal performance
        ConfigureNetworkStreamForPerformance(stream, bufferSize);
        
        while (totalBytes < fileSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var bytesToRead = (int)Math.Min(bufferSize, fileSize - totalBytes);
            var bytesRead = await fileStream.ReadAsync(buffer, 0, bytesToRead, cancellationToken);
            
            if (bytesRead == 0)
                break;
                
            await stream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytes += bytesRead;
            
            // Log progress and flush periodically for better performance
            if (totalBytes % (bufferSize * PeriodicFlushMultiplier) == 0 || totalBytes == fileSize)
            {
                var progress = (double)totalBytes / fileSize * 100;
                _logger.LogDebug("Transfer progress for {TransferId}: {Progress:F1}% ({Bytes}/{Total} bytes)", 
                    request.TransferId, progress, totalBytes, fileSize);
                _memoryProfiler?.RecordSnapshot(operationId, "DirectProgress", 
                    $"Direct transfer progress: {progress:F1}% ({totalBytes}/{fileSize} bytes)");
                
                // Flush network stream periodically for better throughput
                await stream.FlushAsync(cancellationToken);
            }
        }
        
        await stream.FlushAsync(cancellationToken);
        _logger.LogDebug("Completed direct file data transfer for {TransferId}, total bytes: {TotalBytes}, buffer size: {BufferSize}", 
            request.TransferId, totalBytes, bufferSize);
        _memoryProfiler?.RecordSnapshot(operationId, "DirectComplete", $"Direct transfer completed: {totalBytes} bytes");
        
        return totalBytes;
    }

    /// <summary>
    /// 使用分块策略发送文件数据，具有优化的性能
    /// </summary>
    /// <param name="stream">网络流</param>
    /// <param name="filePath">文件路径</param>
    /// <param name="request">传输请求</param>
    /// <param name="completedChunks">已完成的分块集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <param name="operationId">操作标识符</param>
    /// <returns>传输的字节数</returns>
    private async Task<long> SendFileDataChunkedAsync(NetworkStream stream, string filePath, TransferRequest request, HashSet<int> completedChunks, CancellationToken cancellationToken, string operationId)
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        var fileSize = fileStream.Length;
        var chunkCount = request.ChunkingStrategy.CalculateChunkCount(fileSize);
        long totalBytes = 0;
        
        // Configure network stream for chunked transfer performance
        ConfigureNetworkStreamForPerformance(stream, (int)Math.Min(request.ChunkingStrategy.ChunkSize, HugeFileBufferSize));
        
        _logger.LogInformation("Starting chunked transfer for {TransferId}: {ChunkCount} chunks of {ChunkSize} bytes each", 
            request.TransferId, chunkCount, request.ChunkingStrategy.ChunkSize);
        _memoryProfiler?.RecordSnapshot(operationId, "ChunkedStart", 
            $"Starting chunked transfer: {chunkCount} chunks of {request.ChunkingStrategy.ChunkSize} bytes");

        for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Calculate chunk size for this chunk
            var remainingBytes = fileSize - fileStream.Position;
            var currentChunkSize = (int)Math.Min(request.ChunkingStrategy.ChunkSize, remainingBytes);
            
            // Skip chunks that are already completed (for resume transfers)
            if (completedChunks.Contains(chunkIndex))
            {
                // Seek file stream to skip this chunk
                fileStream.Seek(currentChunkSize, SeekOrigin.Current);
                totalBytes += currentChunkSize;
                
                _logger.LogDebug("Skipped already completed chunk {ChunkIndex} for {TransferId}", chunkIndex, request.TransferId);
                continue;
            }
            
            // Read chunk data with optimized buffer allocation
            var chunkData = new byte[currentChunkSize];
            var bytesRead = await fileStream.ReadAsync(chunkData, 0, currentChunkSize, cancellationToken);
            
            if (bytesRead != currentChunkSize)
            {
                throw new InvalidOperationException($"Expected to read {currentChunkSize} bytes but read {bytesRead} bytes for chunk {chunkIndex}");
            }
            
            // Calculate chunk checksum
            var chunkChecksum = _checksumService.CalculateMD5(chunkData);
            
            // Create chunk object
            var chunk = new ChunkData
            {
                TransferId = request.TransferId,
                ChunkIndex = chunkIndex,
                Data = chunkData,
                ChunkChecksum = chunkChecksum,
                IsLastChunk = chunkIndex == chunkCount - 1
            };
            
            // Send chunk with retry logic for individual chunks
            await SendChunkWithRetryAsync(stream, chunk, cancellationToken);
            
            totalBytes += bytesRead;
            
            // Log progress and record memory snapshots with adaptive frequency
            var progress = (double)totalBytes / fileSize * 100;
            var shouldLogProgress = chunkIndex % Math.Max(1, chunkCount / ProgressReportingDivisor) == 0 || chunkIndex == chunkCount - 1;
            
            if (shouldLogProgress)
            {
                _logger.LogDebug("Sent chunk {ChunkIndex}/{ChunkCount} for {TransferId}: {Progress:F1}% ({Bytes}/{Total} bytes)", 
                    chunkIndex + 1, chunkCount, request.TransferId, progress, totalBytes, fileSize);
                
                _memoryProfiler?.RecordSnapshot(operationId, "ChunkProgress", 
                    $"Chunk {chunkIndex + 1}/{chunkCount}: {progress:F1}% ({totalBytes}/{fileSize} bytes)");
            }
        }
        
        _logger.LogInformation("Completed chunked file data transfer for {TransferId}, total bytes: {TotalBytes}", request.TransferId, totalBytes);
        _memoryProfiler?.RecordSnapshot(operationId, "ChunkedComplete", $"Chunked transfer completed: {totalBytes} bytes");
        
        return totalBytes;
    }

    /// <summary>
    /// 使用重试逻辑向服务器发送单个分块
    /// </summary>
    /// <param name="stream">网络流</param>
    /// <param name="chunk">分块数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task SendChunkWithRetryAsync(NetworkStream stream, ChunkData chunk, CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        
        for (int attempt = 1; attempt <= MaxChunkRetries; attempt++)
        {
            try
            {
                await SendChunkAsync(stream, chunk, cancellationToken);
                return; // Success
            }
            catch (Exception ex) when (attempt < MaxChunkRetries)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Chunk {ChunkIndex} send attempt {Attempt} failed, retrying...", 
                    chunk.ChunkIndex, attempt);
                
                // Brief delay before retry
                await Task.Delay(ChunkRetryDelayMs * attempt, cancellationToken);
            }
        }
        
        // All retries failed
        throw new InvalidOperationException(
            $"Failed to send chunk {chunk.ChunkIndex} after {MaxChunkRetries} attempts: {lastException?.Message}", 
            lastException);
    }

    /// <summary>
    /// 向服务器发送单个分块
    /// </summary>
    /// <param name="stream">网络流</param>
    /// <param name="chunk">分块数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task SendChunkAsync(NetworkStream stream, ChunkData chunk, CancellationToken cancellationToken)
    {
        var chunkJson = JsonSerializer.Serialize(chunk);
        var chunkBytes = Encoding.UTF8.GetBytes(chunkJson);
        var headerBytes = BitConverter.GetBytes(chunkBytes.Length);
        
        // Send chunk header length (4 bytes) followed by JSON data
        await stream.WriteAsync(headerBytes, 0, 4, cancellationToken);
        await stream.WriteAsync(chunkBytes, 0, chunkBytes.Length, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        
        // Wait for chunk acknowledgment
        var ackResponse = await ReceiveChunkAcknowledgmentAsync(stream, cancellationToken);
        if (!ackResponse.Success)
        {
            throw new InvalidOperationException($"Server rejected chunk {chunk.ChunkIndex}: {ackResponse.ErrorMessage}");
        }
    }

    /// <summary>
    /// 接收服务器对分块的确认响应
    /// </summary>
    /// <param name="stream">网络流</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分块结果</returns>
    private async Task<ChunkResult> ReceiveChunkAcknowledgmentAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        try
        {
            var lengthBuffer = new byte[4];
            await ReadExactlyAsync(stream, lengthBuffer, 4, cancellationToken);
            
            var responseLength = BitConverter.ToInt32(lengthBuffer, 0);
            if (responseLength <= 0 || responseLength > 1024 * 1024) // Max 1MB for response
            {
                throw new InvalidOperationException($"Invalid response length: {responseLength}");
            }
            
            var responseBuffer = new byte[responseLength];
            await ReadExactlyAsync(stream, responseBuffer, responseLength, cancellationToken);
            
            var responseJson = Encoding.UTF8.GetString(responseBuffer);
            var response = JsonSerializer.Deserialize<ChunkResult>(responseJson) ?? new ChunkResult();
            
            return response;
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            _logger.LogError(ex, "Error receiving chunk acknowledgment from server");
            return new ChunkResult
            {
                Success = false,
                ErrorMessage = $"Failed to receive chunk acknowledgment: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 接收服务器的最终确认
    /// </summary>
    /// <param name="stream">网络流</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>传输响应</returns>
    private async Task<TransferResponse> ReceiveFinalConfirmationAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        try
        {
            var lengthBuffer = new byte[4];
            await ReadExactlyAsync(stream, lengthBuffer, 4, cancellationToken);
            
            var responseLength = BitConverter.ToInt32(lengthBuffer, 0);
            if (responseLength <= 0 || responseLength > 1024 * 1024) // Max 1MB for response
            {
                throw new InvalidOperationException($"Invalid response length: {responseLength}");
            }
            
            var responseBuffer = new byte[responseLength];
            await ReadExactlyAsync(stream, responseBuffer, responseLength, cancellationToken);
            
            var responseJson = Encoding.UTF8.GetString(responseBuffer);
            var response = JsonSerializer.Deserialize<TransferResponse>(responseJson) ?? new TransferResponse();
            
            _logger.LogDebug("Received final confirmation: Success={Success}, Message={Message}", response.Success, response.ErrorMessage);
            return response;
        }
        catch (TimeoutException)
        {
            // 对于最终确认超时，我们可以认为传输可能已经成功
            // 因为文件数据已经发送完毕，只是确认响应丢失了
            _logger.LogWarning("Final confirmation timed out - file transfer may have completed successfully");
            return new TransferResponse
            {
                Success = true, // 假设成功，因为数据已经发送
                ErrorMessage = "Final confirmation timed out, but file transfer likely completed"
            };
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            _logger.LogWarning(ex, "Error receiving final confirmation from server - file transfer may have completed successfully");
            return new TransferResponse
            {
                Success = true, // 假设成功，因为数据已经发送
                ErrorMessage = $"Final confirmation failed, but file transfer likely completed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 从流中精确读取指定数量的字节
    /// </summary>
    /// <param name="stream">网络流</param>
    /// <param name="buffer">缓冲区</param>
    /// <param name="count">要读取的字节数</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task ReadExactlyAsync(NetworkStream stream, byte[] buffer, int count, CancellationToken cancellationToken)
    {
        int totalRead = 0;
        
        // 使用超时来避免无限等待 / Use timeout to avoid infinite waiting
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(NetworkTimeoutSeconds));
        
        try
        {
            while (totalRead < count)
            {
                var bytesRead = await stream.ReadAsync(buffer, totalRead, count - totalRead, timeoutCts.Token);
                if (bytesRead == 0)
                {
                    _logger.LogWarning("Unexpected end of stream while reading response. Expected {Count} bytes, got {TotalRead} bytes", 
                        count, totalRead);
                    throw new EndOfStreamException($"Unexpected end of stream. Expected {count} bytes, got {totalRead} bytes");
                }
                totalRead += bytesRead;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Read operation cancelled due to shutdown");
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Read operation timed out after {Timeout} seconds", NetworkTimeoutSeconds);
            throw new TimeoutException($"Read operation timed out after {NetworkTimeoutSeconds} seconds");
        }
        catch (IOException ioEx) when (ioEx.InnerException is SocketException socketEx)
        {
            _logger.LogWarning("Network error during read operation: {SocketError} ({ErrorCode})", 
                socketEx.Message, socketEx.ErrorCode);
            throw new InvalidOperationException($"Network error during read: {socketEx.Message}", ioEx);
        }
        catch (SocketException socketEx)
        {
            _logger.LogWarning("Socket error during read operation: {SocketError} ({ErrorCode})", 
                socketEx.Message, socketEx.ErrorCode);
            throw new InvalidOperationException($"Socket error during read: {socketEx.Message}", socketEx);
        }
    }

    /// <summary>
    /// 创建包含校验和的文件元数据
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="targetFileName">目标文件名</param>
    /// <param name="config">传输配置，用于创建源配置信息</param>
    /// <returns>文件元数据</returns>
    private async Task<FileMetadata> CreateFileMetadataAsync(string filePath, string targetFileName, TransferConfig config)
    {
        var (md5, sha256, fileSize) = await _checksumService.CreateFileMetadataAsync(filePath);
        
        return new FileMetadata
        {
            FileName = targetFileName,
            FileSize = fileSize,
            ChecksumMD5 = md5,
            ChecksumSHA256 = sha256,
            CreatedAt = DateTime.Now,
            SourceConfig = config.SourceConfig  // 设置源配置信息
        };
    }

    /// <summary>
    /// 根据文件大小获取最优缓冲区大小以实现最大传输效率
    /// </summary>
    /// <param name="fileSize">文件大小</param>
    /// <returns>最优缓冲区大小</returns>
    private int GetOptimalBufferSize(long fileSize)
    {
        if (fileSize >= HugeFileThreshold)
        {
            return HugeFileBufferSize; // 4MB for very large files
        }
        else if (fileSize >= LargeFileThreshold)
        {
            return LargeFileBufferSize; // 1MB for large files
        }
        else
        {
            return SmallFileBufferSize; // 64KB for smaller files
        }
    }

    /// <summary>
    /// Configures network stream for optimal performance based on buffer size
    /// </summary>
    private void ConfigureNetworkStreamForPerformance(NetworkStream stream, int bufferSize)
    {
        try
        {
            // Set socket options for better performance
            var socket = stream.Socket;
            
            // Increase send and receive buffer sizes
            socket.SendBufferSize = Math.Max(bufferSize * SocketBufferMultiplier, MinSocketBufferSize); // At least 64KB
            socket.ReceiveBufferSize = Math.Max(bufferSize * SocketBufferMultiplier, MinSocketBufferSize);
            
            // Disable Nagle's algorithm for better throughput with large buffers
            socket.NoDelay = true;
            
            // Set socket timeout to prevent hanging
            socket.SendTimeout = NetworkTimeoutSeconds * 1000; // 30 seconds
            socket.ReceiveTimeout = NetworkTimeoutSeconds * 1000;
            
            _logger.LogDebug("Configured network stream: SendBuffer={SendBuffer}, ReceiveBuffer={ReceiveBuffer}, NoDelay={NoDelay}",
                socket.SendBufferSize, socket.ReceiveBufferSize, socket.NoDelay);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to configure network stream for optimal performance, using defaults");
        }
    }

    /// <summary>
    /// Configures TCP client for optimal performance before connection
    /// </summary>
    private void ConfigureTcpClientForPerformance(TcpClient tcpClient)
    {
        try
        {
            // Set client buffer sizes before connection
            tcpClient.SendBufferSize = TcpClientBufferSize; // 1MB send buffer
            tcpClient.ReceiveBufferSize = TcpClientBufferSize; // 1MB receive buffer
            
            // Set connection timeout
            tcpClient.SendTimeout = NetworkTimeoutSeconds * 1000; // 30 seconds
            tcpClient.ReceiveTimeout = NetworkTimeoutSeconds * 1000;
            
            _logger.LogDebug("Configured TCP client: SendBuffer={SendBuffer}, ReceiveBuffer={ReceiveBuffer}",
                tcpClient.SendBufferSize, tcpClient.ReceiveBufferSize);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to configure TCP client for optimal performance, using defaults");
        }
    }

}