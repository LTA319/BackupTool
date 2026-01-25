# Implementation Plan: HttpClient Dependency Fix

## Overview

This implementation plan addresses the dependency injection issue where AlertingService cannot be instantiated due to missing HttpClient registration. The solution implements proper HttpClient registration using .NET's typed client pattern, with appropriate configuration, retry policies, and cross-application compatibility.

## Tasks

- [x] 1. Locate and analyze current service registration structure
  - Find the AddSharedServices method in the codebase
  - Identify current AlertingService registration
  - Document existing service registrations to avoid conflicts
  - _Requirements: 1.4, 5.4_

- [ ] 2. Implement HttpClient registration with typed client pattern
  - [x] 2.1 Add HttpClient registration for AlertingService in AddSharedServices method
    - Register HttpClient as typed client for AlertingService
    - Configure basic timeout and user agent settings
    - _Requirements: 1.1, 1.2, 2.1, 2.4_
  
  - [ ]* 2.2 Write unit test for HttpClient registration
    - Test that HttpClient can be resolved from DI container
    - Test that AlertingService can be resolved with HttpClient dependency
    - _Requirements: 1.1, 1.2_
  
  - [ ]* 2.3 Write property test for service resolution consistency
    - **Property 1: Service Resolution Consistency**
    - **Validates: Requirements 1.1, 1.3, 3.1, 5.1, 5.2, 5.3**

- [ ] 3. Implement AlertingConfig registration and binding
  - [x] 3.1 Create AlertingConfig class with appropriate properties
    - Define configuration properties (BaseUrl, TimeoutSeconds, MaxRetryAttempts, etc.)
    - Implement default values and validation
    - _Requirements: 3.1, 3.3_
  
  - [x] 3.2 Add AlertingConfig registration in AddSharedServices method
    - Bind configuration from "Alerting" section
    - Implement fallback to default configuration when section is missing
    - _Requirements: 3.1, 3.2, 3.4_
  
  - [ ]* 3.3 Write unit tests for AlertingConfig registration
    - Test configuration binding with valid configuration
    - Test fallback behavior with missing configuration
    - _Requirements: 3.1, 3.3, 3.4_
  
  - [ ]* 3.4 Write property test for configuration binding consistency
    - **Property 3: Configuration Binding Consistency**
    - **Validates: Requirements 2.3, 3.4, 5.3**

- [x] 4. Checkpoint - Verify basic service registration
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 5. Implement HTTP resilience policies
  - [x] 5.1 Create retry policy with exponential backoff
    - Implement retry policy for transient HTTP failures
    - Configure exponential backoff with jitter
    - Add logging for retry attempts
    - _Requirements: 2.2_
  
  - [x] 5.2 Create timeout policy for HTTP requests
    - Implement per-request timeout policy
    - Configure timeout values from AlertingConfig
    - _Requirements: 2.1, 2.2_
  
  - [x] 5.3 Integrate policies with HttpClient registration
    - Add policy handlers to HttpClient registration
    - Ensure policies work together correctly
    - _Requirements: 2.2, 2.3_
  
  - [ ]* 5.4 Write property test for retry policy behavior
    - **Property 5: Retry Policy Behavior**
    - **Validates: Requirements 2.2**
  
  - [ ]* 5.5 Write unit tests for HTTP client configuration
    - Test timeout configuration is applied correctly
    - Test default headers are set properly
    - _Requirements: 2.1, 2.4_

- [ ] 6. Implement comprehensive error handling and logging
  - [x] 6.1 Add startup validation for service registration
    - Validate all required services can be resolved during startup
    - Log service registration status and configuration
    - _Requirements: 6.4_
  
  - [x] 6.2 Implement error handling for configuration issues
    - Handle missing or invalid configuration gracefully
    - Log descriptive error messages with context
    - _Requirements: 6.2, 6.3_
  
  - [x] 6.3 Add error handling for dependency resolution failures
    - Provide clear error messages for missing dependencies
    - Include dependency chain information in error messages
    - _Requirements: 6.1_
  
  - [ ]* 6.4 Write property test for error handling and logging
    - **Property 6: Error Handling and Logging**
    - **Validates: Requirements 6.1, 6.2, 6.3**

- [ ] 7. Test cross-application compatibility
  - [x] 7.1 Test service registration in client application context
    - Verify HttpClient and AlertingService resolve correctly in client app
    - Test that Backup Monitor functionality works without DI errors
    - _Requirements: 4.1, 4.2, 5.1_
  
  - [-] 7.2 Test service registration in server application context
    - Verify HttpClient and AlertingService resolve correctly in server app
    - Ensure no conflicts with existing server-side services
    - _Requirements: 5.2, 5.4_
  
  - [ ]* 7.3 Write property test for dependency injection completeness
    - **Property 2: Dependency Injection Completeness**
    - **Validates: Requirements 1.2, 3.2, 4.3**
  
  - [ ]* 7.4 Write property test for service registration isolation
    - **Property 7: Service Registration Isolation**
    - **Validates: Requirements 5.4**

- [ ] 8. Integration testing and validation
  - [~] 8.1 Create integration test for complete AlertingService functionality
    - Test AlertingService can be created and used for HTTP operations
    - Verify all dependencies are properly injected and functional
    - _Requirements: 4.2, 4.3_
  
  - [~] 8.2 Test Backup Monitor integration
    - Verify Backup Monitor can open without dependency injection errors
    - Test that AlertingService functions correctly within Backup Monitor
    - _Requirements: 4.1, 4.2_
  
  - [ ]* 8.3 Write property test for HTTP client configuration correctness
    - **Property 4: HTTP Client Configuration Correctness**
    - **Validates: Requirements 2.1, 2.4**

- [ ] 9. Final checkpoint and documentation
  - [~] 9.1 Verify all tests pass and functionality works end-to-end
    - Run all unit tests and property tests
    - Verify Backup Monitor opens successfully
    - Test AlertingService HTTP operations work correctly
  
  - [~] 9.2 Update configuration documentation
    - Document new AlertingConfig configuration section
    - Provide examples of configuration values
    - Document default values and fallback behavior
    - _Requirements: 3.4, 6.3_

- [~] 10. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Property tests validate universal correctness properties with minimum 100 iterations
- Unit tests validate specific examples and edge cases
- Integration tests verify end-to-end functionality in realistic contexts
- The solution uses .NET's recommended typed client pattern for HttpClient registration
- Retry policies use exponential backoff with jitter to prevent retry storms
- Configuration binding supports environment-specific overrides and graceful fallbacks