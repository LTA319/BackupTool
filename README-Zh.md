# MySQL 全文件备份工具

基于 .NET 8 构建的分布式企业级 MySQL 数据库备份解决方案。该系统提供客户端-服务器架构，用于管理大规模数据库备份，具有文件分块、断点续传和全面监控等高级功能。

## 主要功能

- **分布式架构**: 独立的客户端和服务器组件，支持可扩展的备份操作
- **大文件支持**: 通过智能文件分块和重组处理 100GB+ 数据库
- **断点续传**: 中断传输恢复，支持校验和验证和基于令牌的恢复
- **企业级安全**: 身份验证、授权、SSL/TLS 加密和安全凭据存储
- **全面监控**: 详细日志记录、进度报告、警报和内存分析
- **灵活配置**: 基于 SQLite 的配置管理，支持保留策略和调度
- **多线程**: 后台操作，支持实时进度跟踪和超时保护
- **文件完整性**: 所有传输的校验和验证、压缩和加密
- **自动化管理**: 保留策略、备份调度和启动验证

## 组件

### MySqlBackupTool.Client
Windows Forms 客户端应用程序功能：
- **GUI 管理**: 完整的备份配置和监控界面
- **MySQL 控制**: 启动/停止 MySQL 服务和实例管理
- **系统集成**: 系统托盘功能和后台操作
- **配置表单**: 备份设置、调度和保留策略管理
- **实时监控**: 进度跟踪、日志查看和传输状态
- **报告生成**: 备份报告和操作分析

### MySqlBackupTool.Server
带托管服务的控制台服务器应用程序：
- **文件接收**: 支持分块的多线程文件接收
- **存储管理**: 有组织的文件存储和目录结构
- **后台处理**: 用于持续操作的托管服务
- **传输日志**: 详细的传输进度和状态跟踪
- **安全层**: 客户端连接的身份验证和授权

### MySqlBackupTool.Shared
全面的共享库包含：
- **35+ 接口**: 所有组件的完整服务抽象
- **20+ 模型**: 配置、日志记录和操作的数据模型
- **40+ 服务**: 完整的业务逻辑实现
- **仓储模式**: 基于 Entity Framework 的数据访问层
- **高级功能**: 身份验证、加密、压缩和网络

## 技术栈

### 框架和运行时
- **.NET 8.0**: 所有组件的主要框架
- **C# 12**: 启用可空引用类型的编程语言
- **Windows Forms**: 客户端 GUI 框架 (net8.0-windows 目标)

### 数据库和 ORM
- **Entity Framework Core 8.0**: 数据访问的 ORM
- **SQLite**: 配置和日志数据库
- **MySQL 连接器**: MySql.Data 8.2.0 用于数据库连接

### 关键库
- **Microsoft.Extensions.Hosting 8.0.0**: 后台服务和依赖注入
- **Microsoft.Extensions.Logging 8.0.0**: 结构化日志框架
- **Microsoft.Extensions.Http.Polly 8.0.0**: HTTP 弹性和重试策略
- **Polly 8.2.0**: 故障处理的弹性模式
- **MailKit 4.3.0**: 电子邮件通知和警报
- **System.ServiceProcess.ServiceController 8.0.0**: Windows 服务管理

### 测试框架
- **xUnit 2.9.3**: 主要测试框架
- **FsCheck 2.16.6**: 基于属性的测试库
- **Moq 4.20.70**: 单元测试的模拟框架
- **Microsoft.EntityFrameworkCore.InMemory 8.0.0**: 测试用内存数据库

## 架构概览

```
┌─────────────────────┐    网络传输     ┌─────────────────────┐
│   备份客户端         │◄──────────────►│   文件接收服务器      │
│  (Windows Forms)    │                │                     │
├─────────────────────┤                ├─────────────────────┤
│ • MySQL 管理器      │                │ • 分块管理器         │
│ • 压缩服务          │                │ • 存储管理器         │
│ • 传输客户端        │                │ • 文件接收器         │
│ • 身份验证          │                │ • 后台任务          │
│ • 系统托盘          │                │ • 传输日志          │
└─────────────────────┘                └─────────────────────┘
        │                                       │
        ▼                                       ▼
┌─────────────────────┐                ┌─────────────────────┐
│ 客户端 SQLite 数据库 │                │ 服务器 SQLite 数据库 │
│ • 配置信息          │                │ • 传输日志          │
│ • 备份日志          │                │ • 恢复令牌          │
│ • 调度计划          │                │ • 文件元数据        │
│ • 保留策略          │                │ • 身份验证          │
└─────────────────────┘                └─────────────────────┘
```

## 快速开始

### 先决条件
- .NET 8.0 SDK
- Windows 操作系统（客户端应用程序）
- MySQL 服务器（备份目标）

### 快速启动命令

**构建整个解决方案：**
```bash
dotnet build MySqlBackupTool.sln
```

**发布版本构建：**
```bash
dotnet build MySqlBackupTool.sln --configuration Release
```

**运行服务器：**
```bash
dotnet run --project src/MySqlBackupTool.Server/MySqlBackupTool.Server.csproj
```

**运行客户端：**
```bash
dotnet run --project src/MySqlBackupTool.Client/MySqlBackupTool.Client.csproj
```

**运行测试：**
```bash
dotnet test tests/MySqlBackupTool.Tests/MySqlBackupTool.Tests.csproj
```

**运行测试并生成覆盖率报告：**
```bash
dotnet test tests/MySqlBackupTool.Tests/MySqlBackupTool.Tests.csproj --collect:"XPlat Code Coverage"
```

### 项目结构

```
MySqlBackupTool/
├── src/                           # 源代码
│   ├── MySqlBackupTool.Client/    # Windows Forms 客户端应用程序
│   │   ├── Forms/                 # UI 表单（配置、监控、日志等）
│   │   ├── Tools/                 # 实用工具类和系统集成
│   │   └── Properties/            # 应用程序资源
│   ├── MySqlBackupTool.Server/    # 控制台服务器应用程序
│   │   └── FileReceiverService.cs # 主要文件接收服务
│   └── MySqlBackupTool.Shared/    # 共享库组件
│       ├── Data/                  # Entity Framework 和仓储
│       ├── Interfaces/            # 服务契约（35+ 接口）
│       ├── Models/                # 数据模型和 DTO（20+ 模型）
│       ├── Services/              # 业务逻辑（40+ 服务）
│       └── DependencyInjection/   # 服务注册
├── tests/                         # 测试项目
│   └── MySqlBackupTool.Tests/     # 综合测试套件
│       ├── Services/              # 服务层单元测试
│       ├── Integration/           # 端到端集成测试
│       ├── Properties/            # 基于属性的测试（FsCheck）
│       └── Benchmarks/            # 性能基准测试
├── docs/                          # 文档（中英文）
├── examples/                      # 代码示例和样本
└── mardown/                       # 实现总结和指南
```

## 高级功能

### 身份验证和安全
- **多层安全**: 身份验证、授权和审计跟踪
- **安全凭据存储**: 加密凭据管理
- **SSL/TLS 支持**: 安全网络通信
- **证书管理**: 客户端证书验证

### 网络弹性
- **重试策略**: 可配置的重试策略，支持指数退避
- **超时保护**: 所有操作的全面超时处理
- **错误恢复**: 网络中断的自动恢复
- **连接管理**: 优化的连接池和生命周期管理

### 性能优化
- **内存分析**: 内置内存使用监控和优化
- **基准测试套件**: 关键操作的性能测试
- **优化传输**: 针对不同场景的多种文件传输实现
- **压缩流**: 带超时保护的高效压缩

### 运营卓越
- **启动验证**: 启动时的全面系统验证
- **依赖解析**: 自动依赖检查和验证
- **后台服务**: 强大的托管服务实现
- **全面错误处理**: 详细的错误报告和恢复

## 核心接口和服务

### 核心服务
- `IMySQLManager` - MySQL 实例生命周期管理
- `IFileTransferClient` - 文件传输操作（多种实现）
- `IFileReceiver` - 服务器端文件接收和处理
- `ICompressionService` - 带超时保护的文件压缩/解压缩
- `IEncryptionService` - 数据加密/解密

### 数据访问层
- `IRepository<T>` - 通用仓储模式
- `IBackupConfigurationRepository` - 备份配置管理
- `IBackupLogRepository` - 操作日志和历史
- `ITransferLogRepository` - 详细传输进度跟踪
- `IRetentionPolicyRepository` - 备份保留管理

### 基础设施服务
- `ILoggingService` - 增强的日志功能
- `INotificationService` - 警报和通知
- `IMemoryProfiler` - 性能监控和分析
- `IAuthenticationService` - 用户身份验证和安全
- `IBackgroundTaskManager` - 后台服务管理

### 高级功能
- `IBackupOrchestrator` - 协调备份操作
- `IChunkManager` - 文件分块和重组
- `IChecksumService` - 文件完整性验证
- `IErrorRecoveryManager` - 错误处理和恢复
- `INetworkRetryService` - 网络弹性和重试逻辑

## 测试策略

项目包含多种方法的全面测试：

### 测试类别
- **单元测试**: 使用模拟的服务层测试
- **集成测试**: 端到端工作流测试
- **基于属性的测试**: 使用 FsCheck 测试不变量和边界情况
- **基准测试**: 关键操作的性能测试
- **依赖注入测试**: 服务注册验证

### 测试覆盖率
- **服务**: 40+ 服务实现的全面单元测试
- **模型**: 数据模型验证和序列化测试
- **仓储**: 使用内存数据库的数据访问层测试
- **身份验证**: 安全流程和错误场景测试
- **网络操作**: 文件传输和重试策略测试

### 基于属性的测试
使用 FsCheck 测试关键算法：
- 压缩往返属性
- 文件分块和重组不变量
- 身份验证令牌格式验证
- 配置持久化属性
- 网络重试行为验证

## 文档

提供中英文全面文档：

### 中文文档
- **[API 参考](docs/API-Reference-Zh.md)** - 完整的 API 文档，涵盖所有接口和服务
- **[用户指南](docs/User-Guide-Zh.md)** - 全面的用户指南，包含设置说明和最佳实践
- **[配置示例](docs/Configuration-Examples-Zh.md)** - 各种部署场景的实用配置示例
- **[性能基准测试](docs/PerformanceBenchmarking-Zh.md)** - 性能测试和优化指南

### 英文文档 (English Documentation)
- **[API Reference](docs/API-Reference.md)** - Complete API documentation for all interfaces and services
- **[User Guide](docs/User-Guide.md)** - Comprehensive user guide with setup instructions and best practices
- **[Configuration Examples](docs/Configuration-Examples.md)** - Practical configuration examples for various deployment scenarios
- **[Performance Benchmarking](docs/PerformanceBenchmarking.md)** - Performance testing and optimization guide

### 快速链接
- [快速开始](docs/User-Guide-Zh.md#快速开始) - 初始设置和配置
- [基本操作](docs/User-Guide-Zh.md#基本操作) - 运行备份和管理调度
- [API 参考](docs/API-Reference-Zh.md) - 完整接口文档
- [配置示例](docs/Configuration-Examples-Zh.md) - 即用配置模板

## 示例和样本

`examples/` 目录包含实用代码示例：
- **MemoryProfilingExample.cs** - 内存使用监控实现
- **StartupValidationExample.cs** - 应用程序启动时的系统验证
- **TransferLogManagementExample.cs** - 传输日志记录和管理
- **TransferLogViewerExample.cs** - 日志查看和分析

## 贡献

本项目遵循企业开发实践：

### 开发标准
- **清洁架构**: 基于接口设计的清晰关注点分离
- **依赖注入**: 全程使用 Microsoft.Extensions.DependencyInjection
- **异步/等待**: I/O 操作的一致异步模式
- **结构化日志**: 带上下文信息的 Microsoft.Extensions.Logging
- **全面测试**: 单元、集成和基于属性的测试
- **文档**: 所有公共 API 的 XML 注释

### 代码质量
- **基于接口的设计**: 所有服务实现接口以提高可测试性
- **仓储模式**: 带 Entity Framework 实现的通用仓储接口
- **后台服务**: 长时间运行操作的托管服务
- **弹性模式**: 使用 Polly 进行重试策略、断路器和超时处理
- **配置管理**: 带强类型配置类的选项模式

## 许可证

[许可证信息待添加]