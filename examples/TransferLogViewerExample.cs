using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Client.Forms;
using MySqlBackupTool.Shared.DependencyInjection;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Examples;

/// <summary>
/// 传输日志查看器使用示例
/// Transfer log viewer usage example
/// </summary>
public class TransferLogViewerExample
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITransferLogService _transferLogService;
    private readonly IBackupLogService _backupLogService;
    private readonly ILogger<TransferLogViewerExample> _logger;

    public TransferLogViewerExample()
    {
        // 配置服务
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddMySqlBackupToolServices(options =>
        {
            options.DatabasePath = "transfer_log_example.db";
            options.UseInMemoryDatabase = true;
        });
        
        _serviceProvider = services.BuildServiceProvider();
        _transferLogService = _serviceProvider.GetRequiredService<ITransferLogService>();
        _backupLogService = _serviceProvider.GetRequiredService<IBackupLogService>();
        _logger = _serviceProvider.GetRequiredService<ILogger<TransferLogViewerExample>>();
    }

    /// <summary>
    /// 运行传输日志查看器示例
    /// Run transfer log viewer example
    /// </summary>
    public async Task RunAsync()
    {
        _logger.LogInformation("Starting Transfer Log Viewer Example");

        try
        {
            // 1. 创建示例备份日志
            var backupLog = await CreateSampleBackupLogAsync();
            
            // 2. 创建示例传输日志
            await CreateSampleTransferLogsAsync(backupLog.Id);
            
            // 3. 显示传输日志查看器
            await ShowTransferLogViewerAsync(backupLog.Id);
            
            // 4. 演示传输日志操作
            await DemonstrateTransferLogOperationsAsync(backupLog.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running transfer log viewer example");
        }
    }

    /// <summary>
    /// 创建示例备份日志
    /// Create sample backup log
    /// </summary>
    private async Task<BackupLog> CreateSampleBackupLogAsync()
    {
        _logger.LogInformation("Creating sample backup log");
        
        var backupLog = await _backupLogService.StartBackupAsync(1); // 假设配置ID为1
        await _backupLogService.CompleteBackupAsync(backupLog.Id, BackupStatus.Completed, 
            "sample_backup.zip", 1024 * 1024 * 100); // 100MB文件
        
        return backupLog;
    }

    /// <summary>
    /// 创建示例传输日志
    /// Create sample transfer logs
    /// </summary>
    private async Task CreateSampleTransferLogsAsync(int backupLogId)
    {
        _logger.LogInformation("Creating sample transfer logs for backup {BackupLogId}", backupLogId);

        // 模拟100MB文件分成10个分块传输
        var chunkSize = 1024 * 1024 * 10; // 10MB per chunk
        var totalChunks = 10;

        var chunks = new List<ChunkInfo>();
        for (int i = 0; i < totalChunks; i++)
        {
            chunks.Add(new ChunkInfo
            {
                ChunkIndex = i,
                ChunkSize = chunkSize,
                Status = "Pending"
            });
        }

        // 批量创建传输日志
        var transferLogIds = await _transferLogService.BatchCreateTransferChunksAsync(backupLogId, chunks);
        var transferLogIdList = transferLogIds.ToList();

        // 模拟传输过程
        for (int i = 0; i < transferLogIdList.Count; i++)
        {
            var transferLogId = transferLogIdList[i];
            
            // 开始传输
            await _transferLogService.UpdateTransferChunkStatusAsync(transferLogId, "InProgress");
            
            // 模拟传输时间
            await Task.Delay(100);
            
            // 完成传输（大部分成功，少数失败）
            if (i == 3 || i == 7) // 模拟第4和第8个分块失败
            {
                await _transferLogService.CompleteTransferChunkAsync(transferLogId, false, 
                    $"Network timeout during chunk {i} transfer");
            }
            else
            {
                await _transferLogService.CompleteTransferChunkAsync(transferLogId, true);
            }
        }

        _logger.LogInformation("Created {Count} transfer logs with 2 failures", totalChunks);
    }

    /// <summary>
    /// 显示传输日志查看器
    /// Show transfer log viewer
    /// </summary>
    private async Task ShowTransferLogViewerAsync(int backupLogId)
    {
        _logger.LogInformation("Opening Transfer Log Viewer for backup {BackupLogId}", backupLogId);

        // 注意：在实际应用中，这里会显示Windows Forms窗体
        // 这里我们只是演示如何创建和配置窗体
        var transferLogViewer = new TransferLogViewerForm(_serviceProvider);
        transferLogViewer.SetBackupLogId(backupLogId);

        _logger.LogInformation("Transfer Log Viewer configured for backup {BackupLogId}", backupLogId);
        
        // 在实际应用中会调用：
        // transferLogViewer.ShowDialog();
    }

    /// <summary>
    /// 演示传输日志操作
    /// Demonstrate transfer log operations
    /// </summary>
    private async Task DemonstrateTransferLogOperationsAsync(int backupLogId)
    {
        _logger.LogInformation("Demonstrating transfer log operations");

        // 1. 获取传输统计
        var statistics = await _transferLogService.GetTransferStatisticsAsync(backupLogId);
        _logger.LogInformation("Transfer Statistics - Total: {Total}, Successful: {Successful}, Failed: {Failed}, Success Rate: {SuccessRate}%",
            statistics.TotalTransfers, statistics.SuccessfulTransfers, statistics.FailedTransfers, statistics.SuccessRate);

        // 2. 获取失败的传输
        var failedTransfers = await _transferLogService.GetFailedTransferChunksAsync(backupLogId);
        _logger.LogInformation("Found {Count} failed transfers", failedTransfers.Count());

        // 3. 重试失败的传输
        if (failedTransfers.Any())
        {
            var failedIds = failedTransfers.Select(ft => ft.Id);
            var retryCount = await _transferLogService.RetryFailedTransferChunksAsync(failedIds);
            _logger.LogInformation("Reset {Count} failed transfers for retry", retryCount);
        }

        // 4. 获取传输性能指标
        var performanceMetrics = await _transferLogService.GetTransferPerformanceMetricsAsync(backupLogId);
        _logger.LogInformation("Performance Metrics - Average Speed: {Speed} bytes/sec, Efficiency: {Efficiency}%",
            performanceMetrics.AverageTransferSpeedBytesPerSecond, performanceMetrics.TransferEfficiencyPercentage);

        // 5. 导出传输日志
        var csvData = await _transferLogService.ExportTransferLogsAsync(backupLogId, "CSV");
        _logger.LogInformation("Exported transfer logs to CSV format, size: {Size} bytes", csvData.Length);

        var jsonData = await _transferLogService.ExportTransferLogsAsync(backupLogId, "JSON");
        _logger.LogInformation("Exported transfer logs to JSON format, size: {Size} bytes", jsonData.Length);
    }

    /// <summary>
    /// 清理资源
    /// Cleanup resources
    /// </summary>
    public void Dispose()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

/// <summary>
/// 程序入口点示例
/// Example program entry point
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        var example = new TransferLogViewerExample();
        
        try
        {
            await example.RunAsync();
        }
        finally
        {
            example.Dispose();
        }
    }
}