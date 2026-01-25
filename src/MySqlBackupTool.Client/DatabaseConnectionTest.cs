using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Data;
using MySqlBackupTool.Shared.DependencyInjection;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Client;

/// <summary>
/// 数据库连接测试工具类
/// 提供数据库连接测试和问题诊断功能
/// </summary>
public static class DatabaseConnectionTest
{
    /// <summary>
    /// 测试数据库连接并返回诊断信息
    /// 执行全面的数据库连接测试，包括连接性、查询性能等
    /// </summary>
    /// <returns>包含测试结果和诊断信息的DatabaseTestResult对象</returns>
    public static async Task<DatabaseTestResult> TestDatabaseConnectionAsync()
    {
        var result = new DatabaseTestResult();
        var startTime = DateTime.UtcNow;
        
        try
        {
            // 创建用于测试的最小服务提供者
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            
            var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString("client_backup_tool.db");
            services.AddDbContext<BackupDbContext>(options => options.UseSqlite(connectionString));
            
            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            
            result.ConnectionString = connectionString;
            result.DatabasePath = Path.GetFullPath("client_backup_tool.db");
            result.DatabaseExists = File.Exists(result.DatabasePath);
            
            if (result.DatabaseExists)
            {
                var fileInfo = new FileInfo(result.DatabasePath);
                result.DatabaseSize = fileInfo.Length;
                result.LastModified = fileInfo.LastWriteTime;
            }
            
            // 测试数据库上下文创建
            var context = scope.ServiceProvider.GetRequiredService<BackupDbContext>();
            result.ContextCreated = true;
            
            // 测试数据库连接
            var connectionTestStart = DateTime.UtcNow;
            await context.Database.OpenConnectionAsync();
            result.ConnectionTime = DateTime.UtcNow - connectionTestStart;
            result.CanConnect = true;
            
            // 测试基本查询
            var queryTestStart = DateTime.UtcNow;
            var configCount = await context.BackupConfigurations.CountAsync();
            result.QueryTime = DateTime.UtcNow - queryTestStart;
            result.ConfigurationCount = configCount;
            result.CanQuery = true;
            
            // 专门测试正在运行的备份查询
            var runningBackupsTestStart = DateTime.UtcNow;
            var runningBackupsCount = await context.BackupLogs
                .Where(bl => new[] { BackupStatus.Queued, BackupStatus.StoppingMySQL, BackupStatus.Compressing, 
                                   BackupStatus.Transferring, BackupStatus.StartingMySQL, BackupStatus.Verifying }
                           .Contains(bl.Status))
                .CountAsync();
            result.RunningBackupsQueryTime = DateTime.UtcNow - runningBackupsTestStart;
            result.RunningBackupsCount = runningBackupsCount;
            
            await context.Database.CloseConnectionAsync();
            
            result.Success = true;
            result.Message = "数据库连接测试成功完成";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex;
            result.Message = $"数据库连接测试失败: {ex.Message}";
        }
        finally
        {
            result.TotalTime = DateTime.UtcNow - startTime;
        }
        
        return result;
    }
    
    /// <summary>
    /// 尝试修复常见的数据库问题
    /// 执行数据库修复操作，如删除锁文件、重建连接等
    /// </summary>
    /// <returns>包含修复结果和执行操作的DatabaseRepairResult对象</returns>
    public static async Task<DatabaseRepairResult> RepairDatabaseAsync()
    {
        var result = new DatabaseRepairResult();
        var actions = new List<string>();
        
        try
        {
            var databasePath = Path.GetFullPath("client_backup_tool.db");
            
            // 检查锁文件
            var lockFiles = new[] { 
                databasePath + "-wal", 
                databasePath + "-shm",
                databasePath + "-journal"
            };
            
            foreach (var lockFile in lockFiles)
            {
                if (File.Exists(lockFile))
                {
                    try
                    {
                        File.Delete(lockFile);
                        actions.Add($"已删除锁文件: {Path.GetFileName(lockFile)}");
                    }
                    catch (Exception ex)
                    {
                        actions.Add($"删除锁文件 {Path.GetFileName(lockFile)} 失败: {ex.Message}");
                    }
                }
            }
            
            // 清理后测试连接
            var testResult = await TestDatabaseConnectionAsync();
            result.TestResult = testResult;
            result.Success = testResult.Success;
            result.ActionsPerformed = actions;
            
            if (result.Success)
            {
                result.Message = "数据库修复成功完成";
            }
            else
            {
                result.Message = $"数据库修复失败: {testResult.Message}";
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex;
            result.Message = $"数据库修复失败: {ex.Message}";
            result.ActionsPerformed = actions;
        }
        
        return result;
    }
}

/// <summary>
/// 数据库连接测试结果
/// 包含数据库连接测试的详细结果和性能指标
/// </summary>
public class DatabaseTestResult
{
    /// <summary>
    /// 测试是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 测试结果消息
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// 测试过程中发生的错误（如果有）
    /// </summary>
    public Exception? Error { get; set; }

    /// <summary>
    /// 测试总耗时
    /// </summary>
    public TimeSpan TotalTime { get; set; }

    /// <summary>
    /// 数据库连接字符串
    /// </summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>
    /// 数据库文件路径
    /// </summary>
    public string DatabasePath { get; set; } = "";

    /// <summary>
    /// 数据库文件是否存在
    /// </summary>
    public bool DatabaseExists { get; set; }

    /// <summary>
    /// 数据库文件大小（字节）
    /// </summary>
    public long DatabaseSize { get; set; }

    /// <summary>
    /// 数据库文件最后修改时间
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// 数据库上下文是否成功创建
    /// </summary>
    public bool ContextCreated { get; set; }

    /// <summary>
    /// 是否能够连接到数据库
    /// </summary>
    public bool CanConnect { get; set; }

    /// <summary>
    /// 数据库连接耗时
    /// </summary>
    public TimeSpan ConnectionTime { get; set; }

    /// <summary>
    /// 是否能够执行查询
    /// </summary>
    public bool CanQuery { get; set; }

    /// <summary>
    /// 查询执行耗时
    /// </summary>
    public TimeSpan QueryTime { get; set; }

    /// <summary>
    /// 配置数量
    /// </summary>
    public int ConfigurationCount { get; set; }

    /// <summary>
    /// 正在运行的备份查询耗时
    /// </summary>
    public TimeSpan RunningBackupsQueryTime { get; set; }

    /// <summary>
    /// 正在运行的备份数量
    /// </summary>
    public int RunningBackupsCount { get; set; }

    /// <summary>
    /// 返回格式化的测试结果字符串
    /// </summary>
    /// <returns>包含所有测试结果的详细字符串</returns>
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"数据库测试结果: {(Success ? "成功" : "失败")}");
        sb.AppendLine($"消息: {Message}");
        sb.AppendLine($"总耗时: {TotalTime.TotalMilliseconds:F0}毫秒");
        sb.AppendLine();
        sb.AppendLine($"数据库路径: {DatabasePath}");
        sb.AppendLine($"数据库存在: {DatabaseExists}");
        if (DatabaseExists)
        {
            sb.AppendLine($"数据库大小: {DatabaseSize:N0} 字节");
            sb.AppendLine($"最后修改: {LastModified:yyyy-MM-dd HH:mm:ss}");
        }
        sb.AppendLine();
        sb.AppendLine($"上下文已创建: {ContextCreated}");
        sb.AppendLine($"可以连接: {CanConnect}");
        if (CanConnect)
        {
            sb.AppendLine($"连接耗时: {ConnectionTime.TotalMilliseconds:F0}毫秒");
        }
        sb.AppendLine($"可以查询: {CanQuery}");
        if (CanQuery)
        {
            sb.AppendLine($"查询耗时: {QueryTime.TotalMilliseconds:F0}毫秒");
            sb.AppendLine($"配置数量: {ConfigurationCount}");
            sb.AppendLine($"正在运行的备份查询耗时: {RunningBackupsQueryTime.TotalMilliseconds:F0}毫秒");
            sb.AppendLine($"正在运行的备份数量: {RunningBackupsCount}");
        }
        
        if (Error != null)
        {
            sb.AppendLine();
            sb.AppendLine($"错误: {Error.Message}");
            if (Error.InnerException != null)
            {
                sb.AppendLine($"内部错误: {Error.InnerException.Message}");
            }
        }
        
        return sb.ToString();
    }
}

/// <summary>
/// 数据库修复操作结果
/// 包含数据库修复操作的结果和执行的操作列表
/// </summary>
public class DatabaseRepairResult
{
    /// <summary>
    /// 修复是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 修复结果消息
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// 修复过程中发生的错误（如果有）
    /// </summary>
    public Exception? Error { get; set; }

    /// <summary>
    /// 执行的修复操作列表
    /// </summary>
    public List<string> ActionsPerformed { get; set; } = new();

    /// <summary>
    /// 修复后的数据库测试结果
    /// </summary>
    public DatabaseTestResult? TestResult { get; set; }

    /// <summary>
    /// 返回格式化的修复结果字符串
    /// </summary>
    /// <returns>包含所有修复结果的详细字符串</returns>
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"数据库修复结果: {(Success ? "成功" : "失败")}");
        sb.AppendLine($"消息: {Message}");
        
        if (ActionsPerformed.Any())
        {
            sb.AppendLine();
            sb.AppendLine("执行的操作:");
            foreach (var action in ActionsPerformed)
            {
                sb.AppendLine($"  • {action}");
            }
        }
        
        if (TestResult != null)
        {
            sb.AppendLine();
            sb.AppendLine("修复后测试结果:");
            sb.AppendLine(TestResult.ToString());
        }
        
        if (Error != null)
        {
            sb.AppendLine();
            sb.AppendLine($"错误: {Error.Message}");
        }
        
        return sb.ToString();
    }
}