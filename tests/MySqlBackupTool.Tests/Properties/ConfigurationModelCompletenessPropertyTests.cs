using FsCheck;
using FsCheck.Xunit;
using MySqlBackupTool.Shared.Models;
using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Tests.Properties;

/// <summary>
/// Property-based tests for configuration model completeness
/// **Property 9: Configuration model completeness**
/// **Validates: Requirements 6.1, 6.2, 6.4**
/// </summary>
public class ConfigurationModelCompletenessPropertyTests
{
    /// <summary>
    /// Property 9: Configuration model completeness
    /// For any BackupConfiguration instance, it should have ClientId and ClientSecret properties 
    /// and provide validation methods for credential completeness.
    /// **Validates: Requirements 6.1, 6.2, 6.4**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(AuthenticationConfigurationGenerators) })]
    public Property ConfigurationModelCompletenessProperty()
    {
        return Prop.ForAll<BackupConfiguration>(
            AuthenticationConfigurationGenerators.ValidBackupConfigurationWithAuth(),
            config =>
            {
                try
                {
                    // Requirement 6.1: BackupConfiguration should have ClientId and ClientSecret properties
                    var hasClientIdProperty = config.GetType().GetProperty("ClientId") != null;
                    var hasClientSecretProperty = config.GetType().GetProperty("ClientSecret") != null;
                    
                    // Requirement 6.2: Properties should have default values
                    var hasDefaultClientId = !string.IsNullOrWhiteSpace(config.ClientId);
                    var hasDefaultClientSecret = !string.IsNullOrWhiteSpace(config.ClientSecret);
                    
                    // Requirement 6.4: Should provide validation method for credential completeness
                    var hasValidationMethod = config.GetType().GetMethod("HasValidCredentials") != null;
                    var validationMethodWorks = config.HasValidCredentials();
                    
                    // All requirements must be satisfied
                    return hasClientIdProperty && 
                           hasClientSecretProperty && 
                           hasDefaultClientId && 
                           hasDefaultClientSecret && 
                           hasValidationMethod && 
                           validationMethodWorks;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Configuration model completeness test failed: {ex.Message}");
                    return false;
                }
            })
            .Label("Feature: authentication-token-fix, Property 9: Configuration model completeness");
    }

    /// <summary>
    /// Property test for authentication credential validation behavior
    /// **Validates: Requirements 6.4**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property AuthenticationCredentialValidationProperty()
    {
        return Prop.ForAll(
            Arb.Default.String(),
            Arb.Default.String(),
            (clientId, clientSecret) =>
            {
                try
                {
                    var config = new BackupConfiguration
                    {
                        Name = "TestConfig",
                        DataDirectoryPath = Path.GetTempPath(),
                        ServiceName = "mysql",
                        TargetDirectory = Path.GetTempPath(),
                        ClientId = clientId,
                        ClientSecret = clientSecret,
                        MySQLConnection = new MySQLConnectionInfo
                        {
                            Username = "root",
                            Password = "password",
                            ServiceName = "mysql",
                            DataDirectoryPath = Path.GetTempPath(),
                            Port = 3306,
                            Host = "localhost"
                        },
                        TargetServer = new ServerEndpoint
                        {
                            IPAddress = "127.0.0.1",
                            Port = 8080,
                            UseSSL = false
                        },
                        NamingStrategy = new FileNamingStrategy
                        {
                            Pattern = "{timestamp}.zip",
                            DateFormat = "yyyyMMdd_HHmmss",
                            IncludeServerName = false,
                            IncludeDatabaseName = false
                        }
                    };

                    // HasValidCredentials should return true only when both ClientId and ClientSecret are non-empty
                    var expectedResult = !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret);
                    var actualResult = config.HasValidCredentials();

                    return expectedResult == actualResult;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Authentication credential validation test failed: {ex.Message}");
                    return false;
                }
            })
            .Label("Feature: authentication-token-fix, Property 9: Authentication credential validation");
    }

    /// <summary>
    /// Property test for default credential values
    /// **Validates: Requirements 6.1, 6.2**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property DefaultCredentialValuesProperty()
    {
        return Prop.ForAll(
            Arb.Default.String(),
            Arb.Default.String(),
            (name, dataDir) =>
            {
                try
                {
                    // Create a new BackupConfiguration without explicitly setting credentials
                    var config = new BackupConfiguration
                    {
                        Name = string.IsNullOrWhiteSpace(name) ? "TestConfig" : name,
                        DataDirectoryPath = string.IsNullOrWhiteSpace(dataDir) ? Path.GetTempPath() : dataDir,
                        ServiceName = "mysql",
                        TargetDirectory = Path.GetTempPath(),
                        MySQLConnection = new MySQLConnectionInfo
                        {
                            Username = "root",
                            Password = "password",
                            ServiceName = "mysql",
                            DataDirectoryPath = Path.GetTempPath(),
                            Port = 3306,
                            Host = "localhost"
                        },
                        TargetServer = new ServerEndpoint
                        {
                            IPAddress = "127.0.0.1",
                            Port = 8080,
                            UseSSL = false
                        },
                        NamingStrategy = new FileNamingStrategy
                        {
                            Pattern = "{timestamp}.zip",
                            DateFormat = "yyyyMMdd_HHmmss",
                            IncludeServerName = false,
                            IncludeDatabaseName = false
                        }
                    };

                    // Default values should be set automatically
                    var hasDefaultClientId = config.ClientId == "default-client";
                    var hasDefaultClientSecret = config.ClientSecret == "default-secret-2024";
                    var hasValidCredentials = config.HasValidCredentials();

                    return hasDefaultClientId && hasDefaultClientSecret && hasValidCredentials;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Default credential values test failed: {ex.Message}");
                    return false;
                }
            })
            .Label("Feature: authentication-token-fix, Property 9: Default credential values");
    }

    /// <summary>
    /// Property test for data annotation validation on authentication properties
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property AuthenticationPropertiesDataAnnotationValidationProperty()
    {
        return Prop.ForAll(
            Arb.Default.String(),
            Arb.Default.String(),
            (clientId, clientSecret) =>
            {
                try
                {
                    var config = new BackupConfiguration
                    {
                        Name = "TestConfig",
                        DataDirectoryPath = Path.GetTempPath(),
                        ServiceName = "mysql",
                        TargetDirectory = Path.GetTempPath(),
                        ClientId = clientId,
                        ClientSecret = clientSecret,
                        MySQLConnection = new MySQLConnectionInfo
                        {
                            Username = "root",
                            Password = "password",
                            ServiceName = "mysql",
                            DataDirectoryPath = Path.GetTempPath(),
                            Port = 3306,
                            Host = "localhost"
                        },
                        TargetServer = new ServerEndpoint
                        {
                            IPAddress = "127.0.0.1",
                            Port = 8080,
                            UseSSL = false
                        },
                        NamingStrategy = new FileNamingStrategy
                        {
                            Pattern = "{timestamp}.zip",
                            DateFormat = "yyyyMMdd_HHmmss",
                            IncludeServerName = false,
                            IncludeDatabaseName = false
                        }
                    };

                    var validationResults = ValidateConfiguration(config);
                    
                    // Check if validation results contain errors for ClientId or ClientSecret
                    var hasClientIdErrors = validationResults.Any(vr => vr.MemberNames.Contains("ClientId"));
                    var hasClientSecretErrors = validationResults.Any(vr => vr.MemberNames.Contains("ClientSecret"));

                    // If ClientId or ClientSecret are null/empty/whitespace, there should be validation errors
                    var shouldHaveClientIdErrors = string.IsNullOrWhiteSpace(clientId);
                    var shouldHaveClientSecretErrors = string.IsNullOrWhiteSpace(clientSecret);

                    // If ClientId or ClientSecret are too long, there should be validation errors
                    var clientIdTooLong = clientId != null && clientId.Length > 100;
                    var clientSecretTooLong = clientSecret != null && clientSecret.Length > 200;

                    if (clientIdTooLong) shouldHaveClientIdErrors = true;
                    if (clientSecretTooLong) shouldHaveClientSecretErrors = true;

                    return (shouldHaveClientIdErrors == hasClientIdErrors) && 
                           (shouldHaveClientSecretErrors == hasClientSecretErrors);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Data annotation validation test failed: {ex.Message}");
                    return false;
                }
            })
            .Label("Feature: authentication-token-fix, Property 9: Authentication properties data annotation validation");
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
}

/// <summary>
/// Custom generators for creating test data with authentication properties
/// </summary>
public static class AuthenticationConfigurationGenerators
{
    /// <summary>
    /// Generates valid BackupConfiguration instances with authentication properties
    /// </summary>
    public static Arbitrary<BackupConfiguration> ValidBackupConfigurationWithAuth()
    {
        return Arb.From(
            from name in ValidConfigurationName()
            from dataDir in ValidDirectoryPath()
            from serviceName in ValidServiceName()
            from targetDir in ValidDirectoryPath()
            from clientId in ValidClientId()
            from clientSecret in ValidClientSecret()
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
                ClientId = clientId,
                ClientSecret = clientSecret,
                MySQLConnection = mysqlConn,
                TargetServer = serverEndpoint,
                NamingStrategy = namingStrategy,
                IsActive = isActive,
                CreatedAt = DateTime.UtcNow
            });
    }

    /// <summary>
    /// Generates valid client IDs
    /// </summary>
    private static Gen<string> ValidClientId()
    {
        return Gen.OneOf(
            Gen.Constant("default-client"),
            from length in Gen.Choose(1, 50)
            from chars in Gen.ArrayOf(length, Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_".ToCharArray()))
            let clientId = new string(chars)
            where !string.IsNullOrWhiteSpace(clientId)
            select clientId
        );
    }

    /// <summary>
    /// Generates valid client secrets
    /// </summary>
    private static Gen<string> ValidClientSecret()
    {
        return Gen.OneOf(
            Gen.Constant("default-secret-2024"),
            from length in Gen.Choose(8, 100)
            from chars in Gen.ArrayOf(length, Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*-_".ToCharArray()))
            select new string(chars)
        );
    }

    // Reuse generators from ConfigurationGenerators
    private static Gen<string> ValidConfigurationName()
    {
        return from length in Gen.Choose(1, 50)
               from chars in Gen.ArrayOf(length, Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_".ToCharArray()))
               let name = new string(chars)
               where !string.IsNullOrWhiteSpace(name)
               select name;
    }

    private static Gen<string> ValidDirectoryPath()
    {
        var tempDir = Path.GetTempPath();
        return from subDir in Gen.Elements("data", "backup", "mysql", "test", "config")
               select Path.Combine(tempDir, subDir);
    }

    private static Gen<string> ValidServiceName()
    {
        return Gen.Elements("mysql", "mysql80", "mysql-server", "mysql_service", "mariadb", "percona");
    }

    private static Arbitrary<MySQLConnectionInfo> ValidMySQLConnectionInfo()
    {
        return Arb.From(
            from username in Gen.Elements("root", "admin", "backup_user")
            from password in Gen.Elements("password", "secret123", "admin123")
            from serviceName in ValidServiceName()
            from dataDir in ValidDirectoryPath()
            from port in Gen.Choose(1024, 65535)
            from host in Gen.Elements("localhost", "127.0.0.1")
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

    private static Arbitrary<ServerEndpoint> ValidServerEndpoint()
    {
        return Arb.From(
            from ipAddress in Gen.Elements("127.0.0.1", "192.168.1.100", "10.0.0.1")
            from port in Gen.Choose(1024, 65535)
            from useSSL in Arb.Generate<bool>()
            select new ServerEndpoint
            {
                IPAddress = ipAddress,
                Port = port,
                UseSSL = useSSL
            });
    }

    private static Arbitrary<FileNamingStrategy> ValidFileNamingStrategy()
    {
        return Arb.From(
            from pattern in Gen.Elements("{timestamp}.zip", "{timestamp}_{database}.zip")
            from dateFormat in Gen.Elements("yyyyMMdd_HHmmss", "yyyy-MM-dd_HH-mm-ss")
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
}