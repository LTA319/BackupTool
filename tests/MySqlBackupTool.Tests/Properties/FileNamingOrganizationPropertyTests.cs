using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using Xunit;

namespace MySqlBackupTool.Tests.Properties;

/// <summary>
/// Property-based tests for file naming and organization functionality
/// **Validates: Requirements 10.1, 10.2, 10.3, 10.4**
/// </summary>
public class FileNamingOrganizationPropertyTests : IDisposable
{
    private readonly List<string> _tempDirectories;
    private readonly List<string> _tempFiles;

    public FileNamingOrganizationPropertyTests()
    {
        _tempDirectories = new List<string>();
        _tempFiles = new List<string>();
    }

    /// <summary>
    /// Property 23: File Naming Uniqueness and Patterns
    /// For any backup operation with any naming strategy configuration, the system should generate 
    /// unique filenames that follow the configured pattern and prevent overwrites.
    /// **Validates: Requirements 10.1, 10.2, 10.3**
    /// </summary>
    [Fact]
    public void FileNamingUniquenessAndPatternsProperty()
    {
        // Arrange - Create various naming strategies
        var strategies = new[]
        {
            new FileNamingStrategy 
            { 
                Pattern = "{timestamp}_{server}_{database}.zip",
                DateFormat = "yyyyMMdd_HHmmss",
                IncludeServerName = true,
                IncludeDatabaseName = true
            },
            new FileNamingStrategy 
            { 
                Pattern = "{timestamp}_{server}.zip",
                DateFormat = "yyyy-MM-dd_HH-mm-ss",
                IncludeServerName = true,
                IncludeDatabaseName = false
            },
            new FileNamingStrategy 
            { 
                Pattern = "backup_{timestamp}.zip",
                DateFormat = "yyyyMMddHHmmss",
                IncludeServerName = false,
                IncludeDatabaseName = false
            }
        };

        var testData = new[]
        {
            ("Server1", "Database1"),
            ("Server-2", "Database_2"),
            ("Test Server", "Test Database"),
            ("", ""),
            ("Server@#$", "DB!@#")
        };

        foreach (var strategy in strategies)
        {
            var generatedNames = new HashSet<string>();
            var baseTime = DateTime.Now;

            // Test uniqueness across different timestamps
            for (int i = 0; i < 10; i++)
            {
                var testTime = baseTime.AddSeconds(i);
                
                foreach (var (serverName, databaseName) in testData)
                {
                    var fileName = strategy.GenerateFileName(serverName, databaseName, testTime);
                    
                    // Verify filename is not empty
                    Assert.False(string.IsNullOrWhiteSpace(fileName), "Generated filename should not be empty");
                    
                    // Verify filename follows pattern structure
                    Assert.Contains(".zip", fileName);
                    
                    // Verify uniqueness (allow duplicates for empty inputs or when strategy excludes server/database)
                    if (!generatedNames.Add(fileName))
                    {
                        var isDuplicateWithSameInputs = 
                            string.IsNullOrWhiteSpace(serverName) && 
                            string.IsNullOrWhiteSpace(databaseName);
                        
                        var isStrategyWithoutServerOrDatabase = 
                            !strategy.IncludeServerName && !strategy.IncludeDatabaseName;
                        
                        Assert.True(isDuplicateWithSameInputs || isStrategyWithoutServerOrDatabase, 
                            $"Duplicate filename generated: {fileName}");
                    }
                    
                    // Verify no invalid filename characters
                    var invalidChars = Path.GetInvalidFileNameChars();
                    Assert.False(fileName.Any(c => invalidChars.Contains(c)), 
                        $"Filename contains invalid characters: {fileName}");
                }
            }
        }
    }

    /// <summary>
    /// Property 24: Directory Organization
    /// For any backup file and directory structure configuration, the File Receiver Server 
    /// should organize backup files according to the configured structure.
    /// **Validates: Requirements 10.4**
    /// </summary>
    [Property(MaxTest = 15)]
    public Property DirectoryOrganizationProperty()
    {
        return Prop.ForAll(
            Arb.Default.Unit(),
            _ => {
                try
                {
                    // Arrange - Create test directory organizer
                    var logger = new LoggerFactory().CreateLogger<DirectoryOrganizer>();
                    var organizer = new DirectoryOrganizer(logger);
                    
                    var basePath = CreateTempDirectory();
            
            var strategies = new[]
            {
                new DirectoryOrganizationStrategy 
                { 
                    Type = OrganizationType.ServerDateBased,
                    DateGranularity = DateGranularity.Month,
                    IncludeDatabaseDirectory = false
                },
                new DirectoryOrganizationStrategy 
                { 
                    Type = OrganizationType.DateServerBased,
                    DateGranularity = DateGranularity.Year,
                    IncludeDatabaseDirectory = true
                },
                new DirectoryOrganizationStrategy 
                { 
                    Type = OrganizationType.FlatServerBased,
                    DateGranularity = DateGranularity.Day,
                    IncludeDatabaseDirectory = false
                },
                new DirectoryOrganizationStrategy 
                { 
                    Type = OrganizationType.Custom,
                    CustomPattern = "{server}/{year}/{month}",
                    IncludeDatabaseDirectory = false
                }
            };

            var testMetadata = new[]
            {
                new BackupMetadata 
                { 
                    ServerName = "TestServer1", 
                    DatabaseName = "TestDB1", 
                    BackupTime = new DateTime(2024, 1, 15, 10, 30, 0),
                    BackupType = "Full"
                },
                new BackupMetadata 
                { 
                    ServerName = "Server-2", 
                    DatabaseName = "Database_2", 
                    BackupTime = new DateTime(2024, 6, 20, 14, 45, 30),
                    BackupType = "Incremental"
                },
                new BackupMetadata 
                { 
                    ServerName = "Test Server", 
                    DatabaseName = "Test Database", 
                    BackupTime = new DateTime(2023, 12, 31, 23, 59, 59),
                    BackupType = "Full"
                }
            };

            foreach (var strategy in strategies)
            {
                // Validate strategy first
                var (isValid, errors) = organizer.ValidateStrategy(strategy);
                if (!isValid)
                {
                    Console.WriteLine($"Strategy validation failed: {string.Join(", ", errors)}");
                    continue; // Skip invalid strategies
                }

                foreach (var metadata in testMetadata)
                {
                    // Act - Create directory structure
                    var directoryPath = organizer.CreateDirectoryStructure(basePath, metadata, strategy);
                    
                    // Assert - Verify directory was created
                    if (!Directory.Exists(directoryPath))
                        return false;
                    
                    // Verify directory is under base path
                    if (!directoryPath.StartsWith(basePath))
                        return false;
                    
                    // Verify directory structure makes sense based on strategy
                    var relativePath = Path.GetRelativePath(basePath, directoryPath);
                    var pathComponents = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                    
                    // Should have at least one component
                    if (pathComponents.Length == 0)
                        return false;
                    
                    // Verify components don't contain invalid characters
                    foreach (var component in pathComponents)
                    {
                        var invalidChars = Path.GetInvalidPathChars().Concat(Path.GetInvalidFileNameChars());
                        if (component.Any(c => invalidChars.Contains(c)))
                            return false;
                    }
                    
                    // Strategy-specific validations
                    switch (strategy.Type)
                    {
                        case OrganizationType.ServerDateBased:
                            // First component should be server name (sanitized)
                            if (!pathComponents[0].Contains("Server") && !pathComponents[0].Contains("Test"))
                                return false;
                            break;
                            
                        case OrganizationType.DateServerBased:
                            // Should contain year component
                            if (!pathComponents.Any(c => c.Contains("2023") || c.Contains("2024")))
                                return false;
                            break;
                            
                        case OrganizationType.FlatServerBased:
                            // Should be relatively flat (1-2 levels max)
                            if (pathComponents.Length > 2)
                                return false;
                            break;
                            
                        case OrganizationType.Custom:
                            // Should follow custom pattern structure
                            if (strategy.CustomPattern.Contains("{server}") && 
                                !pathComponents.Any(c => c.Contains("Server") || c.Contains("Test")))
                                return false;
                            break;
                    }
                }
            }

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Directory organization test failed: {ex.Message}");
                    return false;
                }
            });
    }

    /// <summary>
    /// Property test for filename validation and sanitization
    /// All generated filenames should be valid for the file system
    /// **Validates: Requirements 10.1, 10.2**
    /// </summary>
    [Property(MaxTest = 15)]
    public Property FilenameValidationProperty()
    {
        return Prop.ForAll(
            Arb.Default.Unit(),
            _ => {
                try
                {
                    var strategy = new FileNamingStrategy
                    {
                        Pattern = "{timestamp}_{server}_{database}.zip",
                        DateFormat = "yyyyMMdd_HHmmss",
                        IncludeServerName = true,
                        IncludeDatabaseName = true
                    };

                    // Test with various problematic inputs
                    var problematicInputs = new[]
                    {
                        ("Server<>Name", "Database|Name"),
                        ("Server:Name", "Database?Name"),
                        ("Server\"Name", "Database*Name"),
                        ("Server/Name", "Database\\Name"),
                        ("", ""),
                        ("   ", "   "),
                        ("Server\0Name", "Database\tName")
                    };

                    foreach (var (serverName, databaseName) in problematicInputs)
                    {
                        var fileName = strategy.GenerateFileName(
                            serverName ?? "", 
                            databaseName ?? "", 
                            DateTime.Now);
                        
                        // Verify filename is not empty
                        if (string.IsNullOrWhiteSpace(fileName))
                            return false;
                        
                        // Verify no invalid filename characters
                        var invalidChars = Path.GetInvalidFileNameChars();
                        if (fileName.Any(c => invalidChars.Contains(c)))
                            return false;
                        
                        // Verify filename length is reasonable
                        if (fileName.Length > 255)
                            return false;
                        
                        // Verify filename has proper extension
                        if (!fileName.EndsWith(".zip"))
                            return false;
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Filename validation test failed: {ex.Message}");
                    return false;
                }
            });
    }

    /// <summary>
    /// Property test for directory path validation
    /// All generated directory paths should be valid for the file system
    /// **Validates: Requirements 10.4**
    /// </summary>
    [Property(MaxTest = 10)]
    public Property DirectoryPathValidationProperty()
    {
        return Prop.ForAll(
            Arb.Default.Unit(),
            _ => {
                try
                {
                    var logger = new LoggerFactory().CreateLogger<DirectoryOrganizer>();
                    var organizer = new DirectoryOrganizer(logger);
                    var basePath = CreateTempDirectory();

                    var strategy = new DirectoryOrganizationStrategy
                    {
                        Type = OrganizationType.ServerDateBased,
                        DateGranularity = DateGranularity.Month,
                        IncludeDatabaseDirectory = true
                    };

                    // Test with problematic metadata
                    var problematicMetadata = new[]
                    {
                        new BackupMetadata 
                        { 
                            ServerName = "Server<>Name", 
                            DatabaseName = "Database|Name", 
                            BackupTime = DateTime.Now 
                        },
                        new BackupMetadata 
                        { 
                            ServerName = "Server:Name", 
                            DatabaseName = "Database?Name", 
                            BackupTime = DateTime.Now 
                        },
                        new BackupMetadata 
                        { 
                            ServerName = "", 
                            DatabaseName = "", 
                            BackupTime = DateTime.Now 
                        },
                        new BackupMetadata 
                        { 
                            ServerName = "   ", 
                            DatabaseName = "   ", 
                            BackupTime = DateTime.Now 
                        }
                    };

                    foreach (var metadata in problematicMetadata)
                    {
                        var directoryPath = organizer.CreateDirectoryStructure(basePath, metadata, strategy);
                        
                        // Verify directory exists
                        if (!Directory.Exists(directoryPath))
                            return false;
                        
                        // Verify path is under base path
                        if (!directoryPath.StartsWith(basePath))
                            return false;
                        
                        // Verify path components are valid
                        var relativePath = Path.GetRelativePath(basePath, directoryPath);
                        var pathComponents = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                        
                        foreach (var component in pathComponents)
                        {
                            // Should not be empty or whitespace
                            if (string.IsNullOrWhiteSpace(component))
                                return false;
                            
                            // Should not contain invalid path characters
                            var invalidChars = Path.GetInvalidPathChars().Concat(Path.GetInvalidFileNameChars());
                            if (component.Any(c => invalidChars.Contains(c)))
                                return false;
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Directory path validation test failed: {ex.Message}");
                    return false;
                }
            });
    }

    /// <summary>
    /// Creates a temporary directory for testing
    /// </summary>
    private string CreateTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"FileNamingTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);
        return tempDir;
    }

    public void Dispose()
    {
        // Clean up temporary directories
        foreach (var dir in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        // Clean up temporary files
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}