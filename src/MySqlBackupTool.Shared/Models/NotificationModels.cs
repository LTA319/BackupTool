using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// Configuration for network retry behavior
/// </summary>
public class NetworkRetryConfig
{
    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    [Range(1, 20)]
    public int MaxRetryAttempts { get; set; } = 5;

    /// <summary>
    /// Base delay between retry attempts (exponential backoff)
    /// </summary>
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Maximum delay between retry attempts
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Whether to add jitter to retry delays to prevent thundering herd
    /// </summary>
    public bool EnableJitter { get; set; } = true;

    /// <summary>
    /// Whether to retry on unknown exceptions
    /// </summary>
    public bool RetryOnUnknownExceptions { get; set; } = false;
}

/// <summary>
/// Exception thrown when network retry attempts are exhausted
/// </summary>
public class NetworkRetryException : BackupException
{
    public string OperationName { get; }
    public int AttemptsExhausted { get; }
    public TimeSpan TotalDuration { get; }

    public NetworkRetryException(
        string operationId, 
        string operationName, 
        int attemptsExhausted, 
        TimeSpan totalDuration, 
        Exception? innerException = null)
        : base(operationId, $"Network operation '{operationName}' failed after {attemptsExhausted} attempts over {totalDuration.TotalSeconds:F1}s", innerException)
    {
        OperationName = operationName;
        AttemptsExhausted = attemptsExhausted;
        TotalDuration = totalDuration;
    }
}

/// <summary>
/// Result of a network connectivity test
/// </summary>
public class NetworkConnectivityResult
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool IsReachable { get; set; }
    public bool PingSuccessful { get; set; }
    public bool TcpConnectionSuccessful { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Configuration for alerting and notification system
/// </summary>
public class AlertingConfig
{
    /// <summary>
    /// Whether alerting is enabled
    /// </summary>
    public bool EnableAlerting { get; set; } = true;

    /// <summary>
    /// Email configuration for alerts
    /// </summary>
    public EmailConfig Email { get; set; } = new();

    /// <summary>
    /// Webhook configuration for alerts
    /// </summary>
    public WebhookConfig Webhook { get; set; } = new();

    /// <summary>
    /// File logging configuration for alerts
    /// </summary>
    public FileLogConfig FileLog { get; set; } = new();

    /// <summary>
    /// Minimum severity level for sending alerts
    /// </summary>
    public AlertSeverity MinimumSeverity { get; set; } = AlertSeverity.Error;

    /// <summary>
    /// Maximum number of alerts to send per hour (rate limiting)
    /// </summary>
    [Range(1, 1000)]
    public int MaxAlertsPerHour { get; set; } = 50;

    /// <summary>
    /// Timeout for notification delivery attempts
    /// </summary>
    public TimeSpan NotificationTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Email configuration for notifications
/// </summary>
public class EmailConfig
{
    /// <summary>
    /// Whether email notifications are enabled
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// SMTP server hostname
    /// </summary>
    [StringLength(255)]
    public string SmtpServer { get; set; } = string.Empty;

    /// <summary>
    /// SMTP server port
    /// </summary>
    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    /// Whether to use SSL/TLS
    /// </summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>
    /// SMTP username
    /// </summary>
    [StringLength(255)]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// SMTP password
    /// </summary>
    [StringLength(255)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// From email address
    /// </summary>
    [EmailAddress]
    [StringLength(255)]
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>
    /// From display name
    /// </summary>
    [StringLength(255)]
    public string FromName { get; set; } = "MySQL Backup Tool";

    /// <summary>
    /// List of recipient email addresses
    /// </summary>
    public List<string> Recipients { get; set; } = new();
}

/// <summary>
/// Webhook configuration for notifications
/// </summary>
public class WebhookConfig
{
    /// <summary>
    /// Whether webhook notifications are enabled
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Webhook URL
    /// </summary>
    [Url]
    [StringLength(2000)]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// HTTP method to use (POST, PUT, etc.)
    /// </summary>
    [StringLength(10)]
    public string HttpMethod { get; set; } = "POST";

    /// <summary>
    /// Content type for the request
    /// </summary>
    [StringLength(100)]
    public string ContentType { get; set; } = "application/json";

    /// <summary>
    /// Custom headers to include in the request
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// Authentication token (if required)
    /// </summary>
    [StringLength(500)]
    public string? AuthToken { get; set; }

    /// <summary>
    /// Authentication header name (e.g., "Authorization", "X-API-Key")
    /// </summary>
    [StringLength(100)]
    public string AuthHeaderName { get; set; } = "Authorization";
}

/// <summary>
/// File logging configuration for notifications
/// </summary>
public class FileLogConfig
{
    /// <summary>
    /// Whether file logging is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Directory path for alert log files
    /// </summary>
    [StringLength(500)]
    public string LogDirectory { get; set; } = "logs/alerts";

    /// <summary>
    /// File name pattern (supports date formatting)
    /// </summary>
    [StringLength(255)]
    public string FileNamePattern { get; set; } = "alerts_{yyyy-MM-dd}.log";

    /// <summary>
    /// Maximum file size before rotation (in MB)
    /// </summary>
    [Range(1, 1000)]
    public int MaxFileSizeMB { get; set; } = 10;

    /// <summary>
    /// Maximum number of log files to keep
    /// </summary>
    [Range(1, 100)]
    public int MaxFileCount { get; set; } = 30;
}

/// <summary>
/// Notification channels available for alerts
/// </summary>
public enum NotificationChannel
{
    Email,
    Webhook,
    FileLog
}

/// <summary>
/// Alert severity levels
/// </summary>
public enum AlertSeverity
{
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}

/// <summary>
/// Generic notification message
/// </summary>
public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public AlertSeverity Severity { get; set; } = AlertSeverity.Info;
    
    [Required]
    [StringLength(200)]
    public string Subject { get; set; } = string.Empty;
    
    [Required]
    [StringLength(5000)]
    public string Message { get; set; } = string.Empty;
    
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    /// <summary>
    /// Operation ID associated with this notification
    /// </summary>
    public string? OperationId { get; set; }
}

/// <summary>
/// Result of a notification delivery attempt
/// </summary>
public class NotificationResult
{
    public bool Success { get; set; }
    public Dictionary<NotificationChannel, bool> ChannelResults { get; set; } = new();
    public Dictionary<NotificationChannel, string?> ChannelErrors { get; set; } = new();
    public TimeSpan Duration { get; set; }
    public int SuccessfulChannels => ChannelResults.Count(kvp => kvp.Value);
    public int FailedChannels => ChannelResults.Count(kvp => !kvp.Value);
}

/// <summary>
/// Enhanced critical error alert with notification tracking
/// </summary>
public class EnhancedCriticalErrorAlert : CriticalErrorAlert
{
    /// <summary>
    /// Notification channels used for this alert
    /// </summary>
    public List<NotificationChannel> NotificationChannels { get; set; } = new();
    
    /// <summary>
    /// Results of notification attempts per channel
    /// </summary>
    public Dictionary<NotificationChannel, bool> NotificationResults { get; set; } = new();
    
    /// <summary>
    /// Error messages for failed notification attempts
    /// </summary>
    public Dictionary<NotificationChannel, string?> NotificationErrors { get; set; } = new();
    
    /// <summary>
    /// Total time taken to send all notifications
    /// </summary>
    public TimeSpan NotificationDuration { get; set; }
}