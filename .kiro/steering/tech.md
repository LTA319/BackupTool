# Technology Stack

## Framework & Runtime
- **.NET 8.0**: Primary framework for all components
- **C# 12**: Programming language with nullable reference types enabled
- **Windows Forms**: Client GUI framework (net8.0-windows target)

## Database & ORM
- **Entity Framework Core 8.0**: ORM for data access
- **SQLite**: Configuration and logging database
- **MySQL Connector**: MySql.Data 8.2.0 for database connections

## Key Libraries
- **Microsoft.Extensions.Hosting**: Background services and dependency injection
- **Microsoft.Extensions.Logging**: Structured logging framework
- **Microsoft.Extensions.Http.Polly**: HTTP resilience and retry policies
- **Polly 8.2.0**: Resilience patterns for fault handling
- **MailKit 4.3.0**: Email notifications and alerting

## Testing Framework
- **xUnit**: Primary testing framework
- **FsCheck**: Property-based testing library
- **Moq**: Mocking framework for unit tests
- **Microsoft.EntityFrameworkCore.InMemory**: In-memory database for testing

## Build System & Commands

### Solution Structure
```
MySqlBackupTool.sln
├── src/MySqlBackupTool.Client (Windows Forms)
├── src/MySqlBackupTool.Server (Console App)
├── src/MySqlBackupTool.Shared (Class Library)
└── tests/MySqlBackupTool.Tests (Test Project)
```

### Common Commands

**Build the entire solution:**
```bash
dotnet build MySqlBackupTool.sln
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

**Build for release:**
```bash
dotnet build MySqlBackupTool.sln --configuration Release
```

## Development Patterns
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection throughout
- **Async/Await**: Consistent async patterns for I/O operations
- **Interface-based Design**: All services implement interfaces for testability
- **Structured Logging**: Microsoft.Extensions.Logging with contextual information
- **Property-based Testing**: FsCheck for testing invariants and edge cases