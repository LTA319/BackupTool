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
            });

        var host = hostBuilder.Build();

        try
        {
            // Initialize database
            await host.Services.InitializeDatabaseAsync();

            // Get logger
            var logger = host.Services.GetRequiredService<ILogger<FormMain>>();
            logger.LogInformation("MySQL Backup Tool Client starting...");

            // Run the Windows Forms application
            Application.Run(new FormMain(host.Services));
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
            await host.StopAsync();
            host.Dispose();
        }
    }
}