using Microsoft.EntityFrameworkCore;
using MySqlBackupTool.Shared.Models;
using System.Text.Json;

namespace MySqlBackupTool.Shared.Data;

/// <summary>
/// MySQL备份工具数据库的Entity Framework DbContext
/// 提供对备份配置、计划、日志等实体的数据访问功能
/// 使用SQLite作为本地数据库存储备份相关的配置和日志信息
/// </summary>
/// <remarks>
/// 该DbContext管理以下主要实体：
/// - BackupConfiguration: 备份配置信息
/// - ScheduleConfiguration: 备份计划配置
/// - BackupLog: 备份执行日志
/// - TransferLog: 文件传输日志
/// - RetentionPolicy: 备份保留策略
/// - ResumeToken: 断点续传令牌
/// - ResumeChunk: 断点续传分块信息
/// </remarks>
public class BackupDbContext : DbContext
{
    #region 构造函数

    /// <summary>
    /// 初始化BackupDbContext类的新实例
    /// </summary>
    /// <param name="options">数据库上下文配置选项，包含连接字符串和其他配置</param>
    public BackupDbContext(DbContextOptions<BackupDbContext> options) : base(options)
    {
    }

    #endregion

    #region 实体集合属性

    /// <summary>
    /// 备份配置实体集合
    /// 存储MySQL数据库备份的配置信息，包括连接参数、目标服务器、文件命名策略等
    /// </summary>
    public DbSet<BackupConfiguration> BackupConfigurations { get; set; }
    
    /// <summary>
    /// 计划配置实体集合
    /// 存储备份任务的调度配置，支持定时、周期性等多种调度方式
    /// </summary>
    public DbSet<ScheduleConfiguration> ScheduleConfigurations { get; set; }
    
    /// <summary>
    /// 备份日志实体集合
    /// 记录每次备份操作的详细信息，包括状态、时间、文件大小、错误信息等
    /// </summary>
    public DbSet<BackupLog> BackupLogs { get; set; }
    
    /// <summary>
    /// 传输日志实体集合
    /// 记录文件传输过程中的详细日志信息，用于跟踪传输进度和诊断问题
    /// </summary>
    public DbSet<TransferLog> TransferLogs { get; set; }
    
    /// <summary>
    /// 保留策略实体集合
    /// 定义备份文件的保留规则，包括保留时间、数量限制、存储空间限制等
    /// </summary>
    public DbSet<RetentionPolicy> RetentionPolicies { get; set; }
    
    /// <summary>
    /// 恢复令牌实体集合
    /// 支持断点续传功能，存储传输中断时的状态信息，允许从中断点继续传输
    /// </summary>
    public DbSet<ResumeToken> ResumeTokens { get; set; }
    
    /// <summary>
    /// 恢复分块实体集合
    /// 存储断点续传过程中已完成的文件分块信息，确保传输的完整性和可恢复性
    /// </summary>
    public DbSet<ResumeChunk> ResumeChunks { get; set; }

    #endregion

    #region 模型配置

    /// <summary>
    /// 配置实体模型和数据库映射关系
    /// 定义表结构、字段约束、关系映射、索引等数据库架构信息
    /// </summary>
    /// <param name="modelBuilder">Entity Framework模型构建器，用于配置实体映射</param>
    /// <remarks>
    /// 该方法配置以下内容：
    /// 1. 实体属性映射和约束
    /// 2. 复杂对象的JSON序列化存储
    /// 3. 实体间的关系映射
    /// 4. 数据库索引优化
    /// 5. 默认值和自动生成字段
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        #region 备份配置实体映射

        // 配置备份配置实体的表结构和约束
        modelBuilder.Entity<BackupConfiguration>(entity =>
        {
            // 设置主键
            entity.HasKey(e => e.Id);
            
            // 配置基本字段约束
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DataDirectoryPath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ServiceName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TargetDirectory).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");

            // 配置身份验证字段约束
            entity.Property(e => e.ClientId).IsRequired().HasMaxLength(100).HasDefaultValue("default-client");
            entity.Property(e => e.ClientSecret).IsRequired().HasMaxLength(200).HasDefaultValue("default-secret-2024");

            // 将复杂对象序列化为JSON存储
            // MySQL连接信息作为JSON字段存储，便于灵活扩展
            entity.Property(e => e.MySQLConnection)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<MySQLConnectionInfo>(v, (JsonSerializerOptions?)null) ?? new MySQLConnectionInfo()
                );

            // 目标服务器信息作为JSON字段存储
            entity.Property(e => e.TargetServer)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<ServerEndpoint>(v, (JsonSerializerOptions?)null) ?? new ServerEndpoint()
                );

            // 文件命名策略作为JSON字段存储
            entity.Property(e => e.NamingStrategy)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<FileNamingStrategy>(v, (JsonSerializerOptions?)null) ?? new FileNamingStrategy()
                );
        });

        #endregion

        #region 计划配置实体映射

        // 配置计划配置实体的表结构和关系
        modelBuilder.Entity<ScheduleConfiguration>(entity =>
        {
            // 设置主键
            entity.HasKey(e => e.Id);
            
            // 配置字段约束
            entity.Property(e => e.ScheduleType).HasConversion<string>();
            entity.Property(e => e.ScheduleTime).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");

            // 配置与备份配置的外键关系（一对多）
            entity.HasOne(e => e.BackupConfiguration)
                .WithMany()
                .HasForeignKey(e => e.BackupConfigId)
                .OnDelete(DeleteBehavior.Cascade); // 删除备份配置时级联删除相关计划
        });

        #endregion

        #region 备份日志实体映射

        // 配置备份日志实体的表结构和关系
        modelBuilder.Entity<BackupLog>(entity =>
        {
            // 设置主键
            entity.HasKey(e => e.Id);
            
            // 配置字段约束和默认值
            entity.Property(e => e.StartTime).HasDefaultValueSql("datetime('now')");
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.FilePath).HasMaxLength(500);
            entity.Property(e => e.ResumeToken).HasMaxLength(100);

            // 配置与备份配置的外键关系
            entity.HasOne<BackupConfiguration>()
                .WithMany()
                .HasForeignKey(e => e.BackupConfigId)
                .OnDelete(DeleteBehavior.Cascade); // 删除备份配置时级联删除相关日志

            // 配置与传输日志的一对多关系
            entity.HasMany(e => e.TransferLogs)
                .WithOne(e => e.BackupLog)
                .HasForeignKey(e => e.BackupLogId)
                .OnDelete(DeleteBehavior.Cascade); // 删除备份日志时级联删除传输日志
        });

        #endregion

        #region 传输日志实体映射

        // 配置传输日志实体的表结构
        modelBuilder.Entity<TransferLog>(entity =>
        {
            // 设置主键
            entity.HasKey(e => e.Id);
            
            // 配置字段约束
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.TransferTime).HasDefaultValueSql("datetime('now')");
        });

        #endregion

        #region 保留策略实体映射

        // 配置保留策略实体的表结构
        modelBuilder.Entity<RetentionPolicy>(entity =>
        {
            // 设置主键
            entity.HasKey(e => e.Id);
            
            // 配置字段约束
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
        });

        #endregion

        #region 恢复令牌实体映射

        // 配置恢复令牌实体的表结构和关系
        modelBuilder.Entity<ResumeToken>(entity =>
        {
            // 设置主键
            entity.HasKey(e => e.Id);
            
            // 配置字段约束
            entity.Property(e => e.Token).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TransferId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.TempDirectory).HasMaxLength(500);
            entity.Property(e => e.ChecksumMD5).HasMaxLength(32);
            entity.Property(e => e.ChecksumSHA256).HasMaxLength(64);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
            entity.Property(e => e.LastActivity).HasDefaultValueSql("datetime('now')");

            // 配置与备份日志的可选外键关系
            entity.HasOne(e => e.BackupLog)
                .WithMany()
                .HasForeignKey(e => e.BackupLogId)
                .OnDelete(DeleteBehavior.SetNull); // 删除备份日志时将外键设为null

            // 配置与恢复分块的一对多关系
            entity.HasMany(e => e.CompletedChunks)
                .WithOne(e => e.ResumeToken)
                .HasForeignKey(e => e.ResumeTokenId)
                .OnDelete(DeleteBehavior.Cascade); // 删除恢复令牌时级联删除分块信息
        });

        #endregion

        #region 恢复分块实体映射

        // 配置恢复分块实体的表结构和约束
        modelBuilder.Entity<ResumeChunk>(entity =>
        {
            // 设置主键
            entity.HasKey(e => e.Id);
            
            // 配置字段约束
            entity.Property(e => e.ChunkChecksum).HasMaxLength(32);
            entity.Property(e => e.CompletedAt).HasDefaultValueSql("datetime('now')");

            // 在恢复令牌ID和分块索引上创建唯一约束，防止重复分块
            entity.HasIndex(e => new { e.ResumeTokenId, e.ChunkIndex })
                .IsUnique()
                .HasDatabaseName("IX_ResumeChunk_TokenId_ChunkIndex");
        });

        #endregion

        #region 性能优化索引

        // 为备份日志创建性能优化索引
        // 按开始时间索引，优化时间范围查询
        modelBuilder.Entity<BackupLog>()
            .HasIndex(e => e.StartTime)
            .HasDatabaseName("IX_BackupLog_StartTime");

        // 按状态索引，优化状态过滤查询
        modelBuilder.Entity<BackupLog>()
            .HasIndex(e => e.Status)
            .HasDatabaseName("IX_BackupLog_Status");

        // 为备份配置创建激活状态索引
        modelBuilder.Entity<BackupConfiguration>()
            .HasIndex(e => e.IsActive)
            .HasDatabaseName("IX_BackupConfiguration_IsActive");

        // 为恢复令牌创建多个查询优化索引
        // 令牌值唯一索引，确保令牌唯一性
        modelBuilder.Entity<ResumeToken>()
            .HasIndex(e => e.Token)
            .IsUnique()
            .HasDatabaseName("IX_ResumeToken_Token");

        // 传输ID索引，优化传输查询
        modelBuilder.Entity<ResumeToken>()
            .HasIndex(e => e.TransferId)
            .HasDatabaseName("IX_ResumeToken_TransferId");

        // 完成状态索引，优化活跃令牌查询
        modelBuilder.Entity<ResumeToken>()
            .HasIndex(e => e.IsCompleted)
            .HasDatabaseName("IX_ResumeToken_IsCompleted");

        // 最后活动时间索引，优化过期令牌清理
        modelBuilder.Entity<ResumeToken>()
            .HasIndex(e => e.LastActivity)
            .HasDatabaseName("IX_ResumeToken_LastActivity");

        // 为计划配置创建查询优化索引
        // 备份配置ID索引
        modelBuilder.Entity<ScheduleConfiguration>()
            .HasIndex(e => e.BackupConfigId)
            .HasDatabaseName("IX_ScheduleConfiguration_BackupConfigId");

        // 启用状态索引
        modelBuilder.Entity<ScheduleConfiguration>()
            .HasIndex(e => e.IsEnabled)
            .HasDatabaseName("IX_ScheduleConfiguration_IsEnabled");

        // 下次执行时间索引，优化调度查询
        modelBuilder.Entity<ScheduleConfiguration>()
            .HasIndex(e => e.NextExecution)
            .HasDatabaseName("IX_ScheduleConfiguration_NextExecution");

        #endregion
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 确保数据库已创建并且架构是最新的
    /// 如果数据库不存在则创建，如果存在则确保表结构正确
    /// </summary>
    /// <returns>异步任务</returns>
    /// <remarks>
    /// 该方法在应用程序启动时调用，确保数据库环境准备就绪
    /// 对于SQLite数据库，会在指定路径创建数据库文件
    /// </remarks>
    public async Task EnsureDatabaseCreatedAsync()
    {
        await Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// 为数据库填充默认数据和初始配置
    /// 在数据库首次创建或数据为空时调用，确保系统有基本的配置数据
    /// </summary>
    /// <returns>异步任务</returns>
    /// <remarks>
    /// 该方法会检查并创建以下默认数据：
    /// - 默认的备份保留策略
    /// - 其他必要的初始配置数据
    /// 
    /// 只有在相应的数据表为空时才会添加默认数据，避免重复创建
    /// </remarks>
    public async Task SeedDefaultDataAsync()
    {
        // 检查是否存在保留策略，如果不存在则添加默认保留策略
        if (!await RetentionPolicies.AnyAsync())
        {
            var defaultPolicy = new RetentionPolicy
            {
                Name = "Default Policy",
                Description = "Keep backups for 30 days or maximum 10 backups",
                MaxAgeDays = 30,        // 保留30天
                MaxCount = 10,          // 最多保留10个备份
                IsEnabled = true        // 默认启用
            };

            RetentionPolicies.Add(defaultPolicy);
            await SaveChangesAsync();
        }
    }

    #endregion
}