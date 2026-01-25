using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;

namespace MySqlBackupTool.Client;

internal static class Program
{
    /// <summary>
    /// The main entry point for the backup client application.
    /// </summary>
    [STAThread]
    static async Task Main()
    {
        // Configure application for high DPI settings
        ApplicationConfiguration.Initialize();

        // Create host builder for dependency injection
        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Add shared services
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString("client_backup_tool.db");
                services.AddSharedServices(connectionString);
                
                // Add client-specific services
                services.AddClientServices();
                
                // Add backup scheduling services (re-enabled)
                //services.AddBackupSchedulingServices();
            });

        var host = hostBuilder.Build();

        try
        {
            // Initialize database
            await host.Services.InitializeDatabaseAsync();

            // Get logger
            var logger = host.Services.GetRequiredService<ILogger<FormMain>>();
            logger.LogInformation("MySQL Backup Tool Client starting...");

            logger.LogInformation("Starting host services...");
            // Start the host services (background services, etc.)
            await host.StartAsync();

            logger.LogInformation("Host services started, creating main form...");

            // Run the Windows Forms application
            using var mainForm = new FormMain(host.Services);
            logger.LogInformation("Main form created, starting application...");
            Application.Run(mainForm);
        }
        catch (Exception ex)
        {
            var logger = host.Services.GetService<ILogger<FormMain>>();
            logger?.LogCritical(ex, "Fatal error occurred during application startup");
            
            MessageBox.Show($"A fatal error occurred during startup:\n\n{ex.Message}", 
                "MySQL Backup Tool - Fatal Error", 
                MessageBoxButtons.OK, 
                MessageBoxIcon.Error);
        }
        finally
        {
            // Gracefully shutdown all services
            try
            {
                await host.StopAsync(TimeSpan.FromSeconds(30));
            }
            catch (Exception ex)
            {
                var logger = host.Services.GetService<ILogger<FormMain>>();
                logger?.LogWarning(ex, "Error occurred during application shutdown");
            }
            finally
            {
                host.Dispose();
            }
        }
    }
}