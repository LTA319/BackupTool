using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 传输操作期间来自服务器的响应 / Response from server during transfer operations
/// </summary>
public class TransferResponse
{
    /// <summary>
    /// 操作是否成功 / Whether operation was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// 错误消息（如果有） / Error message (if any)
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 附加信息 / Additional information
    /// </summary>
    public string? AdditionalInfo { get; set; }
}

/// <summary>
/// 用于接收备份文件的TCP服务器实现 / TCP server implementation for receiving backup files
/// </summary>
public class FileReceiver : IFileReceiver, IDisposable
{
    private readonly ILogger<FileReceiver> _logger;
    private readonly IStorageManager _storageManager;
    private readonly IChunkManager _chunkManager;
    private readonly IChecksumService _checksumService;
    private readonly IAuthenticationService _authenticationService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICredentialStorage _credentialStorage;
    private TcpListener? _tcpListener;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly List<Task> _clientTasks = new();
    private bool _isListening = false;
    private readonly object _lockObject = new();

    /// <summary>
    /// 初始化文件接收器 / Initialize file receiver
    /// </summary>
    /// <param name="logger">日志记录器 / Logger instance</param>
    /// <param name="storageManager">存储管理器 / Storage manager</param>
    /// <param name="chunkManager">块管理器 / Chunk manager</param>
    /// <param name="checksumService">校验和服务 / Checksum service</param>
    /// <param name="authenticationService">身份验证服务 / Authentication service</param>
    /// <param name="authorizationService">授权服务 / Authorization service</param>
    /// <param name="credentialStorage">凭据存储 / Credential storage</param>
    /// <exception cref="ArgumentNullException">当必需参数为null时抛出 / Thrown when required parameters are null</exception>
    public FileReceiver(
        ILogger<FileReceiver> logger,
        IStorageManager storageManager,
        IChunkManager chunkManager,
        IChecksumService checksumService,
        IAuthenticationService authenticationService,
        IAuthorizationService authorizationService,
        ICredentialStorage credentialStorage)
    {
        _logger = logger;
        _storageManager = storageManager;
        _chunkManager = chunkManager;
        _checksumService = checksumService ?? throw new ArgumentNullException(nameof(checksumService));
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _credentialStorage = credentialStorage ?? throw new ArgumentNullException(nameof(credentialStorage));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    }

    /// <summary>
    /// 开始监听传入的文件传输连接 / Starts listening for incoming file transfer connections
    /// </summary>
    /// <param name="port">监听端口 / Port to listen on</param>
    /// <exception cref="Exception">启动监听失败时抛出 / Thrown when starting listener fails</exception>
    public async Task StartListeningAsync(int port)
    {
        lock (_lockObject)
        {
            if (_isListening)
            {
                _logger.LogWarning("File receiver is already listening on port {Port}", port);
                return;
            }

            _tcpListener = new TcpListener(IPAddress.Any, port);
            _cancellationTokenSource = new CancellationTokenSource();
            _isListening = true;
        }

        try
        {
            _tcpListener.Start();
            _logger.LogInformation("File receiver started listening on port {Port}", port);

            // 在后台开始接受客户端 / Start accepting clients in background
            _ = Task.Run(async () => await AcceptClientsAsync(_cancellationTokenSource.Token));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start file receiver on port {Port}", port);
            lock (_lockObject)
            {
                _isListening = false;
            }
            throw;
        }
    }

    /// <summary>
    /// 停止监听传入连接 / Stops listening for incoming connections
    /// </summary>
    /// <exception cref="Exception">停止监听失败时抛出 / Thrown when stopping listener fails</exception>
    public async Task StopListeningAsync()
    {
        lock (_lockObject)
        {
            if (!_isListening)
            {
                _logger.LogWarning("File receiver is not currently listening");
                return;
            }

            _isListening = false;
            _cancellationTokenSource?.Cancel();
        }

        try
        {
            _tcpListener?.Stop();
            _logger.LogInformation("File receiver stopped listening");

            // 等待所有客户端任务完成 / Wait for all client tasks to complete
            if (_clientTasks.Count > 0)
            {
                _logger.LogInformation("Waiting for {Count} client connections to complete", _clientTasks.Count);
                await Task.WhenAll(_clientTasks);
                _clientTasks.Clear();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping file receiver");
            throw;
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    /// <summary>
    /// 从客户端接收文件（用于直接文件接收） / Receives a file from a client (used for direct file reception)
    /// </summary>
    /// <param name="request">接收请求 / Receive request</param>
    /// <returns>接收结果 / Receive result</returns>
    public async Task<ReceiveResult> ReceiveFileAsync(ReceiveRequest request)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Starting file reception for {FileName} (Transfer ID: {TransferId})", 
                request.Metadata.FileName, request.TransferId);

            // 验证存储空间 / Validate storage space
            var hasSpace = await _storageManager.ValidateStorageSpaceAsync(request.Metadata.FileSize);
            if (!hasSpace)
            {
                return new ReceiveResult
                {
                    Success = false,
                    ErrorMessage = "Insufficient storage space available"
                };
            }

            // 如果目标目录不存在则创建 / Create target directory if it doesn't exist
            var targetDir = Path.GetDirectoryName(request.TargetPath);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            return new ReceiveResult
            {
                Success = true,
                FilePath = request.TargetPath,
                BytesReceived = request.Metadata.FileSize,
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving file {FileName}", request.Metadata.FileName);
            return new ReceiveResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// 接受传入的客户端连接 / Accepts incoming client connections
    /// </summary>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _isListening)
            {
                try
                {
                    var tcpClient = await _tcpListener!.AcceptTcpClientAsync();
                    _logger.LogInformation("Accepted client connection from {RemoteEndPoint}", 
                        tcpClient.Client.RemoteEndPoint);

                    // 在后台任务中处理客户端 / Handle client in background task
                    var clientTask = HandleClientAsync(tcpClient, cancellationToken);
                    _clientTasks.Add(clientTask);

                    // 清理已完成的任务 / Clean up completed tasks
                    _clientTasks.RemoveAll(t => t.IsCompleted);
                }
                catch (ObjectDisposedException)
                {
                    // 停止监听器时预期的异常 / Expected when stopping the listener
                    break;
                }
                catch (InvalidOperationException)
                {
                    // 停止监听器时预期的异常 / Expected when stopping the listener
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error accepting client connection");
                    await Task.Delay(1000, cancellationToken); // 重试前短暂延迟 / Brief delay before retrying
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Client acceptance loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in client acceptance loop");
        }
    }

    /// <summary>
    /// 处理与已连接客户端的通信 / Handles communication with a connected client
    /// </summary>
    /// <param name="client">TCP客户端 / TCP client</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var clientEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
        
        try
        {
            using (client)
            {
                client.ReceiveTimeout = 30000; // 30秒超时 / 30 second timeout
                client.SendTimeout = 30000;

                using var stream = client.GetStream();
                
                // 读取传输请求 / Read the transfer request
                var request = await ReadTransferRequestAsync(stream, cancellationToken);
                if (request == null)
                {
                    _logger.LogWarning("Failed to read transfer request from client {Client}", clientEndpoint);
                    return;
                }

                _logger.LogInformation("Received transfer request for {FileName} from client {Client}", 
                    request.Metadata.FileName, clientEndpoint);

                // 处理文件传输 / Process the file transfer
                var result = await ProcessFileTransferAsync(stream, request, cancellationToken);
                
                // 向客户端发送响应 / Send response back to client
                await SendResponseAsync(stream, result, cancellationToken);

                _logger.LogInformation("Completed file transfer for {FileName} from client {Client}. Success: {Success}", 
                    request.Metadata.FileName, clientEndpoint, result.Success);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Client handling cancelled for {Client}", clientEndpoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client {Client}", clientEndpoint);
        }
    }

    /// <summary>
    /// Reads a transfer request from the client stream
    /// </summary>
    private async Task<TransferRequest?> ReadTransferRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        try
        {
            // Read message length (4 bytes)
            var lengthBuffer = new byte[4];
            var bytesRead = await stream.ReadAsync(lengthBuffer, 0, 4, cancellationToken);
            if (bytesRead != 4)
            {
                return null;
            }

            var messageLength = BitConverter.ToInt32(lengthBuffer, 0);
            if (messageLength <= 0 || messageLength > 1024 * 1024) // Max 1MB for request
            {
                _logger.LogWarning("Invalid message length: {Length}", messageLength);
                return null;
            }

            // Read message content
            var messageBuffer = new byte[messageLength];
            var totalBytesRead = 0;
            while (totalBytesRead < messageLength)
            {
                bytesRead = await stream.ReadAsync(messageBuffer, totalBytesRead, 
                    messageLength - totalBytesRead, cancellationToken);
                if (bytesRead == 0)
                {
                    return null; // Connection closed
                }
                totalBytesRead += bytesRead;
            }

            var json = Encoding.UTF8.GetString(messageBuffer);
            return JsonSerializer.Deserialize<TransferRequest>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading transfer request");
            return null;
        }
    }

    /// <summary>
    /// Processes the file transfer from the client
    /// </summary>
    private async Task<ReceiveResult> ProcessFileTransferAsync(NetworkStream stream, TransferRequest request, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Authenticate and authorize the request
            AuthorizationContext? authContext = null;
            
            // Try to decode the authentication token as base64-encoded credentials
            try
            {
                var credentialsBytes = Convert.FromBase64String(request.AuthenticationToken);
                var credentialsString = System.Text.Encoding.UTF8.GetString(credentialsBytes);
                var parts = credentialsString.Split(':', 2);
                
                if (parts.Length == 2)
                {
                    var clientId = parts[0];
                    var clientSecret = parts[1];
                    
                    // Validate credentials directly
                    var storedCredentials = await _credentialStorage.GetCredentialsAsync(clientId);
                    if (storedCredentials != null && 
                        storedCredentials.IsActive && 
                        !storedCredentials.IsExpired &&
                        storedCredentials.VerifySecret(clientSecret, storedCredentials.ClientSecret))
                    {
                        authContext = new AuthorizationContext
                        {
                            ClientId = clientId,
                            Permissions = new List<string>(storedCredentials.Permissions),
                            RequestTime = DateTime.UtcNow
                        };
                        
                        _logger.LogInformation("Successfully authenticated client {ClientId} using direct credentials", clientId);
                    }
                    else
                    {
                        _logger.LogWarning("Authentication failed for client {ClientId}: invalid credentials or inactive client", clientId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to decode authentication token as base64 credentials, trying token validation");
                
                // Fallback to token-based authentication
                authContext = await _authenticationService.GetAuthorizationContextAsync(request.AuthenticationToken);
            }
            
            if (authContext == null)
            {
                await SendTransferResponseAsync(stream, false, "Invalid authentication token", cancellationToken);
                return new ReceiveResult
                {
                    Success = false,
                    ErrorMessage = "Authentication failed"
                };
            }

            // Check authorization for upload operation
            var isAuthorized = await _authorizationService.IsAuthorizedAsync(authContext, "upload_backup");
            if (!isAuthorized)
            {
                await SendTransferResponseAsync(stream, false, "Insufficient permissions for backup upload", cancellationToken);
                return new ReceiveResult
                {
                    Success = false,
                    ErrorMessage = "Authorization failed"
                };
            }

            _logger.LogInformation("Authenticated file transfer request from client {ClientId} for {FileName}", 
                authContext.ClientId, request.Metadata.FileName);

            // Create backup path
            var backupMetadata = new BackupMetadata
            {
                ServerName = request.Metadata.SourceConfig?.Name ?? authContext.ClientId,
                DatabaseName = "MySQL", // Default for now
                BackupTime = request.Metadata.CreatedAt,
                BackupType = "Full",
                EstimatedSize = request.Metadata.FileSize
            };

            var targetPath = await _storageManager.CreateBackupPathAsync(backupMetadata);
            
            // Validate storage space
            var hasSpace = await _storageManager.ValidateStorageSpaceAsync(request.Metadata.FileSize);
            if (!hasSpace)
            {
                await SendTransferResponseAsync(stream, false, "Insufficient storage space available", cancellationToken);
                return new ReceiveResult
                {
                    Success = false,
                    ErrorMessage = "Insufficient storage space available"
                };
            }

            // Send acknowledgment to client
            string? resumeInfo = null;
            string transferId;
            
            if (request.ResumeTransfer && !string.IsNullOrEmpty(request.ResumeToken))
            {
                try
                {
                    // Try to restore the transfer session
                    var restoredTransferId = await _chunkManager.RestoreTransferAsync(request.ResumeToken, request.Metadata);
                    transferId = restoredTransferId;
                    
                    // Get resume information to send to client
                    var resumeData = await _chunkManager.GetResumeInfoAsync(request.ResumeToken);
                    resumeInfo = JsonSerializer.Serialize(resumeData.CompletedChunks);
                    
                    _logger.LogInformation("Restored transfer session {TransferId} from resume token {ResumeToken}. Completed chunks: {Count}",
                        transferId, request.ResumeToken, resumeData.CompletedChunks.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to restore transfer from resume token {ResumeToken}, starting new transfer", request.ResumeToken);
                    // Fall back to new transfer
                    transferId = await _chunkManager.InitializeTransferAsync(request.Metadata);
                }
            }
            else
            {
                // Initialize new transfer session
                transferId = await _chunkManager.InitializeTransferAsync(request.Metadata);
            }
            
            await SendTransferResponseAsync(stream, true, "Ready to receive file", resumeInfo, cancellationToken);
            
            // Determine if this is a chunked transfer
            var shouldChunk = request.Metadata.FileSize > request.ChunkingStrategy.ChunkSize;
            
            long totalBytesReceived;
            string finalPath;
            
            if (shouldChunk)
            {
                totalBytesReceived = await ReceiveFileChunkedAsync(stream, transferId, request, cancellationToken);
                finalPath = await _chunkManager.FinalizeTransferAsync(transferId);
            }
            else
            {
                totalBytesReceived = await ReceiveFileDirectAsync(stream, targetPath, request.Metadata.FileSize, cancellationToken);
                finalPath = targetPath;
                
                // Validate file integrity for direct transfers
                var isValid = await _checksumService.ValidateFileIntegrityAsync(
                    finalPath, 
                    request.Metadata.ChecksumMD5, 
                    request.Metadata.ChecksumSHA256, 
                    cancellationToken);
                
                if (!isValid)
                {
                    // Delete the invalid file
                    try
                    {
                        File.Delete(finalPath);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "Failed to delete invalid file {FilePath}", finalPath);
                    }
                    
                    throw new InvalidOperationException("File integrity validation failed");
                }
            }

            // Send final confirmation
            await SendTransferResponseAsync(stream, true, "Transfer completed successfully", cancellationToken);

            return new ReceiveResult
            {
                Success = true,
                FilePath = finalPath,
                BytesReceived = totalBytesReceived,
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file transfer for {FileName}", request.Metadata.FileName);
            
            // Send error response to client
            try
            {
                await SendTransferResponseAsync(stream, false, ex.Message, cancellationToken);
            }
            catch (Exception sendEx)
            {
                _logger.LogWarning(sendEx, "Failed to send error response to client");
            }
            
            return new ReceiveResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// Receives file data directly without chunking
    /// </summary>
    private async Task<long> ReceiveFileDirectAsync(NetworkStream stream, string targetPath, long expectedSize, CancellationToken cancellationToken)
    {
        long totalBytesReceived = 0;
        var buffer = new byte[64 * 1024]; // 64KB buffer
        
        using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
        
        while (totalBytesReceived < expectedSize)
        {
            var bytesToRead = (int)Math.Min(buffer.Length, expectedSize - totalBytesReceived);
            var bytesRead = await stream.ReadAsync(buffer, 0, bytesToRead, cancellationToken);
            
            if (bytesRead == 0)
            {
                throw new InvalidOperationException("Unexpected end of stream during direct file reception");
            }

            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytesReceived += bytesRead;
            
            // Log progress every 10MB
            if (totalBytesReceived % (10 * 1024 * 1024) == 0 || totalBytesReceived == expectedSize)
            {
                var progress = (double)totalBytesReceived / expectedSize * 100;
                _logger.LogDebug("Direct reception progress: {Progress:F1}% ({Bytes}/{Total} bytes)", 
                    progress, totalBytesReceived, expectedSize);
            }
        }

        await fileStream.FlushAsync(cancellationToken);
        _logger.LogDebug("Completed direct file reception, total bytes: {TotalBytes}", totalBytesReceived);
        
        return totalBytesReceived;
    }

    /// <summary>
    /// Receives file data using chunking protocol
    /// </summary>
    private async Task<long> ReceiveFileChunkedAsync(NetworkStream stream, string transferId, TransferRequest request, CancellationToken cancellationToken)
    {
        long totalBytesReceived = 0;
        var expectedChunks = request.ChunkingStrategy.CalculateChunkCount(request.Metadata.FileSize);
        
        _logger.LogInformation("Starting chunked reception for transfer {TransferId}: expecting {ChunkCount} chunks", 
            transferId, expectedChunks);
        
        for (int expectedChunkIndex = 0; expectedChunkIndex < expectedChunks; expectedChunkIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Receive chunk
            var chunk = await ReceiveChunkAsync(stream, cancellationToken);
            if (chunk == null)
            {
                throw new InvalidOperationException($"Failed to receive chunk {expectedChunkIndex}");
            }
            
            // Validate chunk order
            if (chunk.ChunkIndex != expectedChunkIndex)
            {
                var errorMsg = $"Received chunk {chunk.ChunkIndex} but expected chunk {expectedChunkIndex}";
                await SendChunkResponseAsync(stream, false, errorMsg, chunk.ChunkIndex, cancellationToken);
                throw new InvalidOperationException(errorMsg);
            }
            
            // Process chunk
            var chunkResult = await _chunkManager.ReceiveChunkAsync(transferId, chunk);
            
            // Send chunk acknowledgment
            await SendChunkResponseAsync(stream, chunkResult.Success, chunkResult.ErrorMessage, chunk.ChunkIndex, cancellationToken);
            
            if (!chunkResult.Success)
            {
                throw new InvalidOperationException($"Failed to process chunk {chunk.ChunkIndex}: {chunkResult.ErrorMessage}");
            }
            
            totalBytesReceived += chunk.Data.Length;
            
            // Log progress
            var progress = (double)totalBytesReceived / request.Metadata.FileSize * 100;
            _logger.LogDebug("Received chunk {ChunkIndex}/{ChunkCount}: {Progress:F1}% ({Bytes}/{Total} bytes)", 
                chunk.ChunkIndex + 1, expectedChunks, progress, totalBytesReceived, request.Metadata.FileSize);
        }
        
        _logger.LogInformation("Completed chunked file reception for transfer {TransferId}, total bytes: {TotalBytes}", 
            transferId, totalBytesReceived);
        
        return totalBytesReceived;
    }

    /// <summary>
    /// Receives a single chunk from the client
    /// </summary>
    private async Task<ChunkData?> ReceiveChunkAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        try
        {
            // Read chunk header length (4 bytes)
            var lengthBuffer = new byte[4];
            var bytesRead = await stream.ReadAsync(lengthBuffer, 0, 4, cancellationToken);
            if (bytesRead != 4)
            {
                return null;
            }

            var chunkLength = BitConverter.ToInt32(lengthBuffer, 0);
            if (chunkLength <= 0 || chunkLength > 100 * 1024 * 1024) // Max 100MB for chunk metadata
            {
                _logger.LogWarning("Invalid chunk length: {Length}", chunkLength);
                return null;
            }

            // Read chunk data
            var chunkBuffer = new byte[chunkLength];
            var totalBytesRead = 0;
            while (totalBytesRead < chunkLength)
            {
                bytesRead = await stream.ReadAsync(chunkBuffer, totalBytesRead, 
                    chunkLength - totalBytesRead, cancellationToken);
                if (bytesRead == 0)
                {
                    return null; // Connection closed
                }
                totalBytesRead += bytesRead;
            }

            var json = Encoding.UTF8.GetString(chunkBuffer);
            return JsonSerializer.Deserialize<ChunkData>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving chunk");
            return null;
        }
    }

    /// <summary>
    /// Sends a transfer response to the client
    /// </summary>
    private async Task SendTransferResponseAsync(NetworkStream stream, bool success, string? message, CancellationToken cancellationToken)
    {
        await SendTransferResponseAsync(stream, success, message, null, cancellationToken);
    }

    /// <summary>
    /// Sends a transfer response to the client with optional resume information
    /// </summary>
    private async Task SendTransferResponseAsync(NetworkStream stream, bool success, string? message, string? resumeInfo, CancellationToken cancellationToken)
    {
        try
        {
            var response = new TransferResponse
            {
                Success = success,
                ErrorMessage = message,
                AdditionalInfo = resumeInfo
            };
            
            var json = JsonSerializer.Serialize(response);
            var messageBytes = Encoding.UTF8.GetBytes(json);
            var lengthBytes = BitConverter.GetBytes(messageBytes.Length);

            await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length, cancellationToken);
            await stream.WriteAsync(messageBytes, 0, messageBytes.Length, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending transfer response");
        }
    }

    /// <summary>
    /// Sends a chunk response to the client
    /// </summary>
    private async Task SendChunkResponseAsync(NetworkStream stream, bool success, string? message, int chunkIndex, CancellationToken cancellationToken)
    {
        try
        {
            var response = new ChunkResult
            {
                Success = success,
                ErrorMessage = message,
                ChunkIndex = chunkIndex
            };
            
            var json = JsonSerializer.Serialize(response);
            var messageBytes = Encoding.UTF8.GetBytes(json);
            var lengthBytes = BitConverter.GetBytes(messageBytes.Length);

            await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length, cancellationToken);
            await stream.WriteAsync(messageBytes, 0, messageBytes.Length, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending chunk response");
        }
    }

    /// <summary>
    /// Sends a response back to the client
    /// </summary>
    private async Task SendResponseAsync(NetworkStream stream, ReceiveResult result, CancellationToken cancellationToken)
    {
        try
        {
            var response = new TransferResponse
            {
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                AdditionalInfo = result.FilePath
            };
            
            var json = JsonSerializer.Serialize(response);
            var messageBytes = Encoding.UTF8.GetBytes(json);
            var lengthBytes = BitConverter.GetBytes(messageBytes.Length);

            // Send message length first
            await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length, cancellationToken);
            
            // Send message content
            await stream.WriteAsync(messageBytes, 0, messageBytes.Length, cancellationToken);
            
            await stream.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending response to client");
        }
    }

    public void Dispose()
    {
        StopListeningAsync().GetAwaiter().GetResult();
        _cancellationTokenSource?.Dispose();
    }
}