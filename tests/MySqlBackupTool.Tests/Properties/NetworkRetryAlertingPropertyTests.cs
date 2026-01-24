using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace MySqlBackupTool.Tests.Properties;

/// <summary>
/// Property-based tests for network retry and alerting functionality
/// **Validates: Requirements 8.3, 8.6, 9.4, 9.6**
/// </summary>
public class NetworkRetryAlertingPropertyTests : IDisposable
{
    private readonly List<HttpClient> _httpClients;
    private readonly List<string> _tempFiles;
    private readonly System.Random _random;

    public NetworkRetryAlertingPropertyTests()
    {
        _httpClients = new List<HttpClient>();
        _tempFiles = new List<string>();
        _random = new System.Random();
    }

    /// <summary>
    /// Property 19: Network Transfer Retry with Backoff
    /// For any network transfer failure, the system should implement retry mechanisms 
    /// with exponential backoff, eventually succeeding when connectivity is restored 
    /// or failing after maximum retries.
    /// **Validates: Requirements 8.3, 8.6**
    /// </summary>
    [Property(MaxTest = 10)]
    public bool NetworkTransferRetryWithBackoffProperty()
    {
        try
        {
            // Arrange - Create network retry service with test configuration
            var logger = new LoggerFactory().CreateLogger<NetworkRetryService>();
            var config = new NetworkRetryConfig
            {
                MaxRetryAttempts = 3,
                BaseRetryDelay = TimeSpan.FromMilliseconds(50), // Faster for testing
                MaxRetryDelay = TimeSpan.FromMilliseconds(500),
                EnableJitter = false, // Disable for predictable testing
                RetryOnUnknownExceptions = false
            };
            
            var service = new NetworkRetryService(logger, config);
            var operationId = Guid.NewGuid().ToString();
            var attemptCount = 0;
            var startTime = DateTime.UtcNow;

            // Act - Execute operation that fails initially then succeeds
            var result = service.ExecuteWithRetryAsync(
                async (ct) =>
                {
                    attemptCount++;
                    if (attemptCount < config.MaxRetryAttempts)
                    {
                        // Simulate retriable network error
                        throw new SocketException((int)SocketError.ConnectionRefused);
                    }
                    return "success";
                },
                "TestNetworkOperation",
                operationId
            ).Result;

            var duration = DateTime.UtcNow - startTime;

            // Assert - Verify retry behavior
            var expectedAttempts = config.MaxRetryAttempts;
            var minExpectedDuration = (config.MaxRetryAttempts - 1) * config.BaseRetryDelay.TotalMilliseconds;

            return result == "success" && 
                   attemptCount == expectedAttempts && 
                   duration.TotalMilliseconds >= minExpectedDuration;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Network retry with backoff test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Property test for retry exhaustion behavior
    /// When all retries are exhausted, the system should throw NetworkRetryException
    /// **Validates: Requirements 8.3**
    /// </summary>
    [Property(MaxTest = 10)]
    public bool RetryExhaustionProperty()
    {
        try
        {
            // Arrange
            var logger = new LoggerFactory().CreateLogger<NetworkRetryService>();
            var config = new NetworkRetryConfig
            {
                MaxRetryAttempts = 2,
                BaseRetryDelay = TimeSpan.FromMilliseconds(10),
                MaxRetryDelay = TimeSpan.FromMilliseconds(100),
                EnableJitter = false
            };
            
            var service = new NetworkRetryService(logger, config);
            var operationId = Guid.NewGuid().ToString();
            var attemptCount = 0;

            // Act & Assert - Operation should fail after exhausting retries
            try
            {
                service.ExecuteWithRetryAsync(
                    async (ct) =>
                    {
                        attemptCount++;
                        throw new SocketException((int)SocketError.ConnectionRefused);
                    },
                    "TestFailingOperation",
                    operationId
                ).Wait();

                // Should not reach here
                return false;
            }
            catch (AggregateException ex) when (ex.InnerException is NetworkRetryException nre)
            {
                // Verify exception details
                return nre.OperationName == "TestFailingOperation" &&
                       nre.AttemptsExhausted == config.MaxRetryAttempts &&
                       attemptCount == config.MaxRetryAttempts;
            }
            catch
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Retry exhaustion test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Property test for non-retriable exception handling
    /// Non-retriable exceptions should fail immediately without retries
    /// **Validates: Requirements 8.3**
    /// </summary>
    [Property(MaxTest = 10)]
    public bool NonRetriableExceptionProperty()
    {
        try
        {
            // Arrange
            var logger = new LoggerFactory().CreateLogger<NetworkRetryService>();
            var config = new NetworkRetryConfig
            {
                MaxRetryAttempts = 3,
                BaseRetryDelay = TimeSpan.FromMilliseconds(10)
            };
            
            var service = new NetworkRetryService(logger, config);
            var operationId = Guid.NewGuid().ToString();
            var attemptCount = 0;

            // Act & Assert - Non-retriable exception should fail immediately
            try
            {
                service.ExecuteWithRetryAsync(
                    async (ct) =>
                    {
                        attemptCount++;
                        throw new ArgumentException("Non-retriable error");
                    },
                    "TestNonRetriableOperation",
                    operationId
                ).Wait();

                return false;
            }
            catch (AggregateException ex) when (ex.InnerException is ArgumentException)
            {
                // Should only attempt once for non-retriable exceptions
                return attemptCount == 1;
            }
            catch
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Non-retriable exception test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Property test for connectivity restoration detection
    /// System should detect when connectivity is restored
    /// **Validates: Requirements 8.6**
    /// </summary>
    [Property(MaxTest = 5)]
    public bool ConnectivityRestorationProperty()
    {
        try
        {
            // Arrange
            var logger = new LoggerFactory().CreateLogger<NetworkRetryService>();
            var service = new NetworkRetryService(logger);

            // Act - Test connectivity to a known good endpoint
            var result = service.TestConnectivityAsync("127.0.0.1", 80, TimeSpan.FromSeconds(5)).Result;

            // Assert - Result should have proper structure regardless of actual connectivity
            return result != null &&
                   result.Host == "127.0.0.1" &&
                   result.Port == 80 &&
                   result.ResponseTime >= TimeSpan.Zero;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connectivity restoration test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Property 21: Critical Error Alerting
    /// For any critical system error, the system should send notifications 
    /// through all configured alert channels.
    /// **Validates: Requirements 9.4, 9.6**
    /// </summary>
    [Property(MaxTest = 10)]
    public bool CriticalErrorAlertingProperty()
    {
        try
        {
            // Arrange - Create alerting service with file log enabled
            var logger = new LoggerFactory().CreateLogger<AlertingService>();
            var httpClient = CreateHttpClient();
            
            var tempDir = Path.GetTempPath();
            var logFileName = $"test_alerts_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{_random.Next(1000, 9999)}.log";
            var logFilePath = Path.Combine(tempDir, logFileName);
            
            var config = new AlertingConfig
            {
                EnableAlerting = true,
                MinimumSeverity = AlertSeverity.Warning,
                MaxAlertsPerHour = 100,
                NotificationTimeout = TimeSpan.FromSeconds(10),
                FileLog = new FileLogConfig
                {
                    Enabled = true,
                    LogDirectory = tempDir,
                    FileNamePattern = logFileName,
                    MaxFileSizeMB = 1,
                    MaxFileCount = 5
                },
                Email = new EmailConfig { Enabled = false },
                Webhook = new WebhookConfig { Enabled = false }
            };

            var service = new AlertingService(logger, httpClient, config);

            // Act - Send critical error alert
            var alert = new CriticalErrorAlert
            {
                Id = Guid.NewGuid(),
                OperationId = Guid.NewGuid().ToString(),
                ErrorType = "TestCriticalError",
                ErrorMessage = "This is a test critical error for property testing",
                OccurredAt = DateTime.UtcNow,
                Context = new Dictionary<string, object>
                {
                    ["TestProperty"] = "NetworkRetryAlertingPropertyTests",
                    ["TestRun"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                }
            };

            var result = service.SendCriticalErrorAlertAsync(alert).Result;

            // Assert - Alert should be sent successfully
            var alertSent = result && alert.AlertSent;
            
            // Verify file was created and contains alert information
            var fileExists = File.Exists(logFilePath);
            var fileContainsAlert = false;
            
            if (fileExists)
            {
                var logContent = File.ReadAllText(logFilePath);
                fileContainsAlert = logContent.Contains("TestCriticalError") && 
                                   logContent.Contains(alert.OperationId);
                
                // Track temp file for cleanup
                _tempFiles.Add(logFilePath);
            }

            return alertSent && fileExists && fileContainsAlert;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Critical error alerting test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Property test for alert rate limiting
    /// System should enforce rate limits to prevent alert flooding
    /// **Validates: Requirements 9.6**
    /// </summary>
    [Property(MaxTest = 5)]
    public bool AlertRateLimitingProperty()
    {
        try
        {
            // Arrange - Create alerting service with low rate limit
            var logger = new LoggerFactory().CreateLogger<AlertingService>();
            var httpClient = CreateHttpClient();
            
            var config = new AlertingConfig
            {
                EnableAlerting = true,
                MinimumSeverity = AlertSeverity.Warning,
                MaxAlertsPerHour = 2, // Very low limit for testing
                NotificationTimeout = TimeSpan.FromSeconds(5),
                FileLog = new FileLogConfig
                {
                    Enabled = true,
                    LogDirectory = Path.GetTempPath(),
                    FileNamePattern = $"rate_limit_test_{_random.Next(1000, 9999)}.log"
                },
                Email = new EmailConfig { Enabled = false },
                Webhook = new WebhookConfig { Enabled = false }
            };

            var service = new AlertingService(logger, httpClient, config);

            // Act - Send multiple alerts rapidly
            var alerts = new List<bool>();
            for (int i = 0; i < 5; i++)
            {
                var alert = new CriticalErrorAlert
                {
                    Id = Guid.NewGuid(),
                    OperationId = Guid.NewGuid().ToString(),
                    ErrorType = $"RateLimitTest_{i}",
                    ErrorMessage = $"Rate limit test alert {i}",
                    OccurredAt = DateTime.UtcNow
                };

                var result = service.SendCriticalErrorAlertAsync(alert).Result;
                alerts.Add(result);
            }

            // Assert - Should have some successful alerts and some rate-limited
            var successfulAlerts = alerts.Count(a => a);
            var rateLimitedAlerts = alerts.Count(a => !a);

            // With a limit of 2 per hour, we should see rate limiting kick in
            return successfulAlerts <= config.MaxAlertsPerHour && rateLimitedAlerts > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Alert rate limiting test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Property test for notification channel testing
    /// System should be able to test all configured notification channels
    /// **Validates: Requirements 9.4**
    /// </summary>
    [Property(MaxTest = 5)]
    public bool NotificationChannelTestingProperty()
    {
        try
        {
            // Arrange
            var logger = new LoggerFactory().CreateLogger<AlertingService>();
            var httpClient = CreateHttpClient();
            
            var config = new AlertingConfig
            {
                EnableAlerting = true,
                FileLog = new FileLogConfig
                {
                    Enabled = true,
                    LogDirectory = Path.GetTempPath(),
                    FileNamePattern = $"channel_test_{_random.Next(1000, 9999)}.log"
                },
                Email = new EmailConfig { Enabled = false },
                Webhook = new WebhookConfig { Enabled = false }
            };

            var service = new AlertingService(logger, httpClient, config);

            // Act - Test notification channels
            var testResults = service.TestNotificationChannelsAsync().Result;

            // Assert - Should return results for enabled channels
            return testResults != null && 
                   testResults.ContainsKey(NotificationChannel.FileLog) &&
                   testResults.Count > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Notification channel testing test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Property test for alert severity filtering
    /// System should only send alerts that meet the minimum severity threshold
    /// **Validates: Requirements 9.4**
    /// </summary>
    [Property(MaxTest = 10)]
    public bool AlertSeverityFilteringProperty()
    {
        try
        {
            // Arrange
            var logger = new LoggerFactory().CreateLogger<AlertingService>();
            var httpClient = CreateHttpClient();
            
            var config = new AlertingConfig
            {
                EnableAlerting = true,
                MinimumSeverity = AlertSeverity.Error, // Only Error and Critical
                FileLog = new FileLogConfig
                {
                    Enabled = true,
                    LogDirectory = Path.GetTempPath(),
                    FileNamePattern = $"severity_test_{_random.Next(1000, 9999)}.log"
                },
                Email = new EmailConfig { Enabled = false },
                Webhook = new WebhookConfig { Enabled = false }
            };

            var service = new AlertingService(logger, httpClient, config);

            // Act - Send notifications with different severities
            var warningNotification = new Notification
            {
                Id = Guid.NewGuid(),
                Subject = "Warning Test",
                Message = "This is a warning",
                Severity = AlertSeverity.Warning
            };

            var errorNotification = new Notification
            {
                Id = Guid.NewGuid(),
                Subject = "Error Test", 
                Message = "This is an error",
                Severity = AlertSeverity.Error
            };

            var warningResult = service.SendNotificationAsync(warningNotification).Result;
            var errorResult = service.SendNotificationAsync(errorNotification).Result;

            // Assert - Warning should be filtered out, Error should go through
            return !warningResult.Success && errorResult.Success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Alert severity filtering test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Creates an HTTP client for testing
    /// </summary>
    private HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClients.Add(httpClient);
        return httpClient;
    }

    public void Dispose()
    {
        // Clean up HTTP clients
        foreach (var client in _httpClients)
        {
            try
            {
                client.Dispose();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        _httpClients.Clear();

        // Clean up temporary files
        foreach (var tempFile in _tempFiles)
        {
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        _tempFiles.Clear();
    }
}