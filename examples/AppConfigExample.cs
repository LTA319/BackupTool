using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Examples;

/// <summary>
/// 演示如何使用 appsettings.json 配置数据库初始化选项
/// </summary>
public class AppConfigExample
{
    /// <summary>
    /// 示例：从配置文件加载数据库初始化选项
    /// </summary>
    public static void LoadConfigurationExample()
    {
        // 创建配置构建器
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        // 读取数据库初始化配置
        var initOptions = configuration
            .GetSection(DatabaseInitializationOptions.SectionName)
            .Get<DatabaseInitializationOptions>();

        if (initOptions != null)
        {
            Console.WriteLine("=== 数据库初始化配置 ===");
            
            // 显示保留策略配置
            if (initOptions.DefaultRetentionPolicy != null)
            {
                Console.WriteLine($"\n保留策略: {initOptions.DefaultRetentionPolicy.Name}");
                Console.WriteLine($"  最大天数: {initOptions.DefaultRetentionPolicy.MaxAgeDays}");
                Console.WriteLine($"  最大数量: {initOptions.DefaultRetentionPolicy.MaxCount}");
                Console.WriteLine($"  是否启用: {initOptions.DefaultRetentionPolicy.IsEnabled}");
            }

            // 显示备份配置
            if (initOptions.DefaultBackupConfiguration != null)
            {
                Console.WriteLine($"\n备份配置: {initOptions.DefaultBackupConfiguration.Name}");
                Console.WriteLine($"  目标目录: {initOptions.DefaultBackupConfiguration.TargetDirectory}");
                
                if (initOptions.DefaultBackupConfiguration.MySQLConnection != null)
                {
                    Console.WriteLine($"  MySQL主机: {initOptions.DefaultBackupConfiguration.MySQLConnection.Host}");
                    Console.WriteLine($"  MySQL端口: {initOptions.DefaultBackupConfiguration.MySQLConnection.Port}");
                }
            }

            // 显示调度配置
            if (initOptions.DefaultScheduleConfiguration != null)
            {
                Console.WriteLine($"\n调度配置: {initOptions.DefaultScheduleConfiguration.Name}");
                Console.WriteLine($"  调度类型: {initOptions.DefaultScheduleConfiguration.ScheduleType}");
                Console.WriteLine($"  执行时间: {initOptions.DefaultScheduleConfiguration.DailyTime}");
                Console.WriteLine($"  是否启用: {initOptions.DefaultScheduleConfiguration.IsEnabled}");
            }

            // 显示客户端凭据配置
            if (initOptions.DefaultClientCredentials != null)
            {
                Console.WriteLine($"\n客户端凭据: {initOptions.DefaultClientCredentials.ClientName}");
                Console.WriteLine($"  客户端ID: {initOptions.DefaultClientCredentials.ClientId}");
                Console.WriteLine($"  权限数量: {initOptions.DefaultClientCredentials.Permissions.Count}");
            }
        }
    }

    /// <summary>
    /// 示例：在依赖注入中注册配置选项
    /// </summary>
    public static void RegisterConfigurationExample()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                // 添加配置文件
                config.SetBasePath(AppContext.BaseDirectory)
                      .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                // 注册数据库初始化选项
                services.Configure<DatabaseInitializationOptions>(
                    context.Configuration.GetSection(DatabaseInitializationOptions.SectionName));

                // 现在可以在任何服务中注入 IOptions<DatabaseInitializationOptions>
            })
            .Build();

        Console.WriteLine("配置选项已注册到依赖注入容器");
    }

    /// <summary>
    /// 示例：修改配置值
    /// </summary>
    public static void ModifyConfigurationExample()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        var initOptions = configuration
            .GetSection(DatabaseInitializationOptions.SectionName)
            .Get<DatabaseInitializationOptions>();

        if (initOptions?.DefaultRetentionPolicy != null)
        {
            // 修改保留策略
            initOptions.DefaultRetentionPolicy.MaxAgeDays = 60;
            initOptions.DefaultRetentionPolicy.MaxCount = 20;

            Console.WriteLine("保留策略已更新:");
            Console.WriteLine($"  新的最大天数: {initOptions.DefaultRetentionPolicy.MaxAgeDays}");
            Console.WriteLine($"  新的最大数量: {initOptions.DefaultRetentionPolicy.MaxCount}");
        }
    }

    /// <summary>
    /// 示例：验证配置
    /// </summary>
    public static bool ValidateConfigurationExample()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        var initOptions = configuration
            .GetSection(DatabaseInitializationOptions.SectionName)
            .Get<DatabaseInitializationOptions>();

        if (initOptions == null)
        {
            Console.WriteLine("错误: 无法加载数据库初始化配置");
            return false;
        }

        var isValid = true;

        // 验证保留策略
        if (initOptions.DefaultRetentionPolicy != null)
        {
            if (initOptions.DefaultRetentionPolicy.MaxAgeDays <= 0)
            {
                Console.WriteLine("错误: MaxAgeDays 必须大于 0");
                isValid = false;
            }

            if (initOptions.DefaultRetentionPolicy.MaxCount <= 0)
            {
                Console.WriteLine("错误: MaxCount 必须大于 0");
                isValid = false;
            }
        }

        // 验证备份配置
        if (initOptions.DefaultBackupConfiguration != null)
        {
            if (string.IsNullOrWhiteSpace(initOptions.DefaultBackupConfiguration.TargetDirectory))
            {
                Console.WriteLine("错误: TargetDirectory 不能为空");
                isValid = false;
            }

            if (initOptions.DefaultBackupConfiguration.MySQLConnection != null)
            {
                if (initOptions.DefaultBackupConfiguration.MySQLConnection.Port <= 0 || 
                    initOptions.DefaultBackupConfiguration.MySQLConnection.Port > 65535)
                {
                    Console.WriteLine("错误: MySQL 端口必须在 1-65535 之间");
                    isValid = false;
                }
            }
        }

        // 验证客户端凭据
        if (initOptions.DefaultClientCredentials != null)
        {
            if (string.IsNullOrWhiteSpace(initOptions.DefaultClientCredentials.ClientId))
            {
                Console.WriteLine("错误: ClientId 不能为空");
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(initOptions.DefaultClientCredentials.ClientSecret))
            {
                Console.WriteLine("错误: ClientSecret 不能为空");
                isValid = false;
            }
        }

        if (isValid)
        {
            Console.WriteLine("配置验证通过");
        }

        return isValid;
    }

    /// <summary>
    /// 示例：使用不同环境的配置
    /// </summary>
    public static void EnvironmentSpecificConfigurationExample()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .Build();

        Console.WriteLine($"当前环境: {environment}");

        var initOptions = configuration
            .GetSection(DatabaseInitializationOptions.SectionName)
            .Get<DatabaseInitializationOptions>();

        if (initOptions?.DefaultRetentionPolicy != null)
        {
            Console.WriteLine($"保留策略 (来自 {environment} 配置):");
            Console.WriteLine($"  最大天数: {initOptions.DefaultRetentionPolicy.MaxAgeDays}");
            Console.WriteLine($"  最大数量: {initOptions.DefaultRetentionPolicy.MaxCount}");
        }
    }
}
