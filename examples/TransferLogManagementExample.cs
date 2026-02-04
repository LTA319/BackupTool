using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Examples;

/// <summary>
/// 传输日志管理使用示例
/// Transfer log management usage example
/// </summary>
public class TransferLogManagementExample
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITransferLogService _transferLogService;
    private readonly IBackupLogService _backupLogService;
    private readonly ILogger<TransferLogManagementExample> _logger;

    public TransferLogManagementExample()
    {
        // 配置依赖注入
        var services = new ServiceCollection();
        var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString("example_backup.db");
        
        services.AddSharedServices(connectionString);
        services.AddLogging(builder => builder.AddConsole());
        
        _serviceProvider = services.BuildServiceProvider();
        _transferLogService = _serviceProvider.GetRequiredService<ITransferLogService>();
        _backupLogService = _serviceProvider.GetRequiredService<IBackupLogService>();
        _logger = _serviceProvider.GetRequiredService<ILogger<TransferLogManagementExample>>();
    }

    /// <summary>
    /// 演示完整的传输日志管理流程
    /// Demonstrates complete transfer log management workflow
    /// </summary>
    public async Task RunCompleteWorkflowAsync()
    {
        try
        {
            _logger.LogInformation("开始传输日志管理示例");

            // 1. 创建备份日志
            var backupLog = await _backupLogService.StartBackupAsync(1);
            _logger.LogInformation("创建备份日志，ID: {BackupLogId}", backupLog.Id);

            // 2. 批量创建传输分块
            await DemonstrateBatchChunkCreationAsync(backupLog.Id);

            // 3. 模拟传输过程
            await SimulateTransferProcessAsync(backupLog.Id);

            // 4. 查看传输进度
            await DemonstrateProgressTrackingAsync(backupLog.Id);

            // 5. 处理失败的传输
            await DemonstrateFailureHandlingAsync(backupLog.Id);

            // 6. 生成统计报告
            await DemonstrateStatisticsReportingAsync(backupLog.Id);

            // 7. 导出传输日志
            await DemonstrateLogExportAsync(backupLog.Id);

            // 8. 清理旧日志
            await DemonstrateLogCleanupAsync();

            _logger.LogInformation("传输日志管理示例完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "传输日志管理示例执行失败");
            throw;
        }
    }

    /// <summary>
    /// 演示批量创建传输分块
    /// Demonstrates batch chunk creation
    /// </summary>
    private async Task DemonstrateBatchChunkCreationAsync(int backupLogId)
    {
        _logger.LogInformation("演示批量创建传输分块");

        // 模拟一个100MB文件分成100个1MB的分块
        var chunks = new List<ChunkInfo>();
        for (int i = 0; i < 100; i++)
        {
            chunks.Add(new ChunkInfo
            {
                ChunkIndex = i,
                ChunkSize = 1024 * 1024, // 1MB
                Status = "Pending"
            });
        }

        var transferLogIds = await _transferLogService.BatchCreateTransferChunksAsync(backupLogId, chunks);
        _logger.LogInformation("批量创建了 {Count} 个传输分块", transferLogIds.Count());
    }

    /// <summary>
    /// 模拟传输过程
    /// Simulates transfer process
    /// </summary>
    private async Task SimulateTransferProcessAsync(int backupLogId)
    {
        _logger.LogInformation("模拟传输过程");

        // 获取所有待传输的分块
        var progress = await _transferLogService.GetTransferProgressAsync(backupLogId);
        _logger.LogInformation("总共需要传输 {TotalChunks} 个分块", progress.TotalChunks);

        // 模拟传输前50个分块成功
        for (int i = 0; i < 50; i++)
        {
            var transferLogId = await _transferLogService.StartTransferChunkAsync(backupLogId, i, 1024 * 1024);
            await _transferLogService.UpdateTransferChunkStatusAsync(transferLogId, "InProgress");
            
            // 模拟传输延迟
            await Task.Delay(10);
            
            await _transferLogService.CompleteTransferChunkAsync(transferLogId, success: true);
        }

        // 模拟传输第51-55个分块失败
        for (int i = 50; i < 55; i++)
        {
            var transferLogId = await _transferLogService.StartTransferChunkAsync(backupLogId, i, 1024 * 1024);
            await _transferLogService.UpdateTransferChunkStatusAsync(transferLogId, "InProgress");
            
            await Task.Delay(10);
            
            await _transferLogService.CompleteTransferChunkAsync(transferLogId, success: false, 
                errorMessage: $"网络超时 - 分块 {i}");
        }

        _logger.LogInformation("模拟传输过程完成：50个成功，5个失败");
    }

    /// <summary>
    /// 演示进度跟踪
    /// Demonstrates progress tracking
    /// </summary>
    private async Task DemonstrateProgressTrackingAsync(int backupLogId)
    {
        _logger.LogInformation("演示进度跟踪");

        var progress = await _transferLogService.GetTransferProgressAsync(backupLogId);
        
        _logger.LogInformation("传输进度报告:");
        _logger.LogInformation("  总分块数: {TotalChunks}", progress.TotalChunks);
        _logger.LogInformation("  已完成: {CompletedChunks}", progress.CompletedChunks);
        _logger.LogInformation("  失败: {FailedChunks}", progress.FailedChunks);
        _logger.LogInformation("  进度百分比: {ProgressPercentage:F1}%", progress.ProgressPercentage);
        _logger.LogInformation("  字节进度: {ByteProgressPercentage:F1}%", progress.ByteProgressPercentage);
        _logger.LogInformation("  是否完成: {IsCompleted}", progress.IsCompleted);
    }

    /// <summary>
    /// 演示失败处理
    /// Demonstrates failure handling
    /// </summary>
    private async Task DemonstrateFailureHandlingAsync(int backupLogId)
    {
        _logger.LogInformation("演示失败处理");

        // 获取失败的传输分块
        var failedChunks = await _transferLogService.GetFailedTransferChunksAsync(backupLogId);
        _logger.LogInformation("发现 {Count} 个失败的传输分块", failedChunks.Count());

        if (failedChunks.Any())
        {
            // 重试失败的分块
            var failedIds = failedChunks.Select(fc => fc.Id).ToList();
            var retryCount = await _transferLogService.RetryFailedTransferChunksAsync(failedIds);
            _logger.LogInformation("已重置 {RetryCount} 个失败的传输分块为待重试状态", retryCount);

            // 模拟重试成功
            foreach (var failedChunk in failedChunks.Take(3)) // 重试前3个
            {
                await _transferLogService.UpdateTransferChunkStatusAsync(failedChunk.Id, "InProgress");
                await Task.Delay(10);
                await _transferLogService.CompleteTransferChunkAsync(failedChunk.Id, success: true);
            }

            _logger.LogInformation("重试了 3 个失败的分块，全部成功");
        }
    }

    /// <summary>
    /// 演示统计报告
    /// Demonstrates statistics reporting
    /// </summary>
    private async Task DemonstrateStatisticsReportingAsync(int backupLogId)
    {
        _logger.LogInformation("演示统计报告");

        var statistics = await _transferLogService.GetTransferStatisticsAsync(backupLogId);
        
        _logger.LogInformation("传输统计报告:");
        _logger.LogInformation("  总传输数: {TotalTransfers}", statistics.TotalTransfers);
        _logger.LogInformation("  成功传输: {SuccessfulTransfers}", statistics.SuccessfulTransfers);
        _logger.LogInformation("  失败传输: {FailedTransfers}", statistics.FailedTransfers);
        _logger.LogInformation("  正在进行: {OngoingTransfers}", statistics.OngoingTransfers);
        _logger.LogInformation("  成功率: {SuccessRate:F1}%", statistics.SuccessRate);
        _logger.LogInformation("  失败率: {FailureRate:F1}%", statistics.FailureRate);
        _logger.LogInformation("  总传输字节: {TotalBytes:N0}", statistics.TotalBytesTransferred);
        _logger.LogInformation("  平均传输速度: {AverageSpeed:F0} 字节/秒", statistics.AverageTransferSpeed);

        // 获取性能指标
        var performanceMetrics = await _transferLogService.GetTransferPerformanceMetricsAsync(backupLogId);
        
        _logger.LogInformation("性能指标:");
        _logger.LogInformation("  总传输时间: {TotalTime:F1} 秒", performanceMetrics.TotalTransferTimeSeconds);
        _logger.LogInformation("  平均分块传输时间: {AverageTime:F2} 秒", performanceMetrics.AverageChunkTransferTimeSeconds);
        _logger.LogInformation("  传输效率: {Efficiency:F1}%", performanceMetrics.TransferEfficiencyPercentage);
        _logger.LogInformation("  重试次数: {RetryCount}", performanceMetrics.RetryCount);
    }

    /// <summary>
    /// 演示日志导出
    /// Demonstrates log export
    /// </summary>
    private async Task DemonstrateLogExportAsync(int backupLogId)
    {
        _logger.LogInformation("演示日志导出");

        // 导出为CSV格式
        var csvData = await _transferLogService.ExportTransferLogsAsync(backupLogId, "CSV");
        var csvFilePath = $"transfer_logs_{backupLogId}.csv";
        await File.WriteAllBytesAsync(csvFilePath, csvData);
        _logger.LogInformation("CSV格式传输日志已导出到: {FilePath}", csvFilePath);

        // 导出为JSON格式
        var jsonData = await _transferLogService.ExportTransferLogsAsync(backupLogId, "JSON");
        var jsonFilePath = $"transfer_logs_{backupLogId}.json";
        await File.WriteAllBytesAsync(jsonFilePath, jsonData);
        _logger.LogInformation("JSON格式传输日志已导出到: {FilePath}", jsonFilePath);
    }

    /// <summary>
    /// 演示日志清理
    /// Demonstrates log cleanup
    /// </summary>
    private async Task DemonstrateLogCleanupAsync()
    {
        _logger.LogInformation("演示日志清理");

        // 清理30天前的日志，但保留失败的日志
        var cleanedCount = await _transferLogService.CleanupOldTransferLogsAsync(30, keepFailedLogs: true);
        _logger.LogInformation("清理了 {CleanedCount} 条旧的传输日志（保留失败日志）", cleanedCount);

        // 获取错误摘要
        var startDate = DateTime.Now.AddDays(-30);
        var endDate = DateTime.Now;
        var errorSummary = await _transferLogService.GetTransferErrorSummaryAsync(startDate, endDate);
        
        _logger.LogInformation("错误摘要（最近30天）:");
        foreach (var error in errorSummary.Take(5)) // 显示前5个最常见的错误
        {
            _logger.LogInformation("  错误: {ErrorMessage}", error.ErrorMessage);
            _logger.LogInformation("    发生次数: {Count}", error.OccurrenceCount);
            _logger.LogInformation("    首次发生: {FirstOccurrence}", error.FirstOccurrence);
            _logger.LogInformation("    最后发生: {LastOccurrence}", error.LastOccurrence);
            _logger.LogInformation("    影响的备份: {AffectedBackups}", string.Join(", ", error.AffectedBackupLogIds));
        }
    }

    /// <summary>
    /// 释放资源
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}

/// <summary>
/// 示例程序入口点
/// Example program entry point
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        var example = new TransferLogManagementExample();
        
        try
        {
            await example.RunCompleteWorkflowAsync();
            Console.WriteLine("传输日志管理示例执行成功！");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"示例执行失败: {ex.Message}");
        }
        finally
        {
            example.Dispose();
        }
    }
}