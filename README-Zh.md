# MySQL 全文件备份工具

基于 .NET 8 构建的分布式 MySQL 数据库备份解决方案，提供企业级备份功能和客户端-服务器架构。

## 项目结构

该解决方案分为三个主要项目：

### MySqlBackupTool.Shared
包含客户端和服务器共享的组件：
- **Interfaces**: 核心服务接口 (IMySQLManager, ICompressionService, IFileTransferClient, IFileReceiver)
- **Models**: 配置、传输协议、日志记录和元数据的数据模型
- **Data**: Entity Framework DbContext 和数据库配置
- **Logging**: 带文件输出的自定义日志框架
- **DependencyInjection**: 服务注册和配置

### MySqlBackupTool.Client
用于管理备份操作的 Windows Forms 应用程序：
- **Program.cs**: 带依赖注入设置的应用程序入口点
- **FormMain**: 主用户界面（将在后续任务中实现）
- 处理 MySQL 实例管理、文件压缩和传输启动

### MySqlBackupTool.Server
用于接收和存储备份文件的控制台应用程序：
- **Program.cs**: 带托管服务配置的服务器入口点
- **FileReceiverService**: 处理文件接收的后台服务
- 管理文件分块、重组和存储组织

## 主要功能（计划中）

- **分布式架构**: 独立的客户端和服务器组件
- **大文件支持**: 支持 100GB+ 数据库的文件分块
- **断点续传**: 中断传输恢复功能
- **多线程**: 带进度报告的后台操作
- **全面日志记录**: 详细的操作跟踪和监控
- **灵活配置**: 基于 SQLite 的配置管理
- **文件完整性**: 所有传输的校验和验证
- **保留策略**: 自动备份清理

## 数据库架构

应用程序使用 SQLite 进行配置和日志存储：
- **BackupConfigurations**: 备份作业定义
- **BackupLogs**: 操作历史和状态
- **TransferLogs**: 详细传输进度
- **RetentionPolicies**: 文件清理规则

## 快速开始

### 先决条件
- .NET 8.0 SDK
- Windows 操作系统（客户端应用程序）
- MySQL 服务器（备份目标）

### 构建解决方案
```bash
dotnet build MySqlBackupTool.sln
```

### 运行服务器
```bash
dotnet run --project src/MySqlBackupTool.Server/MySqlBackupTool.Server.csproj
```

### 运行客户端
```bash
dotnet run --project src/MySqlBackupTool.Client/MySqlBackupTool.Client.csproj
```

## 开发状态

✅ **任务 1 已完成**: 项目结构和核心接口
- 包含客户端、服务器和共享项目的解决方案结构
- 已定义核心接口 (IMySQLManager, ICompressionService, IFileTransferClient, IFileReceiver)
- SQLite 数据库架构和 Entity Framework 配置
- 日志框架和依赖注入设置

🔄 **下一步任务**: 配置管理系统实现

## 架构概览

```
┌─────────────────┐    网络传输     ┌─────────────────┐
│    备份客户端    │◄──────────────►│   文件接收服务器  │
│                 │                │                 │
├─────────────────┤                ├─────────────────┤
│   MySQL 管理器  │                │   分块管理器     │
│     压缩服务    │                │   存储管理器     │
│   传输客户端    │                │   文件接收器     │
└─────────────────┘                └─────────────────┘
        │                                   │
        ▼                                   ▼
┌─────────────────┐                ┌─────────────────┐
│   客户端配置     │                │   服务器配置     │
│     数据库      │                │     数据库      │
│   (SQLite)     │                │   (SQLite)     │
└─────────────────┘                └─────────────────┘
```

## 贡献

本项目采用基于任务的开发方法。每个功能都作为独立任务实现，包含全面的测试，包括单元测试和基于属性的测试。

## 许可证

[许可证信息待添加]