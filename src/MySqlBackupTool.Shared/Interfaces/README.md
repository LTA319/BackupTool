# Interfaces 文件夹组织结构

Interfaces 文件夹已按功能领域重新组织，以提高代码的可维护性和可读性。所有接口的命名空间保持不变（`MySqlBackupTool.Shared.Interfaces`）。

## 文件夹分类

### 🎯 Core (核心业务)
包含核心备份业务逻辑的接口：
- `IMySQLManager.cs` - MySQL 实例生命周期管理
- `IBackupOrchestrator.cs` - 备份操作协调器
- `IBackupService.cs` - 备份服务核心接口
- `IBackupScheduler.cs` - 备份调度器

### 🔒 Security (安全)
包含身份验证、授权和加密相关的接口：
- `IAuthenticationService.cs` - 身份验证服务
- `IAuthenticationAuditService.cs` - 身份验证审计服务
- `IEncryptionService.cs` - 加密服务
- `ISecureCredentialStorage.cs` - 安全凭据存储

### 🔄 Transfer (传输)
包含文件传输和处理相关的接口：
- `IFileTransferClient.cs` - 文件传输客户端
- `IFileTransferService.cs` - 文件传输服务
- `IFileReceiver.cs` - 文件接收器
- `IChunkManager.cs` - 文件分块管理器
- `IChecksumService.cs` - 校验和服务
- `ICompressionService.cs` - 压缩服务
- `IStorageManager.cs` - 存储管理器

### 💾 Repositories (数据访问)
包含数据访问层的仓储接口：
- `IRepository.cs` - 通用仓储接口
- `IBackupConfigurationRepository.cs` - 备份配置仓储
- `IBackupLogRepository.cs` - 备份日志仓储
- `ITransferLogRepository.cs` - 传输日志仓储
- `IRetentionPolicyRepository.cs` - 保留策略仓储
- `IScheduleConfigurationRepository.cs` - 调度配置仓储
- `IResumeTokenRepository.cs` - 恢复令牌仓储

### 📊 Monitoring (监控)
包含日志、通知、告警和性能监控相关的接口：
- `ILoggingService.cs` - 日志服务
- `INotificationService.cs` - 通知服务
- `IAlertingService.cs` - 告警服务
- `IBackupLogService.cs` - 备份日志服务
- `ITransferLogService.cs` - 传输日志服务
- `IMemoryProfiler.cs` - 内存分析器
- `IBenchmarkRunner.cs` - 性能基准测试运行器

### 🏗️ Infrastructure (基础设施)
包含基础设施和支持服务的接口：
- `IBackgroundTaskManager.cs` - 后台任务管理器
- `IErrorRecoveryManager.cs` - 错误恢复管理器
- `INetworkRetryService.cs` - 网络重试服务
- `IValidationService.cs` - 验证服务
- `IServiceChecker.cs` - 服务检查器
- `IRetentionPolicyService.cs` - 保留策略服务

## 使用说明

所有接口的命名空间保持为 `MySqlBackupTool.Shared.Interfaces`，因此现有代码无需修改 using 语句。

```csharp
// 使用方式保持不变
using MySqlBackupTool.Shared.Interfaces;

public class MyService : IBackupService
{
    private readonly IMySQLManager _mysqlManager;
    private readonly IFileTransferClient _transferClient;
    private readonly IAuthenticationService _authService;
    
    // 实现代码...
}
```

## 接口统计

- **Core**: 4 个接口
- **Security**: 4 个接口
- **Transfer**: 7 个接口
- **Repositories**: 7 个接口
- **Monitoring**: 7 个接口
- **Infrastructure**: 6 个接口

**总计**: 35 个接口

## 设计原则

1. **单一职责** - 每个文件夹专注于特定的功能领域
2. **高内聚** - 相关的接口组织在一起
3. **易于导航** - 清晰的分类便于快速定位所需接口
4. **向后兼容** - 命名空间不变，不影响现有代码
5. **依赖倒置** - 通过接口实现松耦合架构

## 相关文档

- [Models 组织结构](../Models/README.md) - 查看数据模型的分类组织
- [Services 组织结构](../Services/README.md) - 查看服务实现的分类组织
