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
using System.Security.Cryptography.X509Certificates;

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
        services.AddScoped<IScheduleConfigurationRepository, ScheduleConfigurationRepository>();

        // Add database migration service
        services.AddScoped<DatabaseMigrationService>();

        // Add business services
        services.AddScoped<IBackupLogService, BackupLogService>();
        services.AddScoped<BackupReportingService>();
        services.AddScoped<IRetentionPolicyService, RetentionManagementService>();
        services.AddScoped<RetentionManagementService>();

        // Add network and alerting services
        services.AddScoped<INetworkRetryService, NetworkRetryService>();
        services.AddScoped<IAlertingService, AlertingService>();

        // Add authentication services
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();

        // Add error handling services
        services.AddErrorHandlingServices();

        // Add logging
        services.AddBackupToolLogging();
        
        // Add application-specific logging service
        services.AddScoped<ILoggingService, LoggingService>();
        
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

        return services;
    }

    /// <summary>
    /// Adds client-specific services to the dependency injection container
    /// </summary>
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
            services.AddScoped<IFileTransferClient>(provider =>
            {
                var innerClient = provider.GetRequiredService<FileTransferClient>();
                var errorRecoveryManager = provider.GetRequiredService<IErrorRecoveryManager>();
                var logger = provider.GetRequiredService<ILogger<TimeoutProtectedFileTransferClient>>();
                return new TimeoutProtectedFileTransferClient(innerClient, errorRecoveryManager, logger);
            });
            
            // Register IFileTransferService as an alias for IFileTransferClient for consistency
            services.AddScoped<IFileTransferService>(provider =>
            {
                var innerClient = provider.GetRequiredService<FileTransferClient>();
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

    /// <summary>
    /// Adds server-specific services to the dependency injection container
    /// </summary>
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

    /// <summary>
    /// Adds backup scheduling services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddBackupSchedulingServices(this IServiceCollection services)
    {
        // Add backup scheduler service
        services.AddScoped<IBackupScheduler, BackupSchedulerService>();
        
        // Add the scheduler as a hosted service for background execution
        services.AddHostedService<BackupSchedulerService>();
        
        // Add auto-startup service
        services.AddHostedService<AutoStartupService>();
        
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
}

/// <summary>
/// Configuration options for SSL/TLS services
/// </summary>
public class SslConfiguration
{
    /// <summary>
    /// Whether to use SSL/TLS for network communications
    /// </summary>
    public bool UseSSL { get; set; } = true;

    /// <summary>
    /// Path to the server certificate file
    /// </summary>
    public string? ServerCertificatePath { get; set; }

    /// <summary>
    /// Password for the server certificate file
    /// </summary>
    public string? ServerCertificatePassword { get; set; }

    /// <summary>
    /// Whether to require client certificates
    /// </summary>
    public bool RequireClientCertificate { get; set; } = false;

    /// <summary>
    /// Whether to validate server certificates on the client side
    /// </summary>
    public bool ValidateServerCertificate { get; set; } = true;

    /// <summary>
    /// Whether to allow self-signed certificates
    /// </summary>
    public bool AllowSelfSignedCertificates { get; set; } = false;

    /// <summary>
    /// Expected certificate subject name for validation
    /// </summary>
    public string? ExpectedCertificateSubject { get; set; }

    /// <summary>
    /// Certificate thumbprint for validation
    /// </summary>
    public string? CertificateThumbprint { get; set; }
}