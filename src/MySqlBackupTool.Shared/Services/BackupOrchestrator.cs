using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.ServiceProcess;
using System.Text;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 备份编排器，协调完整的备份工作流程，包括MySQL管理、压缩和文件传输
/// 负责执行完整的备份流程：停止MySQL -> 压缩数据 -> 启动MySQL -> 验证连接 -> 传输文件
/// </summary>
public class BackupOrchestrator : IBackupOrchestrator
{
    private readonly IMySQLManager _mysqlManager;
    private readonly ICompressionService _compressionService;
    private readonly IServiceChecker _serviceChecker;
    private readonly IFileTransferClient _fileTransferClient;
    private readonly IBackupLogService _backupLogService;
    private readonly ITransferLogService _transferLogService;
    private readonly ILogger<BackupOrchestrator> _logger;

    /// <summary>
    /// 构造函数，初始化备份编排器
    /// </summary>
    /// <param name="mysqlManager">MySQL管理器</param>
    /// <param name="compressionService">压缩服务</param>
    /// <param name="serviceChecker">服务检查器</param>
    /// <param name="fileTransferClient">文件传输客户端</param>
    /// <param name="backupLogService">备份日志服务</param>
    /// <param name="transferLogService">传输日志服务</param>
    /// <param name="logger">日志记录器</param>
    public BackupOrchestrator(
        IMySQLManager mysqlManager,
        ICompressionService compressionService,
        IServiceChecker serviceChecker,
        IFileTransferClient fileTransferClient,
        IBackupLogService backupLogService,
        ITransferLogService transferLogService,
        ILogger<BackupOrchestrator> logger)
    {
        _mysqlManager = mysqlManager ?? throw new ArgumentNullException(nameof(mysqlManager));
        _compressionService = compressionService ?? throw new ArgumentNullException(nameof(compressionService));
        _serviceChecker = serviceChecker ?? throw new ArgumentNullException(nameof(serviceChecker));
        _fileTransferClient = fileTransferClient ?? throw new ArgumentNullException(nameof(fileTransferClient));
        _backupLogService = backupLogService ?? throw new ArgumentNullException(nameof(backupLogService));
        _transferLogService = transferLogService ?? throw new ArgumentNullException(nameof(transferLogService));
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
        var startTime = DateTime.Now;
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
                    CompletedAt = DateTime.Now,
                    Duration = DateTime.Now - startTime
                };
            }

            // 创建备份日志条目
            backupLog = await _backupLogService.StartBackupAsync(configuration.Id);

            //服务检查
            var serviceCheckResult = await _serviceChecker.CheckServiceAsync(configuration.MySQLConnection.ServiceName);
            if (!serviceCheckResult.Exists)
            {
                var errorMessage = BuildServiceNotFoundMessage(configuration.MySQLConnection.ServiceName, serviceCheckResult);
                _logger.LogError("Service not found: {ServiceName}", configuration.MySQLConnection.ServiceName);
                return new BackupResult
                {
                    OperationId = operationId,
                    Success = false,
                    ErrorMessage = $"Service Check Result: {errorMessage}",
                    CompletedAt = DateTime.Now,
                    Duration = DateTime.Now - startTime
                };
            }

            if (!serviceCheckResult.CanBeBackedUp)
            {
                var errorMessage = BuildServiceCannotBackupMessage(serviceCheckResult);
                _logger.LogError("Service cannot be backed up: {ServiceName}, Status: {Status}, CanStop: {CanStop}",
                    configuration.MySQLConnection.ServiceName, serviceCheckResult.Status, serviceCheckResult.CanStop);
                return new BackupResult
                {
                    OperationId = operationId,
                    Success = false,
                    ErrorMessage = $"Service Check Result: {errorMessage}",
                    CompletedAt = DateTime.Now,
                    Duration = DateTime.Now - startTime
                };
            }

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
                // 检查服务是否存在
                var serviceExists = await ServiceExistsAsync(configuration.MySQLConnection.ServiceName);

                string errorMessage;
                if (!serviceExists)
                {
                    // 服务不存在，建议可能的服务名
                    var suggestions = await SuggestServiceNamesAsync();
                    errorMessage = $"MySQL服务 '{configuration.MySQLConnection.ServiceName}' 不存在。\n\n" +
                                  "可能的原因:\n" +
                                  "1. MySQL未安装或服务名称不正确\n" +
                                  "2. 常见的MySQL服务名: MySQL, MySQL80, MySQL57, MariaDB\n\n" +
                                 $"检测到的MySQL服务: {(suggestions.Any() ? string.Join(", ", suggestions) : "无")}";
                }
                else
                {
                    // 服务存在但无法停止
                    errorMessage = $"无法停止MySQL服务 '{configuration.MySQLConnection.ServiceName}'。\n\n" +
                                  "可能的原因:\n" +
                                  "1. 权限不足 - 请以管理员身份运行此程序\n" +
                                  "2. 有其他程序正在使用MySQL数据库\n" +
                                  "3. 服务可能处于不可停止的状态\n\n" +
                                  "建议的操作:\n" +
                                  "1. 关闭所有MySQL客户端程序（如MySQL Workbench, phpMyAdmin等）\n" +
                                  "2. 在服务管理器中手动停止MySQL服务\n" +
                                  "3. 以管理员身份重新启动此程序";
                }

                //var errorMessage = $"Failed to stop MySQL service: {configuration.MySQLConnection.ServiceName}";
                _logger.LogError("MySQL stop failed for operation {OperationId}: {ServiceName}", 
                    operationId, configuration.MySQLConnection.ServiceName);

                // 记录详细错误
                await LogServiceDetailsAsync(configuration.MySQLConnection.ServiceName);

                await _backupLogService.CompleteBackupAsync(backupLog.Id, BackupStatus.Failed, errorMessage: errorMessage);
                
                return new BackupResult
                {
                    OperationId = operationId,
                    Success = false,
                    ErrorMessage = errorMessage,
                    CompletedAt = DateTime.Now,
                    Duration = DateTime.Now - startTime
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
                    CompletedAt = DateTime.Now,
                    Duration = DateTime.Now - startTime
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
                    CompletedAt = DateTime.Now,
                    Duration = DateTime.Now - startTime
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
                    CompletedAt = DateTime.Now,
                    Duration = DateTime.Now - startTime
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            // 步骤5：传输文件
            backupProgress.CurrentStatus = BackupStatus.Transferring;
            backupProgress.CurrentOperation = "Transferring backup file";
            backupProgress.OverallProgress = 0.7;
            progress?.Report(backupProgress);

            var fileInfo = new FileInfo(compressedFilePath);
            var targetFileName = configuration.NamingStrategy.GenerateFileName("BackupServer", configuration.Name, DateTime.Now);
            
            var transferConfig = new TransferConfig
            {
                TargetServer = configuration.TargetServer,
                TargetDirectory = configuration.TargetDirectory,
                FileName = targetFileName,
                ChunkingStrategy = new ChunkingStrategy(),
                MaxRetries = 3,
                TimeoutSeconds = 300
            };

            // Ensure the target server has the authentication credentials from the backup configuration
            if (transferConfig.TargetServer.ClientCredentials == null)
            {
                transferConfig.TargetServer.ClientCredentials = new ClientCredentials
                {
                    ClientId = configuration.ClientId,
                    ClientSecret = configuration.ClientSecret
                };
            }
            else if (string.IsNullOrWhiteSpace(transferConfig.TargetServer.ClientCredentials.ClientId) ||
                     string.IsNullOrWhiteSpace(transferConfig.TargetServer.ClientCredentials.ClientSecret))
            {
                // Update existing credentials if they are empty
                transferConfig.TargetServer.ClientCredentials.ClientId = configuration.ClientId;
                transferConfig.TargetServer.ClientCredentials.ClientSecret = configuration.ClientSecret;
            }

            _logger.LogInformation("Transferring backup file for operation {OperationId}, size: {FileSize} bytes", 
                operationId, fileInfo.Length);

            // 计算分块数量并创建传输日志记录
            var chunkSize = transferConfig.ChunkingStrategy.ChunkSize;
            var totalChunks = (int)Math.Ceiling((double)fileInfo.Length / chunkSize);
            
            _logger.LogInformation("File will be transferred in {TotalChunks} chunks for backup log {BackupLogId}", 
                totalChunks, backupLog.Id);

            // 批量创建传输日志记录
            var chunks = Enumerable.Range(0, totalChunks).Select(i => new ChunkInfo
            {
                ChunkIndex = i,
                ChunkSize = Math.Min(chunkSize, fileInfo.Length - (i * chunkSize)),
                Status = "Pending"
            });

            try
            {
                await _transferLogService.BatchCreateTransferChunksAsync(backupLog.Id, chunks);
                _logger.LogInformation("Created {Count} transfer log entries for backup {BackupLogId}", 
                    totalChunks, backupLog.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create transfer log entries, continuing with transfer");
            }

            TransferResult transferResult;
            try
            {
                transferResult = await _fileTransferClient.TransferFileAsync(compressedFilePath, transferConfig, cancellationToken);
                
                // 传输完成后，更新所有传输日志状态
                if (transferResult.Success)
                {
                    _logger.LogInformation("File transfer completed successfully, updating transfer logs");
                    // 注意：这里简化处理，实际应该在传输过程中实时更新每个分块的状态
                    // 可以通过扩展IFileTransferClient接口来支持分块级别的进度回调
                }
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
                    CompletedAt = DateTime.Now,
                    Duration = DateTime.Now - startTime
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
                    CompletedAt = DateTime.Now,
                    Duration = DateTime.Now - startTime
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

            var completedAt = DateTime.Now;
            var duration = completedAt - startTime;

            // 记录成功完成
            await _backupLogService.CompleteBackupAsync(backupLog.Id, BackupStatus.Completed, transferConfig.TargetDirectory +"\\"+ targetFileName, fileInfo.Length);

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
                CompletedAt = DateTime.Now,
                Duration = DateTime.Now - startTime
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
                CompletedAt = DateTime.Now,
                Duration = DateTime.Now - startTime
            };
        }
    }

    // 辅助方法
    private async Task<bool> ServiceExistsAsync(string serviceName)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var service = new ServiceController(serviceName);
                var status = service.Status; // 这行会抛出异常如果服务不存在
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    private async Task<List<string>> SuggestServiceNamesAsync()
    {
        return await Task.Run(() =>
        {
            var suggestions = new List<string>();
            try
            {
                var services = ServiceController.GetServices();
                var mysqlServices = services.Where(s =>
                    s.ServiceName.Contains("mysql", StringComparison.OrdinalIgnoreCase) ||
                    s.ServiceName.Contains("mariadb", StringComparison.OrdinalIgnoreCase))
                    .Select(s => s.ServiceName)
                    .ToList();

                suggestions.AddRange(mysqlServices);
            }
            catch
            {
                // 忽略错误
            }
            return suggestions;
        });
    }

    private async Task LogServiceDetailsAsync(string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            _logger.LogInformation("Service details - Name: {ServiceName}, DisplayName: {DisplayName}, Status: {Status}, Type: {ServiceType}, CanStop: {CanStop}",
                service.ServiceName, service.DisplayName, service.Status, service.ServiceType, service.CanStop);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not get service details for: {ServiceName}", serviceName);
        }
    }
    private string BuildServiceNotFoundMessage(string serviceName, ServiceCheckResult result)
    {
        var suggestions = _serviceChecker.ListMySQLServicesAsync().Result;

        var sb = new StringBuilder();
        sb.AppendLine($"MySQL服务 '{serviceName}' 不存在。");
        sb.AppendLine();
        sb.AppendLine("可能的原因：");
        sb.AppendLine("1. MySQL未正确安装");
        sb.AppendLine("2. 服务名称不正确");
        sb.AppendLine("3. 服务被禁用或删除");
        sb.AppendLine();

        if (suggestions.Any())
        {
            sb.AppendLine("检测到的MySQL服务：");
            foreach (var service in suggestions)
            {
                sb.AppendLine($"  • {service.ServiceName} ({service.DisplayName}) - {service.StatusDescription}");
            }
        }
        else
        {
            sb.AppendLine("未检测到任何MySQL服务。请确保MySQL已安装并运行。");
        }

        return sb.ToString();
    }

    private string BuildServiceCannotBackupMessage(ServiceCheckResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"无法备份服务 '{result.ServiceName}'。");
        sb.AppendLine($"状态: {result.Status}");

        if (result.CanStop == false)
        {
            sb.AppendLine("原因: 服务配置为不可停止");
            if (!string.IsNullOrEmpty(result.AccessError))
            {
                sb.AppendLine($"权限错误: {result.AccessError}");
                sb.AppendLine("建议: 请以管理员身份运行此程序");
            }
        }
        else if (result.DependentServices.Any())
        {
            sb.AppendLine($"警告: 有 {result.DependentServices.Length} 个程序依赖此服务");
            sb.AppendLine("依赖程序:");
            foreach (var dep in result.DependentServices.Take(5))
            {
                sb.AppendLine($"  • {dep}");
            }
            if (result.DependentServices.Length > 5)
            {
                sb.AppendLine($"  ... 以及 {result.DependentServices.Length - 5} 个其他程序");
            }
        }

        if (!string.IsNullOrEmpty(result.BackupAdvice))
        {
            sb.AppendLine();
            sb.AppendLine("建议:");
            sb.AppendLine(result.BackupAdvice);
        }

        return sb.ToString();
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