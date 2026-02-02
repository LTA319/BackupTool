using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 具有网络重试和警报功能的增强文件传输客户端 / Enhanced file transfer client with network retry and alerting capabilities
/// </summary>
public class EnhancedFileTransferClient : IFileTransferClient
{
    private readonly ILogger<EnhancedFileTransferClient> _logger;
    private readonly IFileTransferClient _baseClient;
    private readonly INetworkRetryService _networkRetryService;
    private readonly IAlertingService _alertingService;

    /// <summary>
    /// 初始化增强文件传输客户端 / Initialize enhanced file transfer client
    /// </summary>
    /// <param name="logger">日志记录器 / Logger instance</param>
    /// <param name="baseClient">基础文件传输客户端 / Base file transfer client</param>
    /// <param name="networkRetryService">网络重试服务 / Network retry service</param>
    /// <param name="alertingService">警报服务 / Alerting service</param>
    /// <exception cref="ArgumentNullException">当任何参数为null时抛出 / Thrown when any parameter is null</exception>
    public EnhancedFileTransferClient(
        ILogger<EnhancedFileTransferClient> logger,
        IFileTransferClient baseClient,
        INetworkRetryService networkRetryService,
        IAlertingService alertingService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _baseClient = baseClient ?? throw new ArgumentNullException(nameof(baseClient));
        _networkRetryService = networkRetryService ?? throw new ArgumentNullException(nameof(networkRetryService));
        _alertingService = alertingService ?? throw new ArgumentNullException(nameof(alertingService));
    }

    /// <summary>
    /// 使用增强的重试和警报功能传输文件 / Transfers a file with enhanced retry and alerting capabilities
    /// </summary>
    /// <param name="filePath">要传输的文件路径 / File path to transfer</param>
    /// <param name="config">传输配置 / Transfer configuration</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>传输结果 / Transfer result</returns>
    public async Task<TransferResult> TransferFileAsync(string filePath, TransferConfig config, CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid().ToString();
        var startTime = DateTime.Now;

        try
        {
            _logger.LogInformation("Starting enhanced file transfer for {FilePath} to {Server}:{Port} (Operation ID: {OperationId})",
                filePath, config.TargetServer.IPAddress, config.TargetServer.Port, operationId);

            // 首先测试连接性 / Test connectivity first
            var connectivityResult = await _networkRetryService.TestConnectivityAsync(
                config.TargetServer.IPAddress,
                config.TargetServer.Port,
                TimeSpan.FromSeconds(30),
                cancellationToken);

            if (!connectivityResult.IsReachable)
            {
                _logger.LogWarning("Network connectivity test failed for {Server}:{Port} - {ErrorMessage}",
                    config.TargetServer.IPAddress, config.TargetServer.Port, connectivityResult.ErrorMessage);

                // 等待连接恢复 / Wait for connectivity to be restored
                var connectivityRestored = await _networkRetryService.WaitForConnectivityAsync(
                    config.TargetServer.IPAddress,
                    config.TargetServer.Port,
                    TimeSpan.FromMinutes(5),
                    cancellationToken);

                if (!connectivityRestored)
                {
                    var alert = CreateNetworkConnectivityAlert(operationId, config.TargetServer, connectivityResult.ErrorMessage);
                    await _alertingService.SendCriticalErrorAlertAsync(alert, cancellationToken);

                    return new TransferResult
                    {
                        Success = false,
                        ErrorMessage = $"Network connectivity could not be established: {connectivityResult.ErrorMessage}",
                        Duration = DateTime.Now - startTime
                    };
                }
            }

            // 使用重试逻辑执行传输 / Execute transfer with retry logic
            var result = await _networkRetryService.ExecuteWithRetryAsync(
                async (ct) => await _baseClient.TransferFileAsync(filePath, config, ct),
                "FileTransfer",
                operationId,
                cancellationToken);

            var duration = DateTime.Now - startTime;
            _logger.LogInformation("Enhanced file transfer completed for {FilePath}: Success={Success}, Duration={Duration}ms (Operation ID: {OperationId})",
                filePath, result.Success, duration.TotalMilliseconds, operationId);

            // 如果传输失败则发送警报 / Send alert if transfer failed
            if (!result.Success)
            {
                var alert = CreateTransferFailureAlert(operationId, filePath, config.TargetServer, result.ErrorMessage);
                await _alertingService.SendCriticalErrorAlertAsync(alert, cancellationToken);
            }

            return result;
        }
        catch (NetworkRetryException ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError(ex, "Enhanced file transfer failed after retry attempts for {FilePath} (Operation ID: {OperationId})",
                filePath, operationId);

            // 发送关键错误警报 / Send critical error alert
            var alert = CreateNetworkRetryExhaustedAlert(operationId, filePath, config.TargetServer, ex);
            await _alertingService.SendCriticalErrorAlertAsync(alert, cancellationToken);

            return new TransferResult
            {
                Success = false,
                ErrorMessage = $"Transfer failed after {ex.AttemptsExhausted} retry attempts: {ex.Message}",
                Duration = duration
            };
        }
        catch (Exception ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError(ex, "Unexpected error during enhanced file transfer for {FilePath} (Operation ID: {OperationId})",
                filePath, operationId);

            // 发送关键错误警报 / Send critical error alert
            var alert = CreateUnexpectedErrorAlert(operationId, filePath, config.TargetServer, ex);
            await _alertingService.SendCriticalErrorAlertAsync(alert, cancellationToken);

            return new TransferResult
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}",
                Duration = duration
            };
        }
    }

    /// <summary>
    /// 使用增强的重试和警报功能恢复中断的文件传输 / Resumes an interrupted file transfer with enhanced retry and alerting
    /// </summary>
    /// <param name="resumeToken">恢复令牌 / Resume token</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>传输结果 / Transfer result</returns>
    public async Task<TransferResult> ResumeTransferAsync(string resumeToken, CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid().ToString();
        var startTime = DateTime.Now;

        try
        {
            _logger.LogInformation("Starting enhanced resume transfer with token {ResumeToken} (Operation ID: {OperationId})",
                resumeToken, operationId);

            // 使用重试逻辑执行恢复 / Execute resume with retry logic
            var result = await _networkRetryService.ExecuteWithRetryAsync(
                async (ct) => await _baseClient.ResumeTransferAsync(resumeToken, ct),
                "ResumeTransfer",
                operationId,
                cancellationToken);

            var duration = DateTime.Now - startTime;
            _logger.LogInformation("Enhanced resume transfer completed: Success={Success}, Duration={Duration}ms (Operation ID: {OperationId})",
                result.Success, duration.TotalMilliseconds, operationId);

            // 如果恢复失败则发送警报 / Send alert if resume failed
            if (!result.Success)
            {
                var alert = CreateResumeFailureAlert(operationId, resumeToken, result.ErrorMessage);
                await _alertingService.SendCriticalErrorAlertAsync(alert, cancellationToken);
            }

            return result;
        }
        catch (NetworkRetryException ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError(ex, "Enhanced resume transfer failed after retry attempts for token {ResumeToken} (Operation ID: {OperationId})",
                resumeToken, operationId);

            // Send critical error alert
            var alert = CreateResumeRetryExhaustedAlert(operationId, resumeToken, ex);
            await _alertingService.SendCriticalErrorAlertAsync(alert, cancellationToken);

            return new TransferResult
            {
                Success = false,
                ErrorMessage = $"Resume failed after {ex.AttemptsExhausted} retry attempts: {ex.Message}",
                Duration = duration
            };
        }
        catch (Exception ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError(ex, "Unexpected error during enhanced resume transfer for token {ResumeToken} (Operation ID: {OperationId})",
                resumeToken, operationId);

            // Send critical error alert
            var alert = CreateResumeUnexpectedErrorAlert(operationId, resumeToken, ex);
            await _alertingService.SendCriticalErrorAlertAsync(alert, cancellationToken);

            return new TransferResult
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}",
                Duration = duration
            };
        }
    }

    /// <summary>
    /// 使用完整上下文和增强功能恢复中断的文件传输 / Resumes an interrupted file transfer with full context and enhanced capabilities
    /// </summary>
    /// <param name="resumeToken">恢复令牌 / Resume token</param>
    /// <param name="filePath">文件路径 / File path</param>
    /// <param name="config">传输配置 / Transfer configuration</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>传输结果 / Transfer result</returns>
    public async Task<TransferResult> ResumeTransferAsync(string resumeToken, string filePath, TransferConfig config, CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid().ToString();
        var startTime = DateTime.Now;

        try
        {
            _logger.LogInformation("Starting enhanced resume transfer with token {ResumeToken} for file {FilePath} (Operation ID: {OperationId})",
                resumeToken, filePath, operationId);

            // Test connectivity first
            var connectivityResult = await _networkRetryService.TestConnectivityAsync(
                config.TargetServer.IPAddress,
                config.TargetServer.Port,
                TimeSpan.FromSeconds(30),
                cancellationToken);

            if (!connectivityResult.IsReachable)
            {
                _logger.LogWarning("Network connectivity test failed for resume transfer to {Server}:{Port} - {ErrorMessage}",
                    config.TargetServer.IPAddress, config.TargetServer.Port, connectivityResult.ErrorMessage);

                // Wait for connectivity to be restored
                var connectivityRestored = await _networkRetryService.WaitForConnectivityAsync(
                    config.TargetServer.IPAddress,
                    config.TargetServer.Port,
                    TimeSpan.FromMinutes(5),
                    cancellationToken);

                if (!connectivityRestored)
                {
                    var alert = CreateNetworkConnectivityAlert(operationId, config.TargetServer, connectivityResult.ErrorMessage);
                    await _alertingService.SendCriticalErrorAlertAsync(alert, cancellationToken);

                    return new TransferResult
                    {
                        Success = false,
                        ErrorMessage = $"Network connectivity could not be established for resume: {connectivityResult.ErrorMessage}",
                        Duration = DateTime.Now - startTime
                    };
                }
            }

            // Execute resume with retry logic
            var result = await _networkRetryService.ExecuteWithRetryAsync(
                async (ct) => await _baseClient.ResumeTransferAsync(resumeToken, filePath, config, ct),
                "ResumeTransferWithContext",
                operationId,
                cancellationToken);

            var duration = DateTime.Now - startTime;
            _logger.LogInformation("Enhanced resume transfer with context completed for {FilePath}: Success={Success}, Duration={Duration}ms (Operation ID: {OperationId})",
                filePath, result.Success, duration.TotalMilliseconds, operationId);

            // Send alert if resume failed
            if (!result.Success)
            {
                var alert = CreateResumeWithContextFailureAlert(operationId, resumeToken, filePath, config.TargetServer, result.ErrorMessage);
                await _alertingService.SendCriticalErrorAlertAsync(alert, cancellationToken);
            }

            return result;
        }
        catch (NetworkRetryException ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError(ex, "Enhanced resume transfer with context failed after retry attempts for {FilePath} (Operation ID: {OperationId})",
                filePath, operationId);

            // Send critical error alert
            var alert = CreateResumeWithContextRetryExhaustedAlert(operationId, resumeToken, filePath, config.TargetServer, ex);
            await _alertingService.SendCriticalErrorAlertAsync(alert, cancellationToken);

            return new TransferResult
            {
                Success = false,
                ErrorMessage = $"Resume with context failed after {ex.AttemptsExhausted} retry attempts: {ex.Message}",
                Duration = duration
            };
        }
        catch (Exception ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError(ex, "Unexpected error during enhanced resume transfer with context for {FilePath} (Operation ID: {OperationId})",
                filePath, operationId);

            // Send critical error alert
            var alert = CreateResumeWithContextUnexpectedErrorAlert(operationId, resumeToken, filePath, config.TargetServer, ex);
            await _alertingService.SendCriticalErrorAlertAsync(alert, cancellationToken);

            return new TransferResult
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}",
                Duration = duration
            };
        }
    }

    #region 警报创建方法 / Alert Creation Methods

    /// <summary>
    /// 创建网络连接失败警报 / Create network connectivity failure alert
    /// </summary>
    private CriticalErrorAlert CreateNetworkConnectivityAlert(string operationId, ServerEndpoint server, string? errorMessage)
    {
        return new CriticalErrorAlert
        {
            OperationId = operationId,
            ErrorType = "NetworkConnectivityFailure",
            ErrorMessage = $"Failed to establish network connectivity to {server.IPAddress}:{server.Port}",
            Context = new Dictionary<string, object>
            {
                ["ServerIP"] = server.IPAddress,
                ["ServerPort"] = server.Port,
                ["UseSSL"] = server.UseSSL,
                ["ConnectivityError"] = errorMessage ?? "Unknown"
            }
        };
    }

    /// <summary>
    /// 创建文件传输失败警报 / Create file transfer failure alert
    /// </summary>
    private CriticalErrorAlert CreateTransferFailureAlert(string operationId, string filePath, ServerEndpoint server, string? errorMessage)
    {
        return new CriticalErrorAlert
        {
            OperationId = operationId,
            ErrorType = "FileTransferFailure",
            ErrorMessage = $"File transfer failed for {Path.GetFileName(filePath)}",
            Context = new Dictionary<string, object>
            {
                ["FilePath"] = filePath,
                ["ServerIP"] = server.IPAddress,
                ["ServerPort"] = server.Port,
                ["TransferError"] = errorMessage ?? "Unknown"
            }
        };
    }

    /// <summary>
    /// 创建网络重试耗尽警报 / Create network retry exhausted alert
    /// </summary>
    private CriticalErrorAlert CreateNetworkRetryExhaustedAlert(string operationId, string filePath, ServerEndpoint server, NetworkRetryException ex)
    {
        return new CriticalErrorAlert
        {
            OperationId = operationId,
            ErrorType = "NetworkRetryExhausted",
            ErrorMessage = $"Network retry attempts exhausted for file transfer of {Path.GetFileName(filePath)}",
            StackTrace = ex.StackTrace,
            Context = new Dictionary<string, object>
            {
                ["FilePath"] = filePath,
                ["ServerIP"] = server.IPAddress,
                ["ServerPort"] = server.Port,
                ["AttemptsExhausted"] = ex.AttemptsExhausted,
                ["TotalDuration"] = ex.TotalDuration.ToString(),
                ["OperationName"] = ex.OperationName
            }
        };
    }

    /// <summary>
    /// 创建意外错误警报 / Create unexpected error alert
    /// </summary>
    private CriticalErrorAlert CreateUnexpectedErrorAlert(string operationId, string filePath, ServerEndpoint server, Exception ex)
    {
        return new CriticalErrorAlert
        {
            OperationId = operationId,
            ErrorType = "UnexpectedTransferError",
            ErrorMessage = $"Unexpected error during file transfer of {Path.GetFileName(filePath)}: {ex.Message}",
            StackTrace = ex.StackTrace,
            Context = new Dictionary<string, object>
            {
                ["FilePath"] = filePath,
                ["ServerIP"] = server.IPAddress,
                ["ServerPort"] = server.Port,
                ["ExceptionType"] = ex.GetType().Name
            }
        };
    }

    private CriticalErrorAlert CreateResumeFailureAlert(string operationId, string resumeToken, string? errorMessage)
    {
        return new CriticalErrorAlert
        {
            OperationId = operationId,
            ErrorType = "ResumeTransferFailure",
            ErrorMessage = $"Resume transfer failed for token {resumeToken}",
            Context = new Dictionary<string, object>
            {
                ["ResumeToken"] = resumeToken,
                ["ResumeError"] = errorMessage ?? "Unknown"
            }
        };
    }

    private CriticalErrorAlert CreateResumeRetryExhaustedAlert(string operationId, string resumeToken, NetworkRetryException ex)
    {
        return new CriticalErrorAlert
        {
            OperationId = operationId,
            ErrorType = "ResumeRetryExhausted",
            ErrorMessage = $"Resume transfer retry attempts exhausted for token {resumeToken}",
            StackTrace = ex.StackTrace,
            Context = new Dictionary<string, object>
            {
                ["ResumeToken"] = resumeToken,
                ["AttemptsExhausted"] = ex.AttemptsExhausted,
                ["TotalDuration"] = ex.TotalDuration.ToString(),
                ["OperationName"] = ex.OperationName
            }
        };
    }

    private CriticalErrorAlert CreateResumeUnexpectedErrorAlert(string operationId, string resumeToken, Exception ex)
    {
        return new CriticalErrorAlert
        {
            OperationId = operationId,
            ErrorType = "UnexpectedResumeError",
            ErrorMessage = $"Unexpected error during resume transfer for token {resumeToken}: {ex.Message}",
            StackTrace = ex.StackTrace,
            Context = new Dictionary<string, object>
            {
                ["ResumeToken"] = resumeToken,
                ["ExceptionType"] = ex.GetType().Name
            }
        };
    }

    private CriticalErrorAlert CreateResumeWithContextFailureAlert(string operationId, string resumeToken, string filePath, ServerEndpoint server, string? errorMessage)
    {
        return new CriticalErrorAlert
        {
            OperationId = operationId,
            ErrorType = "ResumeWithContextFailure",
            ErrorMessage = $"Resume transfer with context failed for {Path.GetFileName(filePath)}",
            Context = new Dictionary<string, object>
            {
                ["ResumeToken"] = resumeToken,
                ["FilePath"] = filePath,
                ["ServerIP"] = server.IPAddress,
                ["ServerPort"] = server.Port,
                ["ResumeError"] = errorMessage ?? "Unknown"
            }
        };
    }

    private CriticalErrorAlert CreateResumeWithContextRetryExhaustedAlert(string operationId, string resumeToken, string filePath, ServerEndpoint server, NetworkRetryException ex)
    {
        return new CriticalErrorAlert
        {
            OperationId = operationId,
            ErrorType = "ResumeWithContextRetryExhausted",
            ErrorMessage = $"Resume transfer with context retry attempts exhausted for {Path.GetFileName(filePath)}",
            StackTrace = ex.StackTrace,
            Context = new Dictionary<string, object>
            {
                ["ResumeToken"] = resumeToken,
                ["FilePath"] = filePath,
                ["ServerIP"] = server.IPAddress,
                ["ServerPort"] = server.Port,
                ["AttemptsExhausted"] = ex.AttemptsExhausted,
                ["TotalDuration"] = ex.TotalDuration.ToString(),
                ["OperationName"] = ex.OperationName
            }
        };
    }

    private CriticalErrorAlert CreateResumeWithContextUnexpectedErrorAlert(string operationId, string resumeToken, string filePath, ServerEndpoint server, Exception ex)
    {
        return new CriticalErrorAlert
        {
            OperationId = operationId,
            ErrorType = "UnexpectedResumeWithContextError",
            ErrorMessage = $"Unexpected error during resume transfer with context for {Path.GetFileName(filePath)}: {ex.Message}",
            StackTrace = ex.StackTrace,
            Context = new Dictionary<string, object>
            {
                ["ResumeToken"] = resumeToken,
                ["FilePath"] = filePath,
                ["ServerIP"] = server.IPAddress,
                ["ServerPort"] = server.Port,
                ["ExceptionType"] = ex.GetType().Name
            }
        };
    }

    #endregion
}