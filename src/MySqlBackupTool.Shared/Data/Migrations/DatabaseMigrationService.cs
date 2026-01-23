using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MySqlBackupTool.Shared.Data.Migrations;

/// <summary>
/// Service for managing database migrations and initialization
/// </summary>
public class DatabaseMigrationService
{
    private readonly BackupDbContext _context;
    private readonly ILogger<DatabaseMigrationService> _logger;

    public DatabaseMigrationService(BackupDbContext context, ILogger<DatabaseMigrationService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes the database, creating it if it doesn't exist and applying any pending migrations
    /// </summary>
    public async Task InitializeDatabaseAsync()
    {
        try
        {
            _logger.LogInformation("Initializing database...");

            // Ensure the database is created
            var created = await _context.Database.EnsureCreatedAsync();
            if (created)
            {
                _logger.LogInformation("Database created successfully");
            }
            else
            {
                _logger.LogInformation("Database already exists");
            }

            // Apply any pending migrations
            var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                _logger.LogInformation("Applying {Count} pending migrations: {Migrations}", 
                    pendingMigrations.Count(), 
                    string.Join(", ", pendingMigrations));
                
                await _context.Database.MigrateAsync();
                _logger.LogInformation("Migrations applied successfully");
            }
            else
            {
                _logger.LogInformation("No pending migrations found");
            }

            // Seed default data
            await SeedDefaultDataAsync();

            _logger.LogInformation("Database initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing database");
            throw;
        }
    }

    /// <summary>
    /// Seeds the database with default data if needed
    /// </summary>
    public async Task SeedDefaultDataAsync()
    {
        try
        {
            _logger.LogInformation("Seeding default data...");

            // Add default retention policy if none exists
            if (!await _context.RetentionPolicies.AnyAsync())
            {
                var defaultPolicy = new Models.RetentionPolicy
                {
                    Name = "Default Policy",
                    Description = "Keep backups for 30 days or maximum 10 backups",
                    MaxAgeDays = 30,
                    MaxCount = 10,
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.RetentionPolicies.Add(defaultPolicy);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Default retention policy created");
            }

            _logger.LogInformation("Default data seeding completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding default data");
            throw;
        }
    }

    /// <summary>
    /// Checks if the database exists and is accessible
    /// </summary>
    public async Task<bool> CanConnectAsync()
    {
        try
        {
            return await _context.Database.CanConnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking database connectivity");
            return false;
        }
    }

    /// <summary>
    /// Gets information about the current database state
    /// </summary>
    public async Task<DatabaseInfo> GetDatabaseInfoAsync()
    {
        try
        {
            var info = new DatabaseInfo
            {
                CanConnect = await _context.Database.CanConnectAsync(),
                DatabaseExists = await _context.Database.CanConnectAsync(),
                PendingMigrations = (await _context.Database.GetPendingMigrationsAsync()).ToList(),
                AppliedMigrations = (await _context.Database.GetAppliedMigrationsAsync()).ToList()
            };

            if (info.CanConnect)
            {
                info.ConfigurationCount = await _context.BackupConfigurations.CountAsync();
                info.BackupLogCount = await _context.BackupLogs.CountAsync();
                info.RetentionPolicyCount = await _context.RetentionPolicies.CountAsync();
            }

            return info;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting database information");
            return new DatabaseInfo
            {
                CanConnect = false,
                DatabaseExists = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Creates a backup of the database
    /// </summary>
    public async Task<bool> BackupDatabaseAsync(string backupPath)
    {
        try
        {
            _logger.LogInformation("Creating database backup at {BackupPath}", backupPath);

            // For SQLite, we can simply copy the database file
            var connectionString = _context.Database.GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError("Connection string is null or empty");
                return false;
            }

            // Extract the database file path from the connection string
            var dbPath = ExtractDatabasePath(connectionString);
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                _logger.LogError("Database file not found at {DbPath}", dbPath);
                return false;
            }

            // Ensure the backup directory exists
            var backupDirectory = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrEmpty(backupDirectory) && !Directory.Exists(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
            }

            // Copy the database file
            File.Copy(dbPath, backupPath, overwrite: true);
            
            _logger.LogInformation("Database backup created successfully at {BackupPath}", backupPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating database backup");
            return false;
        }
    }

    /// <summary>
    /// Restores a database from a backup
    /// </summary>
    public async Task<bool> RestoreDatabaseAsync(string backupPath)
    {
        try
        {
            if (!File.Exists(backupPath))
            {
                _logger.LogError("Backup file not found at {BackupPath}", backupPath);
                return false;
            }

            _logger.LogInformation("Restoring database from backup at {BackupPath}", backupPath);

            var connectionString = _context.Database.GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError("Connection string is null or empty");
                return false;
            }

            var dbPath = ExtractDatabasePath(connectionString);
            if (string.IsNullOrEmpty(dbPath))
            {
                _logger.LogError("Could not extract database path from connection string");
                return false;
            }

            // Close any existing connections
            await _context.Database.CloseConnectionAsync();

            // Copy the backup file over the current database
            File.Copy(backupPath, dbPath, overwrite: true);

            _logger.LogInformation("Database restored successfully from {BackupPath}", backupPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring database from backup");
            return false;
        }
    }

    private static string? ExtractDatabasePath(string connectionString)
    {
        // Simple extraction for SQLite connection strings
        // Format: "Data Source=path/to/database.db" or "DataSource=path/to/database.db"
        var parts = connectionString.Split(';');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring("Data Source=".Length);
            }
            if (trimmed.StartsWith("DataSource=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring("DataSource=".Length);
            }
        }
        return null;
    }
}

/// <summary>
/// Information about the current database state
/// </summary>
public class DatabaseInfo
{
    public bool CanConnect { get; set; }
    public bool DatabaseExists { get; set; }
    public List<string> PendingMigrations { get; set; } = new();
    public List<string> AppliedMigrations { get; set; } = new();
    public int ConfigurationCount { get; set; }
    public int BackupLogCount { get; set; }
    public int RetentionPolicyCount { get; set; }
    public string? Error { get; set; }

    public bool HasPendingMigrations => PendingMigrations.Count > 0;
    public bool IsUpToDate => !HasPendingMigrations && CanConnect;
}