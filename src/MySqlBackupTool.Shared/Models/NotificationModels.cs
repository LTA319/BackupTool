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
    /// Base URL for HTTP client operations (optional)
    /// </summary>
    [Url]
    [StringLength(2000)]
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Timeout in seconds for HTTP client requests
    /// </summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of retry attempts for HTTP operations
    /// </summary>
    [Range(0, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Whether to enable circuit breaker pattern for HTTP operations
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = false;

    /// <summary>
    /// Default headers to include in HTTP requests
    /// </summary>
    public Dictionary<string, string> DefaultHeaders { get; set; } = new();

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

/// <summary>
/// Email message for notification service
/// </summary>
public class EmailMessage
{
    /// <summary>
    /// Unique identifier for tracking
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Recipient email address
    /// </summary>
    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string To { get; set; } = string.Empty;
    
    /// <summary>
    /// Email subject
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Subject { get; set; } = string.Empty;
    
    /// <summary>
    /// Email body content
    /// </summary>
    [Required]
    [StringLength(10000)]
    public string Body { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether the body is HTML formatted
    /// </summary>
    public bool IsHtml { get; set; } = true;
    
    /// <summary>
    /// List of attachment file paths
    /// </summary>
    public List<string> Attachments { get; set; } = new();
    
    /// <summary>
    /// Custom email headers
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();
    
    /// <summary>
    /// Priority level of the email
    /// </summary>
    public EmailPriority Priority { get; set; } = EmailPriority.Normal;
    
    /// <summary>
    /// When the email was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// SMTP server configuration
/// </summary>
public class SmtpConfig
{
    /// <summary>
    /// SMTP server hostname
    /// </summary>
    [Required]
    [StringLength(255)]
    public string Host { get; set; } = string.Empty;
    
    /// <summary>
    /// SMTP server port
    /// </summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 587;
    
    /// <summary>
    /// Whether to use SSL/TLS encryption
    /// </summary>
    public bool EnableSsl { get; set; } = true;
    
    /// <summary>
    /// SMTP username for authentication
    /// </summary>
    [StringLength(255)]
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// SMTP password for authentication
    /// </summary>
    [StringLength(255)]
    public string Password { get; set; } = string.Empty;
    
    /// <summary>
    /// From email address
    /// </summary>
    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string FromAddress { get; set; } = string.Empty;
    
    /// <summary>
    /// From display name
    /// </summary>
    [StringLength(255)]
    public string FromName { get; set; } = "MySQL Backup Tool";
    
    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    [Range(5, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Email template for notifications
/// </summary>
public class EmailTemplate
{
    /// <summary>
    /// Unique template identifier
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Template description
    /// </summary>
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Email subject template
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Subject { get; set; } = string.Empty;
    
    /// <summary>
    /// HTML body template
    /// </summary>
    [StringLength(10000)]
    public string HtmlBody { get; set; } = string.Empty;
    
    /// <summary>
    /// Plain text body template
    /// </summary>
    [StringLength(10000)]
    public string TextBody { get; set; } = string.Empty;
    
    /// <summary>
    /// List of required template variables
    /// </summary>
    public List<string> RequiredVariables { get; set; } = new();
    
    /// <summary>
    /// Template category for organization
    /// </summary>
    [StringLength(50)]
    public string Category { get; set; } = "General";
    
    /// <summary>
    /// Whether the template is active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// When the template was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the template was last modified
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Status of a notification delivery
/// </summary>
public class NotificationStatus
{
    /// <summary>
    /// Unique notification identifier
    /// </summary>
    public string NotificationId { get; set; } = string.Empty;
    
    /// <summary>
    /// Current delivery status
    /// </summary>
    public NotificationDeliveryStatus Status { get; set; } = NotificationDeliveryStatus.Pending;
    
    /// <summary>
    /// When the notification was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the notification was sent (if successful)
    /// </summary>
    public DateTime? SentAt { get; set; }
    
    /// <summary>
    /// Number of delivery attempts
    /// </summary>
    public int AttemptCount { get; set; } = 0;
    
    /// <summary>
    /// Last error message (if failed)
    /// </summary>
    public string? LastError { get; set; }
    
    /// <summary>
    /// Recipient email address
    /// </summary>
    public string Recipient { get; set; } = string.Empty;
    
    /// <summary>
    /// Email subject
    /// </summary>
    public string Subject { get; set; } = string.Empty;
    
    /// <summary>
    /// Next retry attempt time (if applicable)
    /// </summary>
    public DateTime? NextRetryAt { get; set; }
}

/// <summary>
/// Email priority levels
/// </summary>
public enum EmailPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Notification delivery status
/// </summary>
public enum NotificationDeliveryStatus
{
    Pending = 1,
    Sending = 2,
    Sent = 3,
    Failed = 4,
    Cancelled = 5
}

/// <summary>
/// Statistics about notification delivery
/// </summary>
public class NotificationStatistics
{
    /// <summary>
    /// Date range start for these statistics
    /// </summary>
    public DateTime FromDate { get; set; }
    
    /// <summary>
    /// Date range end for these statistics
    /// </summary>
    public DateTime ToDate { get; set; }
    
    /// <summary>
    /// Total number of notifications sent
    /// </summary>
    public int TotalSent { get; set; }
    
    /// <summary>
    /// Number of successfully delivered notifications
    /// </summary>
    public int SuccessfulDeliveries { get; set; }
    
    /// <summary>
    /// Number of failed delivery attempts
    /// </summary>
    public int FailedDeliveries { get; set; }
    
    /// <summary>
    /// Number of notifications currently pending
    /// </summary>
    public int PendingDeliveries { get; set; }
    
    /// <summary>
    /// Average delivery time for successful notifications
    /// </summary>
    public TimeSpan AverageDeliveryTime { get; set; }
    
    /// <summary>
    /// Success rate as a percentage (0-100)
    /// </summary>
    public double SuccessRate => TotalSent > 0 ? (double)SuccessfulDeliveries / TotalSent * 100 : 0;
    
    /// <summary>
    /// Most common failure reasons
    /// </summary>
    public Dictionary<string, int> FailureReasons { get; set; } = new();
    
    /// <summary>
    /// Delivery statistics by hour of day
    /// </summary>
    public Dictionary<int, int> DeliveriesByHour { get; set; } = new();
    
    /// <summary>
    /// When these statistics were generated
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}