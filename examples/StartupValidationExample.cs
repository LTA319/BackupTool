using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;

namespace MySqlBackupTool.Examples;

/// <summary>
/// Example demonstrating how to use startup validation for service registration
/// </summary>
public class StartupValidationExample
{
    public static async Task Main(string[] args)
    {
        // Create host builder
        var hostBuilder = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Add shared services
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString("example.db");
                services.AddSharedServices(connectionString, context.Configuration);
                
                // Add other services as needed...
            });

        // Build the host
        using var host = hostBuilder.Build();

        // Validate services during startup
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<StartupValidationExample>>();
            logger.LogInformation("Starting application with service validation...");

            // Validate all services are properly registered
            var validationResult = await host.Services.ValidateServicesAsync();

            if (validationResult.IsValid)
            {
                logger.LogInformation("Service validation passed! All {ServiceCount} services are properly registered.",
                    validationResult.ValidatedServices.Count);
                
                // Initialize database
                await host.Services.InitializeDatabaseAsync();
                
                // Start the application
                await host.RunAsync();
            }
            else
            {
                logger.LogCritical("Service validation failed! {FailedCount} services failed validation:",
                    validationResult.FailedServices.Count);
                
                foreach (var (serviceName, error) in validationResult.FailedServices)
                {
                    logger.LogCritical("  - {ServiceName}: {Error}", serviceName, error);
                }
                
                logger.LogCritical("Application cannot start with missing dependencies. Please fix the service registration issues.");
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            var logger = host.Services.GetRequiredService<ILogger<StartupValidationExample>>();
            logger.LogCritical(ex, "Critical error during startup validation: {ErrorMessage}", ex.Message);
            Environment.Exit(1);
        }
    }
}

/// <summary>
/// Alternative approach using the ValidateServicesOrThrowAsync extension method
/// </summary>
public class StartupValidationExampleWithThrow
{
    public static async Task Main(string[] args)
    {
        var hostBuilder = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString("example.db");
                services.AddSharedServices(connectionString, context.Configuration);
            });

        using var host = hostBuilder.Build();

        try
        {
            var logger = host.Services.GetRequiredService<ILogger<StartupValidationExampleWithThrow>>();
            logger.LogInformation("Starting application with service validation...");

            // This will throw an exception if validation fails
            await host.Services.ValidateServicesOrThrowAsync();
            
            logger.LogInformation("Service validation passed! Starting application...");
            
            await host.Services.InitializeDatabaseAsync();
            await host.RunAsync();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Service validation failed"))
        {
            var logger = host.Services.GetRequiredService<ILogger<StartupValidationExampleWithThrow>>();
            logger.LogCritical("Service validation failed: {ErrorMessage}", ex.Message);
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            var logger = host.Services.GetRequiredService<ILogger<StartupValidationExampleWithThrow>>();
            logger.LogCritical(ex, "Critical error during startup: {ErrorMessage}", ex.Message);
            Environment.Exit(1);
        }
    }
}