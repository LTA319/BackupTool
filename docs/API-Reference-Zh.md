# MySQL 备份工具 - API 参考

## 概述

MySQL 备份工具通过分布式客户端-服务器架构提供了一套全面的 API 来管理 MySQL 数据库备份。本文档涵盖了系统中所有可用的公共接口、模型和服务。

## 目录

1. [核心服务](#核心服务)
2. [备份管理](#备份管理)
3. [文件操作](#文件操作)
4. [通知系统](#通知系统)
5. [调度](#调度)
6. [安全](#安全)
7. [数据模型](#数据模型)
8. [错误处理](#错误处理)

---

## 核心服务

### IMySQLManager

管理 MySQL 实例生命周期操作，包括启动、停止和验证数据库连接。

#### 方法

##### `StopInstanceAsync(string serviceName)`
停止 MySQL 服务实例。

**参数：**
- `serviceName` (string)：要停止的 MySQL 服务名称

**返回：** `Task<bool>` - 如果服务成功停止则返回 True

**示例：**
```csharp
var mysqlManager = serviceProvider.GetService<IMySQLManager>();
bool stopped = await mysqlManager.StopInstanceAsync("MySQL80");
```

##### `StartInstanceAsync(string serviceName)`
启动 MySQL 服务实例。

**参数：**
- `serviceName` (string)：要启动的 MySQL 服务名称

**返回：** `Task<bool>` - 如果服务成功启动则返回 True

##### `VerifyInstanceAvailabilityAsync(MySQLConnectionInfo connection)`
验证 MySQL 实例是否可用并接受连接。

**参数：**
- `connection` (MySQLConnectionInfo)：MySQL 实例的连接信息

**返回：** `Task<bool>` - 如果实例可用则返回 True

##### `VerifyInstanceAvailabilityAsync(MySQLConnectionInfo connection, int timeoutSeconds)`
使用可配置超时验证 MySQL 实例可用性。

**参数：**
- `connection` (MySQLConnectionInfo)：连接信息
- `timeoutSeconds` (int)：连接超时（秒）

**返回：** `Task<bool>` - 如果实例可用则返回 True

---

### ICompressionService

处理备份文件的文件压缩操作。

#### 方法

##### `CompressDirectoryAsync(string sourcePath, string targetPath, IProgress<CompressionProgress>? progress = null)`
将目录压缩为 zip 文件。

**参数：**
- `sourcePath` (string)：要压缩的目录路径
- `targetPath` (string)：应创建压缩文件的路径
- `progress` (IProgress<CompressionProgress>?, 可选)：进度报告器

**返回：** `Task<string>` - 创建的压缩文件路径

**示例：**
```csharp
var compressionService = serviceProvider.GetService<ICompressionService>();
var progress = new Progress<CompressionProgress>(p => 
    Console.WriteLine($"压缩：{p.PercentComplete:F1}%"));

string compressedFile = await compressionService.CompressDirectoryAsync(
    @"C:\MySQL\Data", 
    @"C:\Backups\backup.zip", 
    progress);
```

##### `CleanupAsync(string filePath)`
清理压缩期间创建的临时文件。

**参数：**
- `filePath` (string)：要清理的文件路径

**返回：** `Task`

---

### IFileTransferService

管理客户端和服务器之间的文件传输操作。

#### 方法

##### `TransferFileAsync(string filePath, TransferConfig config, CancellationToken cancellationToken = default)`
将文件传输到远程服务器。

**参数：**
- `filePath` (string)：要传输的文件路径
- `config` (TransferConfig)：传输配置设置
- `cancellationToken` (CancellationToken, 可选)：取消令牌

**返回：** `Task<TransferResult>` - 传输操作的结果

##### `ResumeTransferAsync(string resumeToken, CancellationToken cancellationToken = default)`
恢复中断的文件传输。

**参数：**
- `resumeToken` (string)：标识中断传输的令牌
- `cancellationToken` (CancellationToken, 可选)：取消令牌

**返回：** `Task<TransferResult>` - 恢复传输的结果

##### `ResumeTransferAsync(string resumeToken, string filePath, TransferConfig config, CancellationToken cancellationToken = default)`
使用完整上下文恢复中断的文件传输。

**参数：**
- `resumeToken` (string)：标识中断传输的令牌
- `filePath` (string)：要传输的文件路径
- `config` (TransferConfig)：传输配置设置
- `cancellationToken` (CancellationToken, 可选)：取消令牌

**返回：** `Task<TransferResult>` - 恢复传输的结果

---

## 备份管理

### IBackupScheduler

使用 cron 表达式支持管理备份调度。

#### 方法

##### `StartAsync(CancellationToken cancellationToken = default)`
启动备份调度器服务。

##### `StopAsync(CancellationToken cancellationToken = default)`
停止备份调度器服务。

##### `AddOrUpdateScheduleAsync(ScheduleConfiguration scheduleConfig)`
添加或更新调度配置。

**参数：**
- `scheduleConfig` (ScheduleConfiguration)：要添加或更新的调度配置

**返回：** `Task<ScheduleConfiguration>` - 保存的调度配置

##### `RemoveScheduleAsync(int scheduleId)`
删除调度配置。

**参数：**
- `scheduleId` (int)：要删除的调度 ID

##### `GetAllSchedulesAsync()`
获取所有调度配置。

**返回：** `Task<IEnumerable<ScheduleConfiguration>>` - 所有调度的列表

##### `GetSchedulesForBackupConfigAsync(int backupConfigId)`
获取特定备份配置的调度配置。

**参数：**
- `backupConfigId` (int)：备份配置 ID

**返回：** `Task<List<ScheduleConfiguration>>` - 调度配置列表

##### `SetScheduleEnabledAsync(int scheduleId, bool enabled)`
启用或禁用调度。

**参数：**
- `scheduleId` (int)：调度 ID
- `enabled` (bool)：是否启用或禁用调度

##### `GetNextScheduledTimeAsync()`
获取所有调度中的下一个计划备份时间。

**返回：** `Task<DateTime?>` - 下一个计划备份时间，如果没有启用的调度则为 null

##### `TriggerScheduledBackupAsync(int scheduleId)`
手动触发计划备份。

**参数：**
- `scheduleId` (int)：要触发的调度 ID

##### `ValidateScheduleAsync(ScheduleConfiguration scheduleConfig)`
验证调度配置。

**参数：**
- `scheduleConfig` (ScheduleConfiguration)：要验证的调度配置

**返回：** `Task<(bool IsValid, List<string> Errors)>` - 验证结果

---

### IRetentionPolicyService

管理备份保留策略和自动清理。

#### 方法

##### `ExecuteRetentionPoliciesAsync()`
执行所有启用的保留策略。

**返回：** `Task<RetentionExecutionResult>` - 执行结果

##### `ApplyRetentionPolicyAsync(RetentionPolicy policy)`
应用特定的保留策略。

**参数：**
- `policy` (RetentionPolicy)：要应用的保留策略

**返回：** `Task<RetentionExecutionResult>` - 执行结果

##### `CreateRetentionPolicyAsync(RetentionPolicy policy)`
创建带验证的新保留策略。

**参数：**
- `policy` (RetentionPolicy)：要创建的保留策略

**返回：** `Task<RetentionPolicy>` - 创建的策略

##### `UpdateRetentionPolicyAsync(RetentionPolicy policy)`
更新现有保留策略。

**参数：**
- `policy` (RetentionPolicy)：要更新的保留策略

**返回：** `Task<RetentionPolicy>` - 更新的策略

##### `DeleteRetentionPolicyAsync(int policyId)`
删除保留策略。

**参数：**
- `policyId` (int)：要删除的策略 ID

**返回：** `Task<bool>` - 如果删除成功则返回 True

##### `GetAllRetentionPoliciesAsync()`
获取所有保留策略。

**返回：** `Task<IEnumerable<RetentionPolicy>>` - 所有保留策略

##### `GetEnabledRetentionPoliciesAsync()`
获取所有启用的保留策略。

**返回：** `Task<IEnumerable<RetentionPolicy>>` - 启用的保留策略

##### `GetRetentionPolicyByIdAsync(int policyId)`
按 ID 获取保留策略。

**参数：**
- `policyId` (int)：策略 ID

**返回：** `Task<RetentionPolicy?>` - 策略，如果未找到则为 null

##### `GetRetentionPolicyByNameAsync(string name)`
按名称获取保留策略。

**参数：**
- `name` (string)：策略名称

**返回：** `Task<RetentionPolicy?>` - 策略，如果未找到则为 null

##### `EnableRetentionPolicyAsync(int policyId)`
启用保留策略。

**参数：**
- `policyId` (int)：策略 ID

**返回：** `Task<bool>` - 如果成功则返回 True

##### `DisableRetentionPolicyAsync(int policyId)`
禁用保留策略。

**参数：**
- `policyId` (int)：策略 ID

**返回：** `Task<bool>` - 如果成功则返回 True

##### `GetRetentionPolicyRecommendationsAsync()`
基于当前备份模式获取保留策略建议。

**返回：** `Task<List<RetentionPolicy>>` - 推荐策略

##### `ValidateRetentionPolicyAsync(RetentionPolicy policy)`
验证保留策略配置。

**参数：**
- `policy` (RetentionPolicy)：要验证的策略

**返回：** `Task<(bool IsValid, List<string> Errors)>` - 验证结果

##### `EstimateRetentionImpactAsync(RetentionPolicy policy)`
估计应用保留策略的影响。

**参数：**
- `policy` (RetentionPolicy)：要分析的策略

**返回：** `Task<RetentionImpactEstimate>` - 影响估计

---

## 文件操作

### IEncryptionService

使用 AES-256 提供文件加密和解密功能。

#### 方法

##### `EncryptAsync(string inputPath, string outputPath, string password, CancellationToken cancellationToken = default)`
使用 AES-256 加密加密文件。

**参数：**
- `inputPath` (string)：要加密的文件路径
- `outputPath` (string)：加密文件将保存的路径
- `password` (string)：加密密码
- `cancellationToken` (CancellationToken, 可选)：取消令牌

**返回：** `Task<EncryptionMetadata>` - 加密元数据

**示例：**
```csharp
var encryptionService = serviceProvider.GetService<IEncryptionService>();
var metadata = await encryptionService.EncryptAsync(
    @"C:\Backups\backup.zip",
    @"C:\Backups\backup.zip.enc",
    "SecurePassword123!");
```

##### `DecryptAsync(string inputPath, string outputPath, string password, CancellationToken cancellationToken = default)`
解密使用 AES-256 加密的文件。

**参数：**
- `inputPath` (string)：加密文件的路径
- `outputPath` (string)：解密文件将保存的路径
- `password` (string)：解密密码
- `cancellationToken` (CancellationToken, 可选)：取消令牌

**返回：** `Task` - 异步操作

##### `ValidatePasswordAsync(string encryptedFilePath, string password)`
验证提供的密码是否可以解密加密文件。

**参数：**
- `encryptedFilePath` (string)：加密文件的路径
- `password` (string)：要验证的密码

**返回：** `Task<bool>` - 如果密码正确则返回 True

##### `GetMetadataAsync(string encryptedFilePath)`
从加密文件获取元数据。

**参数：**
- `encryptedFilePath` (string)：加密文件的路径

**返回：** `Task<EncryptionMetadata>` - 加密元数据

##### `GenerateSecurePassword(int length = 32)`
生成安全的随机密码。

**参数：**
- `length` (int, 可选)：密码长度（默认：32）

**返回：** `string` - 安全的随机密码

---

### IValidationService

提供备份文件验证和完整性检查。

#### 方法

##### `ValidateBackupAsync(string filePath, CancellationToken cancellationToken = default)`
验证备份文件的完整性和完整性。

**参数：**
- `filePath` (string)：要验证的备份文件路径
- `cancellationToken` (CancellationToken, 可选)：取消令牌

**返回：** `Task<FileValidationResult>` - 带详细信息的验证结果

##### `ValidateIntegrityAsync(string filePath, string expectedChecksum, ChecksumAlgorithm algorithm = ChecksumAlgorithm.SHA256)`
使用校验和比较验证文件完整性。

**参数：**
- `filePath` (string)：要验证的文件路径
- `expectedChecksum` (string)：预期的校验和值
- `algorithm` (ChecksumAlgorithm, 可选)：要使用的校验和算法

**返回：** `Task<bool>` - 如果校验和匹配则返回 True

##### `CalculateChecksumAsync(string filePath, ChecksumAlgorithm algorithm = ChecksumAlgorithm.SHA256, CancellationToken cancellationToken = default)`
计算文件的校验和。

**参数：**
- `filePath` (string)：文件路径
- `algorithm` (ChecksumAlgorithm, 可选)：要使用的校验和算法
- `cancellationToken` (CancellationToken, 可选)：取消令牌

**返回：** `Task<string>` - 计算的校验和（十六进制字符串）

##### `GenerateReportAsync(string filePath, CancellationToken cancellationToken = default)`
为备份文件生成综合验证报告。

**参数：**
- `filePath` (string)：备份文件路径
- `cancellationToken` (CancellationToken, 可选)：取消令牌

**返回：** `Task<ValidationReport>` - 详细验证报告

##### `ValidateCompressionAsync(string filePath, CancellationToken cancellationToken = default)`
验证备份文件可以成功解压缩。

**参数：**
- `filePath` (string)：压缩备份文件路径
- `cancellationToken` (CancellationToken, 可选)：取消令牌

**返回：** `Task<bool>` - 如果文件可以解压缩则返回 True

##### `ValidateEncryptionAsync(string filePath, string password, CancellationToken cancellationToken = default)`
验证加密备份文件可以使用提供的密码解密。

**参数：**
- `filePath` (string)：加密备份文件路径
- `password` (string)：解密密码
- `cancellationToken` (CancellationToken, 可选)：取消令牌

**返回：** `Task<bool>` - 如果文件可以解密则返回 True

---

## 通知系统

### INotificationService

提供带 SMTP 支持的邮件通知功能。

#### 属性

##### `Configuration`
获取当前 SMTP 配置。

**类型：** `SmtpConfig`

#### 方法

##### `SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)`
异步发送单个邮件消息。

**参数：**
- `message` (EmailMessage)：要发送的邮件消息
- `cancellationToken` (CancellationToken, 可选)：取消令牌

**返回：** `Task<bool>` - 如果邮件发送成功则返回 True

**异常：**
- `ArgumentNullException`：当消息为 null 时抛出
- `InvalidOperationException`：当 SMTP 配置无效时抛出

**示例：**
```csharp
var notificationService = serviceProvider.GetService<INotificationService>();
var message = new EmailMessage
{
    To = "admin@example.com",
    Subject = "备份成功完成",
    Body = "MySQL 备份在 " + DateTime.Now + " 成功完成",
    IsHtml = false
};

bool sent = await notificationService.SendEmailAsync(message);
```

##### `SendBulkEmailAsync(IEnumerable<EmailMessage> messages, CancellationToken cancellationToken = default)`
批量发送多个邮件消息。

**参数：**
- `messages` (IEnumerable<EmailMessage>)：要发送的邮件消息集合
- `cancellationToken` (CancellationToken, 可选)：取消令牌

**返回：** `Task<Dictionary<string, bool>>` - 将消息 ID 映射到其发送成功状态的字典

##### `GetStatusAsync(string notificationId, CancellationToken cancellationToken = default)`
获取特定通知的传递状态。

**参数：**
- `notificationId` (string)：通知的唯一标识符
- `cancellationToken` (CancellationToken, 可选)：取消令牌

**返回：** `Task<NotificationStatus?>` - 当前状态，如果未找到则为 null

##### `GetTemplatesAsync(CancellationToken cancellationToken = default)`
检索所有可用的邮件模板。

**返回：** `Task<IEnumerable<EmailTemplate>>` - 可用邮件模板的集合

##### `GetTemplatesByCategoryAsync(string category, CancellationToken cancellationToken = default)`
按类别检索邮件模板。

**参数：**
- `category` (string)：要过滤的模板类别

**返回：** `Task<IEnumerable<EmailTemplate>>` - 指定类别中的邮件模板集合

##### `GetTemplateAsync(string templateName, CancellationToken cancellationToken = default)`
按名称获取特定邮件模板。

**参数：**
- `templateName` (string)：要检索的模板名称

**返回：** `Task<EmailTemplate?>` - 邮件模板，如果未找到则为 null

##### `TestSmtpConnectionAsync(SmtpConfig config, CancellationToken cancellationToken = default)`
使用提供的配置测试 SMTP 连接。

**参数：**
- `config` (SmtpConfig)：要测试的 SMTP 配置

**返回：** `Task<bool>` - 如果连接测试成功则返回 True

##### `CreateEmailFromTemplateAsync(string templateName, string recipient, Dictionary<string, object> variables, CancellationToken cancellationToken = default)`
从模板创建邮件消息，支持变量替换。

**参数：**
- `templateName` (string)：要使用的模板名称
- `recipient` (string)：收件人邮件地址
- `variables` (Dictionary<string, object>)：要在模板中替换的变量字典

**返回：** `Task<EmailMessage?>` - 创建的邮件消息，如果未找到模板则为 null

##### `UpdateConfiguration(SmtpConfig config)`
更新 SMTP 配置。

**参数：**
- `config` (SmtpConfig)：新的 SMTP 配置

##### `GetStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)`
获取通知传递统计信息。

**参数：**
- `fromDate` (DateTime?, 可选)：统计开始日期
- `toDate` (DateTime?, 可选)：统计结束日期
- `cancellationToken` (CancellationToken, 可选)：取消令牌

**返回：** `Task<NotificationStatistics>` - 通知传递统计信息

---

## 调度

### 调度配置

备份调度使用 cron 表达式进行灵活的时间配置。

#### Cron 表达式格式

系统支持标准 cron 表达式，格式如下：
```
* * * * * *
│ │ │ │ │ │
│ │ │ │ │ └─── 星期几 (0-7, 星期日 = 0 或 7)
│ │ │ │ └───── 月份 (1-12)
│ │ │ └─────── 月中的日 (1-31)
│ │ └───────── 小时 (0-23)
│ └─────────── 分钟 (0-59)
└───────────── 秒 (0-59)
```

#### 常见 Cron 示例

- `0 0 2 * * *` - 每天凌晨 2:00
- `0 0 2 * * 0` - 每周日凌晨 2:00
- `0 0 2 1 * *` - 每月 1 日凌晨 2:00
- `0 */30 * * * *` - 每 30 分钟
- `0 0 */6 * * *` - 每 6 小时

---

## 安全

### 身份验证和授权

系统提供几个与安全相关的接口：

#### IAuthenticationService
处理用户身份验证和会话管理。

#### IEncryptionService
为备份文件提供 AES-256 加密，具有以下安全功能：

- **密钥派生：** PBKDF2，可配置迭代次数（默认：100,000）
- **盐生成：** 每次加密的加密安全随机盐
- **IV 生成：** 每次加密的唯一初始化向量
- **安全内存：** 内存中敏感数据的适当清理

#### 安全最佳实践

1. **密码强度：** 使用强加密密码（最少 12 个字符）
2. **密钥存储：** 安全存储加密密码，永远不要以明文形式存储
3. **网络安全：** 为所有网络通信使用 TLS
4. **文件权限：** 确保备份文件具有适当的访问限制
5. **审计日志：** 为安全事件启用综合日志记录

---

## 数据模型

### 核心配置模型

#### BackupConfiguration
表示完整的备份作业配置。

**关键属性：**
- `Id` (int)：唯一标识符
- `Name` (string)：配置名称（1-100 个字符，字母数字 + 空格/连字符/下划线）
- `MySQLConnection` (MySQLConnectionInfo)：数据库连接详情
- `DataDirectoryPath` (string)：MySQL 数据目录路径
- `ServiceName` (string)：MySQL 服务名称
- `TargetServer` (ServerEndpoint)：目标服务器信息
- `TargetDirectory` (string)：备份目标目录
- `NamingStrategy` (FileNamingStrategy)：文件命名配置
- `IsActive` (bool)：配置是否活动
- `CreatedAt` (DateTime)：创建时间戳

**验证：**
- 实现 `IValidatableObject` 进行综合验证
- 验证目录路径和可访问性
- 检查连接参数
- 确保命名策略有效性

#### MySQLConnectionInfo
数据库连接配置。

**关键属性：**
- `Server` (string)：MySQL 服务器主机名/IP
- `Port` (int)：MySQL 服务器端口（默认：3306）
- `Username` (string)：数据库用户名
- `Password` (string)：数据库密码（建议加密存储）
- `Database` (string)：目标数据库名称
- `ConnectionTimeout` (int)：连接超时（秒）

#### ServerEndpoint
备份存储的目标服务器配置。

**关键属性：**
- `IPAddress` (string)：服务器 IP 地址
- `Port` (int)：服务器端口
- `Protocol` (string)：传输协议（TCP、HTTP 等）
- `AuthenticationRequired` (bool)：是否需要身份验证
- `Credentials` (NetworkCredential)：身份验证凭据

### 通知模型

#### EmailMessage
表示邮件通知。

**关键属性：**
- `Id` (string)：唯一消息标识符
- `To` (string)：收件人邮件地址
- `Subject` (string)：邮件主题（最多 200 个字符）
- `Body` (string)：邮件正文内容（最多 10,000 个字符）
- `IsHtml` (bool)：正文是否为 HTML 格式
- `Attachments` (List<string>)：文件附件路径
- `Headers` (Dictionary<string, string>)：自定义邮件标头
- `Priority` (EmailPriority)：邮件优先级
- `CreatedAt` (DateTime)：创建时间戳

#### SmtpConfig
SMTP 服务器配置。

**关键属性：**
- `Host` (string)：SMTP 服务器主机名
- `Port` (int)：SMTP 服务器端口（默认：587）
- `EnableSsl` (bool)：是否使用 SSL/TLS
- `Username` (string)：SMTP 身份验证用户名
- `Password` (string)：SMTP 身份验证密码
- `FromAddress` (string)：发件人邮件地址
- `FromName` (string)：发件人显示名称
- `TimeoutSeconds` (int)：连接超时（5-300 秒）

#### EmailTemplate
通知的邮件模板。

**关键属性：**
- `Name` (string)：模板标识符
- `Description` (string)：模板描述
- `Subject` (string)：邮件主题模板
- `HtmlBody` (string)：HTML 正文模板
- `TextBody` (string)：纯文本正文模板
- `RequiredVariables` (List<string>)：必需的模板变量
- `Category` (string)：模板类别
- `IsActive` (bool)：模板是否活动

### 加密模型

#### EncryptionMetadata
加密文件的元数据。

**关键属性：**
- `Algorithm` (string)：加密算法（默认："AES-256-CBC"）
- `KeyDerivation` (string)：密钥派生函数（默认："PBKDF2"）
- `Iterations` (int)：PBKDF2 迭代次数（默认：100,000）
- `Salt` (string)：Base64 编码的盐
- `IV` (string)：Base64 编码的初始化向量
- `EncryptedAt` (DateTime)：加密时间戳
- `OriginalSize` (long)：原始文件大小
- `OriginalChecksum` (string)：原始文件的 SHA256 校验和
- `Version` (int)：加密格式版本

#### EncryptionConfig
加密操作的配置。

**关键属性：**
- `Password` (string)：加密密码
- `KeySize` (int)：密钥大小（位）（默认：256）
- `Iterations` (int)：PBKDF2 迭代次数（默认：100,000）
- `SecureDelete` (bool)：是否安全删除原始文件
- `BufferSize` (int)：流缓冲区大小（默认：64KB）
- `CompressBeforeEncryption` (bool)：是否在加密前压缩

### 进度和状态模型

#### CompressionProgress
压缩操作的进度信息。

**关键属性：**
- `PercentComplete` (double)：完成百分比（0-100）
- `BytesProcessed` (long)：到目前为止处理的字节数
- `TotalBytes` (long)：要处理的总字节数
- `CurrentFile` (string)：当前处理的文件
- `EstimatedTimeRemaining` (TimeSpan?)：估计剩余时间

#### TransferResult
文件传输操作的结果。

**关键属性：**
- `Success` (bool)：传输是否成功
- `BytesTransferred` (long)：传输的字节数
- `Duration` (TimeSpan)：传输持续时间
- `AverageSpeed` (double)：平均传输速度（字节/秒）
- `ResumeToken` (string)：用于恢复中断传输的令牌
- `ErrorMessage` (string)：传输失败时的错误消息

---

## 错误处理

### 异常层次结构

系统使用结构化异常层次结构进行综合错误处理：

#### BackupException
所有备份相关错误的基异常类。

**属性：**
- `OperationId` (string)：操作的唯一标识符
- `Timestamp` (DateTime)：错误发生时间
- `Context` (Dictionary<string, object>)：附加错误上下文

#### NetworkRetryException
当网络重试尝试耗尽时抛出。

**属性：**
- `OperationName` (string)：失败操作的名称
- `AttemptsExhausted` (int)：进行的重试尝试次数
- `TotalDuration` (TimeSpan)：重试花费的总时间

#### ValidationException
当验证失败时抛出。

**属性：**
- `ValidationErrors` (List<string>)：验证错误消息列表
- `PropertyName` (string)：验证失败的属性名称

### 错误代码

系统使用标准化错误代码进行一致的错误处理：

- `MYSQL_001`：MySQL 服务启动/停止失败
- `MYSQL_002`：MySQL 连接失败
- `COMPRESS_001`：压缩操作失败
- `TRANSFER_001`：文件传输失败
- `ENCRYPT_001`：加密操作失败
- `VALIDATE_001`：文件验证失败
- `NOTIFY_001`：通知传递失败
- `SCHEDULE_001`：调度配置错误
- `RETENTION_001`：保留策略执行错误

### 日志记录

所有操作都使用 Serilog 进行结构化日志记录：

```csharp
// 示例日志条目结构
{
  "Timestamp": "2024-01-25T10:30:00.000Z",
  "Level": "Information",
  "MessageTemplate": "备份操作 {OperationId} 成功完成",
  "Properties": {
    "OperationId": "backup-20240125-103000",
    "ConfigurationId": 1,
    "Duration": "00:05:30",
    "BytesProcessed": 1073741824,
    "CompressionRatio": 0.65
  }
}
```

---

## 使用示例

### 完整备份工作流

```csharp
// 配置服务
var services = new ServiceCollection();
services.AddMySqlBackupTool();
var serviceProvider = services.BuildServiceProvider();

// 获取所需服务
var mysqlManager = serviceProvider.GetService<IMySQLManager>();
var compressionService = serviceProvider.GetService<ICompressionService>();
var encryptionService = serviceProvider.GetService<IEncryptionService>();
var transferService = serviceProvider.GetService<IFileTransferService>();
var notificationService = serviceProvider.GetService<INotificationService>();

try
{
    // 1. 停止 MySQL 服务
    await mysqlManager.StopInstanceAsync("MySQL80");
    
    // 2. 压缩数据目录
    var compressedFile = await compressionService.CompressDirectoryAsync(
        @"C:\MySQL\Data", 
        @"C:\Temp\backup.zip");
    
    // 3. 加密备份文件
    var encryptedFile = @"C:\Temp\backup.zip.enc";
    await encryptionService.EncryptAsync(compressedFile, encryptedFile, "SecurePassword123!");
    
    // 4. 传输到服务器
    var transferConfig = new TransferConfig
    {
        TargetServer = new ServerEndpoint { IPAddress = "192.168.1.100", Port = 8080 },
        TargetDirectory = "/backups",
        EnableResume = true
    };
    
    var transferResult = await transferService.TransferFileAsync(encryptedFile, transferConfig);
    
    // 5. 发送通知
    var notification = new EmailMessage
    {
        To = "admin@example.com",
        Subject = "备份成功完成",
        Body = $"备份完成。传输了 {transferResult.BytesTransferred} 字节。",
        IsHtml = false
    };
    
    await notificationService.SendEmailAsync(notification);
    
    // 6. 清理临时文件
    await compressionService.CleanupAsync(compressedFile);
    File.Delete(encryptedFile);
}
finally
{
    // 7. 重启 MySQL 服务
    await mysqlManager.StartInstanceAsync("MySQL80");
}
```

### 调度配置

```csharp
var scheduler = serviceProvider.GetService<IBackupScheduler>();

// 创建每日备份调度
var schedule = new ScheduleConfiguration
{
    Name = "每日备份",
    CronExpression = "0 0 2 * * *", // 每天凌晨 2 点
    BackupConfigurationId = 1,
    IsEnabled = true,
    Description = "凌晨 2 点的每日备份"
};

await scheduler.AddOrUpdateScheduleAsync(schedule);
await scheduler.StartAsync();
```

---

## 配置

### 依赖注入设置

```csharp
// Program.cs 或 Startup.cs
services.AddMySqlBackupTool(options =>
{
    options.ConnectionString = "Data Source=backup.db";
    options.EnableEncryption = true;
    options.EnableNotifications = true;
    options.DefaultCompressionLevel = CompressionLevel.Optimal;
});

// 为通知配置 SMTP
services.Configure<SmtpConfig>(options =>
{
    options.Host = "smtp.gmail.com";
    options.Port = 587;
    options.EnableSsl = true;
    options.Username = "backup@example.com";
    options.Password = "app-password";
    options.FromAddress = "backup@example.com";
    options.FromName = "MySQL 备份工具";
});
```

### 配置文件 (appsettings.json)

```json
{
  "MySqlBackupTool": {
    "ConnectionString": "Data Source=backup.db",
    "EnableEncryption": true,
    "EnableNotifications": true,
    "DefaultCompressionLevel": "Optimal",
    "MaxConcurrentBackups": 3,
    "DefaultRetentionDays": 30
  },
  "SmtpConfig": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "EnableSsl": true,
    "Username": "backup@example.com",
    "Password": "app-password",
    "FromAddress": "backup@example.com",
    "FromName": "MySQL 备份工具",
    "TimeoutSeconds": 30
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "MySqlBackupTool": "Debug"
    }
  }
}
```

---

此 API 参考为 MySQL 备份工具中的所有公共接口和模型提供了综合文档。有关实现示例和高级使用场景，请参阅项目中包含的集成测试和示例应用程序。