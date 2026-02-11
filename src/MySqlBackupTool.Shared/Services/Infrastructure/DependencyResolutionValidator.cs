using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 验证依赖解析并提供详细错误信息的服务 / Service for validating dependency resolution and providing detailed error information
/// </summary>
public class DependencyResolutionValidator
{
    private readonly ILogger<DependencyResolutionValidator> _logger;

    /// <summary>
    /// 初始化依赖解析验证器 / Initialize dependency resolution validator
    /// </summary>
    /// <param name="logger">日志记录器 / Logger instance</param>
    /// <exception cref="ArgumentNullException">当logger为null时抛出 / Thrown when logger is null</exception>
    public DependencyResolutionValidator(ILogger<DependencyResolutionValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 验证关键服务是否可以解析，并为失败的服务提供详细错误信息 / Validates that critical services can be resolved and provides detailed error information for failures
    /// </summary>
    /// <param name="serviceProvider">服务提供者实例 / Service provider instance</param>
    /// <returns>包含验证结果和错误详情的验证结果 / Validation result containing success status and error details</returns>
    public DependencyValidationResult ValidateCriticalServices(IServiceProvider serviceProvider)
    {
        var result = new DependencyValidationResult();
        var criticalServices = GetCriticalServiceTypes();

        // 记录开始验证关键服务 / Log start of critical services validation
        _logger.LogInformation("Validating {ServiceCount} critical services for dependency resolution", criticalServices.Count);

        foreach (var serviceInfo in criticalServices)
        {
            try
            {
                // 尝试解析服务 / Attempt to resolve service
                var service = serviceProvider.GetRequiredService(serviceInfo.ServiceType);
                result.ValidServices.Add(serviceInfo.ServiceName);
                
                _logger.LogDebug("✅ Critical service resolved successfully: {ServiceName}", serviceInfo.ServiceName);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Unable to resolve service"))
            {
                // 分析服务解析失败的详细原因 / Analyze detailed reasons for service resolution failure
                var errorDetails = AnalyzeServiceResolutionFailure(serviceInfo, ex);
                result.FailedServices[serviceInfo.ServiceName] = errorDetails;
                
                _logger.LogError("❌ Critical service resolution failed: {ServiceName} - {ErrorSummary}", 
                    serviceInfo.ServiceName, errorDetails.ErrorSummary);
            }
            catch (Exception ex)
            {
                // 处理意外错误 / Handle unexpected errors
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
    /// 获取必须可解析的关键服务类型列表 / Gets the list of critical service types that must be resolvable
    /// </summary>
    /// <returns>关键服务信息列表 / List of critical service information</returns>
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
    /// 分析服务解析失败以提供详细错误信息 / Analyzes a service resolution failure to provide detailed error information
    /// </summary>
    /// <param name="serviceInfo">服务信息 / Service information</param>
    /// <param name="ex">解析异常 / Resolution exception</param>
    /// <returns>详细的服务解析错误信息 / Detailed service resolution error information</returns>
    private ServiceResolutionError AnalyzeServiceResolutionFailure(ServiceInfo serviceInfo, InvalidOperationException ex)
    {
        var error = new ServiceResolutionError
        {
            ServiceName = serviceInfo.ServiceName,
            ServiceType = serviceInfo.ServiceType,
            ErrorSummary = ex.Message
        };

        // 分析依赖链 / Analyze dependency chain
        error.DependencyChain = ExtractDependencyChain(ex);
        
        // 分析构造函数要求 / Analyze constructor requirements
        error.ConstructorAnalysis = AnalyzeConstructorRequirements(serviceInfo.ServiceType);
        
        // 提供具体指导 / Provide specific guidance
        error.ResolutionGuidance = GenerateResolutionGuidance(serviceInfo, ex.Message);
        
        // 构建详细错误消息 / Build detailed error message
        error.DetailedError = BuildDetailedErrorMessage(error);

        return error;
    }

    /// <summary>
    /// 从异常中提取依赖链信息 / Extracts dependency chain information from the exception
    /// </summary>
    /// <param name="ex">包含依赖信息的异常 / Exception containing dependency information</param>
    /// <returns>依赖链类型名称列表 / List of dependency chain type names</returns>
    private List<string> ExtractDependencyChain(Exception ex)
    {
        var chain = new List<string>();
        var currentException = ex;

        while (currentException != null)
        {
            var message = currentException.Message;
            
            // 从常见DI错误模式中提取类型名称 / Extract type names from common DI error patterns
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
    /// 分析服务类型的构造函数要求 / Analyzes constructor requirements for a service type
    /// </summary>
    /// <param name="serviceType">要分析的服务类型 / Service type to analyze</param>
    /// <returns>构造函数分析结果 / Constructor analysis result</returns>
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

            // 分析主构造函数（通常是参数最多的那个） / Analyze the primary constructor (usually the one with the most parameters)
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
    /// 基于服务和错误生成具体的解决指导 / Generates specific resolution guidance based on the service and error
    /// </summary>
    /// <param name="serviceInfo">服务信息 / Service information</param>
    /// <param name="errorMessage">错误消息 / Error message</param>
    /// <returns>解决问题的指导建议 / Resolution guidance recommendations</returns>
    private string GenerateResolutionGuidance(ServiceInfo serviceInfo, string errorMessage)
    {
        var serviceName = serviceInfo.ServiceName;
        var serviceType = serviceInfo.ServiceType;

        // AlertingService特定指导 / AlertingService specific guidance
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

        // AlertingConfig特定指导 / AlertingConfig specific guidance
        if (serviceName == "AlertingConfig")
        {
            return "AlertingConfig should be registered as a singleton with configuration binding. " +
                   "Add services.AddSingleton<AlertingConfig>() with configuration.GetSection(\"Alerting\").Bind() in AddSharedServices method.";
        }

        // HttpClient特定指导 / HttpClient specific guidance
        if (serviceName == "IHttpClientFactory" || errorMessage.Contains("HttpClient"))
        {
            return "HttpClient services require IHttpClientFactory. Add services.AddHttpClient() or services.AddHttpClient<T>() in service registration. " +
                   "For AlertingService, use services.AddHttpClient<AlertingService>().";
        }

        // 数据库上下文指导 / Database context guidance
        if (serviceName == "BackupDbContext")
        {
            return "BackupDbContext requires Entity Framework registration. Ensure services.AddDbContext<BackupDbContext>() is called with proper connection string.";
        }

        // 仓储指导 / Repository guidance
        if (serviceName.EndsWith("Repository"))
        {
            return "Repository services require DbContext. Ensure Entity Framework is properly configured and the repository is registered as scoped.";
        }

        // 日志器指导 / Logger guidance
        if (serviceName.Contains("Logger"))
        {
            return "Logger services should be automatically available. Ensure services.AddLogging() is called during service registration.";
        }

        // 通用指导 / Generic guidance
        return $"Service '{serviceName}' is not registered in the DI container. " +
               "Add the appropriate service registration in AddSharedServices, AddClientServices, or AddServerServices methods. " +
               "Check that all constructor dependencies are also registered.";
    }

    /// <summary>
    /// 从错误分析中构建详细错误消息 / Builds a detailed error message from the error analysis
    /// </summary>
    /// <param name="error">服务解析错误信息 / Service resolution error information</param>
    /// <returns>格式化的详细错误消息 / Formatted detailed error message</returns>
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
    /// 用于验证的服务信息 / Information about a service for validation
    /// </summary>
    /// <param name="ServiceName">服务名称 / Service name</param>
    /// <param name="ServiceType">服务类型 / Service type</param>
    private record ServiceInfo(string ServiceName, Type ServiceType);
}

/// <summary>
/// 依赖验证的结果 / Result of dependency validation
/// </summary>
public class DependencyValidationResult
{
    /// <summary>
    /// 验证是否通过 / Whether validation passed
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// 成功解析的服务列表 / List of successfully resolved services
    /// </summary>
    public List<string> ValidServices { get; set; } = new();
    
    /// <summary>
    /// 失败服务及其错误详情 / Failed services and their error details
    /// </summary>
    public Dictionary<string, ServiceResolutionError> FailedServices { get; set; } = new();
}

/// <summary>
/// 服务解析错误的详细信息 / Detailed information about a service resolution error
/// </summary>
public class ServiceResolutionError
{
    /// <summary>
    /// 服务名称 / Service name
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;
    
    /// <summary>
    /// 服务类型 / Service type
    /// </summary>
    public Type ServiceType { get; set; } = typeof(object);
    
    /// <summary>
    /// 错误摘要 / Error summary
    /// </summary>
    public string ErrorSummary { get; set; } = string.Empty;
    
    /// <summary>
    /// 详细错误信息 / Detailed error information
    /// </summary>
    public string DetailedError { get; set; } = string.Empty;
    
    /// <summary>
    /// 解决指导建议 / Resolution guidance recommendations
    /// </summary>
    public string ResolutionGuidance { get; set; } = string.Empty;
    
    /// <summary>
    /// 依赖链 / Dependency chain
    /// </summary>
    public List<string> DependencyChain { get; set; } = new();
    
    /// <summary>
    /// 构造函数分析结果 / Constructor analysis result
    /// </summary>
    public ConstructorAnalysis? ConstructorAnalysis { get; set; }
}

/// <summary>
/// 服务构造函数要求的分析 / Analysis of constructor requirements for a service
/// </summary>
public class ConstructorAnalysis
{
    /// <summary>
    /// 服务类型名称 / Service type name
    /// </summary>
    public string ServiceType { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否有公共构造函数 / Whether has public constructors
    /// </summary>
    public bool HasPublicConstructors { get; set; }
    
    /// <summary>
    /// 构造函数数量 / Number of constructors
    /// </summary>
    public int ConstructorCount { get; set; }
    
    /// <summary>
    /// 必需依赖数量 / Number of required dependencies
    /// </summary>
    public int RequiredDependencyCount { get; set; }
    
    /// <summary>
    /// 可选依赖数量 / Number of optional dependencies
    /// </summary>
    public int OptionalDependencyCount { get; set; }
    
    /// <summary>
    /// 必需依赖列表 / List of required dependencies
    /// </summary>
    public List<DependencyInfo> RequiredDependencies { get; set; } = new();
    
    /// <summary>
    /// 错误消息（如果分析失败） / Error message (if analysis failed)
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 构造函数依赖的信息 / Information about a constructor dependency
/// </summary>
public class DependencyInfo
{
    /// <summary>
    /// 参数名称 / Parameter name
    /// </summary>
    public string ParameterName { get; set; } = string.Empty;
    
    /// <summary>
    /// 类型名称 / Type name
    /// </summary>
    public string TypeName { get; set; } = string.Empty;
    
    /// <summary>
    /// 完整类型名称 / Full type name
    /// </summary>
    public string FullTypeName { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否为可选参数 / Whether parameter is optional
    /// </summary>
    public bool IsOptional { get; set; }
    
    /// <summary>
    /// 默认值（如果有） / Default value (if any)
    /// </summary>
    public string? DefaultValue { get; set; }
}