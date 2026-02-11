# Services 文件夹组织结构

Services 文件夹已按功能领域重新组织，以提高代码的可维护性和可读性。所有服务的命名空间保持不变（`MySqlBackupTool.Shared.Services`）。

## 文件夹分类

### 🎯 Core (核心业务)
包含核心备份业务逻辑的服务实现：
- `MySQLManager.cs` - MySQL 实例生命周期管理
- `TimeoutProtectedMySQLManager.cs` - 带超时保护的 MySQL 管理器
- `BackupOrchestrator.cs` - 备份操作协调器
- `BackupSchedulerService.cs` - 备份调度服务

### 🔒 Security (安全)
包含身份验证、授权和加密相关的服务：
- `AuthenticationService.cs` - 身份验证服务
- `AuthenticationAuditService.cs` - 身份验证审计服务
- `AuthorizationService.cs` - 授权服务
- `EncryptionService.cs` - 加密服务
- `SecureCredentialStorage.cs` - 安全凭据存储
- `ClientCredentialManager.cs` - 客户端凭据管理器
- `TokenManager.cs` - 令牌管理器
- `CertificateManager.cs` - 证书管理器

### 🔄 Transfer (传输)
包含文件传输和处理相关的服务：

**文件传输客户端实现：**
- `FileTransferClient.cs` - 基础文件传输客户端
- `AuthenticatedFileTransferClient.cs` - 带身份验证的传输客户端
- `EnhancedFileTransferClient.cs` - 增强型传输客户端
- `OptimizedFileTransferClient.cs` - 优化的传输客户端
- `SecureFileTransferClient.cs` - 安全传输客户端
- `TimeoutProtectedFileTransferClient.cs` - 带超时保护的传输客户端

**文件接收器：**
- `FileReceiver.cs` - 基础文件接收器
- `SecureFileReceiver.cs` - 安全文件接收器

**传输支持服务：**
- `ChunkManager.cs` - 文件分块管理器
- `ChecksumService.cs` - 校验和服务
- `CompressionService.cs` - 压缩服务
- `TimeoutProtectedCompressionService.cs` - 带超时保护的压缩服务
- `StorageManager.cs` - 存储管理器
- `DirectoryOrganizer.cs` - 目录组织器

### 📊 Monitoring (监控)
包含日志、通知、告警和性能监控相关的服务：
- `LoggingService.cs` - 日志服务
- `NotificationService.cs` - 通知服务
- `AlertingService.cs` - 告警服务
- `BackupLogService.cs` - 备份日志服务
- `TransferLogService.cs` - 传输日志服务
- `BackupReportingService.cs` - 备份报告服务
- `MemoryProfiler.cs` - 内存分析器
- `BenchmarkRunner.cs` - 性能基准测试运行器

### 🏗️ Infrastructure (基础设施)
包含基础设施和支持服务的实现：

**任务管理：**
- `BackgroundTaskManager.cs` - 后台任务管理器

**错误处理与恢复：**
- `ErrorRecoveryManager.cs` - 错误恢复管理器
- `NetworkRetryService.cs` - 网络重试服务

**验证服务：**
- `ValidationService.cs` - 验证服务
- `StartupValidationService.cs` - 启动验证服务
- `DependencyResolutionValidator.cs` - 依赖解析验证器
- `ServiceChecker .cs` - 服务检查器

**保留策略：**
- `RetentionManagementService.cs` - 保留策略管理服务
- `RetentionPolicyBackgroundService.cs` - 保留策略后台服务
- `RetentionPolicyValidator.cs` - 保留策略验证器

**其他：**
- `AutoStartupService.cs` - 自动启动服务

## 使用说明

所有服务类的命名空间保持为 `MySqlBackupTool.Shared.Services`，因此现有代码无需修改 using 语句。

```csharp
// 使用方式保持不变
using MySqlBackupTool.Shared.Services;

public class MyApplication
{
    private readonly MySQLManager _mysqlManager;
    private readonly FileTransferClient _transferClient;
    private readonly AuthenticationService _authService;
    
    // 实现代码...
}
```

## 服务统计

- **Core**: 4 个服务
- **Security**: 8 个服务
- **Transfer**: 14 个服务
- **Monitoring**: 8 个服务
- **Infrastructure**: 11 个服务

**总计**: 45 个服务

## 服务实现模式

### 装饰器模式
多个传输客户端实现展示了装饰器模式：
- `FileTransferClient` - 基础实现
- `AuthenticatedFileTransferClient` - 添加身份验证
- `SecureFileTransferClient` - 添加安全层
- `TimeoutProtectedFileTransferClient` - 添加超时保护
- `OptimizedFileTransferClient` - 添加性能优化
- `EnhancedFileTransferClient` - 综合增强

### 策略模式
不同的压缩和加密服务实现了策略模式，允许运行时选择不同的算法。

### 后台服务模式
多个服务继承自 `BackgroundService`，实现长期运行的后台任务：
- `BackupSchedulerService`
- `RetentionPolicyBackgroundService`

## 设计原则

1. **单一职责** - 每个服务专注于特定的功能
2. **开闭原则** - 通过装饰器和策略模式支持扩展
3. **依赖倒置** - 所有服务都实现对应的接口
4. **高内聚低耦合** - 相关服务组织在一起，通过接口交互
5. **可测试性** - 基于接口的设计便于单元测试和模拟

## 相关文档

- [Interfaces 组织结构](../Interfaces/README.md) - 查看服务接口的分类组织
- [Models 组织结构](../Models/README.md) - 查看数据模型的分类组织
