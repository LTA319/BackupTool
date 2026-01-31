using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 支持身份验证的文件传输客户端 / File transfer client with authentication support
/// 提供安全的文件传输功能，包括身份验证、校验和验证和断点续传支持
/// Provides secure file transfer functionality including authentication, checksum verification and resume support
/// </summary>
public class AuthenticatedFileTransferClient : IFileTransferClient
{
    private readonly ILogger<AuthenticatedFileTransferClient> _logger;
    private readonly IAuthenticationService _authenticationService;
    private readonly IChecksumService _checksumService;
    private string? _currentAuthToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;

    /// <summary>
    /// 初始化认证文件传输客户端 / Initializes authenticated file transfer client
    /// </summary>
    /// <param name="logger">日志记录器 / Logger instance</param>
    /// <param name="authenticationService">身份验证服务 / Authentication service</param>
    /// <param name="checksumService">校验和服务 / Checksum service</param>
    public AuthenticatedFileTransferClient(
        ILogger<AuthenticatedFileTransferClient> logger,
        IAuthenticationService authenticationService,
        IChecksumService checksumService)
    {
        _logger = logger;
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _checksumService = checksumService ?? throw new ArgumentNullException(nameof(checksumService));
    }

    /// <summary>
    /// 使用身份验证将文件传输到远程服务器 / Transfers a file to a remote server with authentication
    /// 验证文件存在性，获取认证令牌，计算校验和，然后执行安全的文件传输
    /// Validates file existence, obtains authentication token, calculates checksums, then performs secure file transfer
    /// </summary>
    public async Task<TransferResult> TransferFileAsync(string filePath, TransferConfig config, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (config == null)
            throw new ArgumentNullException(nameof(config));

        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Starting authenticated file transfer for {FilePath} to {Server}:{Port}", 
                filePath, config.TargetServer.IPAddress, config.TargetServer.Port);

            // 验证文件是否存在 / Validate file exists
            if (!File.Exists(filePath))
            {
                return new TransferResult
                {
                    Success = false,
                    ErrorMessage = $"File not found: {filePath}",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // 确保我们有有效的身份验证令牌 / Ensure we have a valid authentication token
            var authToken = await EnsureValidAuthTokenAsync(config.TargetServer);
            if (string.IsNullOrEmpty(authToken))
            {
                return new TransferResult
                {
                    Success = false,
                    ErrorMessage = "Failed to obtain authentication token",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // 获取文件信息 / Get file information
            var fileInfo = new FileInfo(filePath);
            var fileName = string.IsNullOrEmpty(config.FileName) ? fileInfo.Name : config.FileName;

            // 计算校验和 / Calculate checksums
            var md5Hash = await _checksumService.CalculateFileMD5Async(filePath, cancellationToken);
            var sha256Hash = await _checksumService.CalculateFileSHA256Async(filePath, cancellationToken);

            // 创建传输请求 / Create transfer request
            var transferRequest = new TransferRequest
            {
                TransferId = Guid.NewGuid().ToString(),
                AuthenticationToken = authToken,
                ClientId = config.TargetServer.ClientCredentials?.ClientId ?? "unknown",
                Metadata = new FileMetadata
                {
                    FileName = fileName,
                    FileSize = fileInfo.Length,
                    ChecksumMD5 = md5Hash,
                    ChecksumSHA256 = sha256Hash,
                    CreatedAt = DateTime.UtcNow
                },
                ChunkingStrategy = config.ChunkingStrategy,
                ResumeTransfer = false
            };

            // 执行传输 / Perform the transfer
            var result = await PerformTransferAsync(filePath, transferRequest, config, cancellationToken);
            result.Duration = DateTime.UtcNow - startTime;

            _logger.LogInformation("File transfer completed for {FilePath}. Success: {Success}, Duration: {Duration}", 
                filePath, result.Success, result.Duration);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("File transfer cancelled for {FilePath}", filePath);
            return new TransferResult
            {
                Success = false,
                ErrorMessage = "Transfer was cancelled",
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during file transfer for {FilePath}", filePath);
            return new TransferResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// 恢复中断的文件传输 / Resumes an interrupted file transfer
    /// 注意：这是一个简化的实现，实际场景中需要存储传输上下文
    /// Note: This is a simplified implementation - in a real scenario, you'd need to store transfer context
    /// </summary>
    public async Task<TransferResult> ResumeTransferAsync(string resumeToken, CancellationToken cancellationToken = default)
    {
        // 这是一个简化的实现 - 在实际场景中，您需要存储传输上下文
        // This is a simplified implementation - in a real scenario, you'd need to store transfer context
        _logger.LogWarning("Resume transfer not fully implemented for token {ResumeToken}", resumeToken);
        return new TransferResult
        {
            Success = false,
            ErrorMessage = "Resume transfer not implemented in authenticated client"
        };
    }

    /// <summary>
    /// 使用完整上下文恢复中断的文件传输 / Resumes an interrupted file transfer with full context
    /// 提供完整的传输上下文来恢复中断的传输，包括重新认证和校验和验证
    /// Provides complete transfer context to resume interrupted transfer, including re-authentication and checksum verification
    /// </summary>
    public async Task<TransferResult> ResumeTransferAsync(string resumeToken, string filePath, TransferConfig config, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(resumeToken))
            throw new ArgumentException("Resume token cannot be null or empty", nameof(resumeToken));

        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Resuming authenticated file transfer for {FilePath} with token {ResumeToken}", 
                filePath, resumeToken);

            // 确保我们有有效的身份验证令牌 / Ensure we have a valid authentication token
            var authToken = await EnsureValidAuthTokenAsync(config.TargetServer);
            if (string.IsNullOrEmpty(authToken))
            {
                return new TransferResult
                {
                    Success = false,
                    ErrorMessage = "Failed to obtain authentication token for resume",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // 获取文件信息 / Get file information
            var fileInfo = new FileInfo(filePath);
            var fileName = string.IsNullOrEmpty(config.FileName) ? fileInfo.Name : config.FileName;

            // 计算校验和 / Calculate checksums
            var md5Hash = await _checksumService.CalculateFileMD5Async(filePath, cancellationToken);
            var sha256Hash = await _checksumService.CalculateFileSHA256Async(filePath, cancellationToken);

            // 创建恢复传输请求 / Create resume transfer request
            var transferRequest = new TransferRequest
            {
                TransferId = resumeToken, // 使用恢复令牌作为传输ID / Use resume token as transfer ID
                AuthenticationToken = authToken,
                ClientId = config.TargetServer.ClientCredentials?.ClientId ?? "unknown",
                Metadata = new FileMetadata
                {
                    FileName = fileName,
                    FileSize = fileInfo.Length,
                    ChecksumMD5 = md5Hash,
                    ChecksumSHA256 = sha256Hash,
                    CreatedAt = DateTime.UtcNow
                },
                ChunkingStrategy = config.ChunkingStrategy,
                ResumeTransfer = true,
                ResumeToken = resumeToken
            };

            // 执行传输 / Perform the transfer
            var result = await PerformTransferAsync(filePath, transferRequest, config, cancellationToken);
            result.Duration = DateTime.UtcNow - startTime;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during resume transfer for {FilePath}", filePath);
            return new TransferResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// 确保我们有有效的身份验证令牌 / Ensures we have a valid authentication token
    /// 检查现有令牌的有效性，如果需要则重新认证
    /// Checks validity of existing token and re-authenticates if needed
    /// </summary>
    private async Task<string?> EnsureValidAuthTokenAsync(ServerEndpoint serverEndpoint)
    {
        // 检查我们是否有有效的令牌 / Check if we have a valid token
        if (!string.IsNullOrEmpty(_currentAuthToken) && DateTime.UtcNow < _tokenExpiresAt.AddMinutes(-5))
        {
            // 令牌仍然有效（有5分钟缓冲） / Token is still valid (with 5-minute buffer)
            return _currentAuthToken;
        }

        // 需要进行身份验证 / Need to authenticate
        if (serverEndpoint.ClientCredentials == null)
        {
            _logger.LogError("No client credentials configured for server endpoint");
            return null;
        }

        // 目前，我们将直接使用客户端凭据作为"令牌"
        // 服务器在收到传输请求时将验证这些凭据
        // 这是一个简化的方法 - 在生产环境中，您需要适当的令牌交换
        // For now, we'll use the client credentials directly as the "token"
        // The server will validate these credentials when it receives the transfer request
        // This is a simplified approach - in production, you'd want proper token exchange
        var credentials = $"{serverEndpoint.ClientCredentials.ClientId}:{serverEndpoint.ClientCredentials.ClientSecret}";
        var token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(credentials));
        
        _currentAuthToken = token;
        _tokenExpiresAt = DateTime.UtcNow.AddHours(1);
        
        _logger.LogInformation("Successfully prepared authentication token for client {ClientId}", serverEndpoint.ClientCredentials.ClientId);
        return _currentAuthToken;
    }

    /// <summary>
    /// 执行实际的文件传输 / Performs the actual file transfer
    /// 建立TCP连接，发送传输请求，传输文件数据并处理服务器响应
    /// Establishes TCP connection, sends transfer request, transfers file data and handles server responses
    /// </summary>
    private async Task<TransferResult> PerformTransferAsync(string filePath, TransferRequest request, TransferConfig config, CancellationToken cancellationToken)
    {
        using var tcpClient = new TcpClient();
        
        try
        {
            // 连接到服务器 / Connect to server
            await tcpClient.ConnectAsync(config.TargetServer.IPAddress, config.TargetServer.Port);
            _logger.LogDebug("Connected to server {Server}:{Port}", config.TargetServer.IPAddress, config.TargetServer.Port);

            using var stream = tcpClient.GetStream();
            
            // 发送传输请求 / Send transfer request
            await SendTransferRequestAsync(stream, request, cancellationToken);
            
            // 读取服务器响应 / Read server response
            var response = await ReadTransferResponseAsync(stream, cancellationToken);
            if (response == null || !response.Success)
            {
                return new TransferResult
                {
                    Success = false,
                    ErrorMessage = response?.ErrorMessage ?? "Failed to get server response"
                };
            }

            // 发送文件数据 / Send file data
            var bytesTransferred = await SendFileDataAsync(stream, filePath, request, cancellationToken);
            
            // 读取最终响应 / Read final response
            var finalResponse = await ReadTransferResponseAsync(stream, cancellationToken);
            
            return new TransferResult
            {
                Success = finalResponse?.Success ?? false,
                ErrorMessage = finalResponse?.ErrorMessage,
                BytesTransferred = bytesTransferred,
                ChecksumHash = request.Metadata.ChecksumSHA256
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during transfer communication");
            return new TransferResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// 向服务器发送传输请求 / Sends the transfer request to the server
    /// </summary>
    private async Task SendTransferRequestAsync(NetworkStream stream, TransferRequest request, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(request);
        var messageBytes = Encoding.UTF8.GetBytes(json);
        var lengthBytes = BitConverter.GetBytes(messageBytes.Length);

        await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length, cancellationToken);
        await stream.WriteAsync(messageBytes, 0, messageBytes.Length, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// 从服务器读取传输响应 / Reads a transfer response from the server
    /// </summary>
    private async Task<TransferResponse?> ReadTransferResponseAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        try
        {
            // 读取消息长度 / Read message length
            var lengthBuffer = new byte[4];
            var bytesRead = await stream.ReadAsync(lengthBuffer, 0, 4, cancellationToken);
            if (bytesRead != 4)
                return null;

            var messageLength = BitConverter.ToInt32(lengthBuffer, 0);
            if (messageLength <= 0 || messageLength > 1024 * 1024)
                return null;

            // 读取消息内容 / Read message content
            var messageBuffer = new byte[messageLength];
            var totalBytesRead = 0;
            while (totalBytesRead < messageLength)
            {
                bytesRead = await stream.ReadAsync(messageBuffer, totalBytesRead, 
                    messageLength - totalBytesRead, cancellationToken);
                if (bytesRead == 0)
                    return null;
                totalBytesRead += bytesRead;
            }

            var json = Encoding.UTF8.GetString(messageBuffer);
            return JsonSerializer.Deserialize<TransferResponse>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading transfer response");
            return null;
        }
    }

    /// <summary>
    /// 向服务器发送文件数据 / Sends file data to the server
    /// 以分块方式读取和发送文件数据，提供进度报告
    /// Reads and sends file data in chunks with progress reporting
    /// </summary>
    private async Task<long> SendFileDataAsync(NetworkStream stream, string filePath, TransferRequest request, CancellationToken cancellationToken)
    {
        long totalBytesSent = 0;
        var buffer = new byte[64 * 1024]; // 64KB缓冲区 / 64KB buffer

        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        
        while (totalBytesSent < request.Metadata.FileSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var bytesToRead = (int)Math.Min(buffer.Length, request.Metadata.FileSize - totalBytesSent);
            var bytesRead = await fileStream.ReadAsync(buffer, 0, bytesToRead, cancellationToken);
            
            if (bytesRead == 0)
                break;

            await stream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytesSent += bytesRead;

            // 每10MB记录一次进度 / Log progress every 10MB
            if (totalBytesSent % (10 * 1024 * 1024) == 0 || totalBytesSent == request.Metadata.FileSize)
            {
                var progress = (double)totalBytesSent / request.Metadata.FileSize * 100;
                _logger.LogDebug("Transfer progress: {Progress:F1}% ({Bytes}/{Total} bytes)", 
                    progress, totalBytesSent, request.Metadata.FileSize);
            }
        }

        await stream.FlushAsync(cancellationToken);
        return totalBytesSent;
    }
}