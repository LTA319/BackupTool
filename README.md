# MySQL Full-File Backup Tool

A distributed enterprise-grade backup solution for MySQL databases built on .NET 8. The system provides a client-server architecture for managing large-scale database backups with advanced features like file chunking, resume capability, and comprehensive monitoring.

## Key Features

- **Distributed Architecture**: Separate client and server components for scalable backup operations
- **Large File Support**: Handles 100GB+ databases through intelligent file chunking and reassembly
- **Resume Capability**: Interrupted transfer recovery with checksum validation and token-based resumption
- **Enterprise Security**: Authentication, authorization, SSL/TLS encryption, and secure credential storage
- **Comprehensive Monitoring**: Detailed logging, progress reporting, alerting, and memory profiling
- **Flexible Configuration**: SQLite-based configuration management with retention policies and scheduling
- **Multi-threading**: Background operations with real-time progress tracking and timeout protection
- **File Integrity**: Checksum validation, compression, and encryption for all transfers
- **Automated Management**: Retention policies, backup scheduling, and startup validation

## Components

### MySqlBackupTool.Client
Windows Forms client application featuring:
- **GUI Management**: Complete backup configuration and monitoring interface
- **MySQL Control**: Start/stop MySQL services and instance management
- **System Integration**: System tray functionality with background operations
- **Configuration Forms**: Backup settings, scheduling, and retention policy management
- **Real-time Monitoring**: Progress tracking, log viewing, and transfer status
- **Report Generation**: Backup reports and operational analytics

### MySqlBackupTool.Server
Console server application with hosted services:
- **File Reception**: Multi-threaded file receiving with chunking support
- **Storage Management**: Organized file storage with directory structure
- **Background Processing**: Hosted services for continuous operation
- **Transfer Logging**: Detailed transfer progress and status tracking
- **Security Layer**: Authentication and authorization for client connections

### MySqlBackupTool.Shared
Comprehensive shared library containing:
- **35+ Interfaces**: Complete service abstractions for all components
- **20+ Models**: Data models for configuration, logging, and operations
- **40+ Services**: Full business logic implementations
- **Repository Pattern**: Entity Framework-based data access layer
- **Advanced Features**: Authentication, encryption, compression, and networking

## Technology Stack

### Framework & Runtime
- **.NET 8.0**: Primary framework for all components
- **C# 12**: Programming language with nullable reference types enabled
- **Windows Forms**: Client GUI framework (net8.0-windows target)

### Database & ORM
- **Entity Framework Core 8.0**: ORM for data access
- **SQLite**: Configuration and logging database
- **MySQL Connector**: MySql.Data 8.2.0 for database connections

### Key Libraries
- **Microsoft.Extensions.Hosting 8.0.0**: Background services and dependency injection
- **Microsoft.Extensions.Logging 8.0.0**: Structured logging framework
- **Microsoft.Extensions.Http.Polly 8.0.0**: HTTP resilience and retry policies
- **Polly 8.2.0**: Resilience patterns for fault handling
- **MailKit 4.3.0**: Email notifications and alerting
- **System.ServiceProcess.ServiceController 8.0.0**: Windows service management

### Testing Framework
- **xUnit 2.9.3**: Primary testing framework
- **FsCheck 2.16.6**: Property-based testing library
- **Moq 4.20.70**: Mocking framework for unit tests
- **Microsoft.EntityFrameworkCore.InMemory 8.0.0**: In-memory database for testing

## Architecture Overview

```
┌─────────────────────┐    Network     ┌─────────────────────┐
│   Backup Client     │◄──────────────►│  File Receiver      │
│   (Windows Forms)   │   Transfer     │     Server          │
├─────────────────────┤                ├─────────────────────┤
│ • MySQL Manager     │                │ • Chunk Manager     │
│ • Compression       │                │ • Storage Manager   │
│ • Transfer Client   │                │ • File Receiver     │
│ • Authentication    │                │ • Background Tasks  │
│ • System Tray       │                │ • Transfer Logging  │
└─────────────────────┘                └─────────────────────┘
        │                                       │
        ▼                                       ▼
┌─────────────────────┐                ┌─────────────────────┐
│ Client SQLite DB    │                │ Server SQLite DB    │
│ • Configurations    │                │ • Transfer Logs     │
│ • Backup Logs       │                │ • Resume Tokens     │
│ • Schedules         │                │ • File Metadata     │
│ • Retention Policies│                │ • Authentication    │
└─────────────────────┘                └─────────────────────┘
```

## Getting Started

### Prerequisites
- .NET 8.0 SDK
- Windows OS (for client application)
- MySQL Server (target for backup)

### Quick Start Commands

**Build the entire solution:**
```bash
dotnet build MySqlBackupTool.sln
```

**Build for release:**
```bash
dotnet build MySqlBackupTool.sln --configuration Release
```

**Run the server:**
```bash
dotnet run --project src/MySqlBackupTool.Server/MySqlBackupTool.Server.csproj
```

**Run the client:**
```bash
dotnet run --project src/MySqlBackupTool.Client/MySqlBackupTool.Client.csproj
```

**Run tests:**
```bash
dotnet test tests/MySqlBackupTool.Tests/MySqlBackupTool.Tests.csproj
```

**Run tests with coverage:**
```bash
dotnet test tests/MySqlBackupTool.Tests/MySqlBackupTool.Tests.csproj --collect:"XPlat Code Coverage"
```

### Project Structure

```
MySqlBackupTool/
├── src/                           # Source code
│   ├── MySqlBackupTool.Client/    # Windows Forms client application
│   │   ├── Forms/                 # UI forms (Configuration, Monitoring, Logs, etc.)
│   │   ├── Tools/                 # Utility classes and system integration
│   │   └── Properties/            # Application resources
│   ├── MySqlBackupTool.Server/    # Console server application  
│   │   └── FileReceiverService.cs # Main file reception service
│   └── MySqlBackupTool.Shared/    # Shared library components
│       ├── Data/                  # Entity Framework and repositories
│       ├── Interfaces/            # Service contracts (35+ interfaces)
│       ├── Models/                # Data models and DTOs (20+ models)
│       ├── Services/              # Business logic (40+ services)
│       └── DependencyInjection/   # Service registration
├── tests/                         # Test projects
│   └── MySqlBackupTool.Tests/     # Comprehensive test suite
│       ├── Services/              # Service layer unit tests
│       ├── Integration/           # End-to-end integration tests
│       ├── Properties/            # Property-based tests (FsCheck)
│       └── Benchmarks/            # Performance benchmarks
├── docs/                          # Documentation (English & Chinese)
├── examples/                      # Code examples and samples
└── mardown/                       # Implementation summaries and guides
```

## Advanced Capabilities

### Authentication & Security
- **Multi-layered Security**: Authentication, authorization, and audit trails
- **Secure Credential Storage**: Encrypted credential management
- **SSL/TLS Support**: Secure network communications
- **Certificate Management**: Client certificate validation

### Network Resilience
- **Retry Policies**: Configurable retry strategies with exponential backoff
- **Timeout Protection**: Comprehensive timeout handling for all operations
- **Error Recovery**: Automatic recovery from network interruptions
- **Connection Management**: Optimized connection pooling and lifecycle

### Performance Optimization
- **Memory Profiling**: Built-in memory usage monitoring and optimization
- **Benchmarking Suite**: Performance testing for critical operations
- **Optimized Transfers**: Multiple file transfer implementations for different scenarios
- **Compression Streaming**: Efficient compression with timeout protection

### Operational Excellence
- **Startup Validation**: Comprehensive system validation on startup
- **Dependency Resolution**: Automatic dependency checking and validation
- **Background Services**: Robust hosted service implementations
- **Comprehensive Error Handling**: Detailed error reporting and recovery

## Key Interfaces & Services

### Core Services
- `IMySQLManager` - MySQL instance lifecycle management
- `IFileTransferClient` - File transfer operations (multiple implementations)
- `IFileReceiver` - Server-side file reception and processing
- `ICompressionService` - File compression/decompression with timeout protection
- `IEncryptionService` - Data encryption/decryption

### Data Access Layer
- `IRepository<T>` - Generic repository pattern
- `IBackupConfigurationRepository` - Backup configuration management
- `IBackupLogRepository` - Operation logging and history
- `ITransferLogRepository` - Detailed transfer progress tracking
- `IRetentionPolicyRepository` - Backup retention management

### Infrastructure Services
- `ILoggingService` - Enhanced logging capabilities
- `INotificationService` - Alerting and notifications
- `IMemoryProfiler` - Performance monitoring and profiling
- `IAuthenticationService` - User authentication and security
- `IBackgroundTaskManager` - Background service management

### Advanced Features
- `IBackupOrchestrator` - Coordinated backup operations
- `IChunkManager` - File chunking and reassembly
- `IChecksumService` - File integrity validation
- `IErrorRecoveryManager` - Error handling and recovery
- `INetworkRetryService` - Network resilience and retry logic

## Testing Strategy

The project includes comprehensive testing with multiple approaches:

### Test Categories
- **Unit Tests**: Service layer testing with mocking
- **Integration Tests**: End-to-end workflow testing
- **Property-based Tests**: FsCheck for testing invariants and edge cases
- **Benchmarks**: Performance testing for critical operations
- **Dependency Injection Tests**: Service registration validation

### Test Coverage
- **Services**: 40+ service implementations with comprehensive unit tests
- **Models**: Data model validation and serialization tests
- **Repositories**: Data access layer testing with in-memory databases
- **Authentication**: Security flow and error scenario testing
- **Network Operations**: File transfer and retry policy testing

### Property-based Testing
Using FsCheck for testing critical algorithms:
- Compression round-trip properties
- File chunking and reassembly invariants
- Authentication token format validation
- Configuration persistence properties
- Network retry behavior validation

## Documentation

Comprehensive documentation is available in both English and Chinese:

### English Documentation
- **[API Reference](docs/API-Reference.md)** - Complete API documentation for all interfaces and services
- **[User Guide](docs/User-Guide.md)** - Comprehensive user guide with setup instructions and best practices
- **[Configuration Examples](docs/Configuration-Examples.md)** - Practical configuration examples for various deployment scenarios
- **[Performance Benchmarking](docs/PerformanceBenchmarking.md)** - Performance testing and optimization guide

### Chinese Documentation (中文文档)
- **[API 参考](docs/API-Reference-Zh.md)** - 完整的 API 文档
- **[用户指南](docs/User-Guide-Zh.md)** - 全面的用户指南
- **[配置示例](docs/Configuration-Examples-Zh.md)** - 实用配置示例
- **[性能基准测试](docs/PerformanceBenchmarking-Zh.md)** - 性能测试指南

### Quick Links
- [Getting Started](docs/User-Guide.md#getting-started) - Initial setup and configuration
- [Basic Operations](docs/User-Guide.md#basic-operations) - Running backups and managing schedules
- [API Reference](docs/API-Reference.md) - Complete interface documentation
- [Configuration Examples](docs/Configuration-Examples.md) - Ready-to-use configuration templates

## Examples & Samples

The `examples/` directory contains practical code samples:
- **MemoryProfilingExample.cs** - Memory usage monitoring implementation
- **StartupValidationExample.cs** - System validation on application startup
- **TransferLogManagementExample.cs** - Transfer logging and management
- **TransferLogViewerExample.cs** - Log viewing and analysis

## Contributing

This project follows enterprise development practices:

### Development Standards
- **Clean Architecture**: Clear separation of concerns with interface-based design
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection throughout
- **Async/Await**: Consistent async patterns for I/O operations
- **Structured Logging**: Microsoft.Extensions.Logging with contextual information
- **Comprehensive Testing**: Unit, integration, and property-based testing
- **Documentation**: XML comments for all public APIs

### Code Quality
- **Interface-based Design**: All services implement interfaces for testability
- **Repository Pattern**: Generic repository interfaces with Entity Framework implementations
- **Background Services**: Hosted services for long-running operations
- **Resilience Patterns**: Polly for retry policies, circuit breakers, and timeout handling
- **Configuration Management**: Options pattern with strongly-typed configuration classes

## License

[License information to be added]