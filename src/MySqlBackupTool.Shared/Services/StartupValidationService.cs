using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Diagnostics;
using System.Text;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Service for validating service registration and configuration during application startup
/// </summary>
public class StartupValidationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StartupValidationService> _logger;

    public StartupValidationService(IServiceProvider serviceProvider, ILogger<StartupValidationService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates all required services can be resolved and logs configuration status
    /// </summary>
    public async Task<StartupValidationResult> ValidateServicesAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new StartupValidationResult();
        
        _logger.LogInformation("Starting service registration validation...");

        try
        {
            // Validate core shared services
            await ValidateCoreServicesAsync(result);
            
            // Validate HTTP client and alerting services
            await ValidateHttpClientServicesAsync(result);
            
            // Validate configuration services
            await ValidateConfigurationServicesAsync(result);
            
            // Validate repository services
            await ValidateRepositoryServicesAsync(result);
            
            // Validate business services
            await ValidateBusinessServicesAsync(result);

            stopwatch.Stop();
            result.ValidationDuration = stopwatch.Elapsed;
            result.IsValid = result.FailedServices.Count == 0;

            if (result.IsValid)
            {
                _logger.LogInformation("Service registration validation completed successfully. " +
                    "Validated {ValidatedCount} services in {Duration}ms",
                    result.ValidatedServices.Count, result.ValidationDuration.TotalMilliseconds);
            }
            else
            {
                _logger.LogError("Service registration validation failed. " +
                    "{FailedCount} services failed validation out of {TotalCount} services checked in {Duration}ms. " +
                    "Failed services: {FailedServices}",
                    result.FailedServices.Count, 
                    result.ValidatedServices.Count + result.FailedServices.Count,
                    result.ValidationDuration.TotalMilliseconds,
                    string.Join(", ", result.FailedServices.Keys));
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.ValidationDuration = stopwatch.Elapsed;
            result.IsValid = false;
            result.ValidationException = ex;
            
            _logger.LogCritical(ex, "Service registration validation failed with exception after {Duration}ms: {ErrorMessage}",
                result.ValidationDuration.TotalMilliseconds, ex.Message);
            
            return result;
        }
    }

    /// <summary>
    /// Validates core infrastructure services
    /// </summary>
    private async Task ValidateCoreServicesAsync(StartupValidationResult result)
    {
        _logger.LogDebug("Validating core infrastructure services...");

        // Validate logging services
        await ValidateServiceAsync<ILogger<StartupValidationService>>(result, "ILogger<T>");
        await ValidateServiceAsync<ILoggingService>(result, "ILoggingService");

        // Validate database context
        await ValidateServiceAsync<Data.BackupDbContext>(result, "BackupDbContext");
        
        // Validate migration service
        await ValidateServiceAsync<Data.Migrations.DatabaseMigrationService>(result, "DatabaseMigrationService");

        // Validate memory profiler
        await ValidateServiceAsync<IMemoryProfiler>(result, "IMemoryProfiler");
        await ValidateServiceAsync<MemoryProfilingConfig>(result, "MemoryProfilingConfig");

        // Validate encryption service
        await ValidateServiceAsync<IEncryptionService>(result, "IEncryptionService");

        // Validate validation service
        await ValidateServiceAsync<IValidationService>(result, "IValidationService");
    }

    /// <summary>
    /// Validates HTTP client and alerting services
    /// </summary>
    private async Task ValidateHttpClientServicesAsync(StartupValidationResult result)
    {
        _logger.LogDebug("Validating HTTP client and alerting services...");

        // Validate HttpClient factory
        await ValidateServiceAsync<IHttpClientFactory>(result, "IHttpClientFactory");

        // Validate AlertingConfig
        await ValidateServiceAsync<AlertingConfig>(result, "AlertingConfig", async config =>
        {
            _logger.LogInformation("AlertingConfig validation: " +
                "EnableAlerting={EnableAlerting}, " +
                "TimeoutSeconds={TimeoutSeconds}, " +
                "MaxRetryAttempts={MaxRetryAttempts}, " +
                "BaseUrl={BaseUrl}, " +
                "EmailEnabled={EmailEnabled}, " +
                "WebhookEnabled={WebhookEnabled}, " +
                "FileLogEnabled={FileLogEnabled}",
                config.EnableAlerting,
                config.TimeoutSeconds,
                config.MaxRetryAttempts,
                config.BaseUrl ?? "Not configured",
                config.Email.Enabled,
                config.Webhook.Enabled,
                config.FileLog.Enabled);

            // Validate configuration values
            if (config.TimeoutSeconds <= 0)
            {
                _logger.LogWarning("AlertingConfig has invalid TimeoutSeconds: {TimeoutSeconds}", config.TimeoutSeconds);
            }
            if (config.MaxRetryAttempts < 0)
            {
                _logger.LogWarning("AlertingConfig has invalid MaxRetryAttempts: {MaxRetryAttempts}", config.MaxRetryAttempts);
            }
        });

        // Validate AlertingService with all its dependencies
        await ValidateServiceAsync<IAlertingService>(result, "IAlertingService", async alertingService =>
        {
            _logger.LogInformation("AlertingService validation: Service resolved successfully with all dependencies");
            
            // Test that the service can access its configuration
            if (alertingService is AlertingService concreteService)
            {
                var config = concreteService.Configuration;
                _logger.LogDebug("AlertingService configuration access test: EnableAlerting={EnableAlerting}",
                    config.EnableAlerting);
            }
        });

        // Validate HttpClient can be created for AlertingService
        try
        {
            var httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(AlertingService));
            
            result.ValidatedServices["HttpClient for AlertingService"] = "Successfully created typed HttpClient";
            _logger.LogDebug("HttpClient for AlertingService: Successfully created with timeout {Timeout}",
                httpClient.Timeout);
        }
        catch (Exception ex)
        {
            result.FailedServices["HttpClient for AlertingService"] = ex.Message;
            _logger.LogError(ex, "Failed to create HttpClient for AlertingService: {ErrorMessage}", ex.Message);
        }
    }

    /// <summary>
    /// Validates configuration services
    /// </summary>
    private async Task ValidateConfigurationServicesAsync(StartupValidationResult result)
    {
        _logger.LogDebug("Validating configuration services...");

        // Validate error recovery configuration
        await ValidateServiceAsync<ErrorRecoveryConfig>(result, "ErrorRecoveryConfig", async config =>
        {
            _logger.LogInformation("ErrorRecoveryConfig validation: " +
                "MaxRetryAttempts={MaxRetryAttempts}, " +
                "BaseRetryDelay={BaseRetryDelay}ms, " +
                "MaxRetryDelay={MaxRetryDelay}ms",
                config.MaxRetryAttempts,
                config.BaseRetryDelay.TotalMilliseconds,
                config.MaxRetryDelay.TotalMilliseconds);
        });

        // Validate SSL configuration (if registered)
        try
        {
            var sslConfig = _serviceProvider.GetService<SslConfiguration>();
            if (sslConfig != null)
            {
                result.ValidatedServices["SslConfiguration"] = "Successfully resolved";
                _logger.LogInformation("SslConfiguration validation: " +
                    "UseSSL={UseSSL}, " +
                    "ValidateServerCertificate={ValidateServerCertificate}, " +
                    "AllowSelfSignedCertificates={AllowSelfSignedCertificates}",
                    sslConfig.UseSSL,
                    sslConfig.ValidateServerCertificate,
                    sslConfig.AllowSelfSignedCertificates);
            }
            else
            {
                _logger.LogDebug("SslConfiguration not registered (optional service)");
            }
        }
        catch (Exception ex)
        {
            result.FailedServices["SslConfiguration"] = ex.Message;
            _logger.LogError(ex, "Failed to resolve SslConfiguration: {ErrorMessage}", ex.Message);
        }
    }

    /// <summary>
    /// Validates repository services
    /// </summary>
    private async Task ValidateRepositoryServicesAsync(StartupValidationResult result)
    {
        _logger.LogDebug("Validating repository services...");

        await ValidateServiceAsync<IBackupConfigurationRepository>(result, "IBackupConfigurationRepository");
        await ValidateServiceAsync<IBackupLogRepository>(result, "IBackupLogRepository");
        await ValidateServiceAsync<IRetentionPolicyRepository>(result, "IRetentionPolicyRepository");
        await ValidateServiceAsync<IResumeTokenRepository>(result, "IResumeTokenRepository");
        await ValidateServiceAsync<IScheduleConfigurationRepository>(result, "IScheduleConfigurationRepository");
    }

    /// <summary>
    /// Validates business services
    /// </summary>
    private async Task ValidateBusinessServicesAsync(StartupValidationResult result)
    {
        _logger.LogDebug("Validating business services...");

        await ValidateServiceAsync<IBackupLogService>(result, "IBackupLogService");
        await ValidateServiceAsync<BackupReportingService>(result, "BackupReportingService");
        await ValidateServiceAsync<IRetentionPolicyService>(result, "IRetentionPolicyService");
        await ValidateServiceAsync<RetentionManagementService>(result, "RetentionManagementService");
        await ValidateServiceAsync<INetworkRetryService>(result, "INetworkRetryService");
        await ValidateServiceAsync<IAuthenticationService>(result, "IAuthenticationService");
        await ValidateServiceAsync<IAuthorizationService>(result, "IAuthorizationService");
        await ValidateServiceAsync<IErrorRecoveryManager>(result, "IErrorRecoveryManager");
    }

    /// <summary>
    /// Validates a specific service type with enhanced error handling and dependency chain analysis
    /// </summary>
    private async Task ValidateServiceAsync<T>(
        StartupValidationResult result, 
        string serviceName, 
        Func<T, Task>? additionalValidation = null) where T : class
    {
        try
        {
            var service = _serviceProvider.GetRequiredService<T>();
            result.ValidatedServices[serviceName] = "Successfully resolved";
            
            _logger.LogDebug("Service validation passed: {ServiceName} ({ServiceType})", 
                serviceName, typeof(T).Name);

            // Run additional validation if provided
            if (additionalValidation != null)
            {
                await additionalValidation(service);
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Unable to resolve service"))
        {
            // Enhanced error handling for dependency resolution failures
            var enhancedError = AnalyzeDependencyResolutionError<T>(ex);
            result.FailedServices[serviceName] = enhancedError;
            
            _logger.LogError(ex, "Service validation failed with dependency resolution error: {ServiceName} ({ServiceType}) - {EnhancedError}", 
                serviceName, typeof(T).Name, enhancedError);
        }
        catch (Exception ex)
        {
            result.FailedServices[serviceName] = ex.Message;
            _logger.LogError(ex, "Service validation failed: {ServiceName} ({ServiceType}) - {ErrorMessage}", 
                serviceName, typeof(T).Name, ex.Message);
        }
    }

    /// <summary>
    /// Analyzes dependency resolution errors to provide detailed information about missing dependencies
    /// </summary>
    private string AnalyzeDependencyResolutionError<T>(InvalidOperationException ex) where T : class
    {
        var serviceType = typeof(T);
        var errorMessage = ex.Message;
        
        // Build detailed error information
        var errorDetails = new StringBuilder();
        errorDetails.AppendLine($"Failed to resolve service '{serviceType.Name}':");
        
        // Extract dependency chain from the error message
        var dependencyChain = ExtractDependencyChainFromException(ex);
        if (dependencyChain.Any())
        {
            errorDetails.AppendLine($"Dependency chain: {string.Join(" → ", dependencyChain)}");
        }
        
        // Analyze constructor dependencies
        var constructorInfo = AnalyzeConstructorDependencies(serviceType);
        if (!string.IsNullOrEmpty(constructorInfo))
        {
            errorDetails.AppendLine($"Constructor analysis: {constructorInfo}");
        }
        
        // Provide specific guidance
        var guidance = GetServiceSpecificGuidance<T>(errorMessage);
        if (!string.IsNullOrEmpty(guidance))
        {
            errorDetails.AppendLine($"Resolution guidance: {guidance}");
        }
        
        // Include original error for reference
        errorDetails.AppendLine($"Original error: {errorMessage}");
        
        return errorDetails.ToString().TrimEnd();
    }

    /// <summary>
    /// Extracts dependency chain information from exception details
    /// </summary>
    private List<string> ExtractDependencyChainFromException(Exception ex)
    {
        var dependencyChain = new List<string>();
        var currentException = ex;
        
        while (currentException != null)
        {
            var message = currentException.Message;
            
            // Look for dependency resolution patterns
            var patterns = new[]
            {
                @"Unable to resolve service for type '([^']+)' while attempting to activate '([^']+)'",
                @"A suitable constructor for type '([^']+)' could not be located",
                @"No service for type '([^']+)' has been registered"
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(message, pattern);
                if (match.Success)
                {
                    for (int i = 1; i < match.Groups.Count; i++)
                    {
                        var typeName = match.Groups[i].Value;
                        if (!dependencyChain.Contains(typeName))
                        {
                            dependencyChain.Add(typeName);
                        }
                    }
                }
            }
            
            currentException = currentException.InnerException;
        }
        
        return dependencyChain;
    }

    /// <summary>
    /// Analyzes constructor dependencies for a service type
    /// </summary>
    private string AnalyzeConstructorDependencies(Type serviceType)
    {
        try
        {
            var constructors = serviceType.GetConstructors();
            if (!constructors.Any())
            {
                return "No public constructors found";
            }

            var primaryConstructor = constructors.OrderByDescending(c => c.GetParameters().Length).First();
            var parameters = primaryConstructor.GetParameters();
            
            if (!parameters.Any())
            {
                return "Default constructor (no dependencies)";
            }

            var dependencies = parameters.Select(p => p.ParameterType.Name).ToList();
            return $"Requires dependencies: {string.Join(", ", dependencies)}";
        }
        catch (Exception ex)
        {
            return $"Failed to analyze constructor: {ex.Message}";
        }
    }

    /// <summary>
    /// Provides service-specific guidance for resolving dependency issues
    /// </summary>
    private string GetServiceSpecificGuidance<T>(string errorMessage) where T : class
    {
        var serviceType = typeof(T);
        var serviceName = serviceType.Name;

        // AlertingService specific guidance
        if (serviceName == "AlertingService" || serviceType.GetInterfaces().Any(i => i.Name == "IAlertingService"))
        {
            if (errorMessage.Contains("HttpClient"))
            {
                return "Register HttpClient using services.AddHttpClient<AlertingService>() in AddSharedServices method";
            }
            if (errorMessage.Contains("AlertingConfig"))
            {
                return "Register AlertingConfig as singleton with configuration binding in AddSharedServices method";
            }
            return "Ensure both HttpClient and AlertingConfig are registered for AlertingService";
        }

        // HttpClient specific guidance
        if (serviceName.Contains("HttpClient") || errorMessage.Contains("HttpClient"))
        {
            return "Register HttpClient using services.AddHttpClient() or services.AddHttpClient<T>() method";
        }

        // Configuration services guidance
        if (serviceName.EndsWith("Config") || serviceName.Contains("Configuration"))
        {
            return "Register configuration objects as singletons with proper configuration section binding";
        }

        // Repository services guidance
        if (serviceName.EndsWith("Repository") || serviceType.GetInterfaces().Any(i => i.Name.EndsWith("Repository")))
        {
            return "Ensure DbContext is registered and repository is registered as scoped service";
        }

        // Logger guidance
        if (serviceName.Contains("Logger") || serviceType.GetInterfaces().Any(i => i.Name.Contains("Logger")))
        {
            return "Logging services should be automatically available - ensure services.AddLogging() is called";
        }

        // Generic guidance
        return "Check service registration in AddSharedServices, AddClientServices, or AddServerServices methods";
    }
}

/// <summary>
/// Result of startup service validation
/// </summary>
public class StartupValidationResult
{
    /// <summary>
    /// Whether all services passed validation
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Services that were successfully validated
    /// </summary>
    public Dictionary<string, string> ValidatedServices { get; set; } = new();

    /// <summary>
    /// Services that failed validation with error messages
    /// </summary>
    public Dictionary<string, string> FailedServices { get; set; } = new();

    /// <summary>
    /// Time taken to complete validation
    /// </summary>
    public TimeSpan ValidationDuration { get; set; }

    /// <summary>
    /// Exception that occurred during validation (if any)
    /// </summary>
    public Exception? ValidationException { get; set; }

    /// <summary>
    /// Total number of services checked
    /// </summary>
    public int TotalServicesChecked => ValidatedServices.Count + FailedServices.Count;

    /// <summary>
    /// Success rate as a percentage
    /// </summary>
    public double SuccessRate => TotalServicesChecked > 0 
        ? (double)ValidatedServices.Count / TotalServicesChecked * 100 
        : 0;
}