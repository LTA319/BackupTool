# App.config Migration Guide

## Overview

The MySqlBackupTool.Server project has been migrated from using JSON configuration files (appsettings.json) to traditional .NET App.config files. This guide explains the changes and how to use the new configuration system.

## Migration Summary

### What Changed
- **Removed**: `appsettings.json` and `appsettings.Development.json`
- **Added**: `App.config` file with XML-based configuration
- **Added**: `AppConfigHelper` class for easy configuration access
- **Added**: Support for development environment configuration overrides

### Benefits of App.config
- **Familiar Format**: Traditional .NET XML configuration format
- **Environment Support**: Built-in development environment overrides
- **Type Safety**: Helper methods for different data types
- **Compatibility**: Works with existing .NET configuration patterns

## Configuration Structure

### App.config Format
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <appSettings>
    <!-- Production settings -->
    <add key="MySqlBackupTool.ConnectionString" value="Data Source=server_backup_tool.db" />
    <add key="ServerConfig.ListenPort" value="8080" />
    
    <!-- Development overrides -->
    <add key="Development.MySqlBackupTool.EnableEncryption" value="false" />
    <add key="Development.ServerConfig.EnableSsl" value="false" />
  </appSettings>
  
  <connectionStrings>
    <add name="DefaultConnection" connectionString="Data Source=server_backup_tool.db" />
  </connectionStrings>
</configuration>
```

## Using the Configuration

### Basic Usage
```csharp
using MySqlBackupTool.Server.Configuration;

// Read string values
var connectionString = AppConfigHelper.GetConfigValue("MySqlBackupTool.ConnectionString");

// Read typed values
var enableSsl = AppConfigHelper.GetBoolValue("ServerConfig.EnableSsl", false);
var port = AppConfigHelper.GetIntValue("ServerConfig.ListenPort", 8080);
var timeout = AppConfigHelper.GetTimeSpanValue("StorageConfig.CleanupInterval");

// Read arrays (comma-separated)
var allowedClients = AppConfigHelper.GetStringArrayValue("ServerConfig.AllowedClients");

// Read connection strings
var dbConnection = AppConfigHelper.GetConnectionString("DefaultConnection");
```
### Development Environment Overrides

The system automatically detects development environment and applies overrides:

```csharp
// Set environment variable
Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");

// This will use Development.MySqlBackupTool.EnableEncryption if available
var encryption = AppConfigHelper.GetBoolValue("MySqlBackupTool.EnableEncryption");
```

## Configuration Mapping

### JSON to App.config Mapping

| JSON Path | App.config Key |
|-----------|----------------|
| `MySqlBackupTool:ConnectionString` | `MySqlBackupTool.ConnectionString` |
| `ServerConfig:ListenPort` | `ServerConfig.ListenPort` |
| `Alerting:Email:Recipients` | `Alerting.Email.Recipients` |

### Array Values
JSON arrays are converted to comma-separated strings:
```xml
<!-- JSON: ["127.0.0.1", "192.168.1.0/24"] -->
<add key="ServerConfig.AllowedClients" value="127.0.0.1,192.168.1.0/24" />
```

## Environment Configuration

### Setting Development Environment
```bash
# Windows Command Prompt
set DOTNET_ENVIRONMENT=Development

# Windows PowerShell
$env:DOTNET_ENVIRONMENT="Development"

# Linux/Mac
export DOTNET_ENVIRONMENT=Development
```

### Development Overrides
Add `Development.` prefix to any key for development-specific values:
```xml
<add key="MySqlBackupTool.EnableEncryption" value="true" />
<add key="Development.MySqlBackupTool.EnableEncryption" value="false" />
```

## Migration Checklist

- [x] Remove `appsettings.json` and `appsettings.Development.json`
- [x] Create `App.config` with all configuration values
- [x] Add `System.Configuration.ConfigurationManager` NuGet package
- [x] Create `AppConfigHelper` class
- [x] Update `Program.cs` to use App.config
- [x] Test configuration loading
- [x] Verify development environment overrides work

## Troubleshooting

### Common Issues

1. **Configuration not found**: Ensure App.config is in the output directory
2. **Environment overrides not working**: Check `DOTNET_ENVIRONMENT` variable
3. **Type conversion errors**: Use appropriate helper methods (GetBoolValue, GetIntValue)
4. **Array parsing issues**: Ensure comma-separated format without extra spaces

### Debugging Configuration
```csharp
// Check if running in development
var isDev = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development";
Console.WriteLine($"Development mode: {isDev}");

// Test configuration loading
var testValue = AppConfigHelper.GetConfigValue("TestKey", "NotFound");
Console.WriteLine($"Test value: {testValue}");
```