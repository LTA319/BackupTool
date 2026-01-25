using Microsoft.EntityFrameworkCore;
using MySqlBackupTool.Shared.Models;
using System.Text.Json;

namespace MySqlBackupTool.Shared.Data;

/// <summary>
/// 备份工具数据库的Entity Framework DbContext
/// 提供对备份配置、计划、日志等实体的数据访问功能
/// </summary>
public class BackupDbContext : DbContext
{
    /// <summary>
    /// 构造函数，初始化数据库上下文
    /// </summary>
    /// <param name="options">数据库上下文选项</param>
    public BackupDbContext(DbContextOptions<BackupDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// 备份配置实体集合
    /// </summary>
    public DbSet<BackupConfiguration> BackupConfigurations { get; set; }
    
    /// <summary>
    /// 计划配置实体集合
    /// </summary>
    public DbSet<ScheduleConfiguration> ScheduleConfigurations { get; set; }
    
    /// <summary>
    /// 备份日志实体集合
    /// </summary>
    public DbSet<BackupLog> BackupLogs { get; set; }
    
    /// <summary>
    /// 传输日志实体集合
    /// </summary>
    public DbSet<TransferLog> TransferLogs { get; set; }
    
    /// <summary>
    /// 保留策略实体集合
    /// </summary>
    public DbSet<RetentionPolicy> RetentionPolicies { get; set; }
    
    /// <summary>
    /// 恢复令牌实体集合
    /// </summary>
    public DbSet<ResumeToken> ResumeTokens { get; set; }
    
    /// <summary>
    /// 恢复分块实体集合
    /// </summary>
    public DbSet<ResumeChunk> ResumeChunks { get; set; }

    /// <summary>
    /// 配置实体模型和数据库映射关系
    /// </summary>
    /// <param name="modelBuilder">模型构建器</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 配置备份配置实体
        modelBuilder.Entity<BackupConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DataDirectoryPath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ServiceName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TargetDirectory).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");

            // 将复杂属性配置为JSON存储
            entity.Property(e => e.MySQLConnection)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<MySQLConnectionInfo>(v, (JsonSerializerOptions?)null) ?? new MySQLConnectionInfo()
                );

            entity.Property(e => e.TargetServer)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<ServerEndpoint>(v, (JsonSerializerOptions?)null) ?? new ServerEndpoint()
                );

            entity.Property(e => e.NamingStrategy)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<FileNamingStrategy>(v, (JsonSerializerOptions?)null) ?? new FileNamingStrategy()
                );
        });

        // 配置计划配置实体
        modelBuilder.Entity<ScheduleConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ScheduleType).HasConversion<string>();
            entity.Property(e => e.ScheduleTime).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");

            // 配置与备份配置的关系
            entity.HasOne(e => e.BackupConfiguration)
                .WithMany()
                .HasForeignKey(e => e.BackupConfigId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 配置备份日志实体
        modelBuilder.Entity<BackupLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StartTime).HasDefaultValueSql("datetime('now')");
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.FilePath).HasMaxLength(500);
            entity.Property(e => e.ResumeToken).HasMaxLength(100);

            // 配置与备份配置的关系
            entity.HasOne<BackupConfiguration>()
                .WithMany()
                .HasForeignKey(e => e.BackupConfigId)
                .OnDelete(DeleteBehavior.Cascade);

            // 配置与传输日志的关系
            entity.HasMany(e => e.TransferLogs)
                .WithOne(e => e.BackupLog)
                .HasForeignKey(e => e.BackupLogId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 配置传输日志实体
        modelBuilder.Entity<TransferLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.TransferTime).HasDefaultValueSql("datetime('now')");
        });

        // 配置保留策略实体
        modelBuilder.Entity<RetentionPolicy>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
        });

        // 配置恢复令牌实体
        modelBuilder.Entity<ResumeToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TransferId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.TempDirectory).HasMaxLength(500);
            entity.Property(e => e.ChecksumMD5).HasMaxLength(32);
            entity.Property(e => e.ChecksumSHA256).HasMaxLength(64);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
            entity.Property(e => e.LastActivity).HasDefaultValueSql("datetime('now')");

            // 配置与备份日志的关系
            entity.HasOne(e => e.BackupLog)
                .WithMany()
                .HasForeignKey(e => e.BackupLogId)
                .OnDelete(DeleteBehavior.SetNull);

            // 配置与恢复分块的关系
            entity.HasMany(e => e.CompletedChunks)
                .WithOne(e => e.ResumeToken)
                .HasForeignKey(e => e.ResumeTokenId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 配置恢复分块实体
        modelBuilder.Entity<ResumeChunk>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ChunkChecksum).HasMaxLength(32);
            entity.Property(e => e.CompletedAt).HasDefaultValueSql("datetime('now')");

            // 在恢复令牌ID和分块索引上创建唯一约束
            entity.HasIndex(e => new { e.ResumeTokenId, e.ChunkIndex })
                .IsUnique()
                .HasDatabaseName("IX_ResumeChunk_TokenId_ChunkIndex");
        });

        // 创建索引以提高查询性能
        modelBuilder.Entity<BackupLog>()
            .HasIndex(e => e.StartTime)
            .HasDatabaseName("IX_BackupLog_StartTime");

        modelBuilder.Entity<BackupLog>()
            .HasIndex(e => e.Status)
            .HasDatabaseName("IX_BackupLog_Status");

        modelBuilder.Entity<BackupConfiguration>()
            .HasIndex(e => e.IsActive)
            .HasDatabaseName("IX_BackupConfiguration_IsActive");

        // 为恢复令牌创建索引
        modelBuilder.Entity<ResumeToken>()
            .HasIndex(e => e.Token)
            .IsUnique()
            .HasDatabaseName("IX_ResumeToken_Token");

        modelBuilder.Entity<ResumeToken>()
            .HasIndex(e => e.TransferId)
            .HasDatabaseName("IX_ResumeToken_TransferId");

        modelBuilder.Entity<ResumeToken>()
            .HasIndex(e => e.IsCompleted)
            .HasDatabaseName("IX_ResumeToken_IsCompleted");

        modelBuilder.Entity<ResumeToken>()
            .HasIndex(e => e.LastActivity)
            .HasDatabaseName("IX_ResumeToken_LastActivity");

        // 为计划配置创建索引
        modelBuilder.Entity<ScheduleConfiguration>()
            .HasIndex(e => e.BackupConfigId)
            .HasDatabaseName("IX_ScheduleConfiguration_BackupConfigId");

        modelBuilder.Entity<ScheduleConfiguration>()
            .HasIndex(e => e.IsEnabled)
            .HasDatabaseName("IX_ScheduleConfiguration_IsEnabled");

        modelBuilder.Entity<ScheduleConfiguration>()
            .HasIndex(e => e.NextExecution)
            .HasDatabaseName("IX_ScheduleConfiguration_NextExecution");
    }

    /// <summary>
    /// 确保数据库已创建并且是最新的
    /// </summary>
    public async Task EnsureDatabaseCreatedAsync()
    {
        await Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// 如果需要，为数据库填充默认数据
    /// </summary>
    public async Task SeedDefaultDataAsync()
    {
        // 如果不存在保留策略，则添加默认保留策略
        if (!await RetentionPolicies.AnyAsync())
        {
            var defaultPolicy = new RetentionPolicy
            {
                Name = "Default Policy",
                Description = "Keep backups for 30 days or maximum 10 backups",
                MaxAgeDays = 30,
                MaxCount = 10,
                IsEnabled = true
            };

            RetentionPolicies.Add(defaultPolicy);
            await SaveChangesAsync();
        }
    }
}