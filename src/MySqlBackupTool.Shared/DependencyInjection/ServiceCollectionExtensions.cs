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
/// Extension methods for configuring dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds shared services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddSharedServices(this IServiceCollection services, string connectionString)
    {
        return services.AddSharedServices(connectionString, null);
    }

    /// <summary>
    /// Adds shared services to the dependency injection container with configuration
    /// </summary>
    public static IServiceCollection AddSharedServices(this IServiceCollection services, string connectionString, IConfiguration? configuration)
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
        
        // Add AlertingConfig with comprehensive configuration binding and error handling
        services.AddSingleton<AlertingConfig>(provider =>
        {
            var logger = provider.GetService<ILogger<AlertingConfig>>();
            var alertingConfig = new AlertingConfig();
            
            try
            {
                if (configuration != null)
                {
                    var alertingSection = configuration.GetSection("Alerting");
                    if (alertingSection.Exists())
                    {
                        logger?.LogInformation("Found Alerting configuration section, attempting to bind configuration values");
                        
                        // Attempt to bind configuration
                        alertingSection.Bind(alertingConfig);
                        
                        // Validate bound configuration values
                        var validationErrors = ValidateAlertingConfig(alertingConfig, logger);
                        
                        if (validationErrors.Any())
                        {
                            logger?.LogWarning("AlertingConfig validation found {ErrorCount} issues: {Errors}. Using corrected values.",
                                validationErrors.Count, string.Join("; ", validationErrors));
                        }
                        else
                        {
                            logger?.LogInformation("AlertingConfig successfully bound and validated from configuration section");
                        }
                        
                        // Log final configuration values for transparency
                        LogAlertingConfigValues(alertingConfig, logger);
                    }
                    else
                    {
                        logger?.LogInformation("No Alerting configuration section found, using default AlertingConfig values");
                        LogAlertingConfigValues(alertingConfig, logger);
                    }
                }
                else
                {
                    logger?.LogInformation("No configuration provided, using default AlertingConfig values");
                    LogAlertingConfigValues(alertingConfig, logger);
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to bind AlertingConfig from configuration section 'Alerting': {ErrorMessage}. " +
                    "Using default configuration values. This may affect alerting functionality.", ex.Message);
                
                // Reset to defaults in case of partial binding
                alertingConfig = new AlertingConfig();
                LogAlertingConfigValues(alertingConfig, logger);
            }
            
            return alertingConfig;
        });
        
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
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();

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

        //添加服务检查
        services.AddSingleton<IServiceChecker, ServiceChecker>();

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