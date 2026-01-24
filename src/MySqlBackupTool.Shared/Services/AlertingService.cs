using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Text.Json;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Service for sending critical error alerts and notifications
/// </summary>
public class AlertingService : IAlertingService
{
    private readonly ILogger<AlertingService> _logger;
    private readonly HttpClient _httpClient;
    private AlertingConfig _configuration;
    private readonly Dictionary<NotificationChannel, DateTime> _lastAlertTimes = new();
    private readonly Dictionary<NotificationChannel, int> _alertCounts = new();

    public AlertingService(ILogger<AlertingService> logger, HttpClient httpClient, AlertingConfig? configuration = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? new AlertingConfig();
    }

    public AlertingConfig Configuration => _configuration;

    public void UpdateConfiguration(AlertingConfig config)
    {
        _configuration = config ?? throw new ArgumentNullException(nameof(config));
        _logger.LogInformation("Alerting configuration updated");
    }

    /// <summary>
    /// Sends a critical error alert through all configured channels
    /// </summary>
    public async Task<bool> SendCriticalErrorAlertAsync(CriticalErrorAlert alert, CancellationToken cancellationToken = default)
    {
        if (!_configuration.EnableAlerting)
        {
            _logger.LogDebug("Alerting is disabled, skipping alert for operation {OperationId}", alert.OperationId);
            return false;
        }

        var startTime = DateTime.UtcNow;
        var operationId = Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation("Sending critical error alert for operation {OperationId}, error type {ErrorType} (Alert ID: {AlertId})",
                alert.OperationId, alert.ErrorType, operationId);

            // Check rate limiting
            if (IsRateLimited())
            {
                _logger.LogWarning("Alert rate limit exceeded, skipping alert for operation {OperationId}", alert.OperationId);
                return false;
            }

            // Convert to notification
            var notification = CreateNotificationFromAlert(alert);
            
            // Determine which channels to use
            var channels = GetEnabledChannels();
            
            if (!channels.Any())
            {
                _logger.LogWarning("No notification channels are enabled, cannot send alert for operation {OperationId}", alert.OperationId);
                return false;
            }

            // Send notification through all channels
            var result = await SendNotificationAsync(notification, channels, cancellationToken);

            // Update alert with notification results
            if (alert is EnhancedCriticalErrorAlert enhancedAlert)
            {
                enhancedAlert.NotificationChannels = channels.ToList();
                enhancedAlert.NotificationResults = result.ChannelResults;
                enhancedAlert.NotificationErrors = result.ChannelErrors;
                enhancedAlert.NotificationDuration = result.Duration;
            }

            // Update legacy properties for backward compatibility
            alert.AlertSent = result.Success;
            alert.AlertSentAt = result.Success ? DateTime.UtcNow : null;

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Critical error alert completed for operation {OperationId}: Success={Success}, Channels={SuccessfulChannels}/{TotalChannels}, Duration={Duration}ms",
                alert.OperationId, result.Success, result.SuccessfulChannels, channels.Count(), duration.TotalMilliseconds);

            return result.Success;
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Failed to send critical error alert for operation {OperationId} after {Duration}ms: {ErrorMessage}",
                alert.OperationId, duration.TotalMilliseconds, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Sends a notification through specified channels
    /// </summary>
    public async Task<NotificationResult> SendNotificationAsync(
        Notification notification, 
        IEnumerable<NotificationChannel>? channels = null, 
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var targetChannels = channels?.ToList() ?? GetEnabledChannels().ToList();
        
        var result = new NotificationResult();

        _logger.LogInformation("Sending notification {NotificationId} through {ChannelCount} channels: {Channels}",
            notification.Id, targetChannels.Count, string.Join(", ", targetChannels));

        // Check severity threshold
        if (notification.Severity < _configuration.MinimumSeverity)
        {
            _logger.LogDebug("Notification {NotificationId} severity {Severity} is below minimum threshold {MinimumSeverity}, skipping",
                notification.Id, notification.Severity, _configuration.MinimumSeverity);
            result.Success = false;
            result.Duration = DateTime.UtcNow - startTime;
            return result;
        }

        // Send through each channel
        var tasks = targetChannels.Select(async channel =>
        {
            try
            {
                using var channelCts = new CancellationTokenSource(_configuration.NotificationTimeout);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, channelCts.Token);

                var channelSuccess = await SendToChannelAsync(notification, channel, combinedCts.Token);
                
                lock (result)
                {
                    result.ChannelResults[channel] = channelSuccess;
                    if (!channelSuccess)
                    {
                        result.ChannelErrors[channel] = "Channel delivery failed";
                    }
                }

                _logger.LogDebug("Notification {NotificationId} sent to {Channel}: Success={Success}",
                    notification.Id, channel, channelSuccess);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                lock (result)
                {
                    result.ChannelResults[channel] = false;
                    result.ChannelErrors[channel] = "Operation was cancelled";
                }
                _logger.LogWarning("Notification {NotificationId} to {Channel} was cancelled", notification.Id, channel);
            }
            catch (OperationCanceledException)
            {
                lock (result)
                {
                    result.ChannelResults[channel] = false;
                    result.ChannelErrors[channel] = "Channel delivery timed out";
                }
                _logger.LogWarning("Notification {NotificationId} to {Channel} timed out after {Timeout}ms",
                    notification.Id, channel, _configuration.NotificationTimeout.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                lock (result)
                {
                    result.ChannelResults[channel] = false;
                    result.ChannelErrors[channel] = ex.Message;
                }
                _logger.LogError(ex, "Failed to send notification {NotificationId} to {Channel}: {ErrorMessage}",
                    notification.Id, channel, ex.Message);
            }
        });

        await Task.WhenAll(tasks);

        result.Duration = DateTime.UtcNow - startTime;
        result.Success = result.SuccessfulChannels > 0;

        _logger.LogInformation("Notification {NotificationId} completed: Success={Success}, Successful={SuccessfulChannels}/{TotalChannels}, Duration={Duration}ms",
            notification.Id, result.Success, result.SuccessfulChannels, targetChannels.Count, result.Duration.TotalMilliseconds);

        return result;
    }

    /// <summary>
    /// Tests connectivity to all configured notification channels
    /// </summary>
    public async Task<Dictionary<NotificationChannel, bool>> TestNotificationChannelsAsync(CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<NotificationChannel, bool>();
        var channels = GetEnabledChannels();

        _logger.LogInformation("Testing {ChannelCount} notification channels: {Channels}",
            channels.Count(), string.Join(", ", channels));

        foreach (var channel in channels)
        {
            try
            {
                using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, testCts.Token);

                var testResult = await TestChannelAsync(channel, combinedCts.Token);
                results[channel] = testResult;

                _logger.LogInformation("Channel {Channel} test result: {Success}", channel, testResult);
            }
            catch (Exception ex)
            {
                results[channel] = false;
                _logger.LogError(ex, "Failed to test channel {Channel}: {ErrorMessage}", channel, ex.Message);
            }
        }

        return results;
    }

    #region Private Helper Methods

    /// <summary>
    /// Gets all enabled notification channels
    /// </summary>
    private IEnumerable<NotificationChannel> GetEnabledChannels()
    {
        var channels = new List<NotificationChannel>();

        if (_configuration.Email.Enabled)
            channels.Add(NotificationChannel.Email);
        
        if (_configuration.Webhook.Enabled)
            channels.Add(NotificationChannel.Webhook);
        
        if (_configuration.FileLog.Enabled)
            channels.Add(NotificationChannel.FileLog);

        return channels;
    }

    /// <summary>
    /// Checks if rate limiting should prevent sending alerts
    /// </summary>
    private bool IsRateLimited()
    {
        var now = DateTime.UtcNow;
        var hourAgo = now.AddHours(-1);

        // Clean up old entries
        var channelsToClean = _lastAlertTimes.Where(kvp => kvp.Value < hourAgo).Select(kvp => kvp.Key).ToList();
        foreach (var channel in channelsToClean)
        {
            _lastAlertTimes.Remove(channel);
            _alertCounts.Remove(channel);
        }

        // Check if we've exceeded the rate limit
        var totalAlerts = _alertCounts.Values.Sum();
        return totalAlerts >= _configuration.MaxAlertsPerHour;
    }

    /// <summary>
    /// Creates a notification from a critical error alert
    /// </summary>
    private Notification CreateNotificationFromAlert(CriticalErrorAlert alert)
    {
        return new Notification
        {
            Id = alert.Id,
            CreatedAt = alert.OccurredAt,
            Severity = AlertSeverity.Critical,
            Subject = $"MySQL Backup Tool Critical Error - {alert.ErrorType}",
            Message = BuildAlertMessage(alert),
            OperationId = alert.OperationId,
            Metadata = new Dictionary<string, object>
            {
                ["ErrorType"] = alert.ErrorType,
                ["OperationId"] = alert.OperationId,
                ["Context"] = alert.Context
            }
        };
    }

    /// <summary>
    /// Builds the alert message content
    /// </summary>
    private string BuildAlertMessage(CriticalErrorAlert alert)
    {
        var sb = new StringBuilder();
        sb.AppendLine("MySQL Backup Tool Critical Error Alert");
        sb.AppendLine("=====================================");
        sb.AppendLine();
        sb.AppendLine($"Alert ID: {alert.Id}");
        sb.AppendLine($"Operation ID: {alert.OperationId}");
        sb.AppendLine($"Error Type: {alert.ErrorType}");
        sb.AppendLine($"Occurred At: {alert.OccurredAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("Error Message:");
        sb.AppendLine(alert.ErrorMessage);
        sb.AppendLine();

        if (alert.Context.Any())
        {
            sb.AppendLine("Context Information:");
            foreach (var kvp in alert.Context)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(alert.StackTrace))
        {
            sb.AppendLine("Stack Trace:");
            sb.AppendLine(alert.StackTrace);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Sends notification to a specific channel
    /// </summary>
    private async Task<bool> SendToChannelAsync(Notification notification, NotificationChannel channel, CancellationToken cancellationToken)
    {
        return channel switch
        {
            NotificationChannel.Email => await SendEmailNotificationAsync(notification, cancellationToken),
            NotificationChannel.Webhook => await SendWebhookNotificationAsync(notification, cancellationToken),
            NotificationChannel.FileLog => await SendFileLogNotificationAsync(notification, cancellationToken),
            _ => throw new ArgumentException($"Unknown notification channel: {channel}")
        };
    }

    /// <summary>
    /// Sends notification via email
    /// </summary>
    private async Task<bool> SendEmailNotificationAsync(Notification notification, CancellationToken cancellationToken)
    {
        try
        {
            if (!_configuration.Email.Recipients.Any())
            {
                _logger.LogWarning("No email recipients configured for notification {NotificationId}", notification.Id);
                return false;
            }

            using var smtpClient = new SmtpClient(_configuration.Email.SmtpServer, _configuration.Email.SmtpPort)
            {
                EnableSsl = _configuration.Email.UseSsl,
                UseDefaultCredentials = false,
                Credentials = new System.Net.NetworkCredential(_configuration.Email.Username, _configuration.Email.Password)
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_configuration.Email.FromAddress, _configuration.Email.FromName),
                Subject = notification.Subject,
                Body = notification.Message,
                IsBodyHtml = false
            };

            foreach (var recipient in _configuration.Email.Recipients)
            {
                mailMessage.To.Add(recipient);
            }

            await smtpClient.SendMailAsync(mailMessage, cancellationToken);

            // Update rate limiting counters
            var now = DateTime.UtcNow;
            _lastAlertTimes[NotificationChannel.Email] = now;
            _alertCounts[NotificationChannel.Email] = _alertCounts.GetValueOrDefault(NotificationChannel.Email, 0) + 1;

            _logger.LogDebug("Email notification {NotificationId} sent successfully to {RecipientCount} recipients",
                notification.Id, _configuration.Email.Recipients.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email notification {NotificationId}: {ErrorMessage}",
                notification.Id, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Sends notification via webhook
    /// </summary>
    private async Task<bool> SendWebhookNotificationAsync(Notification notification, CancellationToken cancellationToken)
    {
        try
        {
            var payload = new
            {
                id = notification.Id,
                timestamp = notification.CreatedAt,
                severity = notification.Severity.ToString(),
                subject = notification.Subject,
                message = notification.Message,
                operationId = notification.OperationId,
                metadata = notification.Metadata
            };

            var jsonContent = JsonSerializer.Serialize(payload);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, _configuration.Webhook.ContentType);

            // Add custom headers
            foreach (var header in _configuration.Webhook.Headers)
            {
                httpContent.Headers.Add(header.Key, header.Value);
            }

            // Add authentication header if configured
            if (!string.IsNullOrEmpty(_configuration.Webhook.AuthToken))
            {
                httpContent.Headers.Add(_configuration.Webhook.AuthHeaderName, _configuration.Webhook.AuthToken);
            }

            HttpResponseMessage response;
            switch (_configuration.Webhook.HttpMethod.ToUpperInvariant())
            {
                case "POST":
                    response = await _httpClient.PostAsync(_configuration.Webhook.Url, httpContent, cancellationToken);
                    break;
                case "PUT":
                    response = await _httpClient.PutAsync(_configuration.Webhook.Url, httpContent, cancellationToken);
                    break;
                default:
                    throw new NotSupportedException($"HTTP method {_configuration.Webhook.HttpMethod} is not supported for webhooks");
            }

            if (response.IsSuccessStatusCode)
            {
                // Update rate limiting counters
                var now = DateTime.UtcNow;
                _lastAlertTimes[NotificationChannel.Webhook] = now;
                _alertCounts[NotificationChannel.Webhook] = _alertCounts.GetValueOrDefault(NotificationChannel.Webhook, 0) + 1;

                _logger.LogDebug("Webhook notification {NotificationId} sent successfully to {Url}, status: {StatusCode}",
                    notification.Id, _configuration.Webhook.Url, response.StatusCode);
                return true;
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Webhook notification {NotificationId} failed with status {StatusCode}: {ResponseContent}",
                    notification.Id, response.StatusCode, responseContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send webhook notification {NotificationId} to {Url}: {ErrorMessage}",
                notification.Id, _configuration.Webhook.Url, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Sends notification to file log
    /// </summary>
    private async Task<bool> SendFileLogNotificationAsync(Notification notification, CancellationToken cancellationToken)
    {
        try
        {
            // Ensure log directory exists
            if (!Directory.Exists(_configuration.FileLog.LogDirectory))
            {
                Directory.CreateDirectory(_configuration.FileLog.LogDirectory);
            }

            // Generate log file name
            var fileName = _configuration.FileLog.FileNamePattern
                .Replace("{yyyy}", DateTime.UtcNow.ToString("yyyy"))
                .Replace("{MM}", DateTime.UtcNow.ToString("MM"))
                .Replace("{dd}", DateTime.UtcNow.ToString("dd"))
                .Replace("{HH}", DateTime.UtcNow.ToString("HH"));

            var filePath = Path.Combine(_configuration.FileLog.LogDirectory, fileName);

            // Create log entry
            var logEntry = $"[{notification.CreatedAt:yyyy-MM-dd HH:mm:ss}] [{notification.Severity}] {notification.Subject}{Environment.NewLine}{notification.Message}{Environment.NewLine}{Environment.NewLine}";

            // Write to file
            await File.AppendAllTextAsync(filePath, logEntry, cancellationToken);

            // Check file size and rotate if necessary
            await RotateLogFileIfNeededAsync(filePath);

            // Update rate limiting counters
            var now = DateTime.UtcNow;
            _lastAlertTimes[NotificationChannel.FileLog] = now;
            _alertCounts[NotificationChannel.FileLog] = _alertCounts.GetValueOrDefault(NotificationChannel.FileLog, 0) + 1;

            _logger.LogDebug("File log notification {NotificationId} written to {FilePath}",
                notification.Id, filePath);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write file log notification {NotificationId}: {ErrorMessage}",
                notification.Id, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Tests connectivity to a specific channel
    /// </summary>
    private async Task<bool> TestChannelAsync(NotificationChannel channel, CancellationToken cancellationToken)
    {
        var testNotification = new Notification
        {
            Subject = "MySQL Backup Tool - Test Notification",
            Message = $"This is a test notification sent at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC to verify channel connectivity.",
            Severity = AlertSeverity.Info
        };

        return await SendToChannelAsync(testNotification, channel, cancellationToken);
    }

    /// <summary>
    /// Rotates log file if it exceeds the maximum size
    /// </summary>
    private async Task RotateLogFileIfNeededAsync(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists || fileInfo.Length < _configuration.FileLog.MaxFileSizeMB * 1024 * 1024)
            {
                return;
            }

            var directory = Path.GetDirectoryName(filePath)!;
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);

            // Find existing rotated files
            var rotatedFiles = Directory.GetFiles(directory, $"{fileNameWithoutExtension}_*{extension}")
                .OrderByDescending(f => f)
                .ToList();

            // Remove excess files
            while (rotatedFiles.Count >= _configuration.FileLog.MaxFileCount - 1)
            {
                File.Delete(rotatedFiles.Last());
                rotatedFiles.RemoveAt(rotatedFiles.Count - 1);
            }

            // Rotate current file
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var rotatedFileName = $"{fileNameWithoutExtension}_{timestamp}{extension}";
            var rotatedFilePath = Path.Combine(directory, rotatedFileName);

            File.Move(filePath, rotatedFilePath);

            _logger.LogInformation("Rotated log file {OriginalFile} to {RotatedFile}",
                filePath, rotatedFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to rotate log file {FilePath}: {ErrorMessage}",
                filePath, ex.Message);
        }
    }

    #endregion
}