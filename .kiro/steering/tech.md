# Technology Stack

## Framework & Runtime
- **.NET 8.0**: Primary framework for all components
- **C# 12**: Programming language with nullable reference types enabled
- **Windows Forms**: Client GUI framework (net8.0-windows target)

## Database & ORM
- **Entity Framework Core 8.0**: ORM for data access
- **SQLite**: Configuration and logging database (Microsoft.EntityFrameworkCore.Sqlite 8.0.0)
- **MySQL Connector**: MySql.Data 8.2.0 for database connections

## Key Libraries
- **Microsoft.Extensions.Hosting 8.0.0**: Background services and dependency injection
- **Microsoft.Extensions.Logging 8.0.0**: Structured logging framework
- **Microsoft.Extensions.Http 8.0.0**: HTTP client factory and configuration
- **Microsoft.Extensions.Http.Polly 8.0.0**: HTTP resilience and retry policies
- **Polly 8.2.0**: Resilience patterns for fault handling
- **MailKit 4.3.0**: Email notifications and alerting
- **System.ServiceProcess.ServiceController 8.0.0**: Windows service management

## Testing Framework
- **xUnit 2.9.3**: Primary testing framework
- **FsCheck 2.16.6**: Property-based testing library
- **FsCheck.Xunit 2.16.6**: xUnit integration for property-based tests
- **Moq 4.20.70**: Mocking framework for unit tests
- **Microsoft.EntityFrameworkCore.InMemory 8.0.0**: In-memory database for testing
- **Microsoft.NET.Test.Sdk 17.14.1**: Test platform and runners
- **coverlet.collector 6.0.4**: Code coverage collection

## Build System & Commands

### Solution Structure
```
MySqlBackupTool.sln
├── src/MySqlBackupTool.Client (Windows Forms - net8.0-windows)
├── src/MySqlBackupTool.Server (Console App - net8.0)
├── src/MySqlBackupTool.Shared (Class Library - net8.0)
└── tests/MySqlBackupTool.Tests (Test Project - net8.0-windows)
```

### Common Commands

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

**Clean solution:**
```bash
dotnet clean MySqlBackupTool.sln
```

**Restore packages:**
```bash
dotnet restore MySqlBackupTool.sln
```

## Development Patterns
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection throughout all projects
- **Async/Await**: Consistent async patterns for I/O operations and long-running tasks
- **Interface-based Design**: All services implement interfaces for testability and modularity
- **Structured Logging**: Microsoft.Extensions.Logging with contextual information and multiple providers
- **Property-based Testing**: FsCheck for testing invariants and edge cases
- **Repository Pattern**: Generic repository interfaces with Entity Framework implementations
- **Background Services**: Hosted services for long-running operations
- **Resilience Patterns**: Polly for retry policies, circuit breakers, and timeout handling
- **Configuration Management**: Options pattern with strongly-typed configuration classes

## Project-Specific Dependencies

### Client Project (MySqlBackupTool.Client)
- Windows Forms UI framework
- MySQL service management
- System tray integration
- Local SQLite database

### Server Project (MySqlBackupTool.Server)
- Console application with hosted services
- File reception and storage management
- Background task processing
- Network communication handling

### Shared Library (MySqlBackupTool.Shared)
- Complete service layer implementation
- Entity Framework data access
- Comprehensive interface definitions
- Cross-cutting concerns (logging, validation, etc.)

### Test Project (MySqlBackupTool.Tests)
- Unit tests for all service implementations
- Property-based tests for critical algorithms
- Integration tests for end-to-end workflows
- In-memory database testing
- Mock-based testing for external dependencies