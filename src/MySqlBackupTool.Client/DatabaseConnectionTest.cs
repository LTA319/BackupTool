using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Data;
using MySqlBackupTool.Shared.DependencyInjection;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Client;

/// <summary>
/// Utility class for testing database connectivity and diagnosing issues
/// </summary>
public static class DatabaseConnectionTest
{
    /// <summary>
    /// Tests database connectivity and returns diagnostic information
    /// </summary>
    public static async Task<DatabaseTestResult> TestDatabaseConnectionAsync()
    {
        var result = new DatabaseTestResult();
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Create a minimal service provider for testing
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            
            var connectionString = ServiceCollectionExtensions.CreateDefaultConnectionString("client_backup_tool.db");
            services.AddDbContext<BackupDbContext>(options => options.UseSqlite(connectionString));
            
            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            
            result.ConnectionString = connectionString;
            result.DatabasePath = Path.GetFullPath("client_backup_tool.db");
            result.DatabaseExists = File.Exists(result.DatabasePath);
            
            if (result.DatabaseExists)
            {
                var fileInfo = new FileInfo(result.DatabasePath);
                result.DatabaseSize = fileInfo.Length;
                result.LastModified = fileInfo.LastWriteTime;
            }
            
            // Test database context creation
            var context = scope.ServiceProvider.GetRequiredService<BackupDbContext>();
            result.ContextCreated = true;
            
            // Test database connection
            var connectionTestStart = DateTime.UtcNow;
            await context.Database.OpenConnectionAsync();
            result.ConnectionTime = DateTime.UtcNow - connectionTestStart;
            result.CanConnect = true;
            
            // Test basic query
            var queryTestStart = DateTime.UtcNow;
            var configCount = await context.BackupConfigurations.CountAsync();
            result.QueryTime = DateTime.UtcNow - queryTestStart;
            result.ConfigurationCount = configCount;
            result.CanQuery = true;
            
            // Test running backups query specifically
            var runningBackupsTestStart = DateTime.UtcNow;
            var runningBackupsCount = await context.BackupLogs
                .Where(bl => new[] { BackupStatus.Queued, BackupStatus.StoppingMySQL, BackupStatus.Compressing, 
                                   BackupStatus.Transferring, BackupStatus.StartingMySQL, BackupStatus.Verifying }
                           .Contains(bl.Status))
                .CountAsync();
            result.RunningBackupsQueryTime = DateTime.UtcNow - runningBackupsTestStart;
            result.RunningBackupsCount = runningBackupsCount;
            
            await context.Database.CloseConnectionAsync();
            
            result.Success = true;
            result.Message = "Database connection test completed successfully";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex;
            result.Message = $"Database connection test failed: {ex.Message}";
        }
        finally
        {
            result.TotalTime = DateTime.UtcNow - startTime;
        }
        
        return result;
    }
    
    /// <summary>
    /// Attempts to repair common database issues
    /// </summary>
    public static async Task<DatabaseRepairResult> RepairDatabaseAsync()
    {
        var result = new DatabaseRepairResult();
        var actions = new List<string>();
        
        try
        {
            var databasePath = Path.GetFullPath("client_backup_tool.db");
            
            // Check for lock files
            var lockFiles = new[] { 
                databasePath + "-wal", 
                databasePath + "-shm",
                databasePath + "-journal"
            };
            
            foreach (var lockFile in lockFiles)
            {
                if (File.Exists(lockFile))
                {
                    try
                    {
                        File.Delete(lockFile);
                        actions.Add($"Deleted lock file: {Path.GetFileName(lockFile)}");
                    }
                    catch (Exception ex)
                    {
                        actions.Add($"Failed to delete lock file {Path.GetFileName(lockFile)}: {ex.Message}");
                    }
                }
            }
            
            // Test connection after cleanup
            var testResult = await TestDatabaseConnectionAsync();
            result.TestResult = testResult;
            result.Success = testResult.Success;
            result.ActionsPerformed = actions;
            
            if (result.Success)
            {
                result.Message = "Database repair completed successfully";
            }
            else
            {
                result.Message = $"Database repair failed: {testResult.Message}";
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex;
            result.Message = $"Database repair failed: {ex.Message}";
            result.ActionsPerformed = actions;
        }
        
        return result;
    }
}

/// <summary>
/// Result of database connection test
/// </summary>
public class DatabaseTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public Exception? Error { get; set; }
    public TimeSpan TotalTime { get; set; }
    
    public string ConnectionString { get; set; } = "";
    public string DatabasePath { get; set; } = "";
    public bool DatabaseExists { get; set; }
    public long DatabaseSize { get; set; }
    public DateTime LastModified { get; set; }
    
    public bool ContextCreated { get; set; }
    public bool CanConnect { get; set; }
    public TimeSpan ConnectionTime { get; set; }
    
    public bool CanQuery { get; set; }
    public TimeSpan QueryTime { get; set; }
    public int ConfigurationCount { get; set; }
    
    public TimeSpan RunningBackupsQueryTime { get; set; }
    public int RunningBackupsCount { get; set; }
    
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Database Test Result: {(Success ? "SUCCESS" : "FAILED")}");
        sb.AppendLine($"Message: {Message}");
        sb.AppendLine($"Total Time: {TotalTime.TotalMilliseconds:F0}ms");
        sb.AppendLine();
        sb.AppendLine($"Database Path: {DatabasePath}");
        sb.AppendLine($"Database Exists: {DatabaseExists}");
        if (DatabaseExists)
        {
            sb.AppendLine($"Database Size: {DatabaseSize:N0} bytes");
            sb.AppendLine($"Last Modified: {LastModified:yyyy-MM-dd HH:mm:ss}");
        }
        sb.AppendLine();
        sb.AppendLine($"Context Created: {ContextCreated}");
        sb.AppendLine($"Can Connect: {CanConnect}");
        if (CanConnect)
        {
            sb.AppendLine($"Connection Time: {ConnectionTime.TotalMilliseconds:F0}ms");
        }
        sb.AppendLine($"Can Query: {CanQuery}");
        if (CanQuery)
        {
            sb.AppendLine($"Query Time: {QueryTime.TotalMilliseconds:F0}ms");
            sb.AppendLine($"Configuration Count: {ConfigurationCount}");
            sb.AppendLine($"Running Backups Query Time: {RunningBackupsQueryTime.TotalMilliseconds:F0}ms");
            sb.AppendLine($"Running Backups Count: {RunningBackupsCount}");
        }
        
        if (Error != null)
        {
            sb.AppendLine();
            sb.AppendLine($"Error: {Error.Message}");
            if (Error.InnerException != null)
            {
                sb.AppendLine($"Inner Error: {Error.InnerException.Message}");
            }
        }
        
        return sb.ToString();
    }
}

/// <summary>
/// Result of database repair operation
/// </summary>
public class DatabaseRepairResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public Exception? Error { get; set; }
    public List<string> ActionsPerformed { get; set; } = new();
    public DatabaseTestResult? TestResult { get; set; }
    
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Database Repair Result: {(Success ? "SUCCESS" : "FAILED")}");
        sb.AppendLine($"Message: {Message}");
        
        if (ActionsPerformed.Any())
        {
            sb.AppendLine();
            sb.AppendLine("Actions Performed:");
            foreach (var action in ActionsPerformed)
            {
                sb.AppendLine($"  • {action}");
            }
        }
        
        if (TestResult != null)
        {
            sb.AppendLine();
            sb.AppendLine("Post-Repair Test Results:");
            sb.AppendLine(TestResult.ToString());
        }
        
        if (Error != null)
        {
            sb.AppendLine();
            sb.AppendLine($"Error: {Error.Message}");
        }
        
        return sb.ToString();
    }
}