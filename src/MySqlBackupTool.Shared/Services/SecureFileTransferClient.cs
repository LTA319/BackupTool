using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 支持SSL/TLS的文件传输客户端实现 / SSL/TLS-enabled file transfer client implementation
/// 提供安全的文件传输功能，支持SSL加密、服务器证书验证、分块传输和断点续传 / Provides secure file transfer with SSL encryption, server certificate validation, chunked transfer, and resume capability
/// </summary>
public class SecureFileTransferClient : IFileTransferClient, IFileTransferService
{
    private readonly ILogger<SecureFileTransferClient> _logger;
    private readonly IChecksumService _checksumService;
    private const int BufferSize = 8192; // 8KB文件读取缓冲区 / 8KB buffer for file reading
    private const int MaxRetryAttempts = 3; // 最大重试次数 / Maximum retry attempts
    private const int BaseRetryDelayMs = 1000; // 1秒基础重试延迟 / 1 second base retry delay

    /// <summary>
    /// 构造函数，初始化安全文件传输客户端 / Constructor, initializes secure file transfer client
    /// </summary>
    /// <param name="logger">日志服务 / Logger service</param>
    /// <param name="checksumService">校验和服务 / Checksum service</param>
    public SecureFileTransferClient(ILogger<SecureFileTransferClient> logger, IChecksumService checksumService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _checksumService = checksumService ?? throw new ArgumentNullException(nameof(checksumService));
    }

    /// <summary>
    /// 使用SSL/TLS将文件传输到远程服务器 / Transfers a file to a remote server using SSL/TLS
    /// 验证输入、创建文件元数据、执行带重试逻辑的传输 / Validates inputs, creates file metadata, performs transfer with retry logic
    /// </summary>
    /// <param name="filePath">要传输的文件路径 / Path of file to transfer</param>
    /// <param name="config">传输配置 / Transfer configuration</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>传输结果 / Transfer result</returns>
    public async Task<TransferResult> TransferFileAsync(string filePath, TransferConfig config, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var transferId = Guid.NewGuid().ToString();
        
        try
        {
            // Check for cancellation early
            cancellationToken.ThrowIfCancellationRequested();
            
            _logger.LogInformation("Starting secure file transfer for {FilePath} to {Server}:{Port} (SSL: {UseSSL})", 
                filePath, config.TargetServer.IPAddress, config.TargetServer.Port, config.TargetServer.UseSSL);

            // Validate inputs
            if (!File.Exists(filePath))
            {
                return new TransferResult
                {
                    Success = false,
                    ErrorMessage = $"File not found: {filePath}",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            var (isValid, errors) = config.TargetServer.ValidateEndpoint();
            if (!isValid)
            {
                return new TransferResult
                {
                    Success = false,
                    ErrorMessage = $"Invalid server endpoint: {string.Join(", ", errors)}",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Get file info and create metadata
            var fileInfo = new FileInfo(filePath);
            var metadata = await CreateFileMetadataAsync(filePath, config.FileName);

            // Create transfer request
            var transferRequest = new TransferRequest
            {
                TransferId = transferId,
                Metadata = metadata,
                ChunkingStrategy = config.ChunkingStrategy,
                ResumeTransfer = false
            };

            // Perform transfer with retry logic
            var result = await TransferWithRetryAsync(filePath, transferRequest, config, cancellationToken);
            result.Duration = DateTime.UtcNow - startTime;

            _logger.LogInformation("Secure file transfer completed. Success: {Success}, Bytes: {Bytes}, Duration: {Duration}",
                result.Success, result.BytesTransferred, result.Duration);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Secure file transfer was cancelled for {FilePath}", filePath);
            return new TransferResult
            {
                Success = false,
                ErrorMessage = "Transfer was cancelled",
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during secure file transfer for {FilePath}", filePath);
            return new TransferResult
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// 恢复中断的文件传输 / Resumes an interrupted file transfer
    /// 需要服务器端协调，建议使用带完整上下文的重载方法 / Requires server-side coordination, recommend using overload with full context
    /// </summary>
    /// <param name="resumeToken">恢复令牌 / Resume token</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>传输结果 / Transfer result</returns>
    public async Task<TransferResult> ResumeTransferAsync(string resumeToken, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Resuming secure file transfer with token {ResumeToken}", resumeToken);

            return new TransferResult
            {
                Success = false,
                ErrorMessage = "Resume functionality requires server-side coordination. Use ResumeTransferAsync(resumeToken, filePath, config) instead.",
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming secure transfer with token {ResumeToken}", resumeToken);
            return new TransferResult
            {
                Success = false,
                ErrorMessage = $"Resume error: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// 使用完整上下文恢复中断的文件传输 / Resumes an interrupted file transfer with full context
    /// 验证输入、创建恢复传输请求并执行带重试逻辑的传输 / Validates inputs, creates resume transfer request and performs transfer with retry logic
    /// </summary>
    /// <param name="resumeToken">恢复令牌 / Resume token</param>
    /// <param name="filePath">文件路径 / File path</param>
    /// <param name="config">传输配置 / Transfer configuration</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>传输结果 / Transfer result</returns>
    public async Task<TransferResult> ResumeTransferAsync(string resumeToken, string filePath, TransferConfig config, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Resuming secure file transfer with token {ResumeToken} for file {FilePath}", resumeToken, filePath);

            // Validate inputs
            if (!File.Exists(filePath))
            {
                return new TransferResult
                {
                    Success = false,
                    ErrorMessage = $"File not found: {filePath}",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            var (isValid, errors) = config.TargetServer.ValidateEndpoint();
            if (!isValid)
            {
                return new TransferResult
                {
                    Success = false,
                    ErrorMessage = $"Invalid server endpoint: {string.Join(", ", errors)}",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Create file metadata
            var metadata = await CreateFileMetadataAsync(filePath, config.FileName);

            // Create resume transfer request
            var transferRequest = new TransferRequest
            {
                TransferId = Guid.NewGuid().ToString(), // New transfer ID for resume
                Metadata = metadata,
                ChunkingStrategy = config.ChunkingStrategy,
                ResumeTransfer = true,
                ResumeToken = resumeToken
            };

            // Perform transfer with retry logic
            var result = await TransferWithRetryAsync(filePath, transferRequest, config, cancellationToken);
            result.Duration = DateTime.UtcNow - startTime;

            _logger.LogInformation("Resume secure transfer completed. Success: {Success}, Bytes: {Bytes}, Duration: {Duration}",
                result.Success, result.BytesTransferred, result.Duration);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Resume secure transfer was cancelled for {FilePath}", filePath);
            return new TransferResult
            {
                Success = false,
                ErrorMessage = "Resume transfer was cancelled",
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during resume secure transfer for {FilePath}", filePath);
            return new TransferResult
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// Performs file transfer with retry logic
    /// </summary>
    private async Task<TransferResult> TransferWithRetryAsync(string filePath, TransferRequest request, TransferConfig config, CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        
        for (int attempt = 1; attempt <= config.MaxRetries; attempt++)
        {
            try
            {
                _logger.LogDebug("Secure transfer attempt {Attempt} of {MaxRetries} for {TransferId}", 
                    attempt, config.MaxRetries, request.TransferId);

                var result = await PerformSecureTransferAsync(filePath, request, config, cancellationToken);
                
                if (result.Success)
                {
                    return result;
                }

                // If not successful but no exception, treat as retriable error
                lastException = new InvalidOperationException(result.ErrorMessage ?? "Transfer failed");
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Secure transfer attempt {Attempt} failed for {TransferId}", attempt, request.TransferId);
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
                await Task.Delay(delay, cancellationToken);
            }
        }

        return new TransferResult
        {
            Success = false,
            ErrorMessage = $"Secure transfer failed after {config.MaxRetries} attempts. Last error: {lastException?.Message}",
            BytesTransferred = 0
        };
    }

    /// <summary>
    /// Performs the actual secure file transfer
    /// </summary>
    private async Task<TransferResult> PerformSecureTransferAsync(string filePath, TransferRequest request, TransferConfig config, CancellationToken cancellationToken)
    {
        using var tcpClient = new TcpClient();
        
        try
        {
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

            _logger.LogDebug("Connected to {Server}:{Port} for secure transfer {TransferId}", 
                config.TargetServer.IPAddress, config.TargetServer.Port, request.TransferId);

            // Create the appropriate stream (SSL or plain TCP)
            Stream networkStream;
            if (config.TargetServer.UseSSL)
            {
                networkStream = await CreateSslStreamAsync(tcpClient, config.TargetServer, cancellationToken);
            }
            else
            {
                networkStream = tcpClient.GetStream();
            }

            using (networkStream)
            {
                // Send transfer request header
                await SendTransferRequestAsync(networkStream, request, cancellationToken);
                
                // Wait for server acknowledgment
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
                            _logger.LogInformation("Resume secure transfer for {TransferId}: {Count} chunks already completed", 
                                request.TransferId, completedChunks.Count);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse resume information, proceeding with full transfer");
                    }
                }

                // Send file data
                var bytesTransferred = await SendFileDataAsync(networkStream, filePath, request, completedChunks, cancellationToken);
                
                // Wait for final confirmation
                var finalResponse = await ReceiveFinalConfirmationAsync(networkStream, cancellationToken);
                
                return new TransferResult
                {
                    Success = finalResponse.Success,
                    ErrorMessage = finalResponse.ErrorMessage,
                    BytesTransferred = bytesTransferred
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during secure file transfer for {TransferId}", request.TransferId);
            throw;
        }
    }

    /// <summary>
    /// Creates an SSL stream for secure communication
    /// </summary>
    private async Task<SslStream> CreateSslStreamAsync(TcpClient tcpClient, ServerEndpoint endpoint, CancellationToken cancellationToken)
    {
        var sslStream = new SslStream(
            tcpClient.GetStream(),
            false,
            ValidateServerCertificate,
            null);

        try
        {
            var targetHost = endpoint.ExpectedCertificateSubject ?? endpoint.IPAddress;
            
            // Configure SSL options
            var sslOptions = new SslClientAuthenticationOptions
            {
                TargetHost = targetHost,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.Online,
                RemoteCertificateValidationCallback = (sender, certificate, chain, errors) => 
                    ValidateServerCertificate(sender, certificate, chain, errors, endpoint)
            };

            // Add client certificate if configured
            if (!string.IsNullOrEmpty(endpoint.CertificatePath))
            {
                var clientCert = endpoint.LoadCertificate();
                if (clientCert != null)
                {
                    sslOptions.ClientCertificates = new X509CertificateCollection { clientCert };
                }
            }

            await sslStream.AuthenticateAsClientAsync(sslOptions, cancellationToken);
            
            _logger.LogDebug("SSL handshake completed. Protocol: {Protocol}, Cipher: {Cipher}", 
                sslStream.SslProtocol, sslStream.CipherAlgorithm);

            return sslStream;
        }
        catch (Exception ex)
        {
            sslStream.Dispose();
            _logger.LogError(ex, "SSL handshake failed for {TargetHost}", endpoint.IPAddress);
            throw new AuthenticationException($"SSL handshake failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates the server certificate
    /// </summary>
    private bool ValidateServerCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        // Default validation - will be overridden by the method with endpoint parameter
        return sslPolicyErrors == SslPolicyErrors.None;
    }

    /// <summary>
    /// Validates the server certificate with endpoint-specific configuration
    /// </summary>
    private bool ValidateServerCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors, ServerEndpoint endpoint)
    {
        // If validation is disabled, accept any certificate
        if (!endpoint.ValidateServerCertificate)
        {
            _logger.LogWarning("Server certificate validation is disabled - accepting any certificate");
            return true;
        }

        // If no errors, certificate is valid
        if (sslPolicyErrors == SslPolicyErrors.None)
        {
            return true;
        }

        // Handle self-signed certificates
        if (endpoint.AllowSelfSignedCertificates && 
            sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors)
        {
            _logger.LogInformation("Accepting self-signed certificate as configured");
            return true;
        }

        // Check specific thumbprint if configured
        if (!string.IsNullOrEmpty(endpoint.CertificateThumbprint) && certificate != null)
        {
            var cert2 = new X509Certificate2(certificate);
            if (string.Equals(cert2.Thumbprint, endpoint.CertificateThumbprint, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Certificate accepted based on matching thumbprint");
                return true;
            }
        }

        _logger.LogWarning("Server certificate validation failed: {Errors}", sslPolicyErrors);
        return false;
    }
    /// <summary>
    /// Sends the transfer request header to the server
    /// </summary>
    private async Task SendTransferRequestAsync(Stream stream, TransferRequest request, CancellationToken cancellationToken)
    {
        var requestJson = JsonSerializer.Serialize(request);
        var requestBytes = Encoding.UTF8.GetBytes(requestJson);
        var headerBytes = BitConverter.GetBytes(requestBytes.Length);
        
        // Send header length (4 bytes) followed by JSON data
        await stream.WriteAsync(headerBytes, 0, 4, cancellationToken);
        await stream.WriteAsync(requestBytes, 0, requestBytes.Length, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        
        _logger.LogDebug("Sent secure transfer request for {TransferId}, size: {Size} bytes", request.TransferId, requestBytes.Length);
    }

    /// <summary>
    /// Receives acknowledgment from server
    /// </summary>
    private async Task<TransferResponse> ReceiveAcknowledgmentAsync(Stream stream, CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[4];
        await ReadExactlyAsync(stream, lengthBuffer, 4, cancellationToken);
        
        var responseLength = BitConverter.ToInt32(lengthBuffer, 0);
        var responseBuffer = new byte[responseLength];
        await ReadExactlyAsync(stream, responseBuffer, responseLength, cancellationToken);
        
        var responseJson = Encoding.UTF8.GetString(responseBuffer);
        var response = JsonSerializer.Deserialize<TransferResponse>(responseJson) ?? new TransferResponse();
        
        _logger.LogDebug("Received secure acknowledgment: Success={Success}, Message={Message}", response.Success, response.ErrorMessage);
        return response;
    }

    /// <summary>
    /// Sends file data to the server
    /// </summary>
    private async Task<long> SendFileDataAsync(Stream stream, string filePath, TransferRequest request, HashSet<int> completedChunks, CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(filePath);
        var fileSize = fileInfo.Length;
        
        _logger.LogDebug("Starting secure file data transfer for {TransferId}, file size: {FileSize} bytes", request.TransferId, fileSize);
        
        // Determine if we should use chunking
        var shouldChunk = fileSize > request.ChunkingStrategy.ChunkSize;
        
        if (shouldChunk)
        {
            return await SendFileDataChunkedAsync(stream, filePath, request, completedChunks, cancellationToken);
        }
        else
        {
            return await SendFileDataDirectAsync(stream, filePath, request, cancellationToken);
        }
    }

    /// <summary>
    /// Sends file data directly without chunking
    /// </summary>
    private async Task<long> SendFileDataDirectAsync(Stream stream, string filePath, TransferRequest request, CancellationToken cancellationToken)
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        var buffer = new byte[BufferSize];
        long totalBytes = 0;
        long fileSize = fileStream.Length;
        
        while (totalBytes < fileSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var bytesToRead = (int)Math.Min(BufferSize, fileSize - totalBytes);
            var bytesRead = await fileStream.ReadAsync(buffer, 0, bytesToRead, cancellationToken);
            
            if (bytesRead == 0)
                break;
                
            await stream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytes += bytesRead;
            
            // Log progress every 10MB
            if (totalBytes % (10 * 1024 * 1024) == 0 || totalBytes == fileSize)
            {
                var progress = (double)totalBytes / fileSize * 100;
                _logger.LogDebug("Secure transfer progress for {TransferId}: {Progress:F1}% ({Bytes}/{Total} bytes)", 
                    request.TransferId, progress, totalBytes, fileSize);
            }
        }
        
        await stream.FlushAsync(cancellationToken);
        _logger.LogDebug("Completed secure direct file data transfer for {TransferId}, total bytes: {TotalBytes}", request.TransferId, totalBytes);
        
        return totalBytes;
    }

    /// <summary>
    /// Sends file data using chunking strategy
    /// </summary>
    private async Task<long> SendFileDataChunkedAsync(Stream stream, string filePath, TransferRequest request, HashSet<int> completedChunks, CancellationToken cancellationToken)
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        var fileSize = fileStream.Length;
        var chunkCount = request.ChunkingStrategy.CalculateChunkCount(fileSize);
        long totalBytes = 0;
        
        _logger.LogInformation("Starting secure chunked transfer for {TransferId}: {ChunkCount} chunks of {ChunkSize} bytes each", 
            request.TransferId, chunkCount, request.ChunkingStrategy.ChunkSize);

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
            
            // Read chunk data
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
            
            // Send chunk
            await SendChunkAsync(stream, chunk, cancellationToken);
            
            totalBytes += bytesRead;
            
            // Log progress
            var progress = (double)totalBytes / fileSize * 100;
            _logger.LogDebug("Sent secure chunk {ChunkIndex}/{ChunkCount} for {TransferId}: {Progress:F1}% ({Bytes}/{Total} bytes)", 
                chunkIndex + 1, chunkCount, request.TransferId, progress, totalBytes, fileSize);
        }
        
        _logger.LogInformation("Completed secure chunked file data transfer for {TransferId}, total bytes: {TotalBytes}", request.TransferId, totalBytes);
        
        return totalBytes;
    }

    /// <summary>
    /// Sends a single chunk to the server
    /// </summary>
    private async Task SendChunkAsync(Stream stream, ChunkData chunk, CancellationToken cancellationToken)
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
    /// Receives acknowledgment for a chunk from server
    /// </summary>
    private async Task<ChunkResult> ReceiveChunkAcknowledgmentAsync(Stream stream, CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[4];
        await ReadExactlyAsync(stream, lengthBuffer, 4, cancellationToken);
        
        var responseLength = BitConverter.ToInt32(lengthBuffer, 0);
        var responseBuffer = new byte[responseLength];
        await ReadExactlyAsync(stream, responseBuffer, responseLength, cancellationToken);
        
        var responseJson = Encoding.UTF8.GetString(responseBuffer);
        var response = JsonSerializer.Deserialize<ChunkResult>(responseJson) ?? new ChunkResult();
        
        return response;
    }

    /// <summary>
    /// Receives final confirmation from server
    /// </summary>
    private async Task<TransferResponse> ReceiveFinalConfirmationAsync(Stream stream, CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[4];
        await ReadExactlyAsync(stream, lengthBuffer, 4, cancellationToken);
        
        var responseLength = BitConverter.ToInt32(lengthBuffer, 0);
        var responseBuffer = new byte[responseLength];
        await ReadExactlyAsync(stream, responseBuffer, responseLength, cancellationToken);
        
        var responseJson = Encoding.UTF8.GetString(responseBuffer);
        var response = JsonSerializer.Deserialize<TransferResponse>(responseJson) ?? new TransferResponse();
        
        _logger.LogDebug("Received secure final confirmation: Success={Success}, Message={Message}", response.Success, response.ErrorMessage);
        return response;
    }

    /// <summary>
    /// Reads exactly the specified number of bytes from the stream
    /// </summary>
    private async Task ReadExactlyAsync(Stream stream, byte[] buffer, int count, CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            var bytesRead = await stream.ReadAsync(buffer, totalRead, count - totalRead, cancellationToken);
            if (bytesRead == 0)
                throw new EndOfStreamException("Unexpected end of stream");
            totalRead += bytesRead;
        }
    }

    /// <summary>
    /// Creates file metadata including checksums
    /// </summary>
    private async Task<FileMetadata> CreateFileMetadataAsync(string filePath, string targetFileName)
    {
        var (md5, sha256, fileSize) = await _checksumService.CreateFileMetadataAsync(filePath);
        
        return new FileMetadata
        {
            FileName = targetFileName,
            FileSize = fileSize,
            ChecksumMD5 = md5,
            ChecksumSHA256 = sha256,
            CreatedAt = DateTime.UtcNow
        };
    }
}