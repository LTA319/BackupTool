using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using MySqlBackupTool.Shared.Data;
using MySqlBackupTool.Shared.Models;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace MySqlBackupTool.Tests.Properties;

/// <summary>
/// Property-based tests for configuration round-trip consistency
/// **Validates: Requirements 2.1, 2.2, 2.3**
/// </summary>
public class ConfigurationRoundTripPropertyTests : IDisposable
{
    private readonly BackupDbContext _context;

    public ConfigurationRoundTripPropertyTests()
    {
        var options = new DbContextOptionsBuilder<BackupDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new BackupDbContext(options);
        _context.Database.EnsureCreated();
    }

    /// <summary>
    /// Property 1: Configuration Round-Trip Consistency
    /// For any valid configuration data, storing the configuration and then retrieving it 
    /// should produce equivalent configuration data.
    /// **Validates: Requirements 2.1, 2.2, 2.3**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ConfigurationGenerators) })]
    public Property ConfigurationRoundTripConsistency(BackupConfiguration originalConfig)
    {
        return Prop.ForAll<BackupConfiguration>(
            ConfigurationGenerators.ValidBackupConfiguration(),
            config =>
            {
                try
                {
                    // Arrange - Store the configuration
                    _context.BackupConfigurations.Add(config);
                    _context.SaveChanges();

                    // Act - Retrieve the configuration
                    var retrievedConfig = _context.BackupConfigurations
                        .First(c => c.Id == config.Id);

                    // Assert - Verify equivalence
                    var isEquivalent = AreConfigurationsEquivalent(config, retrievedConfig);
                    
                    // Clean up for next iteration
                    _context.BackupConfigurations.Remove(retrievedConfig);
                    _context.SaveChanges();

                    return isEquivalent;
                }
                catch (Exception ex)
                {
                    // Log the exception for debugging
                    Console.WriteLine($"Round-trip test failed with exception: {ex.Message}");
                    return false;
                }
            });
    }

    /// <summary>
    /// Property test for MySQL connection info round-trip consistency
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Property(MaxTest = 50, Arbitrary = new[] { typeof(ConfigurationGenerators) })]
    public Property MySQLConnectionInfoRoundTripConsistency(MySQLConnectionInfo originalConnection)
    {
        return Prop.ForAll<MySQLConnectionInfo>(
            ConfigurationGenerators.ValidMySQLConnectionInfo(),
            connection =>
            {
                try
                {
                    // Serialize and deserialize to simulate database storage
                    var json = JsonSerializer.Serialize(connection);
                    var deserializedConnection = JsonSerializer.Deserialize<MySQLConnectionInfo>(json);

                    return AreMySQLConnectionsEquivalent(connection, deserializedConnection!);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"MySQL connection round-trip test failed: {ex.Message}");
                    return false;
                }
            });
    }

    /// <summary>
    /// Property test for server endpoint round-trip consistency
    /// **Validates: Requirements 2.2**
    /// </summary>
    [Property(MaxTest = 50, Arbitrary = new[] { typeof(ConfigurationGenerators) })]
    public Property ServerEndpointRoundTripConsistency(ServerEndpoint originalEndpoint)
    {
        return Prop.ForAll<ServerEndpoint>(
            ConfigurationGenerators.ValidServerEndpoint(),
            endpoint =>
            {
                try
                {
                    // Serialize and deserialize to simulate database storage
                    var json = JsonSerializer.Serialize(endpoint);
                    var deserializedEndpoint = JsonSerializer.Deserialize<ServerEndpoint>(json);

                    return AreServerEndpointsEquivalent(endpoint, deserializedEndpoint!);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Server endpoint round-trip test failed: {ex.Message}");
                    return false;
                }
            });
    }

    /// <summary>
    /// Property test for file naming strategy round-trip consistency
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Property(MaxTest = 50, Arbitrary = new[] { typeof(ConfigurationGenerators) })]
    public Property FileNamingStrategyRoundTripConsistency(FileNamingStrategy originalStrategy)
    {
        return Prop.ForAll<FileNamingStrategy>(
            ConfigurationGenerators.ValidFileNamingStrategy(),
            strategy =>
            {
                try
                {
                    // Serialize and deserialize to simulate database storage
                    var json = JsonSerializer.Serialize(strategy);
                    var deserializedStrategy = JsonSerializer.Deserialize<FileNamingStrategy>(json);

                    return AreFileNamingStrategiesEquivalent(strategy, deserializedStrategy!);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"File naming strategy round-trip test failed: {ex.Message}");
                    return false;
                }
            });
    }

    /// <summary>
    /// Compares two BackupConfiguration objects for equivalence
    /// </summary>
    private static bool AreConfigurationsEquivalent(BackupConfiguration config1, BackupConfiguration config2)
    {
        if (config1 == null && config2 == null) return true;
        if (config1 == null || config2 == null) return false;

        return config1.Name == config2.Name &&
               config1.DataDirectoryPath == config2.DataDirectoryPath &&
               config1.ServiceName == config2.ServiceName &&
               config1.TargetDirectory == config2.TargetDirectory &&
               config1.IsActive == config2.IsActive &&
               config1.ClientId == config2.ClientId &&
               config1.ClientSecret == config2.ClientSecret &&
               AreMySQLConnectionsEquivalent(config1.MySQLConnection, config2.MySQLConnection) &&
               AreServerEndpointsEquivalent(config1.TargetServer, config2.TargetServer) &&
               AreFileNamingStrategiesEquivalent(config1.NamingStrategy, config2.NamingStrategy);
    }

    /// <summary>
    /// Compares two MySQLConnectionInfo objects for equivalence
    /// </summary>
    private static bool AreMySQLConnectionsEquivalent(MySQLConnectionInfo conn1, MySQLConnectionInfo conn2)
    {
        if (conn1 == null && conn2 == null) return true;
        if (conn1 == null || conn2 == null) return false;

        return conn1.Username == conn2.Username &&
               conn1.Password == conn2.Password &&
               conn1.ServiceName == conn2.ServiceName &&
               conn1.DataDirectoryPath == conn2.DataDirectoryPath &&
               conn1.Port == conn2.Port &&
               conn1.Host == conn2.Host;
    }

    /// <summary>
    /// Compares two ServerEndpoint objects for equivalence
    /// </summary>
    private static bool AreServerEndpointsEquivalent(ServerEndpoint endpoint1, ServerEndpoint endpoint2)
    {
        if (endpoint1 == null && endpoint2 == null) return true;
        if (endpoint1 == null || endpoint2 == null) return false;

        return endpoint1.IPAddress == endpoint2.IPAddress &&
               endpoint1.Port == endpoint2.Port &&
               endpoint1.UseSSL == endpoint2.UseSSL;
    }

    /// <summary>
    /// Compares two FileNamingStrategy objects for equivalence
    /// </summary>
    private static bool AreFileNamingStrategiesEquivalent(FileNamingStrategy strategy1, FileNamingStrategy strategy2)
    {
        if (strategy1 == null && strategy2 == null) return true;
        if (strategy1 == null || strategy2 == null) return false;

        return strategy1.Pattern == strategy2.Pattern &&
               strategy1.DateFormat == strategy2.DateFormat &&
               strategy1.IncludeServerName == strategy2.IncludeServerName &&
               strategy1.IncludeDatabaseName == strategy2.IncludeDatabaseName;
    }

    /// <summary>
    /// Property 4: Configuration Validation
    /// For any configuration input, the system should accept valid configurations 
    /// and reject invalid configurations, ensuring only valid configurations are stored.
    /// **Validates: Requirements 2.4**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property ConfigurationValidationProperty()
    {
        // Test that valid configurations pass validation
        var validConfig = new BackupConfiguration
        {
            Name = "TestConfig",
            DataDirectoryPath = Path.GetTempPath(),
            ServiceName = "mysql",
            TargetDirectory = Path.GetTempPath(),
            MySQLConnection = new MySQLConnectionInfo
            {
                Username = "root",
                Password = "password123",
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
            }
        };

        var validationResults = ValidateConfiguration(validConfig);
        var validConfigPasses = validationResults.Count == 0;

        // Test that invalid configurations fail validation
        var invalidConfig = new BackupConfiguration
        {
            Name = "", // Invalid: empty name
            DataDirectoryPath = "", // Invalid: empty path
            ServiceName = "", // Invalid: empty service name
            TargetDirectory = "",
            MySQLConnection = new MySQLConnectionInfo
            {
                Username = "", // Invalid: empty username
                Password = "", // Invalid: empty password
                ServiceName = "",
                DataDirectoryPath = "",
                Port = 0, // Invalid: port out of range
                Host = ""
            },
            TargetServer = new ServerEndpoint
            {
                IPAddress = "", // Invalid: empty IP
                Port = 0, // Invalid: port out of range
                UseSSL = true
            },
            NamingStrategy = new FileNamingStrategy
            {
                Pattern = "", // Invalid: empty pattern
                DateFormat = "", // Invalid: empty date format
                IncludeServerName = false,
                IncludeDatabaseName = false
            }
        };

        var invalidValidationResults = ValidateConfiguration(invalidConfig);
        var invalidConfigFails = invalidValidationResults.Count > 0;

        return (validConfigPasses && invalidConfigFails).ToProperty();
    }

    /// <summary>
    /// Property test for MySQL connection validation
    /// **Validates: Requirements 2.4**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property MySQLConnectionValidationProperty()
    {
        // Test valid MySQL connection
        var validConnection = new MySQLConnectionInfo
        {
            Username = "root",
            Password = "password123",
            ServiceName = "mysql",
            DataDirectoryPath = Path.GetTempPath(),
            Port = 3306,
            Host = "localhost"
        };

        var validResults = ValidateMySQLConnection(validConnection);
        var validPasses = validResults.Count == 0;

        // Test invalid MySQL connection
        var invalidConnection = new MySQLConnectionInfo
        {
            Username = "", // Invalid: empty username
            Password = "", // Invalid: empty password
            ServiceName = "",
            DataDirectoryPath = "",
            Port = 0, // Invalid: port out of range
            Host = ""
        };

        var invalidResults = ValidateMySQLConnection(invalidConnection);
        var invalidFails = invalidResults.Count > 0;

        return (validPasses && invalidFails).ToProperty();
    }

    /// <summary>
    /// Property test for server endpoint validation
    /// **Validates: Requirements 2.4**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property ServerEndpointValidationProperty()
    {
        // Test valid server endpoint
        var validEndpoint = new ServerEndpoint
        {
            IPAddress = "127.0.0.1",
            Port = 8080,
            UseSSL = true
        };

        var validResults = ValidateServerEndpoint(validEndpoint);
        var validPasses = validResults.Count == 0;

        // Test invalid server endpoint
        var invalidEndpoint = new ServerEndpoint
        {
            IPAddress = "", // Invalid: empty IP
            Port = 0, // Invalid: port out of range
            UseSSL = true
        };

        var invalidResults = ValidateServerEndpoint(invalidEndpoint);
        var invalidFails = invalidResults.Count > 0;

        return (validPasses && invalidFails).ToProperty();
    }

    /// <summary>
    /// Property test for file naming strategy validation
    /// **Validates: Requirements 2.4**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property FileNamingStrategyValidationProperty()
    {
        // Test valid file naming strategy
        var validStrategy = new FileNamingStrategy
        {
            Pattern = "{timestamp}_{database}.zip",
            DateFormat = "yyyyMMdd_HHmmss",
            IncludeServerName = false,
            IncludeDatabaseName = true
        };

        var validResults = ValidateFileNamingStrategy(validStrategy);
        var validPasses = validResults.Count == 0;

        // Test invalid file naming strategy
        var invalidStrategy = new FileNamingStrategy
        {
            Pattern = "", // Invalid: empty pattern
            DateFormat = "", // Invalid: empty date format
            IncludeServerName = false,
            IncludeDatabaseName = false
        };

        var invalidResults = ValidateFileNamingStrategy(invalidStrategy);
        var invalidFails = invalidResults.Count > 0;

        return (validPasses && invalidFails).ToProperty();
    }

    /// <summary>
    /// Validates a BackupConfiguration using data annotations and custom validation
    /// </summary>
    private static List<ValidationResult> ValidateConfiguration(BackupConfiguration config)
    {
        var validationResults = new List<ValidationResult>();
        
        if (config == null)
        {
            validationResults.Add(new ValidationResult("Configuration cannot be null"));
            return validationResults;
        }

        var validationContext = new ValidationContext(config);

        // Validate using data annotations
        Validator.TryValidateObject(config, validationContext, validationResults, true);

        // Add custom validation results
        if (config is IValidatableObject validatable)
        {
            validationResults.AddRange(validatable.Validate(validationContext));
        }

        return validationResults;
    }

    /// <summary>
    /// Validates a MySQLConnectionInfo using data annotations and custom validation
    /// </summary>
    private static List<ValidationResult> ValidateMySQLConnection(MySQLConnectionInfo connection)
    {
        var validationResults = new List<ValidationResult>();
        
        if (connection == null)
        {
            validationResults.Add(new ValidationResult("MySQL connection cannot be null"));
            return validationResults;
        }

        var validationContext = new ValidationContext(connection);

        // Validate using data annotations
        Validator.TryValidateObject(connection, validationContext, validationResults, true);

        // Add custom validation results
        if (connection is IValidatableObject validatable)
        {
            validationResults.AddRange(validatable.Validate(validationContext));
        }

        return validationResults;
    }

    /// <summary>
    /// Validates a ServerEndpoint using data annotations and custom validation
    /// </summary>
    private static List<ValidationResult> ValidateServerEndpoint(ServerEndpoint endpoint)
    {
        var validationResults = new List<ValidationResult>();
        
        if (endpoint == null)
        {
            validationResults.Add(new ValidationResult("Server endpoint cannot be null"));
            return validationResults;
        }

        var validationContext = new ValidationContext(endpoint);

        // Validate using data annotations
        Validator.TryValidateObject(endpoint, validationContext, validationResults, true);

        // Add custom validation results
        if (endpoint is IValidatableObject validatable)
        {
            validationResults.AddRange(validatable.Validate(validationContext));
        }

        return validationResults;
    }

    /// <summary>
    /// Validates a FileNamingStrategy using data annotations and custom validation
    /// </summary>
    private static List<ValidationResult> ValidateFileNamingStrategy(FileNamingStrategy strategy)
    {
        var validationResults = new List<ValidationResult>();
        
        if (strategy == null)
        {
            validationResults.Add(new ValidationResult("File naming strategy cannot be null"));
            return validationResults;
        }

        var validationContext = new ValidationContext(strategy);

        // Validate using data annotations
        Validator.TryValidateObject(strategy, validationContext, validationResults, true);

        // Add custom validation results
        if (strategy is IValidatableObject validatable)
        {
            validationResults.AddRange(validatable.Validate(validationContext));
        }

        return validationResults;
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}

/// <summary>
/// Custom generators for creating valid test data
/// </summary>
public static class ConfigurationGenerators
{
    /// <summary>
    /// Generates valid BackupConfiguration instances
    /// </summary>
    public static Arbitrary<BackupConfiguration> ValidBackupConfiguration()
    {
        return Arb.From(
            from name in ValidConfigurationName()
            from dataDir in ValidDirectoryPath()
            from serviceName in ValidServiceName()
            from targetDir in ValidDirectoryPath()
            from mysqlConn in ValidMySQLConnectionInfo().Generator
            from serverEndpoint in ValidServerEndpoint().Generator
            from namingStrategy in ValidFileNamingStrategy().Generator
            from isActive in Arb.Generate<bool>()
            select new BackupConfiguration
            {
                Name = name,
                DataDirectoryPath = dataDir,
                ServiceName = serviceName,
                TargetDirectory = targetDir,
                MySQLConnection = mysqlConn,
                TargetServer = serverEndpoint,
                NamingStrategy = namingStrategy,
                IsActive = isActive,
                CreatedAt = DateTime.Now
            });
    }

    /// <summary>
    /// Generates valid MySQLConnectionInfo instances
    /// </summary>
    public static Arbitrary<MySQLConnectionInfo> ValidMySQLConnectionInfo()
    {
        return Arb.From(
            from username in ValidUsername()
            from password in ValidPassword()
            from serviceName in ValidServiceName()
            from dataDir in ValidDirectoryPath()
            from port in ValidPort()
            from host in ValidHostname()
            select new MySQLConnectionInfo
            {
                Username = username,
                Password = password,
                ServiceName = serviceName,
                DataDirectoryPath = dataDir,
                Port = port,
                Host = host
            });
    }

    /// <summary>
    /// Generates valid ServerEndpoint instances
    /// </summary>
    public static Arbitrary<ServerEndpoint> ValidServerEndpoint()
    {
        return Arb.From(
            from ipAddress in ValidIPAddress()
            from port in ValidPort()
            from useSSL in Arb.Generate<bool>()
            select new ServerEndpoint
            {
                IPAddress = ipAddress,
                Port = port,
                UseSSL = useSSL
            });
    }

    /// <summary>
    /// Generates valid FileNamingStrategy instances
    /// </summary>
    public static Arbitrary<FileNamingStrategy> ValidFileNamingStrategy()
    {
        return Arb.From(
            from pattern in ValidNamingPattern()
            from dateFormat in ValidDateFormat()
            from includeServer in Arb.Generate<bool>()
            from includeDatabase in Arb.Generate<bool>()
            select new FileNamingStrategy
            {
                Pattern = pattern,
                DateFormat = dateFormat,
                IncludeServerName = includeServer,
                IncludeDatabaseName = includeDatabase
            });
    }

    /// <summary>
    /// Generates valid configuration names
    /// </summary>
    private static Gen<string> ValidConfigurationName()
    {
        return from length in Gen.Choose(1, 50)
               from chars in Gen.ArrayOf(length, Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_".ToCharArray()))
               let name = new string(chars)
               where !string.IsNullOrWhiteSpace(name)
               select name;
    }

    /// <summary>
    /// Generates valid directory paths (using temp directory as base)
    /// </summary>
    private static Gen<string> ValidDirectoryPath()
    {
        var tempDir = Path.GetTempPath();
        return from subDir in Gen.Elements("data", "backup", "mysql", "test", "config")
               select Path.Combine(tempDir, subDir);
    }

    /// <summary>
    /// Generates valid service names
    /// </summary>
    private static Gen<string> ValidServiceName()
    {
        return Gen.Elements("mysql", "mysql80", "mysql-server", "mysql_service", "mariadb", "percona");
    }

    /// <summary>
    /// Generates valid usernames
    /// </summary>
    private static Gen<string> ValidUsername()
    {
        return Gen.Elements("root", "admin", "backup_user", "mysql_admin", "dbuser", "testuser");
    }

    /// <summary>
    /// Generates valid passwords
    /// </summary>
    private static Gen<string> ValidPassword()
    {
        return from length in Gen.Choose(8, 20)
               from chars in Gen.ArrayOf(length, Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*".ToCharArray()))
               select new string(chars);
    }

    /// <summary>
    /// Generates valid port numbers
    /// </summary>
    private static Gen<int> ValidPort()
    {
        return Gen.Choose(1024, 65535); // Avoid reserved ports
    }

    /// <summary>
    /// Generates valid hostnames
    /// </summary>
    private static Gen<string> ValidHostname()
    {
        return Gen.Elements("localhost", "127.0.0.1", "192.168.1.100", "10.0.0.1", "mysql.example.com");
    }

    /// <summary>
    /// Generates valid IP addresses
    /// </summary>
    private static Gen<string> ValidIPAddress()
    {
        return from a in Gen.Choose(1, 255)
               from b in Gen.Choose(0, 255)
               from c in Gen.Choose(0, 255)
               from d in Gen.Choose(1, 254)
               select $"{a}.{b}.{c}.{d}";
    }

    /// <summary>
    /// Generates valid naming patterns
    /// </summary>
    private static Gen<string> ValidNamingPattern()
    {
        var patterns = new[]
        {
            "{timestamp}_{database}_{server}.zip",
            "{timestamp}_{database}.zip",
            "{timestamp}_{server}.zip",
            "{timestamp}.zip",
            "backup_{timestamp}_{database}.zip",
            "{database}_{timestamp}.zip"
        };
        return Gen.Elements(patterns);
    }

    /// <summary>
    /// Generates invalid BackupConfiguration instances for validation testing
    /// </summary>
    public static Arbitrary<BackupConfiguration> InvalidBackupConfiguration()
    {
        return Arb.From(
            Gen.OneOf(
                // Invalid name (empty, too long, invalid characters)
                from name in Gen.OneOf(
                    Gen.Constant(""), // Empty name
                    Gen.Constant(new string('a', 101)), // Too long
                    Gen.Constant("invalid<>name"), // Invalid characters
                    Gen.Constant("   ") // Whitespace only
                )
                from dataDir in ValidDirectoryPath()
                from serviceName in ValidServiceName()
                from targetDir in ValidDirectoryPath()
                from mysqlConn in ValidMySQLConnectionInfo().Generator
                from serverEndpoint in ValidServerEndpoint().Generator
                from namingStrategy in ValidFileNamingStrategy().Generator
                select new BackupConfiguration
                {
                    Name = name,
                    DataDirectoryPath = dataDir,
                    ServiceName = serviceName,
                    TargetDirectory = targetDir,
                    MySQLConnection = mysqlConn,
                    TargetServer = serverEndpoint,
                    NamingStrategy = namingStrategy
                },

                // Invalid data directory path
                from name in ValidConfigurationName()
                from dataDir in Gen.OneOf(
                    Gen.Constant(""), // Empty path
                    Gen.Constant(new string('a', 501)), // Too long
                    Gen.Constant("invalid<>path") // Invalid characters
                )
                from serviceName in ValidServiceName()
                from targetDir in ValidDirectoryPath()
                from mysqlConn in ValidMySQLConnectionInfo().Generator
                from serverEndpoint in ValidServerEndpoint().Generator
                from namingStrategy in ValidFileNamingStrategy().Generator
                select new BackupConfiguration
                {
                    Name = name,
                    DataDirectoryPath = dataDir,
                    ServiceName = serviceName,
                    TargetDirectory = targetDir,
                    MySQLConnection = mysqlConn,
                    TargetServer = serverEndpoint,
                    NamingStrategy = namingStrategy
                },

                // Invalid service name
                from name in ValidConfigurationName()
                from dataDir in ValidDirectoryPath()
                from serviceName in Gen.OneOf(
                    Gen.Constant(""), // Empty service name
                    Gen.Constant(new string('a', 101)), // Too long
                    Gen.Constant("invalid service name") // Invalid characters (spaces)
                )
                from targetDir in ValidDirectoryPath()
                from mysqlConn in ValidMySQLConnectionInfo().Generator
                from serverEndpoint in ValidServerEndpoint().Generator
                from namingStrategy in ValidFileNamingStrategy().Generator
                select new BackupConfiguration
                {
                    Name = name,
                    DataDirectoryPath = dataDir,
                    ServiceName = serviceName,
                    TargetDirectory = targetDir,
                    MySQLConnection = mysqlConn,
                    TargetServer = serverEndpoint,
                    NamingStrategy = namingStrategy
                }
            ));
    }

    /// <summary>
    /// Generates invalid MySQLConnectionInfo instances for validation testing
    /// </summary>
    public static Arbitrary<MySQLConnectionInfo> InvalidMySQLConnectionInfo()
    {
        return Arb.From(
            Gen.OneOf(
                // Invalid username
                from username in Gen.OneOf(
                    Gen.Constant(""), // Empty username
                    Gen.Constant(new string('a', 101)), // Too long
                    Gen.Constant("user'name") // Invalid characters
                )
                from password in ValidPassword()
                from serviceName in ValidServiceName()
                from dataDir in ValidDirectoryPath()
                from port in ValidPort()
                from host in ValidHostname()
                select new MySQLConnectionInfo
                {
                    Username = username,
                    Password = password,
                    ServiceName = serviceName,
                    DataDirectoryPath = dataDir,
                    Port = port,
                    Host = host
                },

                // Invalid password
                from username in ValidUsername()
                from password in Gen.OneOf(
                    Gen.Constant(""), // Empty password
                    Gen.Constant(new string('a', 101)) // Too long
                )
                from serviceName in ValidServiceName()
                from dataDir in ValidDirectoryPath()
                from port in ValidPort()
                from host in ValidHostname()
                select new MySQLConnectionInfo
                {
                    Username = username,
                    Password = password,
                    ServiceName = serviceName,
                    DataDirectoryPath = dataDir,
                    Port = port,
                    Host = host
                },

                // Invalid port
                from username in ValidUsername()
                from password in ValidPassword()
                from serviceName in ValidServiceName()
                from dataDir in ValidDirectoryPath()
                from port in Gen.OneOf(
                    Gen.Constant(0), // Invalid port (too low)
                    Gen.Constant(65536) // Invalid port (too high)
                )
                from host in ValidHostname()
                select new MySQLConnectionInfo
                {
                    Username = username,
                    Password = password,
                    ServiceName = serviceName,
                    DataDirectoryPath = dataDir,
                    Port = port,
                    Host = host
                },

                // Invalid host
                from username in ValidUsername()
                from password in ValidPassword()
                from serviceName in ValidServiceName()
                from dataDir in ValidDirectoryPath()
                from port in ValidPort()
                from host in Gen.OneOf(
                    Gen.Constant(""), // Empty host
                    Gen.Constant("invalid host name"), // Invalid characters (spaces)
                    Gen.Constant("host..name") // Invalid format (double dots)
                )
                select new MySQLConnectionInfo
                {
                    Username = username,
                    Password = password,
                    ServiceName = serviceName,
                    DataDirectoryPath = dataDir,
                    Port = port,
                    Host = host
                }
            ));
    }

    /// <summary>
    /// Generates invalid ServerEndpoint instances for validation testing
    /// </summary>
    public static Arbitrary<ServerEndpoint> InvalidServerEndpoint()
    {
        return Arb.From(
            Gen.OneOf(
                // Invalid IP address
                from ipAddress in Gen.OneOf(
                    Gen.Constant(""), // Empty IP
                    Gen.Constant("invalid.ip"), // Invalid format
                    Gen.Constant("256.256.256.256"), // Out of range
                    Gen.Constant("0.0.0.0") // Wildcard IP (invalid for target)
                )
                from port in ValidPort()
                from useSSL in Arb.Generate<bool>()
                select new ServerEndpoint
                {
                    IPAddress = ipAddress,
                    Port = port,
                    UseSSL = useSSL
                },

                // Invalid port
                from ipAddress in ValidIPAddress()
                from port in Gen.OneOf(
                    Gen.Constant(0), // Invalid port (too low)
                    Gen.Constant(65536) // Invalid port (too high)
                )
                from useSSL in Arb.Generate<bool>()
                select new ServerEndpoint
                {
                    IPAddress = ipAddress,
                    Port = port,
                    UseSSL = useSSL
                }
            ));
    }

    /// <summary>
    /// Generates invalid FileNamingStrategy instances for validation testing
    /// </summary>
    public static Arbitrary<FileNamingStrategy> InvalidFileNamingStrategy()
    {
        return Arb.From(
            Gen.OneOf(
                // Invalid pattern (empty or no placeholders)
                from pattern in Gen.OneOf(
                    Gen.Constant(""), // Empty pattern
                    Gen.Constant("no_placeholders.zip"), // No valid placeholders
                    Gen.Constant("invalid<>pattern.zip") // Invalid filename characters
                )
                from dateFormat in ValidDateFormat()
                from includeServer in Arb.Generate<bool>()
                from includeDatabase in Arb.Generate<bool>()
                select new FileNamingStrategy
                {
                    Pattern = pattern,
                    DateFormat = dateFormat,
                    IncludeServerName = includeServer,
                    IncludeDatabaseName = includeDatabase
                },

                // Invalid date format
                from pattern in ValidNamingPattern()
                from dateFormat in Gen.OneOf(
                    Gen.Constant(""), // Empty date format
                    Gen.Constant("invalid_format"), // Invalid format specifiers
                    Gen.Constant("xyz") // No valid format characters
                )
                from includeServer in Arb.Generate<bool>()
                from includeDatabase in Arb.Generate<bool>()
                select new FileNamingStrategy
                {
                    Pattern = pattern,
                    DateFormat = dateFormat,
                    IncludeServerName = includeServer,
                    IncludeDatabaseName = includeDatabase
                },

                // Inconsistent pattern and flags
                from includeServer in Arb.Generate<bool>()
                from includeDatabase in Arb.Generate<bool>()
                from dateFormat in ValidDateFormat()
                select new FileNamingStrategy
                {
                    Pattern = "{timestamp}_{server}_{database}.zip", // Contains both placeholders
                    DateFormat = dateFormat,
                    IncludeServerName = false, // But server flag is false
                    IncludeDatabaseName = includeDatabase
                }
            ));
    }
    /// <summary>
    /// Generates valid date formats
    /// </summary>
    private static Gen<string> ValidDateFormat()
    {
        var formats = new[]
        {
            "yyyyMMdd_HHmmss",
            "yyyy-MM-dd_HH-mm-ss",
            "yyyyMMddHHmmss",
            "yyyy_MM_dd_HH_mm_ss",
            "yyMMdd_HHmm",
            "yyyy-MM-dd"
        };
        return Gen.Elements(formats);
    }
}