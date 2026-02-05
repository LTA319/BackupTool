# App.config 迁移指南

## 概述

MySqlBackupTool.Server 项目已从使用 JSON 配置文件（appsettings.json）迁移到传统的 .NET App.config 文件。本指南解释了这些更改以及如何使用新的配置系统。

## 迁移摘要

### 更改内容
- **移除**: `appsettings.json` 和 `appsettings.Development.json`
- **添加**: 基于 XML 配置的 `App.config` 文件
- **添加**: `AppConfigHelper` 类，便于配置访问
- **添加**: 支持开发环境配置覆盖

### App.config 的优势
- **熟悉格式**: 传统的 .NET XML 配置格式
- **环境支持**: 内置开发环境覆盖功能
- **类型安全**: 针对不同数据类型的辅助方法
- **兼容性**: 与现有 .NET 配置模式兼容

## 配置结构

### App.config 格式
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <appSettings>
    <!-- 生产环境设置 -->
    <add key="MySqlBackupTool.ConnectionString" value="Data Source=server_backup_tool.db" />
    <add key="ServerConfig.ListenPort" value="8080" />
    
    <!-- 开发环境覆盖 -->
    <add key="Development.MySqlBackupTool.EnableEncryption" value="false" />
    <add key="Development.ServerConfig.EnableSsl" value="false" />
  </appSettings>
  
  <connectionStrings>
    <add name="DefaultConnection" connectionString="Data Source=server_backup_tool.db" />
  </connectionStrings>
</configuration>
```

## 使用配置

### 基本用法
```csharp
using MySqlBackupTool.Server.Configuration;

// 读取字符串值
var connectionString = AppConfigHelper.GetConfigValue("MySqlBackupTool.ConnectionString");

// 读取类型化值
var enableSsl = AppConfigHelper.GetBoolValue("ServerConfig.EnableSsl", false);
var port = AppConfigHelper.GetIntValue("ServerConfig.ListenPort", 8080);
var timeout = AppConfigHelper.GetTimeSpanValue("StorageConfig.CleanupInterval");

// 读取数组（逗号分隔）
var allowedClients = AppConfigHelper.GetStringArrayValue("ServerConfig.AllowedClients");

// 读取连接字符串
var dbConnection = AppConfigHelper.GetConnectionString("DefaultConnection");
```
### 开发环境覆盖

系统自动检测开发环境并应用覆盖配置：

```csharp
// 设置环境变量
Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");

// 如果可用，这将使用 Development.MySqlBackupTool.EnableEncryption
var encryption = AppConfigHelper.GetBoolValue("MySqlBackupTool.EnableEncryption");
```

## 配置映射

### JSON 到 App.config 映射

| JSON 路径 | App.config 键 |
|-----------|----------------|
| `MySqlBackupTool:ConnectionString` | `MySqlBackupTool.ConnectionString` |
| `ServerConfig:ListenPort` | `ServerConfig.ListenPort` |
| `Alerting:Email:Recipients` | `Alerting.Email.Recipients` |

### 数组值
JSON 数组转换为逗号分隔的字符串：
```xml
<!-- JSON: ["127.0.0.1", "192.168.1.0/24"] -->
<add key="ServerConfig.AllowedClients" value="127.0.0.1,192.168.1.0/24" />
```

## 环境配置

### 设置开发环境
```bash
# Windows 命令提示符
set DOTNET_ENVIRONMENT=Development

# Windows PowerShell
$env:DOTNET_ENVIRONMENT="Development"

# Linux/Mac
export DOTNET_ENVIRONMENT=Development
```

### 开发环境覆盖
为任何键添加 `Development.` 前缀以设置开发环境特定值：
```xml
<add key="MySqlBackupTool.EnableEncryption" value="true" />
<add key="Development.MySqlBackupTool.EnableEncryption" value="false" />
```

## 迁移检查清单

- [x] 移除 `appsettings.json` 和 `appsettings.Development.json`
- [x] 创建包含所有配置值的 `App.config`
- [x] 添加 `System.Configuration.ConfigurationManager` NuGet 包
- [x] 创建 `AppConfigHelper` 类
- [x] 更新 `Program.cs` 以使用 App.config
- [x] 测试配置加载
- [x] 验证开发环境覆盖功能

## 故障排除

### 常见问题

1. **找不到配置**: 确保 App.config 在输出目录中
2. **环境覆盖不工作**: 检查 `DOTNET_ENVIRONMENT` 变量
3. **类型转换错误**: 使用适当的辅助方法（GetBoolValue、GetIntValue）
4. **数组解析问题**: 确保逗号分隔格式，无多余空格

### 调试配置
```csharp
// 检查是否在开发环境中运行
var isDev = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development";
Console.WriteLine($"开发模式: {isDev}");

// 测试配置加载
var testValue = AppConfigHelper.GetConfigValue("TestKey", "未找到");
Console.WriteLine($"测试值: {testValue}");
```

## 配置示例

完整的 App.config 示例包含所有必要的配置节：

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <appSettings>
    <!-- MySQL备份工具核心配置 -->
    <add key="MySqlBackupTool.ConnectionString" value="Data Source=server_backup_tool.db" />
    <add key="MySqlBackupTool.EnableEncryption" value="true" />
    <add key="MySqlBackupTool.MaxConcurrentBackups" value="5" />
    
    <!-- 服务器配置 -->
    <add key="ServerConfig.ListenPort" value="8080" />
    <add key="ServerConfig.EnableSsl" value="true" />
    
    <!-- 存储配置 -->
    <add key="StorageConfig.PrimaryStoragePath" value="D:/backups" />
    <add key="StorageConfig.EnableCompression" value="true" />
    
    <!-- 开发环境覆盖 -->
    <add key="Development.MySqlBackupTool.EnableEncryption" value="false" />
    <add key="Development.ServerConfig.EnableSsl" value="false" />
  </appSettings>
</configuration>
```