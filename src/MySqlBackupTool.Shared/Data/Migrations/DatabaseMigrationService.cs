using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Data.Migrations;

/// <summary>
/// Service for managing database migrations and initialization
/// </summary>
public class DatabaseMigrationService
{
    private readonly BackupDbContext _context;
    private readonly ILogger<DatabaseMigrationService> _logger;
    private readonly ICredentialStorage _credentialStorage;

    public DatabaseMigrationService(
        BackupDbContext context, 
        ILogger<DatabaseMigrationService> logger,
        ICredentialStorage credentialStorage)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _credentialStorage = credentialStorage ?? throw new ArgumentNullException(nameof(credentialStorage));
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

            // Create default client credentials first
            await SeedDefaultClientCredentialsAsync();

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

            // Add default backup configuration if none exists
            await SeedDefaultBackupConfigurationAsync();

            _logger.LogInformation("Default data seeding completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding default data");
            throw;
        }
    }

    /// <summary>
    /// Seeds default client credentials for testing and initial setup
    /// </summary>
    private async Task SeedDefaultClientCredentialsAsync()
    {
        try
        {
            // Check if any client credentials exist
            var existingClients = await _credentialStorage.ListClientIdsAsync();
            
            // Check if default client exists and has correct permissions
            var defaultClientExists = existingClients.Contains("default-client");
            if (defaultClientExists)
            {
                var existingClient = await _credentialStorage.GetCredentialsAsync("default-client");
                if (existingClient != null)
                {
                    // Check if permissions are correct (using new format)
                    var hasCorrectPermissions = existingClient.Permissions.Contains(BackupPermissions.UploadBackup);
                    if (!hasCorrectPermissions)
                    {
                        _logger.LogInformation("Updating default client credentials with correct permissions");
                        
                        // Update with correct permissions
                        existingClient.Permissions = new List<string>
                        {
                            BackupPermissions.UploadBackup,
                            BackupPermissions.DownloadBackup,
                            BackupPermissions.ListBackups,
                            BackupPermissions.DeleteBackup
                        };
                        
                        await _credentialStorage.UpdateCredentialsAsync(existingClient);
                        _logger.LogInformation("Default client credentials updated with correct permissions");
                        return;
                    }
                    else
                    {
                        _logger.LogDebug("Default client credentials already exist with correct permissions");
                        return;
                    }
                }
            }
            
            if (existingClients.Any() && !defaultClientExists)
            {
                _logger.LogDebug("Other client credentials exist, skipping default credential creation");
                return;
            }

            // Create default client credentials
            var defaultClient = new ClientCredentials
            {
                ClientId = "default-client",
                ClientSecret = "default-secret-2024", // In production, this should be randomly generated
                ClientName = "Default Backup Client",
                Permissions = new List<string>
                {
                    BackupPermissions.UploadBackup,
                    BackupPermissions.DownloadBackup,
                    BackupPermissions.ListBackups,
                    BackupPermissions.DeleteBackup
                },
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = null // No expiration for default client
            };

            var success = await _credentialStorage.StoreCredentialsAsync(defaultClient);
            if (success)
            {
                _logger.LogInformation("Default client credentials created successfully for client '{ClientId}'", defaultClient.ClientId);
                _logger.LogWarning("SECURITY WARNING: Default client credentials are being used. Please change them in production!");
            }
            else
            {
                _logger.LogError("Failed to create default client credentials");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating default client credentials");
            // Don't throw here as this is not critical for database initialization
        }
    }

    /// <summary>
    /// Seeds default backup configuration for testing and initial setup
    /// </summary>
    private async Task SeedDefaultBackupConfigurationAsync()
    {
        try
        {
            // Check if any backup configurations exist
            if (await _context.BackupConfigurations.AnyAsync())
            {
                _logger.LogDebug("Backup configurations already exist, skipping default configuration creation");
                return;
            }

            // Create default backup configuration
            var defaultConfig = new BackupConfiguration
            {
                Name = "Default Configuration",
                MySQLConnection = new MySQLConnectionInfo
                {
                    Host = "localhost",
                    Port = 3306,
                    Username = "root",
                    Password = "", // Empty password for default setup
                    ServiceName = "MySQL80",
                    DataDirectoryPath = @"C:\ProgramData\MySQL\MySQL Server 8.0\Data"
                },
                DataDirectoryPath = @"C:\ProgramData\MySQL\MySQL Server 8.0\Data",
                ServiceName = "MySQL80",
                TargetServer = new ServerEndpoint
                {
                    IPAddress = "127.0.0.1",
                    Port = 8080,
                    UseSSL = false,
                    RequireAuthentication = true,
                    ClientCredentials = new ClientCredentials
                    {
                        ClientId = "default-client",
                        ClientSecret = "default-secret-2024",
                        ClientName = "Default Backup Client"
                    }
                },
                TargetDirectory = "backups",
                NamingStrategy = new FileNamingStrategy
                {
                    Pattern = "{server}_{database}_{timestamp}.zip",
                    DateFormat = "yyyyMMdd_HHmmss",
                    IncludeServerName = true,
                    IncludeDatabaseName = true
                },
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.BackupConfigurations.Add(defaultConfig);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Default backup configuration created successfully");
            _logger.LogWarning("SECURITY WARNING: Default backup configuration is being used. Please review and update the settings!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating default backup configuration");
            // Don't throw here as this is not critical for database initialization
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