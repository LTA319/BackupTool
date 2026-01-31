# Project Structure

## Solution Organization

The solution follows a clean architecture pattern with clear separation of concerns:

```
MySqlBackupTool/
├── src/                           # Source code
│   ├── MySqlBackupTool.Client/    # Windows Forms client application
│   ├── MySqlBackupTool.Server/    # Console server application  
│   └── MySqlBackupTool.Shared/    # Shared library components
├── tests/                         # Test projects
│   └── MySqlBackupTool.Tests/     # Comprehensive test suite
├── docs/                          # Documentation
├── examples/                      # Code examples and samples
└── logs/                          # Application log files
```

## Shared Library Structure

The `MySqlBackupTool.Shared` project contains all common components:

```
MySqlBackupTool.Shared/
├── Data/                          # Entity Framework and repositories
│   ├── BackupDbContext.cs         # Main EF DbContext
│   ├── Migrations/                # Database migration services
│   └── Repositories/              # Repository pattern implementations
├── DependencyInjection/           # Service registration extensions
├── Interfaces/                    # Service contracts and abstractions
├── Models/                        # Data models and DTOs
├── Services/                      # Business logic implementations
├── Logging/                       # Custom logging extensions
└── Helps/                         # Utility and helper classes
```

## Key Interface Categories

**Core Services:**
- `IMySQLManager` - MySQL instance lifecycle management
- `IFileTransferClient` - File transfer operations
- `IFileReceiver` - Server-side file reception
- `ICompressionService` - File compression/decompression
- `IEncryptionService` - Data encryption/decryption

**Data Access:**
- `IRepository<T>` - Generic repository pattern
- `IBackupConfigurationRepository` - Backup configuration management
- `IBackupLogRepository` - Operation logging and history

**Infrastructure:**
- `ILoggingService` - Enhanced logging capabilities
- `INotificationService` - Alerting and notifications
- `IMemoryProfiler` - Performance monitoring

## Test Organization

```
MySqlBackupTool.Tests/
├── Services/                      # Service layer unit tests
├── Models/                        # Model validation tests
├── Data/                          # Repository and data access tests
├── Integration/                   # End-to-end integration tests
├── Properties/                    # Property-based tests (FsCheck)
├── Benchmarks/                    # Performance benchmarks
└── DependencyInjection/           # DI container tests
```

## Naming Conventions

- **Interfaces**: Start with `I` (e.g., `IMySQLManager`)
- **Services**: End with `Service` (e.g., `BackupSchedulerService`)
- **Repositories**: End with `Repository` (e.g., `BackupLogRepository`)
- **Models**: Descriptive nouns (e.g., `BackupConfiguration`, `MySQLConnectionInfo`)
- **Tests**: End with `Tests` (e.g., `MySQLManagerTests`)
- **Property Tests**: End with `PropertyTests` (e.g., `CompressionPropertyTests`)

## Configuration Files

- **appsettings.json**: Server application configuration
- **appsettings.Development.json**: Development-specific settings
- ***.csproj**: Project files with package references and build settings
- **MySqlBackupTool.sln**: Solution file defining project relationships

## Database Files

- **client_backup_tool.db**: Client-side SQLite database
- **server_backup_tool.db**: Server-side SQLite database