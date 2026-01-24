using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Data;
using MySqlBackupTool.Shared.Data.Migrations;
using MySqlBackupTool.Shared.Data.Repositories;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Logging;
using MySqlBackupTool.Shared.Services;

namespace MySqlBackupTool.Shared.DependencyInjection;

/// <summary>
/// Extension methods for configuring dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds shared services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddSharedServices(this IServiceCollection services, string connectionString)
    {
        // Add Entity Framework
        services.AddDbContext<BackupDbContext>(options =>
            options.UseSqlite(connectionString));

        // Add repositories
        services.AddScoped<IBackupConfigurationRepository, BackupConfigurationRepository>();
        services.AddScoped<IBackupLogRepository, BackupLogRepository>();
        services.AddScoped<IRetentionPolicyRepository, RetentionPolicyRepository>();
        services.AddScoped<IResumeTokenRepository, ResumeTokenRepository>();

        // Add database migration service
        services.AddScoped<DatabaseMigrationService>();

        // Add business services
        services.AddScoped<IBackupLogService, BackupLogService>();
        services.AddScoped<BackupReportingService>();
        services.AddScoped<RetentionManagementService>();

        // Add logging
        services.AddBackupToolLogging();

        return services;
    }

    /// <summary>
    /// Adds client-specific services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddClientServices(this IServiceCollection services)
    {
        // Add checksum service
        services.AddScoped<IChecksumService, ChecksumService>();
        
        // Add MySQL management service
        services.AddScoped<IMySQLManager, MySQLManager>();
        
        // Add compression service
        services.AddScoped<ICompressionService, CompressionService>();
        
        // Add file transfer client
        services.AddScoped<IFileTransferClient, FileTransferClient>();
        
        return services;
    }

    /// <summary>
    /// Adds server-specific services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddServerServices(this IServiceCollection services, string? baseStoragePath = null)
    {
        // Add checksum service
        services.AddScoped<IChecksumService, ChecksumService>();
        
        // Add file receiver service
        services.AddScoped<IFileReceiver, FileReceiver>();
        
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

    /// <summary>
    /// Creates the default SQLite connection string
    /// </summary>
    public static string CreateDefaultConnectionString(string databasePath = "backup_tool.db")
    {
        var fullPath = Path.GetFullPath(databasePath);
        return $"Data Source={fullPath}";
    }

    /// <summary>
    /// Ensures the database is properly initialized
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var migrationService = scope.ServiceProvider.GetRequiredService<DatabaseMigrationService>();
        
        await migrationService.InitializeDatabaseAsync();
    }
}