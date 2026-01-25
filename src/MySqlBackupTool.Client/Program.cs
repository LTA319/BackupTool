using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;

namespace MySqlBackupTool.Client;

/// <summary>
/// MySQL备份工具客户端程序入口类
/// 负责应用程序的初始化、依赖注入配置、服务启动和主窗体运行
/// </summary>
/// <remarks>
/// 该类实现了以下功能：
/// 1. 配置高DPI显示设置
/// 2. 设置依赖注入容器和服务注册
/// 3. 初始化数据库连接
/// 4. 启动后台服务
/// 5. 创建并运行主窗体
/// 6. 处理应用程序启动和关闭时的异常
/// </remarks>
internal static class Program
{
    #region 程序入口点

    /// <summary>
    /// 应用程序的主入口点
    /// 配置并启动MySQL备份工具客户端应用程序
    /// </summary>
    /// <returns>异步任务，表示应用程序的生命周期</returns>
    /// <remarks>
    /// 该方法执行以下步骤：
    /// 1. 初始化应用程序配置（高DPI支持）
    /// 2. 创建并配置主机构建器
    /// 3. 注册所需的服务（共享服务、客户端服务等）
    /// 4. 初始化数据库
    /// 5. 启动后台服务
    /// 6. 创建并运行主窗体
    /// 7. 处理异常和优雅关闭
    /// </remarks>
    [STAThread]
    static async Task Main()
    {
        // 配置应用程序以支持高DPI设置
        // 确保在高分辨率显示器上正确显示界面元素
        ApplicationConfiguration.Initialize();

        // 创建主机构建器用于依赖注入和服务配置
        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // 添加共享服务
                // 创建客户端数据库连接字符串并注册共享服务
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString("client_backup_tool.db");
                services.AddSharedServices(connectionString);
                
                // 添加客户端特定的服务
                // 包括窗体服务、UI相关服务等
                services.AddClientServices();
                
                // 添加备份调度服务（当前已禁用）
                // 可根据需要重新启用备份调度功能
                //services.AddBackupSchedulingServices();
            });

        // 构建主机实例
        var host = hostBuilder.Build();

        try
        {
            #region 服务初始化

            // 初始化数据库
            // 确保数据库架构是最新的，创建必要的表结构
            await host.Services.InitializeDatabaseAsync();

            // 获取日志记录器实例
            var logger = host.Services.GetRequiredService<ILogger<FormMain>>();
            logger.LogInformation("MySQL Backup Tool Client starting...");

            logger.LogInformation("Starting host services...");
            // 启动主机服务（后台服务等）
            // 这将启动所有注册的IHostedService实现
            await host.StartAsync();

            logger.LogInformation("Host services started, creating main form...");

            #endregion

            #region 主窗体运行

            // 运行Windows Forms应用程序
            // 创建主窗体实例并传入服务提供者
            using var mainForm = new FormMain(host.Services);
            logger.LogInformation("Main form created, starting application...");
            
            // 启动消息循环，显示主窗体
            Application.Run(mainForm);

            #endregion
        }
        catch (Exception ex)
        {
            #region 启动异常处理

            // 获取日志记录器（可能为null，如果服务初始化失败）
            var logger = host.Services.GetService<ILogger<FormMain>>();
            logger?.LogCritical(ex, "Fatal error occurred during application startup");
            
            // 向用户显示致命错误信息
            MessageBox.Show($"A fatal error occurred during startup:\n\n{ex.Message}", 
                "MySQL Backup Tool - Fatal Error", 
                MessageBoxButtons.OK, 
                MessageBoxIcon.Error);

            #endregion
        }
        finally
        {
            #region 优雅关闭

            // 优雅地关闭所有服务
            try
            {
                // 停止主机服务，最多等待30秒
                await host.StopAsync(TimeSpan.FromSeconds(30));
            }
            catch (Exception ex)
            {
                // 记录关闭过程中的警告，但不阻止应用程序退出
                var logger = host.Services.GetService<ILogger<FormMain>>();
                logger?.LogWarning(ex, "Error occurred during application shutdown");
            }
            finally
            {
                // 释放主机资源
                host.Dispose();
            }

            #endregion
        }
    }

    #endregion
}