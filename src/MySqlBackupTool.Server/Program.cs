using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;

namespace MySqlBackupTool.Server;

/// <summary>
/// MySQL备份工具服务器端程序入口类
/// 负责启动和管理文件接收服务器，处理来自客户端的备份文件传输请求
/// </summary>
internal class Program
{
    /// <summary>
    /// 程序主入口点
    /// 初始化并启动MySQL备份工具服务器，配置依赖注入容器和后台服务
    /// </summary>
    /// <param name="args">命令行参数</param>
    /// <returns>异步任务</returns>
    static async Task Main(string[] args)
    {
        // 显示程序标题和分隔线
        Console.WriteLine("MySQL Backup Tool - File Receiver Server");
        Console.WriteLine("========================================");

        // 创建主机构建器用于依赖注入和服务配置
        var hostBuilder = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // 添加共享服务
                // 创建默认数据库连接字符串，使用服务器端数据库文件
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString("server_backup_tool.db");
                services.AddSharedServices(connectionString);
                
                // 添加服务器特定的服务
                // 设置备份文件存储路径到系统公共应用程序数据目录
                var storagePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MySqlBackupTool", "Backups");
                
                // 注册服务器服务，第二个参数false表示不启用SSL（可根据需要调整）
                services.AddServerServices(storagePath, false);
                
                // 添加保留策略后台服务，每24小时运行一次清理过期备份
                services.AddRetentionPolicyBackgroundService(TimeSpan.FromHours(24));
                
                // 添加文件接收服务作为托管服务
                services.AddHostedService<FileReceiverService>();
            });

        // 构建主机实例
        var host = hostBuilder.Build();

        try
        {
            // 初始化数据库，确保数据库架构是最新的
            await host.Services.InitializeDatabaseAsync();

            // 获取日志记录器实例
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("MySQL Backup Tool Server starting...");

            // 显示启动信息
            Console.WriteLine("Server is starting...");
            Console.WriteLine("Press Ctrl+C to stop the server");

            // 设置优雅关闭处理程序
            // 当用户按下Ctrl+C时，优雅地停止服务器
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; // 取消默认的进程终止行为
                logger.LogInformation("Shutdown requested by user");
                // 等待最多30秒来完成正在进行的操作
                host.StopAsync().Wait(TimeSpan.FromSeconds(30));
            };

            // 运行主机，这将启动所有注册的后台服务
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            // 处理启动过程中的致命错误
            try
            {
                var logger = host.Services.GetService<ILogger<Program>>();
                logger?.LogCritical(ex, "Fatal error occurred during server startup");
            }
            catch
            {
                // 如果服务已被释放，忽略日志记录错误
            }
            
            // 向控制台输出错误信息
            Console.WriteLine($"Fatal error: {ex.Message}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
        finally
        {
            // 显示关闭信息
            Console.WriteLine("Server is shutting down...");
        }
    }
}