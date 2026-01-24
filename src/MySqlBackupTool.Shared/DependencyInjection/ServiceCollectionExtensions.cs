using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Data;
using MySqlBackupTool.Shared.Data.Migrations;
using MySqlBackupTool.Shared.Data.Repositories;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Logging;
using MySqlBackupTool.Shared.Models;
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

        // Add error handling services
        services.AddErrorHandlingServices();

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
        
        // Add core services (without timeout protection)
        services.AddScoped<MySQLManager>();
        services.AddScoped<CompressionService>();
        services.AddScoped<FileTransferClient>();
        
        // Add timeout-protected services as the primary implementations
        services.AddScoped<IMySQLManager>(provider =>
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
        
        services.AddScoped<IFileTransferClient>(provider =>
        {
            var innerClient = provider.GetRequiredService<FileTransferClient>();
            var errorRecoveryManager = provider.GetRequiredService<IErrorRecoveryManager>();
            var logger = provider.GetRequiredService<ILogger<TimeoutProtectedFileTransferClient>>();
            return new TimeoutProtectedFileTransferClient(innerClient, errorRecoveryManager, logger);
        });
        
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
    /// Adds error handling services with custom configuration
    /// </summary>
    public static IServiceCollection AddErrorHandlingServices(this IServiceCollection services, Action<ErrorRecoveryConfig> configureOptions)
    {
        var config = new ErrorRecoveryConfig();
        configureOptions(config);
        
        return services.AddErrorHandlingServices(config);
    }
}