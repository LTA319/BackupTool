using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Service for validating dependency resolution and providing detailed error information
/// </summary>
public class DependencyResolutionValidator
{
    private readonly ILogger<DependencyResolutionValidator> _logger;

    public DependencyResolutionValidator(ILogger<DependencyResolutionValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates that critical services can be resolved and provides detailed error information for failures
    /// </summary>
    public DependencyValidationResult ValidateCriticalServices(IServiceProvider serviceProvider)
    {
        var result = new DependencyValidationResult();
        var criticalServices = GetCriticalServiceTypes();

        _logger.LogInformation("Validating {ServiceCount} critical services for dependency resolution", criticalServices.Count);

        foreach (var serviceInfo in criticalServices)
        {
            try
            {
                var service = serviceProvider.GetRequiredService(serviceInfo.ServiceType);
                result.ValidServices.Add(serviceInfo.ServiceName);
                
                _logger.LogDebug("✅ Critical service resolved successfully: {ServiceName}", serviceInfo.ServiceName);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Unable to resolve service"))
            {
                var errorDetails = AnalyzeServiceResolutionFailure(serviceInfo, ex);
                result.FailedServices[serviceInfo.ServiceName] = errorDetails;
                
                _logger.LogError("❌ Critical service resolution failed: {ServiceName} - {ErrorSummary}", 
                    serviceInfo.ServiceName, errorDetails.ErrorSummary);
            }
            catch (Exception ex)
            {
                var errorDetails = new ServiceResolutionError
                {
                    ServiceName = serviceInfo.ServiceName,
                    ServiceType = serviceInfo.ServiceType,
                    ErrorSummary = ex.Message,
                    DetailedError = ex.ToString(),
                    ResolutionGuidance = "Unexpected error during service resolution. Check service registration and dependencies."
                };
                
                result.FailedServices[serviceInfo.ServiceName] = errorDetails;
                
                _logger.LogError(ex, "❌ Unexpected error resolving critical service: {ServiceName}", serviceInfo.ServiceName);
            }
        }

        result.IsValid = result.FailedServices.Count == 0;
        
        if (result.IsValid)
        {
            _logger.LogInformation("✅ All {ServiceCount} critical services resolved successfully", criticalServices.Count);
        }
        else
        {
            _logger.LogError("❌ {FailedCount} out of {TotalCount} critical services failed to resolve", 
                result.FailedServices.Count, criticalServices.Count);
        }

        return result;
    }

    /// <summary>
    /// Gets the list of critical service types that must be resolvable
    /// </summary>
    private List<ServiceInfo> GetCriticalServiceTypes()
    {
        return new List<ServiceInfo>
        {
            new("IAlertingService", typeof(Interfaces.IAlertingService)),
            new("AlertingConfig", typeof(Models.AlertingConfig)),
            new("IHttpClientFactory", typeof(System.Net.Http.IHttpClientFactory)),
            new("BackupDbContext", typeof(Data.BackupDbContext)),
            new("ILogger<T>", typeof(ILogger<DependencyResolutionValidator>)),
            new("ILoggingService", typeof(Interfaces.ILoggingService)),
            new("IValidationService", typeof(Interfaces.IValidationService)),
            new("IErrorRecoveryManager", typeof(Interfaces.IErrorRecoveryManager)),
            new("StartupValidationService", typeof(StartupValidationService))
        };
    }

    /// <summary>
    /// Analyzes a service resolution failure to provide detailed error information
    /// </summary>
    private ServiceResolutionError AnalyzeServiceResolutionFailure(ServiceInfo serviceInfo, InvalidOperationException ex)
    {
        var error = new ServiceResolutionError
        {
            ServiceName = serviceInfo.ServiceName,
            ServiceType = serviceInfo.ServiceType,
            ErrorSummary = ex.Message
        };

        // Analyze dependency chain
        error.DependencyChain = ExtractDependencyChain(ex);
        
        // Analyze constructor requirements
        error.ConstructorAnalysis = AnalyzeConstructorRequirements(serviceInfo.ServiceType);
        
        // Provide specific guidance
        error.ResolutionGuidance = GenerateResolutionGuidance(serviceInfo, ex.Message);
        
        // Build detailed error message
        error.DetailedError = BuildDetailedErrorMessage(error);

        return error;
    }

    /// <summary>
    /// Extracts dependency chain information from the exception
    /// </summary>
    private List<string> ExtractDependencyChain(Exception ex)
    {
        var chain = new List<string>();
        var currentException = ex;

        while (currentException != null)
        {
            var message = currentException.Message;
            
            // Extract type names from common DI error patterns
            var patterns = new[]
            {
                @"Unable to resolve service for type '([^']+)' while attempting to activate '([^']+)'",
                @"A suitable constructor for type '([^']+)' could not be located",
                @"No service for type '([^']+)' has been registered"
            };

            foreach (var pattern in patterns)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(message, pattern);
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    for (int i = 1; i < match.Groups.Count; i++)
                    {
                        var typeName = match.Groups[i].Value;
                        if (!chain.Contains(typeName))
                        {
                            chain.Add(typeName);
                        }
                    }
                }
            }

            currentException = currentException.InnerException;
        }

        return chain;
    }

    /// <summary>
    /// Analyzes constructor requirements for a service type
    /// </summary>
    private ConstructorAnalysis AnalyzeConstructorRequirements(Type serviceType)
    {
        var analysis = new ConstructorAnalysis
        {
            ServiceType = serviceType.Name
        };

        try
        {
            var constructors = serviceType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            
            if (!constructors.Any())
            {
                analysis.HasPublicConstructors = false;
                analysis.ErrorMessage = "No public constructors found";
                return analysis;
            }

            analysis.HasPublicConstructors = true;
            analysis.ConstructorCount = constructors.Length;

            // Analyze the primary constructor (usually the one with the most parameters)
            var primaryConstructor = constructors.OrderByDescending(c => c.GetParameters().Length).First();
            var parameters = primaryConstructor.GetParameters();

            analysis.RequiredDependencies = parameters.Select(p => new DependencyInfo
            {
                ParameterName = p.Name ?? "unknown",
                TypeName = p.ParameterType.Name,
                FullTypeName = p.ParameterType.FullName ?? p.ParameterType.Name,
                IsOptional = p.HasDefaultValue,
                DefaultValue = p.HasDefaultValue ? p.DefaultValue?.ToString() : null
            }).ToList();

            analysis.RequiredDependencyCount = parameters.Count(p => !p.HasDefaultValue);
            analysis.OptionalDependencyCount = parameters.Count(p => p.HasDefaultValue);

        }
        catch (Exception ex)
        {
            analysis.ErrorMessage = $"Failed to analyze constructors: {ex.Message}";
        }

        return analysis;
    }

    /// <summary>
    /// Generates specific resolution guidance based on the service and error
    /// </summary>
    private string GenerateResolutionGuidance(ServiceInfo serviceInfo, string errorMessage)
    {
        var serviceName = serviceInfo.ServiceName;
        var serviceType = serviceInfo.ServiceType;

        // AlertingService specific guidance
        if (serviceName == "IAlertingService")
        {
            if (errorMessage.Contains("HttpClient"))
            {
                return "AlertingService requires HttpClient. Add services.AddHttpClient<AlertingService>() in AddSharedServices method. " +
                       "Ensure this is called before services.AddScoped<IAlertingService, AlertingService>().";
            }
            if (errorMessage.Contains("AlertingConfig"))
            {
                return "AlertingService requires AlertingConfig. Ensure AlertingConfig is registered as a singleton with proper configuration binding in AddSharedServices method.";
            }
            return "AlertingService requires both HttpClient and AlertingConfig. Check that both dependencies are properly registered in AddSharedServices method.";
        }

        // AlertingConfig specific guidance
        if (serviceName == "AlertingConfig")
        {
            return "AlertingConfig should be registered as a singleton with configuration binding. " +
                   "Add services.AddSingleton<AlertingConfig>() with configuration.GetSection(\"Alerting\").Bind() in AddSharedServices method.";
        }

        // HttpClient specific guidance
        if (serviceName == "IHttpClientFactory" || errorMessage.Contains("HttpClient"))
        {
            return "HttpClient services require IHttpClientFactory. Add services.AddHttpClient() or services.AddHttpClient<T>() in service registration. " +
                   "For AlertingService, use services.AddHttpClient<AlertingService>().";
        }

        // Database context guidance
        if (serviceName == "BackupDbContext")
        {
            return "BackupDbContext requires Entity Framework registration. Ensure services.AddDbContext<BackupDbContext>() is called with proper connection string.";
        }

        // Repository guidance
        if (serviceName.EndsWith("Repository"))
        {
            return "Repository services require DbContext. Ensure Entity Framework is properly configured and the repository is registered as scoped.";
        }

        // Logger guidance
        if (serviceName.Contains("Logger"))
        {
            return "Logger services should be automatically available. Ensure services.AddLogging() is called during service registration.";
        }

        // Generic guidance
        return $"Service '{serviceName}' is not registered in the DI container. " +
               "Add the appropriate service registration in AddSharedServices, AddClientServices, or AddServerServices methods. " +
               "Check that all constructor dependencies are also registered.";
    }

    /// <summary>
    /// Builds a detailed error message from the error analysis
    /// </summary>
    private string BuildDetailedErrorMessage(ServiceResolutionError error)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"Service Resolution Failure: {error.ServiceName}");
        sb.AppendLine($"Service Type: {error.ServiceType.FullName}");
        sb.AppendLine();
        
        if (error.DependencyChain.Any())
        {
            sb.AppendLine($"Dependency Chain: {string.Join(" → ", error.DependencyChain)}");
            sb.AppendLine();
        }

        if (error.ConstructorAnalysis != null)
        {
            sb.AppendLine("Constructor Analysis:");
            if (error.ConstructorAnalysis.HasPublicConstructors)
            {
                sb.AppendLine($"  • Public constructors: {error.ConstructorAnalysis.ConstructorCount}");
                sb.AppendLine($"  • Required dependencies: {error.ConstructorAnalysis.RequiredDependencyCount}");
                sb.AppendLine($"  • Optional dependencies: {error.ConstructorAnalysis.OptionalDependencyCount}");
                
                if (error.ConstructorAnalysis.RequiredDependencies.Any())
                {
                    sb.AppendLine("  • Dependencies:");
                    foreach (var dep in error.ConstructorAnalysis.RequiredDependencies)
                    {
                        var optional = dep.IsOptional ? " (optional)" : " (required)";
                        sb.AppendLine($"    - {dep.TypeName} {dep.ParameterName}{optional}");
                    }
                }
            }
            else
            {
                sb.AppendLine($"  • Error: {error.ConstructorAnalysis.ErrorMessage}");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"Resolution Guidance: {error.ResolutionGuidance}");
        sb.AppendLine();
        sb.AppendLine($"Original Error: {error.ErrorSummary}");

        return sb.ToString();
    }

    /// <summary>
    /// Information about a service for validation
    /// </summary>
    private record ServiceInfo(string ServiceName, Type ServiceType);
}

/// <summary>
/// Result of dependency validation
/// </summary>
public class DependencyValidationResult
{
    public bool IsValid { get; set; }
    public List<string> ValidServices { get; set; } = new();
    public Dictionary<string, ServiceResolutionError> FailedServices { get; set; } = new();
}

/// <summary>
/// Detailed information about a service resolution error
/// </summary>
public class ServiceResolutionError
{
    public string ServiceName { get; set; } = string.Empty;
    public Type ServiceType { get; set; } = typeof(object);
    public string ErrorSummary { get; set; } = string.Empty;
    public string DetailedError { get; set; } = string.Empty;
    public string ResolutionGuidance { get; set; } = string.Empty;
    public List<string> DependencyChain { get; set; } = new();
    public ConstructorAnalysis? ConstructorAnalysis { get; set; }
}

/// <summary>
/// Analysis of constructor requirements for a service
/// </summary>
public class ConstructorAnalysis
{
    public string ServiceType { get; set; } = string.Empty;
    public bool HasPublicConstructors { get; set; }
    public int ConstructorCount { get; set; }
    public int RequiredDependencyCount { get; set; }
    public int OptionalDependencyCount { get; set; }
    public List<DependencyInfo> RequiredDependencies { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Information about a constructor dependency
/// </summary>
public class DependencyInfo
{
    public string ParameterName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string FullTypeName { get; set; } = string.Empty;
    public bool IsOptional { get; set; }
    public string? DefaultValue { get; set; }
}