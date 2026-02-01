using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using MySqlBackupTool.Shared.Data;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Tests.Properties;

/// <summary>
/// Property-based tests for configuration persistence consistency
/// **Validates: Requirements 1.5, 6.5**
/// </summary>
public class ConfigurationPersistencePropertyTests : IDisposable
{
    private readonly BackupDbContext _context;

    public ConfigurationPersistencePropertyTests()
    {
        var options = new DbContextOptionsBuilder<BackupDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new BackupDbContext(options);
        _context.Database.EnsureCreated();
    }

    /// <summary>
    /// Property 5: Configuration persistence consistency
    /// For any backup configuration with updated credentials, saving and then loading 
    /// the configuration should yield the same credential values.
    /// **Validates: Requirements 1.5, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConfigurationPersistenceConsistency()
    {
        return Prop.ForAll<string, string>((clientId, clientSecret) =>
        {
            // Filter out invalid inputs
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                return true; // Skip invalid inputs

            if (clientId.Contains(':') || clientSecret.Contains(':'))
                return true; // Skip inputs that would fail validation

            if (clientId.Length > 100 || clientSecret.Length > 200)
                return true; // Skip inputs that are too long

            try
            {
                // Arrange - Create a configuration with specific credentials
                var originalConfig = new BackupConfiguration
                {
                    Name = $"TestConfig_{Guid.NewGuid():N}",
                    DataDirectoryPath = Path.GetTempPath(),
                    ServiceName = "mysql",
                    TargetDirectory = Path.GetTempPath(),
                    ClientId = clientId,
                    ClientSecret = clientSecret,
                    MySQLConnection = new MySQLConnectionInfo
                    {
                        Username = "testuser",
                        Password = "testpass",
                        ServiceName = "mysql",
                        DataDirectoryPath = Path.GetTempPath(),
                        Port = 3306,
                        Host = "localhost"
                    },
                    TargetServer = new ServerEndpoint
                    {
                        IPAddress = "127.0.0.1",
                        Port = 8080,
                        UseSSL = true
                    },
                    NamingStrategy = new FileNamingStrategy
                    {
                        Pattern = "{timestamp}_{database}.zip",
                        DateFormat = "yyyyMMdd_HHmmss",
                        IncludeServerName = false,
                        IncludeDatabaseName = true
                    },
                    IsActive = true
                };

                // Act - Save the configuration
                _context.BackupConfigurations.Add(originalConfig);
                _context.SaveChanges();

                // Load the configuration
                var loadedConfig = _context.BackupConfigurations
                    .First(c => c.Id == originalConfig.Id);

                // Update credentials
                var newClientId = $"updated-{clientId}";
                var newClientSecret = $"updated-{clientSecret}";
                
                loadedConfig.ClientId = newClientId;
                loadedConfig.ClientSecret = newClientSecret;
                
                // Save the updated configuration
                _context.SaveChanges();

                // Load again to verify persistence
                var reloadedConfig = _context.BackupConfigurations
                    .First(c => c.Id == originalConfig.Id);

                // Assert - Verify credentials were persisted correctly
                var credentialsPersisted = reloadedConfig.ClientId == newClientId &&
                                         reloadedConfig.ClientSecret == newClientSecret &&
                                         reloadedConfig.HasValidCredentials();

                // Clean up
                _context.BackupConfigurations.Remove(reloadedConfig);
                _context.SaveChanges();

                return credentialsPersisted;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Configuration persistence test failed: {ex.Message}");
                return false;
            }
        })
        .Label("Feature: authentication-token-fix, Property 5: Configuration persistence consistency");
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}