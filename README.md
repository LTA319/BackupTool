# MySQL Full-File Backup Tool

A distributed backup solution for MySQL databases built on .NET 8, providing enterprise-grade backup capabilities with client-server architecture.

## Project Structure

The solution is organized into three main projects:

### MySqlBackupTool.Shared
Contains shared components used by both client and server:
- **Interfaces**: Core service interfaces (IMySQLManager, ICompressionService, IFileTransferClient, IFileReceiver)
- **Models**: Data models for configuration, transfer protocols, logging, and metadata
- **Data**: Entity Framework DbContext and database configuration
- **Logging**: Custom logging framework with file output
- **DependencyInjection**: Service registration and configuration

### MySqlBackupTool.Client
Windows Forms application for managing backup operations:
- **Program.cs**: Application entry point with dependency injection setup
- **FormMain**: Main user interface (to be implemented in later tasks)
- Handles MySQL instance management, file compression, and transfer initiation

### MySqlBackupTool.Server
Console application for receiving and storing backup files:
- **Program.cs**: Server entry point with hosted service configuration
- **FileReceiverService**: Background service for handling file reception
- Manages file chunking, reassembly, and storage organization

## Key Features (Planned)

- **Distributed Architecture**: Separate client and server components
- **Large File Support**: File chunking for 100GB+ databases
- **Resume Capability**: Interrupted transfer recovery
- **Multi-threading**: Background operations with progress reporting
- **Comprehensive Logging**: Detailed operation tracking and monitoring
- **Flexible Configuration**: SQLite-based configuration management
- **File Integrity**: Checksum validation for all transfers
- **Retention Policies**: Automated backup cleanup

## Database Schema

The application uses SQLite for configuration and logging storage:
- **BackupConfigurations**: Backup job definitions
- **BackupLogs**: Operation history and status
- **TransferLogs**: Detailed transfer progress
- **RetentionPolicies**: File cleanup rules

## Getting Started

### Prerequisites
- .NET 8.0 SDK
- Windows OS (for client application)
- MySQL Server (target for backup)

### Building the Solution
```bash
dotnet build MySqlBackupTool.sln
```

### Running the Server
```bash
dotnet run --project src/MySqlBackupTool.Server/MySqlBackupTool.Server.csproj
```

### Running the Client
```bash
dotnet run --project src/MySqlBackupTool.Client/MySqlBackupTool.Client.csproj
```

## Development Status

✅ **Task 1 Complete**: Project structure and core interfaces
- Solution structure with Client, Server, and Shared projects
- Core interfaces defined (IMySQLManager, ICompressionService, IFileTransferClient, IFileReceiver)
- SQLite database schema and Entity Framework configuration
- Logging framework and dependency injection setup

🔄 **Next Tasks**: Configuration management system implementation

## Architecture Overview

```
┌─────────────────┐    Network     ┌─────────────────┐
│  Backup Client  │◄──────────────►│ File Receiver   │
│                 │    Transfer    │     Server      │
├─────────────────┤                ├─────────────────┤
│ MySQL Manager   │                │ Chunk Manager   │
│ Compression     │                │ Storage Manager │
│ Transfer Client │                │ File Receiver   │
└─────────────────┘                └─────────────────┘
        │                                   │
        ▼                                   ▼
┌─────────────────┐                ┌─────────────────┐
│ Client Config   │                │ Server Config   │
│   Database      │                │   Database      │
│   (SQLite)      │                │   (SQLite)      │
└─────────────────┘                └─────────────────┘
```

## Documentation

Comprehensive documentation is available in the `docs/` directory:

- **[API Reference](docs/API-Reference.md)** - Complete API documentation for all interfaces and services
- **[User Guide](docs/User-Guide.md)** - Comprehensive user guide with setup instructions and best practices
- **[Configuration Examples](docs/Configuration-Examples.md)** - Practical configuration examples for various deployment scenarios
- **[Performance Benchmarking](docs/PerformanceBenchmarking.md)** - Performance testing and optimization guide

### Quick Links

- [Getting Started](docs/User-Guide.md#getting-started) - Initial setup and configuration
- [Basic Operations](docs/User-Guide.md#basic-operations) - Running backups and managing schedules
- [API Reference](docs/API-Reference.md) - Complete interface documentation
- [Configuration Examples](docs/Configuration-Examples.md) - Ready-to-use configuration templates

## Contributing

This project follows a task-based development approach. Each feature is implemented as a discrete task with comprehensive testing including both unit tests and property-based tests.

### Development Documentation

- All public APIs are documented with XML comments
- Comprehensive unit test coverage for all services
- Property-based testing for critical components
- Integration tests for end-to-end workflows

## License

[License information to be added]