using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Diagnostics;

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
    private readonly ISecureCredentialStorage _credentialStorage;
    private readonly IAuthenticationAuditService _auditService;
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
    /// <param name="auditService">审计服务 / Audit service</param>
    /// <exception cref="ArgumentNullException">当必需参数为null时抛出 / Thrown when required parameters are null</exception>
    public FileReceiver(
        ILogger<FileReceiver> logger,
        IStorageManager storageManager,
        IChunkManager chunkManager,
        IChecksumService checksumService,
        IAuthenticationService authenticationService,
        IAuthorizationService authorizationService,
        ISecureCredentialStorage credentialStorage,
        IAuthenticationAuditService auditService)
    {
        _logger = logger;
        _storageManager = storageManager;
        _chunkManager = chunkManager;
        _checksumService = checksumService ?? throw new ArgumentNullException(nameof(checksumService));
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _credentialStorage = credentialStorage ?? throw new ArgumentNullException(nameof(credentialStorage));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
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
    /// 验证身份验证令牌 / Validates authentication token
    /// 解码base64令牌并验证格式和凭据 / Decodes base64 token and validates format and credentials
    /// </summary>
    /// <param name="token">Base64编码的身份验证令牌 / Base64-encoded authentication token</param>
    /// <returns>身份验证结果 / Authentication result</returns>
    public async Task<AuthenticationResult> ValidateTokenAsync(string token)
    {
        var stopwatch = Stopwatch.StartNew();
        string? clientId = null;

        try
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                const string errorMsg = "Authentication token is required";
                _logger.LogWarning("Authentication failed: empty or null token");
                
                // 记录审计日志
                await _auditService.LogAuthenticationEventAsync(
                    AuthenticationAuditLog.Failure(null, AuthenticationOperation.TokenValidation, 
                        "AUTH_001", errorMsg, stopwatch.ElapsedMilliseconds));
                
                return AuthenticationResult.Failure(errorMsg);
            }

            // 解码base64令牌 / Decode base64 token
            byte[] decodedBytes;
            try
            {
                decodedBytes = Convert.FromBase64String(token);
            }
            catch (FormatException ex)
            {
                const string errorMsg = "Invalid token format";
                _logger.LogWarning(ex, "Authentication failed: invalid base64 token format");
                
                // 记录审计日志
                await _auditService.LogAuthenticationEventAsync(
                    AuthenticationAuditLog.Failure(null, AuthenticationOperation.TokenValidation, 
                        "AUTH_002", errorMsg, stopwatch.ElapsedMilliseconds));
                
                return AuthenticationResult.Failure(errorMsg);
            }

            var credentials = Encoding.UTF8.GetString(decodedBytes);

            // 解析clientId:clientSecret格式 / Parse clientId:clientSecret format
            var parts = credentials.Split(':', 2);
            if (parts.Length != 2)
            {
                const string errorMsg = "Invalid token format";
                _logger.LogWarning("Authentication failed: token does not match clientId:clientSecret format");
                
                // 记录审计日志
                await _auditService.LogAuthenticationEventAsync(
                    AuthenticationAuditLog.Failure(null, AuthenticationOperation.TokenValidation, 
                        "AUTH_003", errorMsg, stopwatch.ElapsedMilliseconds));
                
                return AuthenticationResult.Failure(errorMsg);
            }

            clientId = parts[0];
            var clientSecret = parts[1];

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                const string errorMsg = "Invalid credentials format";
                _logger.LogWarning("Authentication failed: empty clientId or clientSecret");
                
                // 记录审计日志
                await _auditService.LogAuthenticationEventAsync(
                    AuthenticationAuditLog.Failure(clientId, AuthenticationOperation.TokenValidation, 
                        "AUTH_004", errorMsg, stopwatch.ElapsedMilliseconds));
                
                return AuthenticationResult.Failure(errorMsg);
            }

            // 验证凭据 / Validate credentials
            var isValid = await _credentialStorage.ValidateCredentialsAsync(clientId, clientSecret);

            if (isValid)
            {
                _logger.LogInformation("Authentication successful for client {ClientId}", clientId);
                
                // 记录成功的审计日志
                await _auditService.LogAuthenticationEventAsync(
                    AuthenticationAuditLog.Success(clientId, AuthenticationOperation.TokenValidation, stopwatch.ElapsedMilliseconds));
                
                return AuthenticationResult.Success(clientId);
            }
            else
            {
                const string errorMsg = "Invalid credentials";
                _logger.LogWarning("Authentication failed for client {ClientId}: invalid credentials", clientId);
                
                // 记录失败的审计日志
                await _auditService.LogAuthenticationEventAsync(
                    AuthenticationAuditLog.Failure(clientId, AuthenticationOperation.TokenValidation, 
                        "AUTH_005", errorMsg, stopwatch.ElapsedMilliseconds));
                
                return AuthenticationResult.Failure(errorMsg);
            }
        }
        catch (Exception ex)
        {
            const string errorMsg = "Token validation error";
            _logger.LogError(ex, "Error validating authentication token");
            
            // 记录审计日志
            await _auditService.LogAuthenticationEventAsync(
                AuthenticationAuditLog.Failure(clientId, AuthenticationOperation.TokenValidation, 
                    "AUTH_006", $"{errorMsg}: {ex.Message}", stopwatch.ElapsedMilliseconds));
            
            return AuthenticationResult.Failure(errorMsg);
        }
    }

    /// <summary>
    /// 从客户端接收文件（用于直接文件接收） / Receives a file from a client (used for direct file reception)
    /// </summary>
    /// <param name="request">接收请求 / Receive request</param>
    /// <returns>接收结果 / Receive result</returns>
    public async Task<ReceiveResult> ReceiveFileAsync(ReceiveRequest request)
    {
        var startTime = DateTime.Now;
        
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
                Duration = DateTime.Now - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving file {FileName}", request.Metadata.FileName);
            return new ReceiveResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = DateTime.Now - startTime
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
                
                // 检查连接状态 / Check connection status
                if (!client.Connected)
                {
                    _logger.LogWarning("Client {Client} disconnected before processing", clientEndpoint);
                    return;
                }
                
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
                
                // 检查连接是否仍然有效再发送响应 / Check if connection is still valid before sending response
                if (client.Connected && stream.CanWrite)
                {
                    // 向客户端发送响应 / Send response back to client
                    await SendResponseAsync(stream, result, cancellationToken);
                }
                else
                {
                    _logger.LogInformation("Client {Client} disconnected before final response could be sent. Transfer result: {Success}", 
                        clientEndpoint, result.Success);
                }

                _logger.LogInformation("Completed file transfer for {FileName} from client {Client}. Success: {Success}", 
                    request.Metadata.FileName, clientEndpoint, result.Success);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Client handling cancelled for {Client}", clientEndpoint);
        }
        catch (IOException ioEx) when (ioEx.InnerException is SocketException socketEx)
        {
            // 网络连接问题是常见的，特别是在文件传输完成后 / Network issues are common, especially after file transfer completion
            _logger.LogInformation("Network error handling client {Client}: {SocketError} ({ErrorCode})", 
                clientEndpoint, socketEx.Message, socketEx.ErrorCode);
        }
        catch (SocketException socketEx)
        {
            _logger.LogInformation("Socket error handling client {Client}: {SocketError} ({ErrorCode})", 
                clientEndpoint, socketEx.Message, socketEx.ErrorCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error handling client {Client}", clientEndpoint);
        }
    }

    /// <summary>
    /// Reads a transfer request from the client stream
    /// </summary>
    private async Task<TransferRequest?> ReadTransferRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        try
        {
            // 使用超时来避免无限等待 / Use timeout to avoid infinite waiting
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30)); // 30秒超时

            // Read message length (4 bytes)
            var lengthBuffer = new byte[4];
            var bytesRead = await stream.ReadAsync(lengthBuffer, 0, 4, timeoutCts.Token);
            if (bytesRead != 4)
            {
                _logger.LogWarning("Expected 4 bytes for message length, got {BytesRead}", bytesRead);
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
                    messageLength - totalBytesRead, timeoutCts.Token);
                if (bytesRead == 0)
                {
                    _logger.LogWarning("Connection closed while reading transfer request");
                    return null; // Connection closed
                }
                totalBytesRead += bytesRead;
            }

            var json = Encoding.UTF8.GetString(messageBuffer);
            var request = JsonSerializer.Deserialize<TransferRequest>(json);
            
            if (request == null)
            {
                _logger.LogWarning("Failed to deserialize transfer request");
            }
            
            return request;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Transfer request reading cancelled due to shutdown");
            return null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Transfer request reading timed out");
            return null;
        }
        catch (IOException ioEx) when (ioEx.InnerException is SocketException socketEx)
        {
            _logger.LogInformation("Network error reading transfer request: {SocketError} ({ErrorCode})", 
                socketEx.Message, socketEx.ErrorCode);
            return null;
        }
        catch (SocketException socketEx)
        {
            _logger.LogInformation("Socket error reading transfer request: {SocketError} ({ErrorCode})", 
                socketEx.Message, socketEx.ErrorCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error reading transfer request");
            return null;
        }
    }

    /// <summary>
    /// Processes the file transfer from the client
    /// </summary>
    private async Task<ReceiveResult> ProcessFileTransferAsync(NetworkStream stream, TransferRequest request, CancellationToken cancellationToken)
    {
        var startTime = DateTime.Now;
        
        try
        {
            // 身份验证和授权请求 / Authenticate and authorize the request
            AuthorizationContext? authContext = null;
            
            // 使用新的ValidateTokenAsync方法进行身份验证 / Use new ValidateTokenAsync method for authentication
            var authResult = await ValidateTokenAsync(request.AuthenticationToken);
            
            if (!authResult.IsSuccess)
            {
                // 记录身份验证失败，不暴露敏感信息 / Log authentication failure without exposing sensitive information
                _logger.LogWarning("Authentication failed for token validation: {ErrorMessage}", authResult.ErrorMessage);
                
                await SendTransferResponseAsync(stream, false, "Authentication failed", cancellationToken);
                return new ReceiveResult
                {
                    Success = false,
                    ErrorMessage = "Authentication failed"
                };
            }

            // 获取客户端凭据以构建授权上下文 / Get client credentials to build authorization context
            var clientCredentials = await _credentialStorage.GetCredentialsByClientIdAsync(authResult.ClientId!);
            if (clientCredentials == null)
            {
                _logger.LogError("Client credentials not found for authenticated client {ClientId}", authResult.ClientId);
                
                await SendTransferResponseAsync(stream, false, "Authorization failed", cancellationToken);
                return new ReceiveResult
                {
                    Success = false,
                    ErrorMessage = "Authorization failed"
                };
            }

            authContext = new AuthorizationContext
            {
                ClientId = authResult.ClientId!,
                Permissions = new List<string>(clientCredentials.Permissions),
                RequestTime = DateTime.Now,
                Operation = "upload_backup"
            };

            // 检查上传操作的授权 / Check authorization for upload operation
            var isAuthorized = await _authorizationService.IsAuthorizedAsync(authContext, "upload_backup");
            if (!isAuthorized)
            {
                // 记录授权失败，包含客户端ID用于审计 / Log authorization failure with client ID for audit
                _logger.LogWarning("Authorization failed for client {ClientId}: insufficient permissions for backup upload", authContext.ClientId);
                
                await SendTransferResponseAsync(stream, false, "Insufficient permissions for backup upload", cancellationToken);
                return new ReceiveResult
                {
                    Success = false,
                    ErrorMessage = "Authorization failed"
                };
            }

            // 记录成功的身份验证和授权 / Log successful authentication and authorization
            _logger.LogInformation("Successfully authenticated and authorized file transfer request from client {ClientId} for {FileName}", 
                authContext.ClientId, request.Metadata.FileName);

            // Create backup path using client's target directory if available
            var backupMetadata = new BackupMetadata
            {
                ServerName = request.Metadata.SourceConfig?.Name ?? authContext.ClientId,
                DatabaseName = "MySQL", // Default for now
                BackupTime = request.Metadata.CreatedAt,
                BackupType = "Full",
                EstimatedSize = request.Metadata.FileSize
            };

            // Extract target directory from client configuration
            string? clientTargetDirectory = request.Metadata.SourceConfig?.TargetDirectory;
            
            // Use client's target directory if provided, otherwise use server's default
            var targetPath = await _storageManager.CreateBackupPathAsync(backupMetadata, clientTargetDirectory);
            
            _logger.LogInformation("Using {DirectoryType} target directory for backup storage: {Directory}", 
                !string.IsNullOrWhiteSpace(clientTargetDirectory) ? "client-specified" : "server-default",
                !string.IsNullOrWhiteSpace(clientTargetDirectory) ? clientTargetDirectory : "default server path");
            
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
                Duration = DateTime.Now - startTime
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
                Duration = DateTime.Now - startTime
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
            // 检查连接是否仍然有效 / Check if connection is still valid
            if (!stream.CanWrite || !stream.Socket.Connected)
            {
                _logger.LogWarning("Cannot send transfer response: connection is closed or not writable");
                return;
            }

            var response = new TransferResponse
            {
                Success = success,
                ErrorMessage = message,
                AdditionalInfo = resumeInfo
            };
            
            var json = JsonSerializer.Serialize(response);
            var messageBytes = Encoding.UTF8.GetBytes(json);
            var lengthBytes = BitConverter.GetBytes(messageBytes.Length);

            // 使用超时发送响应 / Send response with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10)); // 10秒超时

            await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length, timeoutCts.Token);
            await stream.WriteAsync(messageBytes, 0, messageBytes.Length, timeoutCts.Token);
            await stream.FlushAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Transfer response sending cancelled due to shutdown");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Transfer response sending timed out - client may have disconnected");
        }
        catch (IOException ioEx) when (ioEx.InnerException is SocketException socketEx)
        {
            _logger.LogInformation("Client disconnected during transfer response sending: {SocketError} ({ErrorCode})", 
                socketEx.Message, socketEx.ErrorCode);
        }
        catch (SocketException socketEx)
        {
            _logger.LogInformation("Network error sending transfer response: {SocketError} ({ErrorCode})", 
                socketEx.Message, socketEx.ErrorCode);
        }
        catch (ObjectDisposedException)
        {
            _logger.LogInformation("Cannot send transfer response: stream has been disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending transfer response");
        }
    }

    /// <summary>
    /// Sends a chunk response to the client
    /// </summary>
    private async Task SendChunkResponseAsync(NetworkStream stream, bool success, string? message, int chunkIndex, CancellationToken cancellationToken)
    {
        try
        {
            // 检查连接是否仍然有效 / Check if connection is still valid
            if (!stream.CanWrite || !stream.Socket.Connected)
            {
                _logger.LogWarning("Cannot send chunk response: connection is closed or not writable");
                return;
            }

            var response = new ChunkResult
            {
                Success = success,
                ErrorMessage = message,
                ChunkIndex = chunkIndex
            };
            
            var json = JsonSerializer.Serialize(response);
            var messageBytes = Encoding.UTF8.GetBytes(json);
            var lengthBytes = BitConverter.GetBytes(messageBytes.Length);

            // 使用超时发送响应 / Send response with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5)); // 5秒超时，块响应应该更快

            await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length, timeoutCts.Token);
            await stream.WriteAsync(messageBytes, 0, messageBytes.Length, timeoutCts.Token);
            await stream.FlushAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Chunk response sending cancelled due to shutdown");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Chunk response sending timed out for chunk {ChunkIndex} - client may have disconnected", chunkIndex);
        }
        catch (IOException ioEx) when (ioEx.InnerException is SocketException socketEx)
        {
            _logger.LogInformation("Client disconnected during chunk response sending for chunk {ChunkIndex}: {SocketError} ({ErrorCode})", 
                chunkIndex, socketEx.Message, socketEx.ErrorCode);
        }
        catch (SocketException socketEx)
        {
            _logger.LogInformation("Network error sending chunk response for chunk {ChunkIndex}: {SocketError} ({ErrorCode})", 
                chunkIndex, socketEx.Message, socketEx.ErrorCode);
        }
        catch (ObjectDisposedException)
        {
            _logger.LogInformation("Cannot send chunk response for chunk {ChunkIndex}: stream has been disposed", chunkIndex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending chunk response for chunk {ChunkIndex}", chunkIndex);
        }
    }

    /// <summary>
    /// Sends a response back to the client
    /// </summary>
    private async Task SendResponseAsync(NetworkStream stream, ReceiveResult result, CancellationToken cancellationToken)
    {
        try
        {
            // 检查连接是否仍然有效 / Check if connection is still valid
            if (!stream.CanWrite || !stream.Socket.Connected)
            {
                _logger.LogWarning("Cannot send response: connection is closed or not writable");
                return;
            }

            var response = new TransferResponse
            {
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                AdditionalInfo = result.FilePath
            };
            
            var json = JsonSerializer.Serialize(response);
            var messageBytes = Encoding.UTF8.GetBytes(json);
            var lengthBytes = BitConverter.GetBytes(messageBytes.Length);

            // 使用超时发送响应 / Send response with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10)); // 10秒超时

            // Send message length first
            await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length, timeoutCts.Token);
            
            // Send message content
            await stream.WriteAsync(messageBytes, 0, messageBytes.Length, timeoutCts.Token);
            
            await stream.FlushAsync(timeoutCts.Token);
            
            _logger.LogDebug("Successfully sent response to client");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Response sending cancelled due to shutdown");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Response sending timed out - client may have disconnected");
        }
        catch (IOException ioEx) when (ioEx.InnerException is SocketException socketEx)
        {
            // 网络连接问题，这是常见的，不需要记录为错误 / Network connection issues are common, don't log as error
            _logger.LogInformation("Client disconnected during response sending: {SocketError} ({ErrorCode})", 
                socketEx.Message, socketEx.ErrorCode);
        }
        catch (SocketException socketEx)
        {
            // 直接的Socket异常 / Direct socket exceptions
            _logger.LogInformation("Network error sending response to client: {SocketError} ({ErrorCode})", 
                socketEx.Message, socketEx.ErrorCode);
        }
        catch (ObjectDisposedException)
        {
            _logger.LogInformation("Cannot send response: stream has been disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending response to client");
        }
    }

    public void Dispose()
    {
        StopListeningAsync().GetAwaiter().GetResult();
        _cancellationTokenSource?.Dispose();
    }
}