using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 支持SSL/TLS的TCP服务器实现，用于接收备份文件 / SSL/TLS-enabled TCP server implementation for receiving backup files
/// 提供安全的文件接收功能，支持SSL加密、客户端证书验证、分块传输和断点续传 / Provides secure file reception with SSL encryption, client certificate validation, chunked transfer, and resume capability
/// </summary>
public class SecureFileReceiver : IFileReceiver, IDisposable
{
    private readonly ILogger<SecureFileReceiver> _logger;
    private readonly IStorageManager _storageManager;
    private readonly IChunkManager _chunkManager;
    private readonly IChecksumService _checksumService;
    private TcpListener? _tcpListener;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly List<Task> _clientTasks = new();
    private bool _isListening = false;
    private readonly object _lockObject = new();
    private X509Certificate2? _serverCertificate; // 服务器SSL证书 / Server SSL certificate
    private readonly bool _requireClientCertificate; // 是否要求客户端证书 / Whether to require client certificate
    private readonly bool _useSSL; // 是否使用SSL加密 / Whether to use SSL encryption

    /// <summary>
    /// 构造函数，初始化安全文件接收器 / Constructor, initializes secure file receiver
    /// </summary>
    /// <param name="logger">日志服务 / Logger service</param>
    /// <param name="storageManager">存储管理器 / Storage manager</param>
    /// <param name="chunkManager">分块管理器 / Chunk manager</param>
    /// <param name="checksumService">校验和服务 / Checksum service</param>
    /// <param name="serverCertificate">服务器SSL证书（可选） / Server SSL certificate (optional)</param>
    /// <param name="requireClientCertificate">是否要求客户端证书 / Whether to require client certificate</param>
    /// <param name="useSSL">是否使用SSL加密 / Whether to use SSL encryption</param>
    public SecureFileReceiver(
        ILogger<SecureFileReceiver> logger,
        IStorageManager storageManager,
        IChunkManager chunkManager,
        IChecksumService checksumService,
        X509Certificate2? serverCertificate = null,
        bool requireClientCertificate = false,
        bool useSSL = true)
    {
        _logger = logger;
        _storageManager = storageManager;
        _chunkManager = chunkManager;
        _checksumService = checksumService ?? throw new ArgumentNullException(nameof(checksumService));
        _serverCertificate = serverCertificate;
        _requireClientCertificate = requireClientCertificate;
        _useSSL = useSSL;
    }

    /// <summary>
    /// 开始监听传入的文件传输连接 / Starts listening for incoming file transfer connections
    /// 在指定端口上启动TCP监听器，支持SSL加密连接 / Starts TCP listener on specified port with SSL encryption support
    /// </summary>
    /// <param name="port">监听端口 / Port to listen on</param>
    /// <returns>异步任务 / Async task</returns>
    public async Task StartListeningAsync(int port)
    {
        lock (_lockObject)
        {
            if (_isListening)
            {
                _logger.LogWarning("Secure file receiver is already listening on port {Port}", port);
                return;
            }

            _tcpListener = new TcpListener(IPAddress.Any, port);
            _cancellationTokenSource = new CancellationTokenSource();
            _isListening = true;
        }

        try
        {
            _tcpListener.Start();
            _logger.LogInformation("Secure file receiver started listening on port {Port} (SSL: {UseSSL})", port, _useSSL);

            // Start accepting clients in background
            _ = Task.Run(async () => await AcceptClientsAsync(_cancellationTokenSource.Token));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start secure file receiver on port {Port}", port);
            lock (_lockObject)
            {
                _isListening = false;
            }
            throw;
        }
    }

    /// <summary>
    /// 停止监听传入连接 / Stops listening for incoming connections
    /// 停止TCP监听器并等待所有客户端连接完成 / Stops TCP listener and waits for all client connections to complete
    /// </summary>
    /// <returns>异步任务 / Async task</returns>
    public async Task StopListeningAsync()
    {
        lock (_lockObject)
        {
            if (!_isListening)
            {
                _logger.LogWarning("Secure file receiver is not currently listening");
                return;
            }

            _isListening = false;
            _cancellationTokenSource?.Cancel();
        }

        try
        {
            _tcpListener?.Stop();
            _logger.LogInformation("Secure file receiver stopped listening");

            // Wait for all client tasks to complete
            if (_clientTasks.Count > 0)
            {
                _logger.LogInformation("Waiting for {Count} client connections to complete", _clientTasks.Count);
                await Task.WhenAll(_clientTasks);
                _clientTasks.Clear();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping secure file receiver");
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
    /// 验证存储空间并创建目标目录，处理文件接收请求 / Validates storage space and creates target directory, handles file reception request
    /// </summary>
    /// <param name="request">接收请求 / Receive request</param>
    /// <returns>接收结果 / Receive result</returns>
    public async Task<ReceiveResult> ReceiveFileAsync(ReceiveRequest request)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Starting secure file reception for {FileName} (Transfer ID: {TransferId})", 
                request.Metadata.FileName, request.TransferId);

            // Validate storage space
            var hasSpace = await _storageManager.ValidateStorageSpaceAsync(request.Metadata.FileSize);
            if (!hasSpace)
            {
                return new ReceiveResult
                {
                    Success = false,
                    ErrorMessage = "Insufficient storage space available"
                };
            }

            // Create target directory if it doesn't exist
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
            _logger.LogError(ex, "Error receiving secure file {FileName}", request.Metadata.FileName);
            return new ReceiveResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// 设置用于SSL/TLS连接的服务器证书 / Sets the server certificate for SSL/TLS connections
    /// </summary>
    /// <param name="certificate">X509证书 / X509 certificate</param>
    public void SetServerCertificate(X509Certificate2 certificate)
    {
        _serverCertificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
        _logger.LogInformation("Server certificate set: Subject={Subject}, Thumbprint={Thumbprint}", 
            certificate.Subject, certificate.Thumbprint);
    }

    /// <summary>
    /// 接受传入的客户端连接 / Accepts incoming client connections
    /// 在后台循环接受客户端连接并为每个连接创建处理任务 / Loops in background to accept client connections and creates handling task for each connection
    /// </summary>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>异步任务 / Async task</returns>
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

                    // Handle client in background task
                    var clientTask = HandleClientAsync(tcpClient, cancellationToken);
                    _clientTasks.Add(clientTask);

                    // Clean up completed tasks
                    _clientTasks.RemoveAll(t => t.IsCompleted);
                }
                catch (ObjectDisposedException)
                {
                    // Expected when stopping the listener
                    break;
                }
                catch (InvalidOperationException)
                {
                    // Expected when stopping the listener
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error accepting client connection");
                    await Task.Delay(1000, cancellationToken); // Brief delay before retrying
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
    /// 创建SSL流、读取传输请求、处理文件传输并发送响应 / Creates SSL stream, reads transfer request, processes file transfer and sends response
    /// </summary>
    /// <param name="client">TCP客户端 / TCP client</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>异步任务 / Async task</returns>
    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var clientEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
        
        try
        {
            using (client)
            {
                client.ReceiveTimeout = 30000; // 30 second timeout
                client.SendTimeout = 30000;

                // Create the appropriate stream (SSL or plain TCP)
                Stream stream;
                if (_useSSL)
                {
                    stream = await CreateSslStreamAsync(client, cancellationToken);
                }
                else
                {
                    stream = client.GetStream();
                }

                using (stream)
                {
                    // Read the transfer request
                    var request = await ReadTransferRequestAsync(stream, cancellationToken);
                    if (request == null)
                    {
                        _logger.LogWarning("Failed to read transfer request from client {Client}", clientEndpoint);
                        return;
                    }

                    _logger.LogInformation("Received secure transfer request for {FileName} from client {Client}", 
                        request.Metadata.FileName, clientEndpoint);

                    // Process the file transfer
                    var result = await ProcessFileTransferAsync(stream, request, cancellationToken);
                    
                    // Send response back to client
                    await SendResponseAsync(stream, result, cancellationToken);

                    _logger.LogInformation("Completed secure file transfer for {FileName} from client {Client}. Success: {Success}", 
                        request.Metadata.FileName, clientEndpoint, result.Success);
                }
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
    /// 为安全服务器通信创建SSL流 / Creates an SSL stream for secure server communication
    /// 配置SSL服务器选项并执行SSL握手 / Configures SSL server options and performs SSL handshake
    /// </summary>
    /// <param name="tcpClient">TCP客户端 / TCP client</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>配置好的SSL流 / Configured SSL stream</returns>
    /// <exception cref="InvalidOperationException">当未配置服务器证书时抛出 / Thrown when server certificate is not configured</exception>
    /// <exception cref="AuthenticationException">当SSL握手失败时抛出 / Thrown when SSL handshake fails</exception>
    private async Task<SslStream> CreateSslStreamAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        if (_serverCertificate == null)
        {
            throw new InvalidOperationException("Server certificate is required for SSL connections but not configured");
        }

        var sslStream = new SslStream(
            tcpClient.GetStream(),
            false,
            ValidateClientCertificate,
            null);

        try
        {
            // Configure SSL server options
            var sslOptions = new SslServerAuthenticationOptions
            {
                ServerCertificate = _serverCertificate,
                ClientCertificateRequired = _requireClientCertificate,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.Online,
                RemoteCertificateValidationCallback = ValidateClientCertificate
            };

            await sslStream.AuthenticateAsServerAsync(sslOptions, cancellationToken);
            
            _logger.LogDebug("SSL handshake completed with client. Protocol: {Protocol}, Cipher: {Cipher}", 
                sslStream.SslProtocol, sslStream.CipherAlgorithm);

            return sslStream;
        }
        catch (Exception ex)
        {
            sslStream.Dispose();
            _logger.LogError(ex, "SSL handshake failed with client");
            throw new AuthenticationException($"SSL handshake failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 验证客户端证书 / Validates the client certificate
    /// 根据配置决定是否要求客户端证书，并验证证书的有效性 / Determines whether to require client certificate based on configuration and validates certificate validity
    /// </summary>
    /// <param name="sender">发送者对象 / Sender object</param>
    /// <param name="certificate">客户端证书 / Client certificate</param>
    /// <param name="chain">证书链 / Certificate chain</param>
    /// <param name="sslPolicyErrors">SSL策略错误 / SSL policy errors</param>
    /// <returns>如果证书有效返回true，否则返回false / Returns true if certificate is valid, false otherwise</returns>
    private bool ValidateClientCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        // If client certificates are not required, accept the connection
        if (!_requireClientCertificate)
        {
            return true;
        }

        // If no certificate provided but required, reject
        if (certificate == null)
        {
            _logger.LogWarning("Client certificate required but not provided");
            return false;
        }

        // If no errors, certificate is valid
        if (sslPolicyErrors == SslPolicyErrors.None)
        {
            _logger.LogDebug("Client certificate validated successfully: {Subject}", certificate.Subject);
            return true;
        }

        _logger.LogWarning("Client certificate validation failed: {Errors}", sslPolicyErrors);
        return false;
    }
    /// <summary>
    /// 从客户端流中读取传输请求 / Reads a transfer request from the client stream
    /// 读取消息长度和内容，反序列化为传输请求对象 / Reads message length and content, deserializes to transfer request object
    /// </summary>
    /// <param name="stream">客户端流 / Client stream</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>传输请求对象，失败时返回null / Transfer request object, null on failure</returns>
    private async Task<TransferRequest?> ReadTransferRequestAsync(Stream stream, CancellationToken cancellationToken)
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
            _logger.LogError(ex, "Error reading secure transfer request");
            return null;
        }
    }

    /// <summary>
    /// 处理来自客户端的文件传输 / Processes the file transfer from the client
    /// 创建备份路径、验证存储空间、初始化传输会话并接收文件数据 / Creates backup path, validates storage space, initializes transfer session and receives file data
    /// </summary>
    /// <param name="stream">客户端流 / Client stream</param>
    /// <param name="request">传输请求 / Transfer request</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>接收结果 / Receive result</returns>
    private async Task<ReceiveResult> ProcessFileTransferAsync(Stream stream, TransferRequest request, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Create backup path
            var backupMetadata = new BackupMetadata
            {
                ServerName = request.Metadata.SourceConfig?.Name ?? "Unknown",
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
                    
                    _logger.LogInformation("Restored secure transfer session {TransferId} from resume token {ResumeToken}. Completed chunks: {Count}",
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
            _logger.LogError(ex, "Error processing secure file transfer for {FileName}", request.Metadata.FileName);
            
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
    private async Task<long> ReceiveFileDirectAsync(Stream stream, string targetPath, long expectedSize, CancellationToken cancellationToken)
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
                throw new InvalidOperationException("Unexpected end of stream during secure direct file reception");
            }

            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytesReceived += bytesRead;
            
            // Log progress every 10MB
            if (totalBytesReceived % (10 * 1024 * 1024) == 0 || totalBytesReceived == expectedSize)
            {
                var progress = (double)totalBytesReceived / expectedSize * 100;
                _logger.LogDebug("Secure direct reception progress: {Progress:F1}% ({Bytes}/{Total} bytes)", 
                    progress, totalBytesReceived, expectedSize);
            }
        }

        await fileStream.FlushAsync(cancellationToken);
        _logger.LogDebug("Completed secure direct file reception, total bytes: {TotalBytes}", totalBytesReceived);
        
        return totalBytesReceived;
    }

    /// <summary>
    /// Receives file data using chunking protocol
    /// </summary>
    private async Task<long> ReceiveFileChunkedAsync(Stream stream, string transferId, TransferRequest request, CancellationToken cancellationToken)
    {
        long totalBytesReceived = 0;
        var expectedChunks = request.ChunkingStrategy.CalculateChunkCount(request.Metadata.FileSize);
        
        _logger.LogInformation("Starting secure chunked reception for transfer {TransferId}: expecting {ChunkCount} chunks", 
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
            _logger.LogDebug("Received secure chunk {ChunkIndex}/{ChunkCount}: {Progress:F1}% ({Bytes}/{Total} bytes)", 
                chunk.ChunkIndex + 1, expectedChunks, progress, totalBytesReceived, request.Metadata.FileSize);
        }
        
        _logger.LogInformation("Completed secure chunked file reception for transfer {TransferId}, total bytes: {TotalBytes}", 
            transferId, totalBytesReceived);
        
        return totalBytesReceived;
    }

    /// <summary>
    /// Receives a single chunk from the client
    /// </summary>
    private async Task<ChunkData?> ReceiveChunkAsync(Stream stream, CancellationToken cancellationToken)
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
            _logger.LogError(ex, "Error receiving secure chunk");
            return null;
        }
    }

    /// <summary>
    /// Sends a transfer response to the client
    /// </summary>
    private async Task SendTransferResponseAsync(Stream stream, bool success, string? message, CancellationToken cancellationToken)
    {
        await SendTransferResponseAsync(stream, success, message, null, cancellationToken);
    }

    /// <summary>
    /// Sends a transfer response to the client with optional resume information
    /// </summary>
    private async Task SendTransferResponseAsync(Stream stream, bool success, string? message, string? resumeInfo, CancellationToken cancellationToken)
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
            _logger.LogError(ex, "Error sending secure transfer response");
        }
    }

    /// <summary>
    /// Sends a chunk response to the client
    /// </summary>
    private async Task SendChunkResponseAsync(Stream stream, bool success, string? message, int chunkIndex, CancellationToken cancellationToken)
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
            _logger.LogError(ex, "Error sending secure chunk response");
        }
    }

    /// <summary>
    /// Sends a response back to the client
    /// </summary>
    private async Task SendResponseAsync(Stream stream, ReceiveResult result, CancellationToken cancellationToken)
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
            _logger.LogError(ex, "Error sending secure response to client");
        }
    }

    public void Dispose()
    {
        StopListeningAsync().GetAwaiter().GetResult();
        _cancellationTokenSource?.Dispose();
        _serverCertificate?.Dispose();
    }
}