# Design Document: HttpClient Dependency Fix

## Overview

This design addresses the dependency injection issue in the MySQL Backup Tool where AlertingService cannot be instantiated due to missing HttpClient registration. The solution implements proper HttpClient registration using .NET's recommended IHttpClientFactory pattern, ensuring reliable HTTP operations with appropriate configuration for timeouts, retry policies, and cross-application compatibility.

The fix will be implemented in the AddSharedServices method to ensure consistency across both client and server applications, with proper configuration management for AlertingConfig dependencies.

## Architecture

### Current State
```
AddSharedServices() Method:
├── AlertingService (registered) ✓
│   └── Constructor Dependencies:
│       ├── ILogger<AlertingService> (available) ✓
│       ├── HttpClient (missing) ❌
│       └── AlertingConfig? (unknown status) ❓
└── Other shared services...
```

### Target State
```
AddSharedServices() Method:
├── IHttpClientFactory (registered) ✓
├── HttpClient (via typed client) ✓
├── AlertingConfig (registered) ✓
├── AlertingService (registered) ✓
│   └── Constructor Dependencies:
│       ├── ILogger<AlertingService> (available) ✓
│       ├── HttpClient (injected) ✓
│       └── AlertingConfig? (injected) ✓
└── Other shared services...
```

### Registration Strategy

The design uses the **Typed Client** pattern rather than IHttpClientFactory injection directly into AlertingService. This approach:
- Allows direct HttpClient injection into AlertingService constructor
- Provides better encapsulation and configuration management
- Maintains compatibility with existing AlertingService constructor signature
- Enables specific configuration for AlertingService HTTP operations

## Components and Interfaces

### 1. HttpClient Registration Component

**Purpose**: Register HttpClient as a typed client for AlertingService

**Implementation Approach**:
```csharp
// In AddSharedServices method
services.AddHttpClient<AlertingService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "MySqlBackupTool/1.0");
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetTimeoutPolicy());
```

**Key Features**:
- Typed client registration specifically for AlertingService
- Centralized configuration in AddSharedServices
- Automatic HttpClient lifecycle management
- Support for policy handlers (retry, timeout, circuit breaker)

### 2. Configuration Management Component

**Purpose**: Handle AlertingConfig registration and binding

**Implementation Approach**:
```csharp
// Configuration binding with fallback
services.Configure<AlertingConfig>(configuration.GetSection("Alerting"));
services.AddSingleton<AlertingConfig>(provider =>
{
    var config = provider.GetService<IOptions<AlertingConfig>>()?.Value;
    return config ?? new AlertingConfig(); // Provide defaults
});
```

**Key Features**:
- Configuration section binding from appsettings
- Graceful fallback to default configuration
- Singleton lifetime for configuration consistency
- Support for environment-specific settings

### 3. Resilience Policy Component

**Purpose**: Implement retry and timeout policies for HTTP operations

**Policy Configuration**:
- **Retry Policy**: Exponential backoff with jitter
- **Timeout Policy**: Per-request timeout separate from HttpClient timeout
- **Circuit Breaker**: Optional for high-availability scenarios

**Implementation Details**:
```csharp
private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return Policy
        .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
        .Or<HttpRequestException>()
        .Or<TaskCanceledException>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => 
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + 
                TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                // Log retry attempts
            });
}
```

## Data Models

### AlertingConfig Model

**Purpose**: Configuration object for AlertingService HTTP operations

**Properties**:
```csharp
public class AlertingConfig
{
    public string? BaseUrl { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetryAttempts { get; set; } = 3;
    public bool EnableCircuitBreaker { get; set; } = false;
    public Dictionary<string, string> DefaultHeaders { get; set; } = new();
}
```

**Configuration Binding**:
- Binds from "Alerting" configuration section
- Provides sensible defaults for all properties
- Supports environment-specific overrides
- Validates configuration values during startup

### Service Registration Model

**Purpose**: Encapsulate service registration logic

**Structure**:
```csharp
public static class HttpClientServiceExtensions
{
    public static IServiceCollection AddAlertingHttpClient(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // HttpClient registration logic
        // Configuration binding logic
        // Policy registration logic
        return services;
    }
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

Let me analyze the acceptance criteria to determine which ones can be tested as properties.

Based on the prework analysis, I'll now create properties that eliminate redundancy and focus on the most valuable correctness guarantees:

### Property 1: Service Resolution Consistency
*For any* application context (client or server), when AddSharedServices is called, all required services (HttpClient, AlertingService, AlertingConfig) should be resolvable from the DI container without errors.
**Validates: Requirements 1.1, 1.3, 3.1, 5.1, 5.2, 5.3**

### Property 2: Dependency Injection Completeness
*For any* service resolution request for AlertingService, all constructor dependencies (ILogger, HttpClient, AlertingConfig) should be successfully injected and non-null.
**Validates: Requirements 1.2, 3.2, 4.3**

### Property 3: Configuration Binding Consistency
*For any* valid configuration section, AlertingConfig should be bound with the same values regardless of application context, and missing configuration should result in consistent default values.
**Validates: Requirements 2.3, 3.4, 5.3**

### Property 4: HTTP Client Configuration Correctness
*For any* HttpClient instance resolved from the DI container, it should have the configured timeout values, default headers, and retry policies applied.
**Validates: Requirements 2.1, 2.4**

### Property 5: Retry Policy Behavior
*For any* HTTP request that results in a transient failure, the retry policy should attempt the configured number of retries with exponential backoff before ultimately failing.
**Validates: Requirements 2.2**

### Property 6: Error Handling and Logging
*For any* configuration or dependency resolution error, the system should log descriptive error messages and continue operation with fallback behavior where appropriate.
**Validates: Requirements 6.1, 6.2, 6.3**

### Property 7: Service Registration Isolation
*For any* existing service registration in client or server applications, adding the HttpClient and AlertingService registrations should not conflict with or override existing services.
**Validates: Requirements 5.4**

## Error Handling

### Configuration Errors
- **Missing Configuration Section**: When AlertingConfig section is missing, system logs warning and uses default configuration
- **Invalid Configuration Values**: When configuration contains invalid values (negative timeouts, etc.), system logs error and uses validated defaults
- **Configuration Binding Failures**: When configuration binding fails, system logs detailed error with section name and continues with defaults

### Dependency Resolution Errors
- **Missing Dependencies**: When required dependencies are not registered, system provides clear error message indicating which service and dependency are missing
- **Circular Dependencies**: When circular dependencies are detected, system provides clear error message with dependency chain
- **Service Activation Errors**: When service construction fails, system logs detailed error with inner exception details

### HTTP Client Errors
- **Connection Failures**: Retry policy handles transient connection failures with exponential backoff
- **Timeout Errors**: Separate timeout policy handles request timeouts independently of retry policy
- **Configuration Errors**: Invalid HttpClient configuration (negative timeouts, etc.) logs error and uses safe defaults

### Logging Strategy
- **Startup Validation**: Log all service registrations and configuration bindings during startup
- **Error Context**: Include relevant context (service type, configuration section, dependency chain) in error messages
- **Performance Impact**: Use structured logging with appropriate log levels to minimize performance impact

## Testing Strategy

### Unit Testing Approach
Unit tests will focus on specific examples and edge cases that demonstrate correct behavior:

- **Service Registration Examples**: Test that specific services can be resolved from DI container
- **Configuration Binding Examples**: Test that specific configuration values are correctly bound
- **Error Condition Examples**: Test specific error scenarios (missing config, invalid values, etc.)
- **Integration Examples**: Test that AlertingService can be successfully created and used

### Property-Based Testing Approach
Property tests will verify universal properties across all inputs using a minimum of 100 iterations per test:

- **Configuration Consistency**: Generate random configuration values and verify consistent binding across contexts
- **Service Resolution**: Generate different DI container configurations and verify service resolution works
- **Retry Behavior**: Generate various HTTP failure scenarios and verify retry policy behavior
- **Error Handling**: Generate various error conditions and verify appropriate logging and fallback behavior

### Testing Framework Configuration
- **Property Testing Library**: Use FsCheck for .NET or similar property-based testing framework
- **Test Iterations**: Minimum 100 iterations per property test to ensure comprehensive coverage
- **Test Tagging**: Each property test tagged with format: **Feature: httpclient-dependency-fix, Property {number}: {property_text}**
- **Mock Strategy**: Use mock HTTP handlers for testing retry policies and HTTP behavior without external dependencies

### Test Organization
```
Tests/
├── Unit/
│   ├── ServiceRegistrationTests.cs
│   ├── ConfigurationBindingTests.cs
│   ├── ErrorHandlingTests.cs
│   └── IntegrationTests.cs
└── Properties/
    ├── ServiceResolutionProperties.cs
    ├── ConfigurationConsistencyProperties.cs
    ├── RetryPolicyProperties.cs
    └── ErrorHandlingProperties.cs
```

### Coverage Requirements
- **Unit Tests**: Cover specific examples, edge cases, and error conditions
- **Property Tests**: Cover universal behaviors and consistency requirements
- **Integration Tests**: Verify end-to-end functionality in realistic application contexts
- **Performance Tests**: Verify that DI registration and resolution performance is acceptable