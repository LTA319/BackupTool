# Models 文件夹组织结构

Models 文件夹已按功能领域重新组织，以提高代码的可维护性和可读性。所有文件的命名空间保持不变（`MySqlBackupTool.Shared.Models`）。

## 文件夹分类

### 📁 Configuration (配置)
包含系统配置相关的模型类：
- `BackupConfiguration.cs` - 备份操作配置
- `ScheduleConfiguration.cs` - 调度配置
- `MySQLConnectionInfo.cs` - MySQL连接信息
- `ServerEndpoint.cs` - 服务器端点配置
- `FileNamingStrategy.cs` - 文件命名策略
- `SslConfiguration.cs` - SSL/TLS配置
- `DatabaseInitializationOptions.cs` - 数据库初始化选项

### 🔒 Security (安全)
包含身份验证、授权和加密相关的模型：
- `AuthenticationModels.cs` - 身份验证模型（凭据、令牌、审计日志等）
- `EncryptionModels.cs` - 加密相关模型

### 🔄 Transfer (传输)
包含文件传输操作相关的模型：
- `TransferModels.cs` - 传输配置、请求、响应、分块策略等
- `ResumeModels.cs` - 传输恢复相关模型

### ⚙️ Operations (操作)
包含备份操作和任务管理相关的模型：
- `BackupOperationModels.cs` - 备份操作模型
- `BackupMetadata.cs` - 备份元数据
- `BackgroundTaskModels.cs` - 后台任务模型
- `ValidationModels.cs` - 验证相关模型
- `ErrorModels.cs` - 错误处理模型

### 📊 Monitoring (监控)
包含日志、通知和报告相关的模型：
- `LoggingModels.cs` - 日志记录模型
- `NotificationModels.cs` - 通知和告警模型
- `ReportingModels.cs` - 报告和分析模型

### 🔍 Diagnostics (诊断)
包含性能分析和系统诊断相关的模型：
- `MemoryProfilingModels.cs` - 内存分析模型
- `BenchmarkModels.cs` - 性能基准测试模型
- `ServiceCheckResultModels.cs` - 服务检查结果模型

## 使用说明

所有模型类的命名空间保持为 `MySqlBackupTool.Shared.Models`，因此现有代码无需修改 using 语句。

```csharp
// 使用方式保持不变
using MySqlBackupTool.Shared.Models;

var config = new BackupConfiguration();
var authRequest = new AuthenticationRequest();
var transferConfig = new TransferConfig();
```

## 设计原则

1. **单一职责** - 每个文件夹专注于特定的功能领域
2. **高内聚** - 相关的模型类组织在一起
3. **易于导航** - 清晰的分类便于快速定位所需模型
4. **向后兼容** - 命名空间不变，不影响现有代码

## 相关文档

- [Interfaces 组织结构](../Interfaces/README.md) - 查看接口的分类组织
