using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Text.Json;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 发送关键错误警报和通知的服务 / Service for sending critical error alerts and notifications
/// 提供多渠道通知功能，包括邮件、Webhook和文件日志，支持速率限制和配置管理
/// Provides multi-channel notification functionality including email, webhook and file logging with rate limiting and configuration management
/// </summary>
public class AlertingService : IAlertingService
{
    private readonly ILogger<AlertingService> _logger;
    private readonly HttpClient _httpClient;
    private AlertingConfig _configuration;
    private readonly Dictionary<NotificationChannel, DateTime> _lastAlertTimes = new();
    private readonly Dictionary<NotificationChannel, int> _alertCounts = new();

    /// <summary>
    /// 初始化警报服务实例 / Initializes alerting service instance
    /// </summary>
    /// <param name="logger">日志记录器 / Logger instance</param>
    /// <param name="httpClient">HTTP客户端用于Webhook通知 / HTTP client for webhook notifications</param>
    /// <param name="configuration">警报配置，可选 / Alerting configuration, optional</param>
    public AlertingService(ILogger<AlertingService> logger, HttpClient httpClient, AlertingConfig? configuration = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? new AlertingConfig();
    }

    /// <summary>
    /// 获取当前的警报配置 / Gets the current alerting configuration
    /// </summary>
    public AlertingConfig Configuration => _configuration;

    /// <summary>
    /// 更新警报配置 / Updates the alerting configuration
    /// </summary>
    /// <param name="config">新的配置设置 / New configuration settings</param>
    public void UpdateConfiguration(AlertingConfig config)
    {
        _configuration = config ?? throw new ArgumentNullException(nameof(config));
        _logger.LogInformation("Alerting configuration updated");
    }

    /// <summary>
    /// 通过所有配置的渠道发送关键错误警报 / Sends a critical error alert through all configured channels
    /// 检查速率限制，创建通知并通过启用的渠道发送警报
    /// Checks rate limiting, creates notification and sends alert through enabled channels
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

            // 检查速率限制 / Check rate limiting
            if (IsRateLimited())
            {
                _logger.LogWarning("Alert rate limit exceeded, skipping alert for operation {OperationId}", alert.OperationId);
                return false;
            }

            // 转换为通知 / Convert to notification
            var notification = CreateNotificationFromAlert(alert);
            
            // 确定要使用的渠道 / Determine which channels to use
            var channels = GetEnabledChannels();
            
            if (!channels.Any())
            {
                _logger.LogWarning("No notification channels are enabled, cannot send alert for operation {OperationId}", alert.OperationId);
                return false;
            }

            // 通过所有渠道发送通知 / Send notification through all channels
            var result = await SendNotificationAsync(notification, channels, cancellationToken);

            // 使用通知结果更新警报 / Update alert with notification results
            if (alert is EnhancedCriticalErrorAlert enhancedAlert)
            {
                enhancedAlert.NotificationChannels = channels.ToList();
                enhancedAlert.NotificationResults = result.ChannelResults;
                enhancedAlert.NotificationErrors = result.ChannelErrors;
                enhancedAlert.NotificationDuration = result.Duration;
            }

            // 更新向后兼容的属性 / Update legacy properties for backward compatibility
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
    /// 通过指定渠道发送通知 / Sends a notification through specified channels
    /// 并行发送通知到多个渠道，收集结果并处理超时和错误
    /// Sends notifications to multiple channels in parallel, collects results and handles timeouts and errors
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

        // 检查严重性阈值 / Check severity threshold
        if (notification.Severity < _configuration.MinimumSeverity)
        {
            _logger.LogDebug("Notification {NotificationId} severity {Severity} is below minimum threshold {MinimumSeverity}, skipping",
                notification.Id, notification.Severity, _configuration.MinimumSeverity);
            result.Success = false;
            result.Duration = DateTime.UtcNow - startTime;
            return result;
        }

        // 通过每个渠道发送 / Send through each channel
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
    /// 测试所有配置的通知渠道的连接性 / Tests connectivity to all configured notification channels
    /// 向每个启用的渠道发送测试通知以验证配置和连接性
    /// Sends test notifications to each enabled channel to verify configuration and connectivity
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
    /// 获取所有启用的通知渠道 / Gets all enabled notification channels
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
    /// 检查速率限制是否应阻止发送警报 / Checks if rate limiting should prevent sending alerts
    /// </summary>
    private bool IsRateLimited()
    {
        var now = DateTime.UtcNow;
        var hourAgo = now.AddHours(-1);

        // 清理旧条目 / Clean up old entries
        var channelsToClean = _lastAlertTimes.Where(kvp => kvp.Value < hourAgo).Select(kvp => kvp.Key).ToList();
        foreach (var channel in channelsToClean)
        {
            _lastAlertTimes.Remove(channel);
            _alertCounts.Remove(channel);
        }

        // 检查是否超过速率限制 / Check if we've exceeded the rate limit
        var totalAlerts = _alertCounts.Values.Sum();
        return totalAlerts >= _configuration.MaxAlertsPerHour;
    }

    /// <summary>
    /// 从关键错误警报创建通知 / Creates a notification from a critical error alert
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
    /// 构建警报消息内容 / Builds the alert message content
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
    /// 向特定渠道发送通知 / Sends notification to a specific channel
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
    /// 通过邮件发送通知 / Sends notification via email
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

            // 更新速率限制计数器 / Update rate limiting counters
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
    /// 通过Webhook发送通知 / Sends notification via webhook
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

            // 添加自定义头部 / Add custom headers
            foreach (var header in _configuration.Webhook.Headers)
            {
                httpContent.Headers.Add(header.Key, header.Value);
            }

            // 如果配置了认证令牌，添加认证头部 / Add authentication header if configured
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
                // 更新速率限制计数器 / Update rate limiting counters
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
    /// 向文件日志发送通知 / Sends notification to file log
    /// </summary>
    private async Task<bool> SendFileLogNotificationAsync(Notification notification, CancellationToken cancellationToken)
    {
        try
        {
            // 确保日志目录存在 / Ensure log directory exists
            if (!Directory.Exists(_configuration.FileLog.LogDirectory))
            {
                Directory.CreateDirectory(_configuration.FileLog.LogDirectory);
            }

            // 生成日志文件名 / Generate log file name
            var fileName = _configuration.FileLog.FileNamePattern
                .Replace("{yyyy}", DateTime.UtcNow.ToString("yyyy"))
                .Replace("{MM}", DateTime.UtcNow.ToString("MM"))
                .Replace("{dd}", DateTime.UtcNow.ToString("dd"))
                .Replace("{HH}", DateTime.UtcNow.ToString("HH"));

            var filePath = Path.Combine(_configuration.FileLog.LogDirectory, fileName);

            // 创建日志条目 / Create log entry
            var logEntry = $"[{notification.CreatedAt:yyyy-MM-dd HH:mm:ss}] [{notification.Severity}] {notification.Subject}{Environment.NewLine}{notification.Message}{Environment.NewLine}{Environment.NewLine}";

            // 写入文件 / Write to file
            await File.AppendAllTextAsync(filePath, logEntry, cancellationToken);

            // 检查文件大小并在必要时轮转 / Check file size and rotate if necessary
            await RotateLogFileIfNeededAsync(filePath);

            // 更新速率限制计数器 / Update rate limiting counters
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
    /// 测试特定渠道的连接性 / Tests connectivity to a specific channel
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
    /// 如果日志文件超过最大大小则进行轮转 / Rotates log file if it exceeds the maximum size
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

            // 查找现有的轮转文件 / Find existing rotated files
            var rotatedFiles = Directory.GetFiles(directory, $"{fileNameWithoutExtension}_*{extension}")
                .OrderByDescending(f => f)
                .ToList();

            // 删除多余的文件 / Remove excess files
            while (rotatedFiles.Count >= _configuration.FileLog.MaxFileCount - 1)
            {
                File.Delete(rotatedFiles.Last());
                rotatedFiles.RemoveAt(rotatedFiles.Count - 1);
            }

            // 轮转当前文件 / Rotate current file
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