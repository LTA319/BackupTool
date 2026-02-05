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
├── docs/                          # Documentation (English & Chinese)
├── examples/                      # Code examples and samples
├── mardown/                       # Implementation summaries and guides
└── logs/                          # Application log files (runtime)
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
├── Interfaces/                    # Service contracts and abstractions (35+ interfaces)
├── Models/                        # Data models and DTOs (20+ model files)
├── Services/                      # Business logic implementations (40+ services)
├── Logging/                       # Custom logging extensions
└── Helps/                         # Utility and helper classes
```

## Key Interface Categories

**Core Services:**
- `IMySQLManager` - MySQL instance lifecycle management
- `IFileTransferClient` - File transfer operations with multiple implementations
- `IFileReceiver` - Server-side file reception and processing
- `ICompressionService` - File compression/decompression with timeout protection
- `IEncryptionService` - Data encryption/decryption

**Data Access:**
- `IRepository<T>` - Generic repository pattern
- `IBackupConfigurationRepository` - Backup configuration management
- `IBackupLogRepository` - Operation logging and history
- `ITransferLogRepository` - Detailed transfer progress tracking
- `IRetentionPolicyRepository` - Backup retention management
- `IScheduleConfigurationRepository` - Backup scheduling

**Infrastructure:**
- `ILoggingService` - Enhanced logging capabilities
- `INotificationService` - Alerting and notifications
- `IMemoryProfiler` - Performance monitoring and profiling
- `IAuthenticationService` - User authentication and security
- `IBackgroundTaskManager` - Background service management

**Advanced Features:**
- `IBackupOrchestrator` - Coordinated backup operations
- `IChunkManager` - File chunking and reassembly
- `IChecksumService` - File integrity validation
- `IErrorRecoveryManager` - Error handling and recovery
- `INetworkRetryService` - Network resilience and retry logic

## Test Organization

```
MySqlBackupTool.Tests/
├── Services/                      # Service layer unit tests
├── Models/                        # Model validation tests
├── Data/                          # Repository and data access tests
├── Integration/                   # End-to-end integration tests
├── Properties/                    # Property-based tests (FsCheck)
├── Benchmarks/                    # Performance benchmarks (excluded from build)
└── DependencyInjection/           # DI container tests
```

## Naming Conventions

- **Interfaces**: Start with `I` (e.g., `IMySQLManager`, `IFileTransferClient`)
- **Services**: End with `Service` (e.g., `BackupSchedulerService`, `CompressionService`)
- **Repositories**: End with `Repository` (e.g., `BackupLogRepository`, `TransferLogRepository`)
- **Models**: Descriptive nouns (e.g., `BackupConfiguration`, `MySQLConnectionInfo`)
- **Tests**: End with `Tests` (e.g., `MySQLManagerTests`, `FileTransferTests`)
- **Property Tests**: End with `PropertyTests` (e.g., `CompressionPropertyTests`)

## Configuration Files

- **appsettings.json**: Server application configuration
- **appsettings.Development.json**: Development-specific settings
- ***.csproj**: Project files with package references and build settings
- **MySqlBackupTool.sln**: Solution file defining project relationships

## Database Files

- **client_backup_tool.db**: Client-side SQLite database for configuration and logs
- **server_backup_tool.db**: Server-side SQLite database for transfer management

## Documentation Structure

```
docs/
├── API-Reference.md               # Complete API documentation
├── API-Reference-Zh.md            # Chinese API documentation
├── User-Guide.md                  # Comprehensive user guide
├── User-Guide-Zh.md               # Chinese user guide
├── Configuration-Examples.md      # Practical configuration examples
├── Configuration-Examples-Zh.md   # Chinese configuration examples
├── PerformanceBenchmarking.md     # Performance testing guide
├── PerformanceBenchmarking-Zh.md  # Chinese performance guide
└── README.md                      # Documentation overview
```