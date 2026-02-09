using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Tools;

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
            .ConfigureAppConfiguration((context, config) =>
            {
                // 清除默认配置源
                config.Sources.Clear();
                
                // 添加App.config配置源
                config.Add(new AppConfigConfigurationSource());
                
                // 添加环境变量支持
                config.AddEnvironmentVariables();
                
                // 添加命令行参数支持
                if (args != null)
                {
                    config.AddCommandLine(args);
                }
            })
            .ConfigureServices((context, services) =>
            {
                // 添加共享服务
                // 从App.config读取数据库连接字符串
                var connectionString = AppConfigHelper.GetConnectionString("DefaultConnection", 
                    ServiceCollectionExtensions.CreateDefaultConnectionString("server_backup_tool.db"));

                services.AddSharedServices(connectionString, context.Configuration);
                
                // 添加服务器特定的服务
                // 从配置读取SSL设置
                var useSecureReceiver = AppConfigHelper.GetBoolValue("ServerConfig.EnableSsl", false);
                var baseStoragePath = AppConfigHelper.GetConfigValue("StorageConfig.PrimaryStoragePath");
                
                services.AddServerServices(baseStoragePath: string.IsNullOrEmpty(baseStoragePath) ? null : baseStoragePath, 
                                         useSecureReceiver: useSecureReceiver);
                
                // 添加保留策略后台服务
                var cleanupInterval = AppConfigHelper.GetTimeSpanValue("StorageConfig.CleanupInterval", TimeSpan.FromHours(24));
                services.AddRetentionPolicyBackgroundService(cleanupInterval);


                //services.AddHostedService<FileReceiverService>();
                // 从配置读取监听端口
                var listenPort = AppConfigHelper.GetIntValue("ServerConfig.ListenPort", 8080);
                
                // 添加文件接收服务作为托管服务，并传递配置的端口
                services.AddHostedService(provider =>
                {
                    var logger = provider.GetRequiredService<ILogger<FileReceiverService>>();
                    var fileReceiver = provider.GetRequiredService<IFileReceiver>();
                    return new FileReceiverService(logger, fileReceiver, listenPort);
                });
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