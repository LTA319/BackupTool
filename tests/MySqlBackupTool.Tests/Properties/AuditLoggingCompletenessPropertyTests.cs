using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using System.Diagnostics;
using Xunit;

namespace MySqlBackupTool.Tests.Properties;

/// <summary>
/// Property-based tests for authentication audit logging completeness
/// **Property 8: Audit logging completeness**
/// **Validates: Requirements 4.5, 5.5**
/// </summary>
public class AuditLoggingCompletenessPropertyTests
{
    private readonly Mock<ILogger<AuthenticatedFileTransferClient>> _clientLoggerMock;
    private readonly Mock<ILogger<FileReceiver>> _serverLoggerMock;
    private readonly Mock<ILogger<AuthenticationAuditService>> _auditLoggerMock;
    private readonly Mock<IAuthenticationService> _authServiceMock;
    private readonly Mock<IChecksumService> _checksumServiceMock;
    private readonly Mock<ISecureCredentialStorage> _credentialStorageMock;
    private readonly Mock<IStorageManager> _storageManagerMock;
    private readonly Mock<IChunkManager> _chunkManagerMock;
    private readonly Mock<IAuthorizationService> _authorizationServiceMock;

    public AuditLoggingCompletenessPropertyTests()
    {
        _clientLoggerMock = new Mock<ILogger<AuthenticatedFileTransferClient>>();
        _serverLoggerMock = new Mock<ILogger<FileReceiver>>();
        _auditLoggerMock = new Mock<ILogger<AuthenticationAuditService>>();
        _authServiceMock = new Mock<IAuthenticationService>();
        _checksumServiceMock = new Mock<IChecksumService>();
        _credentialStorageMock = new Mock<ISecureCredentialStorage>();
        _storageManagerMock = new Mock<IStorageManager>();
        _chunkManagerMock = new Mock<IChunkManager>();
        _authorizationServiceMock = new Mock<IAuthorizationService>();
    }

    /// <summary>
    /// Property: All authentication token creation attempts must be logged with timestamps, client identifiers, and outcomes
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TokenCreationAuditLogging()
    {
        return Prop.ForAll(
            Gen.Elements("client1", "client2", "testclient", "backup-client").ToArbitrary(),
            Gen.Elements("secret123", "password456", "mysecret", "backup-secret").ToArbitrary(),
            Arb.From<bool>(),
            (clientId, clientSecret, shouldSucceed) =>
            {
                // Arrange
                var auditLogs = new List<AuthenticationAuditLog>();
                var auditService = new TestAuthenticationAuditService(auditLogs);
                
                var credentials = new ClientCredentials
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret
                };

                _credentialStorageMock.Setup(x => x.EnsureDefaultCredentialsExistAsync())
                    .ReturnsAsync(true);
                _credentialStorageMock.Setup(x => x.GetDefaultCredentialsAsync())
                    .ReturnsAsync(shouldSucceed ? credentials : null);

                var client = new AuthenticatedFileTransferClient(
                    _clientLoggerMock.Object,
                    _authServiceMock.Object,
                    _checksumServiceMock.Object,
                    _credentialStorageMock.Object,
                    auditService);

                var config = new BackupConfiguration
                {
                    ClientId = shouldSucceed ? clientId : "",
                    ClientSecret = shouldSucceed ? clientSecret : ""
                };

                // Act
                var stopwatch = Stopwatch.StartNew();
                Exception? caughtException = null;
                try
                {
                    var result = client.CreateAuthenticationTokenAsync(config).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                }
                stopwatch.Stop();

                // Assert
                var auditLog = auditLogs.FirstOrDefault();
                
                return (auditLog != null).Label("Audit log must be created")
                    .And((auditLog.Operation == AuthenticationOperation.TokenCreation).Label("Operation must be TokenCreation"))
                    .And((auditLog.Timestamp <= DateTime.UtcNow && auditLog.Timestamp >= DateTime.UtcNow.AddMinutes(-1)).Label("Timestamp must be recent"))
                    .And((auditLog.DurationMs >= 0 && auditLog.DurationMs <= stopwatch.ElapsedMilliseconds + 1000).Label("Duration must be reasonable"))
                    .And((shouldSucceed ? 
                           (auditLog.Outcome == AuthenticationOutcome.Success && auditLog.ClientId == clientId) :
                           (auditLog.Outcome == AuthenticationOutcome.Failure && !string.IsNullOrEmpty(auditLog.ErrorCode))
                       ).Label("Outcome must match expected result"));
            })
            .Label("Feature: authentication-token-fix, Property 8: Audit logging completeness - Token Creation");
    }

    /// <summary>
    /// Property: All authentication token validation attempts must be logged with timestamps, client identifiers, and outcomes
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TokenValidationAuditLogging()
    {
        return Prop.ForAll(
            Gen.Elements("client1", "client2", "testclient", "backup-client").ToArbitrary(),
            Gen.Elements("secret123", "password456", "mysecret", "backup-secret").ToArbitrary(),
            Arb.From<bool>(),
            (clientId, clientSecret, shouldSucceed) =>
            {
                // Arrange
                var auditLogs = new List<AuthenticationAuditLog>();
                var auditService = new TestAuthenticationAuditService(auditLogs);

                _credentialStorageMock.Setup(x => x.ValidateCredentialsAsync(clientId, clientSecret))
                    .ReturnsAsync(shouldSucceed);

                var fileReceiver = new FileReceiver(
                    _serverLoggerMock.Object,
                    _storageManagerMock.Object,
                    _chunkManagerMock.Object,
                    _checksumServiceMock.Object,
                    _authServiceMock.Object,
                    _authorizationServiceMock.Object,
                    _credentialStorageMock.Object,
                    auditService);

                // Create a valid base64 token
                var credentials = $"{clientId}:{clientSecret}";
                var token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(credentials));

                // Act
                var stopwatch = Stopwatch.StartNew();
                var result = fileReceiver.ValidateTokenAsync(token).GetAwaiter().GetResult();
                stopwatch.Stop();

                // Assert
                var auditLog = auditLogs.FirstOrDefault();
                
                return (auditLog != null).Label("Audit log must be created")
                    .And((auditLog.Operation == AuthenticationOperation.TokenValidation).Label("Operation must be TokenValidation"))
                    .And((auditLog.Timestamp <= DateTime.UtcNow && auditLog.Timestamp >= DateTime.UtcNow.AddMinutes(-1)).Label("Timestamp must be recent"))
                    .And((auditLog.DurationMs >= 0 && auditLog.DurationMs <= stopwatch.ElapsedMilliseconds + 1000).Label("Duration must be reasonable"))
                    .And((auditLog.ClientId == clientId).Label("Client ID must be logged"))
                    .And((shouldSucceed ? 
                           (auditLog.Outcome == AuthenticationOutcome.Success) :
                           (auditLog.Outcome == AuthenticationOutcome.Failure && !string.IsNullOrEmpty(auditLog.ErrorCode))
                       ).Label("Outcome must match expected result"));
            })
            .Label("Feature: authentication-token-fix, Property 8: Audit logging completeness - Token Validation");
    }

    /// <summary>
    /// Property: All authentication failures must be logged without exposing sensitive information
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AuthenticationFailureSecurityLogging()
    {
        return Prop.ForAll(
            Gen.Elements("client1", "client2", "testclient", "backup-client").ToArbitrary(),
            Gen.Elements("invalid123", "wrongpass", "badsecret", "incorrect").ToArbitrary(),
            (clientId, invalidSecret) =>
            {
                // Arrange
                var auditLogs = new List<AuthenticationAuditLog>();
                var auditService = new TestAuthenticationAuditService(auditLogs);

                _credentialStorageMock.Setup(x => x.ValidateCredentialsAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(false);

                var fileReceiver = new FileReceiver(
                    _serverLoggerMock.Object,
                    _storageManagerMock.Object,
                    _chunkManagerMock.Object,
                    _checksumServiceMock.Object,
                    _authServiceMock.Object,
                    _authorizationServiceMock.Object,
                    _credentialStorageMock.Object,
                    auditService);

                // Create a token with invalid credentials
                var credentials = $"{clientId}:{invalidSecret}";
                var token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(credentials));

                // Act
                var result = fileReceiver.ValidateTokenAsync(token).GetAwaiter().GetResult();

                // Assert
                var auditLog = auditLogs.FirstOrDefault();
                
                return (auditLog != null).Label("Audit log must be created for failures")
                    .And((auditLog.Outcome == AuthenticationOutcome.Failure).Label("Outcome must be Failure"))
                    .And((auditLog.ClientId == clientId).Label("Client ID must be logged"))
                    .And((!string.IsNullOrEmpty(auditLog.ErrorCode)).Label("Error code must be present"))
                    .And((!string.IsNullOrEmpty(auditLog.ErrorMessage)).Label("Error message must be present"))
                    .And((!auditLog.ErrorMessage!.Contains(invalidSecret)).Label("Error message must not contain sensitive information"))
                    .And((!auditLog.AdditionalData.Values.Any(v => v.ToString()?.Contains(invalidSecret) == true)).Label("Additional data must not contain sensitive information"));
            })
            .Label("Feature: authentication-token-fix, Property 8: Audit logging completeness - Security");
    }

    /// <summary>
    /// Property: Audit logs must contain all required fields for compliance
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AuditLogComplianceFields()
    {
        return Prop.ForAll(
            Arb.From<AuthenticationOperation>(),
            Arb.From<AuthenticationOutcome>(),
            Gen.Elements("client1", "client2", "testclient", "backup-client").ToArbitrary(),
            (operation, outcome, clientId) =>
            {
                // Arrange & Act
                var auditLog = outcome == AuthenticationOutcome.Success
                    ? AuthenticationAuditLog.Success(clientId, operation, 100)
                    : AuthenticationAuditLog.Failure(clientId, operation, "TEST_ERROR", "Test error message", 100);

                // Assert - All required fields for compliance must be present
                return (!string.IsNullOrEmpty(auditLog.Id)).Label("ID must be present")
                    .And((auditLog.Timestamp != default).Label("Timestamp must be set"))
                    .And((auditLog.ClientId == clientId).Label("Client ID must be set"))
                    .And((auditLog.Operation == operation).Label("Operation must be set"))
                    .And((auditLog.Outcome == outcome).Label("Outcome must be set"))
                    .And((auditLog.DurationMs >= 0).Label("Duration must be non-negative"))
                    .And((auditLog.AdditionalData != null).Label("Additional data must be initialized"))
                    .And((outcome == AuthenticationOutcome.Failure ? 
                           (!string.IsNullOrEmpty(auditLog.ErrorCode) && !string.IsNullOrEmpty(auditLog.ErrorMessage)) :
                           true
                       ).Label("Failure logs must have error details"));
            })
            .Label("Feature: authentication-token-fix, Property 8: Audit logging completeness - Compliance Fields");
    }

    /// <summary>
    /// Property: Audit service must handle concurrent logging operations safely
    /// </summary>
    [Property(MaxTest = 50)]
    public Property ConcurrentAuditLogging()
    {
        return Prop.ForAll(
            Gen.Choose(1, 10).ToArbitrary(),
            Gen.Elements("client1", "client2", "testclient", "backup-client").ToArbitrary(),
            (concurrentOperations, clientId) =>
            {
                // Arrange
                var auditLogs = new List<AuthenticationAuditLog>();
                var auditService = new TestAuthenticationAuditService(auditLogs);

                // Act - Perform concurrent audit logging operations
                var tasks = Enumerable.Range(0, concurrentOperations)
                    .Select(i => Task.Run(async () =>
                    {
                        var log = AuthenticationAuditLog.Success($"{clientId}_{i}", 
                            AuthenticationOperation.TokenValidation, i * 10);
                        await auditService.LogAuthenticationEventAsync(log);
                    }))
                    .ToArray();

                Task.WaitAll(tasks);

                // Assert
                return (auditLogs.Count == concurrentOperations).Label("All concurrent operations must be logged")
                    .And((auditLogs.All(log => log.ClientId?.StartsWith(clientId) == true)).Label("All logs must have correct client ID prefix"))
                    .And((auditLogs.Select(log => log.ClientId).Distinct().Count() == concurrentOperations).Label("All logs must be unique"));
            })
            .Label("Feature: authentication-token-fix, Property 8: Audit logging completeness - Concurrency");
    }

    /// <summary>
    /// Test implementation of IAuthenticationAuditService for property testing
    /// </summary>
    private class TestAuthenticationAuditService : IAuthenticationAuditService
    {
        private readonly List<AuthenticationAuditLog> _logs;

        public TestAuthenticationAuditService(List<AuthenticationAuditLog> logs)
        {
            _logs = logs;
        }

        public Task LogAuthenticationEventAsync(AuthenticationAuditLog auditLog)
        {
            lock (_logs)
            {
                _logs.Add(auditLog);
            }
            return Task.CompletedTask;
        }

        public Task<List<AuthenticationAuditLog>> GetAuditLogsAsync(DateTime startTime, DateTime endTime, string? clientId = null)
        {
            lock (_logs)
            {
                var filtered = _logs.Where(log => 
                    log.Timestamp >= startTime && 
                    log.Timestamp <= endTime &&
                    (clientId == null || log.ClientId == clientId))
                    .ToList();
                return Task.FromResult(filtered);
            }
        }

        public Task<int> CleanupExpiredLogsAsync(int retentionDays)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            lock (_logs)
            {
                var toRemove = _logs.Where(log => log.Timestamp < cutoffDate).ToList();
                foreach (var log in toRemove)
                {
                    _logs.Remove(log);
                }
                return Task.FromResult(toRemove.Count);
            }
        }
    }
}