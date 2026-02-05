using MySqlBackupTool.Server.Configuration;

namespace MySqlBackupTool.Examples;

/// <summary>
/// App.config配置使用示例
/// 演示如何从App.config文件读取各种类型的配置值
/// </summary>
public class AppConfigExample
{
    /// <summary>
    /// 演示App.config配置读取的示例方法
    /// </summary>
    public static void DemonstrateAppConfigUsage()
    {
        Console.WriteLine("=== App.config配置读取示例 ===");
        Console.WriteLine();

        // 读取字符串配置
        var connectionString = AppConfigHelper.GetConfigValue("MySqlBackupTool.ConnectionString", "默认连接字符串");
        Console.WriteLine($"数据库连接字符串: {connectionString}");

        // 读取布尔配置
        var enableEncryption = AppConfigHelper.GetBoolValue("MySqlBackupTool.EnableEncryption", false);
        Console.WriteLine($"启用加密: {enableEncryption}");

        // 读取整数配置
        var maxConcurrentBackups = AppConfigHelper.GetIntValue("MySqlBackupTool.MaxConcurrentBackups", 1);
        Console.WriteLine($"最大并发备份数: {maxConcurrentBackups}");

        var listenPort = AppConfigHelper.GetIntValue("ServerConfig.ListenPort", 8080);
        Console.WriteLine($"监听端口: {listenPort}");

        // 读取时间间隔配置
        var cleanupInterval = AppConfigHelper.GetTimeSpanValue("StorageConfig.CleanupInterval", TimeSpan.FromHours(24));
        Console.WriteLine($"清理间隔: {cleanupInterval}");

        // 读取字符串数组配置
        var allowedClients = AppConfigHelper.GetStringArrayValue("ServerConfig.AllowedClients");
        Console.WriteLine($"允许的客户端: [{string.Join(", ", allowedClients)}]");

        var emailRecipients = AppConfigHelper.GetStringArrayValue("Alerting.Email.Recipients");
        Console.WriteLine($"邮件接收者: [{string.Join(", ", emailRecipients)}]");

        // 读取连接字符串
        var defaultConnection = AppConfigHelper.GetConnectionString("DefaultConnection", "默认连接");
        Console.WriteLine($"默认连接字符串: {defaultConnection}");

        Console.WriteLine();
        Console.WriteLine("=== 开发环境配置覆盖示例 ===");
        Console.WriteLine("设置环境变量 DOTNET_ENVIRONMENT=Development 来测试开发环境配置");
        Console.WriteLine();

        // 演示开发环境配置覆盖
        var isDevelopment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development";
        if (isDevelopment)
        {
            Console.WriteLine("当前运行在开发环境，以下配置将使用开发环境的值:");
            
            var devEncryption = AppConfigHelper.GetBoolValue("MySqlBackupTool.EnableEncryption", false);
            Console.WriteLine($"开发环境 - 启用加密: {devEncryption}");
            
            var devMaxBackups = AppConfigHelper.GetIntValue("MySqlBackupTool.MaxConcurrentBackups", 1);
            Console.WriteLine($"开发环境 - 最大并发备份数: {devMaxBackups}");
            
            var devStoragePath = AppConfigHelper.GetConfigValue("StorageConfig.PrimaryStoragePath", "默认路径");
            Console.WriteLine($"开发环境 - 存储路径: {devStoragePath}");
        }
        else
        {
            Console.WriteLine("当前运行在生产环境，使用默认配置值");
        }
    }

    /// <summary>
    /// 演示如何在服务中使用App.config配置
    /// </summary>
    public static void DemonstrateServiceConfiguration()
    {
        Console.WriteLine();
        Console.WriteLine("=== 服务配置示例 ===");

        // 服务器配置
        var serverConfig = new
        {
            ListenPort = AppConfigHelper.GetIntValue("ServerConfig.ListenPort", 8080),
            MaxConnections = AppConfigHelper.GetIntValue("ServerConfig.MaxConcurrentConnections", 10),
            EnableSsl = AppConfigHelper.GetBoolValue("ServerConfig.EnableSsl", false),
            AuthRequired = AppConfigHelper.GetBoolValue("ServerConfig.AuthenticationRequired", true),
            AllowedClients = AppConfigHelper.GetStringArrayValue("ServerConfig.AllowedClients")
        };

        Console.WriteLine($"服务器配置:");
        Console.WriteLine($"  监听端口: {serverConfig.ListenPort}");
        Console.WriteLine($"  最大连接数: {serverConfig.MaxConnections}");
        Console.WriteLine($"  启用SSL: {serverConfig.EnableSsl}");
        Console.WriteLine($"  需要认证: {serverConfig.AuthRequired}");
        Console.WriteLine($"  允许的客户端: [{string.Join(", ", serverConfig.AllowedClients)}]");

        // 存储配置
        var storageConfig = new
        {
            PrimaryPath = AppConfigHelper.GetConfigValue("StorageConfig.PrimaryStoragePath", "backups"),
            EnableCompression = AppConfigHelper.GetBoolValue("StorageConfig.EnableCompression", true),
            EnableEncryption = AppConfigHelper.GetBoolValue("StorageConfig.EnableEncryption", false),
            MaxSize = AppConfigHelper.GetConfigValue("StorageConfig.MaxStorageSize", "10GB"),
            CleanupInterval = AppConfigHelper.GetTimeSpanValue("StorageConfig.CleanupInterval", TimeSpan.FromHours(24))
        };

        Console.WriteLine($"存储配置:");
        Console.WriteLine($"  主存储路径: {storageConfig.PrimaryPath}");
        Console.WriteLine($"  启用压缩: {storageConfig.EnableCompression}");
        Console.WriteLine($"  启用加密: {storageConfig.EnableEncryption}");
        Console.WriteLine($"  最大存储大小: {storageConfig.MaxSize}");
        Console.WriteLine($"  清理间隔: {storageConfig.CleanupInterval}");

        // 警报配置
        var alertingConfig = new
        {
            Enabled = AppConfigHelper.GetBoolValue("Alerting.EnableAlerting", true),
            TimeoutSeconds = AppConfigHelper.GetIntValue("Alerting.TimeoutSeconds", 30),
            MaxRetries = AppConfigHelper.GetIntValue("Alerting.MaxRetryAttempts", 3),
            EmailEnabled = AppConfigHelper.GetBoolValue("Alerting.Email.Enabled", false),
            EmailRecipients = AppConfigHelper.GetStringArrayValue("Alerting.Email.Recipients")
        };

        Console.WriteLine($"警报配置:");
        Console.WriteLine($"  启用警报: {alertingConfig.Enabled}");
        Console.WriteLine($"  超时时间: {alertingConfig.TimeoutSeconds}秒");
        Console.WriteLine($"  最大重试次数: {alertingConfig.MaxRetries}");
        Console.WriteLine($"  启用邮件警报: {alertingConfig.EmailEnabled}");
        Console.WriteLine($"  邮件接收者: [{string.Join(", ", alertingConfig.EmailRecipients)}]");
    }
}