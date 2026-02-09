using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Data.Migrations;

/// <summary>
/// 数据库迁移和初始化服务
/// 负责管理数据库的创建、迁移、初始化和维护操作
/// </summary>
/// <remarks>
/// 该服务提供以下功能：
/// 1. 数据库初始化和架构迁移
/// 2. 默认数据种子填充
/// 3. 数据库连接性检查
/// 4. 数据库备份和恢复
/// 5. 数据库状态信息获取
/// 
/// 主要用于应用程序启动时确保数据库环境正确配置
/// </remarks>
public class DatabaseMigrationService
{
    #region 私有字段

    /// <summary>
    /// 数据库上下文实例，用于执行数据库操作
    /// </summary>
    private readonly BackupDbContext _context;

    /// <summary>
    /// 日志记录器，用于记录迁移过程中的信息和错误
    /// </summary>
    private readonly ILogger<DatabaseMigrationService> _logger;

    /// <summary>
    /// 凭据存储服务，用于管理客户端认证凭据
    /// </summary>
    private readonly ICredentialStorage _credentialStorage;

    /// <summary>
    /// 数据库初始化配置选项
    /// </summary>
    private readonly DatabaseInitializationOptions _initOptions;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化DatabaseMigrationService类的新实例
    /// </summary>
    /// <param name="context">数据库上下文实例</param>
    /// <param name="logger">日志记录器实例</param>
    /// <param name="credentialStorage">凭据存储服务实例</param>
    /// <param name="initOptions">数据库初始化配置选项</param>
    /// <exception cref="ArgumentNullException">当任何参数为null时抛出</exception>
    public DatabaseMigrationService(
        BackupDbContext context, 
        ILogger<DatabaseMigrationService> logger,
        ICredentialStorage credentialStorage,
        IOptions<DatabaseInitializationOptions> initOptions)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _credentialStorage = credentialStorage ?? throw new ArgumentNullException(nameof(credentialStorage));
        _initOptions = initOptions?.Value ?? new DatabaseInitializationOptions();
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 初始化数据库，创建数据库（如果不存在）并应用所有待处理的迁移
    /// </summary>
    /// <returns>异步任务</returns>
    /// <remarks>
    /// 该方法执行以下操作：
    /// 1. 确保数据库文件存在，如果不存在则创建
    /// 2. 检查并应用所有待处理的数据库迁移
    /// 3. 填充默认的种子数据
    /// 4. 记录详细的操作日志
    /// 
    /// 通常在应用程序启动时调用，确保数据库环境准备就绪
    /// </remarks>
    /// <exception cref="Exception">当数据库初始化过程中发生错误时抛出</exception>
    public async Task InitializeDatabaseAsync()
    {
        try
        {
            _logger.LogInformation("正在初始化数据库...");

            // 确保数据库已创建
            var created = await _context.Database.EnsureCreatedAsync();
            if (created)
            {
                _logger.LogInformation("数据库创建成功");
            }
            else
            {
                _logger.LogInformation("数据库已存在");
            }

            // 应用所有待处理的迁移
            var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                _logger.LogInformation("正在应用 {Count} 个待处理的迁移: {Migrations}", 
                    pendingMigrations.Count(), 
                    string.Join(", ", pendingMigrations));
                
                await _context.Database.MigrateAsync();
                _logger.LogInformation("迁移应用成功");
            }
            else
            {
                _logger.LogInformation("没有发现待处理的迁移");
            }

            // 填充默认种子数据
            await SeedDefaultDataAsync();

            _logger.LogInformation("数据库初始化成功完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化数据库时发生错误");
            throw;
        }
    }

    /// <summary>
    /// 为数据库填充默认种子数据
    /// </summary>
    /// <returns>异步任务</returns>
    /// <remarks>
    /// 该方法会检查并创建以下默认数据：
    /// 1. 默认客户端认证凭据（用于测试和初始设置）
    /// 2. 默认备份保留策略
    /// 3. 默认备份配置（如果需要）
    /// 4. 默认调度配置（关联到默认备份配置）
    /// 
    /// 只有在相应数据不存在时才会创建，避免重复数据
    /// </remarks>
    /// <exception cref="Exception">当种子数据创建过程中发生错误时抛出</exception>
    public async Task SeedDefaultDataAsync()
    {
        try
        {
            _logger.LogInformation("正在填充默认种子数据...");

            // 首先创建默认客户端凭据
            await SeedDefaultClientCredentialsAsync();

            // 添加默认保留策略（如果不存在）
            if (!await _context.RetentionPolicies.AnyAsync())
            {
                var policyOptions = _initOptions.DefaultRetentionPolicy;
                if (policyOptions != null)
                {
                    // 直接使用配置的 RetentionPolicy 对象
                    policyOptions.CreatedAt = DateTime.Now;

                    _context.RetentionPolicies.Add(policyOptions);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("默认保留策略创建成功: {PolicyName}", policyOptions.Name);
                }
                else 
                {
                    var defaultPolicy = new Models.RetentionPolicy
                    {
                        Name = "Default Policy",
                        Description = "Keep backups for 30 days or maximum 10 backups",
                        MaxAgeDays = 30,        // 保留30天
                        MaxCount = 10,          // 最多保留10个备份
                        IsEnabled = true,       // 默认启用
                        CreatedAt = DateTime.Now
                    };

                    _context.RetentionPolicies.Add(defaultPolicy);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("默认保留策略创建成功");
                }
            }

            // 添加默认备份配置（如果不存在），并返回创建的配置
            var defaultBackupConfig = await SeedDefaultBackupConfigurationAsync();

            // 添加默认调度配置（如果不存在），关联到默认备份配置
            await SeedDefaultScheduleConfigurationAsync(defaultBackupConfig);

            _logger.LogInformation("默认种子数据填充完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "填充默认种子数据时发生错误");
            throw;
        }
    }

    #endregion

    /// <summary>
    /// Seeds default client credentials for testing and initial setup
    /// </summary>
    private async Task SeedDefaultClientCredentialsAsync()
    {
        try
        {
            var credOptions = _initOptions.DefaultClientCredentials;
            if (credOptions == null)
            {
                _logger.LogDebug("No default client credentials configured, skipping creation");
                return;
            }

            // Check if any client credentials exist
            var existingClients = await _credentialStorage.ListClientIdsAsync();
            
            // Check if default client exists and has correct permissions
            var defaultClientExists = existingClients.Contains(credOptions.ClientId);
            if (defaultClientExists)
            {
                var existingClient = await _credentialStorage.GetCredentialsAsync(credOptions.ClientId);
                if (existingClient != null)
                {
                    // Check if permissions are correct (using new format)
                    var hasCorrectPermissions = existingClient.Permissions.Contains(BackupPermissions.UploadBackup);
                    if (!hasCorrectPermissions)
                    {
                        _logger.LogInformation("Updating default client credentials with correct permissions");
                        
                        // Update with correct permissions from config
                        existingClient.Permissions = credOptions.Permissions.Any() 
                            ? credOptions.Permissions 
                            : new List<string>
                            {
                                BackupPermissions.UploadBackup,
                                BackupPermissions.DownloadBackup,
                                BackupPermissions.ListBackups,
                                BackupPermissions.DeleteBackup
                            };
                        
                        await _credentialStorage.UpdateCredentialsAsync(existingClient);
                        _logger.LogInformation("Default client credentials updated with correct permissions");
                        return;
                    }
                    else
                    {
                        _logger.LogDebug("Default client credentials already exist with correct permissions");
                        return;
                    }
                }
            }
            
            if (existingClients.Any() && !defaultClientExists)
            {
                _logger.LogDebug("Other client credentials exist, skipping default credential creation");
                return;
            }

            // 直接使用配置的 ClientCredentials 对象
            credOptions.CreatedAt = DateTime.Now;
            credOptions.ExpiresAt = null; // No expiration for default client

            // Ensure permissions are set
            if (!credOptions.Permissions.Any())
            {
                credOptions.Permissions = new List<string>
                {
                    BackupPermissions.UploadBackup,
                    BackupPermissions.DownloadBackup,
                    BackupPermissions.ListBackups,
                    BackupPermissions.DeleteBackup
                };
            }

            var success = await _credentialStorage.StoreCredentialsAsync(credOptions);
            if (success)
            {
                _logger.LogInformation("Default client credentials created successfully for client '{ClientId}'", credOptions.ClientId);
                _logger.LogWarning("SECURITY WARNING: Default client credentials are being used. Please change them in production!");
            }
            else
            {
                _logger.LogError("Failed to create default client credentials");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating default client credentials");
            // Don't throw here as this is not critical for database initialization
        }
    }

    /// <summary>
    /// Seeds default backup configuration for testing and initial setup
    /// </summary>
    /// <returns>创建的默认备份配置，如果已存在或未配置则返回 null</returns>
    private async Task<BackupConfiguration?> SeedDefaultBackupConfigurationAsync()
    {
        try
        {
            var configOptions = _initOptions.DefaultBackupConfiguration;
            if (configOptions == null)
            {
                _logger.LogDebug("No default backup configuration configured, skipping creation");
                return null;
            }

            // Check if any backup configurations exist
            if (await _context.BackupConfigurations.AnyAsync())
            {
                _logger.LogDebug("Backup configurations already exist, skipping default configuration creation");
                return null;
            }

            // 直接使用配置的 BackupConfiguration 对象
            configOptions.CreatedAt = DateTime.Now;

            _context.BackupConfigurations.Add(configOptions);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Default backup configuration created successfully: {ConfigName} (ID: {ConfigId})", 
                configOptions.Name, configOptions.Id);
            _logger.LogWarning("SECURITY WARNING: Default backup configuration is being used. Please review and update the settings!");
            
            return configOptions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating default backup configuration");
            // Don't throw here as this is not critical for database initialization
            return null;
        }
    }

    /// <summary>
    /// Seeds default schedule configuration for testing and initial setup
    /// </summary>
    /// <param name="defaultBackupConfig">默认备份配置，如果为 null 则查找第一个可用的备份配置</param>
    private async Task SeedDefaultScheduleConfigurationAsync(BackupConfiguration? defaultBackupConfig)
    {
        try
        {
            var scheduleOptions = _initOptions.DefaultScheduleConfiguration;
            if (scheduleOptions == null)
            {
                _logger.LogDebug("No default schedule configuration configured, skipping creation");
                return;
            }

            // Check if any schedule configurations exist
            if (await _context.ScheduleConfigurations.AnyAsync())
            {
                _logger.LogDebug("Schedule configurations already exist, skipping default schedule creation");
                return;
            }

            // 优先使用传入的默认备份配置，否则查找第一个可用的备份配置
            var backupConfig = defaultBackupConfig ?? await _context.BackupConfigurations.FirstOrDefaultAsync();
            if (backupConfig == null)
            {
                _logger.LogDebug("No backup configuration exists, skipping default schedule creation");
                return;
            }

            // 直接使用配置的 ScheduleConfiguration 对象，设置关联的 BackupConfigId
            scheduleOptions.BackupConfigId = backupConfig.Id;
            scheduleOptions.CreatedAt = DateTime.Now;
            
            // Calculate next execution time
            scheduleOptions.NextExecution = scheduleOptions.CalculateNextExecution();

            _context.ScheduleConfigurations.Add(scheduleOptions);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Default schedule configuration created successfully: {ScheduleType} at {ScheduleTime}, linked to backup config '{BackupConfigName}' (ID: {BackupConfigId})", 
                scheduleOptions.ScheduleType, scheduleOptions.ScheduleTime, backupConfig.Name, backupConfig.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating default schedule configuration");
            // Don't throw here as this is not critical for database initialization
        }
    }

    /// <summary>
    /// Checks if the database exists and is accessible
    /// </summary>
    public async Task<bool> CanConnectAsync()
    {
        try
        {
            return await _context.Database.CanConnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking database connectivity");
            return false;
        }
    }

    /// <summary>
    /// Gets information about the current database state
    /// </summary>
    public async Task<DatabaseInfo> GetDatabaseInfoAsync()
    {
        try
        {
            var info = new DatabaseInfo
            {
                CanConnect = await _context.Database.CanConnectAsync(),
                DatabaseExists = await _context.Database.CanConnectAsync(),
                PendingMigrations = (await _context.Database.GetPendingMigrationsAsync()).ToList(),
                AppliedMigrations = (await _context.Database.GetAppliedMigrationsAsync()).ToList()
            };

            if (info.CanConnect)
            {
                info.ConfigurationCount = await _context.BackupConfigurations.CountAsync();
                info.BackupLogCount = await _context.BackupLogs.CountAsync();
                info.RetentionPolicyCount = await _context.RetentionPolicies.CountAsync();
            }

            return info;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting database information");
            return new DatabaseInfo
            {
                CanConnect = false,
                DatabaseExists = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Creates a backup of the database
    /// </summary>
    public async Task<bool> BackupDatabaseAsync(string backupPath)
    {
        try
        {
            _logger.LogInformation("Creating database backup at {BackupPath}", backupPath);

            // For SQLite, we can simply copy the database file
            var connectionString = _context.Database.GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError("Connection string is null or empty");
                return false;
            }

            // Extract the database file path from the connection string
            var dbPath = ExtractDatabasePath(connectionString);
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                _logger.LogError("Database file not found at {DbPath}", dbPath);
                return false;
            }

            // Ensure the backup directory exists
            var backupDirectory = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrEmpty(backupDirectory) && !Directory.Exists(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
            }

            // Copy the database file
            File.Copy(dbPath, backupPath, overwrite: true);
            
            _logger.LogInformation("Database backup created successfully at {BackupPath}", backupPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating database backup");
            return false;
        }
    }

    /// <summary>
    /// Restores a database from a backup
    /// </summary>
    public async Task<bool> RestoreDatabaseAsync(string backupPath)
    {
        try
        {
            if (!File.Exists(backupPath))
            {
                _logger.LogError("Backup file not found at {BackupPath}", backupPath);
                return false;
            }

            _logger.LogInformation("Restoring database from backup at {BackupPath}", backupPath);

            var connectionString = _context.Database.GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError("Connection string is null or empty");
                return false;
            }

            var dbPath = ExtractDatabasePath(connectionString);
            if (string.IsNullOrEmpty(dbPath))
            {
                _logger.LogError("Could not extract database path from connection string");
                return false;
            }

            // Close any existing connections
            await _context.Database.CloseConnectionAsync();

            // Copy the backup file over the current database
            File.Copy(backupPath, dbPath, overwrite: true);

            _logger.LogInformation("Database restored successfully from {BackupPath}", backupPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring database from backup");
            return false;
        }
    }

    private static string? ExtractDatabasePath(string connectionString)
    {
        // Simple extraction for SQLite connection strings
        // Format: "Data Source=path/to/database.db" or "DataSource=path/to/database.db"
        var parts = connectionString.Split(';');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring("Data Source=".Length);
            }
            if (trimmed.StartsWith("DataSource=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring("DataSource=".Length);
            }
        }
        return null;
    }
}

/// <summary>
/// Information about the current database state
/// </summary>
public class DatabaseInfo
{
    public bool CanConnect { get; set; }
    public bool DatabaseExists { get; set; }
    public List<string> PendingMigrations { get; set; } = new();
    public List<string> AppliedMigrations { get; set; } = new();
    public int ConfigurationCount { get; set; }
    public int BackupLogCount { get; set; }
    public int RetentionPolicyCount { get; set; }
    public string? Error { get; set; }

    public bool HasPendingMigrations => PendingMigrations.Count > 0;
    public bool IsUpToDate => !HasPendingMigrations && CanConnect;
}