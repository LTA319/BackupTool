using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Data;
using MySqlBackupTool.Shared.Data.Migrations;
using MySqlBackupTool.Shared.Data.Repositories;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Logging;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Polly;
using Polly.Extensions.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace MySqlBackupTool.Shared.DependencyInjection;

/// <summary>
/// 依赖注入服务配置扩展方法类
/// 为MySQL备份工具提供统一的服务注册和配置功能
/// </summary>
/// <remarks>
/// 该类提供以下主要功能：
/// 1. 共享服务注册 - 数据库、仓储、业务服务等
/// 2. 客户端特定服务注册 - 备份执行、文件传输等
/// 3. 服务器端特定服务注册 - 文件接收、存储管理等
/// 4. 调度服务注册 - 定时备份、后台任务等
/// 5. 错误处理和重试策略配置
/// 6. SSL/TLS安全配置
/// 7. 服务验证和诊断功能
/// 
/// 设计原则：
/// - 模块化：不同类型的服务分别注册
/// - 可配置：支持通过配置文件自定义行为
/// - 容错性：包含完善的错误处理和重试机制
/// - 安全性：支持SSL/TLS和认证配置
/// - 可测试性：支持依赖注入和接口抽象
/// </remarks>
public static class ServiceCollectionExtensions
{
    #region 共享服务注册

    /// <summary>
    /// 向依赖注入容器添加共享服务（简化版本）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connectionString">数据库连接字符串</param>
    /// <returns>配置后的服务集合，支持链式调用</returns>
    /// <remarks>
    /// 这是AddSharedServices方法的简化版本，不需要配置参数
    /// 适用于不需要复杂配置的场景
    /// </remarks>
    public static IServiceCollection AddSharedServices(this IServiceCollection services, string connectionString)
    {
        return services.AddSharedServices(connectionString, null);
    }

    /// <summary>
    /// 向依赖注入容器添加共享服务（完整版本）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connectionString">数据库连接字符串</param>
    /// <param name="configuration">应用程序配置对象，可为null</param>
    /// <returns>配置后的服务集合，支持链式调用</returns>
    /// <remarks>
    /// 该方法注册以下类型的服务：
    /// 
    /// 1. 数据访问层：
    ///    - Entity Framework DbContext
    ///    - 各种仓储接口和实现
    ///    - 数据库迁移服务
    /// 
    /// 2. 业务服务层：
    ///    - 备份日志服务
    ///    - 备份报告服务
    ///    - 保留策略服务
    /// 
    /// 3. 网络和通信服务：
    ///    - HTTP客户端配置
    ///    - 网络重试服务
    ///    - 告警服务
    /// 
    /// 4. 安全和认证服务：
    ///    - 凭据存储服务
    ///    - 令牌管理服务
    ///    - 认证和授权服务
    /// 
    /// 5. 基础设施服务：
    ///    - 日志记录服务
    ///    - 内存分析服务
    ///    - 加密和压缩服务
    ///    - 错误处理服务
    /// 
    /// 6. 验证和诊断服务：
    ///    - 启动验证服务
    ///    - 依赖关系验证服务
    /// </remarks>
    /// <exception cref="ArgumentNullException">当services或connectionString为null时抛出</exception>
    /// <example>
    /// <code>
    /// var services = new ServiceCollection();
    /// var connectionString = "Data Source=backup.db";
    /// var configuration = new ConfigurationBuilder().Build();
    /// 
    /// services.AddSharedServices(connectionString, configuration);
    /// 
    /// var serviceProvider = services.BuildServiceProvider();
    /// var dbContext = serviceProvider.GetRequiredService&lt;BackupDbContext&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddSharedServices(this IServiceCollection services, string connectionString, IConfiguration? configuration)
    {
        #region 数据访问层服务注册

        // 注册Entity Framework数据库上下文
        // 使用SQLite作为本地数据库，存储备份配置、日志等信息
        services.AddDbContext<BackupDbContext>(options =>
            options.UseSqlite(connectionString));

        // 注册仓储模式实现
        // 为各种实体提供统一的数据访问接口
        services.AddScoped<IBackupConfigurationRepository, BackupConfigurationRepository>();
        services.AddScoped<IBackupLogRepository, BackupLogRepository>();
        services.AddScoped<IRetentionPolicyRepository, RetentionPolicyRepository>();
        services.AddScoped<IResumeTokenRepository, ResumeTokenRepository>();
        services.AddScoped<IScheduleConfigurationRepository, ScheduleConfigurationRepository>();

        // 注册数据库迁移服务
        // 负责数据库初始化、架构更新和种子数据填充
        services.AddScoped<DatabaseMigrationService>();

        #endregion

        #region 业务服务层注册

        // 注册核心业务服务
        services.AddScoped<IBackupLogService, BackupLogService>();           // 备份日志管理服务
        services.AddScoped<BackupReportingService>();                        // 备份报告生成服务
        services.AddScoped<IRetentionPolicyService, RetentionManagementService>(); // 保留策略服务接口
        services.AddScoped<RetentionManagementService>();                    // 保留策略管理服务实现

        #endregion

        #region 网络和通信服务注册

        // 注册网络重试服务
        // 提供网络操作的重试机制和错误恢复
        services.AddScoped<INetworkRetryService, NetworkRetryService>();

        #endregion
        
        #region 告警配置服务注册

        // 注册告警配置为单例服务
        // 告警配置包含邮件、Webhook、文件日志等多种通知方式的设置
        services.AddSingleton<AlertingConfig>(provider =>
        {
            var logger = provider.GetService<ILogger<AlertingConfig>>();
            var alertingConfig = new AlertingConfig();
            
            try
            {
                if (configuration != null)
                {
                    // 尝试从配置文件中读取告警配置
                    var alertingSection = configuration.GetSection("Alerting");
                    if (alertingSection.Exists())
                    {
                        logger?.LogInformation("发现告警配置节，正在尝试绑定配置值");
                        
                        // 将配置节绑定到告警配置对象
                        alertingSection.Bind(alertingConfig);
                        
                        // 验证绑定后的配置值
                        var validationErrors = ValidateAlertingConfig(alertingConfig, logger);
                        
                        if (validationErrors.Any())
                        {
                            logger?.LogWarning("告警配置验证发现 {ErrorCount} 个问题: {Errors}。使用修正后的值。",
                                validationErrors.Count, string.Join("; ", validationErrors));
                        }
                        else
                        {
                            logger?.LogInformation("告警配置成功从配置节绑定并验证");
                        }
                        
                        // 记录最终的配置值以便透明化
                        LogAlertingConfigValues(alertingConfig, logger);
                    }
                    else
                    {
                        logger?.LogInformation("未找到告警配置节，使用默认告警配置值");
                        LogAlertingConfigValues(alertingConfig, logger);
                    }
                }
                else
                {
                    logger?.LogInformation("未提供配置对象，使用默认告警配置值");
                    LogAlertingConfigValues(alertingConfig, logger);
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "从配置节'Alerting'绑定告警配置失败: {ErrorMessage}。" +
                    "使用默认配置值。这可能会影响告警功能。", ex.Message);
                
                // 发生异常时重置为默认配置，防止部分绑定导致的问题
                alertingConfig = new AlertingConfig();
                LogAlertingConfigValues(alertingConfig, logger);
            }
            
            return alertingConfig;
        });

        #endregion
        
        // Add HttpClient for AlertingService with typed client pattern, retry policy, and error handling
        services.AddHttpClient<AlertingService>((serviceProvider, client) =>
        {
            try
            {
                // Get timeout from AlertingConfig or use default
                var alertingConfig = serviceProvider.GetService<AlertingConfig>();
                var logger = serviceProvider.GetService<ILogger<AlertingService>>();
                
                var timeoutSeconds = alertingConfig?.TimeoutSeconds ?? 30;
                var baseUrl = alertingConfig?.BaseUrl;
                
                // Validate and apply timeout
                if (timeoutSeconds <= 0 || timeoutSeconds > 300)
                {
                    logger?.LogWarning("Invalid HttpClient timeout value {InvalidTimeout}s from AlertingConfig, using default: 30s", timeoutSeconds);
                    timeoutSeconds = 30;
                }
                
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                client.DefaultRequestHeaders.Add("User-Agent", "MySqlBackupTool/1.0");
                
                // Apply base URL if configured and valid
                if (!string.IsNullOrEmpty(baseUrl))
                {
                    if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && 
                        (uri.Scheme == "http" || uri.Scheme == "https"))
                    {
                        client.BaseAddress = uri;
                        logger?.LogDebug("HttpClient configured with BaseAddress: {BaseUrl}", baseUrl);
                    }
                    else
                    {
                        logger?.LogWarning("Invalid BaseUrl in AlertingConfig: {InvalidUrl}. BaseAddress not set.", baseUrl);
                    }
                }
                
                // Apply default headers from configuration
                if (alertingConfig?.DefaultHeaders != null)
                {
                    foreach (var header in alertingConfig.DefaultHeaders)
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(header.Key) && !string.IsNullOrEmpty(header.Value))
                            {
                                client.DefaultRequestHeaders.Add(header.Key, header.Value);
                                logger?.LogDebug("Added default header: {HeaderName} = {HeaderValue}", header.Key, header.Value);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger?.LogWarning(ex, "Failed to add default header {HeaderName}: {ErrorMessage}", header.Key, ex.Message);
                        }
                    }
                }
                
                logger?.LogInformation("HttpClient for AlertingService configured successfully: Timeout={Timeout}s, BaseAddress={BaseAddress}",
                    timeoutSeconds, client.BaseAddress?.ToString() ?? "Not set");
            }
            catch (Exception ex)
            {
                var logger = serviceProvider.GetService<ILogger<AlertingService>>();
                logger?.LogError(ex, "Failed to configure HttpClient for AlertingService: {ErrorMessage}. Using basic configuration.", ex.Message);
                
                // Fallback to basic configuration
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("User-Agent", "MySqlBackupTool/1.0");
            }
        })
        .AddPolicyHandler(GetRetryPolicy(configuration))
        .AddPolicyHandler(GetTimeoutPolicy(configuration));
        
        // Register IAlertingService using the HttpClient-configured AlertingService
        services.AddScoped<IAlertingService>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<AlertingService>>();
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(AlertingService));
            var alertingConfig = serviceProvider.GetService<AlertingConfig>();
            
            return new AlertingService(logger, httpClient, alertingConfig);
        });

        // Add authentication services
        services.AddSingleton<CredentialStorageConfig>(provider =>
        {
            var config = new CredentialStorageConfig
            {
                CredentialsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MySqlBackupTool", "credentials.dat"),
                EncryptionKey = "MySqlBackupTool-DefaultKey-2024", // In production, this should be configurable
                UseWindowsDPAPI = true,
                MaxAuthenticationAttempts = 5,
                LockoutDurationMinutes = 15
            };
            
            if (configuration != null)
            {
                var credentialSection = configuration.GetSection("CredentialStorage");
                if (credentialSection.Exists())
                {
                    credentialSection.Bind(config);
                }
            }
            
            // Ensure the credentials directory exists
            var credentialsDir = Path.GetDirectoryName(config.CredentialsFilePath);
            if (!string.IsNullOrEmpty(credentialsDir) && !Directory.Exists(credentialsDir))
            {
                Directory.CreateDirectory(credentialsDir);
            }
            
            return config;
        });
        services.AddScoped<ICredentialStorage, SecureCredentialStorage>();
        services.AddScoped<ISecureCredentialStorage, SecureCredentialStorage>();
        services.AddScoped<ITokenManager, TokenManager>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<IAuthenticationAuditService, AuthenticationAuditService>();

        // Add error handling services
        services.AddErrorHandlingServices();

        // Add logging
        services.AddBackupToolLogging();
        
        // Add application-specific logging service
        services.AddScoped<ILoggingService, LoggingService>();
        
        // Add memory profiler
        services.AddSingleton<MemoryProfilingConfig>();
        services.AddScoped<IMemoryProfiler, MemoryProfiler>();
        
        // Add encryption service
        services.AddScoped<IEncryptionService, EncryptionService>();
        
        // Add compression service (needed by validation service)
        // Use basic compression service in shared services to avoid circular dependency
        services.AddScoped<CompressionService>();
        services.AddScoped<ICompressionService>(provider => provider.GetRequiredService<CompressionService>());
        
        // Add basic MySQL manager (needed by error recovery manager)
        services.AddScoped<MySQLManager>();
        services.AddScoped<IMySQLManager>(provider => provider.GetRequiredService<MySQLManager>());
        
        // Add validation service
        services.AddScoped<IValidationService, ValidationService>();
        
        // Add startup validation service
        services.AddScoped<StartupValidationService>();

        // Add dependency resolution validation service for early error detection
        services.AddSingleton<DependencyResolutionValidator>();

        return services;
    }

    #endregion

    #region 客户端服务注册

    /// <summary>
    /// 向依赖注入容器添加客户端特定的服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="useSecureTransfer">是否使用安全传输（SSL/TLS），默认为true</param>
    /// <returns>配置后的服务集合，支持链式调用</returns>
    /// <remarks>
    /// 该方法注册客户端执行备份操作所需的服务：
    /// 
    /// 1. 核心服务：
    ///    - 校验和服务：验证文件完整性
    ///    - 证书管理器：管理SSL/TLS证书
    /// 
    /// 2. 备份执行服务：
    ///    - MySQL管理器：控制MySQL服务和数据库操作
    ///    - 压缩服务：压缩备份文件
    ///    - 文件传输客户端：传输备份文件到服务器
    /// 
    /// 3. 超时保护服务：
    ///    - 为核心服务添加超时保护装饰器
    ///    - 防止长时间运行的操作阻塞应用程序
    /// 
    /// 4. 高级功能：
    ///    - 备份编排器：协调整个备份流程
    ///    - 后台任务管理器：管理异步备份任务
    /// 
    /// 安全传输选项：
    /// - true：使用SecureFileTransferClient（SSL/TLS加密）
    /// - false：使用AuthenticatedFileTransferClient（仅认证，无加密）
    /// </remarks>
    /// <example>
    /// <code>
    /// // 使用安全传输（推荐用于生产环境）
    /// services.AddClientServices(useSecureTransfer: true);
    /// 
    /// // 使用非安全传输（适用于内网环境）
    /// services.AddClientServices(useSecureTransfer: false);
    /// </code>
    /// </example>
    public static IServiceCollection AddClientServices(this IServiceCollection services, bool useSecureTransfer = true)
    {
        // Add checksum service
        services.AddScoped<IChecksumService, ChecksumService>();
        
        // Add certificate manager
        services.AddScoped<CertificateManager>();
        
        // Configure SSL services
        services.ConfigureSslServices(ssl =>
        {
            ssl.UseSSL = useSecureTransfer;
            ssl.AllowSelfSignedCertificates = true; // For development
            ssl.ValidateServerCertificate = false; // For development
        });
        
        // Add core services (without timeout protection)
        services.AddScoped<MySQLManager>();
        services.AddScoped<CompressionService>();
        services.AddScoped<FileTransferClient>();
        services.AddScoped<SecureFileTransferClient>();
        services.AddScoped<AuthenticatedFileTransferClient>();
        
        // Add timeout-protected services as the primary implementations
        services.AddScoped<IMySQLManager>(provider =>
        {
            var innerManager = provider.GetRequiredService<MySQLManager>();
            var errorRecoveryManager = provider.GetRequiredService<IErrorRecoveryManager>();
            var logger = provider.GetRequiredService<ILogger<TimeoutProtectedMySQLManager>>();
            return new TimeoutProtectedMySQLManager(innerManager, errorRecoveryManager, logger);
        });
        
        // Register IBackupService as an alias for IMySQLManager for test compatibility
        services.AddScoped<IBackupService>(provider =>
        {
            var innerManager = provider.GetRequiredService<MySQLManager>();
            var errorRecoveryManager = provider.GetRequiredService<IErrorRecoveryManager>();
            var logger = provider.GetRequiredService<ILogger<TimeoutProtectedMySQLManager>>();
            return new TimeoutProtectedMySQLManager(innerManager, errorRecoveryManager, logger);
        });
        
        services.AddScoped<ICompressionService>(provider =>
        {
            var innerService = provider.GetRequiredService<CompressionService>();
            var errorRecoveryManager = provider.GetRequiredService<IErrorRecoveryManager>();
            var logger = provider.GetRequiredService<ILogger<TimeoutProtectedCompressionService>>();
            return new TimeoutProtectedCompressionService(innerService, errorRecoveryManager, logger);
        });
        
        // Register the appropriate file transfer client based on security preference
        if (useSecureTransfer)
        {
            services.AddScoped<IFileTransferClient>(provider =>
            {
                var innerClient = provider.GetRequiredService<SecureFileTransferClient>();
                var errorRecoveryManager = provider.GetRequiredService<IErrorRecoveryManager>();
                var logger = provider.GetRequiredService<ILogger<TimeoutProtectedFileTransferClient>>();
                return new TimeoutProtectedFileTransferClient(innerClient, errorRecoveryManager, logger);
            });
            
            // Register IFileTransferService as an alias for IFileTransferClient for consistency
            services.AddScoped<IFileTransferService>(provider =>
            {
                var innerClient = provider.GetRequiredService<SecureFileTransferClient>();
                var errorRecoveryManager = provider.GetRequiredService<IErrorRecoveryManager>();
                var logger = provider.GetRequiredService<ILogger<TimeoutProtectedFileTransferClient>>();
                return new TimeoutProtectedFileTransferClient(innerClient, errorRecoveryManager, logger);
            });
        }
        else
        {
            // Use authenticated file transfer client for non-secure transfers
            services.AddScoped<IFileTransferClient>(provider =>
            {
                var innerClient = provider.GetRequiredService<AuthenticatedFileTransferClient>();
                var errorRecoveryManager = provider.GetRequiredService<IErrorRecoveryManager>();
                var logger = provider.GetRequiredService<ILogger<TimeoutProtectedFileTransferClient>>();
                return new TimeoutProtectedFileTransferClient(innerClient, errorRecoveryManager, logger);
            });
            
            // Register IFileTransferService as an alias for IFileTransferClient for consistency
            services.AddScoped<IFileTransferService>(provider =>
            {
                var innerClient = provider.GetRequiredService<AuthenticatedFileTransferClient>();
                var errorRecoveryManager = provider.GetRequiredService<IErrorRecoveryManager>();
                var logger = provider.GetRequiredService<ILogger<TimeoutProtectedFileTransferClient>>();
                return new TimeoutProtectedFileTransferClient(innerClient, errorRecoveryManager, logger);
            });
        }

        // Add backup orchestrator
        services.AddScoped<IBackupOrchestrator, BackupOrchestrator>();

        // Add background task manager
        services.AddScoped<IBackgroundTaskManager, BackgroundTaskManager>();
        
        return services;
    }

    #endregion

    #region 服务器端服务注册

    /// <summary>
    /// 向依赖注入容器添加服务器端特定的服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="baseStoragePath">基础存储路径，如果为null则使用默认路径</param>
    /// <param name="useSecureReceiver">是否使用安全文件接收器（SSL/TLS），默认为true</param>
    /// <returns>配置后的服务集合，支持链式调用</returns>
    /// <remarks>
    /// 该方法注册服务器端接收和管理备份文件所需的服务：
    /// 
    /// 1. 核心服务：
    ///    - 校验和服务：验证接收文件的完整性
    ///    - 证书管理器：管理SSL/TLS服务器证书
    /// 
    /// 2. 文件接收服务：
    ///    - FileReceiver：基础文件接收功能
    ///    - SecureFileReceiver：支持SSL/TLS的安全文件接收
    /// 
    /// 3. 存储管理服务：
    ///    - 分块管理器：处理大文件的分块传输
    ///    - 存储管理器：管理备份文件的存储和组织
    /// 
    /// 4. 安全配置：
    ///    - SSL/TLS配置：根据useSecureReceiver参数配置安全传输
    ///    - 开发证书：为测试环境提供自签名证书
    /// 
    /// 存储路径配置：
    /// - 如果baseStoragePath为null，使用空字符串作为默认值
    /// - 存储管理器会根据路径创建必要的目录结构
    /// 
    /// 安全接收器选项：
    /// - true：使用SecureFileReceiver（SSL/TLS加密，推荐用于生产环境）
    /// - false：使用FileReceiver（无加密，适用于内网环境）
    /// </remarks>
    /// <example>
    /// <code>
    /// // 生产环境配置（安全传输 + 自定义存储路径）
    /// services.AddServerServices("/var/backups/mysql", useSecureReceiver: true);
    /// 
    /// // 开发环境配置（无加密 + 默认路径）
    /// services.AddServerServices(useSecureReceiver: false);
    /// 
    /// // 使用默认配置
    /// services.AddServerServices();
    /// </code>
    /// </example>
    public static IServiceCollection AddServerServices(this IServiceCollection services, string? baseStoragePath = null, bool useSecureReceiver = true)
    {
        // Add checksum service
        services.AddScoped<IChecksumService, ChecksumService>();
        
        // Add certificate manager
        services.AddScoped<CertificateManager>();
        
        // Configure SSL services
        services.ConfigureSslServices(ssl =>
        {
            ssl.UseSSL = useSecureReceiver;
            ssl.AllowSelfSignedCertificates = true; // For development
            ssl.ValidateServerCertificate = false; // For development
        });
        
        // Add development certificate for testing
        if (useSecureReceiver)
        {
            services.AddDevelopmentCertificate();
        }
        
        // Add file receiver services
        services.AddScoped<FileReceiver>();
        services.AddScoped<SecureFileReceiver>();
        
        // Register the appropriate file receiver based on security preference
        if (useSecureReceiver)
        {
            services.AddScoped<IFileReceiver>(provider => provider.GetRequiredService<SecureFileReceiver>());
        }
        else
        {
            services.AddScoped<IFileReceiver>(provider => provider.GetRequiredService<FileReceiver>());
        }
        
        // Add chunk manager
        services.AddScoped<IChunkManager, ChunkManager>();
        
        // Add storage manager
        services.AddScoped<IStorageManager>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<StorageManager>>();
            return new StorageManager(logger, baseStoragePath ?? "");
        });
        
        return services;
    }

    #endregion

    #region 备份调度服务注册

    /// <summary>
    /// 向依赖注入容器添加备份调度相关的服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>配置后的服务集合，支持链式调用</returns>
    /// <remarks>
    /// 该方法注册以下调度相关服务：
    /// 
    /// 1. 备份调度器服务：
    ///    - IBackupScheduler接口和BackupSchedulerService实现
    ///    - 负责根据配置的时间表自动执行备份操作
    /// 
    /// 2. 服务检查器：
    ///    - IServiceChecker接口和ServiceChecker实现
    ///    - 监控系统服务状态，确保备份环境正常
    /// 
    /// 3. 托管服务（后台服务）：
    ///    - BackupSchedulerService：作为后台服务运行，持续监控和执行调度任务
    ///    - AutoStartupService：应用程序启动时的自动化服务
    /// 
    /// 调度功能特性：
    /// - 支持多种调度类型（每日、每周、每月等）
    /// - 自动处理调度冲突和重叠
    /// - 提供调度状态监控和日志记录
    /// - 支持手动触发和暂停调度
    /// 
    /// 后台服务说明：
    /// - 这些服务在应用程序启动时自动开始运行
    /// - 在应用程序关闭时自动停止
    /// - 提供优雅的启动和关闭处理
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddBackupSchedulingServices();
    /// 
    /// // 在应用程序中使用调度器
    /// var scheduler = serviceProvider.GetRequiredService&lt;IBackupScheduler&gt;();
    /// await scheduler.ScheduleBackupAsync(configId, scheduleType, scheduleTime);
    /// </code>
    /// </example>
    public static IServiceCollection AddBackupSchedulingServices(this IServiceCollection services)
    {
        #region 调度服务注册

        // 注册备份调度器服务
        // 提供备份任务的调度和执行功能
        services.AddScoped<IBackupScheduler, BackupSchedulerService>();

        // 注册服务检查器
        // 监控系统服务状态，确保备份环境健康
        services.AddSingleton<IServiceChecker, ServiceChecker>();

        #endregion

        #region 托管服务注册

        // 将调度器注册为托管服务，在后台持续运行
        // 这样调度器可以在应用程序生命周期内持续工作
        services.AddHostedService<BackupSchedulerService>();
        
        // 注册自动启动服务
        // 处理应用程序启动时的初始化任务
        services.AddHostedService<AutoStartupService>();

        #endregion
        
        return services;
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 创建默认的SQLite数据库连接字符串
    /// </summary>
    /// <param name="databasePath">数据库文件路径，默认为"backup_tool.db"</param>
    /// <returns>格式化的SQLite连接字符串</returns>
    /// <remarks>
    /// 该方法生成标准的SQLite连接字符串格式：Data Source={完整路径}
    /// 
    /// 功能特性：
    /// - 自动将相对路径转换为绝对路径
    /// - 确保路径格式正确
    /// - 支持自定义数据库文件名
    /// 
    /// 使用场景：
    /// - 应用程序启动时配置数据库连接
    /// - 测试环境中创建临时数据库
    /// - 不同环境使用不同的数据库文件
    /// </remarks>
    /// <example>
    /// <code>
    /// // 使用默认数据库文件名
    /// var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString();
    /// // 结果: "Data Source=C:\App\backup_tool.db"
    /// 
    /// // 使用自定义数据库文件名
    /// var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString("my_backup.db");
    /// // 结果: "Data Source=C:\App\my_backup.db"
    /// 
    /// // 使用完整路径
    /// var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString(@"C:\Data\backup.db");
    /// // 结果: "Data Source=C:\Data\backup.db"
    /// </code>
    /// </example>
    public static string CreateDefaultConnectionString(string databasePath = "backup_tool.db")
    {
        // 将路径转换为绝对路径，确保路径格式正确
        var fullPath = Path.GetFullPath(databasePath);
        
        // 返回SQLite标准连接字符串格式
        return $"Data Source={fullPath}";
    }

    /// <summary>
    /// 确保数据库正确初始化
    /// </summary>
    /// <param name="serviceProvider">服务提供者实例</param>
    /// <returns>异步任务</returns>
    /// <remarks>
    /// 该扩展方法简化了数据库初始化过程：
    /// 
    /// 执行步骤：
    /// 1. 创建服务作用域
    /// 2. 获取数据库迁移服务
    /// 3. 执行数据库初始化操作
    /// 4. 自动释放资源
    /// 
    /// 初始化内容：
    /// - 创建数据库文件（如果不存在）
    /// - 应用数据库架构迁移
    /// - 填充默认种子数据
    /// - 验证数据库完整性
    /// 
    /// 使用时机：
    /// - 应用程序启动时
    /// - 数据库升级后
    /// - 测试环境准备时
    /// </remarks>
    /// <exception cref="Exception">当数据库初始化失败时抛出</exception>
    /// <example>
    /// <code>
    /// var serviceProvider = services.BuildServiceProvider();
    /// 
    /// // 初始化数据库
    /// await serviceProvider.InitializeDatabaseAsync();
    /// 
    /// // 现在可以安全地使用数据库服务
    /// var dbContext = serviceProvider.GetRequiredService&lt;BackupDbContext&gt;();
    /// </code>
    /// </example>
    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
    {
        // 创建服务作用域，确保正确的服务生命周期管理
        using var scope = serviceProvider.CreateScope();
        
        // 获取数据库迁移服务实例
        var migrationService = scope.ServiceProvider.GetRequiredService<DatabaseMigrationService>();
        
        // 执行数据库初始化
        await migrationService.InitializeDatabaseAsync();
    }

    #endregion

    /// <summary>
    /// Validates all required services can be resolved during startup and logs configuration status
    /// </summary>
    public static async Task<StartupValidationResult> ValidateServicesAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var validationService = scope.ServiceProvider.GetRequiredService<StartupValidationService>();
        
        return await validationService.ValidateServicesAsync();
    }

    /// <summary>
    /// Validates critical service dependencies and provides detailed error information for failures
    /// </summary>
    public static DependencyValidationResult ValidateCriticalDependencies(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<DependencyResolutionValidator>();
        
        return validator.ValidateCriticalServices(serviceProvider);
    }

    /// <summary>
    /// Validates critical dependencies and throws a detailed exception if validation fails
    /// </summary>
    public static void ValidateCriticalDependenciesOrThrow(this IServiceProvider serviceProvider)
    {
        var result = serviceProvider.ValidateCriticalDependencies();
        
        if (!result.IsValid)
        {
            var errorMessage = BuildCriticalDependencyErrorMessage(result);
            throw new InvalidOperationException(errorMessage);
        }
    }

    /// <summary>
    /// Builds a comprehensive error message for critical dependency validation failures
    /// </summary>
    private static string BuildCriticalDependencyErrorMessage(DependencyValidationResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Critical dependency validation failed. {result.FailedServices.Count} critical services could not be resolved:");
        sb.AppendLine();

        foreach (var failedService in result.FailedServices.Values)
        {
            sb.AppendLine($"❌ {failedService.ServiceName}:");
            sb.AppendLine($"   Type: {failedService.ServiceType.Name}");
            
            if (failedService.DependencyChain.Any())
            {
                sb.AppendLine($"   Dependency Chain: {string.Join(" → ", failedService.DependencyChain)}");
            }
            
            if (failedService.ConstructorAnalysis != null && failedService.ConstructorAnalysis.RequiredDependencies.Any())
            {
                var requiredDeps = failedService.ConstructorAnalysis.RequiredDependencies
                    .Where(d => !d.IsOptional)
                    .Select(d => d.TypeName);
                sb.AppendLine($"   Required Dependencies: {string.Join(", ", requiredDeps)}");
            }
            
            sb.AppendLine($"   Resolution Guidance: {failedService.ResolutionGuidance}");
            sb.AppendLine($"   Error: {failedService.ErrorSummary}");
            sb.AppendLine();
        }

        if (result.ValidServices.Any())
        {
            sb.AppendLine($"✅ Successfully resolved services ({result.ValidServices.Count}): {string.Join(", ", result.ValidServices)}");
            sb.AppendLine();
        }

        sb.AppendLine("Please check the service registration in AddSharedServices, AddClientServices, or AddServerServices methods.");
        sb.AppendLine("Ensure all required dependencies are registered before the services that depend on them.");

        return sb.ToString();
    }
    public static async Task ValidateServicesOrThrowAsync(this IServiceProvider serviceProvider)
    {
        var result = await serviceProvider.ValidateServicesAsync();
        
        if (!result.IsValid)
        {
            var errorMessage = BuildDetailedValidationErrorMessage(result);
            
            if (result.ValidationException != null)
            {
                throw new InvalidOperationException(errorMessage, result.ValidationException);
            }
            else
            {
                throw new InvalidOperationException(errorMessage);
            }
        }
    }

    /// <summary>
    /// Builds a detailed error message for service validation failures with dependency chain information
    /// </summary>
    private static string BuildDetailedValidationErrorMessage(StartupValidationResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Service validation failed. {result.FailedServices.Count} services failed validation out of {result.TotalServicesChecked} total services:");
        sb.AppendLine();

        foreach (var failedService in result.FailedServices)
        {
            sb.AppendLine($"❌ {failedService.Key}:");
            
            // Parse the error message to provide more context
            var errorMessage = failedService.Value;
            var dependencyChain = ExtractDependencyChainFromError(errorMessage);
            
            if (dependencyChain.Any())
            {
                sb.AppendLine($"   Error: {errorMessage}");
                sb.AppendLine($"   Dependency Chain: {string.Join(" → ", dependencyChain)}");
                
                // Provide specific guidance based on common dependency issues
                var guidance = GetDependencyResolutionGuidance(failedService.Key, errorMessage);
                if (!string.IsNullOrEmpty(guidance))
                {
                    sb.AppendLine($"   Guidance: {guidance}");
                }
            }
            else
            {
                sb.AppendLine($"   Error: {errorMessage}");
            }
            sb.AppendLine();
        }

        if (result.ValidatedServices.Any())
        {
            sb.AppendLine($"✅ Successfully validated services ({result.ValidatedServices.Count}):");
            foreach (var validatedService in result.ValidatedServices.Take(5)) // Show first 5 to avoid clutter
            {
                sb.AppendLine($"   • {validatedService.Key}");
            }
            if (result.ValidatedServices.Count > 5)
            {
                sb.AppendLine($"   • ... and {result.ValidatedServices.Count - 5} more");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"Validation completed in {result.ValidationDuration.TotalMilliseconds:F0}ms");
        sb.AppendLine("Please check the service registration in AddSharedServices, AddClientServices, or AddServerServices methods.");

        return sb.ToString();
    }

    /// <summary>
    /// Extracts dependency chain information from error messages
    /// </summary>
    private static List<string> ExtractDependencyChainFromError(string errorMessage)
    {
        var dependencyChain = new List<string>();
        
        // Common patterns in dependency injection error messages
        var patterns = new[]
        {
            // Pattern: "Unable to resolve service for type 'TypeA' while attempting to activate 'TypeB'"
            @"Unable to resolve service for type '([^']+)' while attempting to activate '([^']+)'",
            // Pattern: "A suitable constructor for type 'TypeA' could not be located"
            @"A suitable constructor for type '([^']+)' could not be located",
            // Pattern: "No service for type 'TypeA' has been registered"
            @"No service for type '([^']+)' has been registered"
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(errorMessage, pattern);
            if (match.Success)
            {
                if (match.Groups.Count > 2)
                {
                    // Pattern with dependency chain (TypeA → TypeB)
                    dependencyChain.Add(match.Groups[1].Value);
                    dependencyChain.Add(match.Groups[2].Value);
                }
                else if (match.Groups.Count > 1)
                {
                    // Pattern with single type
                    dependencyChain.Add(match.Groups[1].Value);
                }
                break;
            }
        }

        // Extract additional context from stack trace-like information
        if (errorMessage.Contains("→"))
        {
            var parts = errorMessage.Split('→').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p));
            dependencyChain.AddRange(parts);
        }

        return dependencyChain.Distinct().ToList();
    }

    /// <summary>
    /// Provides specific guidance for common dependency resolution issues
    /// </summary>
    private static string GetDependencyResolutionGuidance(string serviceName, string errorMessage)
    {
        // HttpClient related issues
        if (serviceName.Contains("HttpClient") || errorMessage.Contains("HttpClient"))
        {
            return "Ensure HttpClient is registered using services.AddHttpClient<T>() or services.AddHttpClient() in the service registration.";
        }

        // AlertingService related issues
        if (serviceName.Contains("AlertingService"))
        {
            if (errorMessage.Contains("HttpClient"))
            {
                return "AlertingService requires HttpClient. Ensure HttpClient is registered using services.AddHttpClient<AlertingService>().";
            }
            if (errorMessage.Contains("AlertingConfig"))
            {
                return "AlertingService requires AlertingConfig. Ensure AlertingConfig is registered as a singleton in the service registration.";
            }
        }

        // Configuration related issues
        if (serviceName.Contains("Config") || errorMessage.Contains("Config"))
        {
            return "Configuration objects should be registered as singletons. Check if the configuration section exists in appsettings.json.";
        }

        // Repository related issues
        if (serviceName.Contains("Repository"))
        {
            return "Repository services require DbContext. Ensure Entity Framework is properly configured with AddDbContext<T>().";
        }

        // Logger related issues
        if (serviceName.Contains("ILogger") || errorMessage.Contains("ILogger"))
        {
            return "Logging services should be automatically available. Ensure services.AddLogging() is called during service registration.";
        }

        // Generic guidance for missing services
        if (errorMessage.Contains("No service for type") || errorMessage.Contains("has been registered"))
        {
            return "The required service is not registered in the DI container. Add the appropriate service registration in AddSharedServices, AddClientServices, or AddServerServices.";
        }

        // Constructor issues
        if (errorMessage.Contains("suitable constructor"))
        {
            return "The service constructor has dependencies that cannot be resolved. Check that all constructor parameters have corresponding service registrations.";
        }

        return string.Empty;
    }

    /// <summary>
    /// Adds error handling and recovery services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddErrorHandlingServices(this IServiceCollection services, ErrorRecoveryConfig? config = null)
    {
        // Register error recovery configuration
        if (config != null)
        {
            services.AddSingleton(config);
        }
        else
        {
            services.AddSingleton(new ErrorRecoveryConfig());
        }

        // Register error recovery manager
        services.AddScoped<IErrorRecoveryManager, ErrorRecoveryManager>();

        return services;
    }

    /// <summary>
    /// Adds retention policy background service for automatic cleanup
    /// </summary>
    public static IServiceCollection AddRetentionPolicyBackgroundService(
        this IServiceCollection services, 
        Action<RetentionPolicyBackgroundServiceOptions>? configureOptions = null)
    {
        var options = new RetentionPolicyBackgroundServiceOptions();
        configureOptions?.Invoke(options);

        if (options.IsEnabled)
        {
            services.AddSingleton(options);
            services.AddHostedService<RetentionPolicyBackgroundService>();
        }

        return services;
    }

    /// <summary>
    /// Adds retention policy background service with specific interval
    /// </summary>
    public static IServiceCollection AddRetentionPolicyBackgroundService(
        this IServiceCollection services, 
        TimeSpan executionInterval)
    {
        return services.AddRetentionPolicyBackgroundService(options =>
        {
            options.ExecutionInterval = executionInterval;
            options.IsEnabled = true;
        });
    }

    /// <summary>
    /// Adds error handling services with custom configuration
    /// </summary>
    public static IServiceCollection AddErrorHandlingServices(this IServiceCollection services, Action<ErrorRecoveryConfig> configureOptions)
    {
        var config = new ErrorRecoveryConfig();
        configureOptions(config);
        
        return services.AddErrorHandlingServices(config);
    }

    /// <summary>
    /// Configures SSL/TLS services with certificate management
    /// </summary>
    public static IServiceCollection ConfigureSslServices(this IServiceCollection services, Action<SslConfiguration>? configureSsl = null)
    {
        var sslConfig = new SslConfiguration();
        configureSsl?.Invoke(sslConfig);

        services.AddSingleton(sslConfig);
        
        return services;
    }

    /// <summary>
    /// Creates a self-signed certificate for development/testing purposes
    /// </summary>
    public static IServiceCollection AddDevelopmentCertificate(this IServiceCollection services, string subjectName = "localhost", int validityDays = 365)
    {
        services.AddSingleton(provider =>
        {
            var certificateManager = provider.GetRequiredService<CertificateManager>();
            return certificateManager.CreateSelfSignedCertificate(subjectName, TimeSpan.FromDays(validityDays));
        });

        return services;
    }

    /// <summary>
    /// Creates a retry policy for HTTP operations with exponential backoff and jitter
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(IConfiguration? configuration)
    {
        // Get retry configuration from AlertingConfig or use defaults
        var maxRetryAttempts = 3;
        var configurationSource = "default";
        
        try
        {
            if (configuration != null)
            {
                var alertingSection = configuration.GetSection("Alerting");
                if (alertingSection.Exists())
                {
                    var configuredAttempts = alertingSection.GetValue<int?>("MaxRetryAttempts");
                    if (configuredAttempts.HasValue)
                    {
                        if (configuredAttempts.Value >= 0 && configuredAttempts.Value <= 10)
                        {
                            maxRetryAttempts = configuredAttempts.Value;
                            configurationSource = "configuration";
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Invalid MaxRetryAttempts value {configuredAttempts.Value} in configuration. Must be between 0 and 10. Using default: {maxRetryAttempts}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to read MaxRetryAttempts from configuration: {ex.Message}. Using default: {maxRetryAttempts}");
        }

        Console.WriteLine($"HTTP retry policy configured with {maxRetryAttempts} max attempts (source: {configurationSource})");

        return HttpPolicyExtensions
            .HandleTransientHttpError() // Handles HttpRequestException and 5XX, 408 status codes
            .OrResult(msg => !msg.IsSuccessStatusCode && (int)msg.StatusCode >= 500) // Additional server errors
            .WaitAndRetryAsync(
                retryCount: maxRetryAttempts,
                sleepDurationProvider: retryAttempt =>
                {
                    // Exponential backoff: 2^attempt seconds + jitter
                    var exponentialDelay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                    var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));
                    return exponentialDelay + jitter;
                },
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // Log retry attempts using structured logging
                    // The HttpClient factory will provide logging through the DI container
                    var operationKey = "HttpClientRetry";
                    context[operationKey] = $"HTTP retry attempt {retryCount}/{maxRetryAttempts} after {timespan.TotalMilliseconds:F0}ms delay";
                    
                    // Also log to console for immediate visibility during testing
                    Console.WriteLine($"HTTP retry attempt {retryCount}/{maxRetryAttempts} after {timespan.TotalMilliseconds:F0}ms delay");
                });
    }

    /// <summary>
    /// Creates a timeout policy for HTTP operations using configuration values
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(IConfiguration? configuration)
    {
        // Get timeout configuration from AlertingConfig or use default
        var timeoutSeconds = 10; // Default per-request timeout
        var configurationSource = "default";
        
        try
        {
            if (configuration != null)
            {
                var alertingSection = configuration.GetSection("Alerting");
                if (alertingSection.Exists())
                {
                    var configuredTimeout = alertingSection.GetValue<int?>("TimeoutSeconds");
                    if (configuredTimeout.HasValue)
                    {
                        if (configuredTimeout.Value > 0 && configuredTimeout.Value <= 300)
                        {
                            // Use a shorter per-request timeout than the overall HttpClient timeout
                            // This allows for multiple retry attempts within the overall timeout window
                            timeoutSeconds = Math.Max(5, configuredTimeout.Value / 3); // Use 1/3 of configured timeout, minimum 5 seconds
                            configurationSource = "configuration";
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Invalid TimeoutSeconds value {configuredTimeout.Value} in configuration. Must be between 1 and 300. Using default calculation: {timeoutSeconds}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to read TimeoutSeconds from configuration: {ex.Message}. Using default: {timeoutSeconds}");
        }

        Console.WriteLine($"HTTP timeout policy configured with {timeoutSeconds}s per-request timeout (source: {configurationSource})");

        return Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(timeoutSeconds)); // Per-request timeout
    }

    /// <summary>
    /// Validates AlertingConfig values and corrects invalid ones, returning a list of validation errors
    /// </summary>
    private static List<string> ValidateAlertingConfig(AlertingConfig config, ILogger? logger)
    {
        var errors = new List<string>();

        // Validate TimeoutSeconds
        if (config.TimeoutSeconds <= 0)
        {
            errors.Add($"TimeoutSeconds must be positive, found: {config.TimeoutSeconds}");
            config.TimeoutSeconds = 30; // Reset to default
            logger?.LogWarning("Invalid TimeoutSeconds value {InvalidValue}, reset to default: {DefaultValue}",
                config.TimeoutSeconds, 30);
        }
        else if (config.TimeoutSeconds > 300)
        {
            errors.Add($"TimeoutSeconds should not exceed 300 seconds, found: {config.TimeoutSeconds}");
            config.TimeoutSeconds = 300; // Cap at maximum
            logger?.LogWarning("TimeoutSeconds value {InvalidValue} exceeds maximum, capped to: {MaxValue}",
                config.TimeoutSeconds, 300);
        }

        // Validate MaxRetryAttempts
        if (config.MaxRetryAttempts < 0)
        {
            errors.Add($"MaxRetryAttempts cannot be negative, found: {config.MaxRetryAttempts}");
            config.MaxRetryAttempts = 3; // Reset to default
            logger?.LogWarning("Invalid MaxRetryAttempts value {InvalidValue}, reset to default: {DefaultValue}",
                config.MaxRetryAttempts, 3);
        }
        else if (config.MaxRetryAttempts > 10)
        {
            errors.Add($"MaxRetryAttempts should not exceed 10, found: {config.MaxRetryAttempts}");
            config.MaxRetryAttempts = 10; // Cap at maximum
            logger?.LogWarning("MaxRetryAttempts value {InvalidValue} exceeds maximum, capped to: {MaxValue}",
                config.MaxRetryAttempts, 10);
        }

        // Validate MaxAlertsPerHour
        if (config.MaxAlertsPerHour <= 0)
        {
            errors.Add($"MaxAlertsPerHour must be positive, found: {config.MaxAlertsPerHour}");
            config.MaxAlertsPerHour = 50; // Reset to default
            logger?.LogWarning("Invalid MaxAlertsPerHour value {InvalidValue}, reset to default: {DefaultValue}",
                config.MaxAlertsPerHour, 50);
        }
        else if (config.MaxAlertsPerHour > 1000)
        {
            errors.Add($"MaxAlertsPerHour should not exceed 1000, found: {config.MaxAlertsPerHour}");
            config.MaxAlertsPerHour = 1000; // Cap at maximum
            logger?.LogWarning("MaxAlertsPerHour value {InvalidValue} exceeds maximum, capped to: {MaxValue}",
                config.MaxAlertsPerHour, 1000);
        }

        // Validate BaseUrl if provided
        if (!string.IsNullOrEmpty(config.BaseUrl))
        {
            if (!Uri.TryCreate(config.BaseUrl, UriKind.Absolute, out var uri) || 
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                errors.Add($"BaseUrl must be a valid HTTP/HTTPS URL, found: {config.BaseUrl}");
                config.BaseUrl = null; // Clear invalid URL
                logger?.LogWarning("Invalid BaseUrl value {InvalidValue}, cleared to null", config.BaseUrl);
            }
        }

        // Validate NotificationTimeout
        if (config.NotificationTimeout <= TimeSpan.Zero)
        {
            errors.Add($"NotificationTimeout must be positive, found: {config.NotificationTimeout}");
            config.NotificationTimeout = TimeSpan.FromSeconds(30); // Reset to default
            logger?.LogWarning("Invalid NotificationTimeout value {InvalidValue}, reset to default: {DefaultValue}",
                config.NotificationTimeout, TimeSpan.FromSeconds(30));
        }
        else if (config.NotificationTimeout > TimeSpan.FromMinutes(10))
        {
            errors.Add($"NotificationTimeout should not exceed 10 minutes, found: {config.NotificationTimeout}");
            config.NotificationTimeout = TimeSpan.FromMinutes(10); // Cap at maximum
            logger?.LogWarning("NotificationTimeout value {InvalidValue} exceeds maximum, capped to: {MaxValue}",
                config.NotificationTimeout, TimeSpan.FromMinutes(10));
        }

        // Validate Email configuration if enabled
        if (config.Email.Enabled)
        {
            var emailErrors = ValidateEmailConfig(config.Email, logger);
            errors.AddRange(emailErrors);
        }

        // Validate Webhook configuration if enabled
        if (config.Webhook.Enabled)
        {
            var webhookErrors = ValidateWebhookConfig(config.Webhook, logger);
            errors.AddRange(webhookErrors);
        }

        // Validate FileLog configuration if enabled
        if (config.FileLog.Enabled)
        {
            var fileLogErrors = ValidateFileLogConfig(config.FileLog, logger);
            errors.AddRange(fileLogErrors);
        }

        return errors;
    }

    /// <summary>
    /// Validates EmailConfig and corrects invalid values
    /// </summary>
    private static List<string> ValidateEmailConfig(EmailConfig emailConfig, ILogger? logger)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(emailConfig.SmtpServer))
        {
            errors.Add("Email.SmtpServer is required when email notifications are enabled");
            logger?.LogWarning("Email notifications may not work properly: SmtpServer is not configured");
        }

        if (emailConfig.SmtpPort <= 0 || emailConfig.SmtpPort > 65535)
        {
            errors.Add($"Email.SmtpPort must be between 1 and 65535, found: {emailConfig.SmtpPort}");
            emailConfig.SmtpPort = 587; // Reset to default
            logger?.LogWarning("Invalid Email.SmtpPort value {InvalidValue}, reset to default: {DefaultValue}",
                emailConfig.SmtpPort, 587);
        }

        if (string.IsNullOrWhiteSpace(emailConfig.FromAddress))
        {
            errors.Add("Email.FromAddress is required when email notifications are enabled");
            logger?.LogWarning("Email notifications may not work properly: FromAddress is not configured");
        }
        else if (!IsValidEmail(emailConfig.FromAddress))
        {
            errors.Add($"Email.FromAddress is not a valid email address: {emailConfig.FromAddress}");
            logger?.LogWarning("Email notifications may not work properly: FromAddress is not a valid email address: {InvalidEmail}",
                emailConfig.FromAddress);
        }

        if (!emailConfig.Recipients.Any())
        {
            errors.Add("Email.Recipients list is empty when email notifications are enabled");
            logger?.LogWarning("Email notifications may not work properly: Recipients list is empty");
        }
        else
        {
            var invalidRecipients = emailConfig.Recipients.Where(r => !IsValidEmail(r)).ToList();
            if (invalidRecipients.Any())
            {
                errors.Add($"Email.Recipients contains invalid email addresses: {string.Join(", ", invalidRecipients)}");
                // Remove invalid recipients
                foreach (var invalid in invalidRecipients)
                {
                    emailConfig.Recipients.Remove(invalid);
                }
                logger?.LogWarning("Removed invalid email recipients: {InvalidRecipients}",
                    string.Join(", ", invalidRecipients));
                
                if (!emailConfig.Recipients.Any())
                {
                    logger?.LogWarning("Email notifications may not work properly: No valid recipients remaining after removing invalid ones");
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Validates WebhookConfig and corrects invalid values
    /// </summary>
    private static List<string> ValidateWebhookConfig(WebhookConfig webhookConfig, ILogger? logger)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(webhookConfig.Url))
        {
            errors.Add("Webhook.Url is required when webhook notifications are enabled");
            logger?.LogWarning("Webhook notifications may not work properly: Url is not configured");
        }
        else if (!Uri.TryCreate(webhookConfig.Url, UriKind.Absolute, out var uri) || 
                 (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            errors.Add($"Webhook.Url must be a valid HTTP/HTTPS URL, found: {webhookConfig.Url}");
            logger?.LogWarning("Webhook notifications may not work properly: Invalid Url: {InvalidUrl}",
                webhookConfig.Url);
        }

        var validMethods = new[] { "POST", "PUT", "PATCH" };
        if (!validMethods.Contains(webhookConfig.HttpMethod.ToUpperInvariant()))
        {
            errors.Add($"Webhook.HttpMethod must be one of {string.Join(", ", validMethods)}, found: {webhookConfig.HttpMethod}");
            webhookConfig.HttpMethod = "POST"; // Reset to default
            logger?.LogWarning("Invalid Webhook.HttpMethod value {InvalidValue}, reset to default: {DefaultValue}",
                webhookConfig.HttpMethod, "POST");
        }

        if (string.IsNullOrWhiteSpace(webhookConfig.ContentType))
        {
            errors.Add("Webhook.ContentType cannot be empty");
            webhookConfig.ContentType = "application/json"; // Reset to default
            logger?.LogWarning("Empty Webhook.ContentType, reset to default: {DefaultValue}", "application/json");
        }

        return errors;
    }

    /// <summary>
    /// Validates FileLogConfig and corrects invalid values
    /// </summary>
    private static List<string> ValidateFileLogConfig(FileLogConfig fileLogConfig, ILogger? logger)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(fileLogConfig.LogDirectory))
        {
            errors.Add("FileLog.LogDirectory cannot be empty");
            fileLogConfig.LogDirectory = "logs/alerts"; // Reset to default
            logger?.LogWarning("Empty FileLog.LogDirectory, reset to default: {DefaultValue}", "logs/alerts");
        }

        if (string.IsNullOrWhiteSpace(fileLogConfig.FileNamePattern))
        {
            errors.Add("FileLog.FileNamePattern cannot be empty");
            fileLogConfig.FileNamePattern = "alerts_{yyyy-MM-dd}.log"; // Reset to default
            logger?.LogWarning("Empty FileLog.FileNamePattern, reset to default: {DefaultValue}",
                "alerts_{yyyy-MM-dd}.log");
        }

        if (fileLogConfig.MaxFileSizeMB <= 0)
        {
            errors.Add($"FileLog.MaxFileSizeMB must be positive, found: {fileLogConfig.MaxFileSizeMB}");
            fileLogConfig.MaxFileSizeMB = 10; // Reset to default
            logger?.LogWarning("Invalid FileLog.MaxFileSizeMB value {InvalidValue}, reset to default: {DefaultValue}",
                fileLogConfig.MaxFileSizeMB, 10);
        }

        if (fileLogConfig.MaxFileCount <= 0)
        {
            errors.Add($"FileLog.MaxFileCount must be positive, found: {fileLogConfig.MaxFileCount}");
            fileLogConfig.MaxFileCount = 30; // Reset to default
            logger?.LogWarning("Invalid FileLog.MaxFileCount value {InvalidValue}, reset to default: {DefaultValue}",
                fileLogConfig.MaxFileCount, 30);
        }

        return errors;
    }

    /// <summary>
    /// Logs the final AlertingConfig values for transparency and debugging
    /// </summary>
    private static void LogAlertingConfigValues(AlertingConfig config, ILogger? logger)
    {
        if (logger == null) return;

        logger.LogInformation("AlertingConfig values: " +
            "EnableAlerting={EnableAlerting}, " +
            "TimeoutSeconds={TimeoutSeconds}, " +
            "MaxRetryAttempts={MaxRetryAttempts}, " +
            "MaxAlertsPerHour={MaxAlertsPerHour}, " +
            "BaseUrl={BaseUrl}, " +
            "NotificationTimeout={NotificationTimeout}ms, " +
            "MinimumSeverity={MinimumSeverity}",
            config.EnableAlerting,
            config.TimeoutSeconds,
            config.MaxRetryAttempts,
            config.MaxAlertsPerHour,
            config.BaseUrl ?? "Not configured",
            config.NotificationTimeout.TotalMilliseconds,
            config.MinimumSeverity);

        logger.LogInformation("AlertingConfig channel status: " +
            "Email={EmailEnabled}, " +
            "Webhook={WebhookEnabled}, " +
            "FileLog={FileLogEnabled}",
            config.Email.Enabled,
            config.Webhook.Enabled,
            config.FileLog.Enabled);

        if (config.Email.Enabled)
        {
            logger.LogInformation("Email configuration: " +
                "SmtpServer={SmtpServer}, " +
                "SmtpPort={SmtpPort}, " +
                "UseSsl={UseSsl}, " +
                "FromAddress={FromAddress}, " +
                "Recipients={RecipientCount}",
                config.Email.SmtpServer,
                config.Email.SmtpPort,
                config.Email.UseSsl,
                config.Email.FromAddress,
                config.Email.Recipients.Count);
        }

        if (config.Webhook.Enabled)
        {
            logger.LogInformation("Webhook configuration: " +
                "Url={Url}, " +
                "HttpMethod={HttpMethod}, " +
                "ContentType={ContentType}, " +
                "HasAuthToken={HasAuthToken}",
                config.Webhook.Url,
                config.Webhook.HttpMethod,
                config.Webhook.ContentType,
                !string.IsNullOrEmpty(config.Webhook.AuthToken));
        }

        if (config.FileLog.Enabled)
        {
            logger.LogInformation("FileLog configuration: " +
                "LogDirectory={LogDirectory}, " +
                "FileNamePattern={FileNamePattern}, " +
                "MaxFileSizeMB={MaxFileSizeMB}, " +
                "MaxFileCount={MaxFileCount}",
                config.FileLog.LogDirectory,
                config.FileLog.FileNamePattern,
                config.FileLog.MaxFileSizeMB,
                config.FileLog.MaxFileCount);
        }
    }

    /// <summary>
    /// Validates if a string is a valid email address
    /// </summary>
    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}