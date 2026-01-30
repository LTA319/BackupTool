using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.DependencyInjection;

namespace MySqlBackupTool.Server;

internal class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("MySQL Backup Tool - File Receiver Server");
        Console.WriteLine("========================================");

        // Create host builder for dependency injection
        var hostBuilder = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Add shared services
                var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString("server_backup_tool.db");
                services.AddSharedServices(connectionString);
                
                // Add server-specific services
                var storagePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MySqlBackupTool", "Backups");
                
                //services.AddServerServices(storagePath);
                services.AddServerServices(storagePath,false);
                
                // Add retention policy background service (runs every 24 hours)
                services.AddRetentionPolicyBackgroundService(TimeSpan.FromHours(24));
                
                // Add hosted service for the file receiver
                services.AddHostedService<FileReceiverService>();
            });

        var host = hostBuilder.Build();

        try
        {
            // Initialize database
            await host.Services.InitializeDatabaseAsync();

            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("MySQL Backup Tool Server starting...");

            Console.WriteLine("Server is starting...");
            Console.WriteLine("Press Ctrl+C to stop the server");

            // Set up graceful shutdown
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                logger.LogInformation("Shutdown requested by user");
                host.StopAsync().Wait(TimeSpan.FromSeconds(30));
            };

            // Run the host
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            try
            {
                var logger = host.Services.GetService<ILogger<Program>>();
                logger?.LogCritical(ex, "Fatal error occurred during server startup");
            }
            catch
            {
                // Ignore logging errors if services are disposed
            }
            
            Console.WriteLine($"Fatal error: {ex.Message}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
        finally
        {
            Console.WriteLine("Server is shutting down...");
        }
    }
}