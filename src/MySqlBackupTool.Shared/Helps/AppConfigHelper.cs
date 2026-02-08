using System.Configuration;
using Microsoft.Extensions.Configuration;
using ConfigManager = System.Configuration.ConfigurationManager;

namespace MySqlBackupTool.Shared.Tools;

/// <summary>
/// App.config配置文件助手类
/// 提供从App.config文件读取配置的功能，支持开发环境配置覆盖
/// </summary>
public static class AppConfigHelper
{
    /// <summary>
    /// 创建基于App.config的配置构建器
    /// </summary>
    /// <returns>配置构建器实例</returns>
    public static IConfigurationBuilder CreateConfigurationBuilder()
    {
        var builder = new ConfigurationBuilder();
        
        // 添加App.config配置源
        builder.Add(new AppConfigConfigurationSource());
        
        return builder;
    }
    
    /// <summary>
    /// 获取配置值，支持开发环境覆盖
    /// </summary>
    /// <param name="key">配置键</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>配置值</returns>
    public static string GetConfigValue(string key, string defaultValue = "")
    {
        // 检查是否为开发环境
        var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" ||
                           Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development";
        
        // 如果是开发环境，先尝试获取开发环境特定的配置
        if (isDevelopment)
        {
            var devKey = $"Development.{key}";
            var devValue = ConfigManager.AppSettings[devKey];
            if (!string.IsNullOrEmpty(devValue))
            {
                return devValue;
            }
        }
        
        // 获取默认配置值
        var value = ConfigManager.AppSettings[key];
        return !string.IsNullOrEmpty(value) ? value : defaultValue;
    }
    
    /// <summary>
    /// 获取布尔类型配置值
    /// </summary>
    /// <param name="key">配置键</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>布尔值</returns>
    public static bool GetBoolValue(string key, bool defaultValue = false)
    {
        var value = GetConfigValue(key);
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }
    
    /// <summary>
    /// 获取整数类型配置值
    /// </summary>
    /// <param name="key">配置键</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>整数值</returns>
    public static int GetIntValue(string key, int defaultValue = 0)
    {
        var value = GetConfigValue(key);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }
    
    /// <summary>
    /// 获取时间间隔类型配置值
    /// </summary>
    /// <param name="key">配置键</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>时间间隔值</returns>
    public static TimeSpan GetTimeSpanValue(string key, TimeSpan defaultValue = default)
    {
        var value = GetConfigValue(key);
        return TimeSpan.TryParse(value, out var result) ? result : defaultValue;
    }
    
    /// <summary>
    /// 获取字符串数组配置值（逗号分隔）
    /// </summary>
    /// <param name="key">配置键</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>字符串数组</returns>
    public static string[] GetStringArrayValue(string key, string[]? defaultValue = null)
    {
        var value = GetConfigValue(key);
        if (string.IsNullOrEmpty(value))
        {
            return defaultValue ?? Array.Empty<string>();
        }
        
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                   .Select(s => s.Trim())
                   .ToArray();
    }
    
    /// <summary>
    /// 获取连接字符串
    /// </summary>
    /// <param name="name">连接字符串名称</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>连接字符串</returns>
    public static string GetConnectionString(string name, string defaultValue = "")
    {
        var connectionString = ConfigManager.ConnectionStrings[name];
        return connectionString?.ConnectionString ?? defaultValue;
    }
}

/// <summary>
/// App.config配置源
/// 将App.config文件作为Microsoft.Extensions.Configuration的配置源
/// </summary>
public class AppConfigConfigurationSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new AppConfigConfigurationProvider();
    }
}

/// <summary>
/// App.config配置提供程序
/// 实现从App.config文件读取配置的逻辑
/// </summary>
public class AppConfigConfigurationProvider : ConfigurationProvider
{
    public override void Load()
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        
        // 读取appSettings节点
        foreach (string key in ConfigManager.AppSettings.AllKeys)
        {
            var value = ConfigManager.AppSettings[key];
            if (!string.IsNullOrEmpty(value))
            {
                // 将App.config的键格式转换为Microsoft.Extensions.Configuration格式
                var configKey = key.Replace('.', ':');
                data[configKey] = value;
            }
        }
        
        // 读取connectionStrings节点
        foreach (ConnectionStringSettings connectionString in ConfigManager.ConnectionStrings)
        {
            if (!string.IsNullOrEmpty(connectionString.Name) && !string.IsNullOrEmpty(connectionString.ConnectionString))
            {
                data[$"ConnectionStrings:{connectionString.Name}"] = connectionString.ConnectionString;
            }
        }
        
        Data = data;
    }
}