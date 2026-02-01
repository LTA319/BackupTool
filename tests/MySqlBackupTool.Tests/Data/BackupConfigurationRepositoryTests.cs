using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Data;
using MySqlBackupTool.Shared.Data.Repositories;
using MySqlBackupTool.Shared.Models;
using Xunit;

namespace MySqlBackupTool.Tests.Data;

public class BackupConfigurationRepositoryTests : IDisposable
{
    private readonly BackupDbContext _context;
    private readonly BackupConfigurationRepository _repository;

    public BackupConfigurationRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<BackupDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new BackupDbContext(options);
        _repository = new BackupConfigurationRepository(_context);
    }

    [Fact]
    public async Task AddAsync_ValidConfiguration_ShouldAddSuccessfully()
    {
        // Arrange
        var configuration = CreateValidConfiguration("Test Config");

        // Act
        var result = await _repository.AddAsync(configuration);
        await _repository.SaveChangesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("Test Config", result.Name);
        Assert.True(result.CreatedAt > DateTime.MinValue);
    }

    [Fact]
    public async Task GetByNameAsync_ExistingConfiguration_ShouldReturnConfiguration()
    {
        // Arrange
        var configuration = CreateValidConfiguration("Unique Config");
        await _repository.AddAsync(configuration);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _repository.GetByNameAsync("Unique Config");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Unique Config", result.Name);
    }

    [Fact]
    public async Task GetByNameAsync_NonExistingConfiguration_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByNameAsync("Non-existing Config");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task IsNameUniqueAsync_UniqueNameNewConfiguration_ShouldReturnTrue()
    {
        // Arrange
        var configuration = CreateValidConfiguration("Existing Config");
        await _repository.AddAsync(configuration);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _repository.IsNameUniqueAsync("New Config");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsNameUniqueAsync_DuplicateNameNewConfiguration_ShouldReturnFalse()
    {
        // Arrange
        var configuration = CreateValidConfiguration("Existing Config");
        await _repository.AddAsync(configuration);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _repository.IsNameUniqueAsync("Existing Config");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsNameUniqueAsync_DuplicateNameSameConfiguration_ShouldReturnTrue()
    {
        // Arrange
        var configuration = CreateValidConfiguration("Existing Config");
        var added = await _repository.AddAsync(configuration);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _repository.IsNameUniqueAsync("Existing Config", added.Id);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetActiveConfigurationsAsync_MixedActiveInactive_ShouldReturnOnlyActive()
    {
        // Arrange
        var activeConfig1 = CreateValidConfiguration("Active 1");
        activeConfig1.IsActive = true;
        
        var activeConfig2 = CreateValidConfiguration("Active 2");
        activeConfig2.IsActive = true;
        
        var inactiveConfig = CreateValidConfiguration("Inactive");
        inactiveConfig.IsActive = false;

        await _repository.AddAsync(activeConfig1);
        await _repository.AddAsync(activeConfig2);
        await _repository.AddAsync(inactiveConfig);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _repository.GetActiveConfigurationsAsync();

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, config => Assert.True(config.IsActive));
    }

    [Fact]
    public async Task ActivateConfigurationAsync_ExistingConfiguration_ShouldSetActiveTrue()
    {
        // Arrange
        var configuration = CreateValidConfiguration("Test Config");
        configuration.IsActive = false;
        var added = await _repository.AddAsync(configuration);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _repository.ActivateConfigurationAsync(added.Id);

        // Assert
        Assert.True(result);
        var updated = await _repository.GetByIdAsync(added.Id);
        Assert.NotNull(updated);
        Assert.True(updated.IsActive);
    }

    [Fact]
    public async Task DeactivateConfigurationAsync_ExistingConfiguration_ShouldSetActiveFalse()
    {
        // Arrange
        var configuration = CreateValidConfiguration("Test Config");
        configuration.IsActive = true;
        var added = await _repository.AddAsync(configuration);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _repository.DeactivateConfigurationAsync(added.Id);

        // Assert
        Assert.True(result);
        var updated = await _repository.GetByIdAsync(added.Id);
        Assert.NotNull(updated);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task GetByTargetServerAsync_MatchingServer_ShouldReturnConfigurations()
    {
        // Arrange
        var config1 = CreateValidConfiguration("Config 1");
        config1.TargetServer.IPAddress = "192.168.1.100";
        config1.TargetServer.Port = 8080;

        var config2 = CreateValidConfiguration("Config 2");
        config2.TargetServer.IPAddress = "192.168.1.100";
        config2.TargetServer.Port = 8080;

        var config3 = CreateValidConfiguration("Config 3");
        config3.TargetServer.IPAddress = "192.168.1.101";
        config3.TargetServer.Port = 8080;

        await _repository.AddAsync(config1);
        await _repository.AddAsync(config2);
        await _repository.AddAsync(config3);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTargetServerAsync("192.168.1.100", 8080);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, config => 
        {
            Assert.Equal("192.168.1.100", config.TargetServer.IPAddress);
            Assert.Equal(8080, config.TargetServer.Port);
        });
    }

    [Fact]
    public async Task ValidateAndSaveAsync_ValidConfiguration_ShouldSaveSuccessfully()
    {
        // Arrange
        var configuration = CreateValidConfiguration("Valid Config");

        // Act - Skip connection validation for unit tests
        var result = await _repository.ValidateAndSaveAsync(configuration, validateConnections: false);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.Configuration);
        Assert.True(result.Configuration.Id > 0);
    }

    [Fact]
    public async Task ValidateAndSaveAsync_InvalidConfiguration_ShouldReturnErrors()
    {
        // Arrange
        var configuration = new BackupConfiguration
        {
            Name = "", // Invalid: empty name
            MySQLConnection = new MySQLConnectionInfo(),
            TargetServer = new ServerEndpoint(),
            NamingStrategy = new FileNamingStrategy()
        };

        // Act
        var result = await _repository.ValidateAndSaveAsync(configuration);

        // Assert
        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
        Assert.Null(result.Configuration);
    }

    [Fact]
    public async Task ValidateAndSaveAsync_DuplicateName_ShouldReturnError()
    {
        // Arrange
        var existingConfig = CreateValidConfiguration("Duplicate Name");
        await _repository.AddAsync(existingConfig);
        await _repository.SaveChangesAsync();

        var newConfig = CreateValidConfiguration("Duplicate Name");

        // Act
        var result = await _repository.ValidateAndSaveAsync(newConfig);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("already in use"));
    }

    [Fact]
    public async Task ValidateAndSaveAsync_InvalidCredentials_ShouldReturnErrors()
    {
        // Arrange
        var configuration = CreateValidConfiguration("Test Config");
        configuration.ClientId = ""; // Invalid: empty client ID
        configuration.ClientSecret = ""; // Invalid: empty client secret

        // Act
        var result = await _repository.ValidateAndSaveAsync(configuration, validateConnections: false);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Client ID cannot be empty"));
        Assert.Contains(result.Errors, e => e.Contains("Client Secret cannot be empty"));
    }

    [Fact]
    public async Task ValidateAndSaveAsync_CredentialsWithColon_ShouldReturnErrors()
    {
        // Arrange
        var configuration = CreateValidConfiguration("Test Config");
        configuration.ClientId = "client:with:colon"; // Invalid: contains colon
        configuration.ClientSecret = "secret:with:colon"; // Invalid: contains colon

        // Act
        var result = await _repository.ValidateAndSaveAsync(configuration, validateConnections: false);

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Client ID cannot contain colon"));
        Assert.Contains(result.Errors, e => e.Contains("Client Secret cannot contain colon"));
    }

    [Fact]
    public async Task UpdateCredentialsAsync_ValidCredentials_ShouldUpdateSuccessfully()
    {
        // Arrange
        var configuration = CreateValidConfiguration("Test Config");
        var added = await _repository.AddAsync(configuration);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _repository.UpdateCredentialsAsync(added.Id, "new-client-id", "new-client-secret");

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Errors);

        // Verify the credentials were updated
        var updated = await _repository.GetByIdAsync(added.Id);
        Assert.NotNull(updated);
        Assert.Equal("new-client-id", updated.ClientId);
        Assert.Equal("new-client-secret", updated.ClientSecret);
    }

    [Fact]
    public async Task UpdateCredentialsAsync_InvalidCredentials_ShouldReturnErrors()
    {
        // Arrange
        var configuration = CreateValidConfiguration("Test Config");
        var added = await _repository.AddAsync(configuration);
        await _repository.SaveChangesAsync();

        // Act
        var result = await _repository.UpdateCredentialsAsync(added.Id, "client:with:colon", "");

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Client ID cannot contain colon"));
        Assert.Contains(result.Errors, e => e.Contains("Client Secret cannot be empty"));

        // Verify the credentials were not updated
        var unchanged = await _repository.GetByIdAsync(added.Id);
        Assert.NotNull(unchanged);
        Assert.Equal("default-client", unchanged.ClientId);
        Assert.Equal("default-secret-2024", unchanged.ClientSecret);
    }

    [Fact]
    public async Task UpdateCredentialsAsync_NonExistentConfiguration_ShouldReturnError()
    {
        // Act
        var result = await _repository.UpdateCredentialsAsync(999, "new-client-id", "new-client-secret");

        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Configuration with ID 999 not found"));
    }

    private static BackupConfiguration CreateValidConfiguration(string name)
    {
        // Create a temporary directory for testing
        var tempDir = Path.GetTempPath();
        
        return new BackupConfiguration
        {
            Name = name,
            MySQLConnection = new MySQLConnectionInfo
            {
                Username = "testuser",
                Password = "testpass",
                ServiceName = "mysql",
                DataDirectoryPath = tempDir, // Use temp directory that exists
                Host = "localhost",
                Port = 3306
            },
            DataDirectoryPath = tempDir, // Use temp directory that exists
            ServiceName = "mysql",
            TargetServer = new ServerEndpoint
            {
                IPAddress = "127.0.0.1", // Use localhost IP
                Port = 8080,
                UseSSL = true
            },
            TargetDirectory = tempDir, // Use temp directory that exists
            NamingStrategy = new FileNamingStrategy
            {
                Pattern = "{timestamp}_{database}_{server}.zip",
                DateFormat = "yyyyMMdd_HHmmss",
                IncludeServerName = true,
                IncludeDatabaseName = true
            },
            IsActive = true
        };
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}