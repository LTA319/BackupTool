# MySQL备份工具服务器端

## 项目概述

MySQL备份工具服务器端是一个基于.NET 8.0的控制台应用程序，作为备份系统的服务器组件运行。它负责接收来自客户端的备份文件传输请求，管理备份文件存储，并提供备份文件的集中管理功能。

## 主要功能

### 核心功能
- **文件接收服务**：监听指定端口，接收来自客户端的备份文件
- **并发处理**：支持多个客户端同时连接和传输文件
- **安全传输**：支持SSL/TLS加密传输和客户端认证
- **文件管理**：自动组织和管理接收到的备份文件
- **保留策略**：根据配置的保留策略自动清理过期备份

### 高级功能
- **压缩支持**：支持备份文件的实时压缩和解压缩
- **加密支持**：支持备份文件的加密存储
- **完整性验证**：通过校验和验证文件传输的完整性
- **断点续传**：支持大文件的断点续传功能
- **监控告警**：集成告警系统，及时通知异常情况

## 项目结构

```
src/MySqlBackupTool.Server/
├── Program.cs                    # 程序入口点，配置和启动服务
├── FileReceiverService.cs        # 文件接收服务的后台服务实现
├── MySqlBackupTool.Server.csproj # 项目文件，定义依赖和配置
├── appsettings.json              # 生产环境配置文件
├── appsettings.Development.json  # 开发环境配置文件
├── 配置说明.md                   # 详细的配置参数说明文档
└── README.md                     # 项目说明文档（本文件）
```

## 技术架构

### 框架和技术栈
- **.NET 8.0**：基础运行时框架
- **Microsoft.Extensions.Hosting**：托管服务框架
- **Microsoft.Extensions.DependencyInjection**：依赖注入容器
- **Microsoft.Extensions.Logging**：结构化日志记录

### 设计模式
- **托管服务模式**：使用BackgroundService实现长期运行的后台服务
- **依赖注入**：通过DI容器管理服务生命周期和依赖关系
- **接口分离**：通过接口定义服务契约，提高可测试性和可维护性
- **配置模式**：使用强类型配置和选项模式

## 服务组件

### 1. Program类
- **职责**：应用程序入口点，负责配置和启动服务
- **功能**：
  - 配置依赖注入容器
  - 初始化数据库
  - 设置优雅关闭处理
  - 启动托管服务

### 2. FileReceiverService类
- **职责**：文件接收服务的后台服务实现
- **功能**：
  - 启动和管理文件接收器
  - 处理服务生命周期事件
  - 错误处理和日志记录
  - 优雅关闭处理

## 配置管理

服务器支持多环境配置：

### 配置文件层次
1. `appsettings.json` - 基础配置
2. `appsettings.{Environment}.json` - 环境特定配置
3. 环境变量 - 运行时配置覆盖
4. 命令行参数 - 启动时配置覆盖

### 主要配置节
- **MySqlBackupTool**：核心应用配置
- **ServerConfig**：服务器网络配置
- **StorageConfig**：存储相关配置
- **Alerting**：告警系统配置
- **Logging**：日志记录配置
- **MemoryProfiling**：内存分析配置

详细配置说明请参考 [配置说明.md](./配置说明.md)

## 部署和运行

### 开发环境运行
```bash
# 进入项目目录
cd src/MySqlBackupTool.Server

# 运行开发环境
dotnet run --environment Development

# 或者构建后运行
dotnet build
dotnet run
```

### 生产环境部署
```bash
# 发布应用
dotnet publish -c Release -o ./publish

# 运行发布版本
cd publish
./MySqlBackupTool.Server

# 或者作为Windows服务运行
sc create MySqlBackupServer binPath="C:\path\to\MySqlBackupTool.Server.exe"
sc start MySqlBackupServer
```

### Docker部署
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY publish/ .
EXPOSE 8080
ENTRYPOINT ["dotnet", "MySqlBackupTool.Server.dll"]
```

## 监控和维护

### 日志记录
- **结构化日志**：使用Microsoft.Extensions.Logging进行结构化日志记录
- **日志级别**：支持Trace、Debug、Information、Warning、Error、Critical级别
- **日志输出**：支持控制台、文件、第三方日志系统输出

### 性能监控
- **内存分析**：可选的内存使用情况监控和分析
- **连接监控**：监控客户端连接数和传输状态
- **存储监控**：监控磁盘使用情况和清理状态

### 告警系统
- **邮件告警**：支持SMTP邮件通知
- **Webhook告警**：支持HTTP Webhook通知
- **文件日志告警**：本地文件日志记录

## 安全考虑

### 网络安全
- **SSL/TLS加密**：支持传输层加密
- **客户端认证**：支持基于证书或令牌的客户端认证
- **IP白名单**：限制允许连接的客户端IP地址

### 数据安全
- **文件加密**：支持备份文件的静态加密
- **完整性校验**：通过校验和验证文件完整性
- **访问控制**：基于角色的访问控制

### 运行时安全
- **输入验证**：严格验证客户端输入
- **资源限制**：限制并发连接数和文件大小
- **错误处理**：安全的错误处理，避免信息泄露

## 故障排除

### 常见问题

1. **端口占用**
   - 检查配置的端口是否被其他程序占用
   - 使用`netstat -an | findstr :8080`检查端口状态

2. **权限问题**
   - 确保程序有足够权限访问存储目录
   - 检查防火墙设置是否阻止了端口访问

3. **SSL证书问题**
   - 验证证书文件路径和密码
   - 确保证书未过期且格式正确

4. **数据库连接问题**
   - 检查数据库文件路径和权限
   - 验证连接字符串格式

### 日志分析
- 查看应用程序日志了解详细错误信息
- 检查系统事件日志获取系统级错误
- 使用性能计数器监控资源使用情况

## 扩展和定制

### 自定义服务
可以通过实现相应接口来扩展功能：
- `IFileReceiver`：自定义文件接收逻辑
- `IStorageManager`：自定义存储管理
- `IAlertingService`：自定义告警服务

### 插件架构
支持通过依赖注入容器注册自定义服务和中间件。

## 版本历史

- **v1.0.0**：初始版本，基本的文件接收和存储功能
- 后续版本将添加更多高级功能和性能优化

## 许可证

本项目采用MIT许可证，详情请参考LICENSE文件。

## 支持和贡献

如有问题或建议，请通过以下方式联系：
- 提交Issue到项目仓库
- 发送邮件到项目维护者
- 参与项目讨论和代码贡献