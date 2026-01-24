using System.Collections.Concurrent;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Service for sending email notifications via SMTP
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly ConcurrentDictionary<string, NotificationStatus> _notificationStatuses;
    private readonly ConcurrentDictionary<string, EmailTemplate> _templates;
    private SmtpConfig _configuration;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _notificationStatuses = new ConcurrentDictionary<string, NotificationStatus>();
        _templates = new ConcurrentDictionary<string, EmailTemplate>();
        _configuration = new SmtpConfig();
        
        // Initialize with default templates
        InitializeDefaultTemplates();
    }

    public SmtpConfig Configuration => _configuration;

    public void UpdateConfiguration(SmtpConfig config)
    {
        _configuration = config ?? throw new ArgumentNullException(nameof(config));
        _logger.LogInformation("SMTP configuration updated for host {Host}:{Port}", config.Host, config.Port);
    }

    /// <summary>
    /// Sends a single email message asynchronously
    /// </summary>
    public async Task<bool> SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        _logger.LogInformation("Sending email to {Recipient} with subject '{Subject}'", message.To, message.Subject);

        // Update status to sending
        var status = new NotificationStatus
        {
            NotificationId = message.Id,
            Status = NotificationDeliveryStatus.Sending,
            CreatedAt = message.CreatedAt,
            Recipient = message.To,
            Subject = message.Subject,
            AttemptCount = 1
        };
        _notificationStatuses.AddOrUpdate(message.Id, status, (key, existing) => 
        {
            existing.Status = NotificationDeliveryStatus.Sending;
            existing.AttemptCount++;
            return existing;
        });

        try
        {
            using var client = new SmtpClient();
            
            // Configure timeout
            client.Timeout = _configuration.TimeoutSeconds * 1000;

            // Connect to SMTP server
            await client.ConnectAsync(_configuration.Host, _configuration.Port, 
                _configuration.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None, 
                cancellationToken);

            // Authenticate if credentials are provided
            if (!string.IsNullOrEmpty(_configuration.Username) && !string.IsNullOrEmpty(_configuration.Password))
            {
                await client.AuthenticateAsync(_configuration.Username, _configuration.Password, cancellationToken);
            }

            // Create MimeMessage
            var mimeMessage = CreateMimeMessage(message);

            // Send the message
            await client.SendAsync(mimeMessage, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            // Update status to sent
            status.Status = NotificationDeliveryStatus.Sent;
            status.SentAt = DateTime.UtcNow;
            _notificationStatuses.AddOrUpdate(message.Id, status, (key, existing) => status);

            _logger.LogInformation("Successfully sent email to {Recipient}", message.To);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipient}: {Error}", message.To, ex.Message);

            // Update status to failed
            status.Status = NotificationDeliveryStatus.Failed;
            status.LastError = ex.Message;
            _notificationStatuses.AddOrUpdate(message.Id, status, (key, existing) => status);

            return false;
        }
    }

    /// <summary>
    /// Sends multiple email messages in bulk
    /// </summary>
    public async Task<Dictionary<string, bool>> SendBulkEmailAsync(IEnumerable<EmailMessage> messages, CancellationToken cancellationToken = default)
    {
        if (messages == null)
            throw new ArgumentNullException(nameof(messages));

        var results = new Dictionary<string, bool>();
        var messageList = messages.ToList();

        _logger.LogInformation("Sending bulk email to {Count} recipients", messageList.Count);

        // Send emails concurrently with limited parallelism
        var semaphore = new SemaphoreSlim(5); // Limit to 5 concurrent sends
        var tasks = messageList.Select(async message =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var success = await SendEmailAsync(message, cancellationToken);
                lock (results)
                {
                    results[message.Id] = success;
                }
                return success;
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        var successCount = results.Values.Count(success => success);
        _logger.LogInformation("Bulk email completed: {SuccessCount}/{TotalCount} sent successfully", 
            successCount, messageList.Count);

        return results;
    }

    /// <summary>
    /// Gets the delivery status of a specific notification
    /// </summary>
    public async Task<NotificationStatus?> GetStatusAsync(string notificationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(notificationId))
            throw new ArgumentException("Notification ID cannot be null or empty", nameof(notificationId));

        return await Task.FromResult(_notificationStatuses.TryGetValue(notificationId, out var status) ? status : null);
    }

    /// <summary>
    /// Retrieves all available email templates
    /// </summary>
    public async Task<IEnumerable<EmailTemplate>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_templates.Values.Where(t => t.IsActive));
    }

    /// <summary>
    /// Retrieves email templates by category
    /// </summary>
    public async Task<IEnumerable<EmailTemplate>> GetTemplatesByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_templates.Values.Where(t => t.IsActive && 
            string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Gets a specific email template by name
    /// </summary>
    public async Task<EmailTemplate?> GetTemplateAsync(string templateName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(templateName))
            throw new ArgumentException("Template name cannot be null or empty", nameof(templateName));

        return await Task.FromResult(_templates.TryGetValue(templateName, out var template) ? template : null);
    }

    /// <summary>
    /// Tests the SMTP connection with the provided configuration
    /// </summary>
    public async Task<bool> TestSmtpConnectionAsync(SmtpConfig config, CancellationToken cancellationToken = default)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        _logger.LogInformation("Testing SMTP connection to {Host}:{Port}", config.Host, config.Port);

        try
        {
            using var client = new SmtpClient();
            client.Timeout = config.TimeoutSeconds * 1000;

            await client.ConnectAsync(config.Host, config.Port, 
                config.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None, 
                cancellationToken);

            if (!string.IsNullOrEmpty(config.Username) && !string.IsNullOrEmpty(config.Password))
            {
                await client.AuthenticateAsync(config.Username, config.Password, cancellationToken);
            }

            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("SMTP connection test successful");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP connection test failed: {Error}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Creates an email message from a template with variable substitution
    /// </summary>
    public async Task<EmailMessage?> CreateEmailFromTemplateAsync(
        string templateName, 
        string recipient, 
        Dictionary<string, object> variables, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(templateName))
            throw new ArgumentException("Template name cannot be null or empty", nameof(templateName));
        
        if (string.IsNullOrEmpty(recipient))
            throw new ArgumentException("Recipient cannot be null or empty", nameof(recipient));

        var template = await GetTemplateAsync(templateName, cancellationToken);
        if (template == null)
        {
            _logger.LogWarning("Template '{TemplateName}' not found", templateName);
            return null;
        }

        variables ??= new Dictionary<string, object>();

        // Check for required variables
        var missingVariables = template.RequiredVariables.Where(v => !variables.ContainsKey(v)).ToList();
        if (missingVariables.Any())
        {
            _logger.LogError("Missing required variables for template '{TemplateName}': {MissingVariables}", 
                templateName, string.Join(", ", missingVariables));
            return null;
        }

        // Substitute variables in subject and body
        var subject = SubstituteVariables(template.Subject, variables);
        var body = string.IsNullOrEmpty(template.HtmlBody) 
            ? SubstituteVariables(template.TextBody, variables)
            : SubstituteVariables(template.HtmlBody, variables);
        var isHtml = !string.IsNullOrEmpty(template.HtmlBody);

        return new EmailMessage
        {
            To = recipient,
            Subject = subject,
            Body = body,
            IsHtml = isHtml
        };
    }

    /// <summary>
    /// Gets statistics about notification delivery
    /// </summary>
    public async Task<NotificationStatistics> GetStatisticsAsync(
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        CancellationToken cancellationToken = default)
    {
        var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = toDate ?? DateTime.UtcNow;

        var relevantStatuses = _notificationStatuses.Values
            .Where(s => s.CreatedAt >= from && s.CreatedAt <= to)
            .ToList();

        var statistics = new NotificationStatistics
        {
            FromDate = from,
            ToDate = to,
            TotalSent = relevantStatuses.Count,
            SuccessfulDeliveries = relevantStatuses.Count(s => s.Status == NotificationDeliveryStatus.Sent),
            FailedDeliveries = relevantStatuses.Count(s => s.Status == NotificationDeliveryStatus.Failed),
            PendingDeliveries = relevantStatuses.Count(s => s.Status == NotificationDeliveryStatus.Pending),
            GeneratedAt = DateTime.UtcNow
        };

        // Calculate average delivery time for successful deliveries
        var successfulWithTimes = relevantStatuses
            .Where(s => s.Status == NotificationDeliveryStatus.Sent && s.SentAt.HasValue)
            .ToList();

        if (successfulWithTimes.Any())
        {
            var totalDeliveryTime = successfulWithTimes
                .Sum(s => (s.SentAt!.Value - s.CreatedAt).TotalMilliseconds);
            statistics.AverageDeliveryTime = TimeSpan.FromMilliseconds(totalDeliveryTime / successfulWithTimes.Count);
        }

        // Group failure reasons
        statistics.FailureReasons = relevantStatuses
            .Where(s => s.Status == NotificationDeliveryStatus.Failed && !string.IsNullOrEmpty(s.LastError))
            .GroupBy(s => s.LastError!)
            .ToDictionary(g => g.Key, g => g.Count());

        // Group deliveries by hour
        statistics.DeliveriesByHour = relevantStatuses
            .Where(s => s.SentAt.HasValue)
            .GroupBy(s => s.SentAt!.Value.Hour)
            .ToDictionary(g => g.Key, g => g.Count());

        return await Task.FromResult(statistics);
    }

    /// <summary>
    /// Creates a MimeMessage from an EmailMessage
    /// </summary>
    private MimeMessage CreateMimeMessage(EmailMessage message)
    {
        var mimeMessage = new MimeMessage();
        
        // Set sender
        mimeMessage.From.Add(new MailboxAddress(_configuration.FromName, _configuration.FromAddress));
        
        // Set recipient
        mimeMessage.To.Add(MailboxAddress.Parse(message.To));
        
        // Set subject
        mimeMessage.Subject = message.Subject;

        // Add custom headers
        foreach (var header in message.Headers)
        {
            mimeMessage.Headers.Add(header.Key, header.Value);
        }

        // Set priority
        mimeMessage.Priority = message.Priority switch
        {
            EmailPriority.Low => MessagePriority.NonUrgent,
            EmailPriority.High => MessagePriority.Urgent,
            EmailPriority.Critical => MessagePriority.Urgent,
            _ => MessagePriority.Normal
        };

        // Create body
        var bodyBuilder = new BodyBuilder();
        
        if (message.IsHtml)
        {
            bodyBuilder.HtmlBody = message.Body;
        }
        else
        {
            bodyBuilder.TextBody = message.Body;
        }

        // Add attachments
        foreach (var attachmentPath in message.Attachments)
        {
            if (File.Exists(attachmentPath))
            {
                bodyBuilder.Attachments.Add(attachmentPath);
            }
            else
            {
                _logger.LogWarning("Attachment file not found: {AttachmentPath}", attachmentPath);
            }
        }

        mimeMessage.Body = bodyBuilder.ToMessageBody();
        
        return mimeMessage;
    }

    /// <summary>
    /// Substitutes variables in a template string
    /// </summary>
    private static string SubstituteVariables(string template, Dictionary<string, object> variables)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        var result = template;
        foreach (var variable in variables)
        {
            var placeholder = $"{{{variable.Key}}}";
            result = result.Replace(placeholder, variable.Value?.ToString() ?? string.Empty);
        }

        return result;
    }

    /// <summary>
    /// Initializes default email templates
    /// </summary>
    private void InitializeDefaultTemplates()
    {
        // Backup Success Template
        var backupSuccessTemplate = new EmailTemplate
        {
            Name = "backup-success",
            Description = "Template for successful backup notifications",
            Category = "Backup",
            Subject = "Backup Completed Successfully - {DatabaseName}",
            HtmlBody = @"
                <html>
                <body>
                    <h2>Backup Completed Successfully</h2>
                    <p>The backup operation for database <strong>{DatabaseName}</strong> has completed successfully.</p>
                    <ul>
                        <li><strong>Database:</strong> {DatabaseName}</li>
                        <li><strong>Backup File:</strong> {BackupFileName}</li>
                        <li><strong>File Size:</strong> {FileSize}</li>
                        <li><strong>Duration:</strong> {Duration}</li>
                        <li><strong>Completed At:</strong> {CompletedAt}</li>
                    </ul>
                    <p>The backup file has been stored securely and is ready for use if needed.</p>
                </body>
                </html>",
            TextBody = @"
                Backup Completed Successfully
                
                The backup operation for database {DatabaseName} has completed successfully.
                
                Details:
                - Database: {DatabaseName}
                - Backup File: {BackupFileName}
                - File Size: {FileSize}
                - Duration: {Duration}
                - Completed At: {CompletedAt}
                
                The backup file has been stored securely and is ready for use if needed.",
            RequiredVariables = new List<string> { "DatabaseName", "BackupFileName", "FileSize", "Duration", "CompletedAt" }
        };

        // Backup Failure Template
        var backupFailureTemplate = new EmailTemplate
        {
            Name = "backup-failure",
            Description = "Template for failed backup notifications",
            Category = "Backup",
            Subject = "Backup Failed - {DatabaseName}",
            HtmlBody = @"
                <html>
                <body>
                    <h2 style='color: red;'>Backup Failed</h2>
                    <p>The backup operation for database <strong>{DatabaseName}</strong> has failed.</p>
                    <ul>
                        <li><strong>Database:</strong> {DatabaseName}</li>
                        <li><strong>Error:</strong> {ErrorMessage}</li>
                        <li><strong>Failed At:</strong> {FailedAt}</li>
                        <li><strong>Duration:</strong> {Duration}</li>
                    </ul>
                    <p style='color: red;'><strong>Action Required:</strong> Please investigate the error and retry the backup operation.</p>
                </body>
                </html>",
            TextBody = @"
                Backup Failed
                
                The backup operation for database {DatabaseName} has failed.
                
                Details:
                - Database: {DatabaseName}
                - Error: {ErrorMessage}
                - Failed At: {FailedAt}
                - Duration: {Duration}
                
                Action Required: Please investigate the error and retry the backup operation.",
            RequiredVariables = new List<string> { "DatabaseName", "ErrorMessage", "FailedAt", "Duration" }
        };

        _templates.TryAdd(backupSuccessTemplate.Name, backupSuccessTemplate);
        _templates.TryAdd(backupFailureTemplate.Name, backupFailureTemplate);

        _logger.LogInformation("Initialized {Count} default email templates", _templates.Count);
    }
}