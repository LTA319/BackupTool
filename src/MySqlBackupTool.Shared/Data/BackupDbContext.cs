using Microsoft.EntityFrameworkCore;
using MySqlBackupTool.Shared.Models;
using System.Text.Json;

namespace MySqlBackupTool.Shared.Data;

/// <summary>
/// Entity Framework DbContext for the backup tool database
/// </summary>
public class BackupDbContext : DbContext
{
    public BackupDbContext(DbContextOptions<BackupDbContext> options) : base(options)
    {
    }

    public DbSet<BackupConfiguration> BackupConfigurations { get; set; }
    public DbSet<ScheduleConfiguration> ScheduleConfigurations { get; set; }
    public DbSet<BackupLog> BackupLogs { get; set; }
    public DbSet<TransferLog> TransferLogs { get; set; }
    public DbSet<RetentionPolicy> RetentionPolicies { get; set; }
    public DbSet<ResumeToken> ResumeTokens { get; set; }
    public DbSet<ResumeChunk> ResumeChunks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure BackupConfiguration
        modelBuilder.Entity<BackupConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DataDirectoryPath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ServiceName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TargetDirectory).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");

            // Configure complex properties as JSON
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

        // Configure ScheduleConfiguration
        modelBuilder.Entity<ScheduleConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ScheduleType).HasConversion<string>();
            entity.Property(e => e.ScheduleTime).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");

            // Configure relationship with BackupConfiguration
            entity.HasOne(e => e.BackupConfiguration)
                .WithMany()
                .HasForeignKey(e => e.BackupConfigId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure BackupLog
        modelBuilder.Entity<BackupLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StartTime).HasDefaultValueSql("datetime('now')");
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.FilePath).HasMaxLength(500);
            entity.Property(e => e.ResumeToken).HasMaxLength(100);

            // Configure relationship with BackupConfiguration
            entity.HasOne<BackupConfiguration>()
                .WithMany()
                .HasForeignKey(e => e.BackupConfigId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure relationship with TransferLogs
            entity.HasMany(e => e.TransferLogs)
                .WithOne(e => e.BackupLog)
                .HasForeignKey(e => e.BackupLogId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure TransferLog
        modelBuilder.Entity<TransferLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.TransferTime).HasDefaultValueSql("datetime('now')");
        });

        // Configure RetentionPolicy
        modelBuilder.Entity<RetentionPolicy>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
        });

        // Configure ResumeToken
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

            // Configure relationship with BackupLog
            entity.HasOne(e => e.BackupLog)
                .WithMany()
                .HasForeignKey(e => e.BackupLogId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure relationship with ResumeChunks
            entity.HasMany(e => e.CompletedChunks)
                .WithOne(e => e.ResumeToken)
                .HasForeignKey(e => e.ResumeTokenId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure ResumeChunk
        modelBuilder.Entity<ResumeChunk>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ChunkChecksum).HasMaxLength(32);
            entity.Property(e => e.CompletedAt).HasDefaultValueSql("datetime('now')");

            // Create unique constraint on ResumeTokenId + ChunkIndex
            entity.HasIndex(e => new { e.ResumeTokenId, e.ChunkIndex })
                .IsUnique()
                .HasDatabaseName("IX_ResumeChunk_TokenId_ChunkIndex");
        });

        // Create indexes for better query performance
        modelBuilder.Entity<BackupLog>()
            .HasIndex(e => e.StartTime)
            .HasDatabaseName("IX_BackupLog_StartTime");

        modelBuilder.Entity<BackupLog>()
            .HasIndex(e => e.Status)
            .HasDatabaseName("IX_BackupLog_Status");

        modelBuilder.Entity<BackupConfiguration>()
            .HasIndex(e => e.IsActive)
            .HasDatabaseName("IX_BackupConfiguration_IsActive");

        // Create indexes for resume tokens
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

        // Create indexes for schedule configurations
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
    /// Ensures the database is created and up to date
    /// </summary>
    public async Task EnsureDatabaseCreatedAsync()
    {
        await Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Seeds the database with default data if needed
    /// </summary>
    public async Task SeedDefaultDataAsync()
    {
        // Add default retention policy if none exists
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