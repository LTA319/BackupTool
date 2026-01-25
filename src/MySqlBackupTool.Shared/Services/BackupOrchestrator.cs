using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 备份编排器，协调完整的备份工作流程，包括MySQL管理、压缩和文件传输
/// 负责执行完整的备份流程：停止MySQL -> 压缩数据 -> 启动MySQL -> 验证连接 -> 传输文件
/// </summary>
public class BackupOrchestrator : IBackupOrchestrator
{
    private readonly IMySQLManager _mysqlManager;
    private readonly ICompressionService _compressionService;
    private readonly IFileTransferClient _fileTransferClient;
    private readonly IBackupLogService _backupLogService;
    private readonly ILogger<BackupOrchestrator> _logger;

    /// <summary>
    /// 构造函数，初始化备份编排器
    /// </summary>
    /// <param name="mysqlManager">MySQL管理器</param>
    /// <param name="compressionService">压缩服务</param>
    /// <param name="fileTransferClient">文件传输客户端</param>
    /// <param name="backupLogService">备份日志服务</param>
    /// <param name="logger">日志记录器</param>
    public BackupOrchestrator(
        IMySQLManager mysqlManager,
        ICompressionService compressionService,
        IFileTransferClient fileTransferClient,
        IBackupLogService backupLogService,
        ILogger<BackupOrchestrator> logger)
    {
        _mysqlManager = mysqlManager ?? throw new ArgumentNullException(nameof(mysqlManager));
        _compressionService = compressionService ?? throw new ArgumentNullException(nameof(compressionService));
        _fileTransferClient = fileTransferClient ?? throw new ArgumentNullException(nameof(fileTransferClient));
        _backupLogService = backupLogService ?? throw new ArgumentNullException(nameof(backupLogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 执行完整的备份工作流程
    /// </summary>
    /// <param name="configuration">备份配置</param>
    /// <param name="progress">进度报告器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>备份结果</returns>
    public async Task<BackupResult> ExecuteBackupAsync(BackupConfiguration configuration, IProgress<BackupProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid();
        var startTime = DateTime.UtcNow;
        BackupLog? backupLog = null;
        var backupProgress = new BackupProgress
        {
            OperationId = operationId,
            CurrentStatus = BackupStatus.Queued,
            OverallProgress = 0.0,
            CurrentOperation = "Initializing backup operation"
        };

        _logger.LogInformation("Starting backup operation {OperationId} for configuration {ConfigurationName}", 
            operationId, configuration.Name);

        try
        {
            // 验证配置
            var validationResult = await ValidateConfigurationAsync(configuration);
            if (!validationResult.IsValid)
            {
                var errorMessage = string.Join(", ", validationResult.Errors);
                _logger.LogError("Configuration validation failed for operation {OperationId}: {Errors}", operationId, errorMessage);
                
                return new BackupResult
                {
                    OperationId = operationId,
                    Success = false,
                    ErrorMessage = $"Configuration validation failed: {errorMessage}",
                    CompletedAt = DateTime.UtcNow,
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // 创建备份日志条目
            backupLog = await _backupLogService.StartBackupAsync(configuration.Id);

            // 步骤1：停止MySQL实例
            backupProgress.CurrentStatus = BackupStatus.StoppingMySQL;
            backupProgress.CurrentOperation = "Stopping MySQL service";
            backupProgress.OverallProgress = 0.1;
            progress?.Report(backupProgress);

            _logger.LogInformation("Stopping MySQL service {ServiceName} for operation {OperationId}", 
                configuration.MySQLConnection.ServiceName, operationId);

            var stopResult = await _mysqlManager.StopInstanceAsync(configuration.MySQLConnection.ServiceName);
            if (!stopResult)
            {
                var errorMessage = $"Failed to stop MySQL service: {configuration.MySQLConnection.ServiceName}";
                _logger.LogError("MySQL stop failed for operation {OperationId}: {ServiceName}", 
                    operationId, configuration.MySQLConnection.ServiceName);

                await _backupLogService.CompleteBackupAsync(backupLog.Id, BackupStatus.Failed, errorMessage: errorMessage);
                
                return new BackupResult
                {
                    OperationId = operationId,
                    Success = false,
                    ErrorMessage = errorMessage,
                    CompletedAt = DateTime.UtcNow,
                    Duration = DateTime.UtcNow - startTime
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            // 步骤2：压缩数据目录
            backupProgress.CurrentStatus = BackupStatus.Compressing;
            backupProgress.CurrentOperation = "Compressing database files";
            backupProgress.OverallProgress = 0.2;
            progress?.Report(backupProgress);

            var tempBackupPath = Path.Combine(Path.GetTempPath(), $"backup_{operationId}.zip");
            var compressionProgress = new Progress<CompressionProgress>(cp =>
            {
                backupProgress.CurrentOperation = $"Compressing: {cp.CurrentFile}";
                backupProgress.OverallProgress = 0.2 + (cp.Progress * 0.3); // 20% 到 50%
                progress?.Report(backupProgress);
            });

            _logger.LogInformation("Compressing data directory {DataDirectory} for operation {OperationId}", 
                configuration.MySQLConnection.DataDirectoryPath, operationId);

            string compressedFilePath;
            try
            {
                compressedFilePath = await _compressionService.CompressDirectoryAsync(
                    configuration.MySQLConnection.DataDirectoryPath,
                    tempBackupPath,
                    compressionProgress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Compression failed for operation {OperationId}", operationId);
                
                // 在返回错误前尝试重启MySQL
                await RestartMySQLAsync(configuration.MySQLConnection.ServiceName, operationId);
                await _backupLogService.CompleteBackupAsync(backupLog.Id, BackupStatus.Failed, errorMessage: $"Compression failed: {ex.Message}");
                
                return new BackupResult
                {
                    OperationId = operationId,
                    Success = false,
                    ErrorMessage = $"Compression failed: {ex.Message}",
                    CompletedAt = DateTime.UtcNow,
                    Duration = DateTime.UtcNow - startTime
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            // 步骤3：启动MySQL实例
            backupProgress.CurrentStatus = BackupStatus.StartingMySQL;
            backupProgress.CurrentOperation = "Starting MySQL service";
            backupProgress.OverallProgress = 0.5;
            progress?.Report(backupProgress);

            _logger.LogInformation("Starting MySQL service {ServiceName} for operation {OperationId}", 
                configuration.MySQLConnection.ServiceName, operationId);

            var startResult = await _mysqlManager.StartInstanceAsync(configuration.MySQLConnection.ServiceName);
            if (!startResult)
            {
                var errorMessage = $"Failed to start MySQL service: {configuration.MySQLConnection.ServiceName}";
                _logger.LogError("MySQL start failed for operation {OperationId}: {ServiceName}", 
                    operationId, configuration.MySQLConnection.ServiceName);

                // 清理压缩文件
                await CleanupTempFileAsync(compressedFilePath, operationId);
                await _backupLogService.CompleteBackupAsync(backupLog.Id, BackupStatus.Failed, errorMessage: errorMessage);
                
                return new BackupResult
                {
                    OperationId = operationId,
                    Success = false,
                    ErrorMessage = errorMessage,
                    CompletedAt = DateTime.UtcNow,
                    Duration = DateTime.UtcNow - startTime
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            // 步骤4：验证MySQL可用性
            backupProgress.CurrentStatus = BackupStatus.Verifying;
            backupProgress.CurrentOperation = "Verifying MySQL availability";
            backupProgress.OverallProgress = 0.6;
            progress?.Report(backupProgress);

            _logger.LogInformation("Verifying MySQL availability for operation {OperationId}", operationId);

            var verifyResult = await _mysqlManager.VerifyInstanceAvailabilityAsync(configuration.MySQLConnection);
            if (!verifyResult)
            {
                var errorMessage = "MySQL instance is not available after restart";
                _logger.LogError("MySQL verification failed for operation {OperationId}", operationId);

                // 清理压缩文件
                await CleanupTempFileAsync(compressedFilePath, operationId);
                await _backupLogService.CompleteBackupAsync(backupLog.Id, BackupStatus.Failed, errorMessage: errorMessage);
                
                return new BackupResult
                {
                    OperationId = operationId,
                    Success = false,
                    ErrorMessage = errorMessage,
                    CompletedAt = DateTime.UtcNow,
                    Duration = DateTime.UtcNow - startTime
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            // 步骤5：传输文件
            backupProgress.CurrentStatus = BackupStatus.Transferring;
            backupProgress.CurrentOperation = "Transferring backup file";
            backupProgress.OverallProgress = 0.7;
            progress?.Report(backupProgress);

            var fileInfo = new FileInfo(compressedFilePath);
            var targetFileName = configuration.NamingStrategy.GenerateFileName("BackupServer", configuration.Name, DateTime.UtcNow);
            
            var transferConfig = new TransferConfig
            {
                TargetServer = configuration.TargetServer,
                TargetDirectory = configuration.TargetDirectory,
                FileName = targetFileName,
                ChunkingStrategy = new ChunkingStrategy(),
                MaxRetries = 3,
                TimeoutSeconds = 300
            };

            _logger.LogInformation("Transferring backup file for operation {OperationId}, size: {FileSize} bytes", 
                operationId, fileInfo.Length);

            TransferResult transferResult;
            try
            {
                transferResult = await _fileTransferClient.TransferFileAsync(compressedFilePath, transferConfig, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File transfer failed for operation {OperationId}", operationId);
                
                // 清理压缩文件
                await CleanupTempFileAsync(compressedFilePath, operationId);
                await _backupLogService.CompleteBackupAsync(backupLog.Id, BackupStatus.Failed, errorMessage: $"File transfer failed: {ex.Message}");
                
                return new BackupResult
                {
                    OperationId = operationId,
                    Success = false,
                    ErrorMessage = $"File transfer failed: {ex.Message}",
                    CompletedAt = DateTime.UtcNow,
                    Duration = DateTime.UtcNow - startTime
                };
            }

            if (!transferResult.Success)
            {
                _logger.LogError("File transfer failed for operation {OperationId}: {ErrorMessage}", 
                    operationId, transferResult.ErrorMessage);

                // 清理压缩文件
                await CleanupTempFileAsync(compressedFilePath, operationId);
                await _backupLogService.CompleteBackupAsync(backupLog.Id, BackupStatus.Failed, errorMessage: transferResult.ErrorMessage ?? "Unknown transfer error");
                
                return new BackupResult
                {
                    OperationId = operationId,
                    Success = false,
                    ErrorMessage = transferResult.ErrorMessage ?? "File transfer failed",
                    CompletedAt = DateTime.UtcNow,
                    Duration = DateTime.UtcNow - startTime
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            // 步骤6：清理
            backupProgress.CurrentStatus = BackupStatus.Completed;
            backupProgress.CurrentOperation = "Cleaning up temporary files";
            backupProgress.OverallProgress = 0.9;
            progress?.Report(backupProgress);

            await CleanupTempFileAsync(compressedFilePath, operationId);

            // 最终进度更新
            backupProgress.OverallProgress = 1.0;
            backupProgress.CurrentOperation = "Backup completed successfully";
            progress?.Report(backupProgress);

            var completedAt = DateTime.UtcNow;
            var duration = completedAt - startTime;

            // 记录成功完成
            await _backupLogService.CompleteBackupAsync(backupLog.Id, BackupStatus.Completed, targetFileName, fileInfo.Length);

            _logger.LogInformation("Backup operation {OperationId} completed successfully in {Duration}", 
                operationId, duration);

            return new BackupResult
            {
                OperationId = operationId,
                Success = true,
                BackupFilePath = targetFileName,
                FileSize = fileInfo.Length,
                Duration = duration,
                CompletedAt = completedAt,
                ChecksumHash = transferResult.ChecksumHash
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Backup operation {OperationId} was cancelled", operationId);
            
            // 确保在操作取消时重启MySQL
            await RestartMySQLAsync(configuration.MySQLConnection.ServiceName, operationId);
            await _backupLogService.CancelBackupAsync(backupLog?.Id ?? 0, "Operation was cancelled by user");
            
            return new BackupResult
            {
                OperationId = operationId,
                Success = false,
                ErrorMessage = "Backup operation was cancelled",
                CompletedAt = DateTime.UtcNow,
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during backup operation {OperationId}", operationId);
            
            // 确保在操作失败时重启MySQL
            await RestartMySQLAsync(configuration.MySQLConnection.ServiceName, operationId);
            await _backupLogService.CompleteBackupAsync(backupLog?.Id ?? 0, BackupStatus.Failed, errorMessage: $"Unexpected error: {ex.Message}");
            
            return new BackupResult
            {
                OperationId = operationId,
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}",
                CompletedAt = DateTime.UtcNow,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// 在执行前验证备份配置
    /// </summary>
    /// <param name="configuration">要验证的备份配置</param>
    /// <returns>验证结果</returns>
    public async Task<BackupValidationResult> ValidateConfigurationAsync(BackupConfiguration configuration)
    {
        var result = new BackupValidationResult { IsValid = true };
        var errors = new List<string>();
        var warnings = new List<string>();

        try
        {
            // 验证MySQL连接
            if (configuration.MySQLConnection == null)
            {
                errors.Add("MySQL connection information is required");
            }
            else
            {
                var connectionValidation = configuration.MySQLConnection.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(configuration.MySQLConnection));
                errors.AddRange(connectionValidation.Select(v => v.ErrorMessage ?? "Unknown validation error"));

                // 检查数据目录是否存在
                if (!Directory.Exists(configuration.MySQLConnection.DataDirectoryPath))
                {
                    errors.Add($"MySQL data directory not found: {configuration.MySQLConnection.DataDirectoryPath}");
                }
            }

            // 验证目标服务器
            if (configuration.TargetServer == null)
            {
                errors.Add("Target server information is required");
            }
            else
            {
                var (isValid, serverErrors) = configuration.TargetServer.ValidateEndpoint();
                if (!isValid)
                {
                    errors.AddRange(serverErrors);
                }
            }

            // 验证备份目录
            if (string.IsNullOrWhiteSpace(configuration.TargetDirectory))
            {
                errors.Add("Target directory is required");
            }

            // 验证命名策略
            if (configuration.NamingStrategy == null)
            {
                warnings.Add("No naming strategy specified, using default");
            }

            // 检查磁盘空间（仅警告）
            try
            {
                var tempPath = Path.GetTempPath();
                var drive = new DriveInfo(Path.GetPathRoot(tempPath) ?? tempPath);
                if (drive.AvailableFreeSpace < 1024 * 1024 * 1024) // 少于1GB
                {
                    warnings.Add($"Low disk space available for temporary files: {drive.AvailableFreeSpace / (1024 * 1024)} MB");
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Could not check available disk space: {ex.Message}");
            }

            result.IsValid = !errors.Any();
            result.Errors = errors;
            result.Warnings = warnings;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during configuration validation");
            
            return new BackupValidationResult
            {
                IsValid = false,
                Errors = new[] { $"Validation error: {ex.Message}" }
            };
        }
    }

    /// <summary>
    /// 尝试重启MySQL服务
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    /// <param name="operationId">操作ID</param>
    private async Task RestartMySQLAsync(string serviceName, Guid operationId)
    {
        try
        {
            _logger.LogInformation("Attempting to restart MySQL service {ServiceName} for operation {OperationId}", 
                serviceName, operationId);
            
            var result = await _mysqlManager.StartInstanceAsync(serviceName);
            if (result)
            {
                _logger.LogInformation("Successfully restarted MySQL service {ServiceName} for operation {OperationId}", 
                    serviceName, operationId);
            }
            else
            {
                _logger.LogError("Failed to restart MySQL service {ServiceName} for operation {OperationId}", 
                    serviceName, operationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting MySQL service {ServiceName} for operation {OperationId}", 
                serviceName, operationId);
        }
    }

    /// <summary>
    /// 清理临时备份文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="operationId">操作ID</param>
    private async Task CleanupTempFileAsync(string filePath, Guid operationId)
    {
        try
        {
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                _logger.LogDebug("Cleaning up temporary file {FilePath} for operation {OperationId}", filePath, operationId);
                await _compressionService.CleanupAsync(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup temporary file {FilePath} for operation {OperationId}", 
                filePath, operationId);
        }
    }
}