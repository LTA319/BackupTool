using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// File transfer client with authentication support
/// </summary>
public class AuthenticatedFileTransferClient : IFileTransferClient
{
    private readonly ILogger<AuthenticatedFileTransferClient> _logger;
    private readonly IAuthenticationService _authenticationService;
    private readonly IChecksumService _checksumService;
    private string? _currentAuthToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;

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
    /// Transfers a file to a remote server with authentication
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

            // Validate file exists
            if (!File.Exists(filePath))
            {
                return new TransferResult
                {
                    Success = false,
                    ErrorMessage = $"File not found: {filePath}",
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Ensure we have a valid authentication token
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

            // Get file information
            var fileInfo = new FileInfo(filePath);
            var fileName = string.IsNullOrEmpty(config.FileName) ? fileInfo.Name : config.FileName;

            // Calculate checksums
            var md5Hash = await _checksumService.CalculateFileMD5Async(filePath, cancellationToken);
            var sha256Hash = await _checksumService.CalculateFileSHA256Async(filePath, cancellationToken);

            // Create transfer request
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

            // Perform the transfer
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
    /// Resumes an interrupted file transfer
    /// </summary>
    public async Task<TransferResult> ResumeTransferAsync(string resumeToken, CancellationToken cancellationToken = default)
    {
        // This is a simplified implementation - in a real scenario, you'd need to store transfer context
        _logger.LogWarning("Resume transfer not fully implemented for token {ResumeToken}", resumeToken);
        return new TransferResult
        {
            Success = false,
            ErrorMessage = "Resume transfer not implemented in authenticated client"
        };
    }

    /// <summary>
    /// Resumes an interrupted file transfer with full context
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

            // Ensure we have a valid authentication token
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

            // Get file information
            var fileInfo = new FileInfo(filePath);
            var fileName = string.IsNullOrEmpty(config.FileName) ? fileInfo.Name : config.FileName;

            // Calculate checksums
            var md5Hash = await _checksumService.CalculateFileMD5Async(filePath, cancellationToken);
            var sha256Hash = await _checksumService.CalculateFileSHA256Async(filePath, cancellationToken);

            // Create resume transfer request
            var transferRequest = new TransferRequest
            {
                TransferId = resumeToken, // Use resume token as transfer ID
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

            // Perform the transfer
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
    /// Ensures we have a valid authentication token
    /// </summary>
    private async Task<string?> EnsureValidAuthTokenAsync(ServerEndpoint serverEndpoint)
    {
        // Check if we have a valid token
        if (!string.IsNullOrEmpty(_currentAuthToken) && DateTime.UtcNow < _tokenExpiresAt.AddMinutes(-5))
        {
            // Token is still valid (with 5-minute buffer)
            return _currentAuthToken;
        }

        // Need to authenticate
        if (serverEndpoint.ClientCredentials == null)
        {
            _logger.LogError("No client credentials configured for server endpoint");
            return null;
        }

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
    /// Performs the actual file transfer
    /// </summary>
    private async Task<TransferResult> PerformTransferAsync(string filePath, TransferRequest request, TransferConfig config, CancellationToken cancellationToken)
    {
        using var tcpClient = new TcpClient();
        
        try
        {
            // Connect to server
            await tcpClient.ConnectAsync(config.TargetServer.IPAddress, config.TargetServer.Port);
            _logger.LogDebug("Connected to server {Server}:{Port}", config.TargetServer.IPAddress, config.TargetServer.Port);

            using var stream = tcpClient.GetStream();
            
            // Send transfer request
            await SendTransferRequestAsync(stream, request, cancellationToken);
            
            // Read server response
            var response = await ReadTransferResponseAsync(stream, cancellationToken);
            if (response == null || !response.Success)
            {
                return new TransferResult
                {
                    Success = false,
                    ErrorMessage = response?.ErrorMessage ?? "Failed to get server response"
                };
            }

            // Send file data
            var bytesTransferred = await SendFileDataAsync(stream, filePath, request, cancellationToken);
            
            // Read final response
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
    /// Sends the transfer request to the server
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
    /// Reads a transfer response from the server
    /// </summary>
    private async Task<TransferResponse?> ReadTransferResponseAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        try
        {
            // Read message length
            var lengthBuffer = new byte[4];
            var bytesRead = await stream.ReadAsync(lengthBuffer, 0, 4, cancellationToken);
            if (bytesRead != 4)
                return null;

            var messageLength = BitConverter.ToInt32(lengthBuffer, 0);
            if (messageLength <= 0 || messageLength > 1024 * 1024)
                return null;

            // Read message content
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
    /// Sends file data to the server
    /// </summary>
    private async Task<long> SendFileDataAsync(NetworkStream stream, string filePath, TransferRequest request, CancellationToken cancellationToken)
    {
        long totalBytesSent = 0;
        var buffer = new byte[64 * 1024]; // 64KB buffer

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

            // Log progress every 10MB
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