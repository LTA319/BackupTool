using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Tools;

/// <summary>
/// 数据库初始化配置加载器
/// 从 App.config 读取配置并构建 DatabaseInitializationOptions 对象
/// </summary>
public static class DatabaseInitializationConfigLoader
{
    private const string Prefix = "DatabaseInitialization";

    /// <summary>
    /// 从 App.config 加载数据库初始化配置
    /// </summary>
    /// <returns>数据库初始化配置选项</returns>
    public static DatabaseInitializationOptions LoadFromAppConfig()
    {
        var options = new DatabaseInitializationOptions
        {
            DefaultRetentionPolicy = LoadRetentionPolicy(),
            DefaultBackupConfiguration = LoadBackupConfiguration(),
            DefaultScheduleConfiguration = LoadScheduleConfiguration(),
            DefaultClientCredentials = LoadClientCredentials()
        };

        return options;
    }

    /// <summary>
    /// 加载默认保留策略配置
    /// </summary>
    private static RetentionPolicy? LoadRetentionPolicy()
    {
        var name = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultRetentionPolicy.Name");
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return new RetentionPolicy
        {
            Name = name,
            Description = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultRetentionPolicy.Description", ""),
            MaxAgeDays = AppConfigHelper.GetIntValue($"{Prefix}.DefaultRetentionPolicy.MaxAgeDays", 30),
            MaxCount = AppConfigHelper.GetIntValue($"{Prefix}.DefaultRetentionPolicy.MaxCount", 10),
            IsEnabled = AppConfigHelper.GetBoolValue($"{Prefix}.DefaultRetentionPolicy.IsEnabled", true),
            CreatedAt = DateTime.Now
        };
    }

    /// <summary>
    /// 加载默认备份配置
    /// </summary>
    private static BackupConfiguration? LoadBackupConfiguration()
    {
        var name = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultBackupConfiguration.Name");
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return new BackupConfiguration
        {
            Name = name,
            DataDirectoryPath = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultBackupConfiguration.DataDirectoryPath", ""),
            ServiceName = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultBackupConfiguration.ServiceName", "MySQL"),
            TargetDirectory = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultBackupConfiguration.TargetDirectory", ""),
            IsActive = AppConfigHelper.GetBoolValue($"{Prefix}.DefaultBackupConfiguration.IsActive", true),
            MySQLConnection = LoadMySQLConnection(),
            TargetServer = LoadTargetServer(),
            NamingStrategy = LoadNamingStrategy(),
            CreatedAt = DateTime.Now
        };
    }

    /// <summary>
    /// 加载 MySQL 连接配置
    /// </summary>
    private static MySQLConnectionInfo LoadMySQLConnection()
    {
        return new MySQLConnectionInfo
        {
            Host = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultBackupConfiguration.MySQLConnection.Host", "localhost"),
            Port = AppConfigHelper.GetIntValue($"{Prefix}.DefaultBackupConfiguration.MySQLConnection.Port", 3306),
            Username = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultBackupConfiguration.MySQLConnection.Username", "root"),
            Password = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultBackupConfiguration.MySQLConnection.Password", ""),
            ServiceName = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultBackupConfiguration.MySQLConnection.ServiceName", "MySQL80"),
            DataDirectoryPath = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultBackupConfiguration.MySQLConnection.DataDirectoryPath", "")
        };
    }

    /// <summary>
    /// 加载目标服务器配置
    /// </summary>
    private static ServerEndpoint LoadTargetServer()
    {
        var clientId = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultClientCredentials.ClientId", "default-client");
        var clientSecret = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultClientCredentials.ClientSecret", "default-secret-2024");
        var clientName = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultClientCredentials.ClientName", "Default Backup Client");

        return new ServerEndpoint
        {
            IPAddress = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultBackupConfiguration.TargetServer.IPAddress", "127.0.0.1"),
            Port = AppConfigHelper.GetIntValue($"{Prefix}.DefaultBackupConfiguration.TargetServer.Port", 8080),
            UseSSL = AppConfigHelper.GetBoolValue($"{Prefix}.DefaultBackupConfiguration.TargetServer.UseSSL", false),
            RequireAuthentication = AppConfigHelper.GetBoolValue($"{Prefix}.DefaultBackupConfiguration.TargetServer.RequireAuthentication", true),
            ClientCredentials = new ClientCredentials
            {
                ClientId = clientId,
                ClientSecret = clientSecret,
                ClientName = clientName
            }
        };
    }

    /// <summary>
    /// 加载文件命名策略配置
    /// </summary>
    private static FileNamingStrategy LoadNamingStrategy()
    {
        return new FileNamingStrategy
        {
            Pattern = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultBackupConfiguration.NamingStrategy.Pattern", "{server}_{database}_{timestamp}.zip"),
            DateFormat = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultBackupConfiguration.NamingStrategy.DateFormat", "yyyyMMdd_HHmmss"),
            IncludeServerName = AppConfigHelper.GetBoolValue($"{Prefix}.DefaultBackupConfiguration.NamingStrategy.IncludeServerName", true),
            IncludeDatabaseName = AppConfigHelper.GetBoolValue($"{Prefix}.DefaultBackupConfiguration.NamingStrategy.IncludeDatabaseName", true)
        };
    }

    /// <summary>
    /// 加载默认调度配置
    /// </summary>
    private static ScheduleConfiguration? LoadScheduleConfiguration()
    {
        var scheduleTypeStr = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultScheduleConfiguration.ScheduleType");
        if (string.IsNullOrEmpty(scheduleTypeStr))
        {
            return null;
        }

        if (!Enum.TryParse<ScheduleType>(scheduleTypeStr, true, out var scheduleType))
        {
            scheduleType = ScheduleType.Daily;
        }

        return new ScheduleConfiguration
        {
            BackupConfigId = 0, // 将在创建时设置
            ScheduleType = scheduleType,
            ScheduleTime = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultScheduleConfiguration.ScheduleTime", "02:00:00"),
            IsEnabled = AppConfigHelper.GetBoolValue($"{Prefix}.DefaultScheduleConfiguration.IsEnabled", false),
            CreatedAt = DateTime.Now
        };
    }

    /// <summary>
    /// 加载默认客户端凭据配置
    /// </summary>
    private static ClientCredentials? LoadClientCredentials()
    {
        var clientId = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultClientCredentials.ClientId");
        if (string.IsNullOrEmpty(clientId))
        {
            return null;
        }

        var permissionsStr = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultClientCredentials.Permissions", "");
        var permissions = string.IsNullOrEmpty(permissionsStr)
            ? new List<string>()
            : permissionsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(p => p.Trim())
                           .ToList();

        return new ClientCredentials
        {
            ClientId = clientId,
            ClientSecret = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultClientCredentials.ClientSecret", ""),
            ClientName = AppConfigHelper.GetConfigValue($"{Prefix}.DefaultClientCredentials.ClientName", "Default Backup Client"),
            Permissions = permissions,
            IsActive = AppConfigHelper.GetBoolValue($"{Prefix}.DefaultClientCredentials.IsActive", true),
            CreatedAt = DateTime.Now
        };
    }
}
