using System.Collections.Concurrent;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 通过SMTP发送邮件通知的服务 / Service for sending email notifications via SMTP
/// 支持单个和批量邮件发送、模板管理、状态跟踪和统计功能 / Supports single and bulk email sending, template management, status tracking and statistics
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly ConcurrentDictionary<string, NotificationStatus> _notificationStatuses;
    private readonly ConcurrentDictionary<string, EmailTemplate> _templates;
    private SmtpConfig _configuration;

    /// <summary>
    /// 初始化通知服务 / Initialize notification service
    /// </summary>
    /// <param name="logger">日志记录器 / Logger instance</param>
    /// <exception cref="ArgumentNullException">当logger为null时抛出 / Thrown when logger is null</exception>
    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _notificationStatuses = new ConcurrentDictionary<string, NotificationStatus>();
        _templates = new ConcurrentDictionary<string, EmailTemplate>();
        _configuration = new SmtpConfig();
        
        // 初始化默认模板 / Initialize with default templates
        InitializeDefaultTemplates();
    }

    /// <summary>
    /// 获取当前SMTP配置 / Gets current SMTP configuration
    /// </summary>
    public SmtpConfig Configuration => _configuration;

    /// <summary>
    /// 更新SMTP配置 / Updates SMTP configuration
    /// </summary>
    /// <param name="config">新的SMTP配置 / New SMTP configuration</param>
    /// <exception cref="ArgumentNullException">当config为null时抛出 / Thrown when config is null</exception>
    public void UpdateConfiguration(SmtpConfig config)
    {
        _configuration = config ?? throw new ArgumentNullException(nameof(config));
        _logger.LogInformation("SMTP configuration updated for host {Host}:{Port}", config.Host, config.Port);
    }

    /// <summary>
    /// 异步发送单个邮件消息 / Sends a single email message asynchronously
    /// 支持HTML和纯文本格式、附件、自定义头部和优先级设置 / Supports HTML and plain text formats, attachments, custom headers and priority settings
    /// </summary>
    /// <param name="message">要发送的邮件消息 / Email message to send</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>发送成功返回true，失败返回false / Returns true if sent successfully, false if failed</returns>
    /// <exception cref="ArgumentNullException">当message为null时抛出 / Thrown when message is null</exception>
    public async Task<bool> SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        _logger.LogInformation("Sending email to {Recipient} with subject '{Subject}'", message.To, message.Subject);

        // 更新状态为发送中 / Update status to sending
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
            
            // 配置超时时间 / Configure timeout
            client.Timeout = _configuration.TimeoutSeconds * 1000;

            // 连接到SMTP服务器 / Connect to SMTP server
            await client.ConnectAsync(_configuration.Host, _configuration.Port, 
                _configuration.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None, 
                cancellationToken);

            // 如果提供了凭据则进行身份验证 / Authenticate if credentials are provided
            if (!string.IsNullOrEmpty(_configuration.Username) && !string.IsNullOrEmpty(_configuration.Password))
            {
                await client.AuthenticateAsync(_configuration.Username, _configuration.Password, cancellationToken);
            }

            // 创建MimeMessage / Create MimeMessage
            var mimeMessage = CreateMimeMessage(message);

            // 发送消息 / Send the message
            await client.SendAsync(mimeMessage, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            // 更新状态为已发送 / Update status to sent
            status.Status = NotificationDeliveryStatus.Sent;
            status.SentAt = DateTime.Now;
            _notificationStatuses.AddOrUpdate(message.Id, status, (key, existing) => status);

            _logger.LogInformation("Successfully sent email to {Recipient}", message.To);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipient}: {Error}", message.To, ex.Message);

            // 更新状态为失败 / Update status to failed
            status.Status = NotificationDeliveryStatus.Failed;
            status.LastError = ex.Message;
            _notificationStatuses.AddOrUpdate(message.Id, status, (key, existing) => status);

            return false;
        }
    }

    /// <summary>
    /// 批量发送多个邮件消息 / Sends multiple email messages in bulk
    /// 使用并发发送以提高性能，支持限制并发数量 / Uses concurrent sending for performance with configurable concurrency limit
    /// </summary>
    /// <param name="messages">要发送的邮件消息集合 / Collection of email messages to send</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>每个消息的发送结果字典 / Dictionary of send results for each message</returns>
    /// <exception cref="ArgumentNullException">当messages为null时抛出 / Thrown when messages is null</exception>
    public async Task<Dictionary<string, bool>> SendBulkEmailAsync(IEnumerable<EmailMessage> messages, CancellationToken cancellationToken = default)
    {
        if (messages == null)
            throw new ArgumentNullException(nameof(messages));

        var results = new Dictionary<string, bool>();
        var messageList = messages.ToList();

        _logger.LogInformation("Sending bulk email to {Count} recipients", messageList.Count);

        // 使用有限并行度并发发送邮件 / Send emails concurrently with limited parallelism
        var semaphore = new SemaphoreSlim(5); // 限制为5个并发发送 / Limit to 5 concurrent sends
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
    /// 获取指定通知的投递状态 / Gets the delivery status of a specific notification
    /// </summary>
    /// <param name="notificationId">通知ID / Notification ID</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>通知状态或null（如果未找到） / Notification status or null if not found</returns>
    /// <exception cref="ArgumentException">当notificationId为空时抛出 / Thrown when notificationId is empty</exception>
    public async Task<NotificationStatus?> GetStatusAsync(string notificationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(notificationId))
            throw new ArgumentException("Notification ID cannot be null or empty", nameof(notificationId));

        return await Task.FromResult(_notificationStatuses.TryGetValue(notificationId, out var status) ? status : null);
    }

    /// <summary>
    /// 检索所有可用的邮件模板 / Retrieves all available email templates
    /// 只返回活跃状态的模板 / Returns only active templates
    /// </summary>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>活跃邮件模板集合 / Collection of active email templates</returns>
    public async Task<IEnumerable<EmailTemplate>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_templates.Values.Where(t => t.IsActive));
    }

    /// <summary>
    /// 按类别检索邮件模板 / Retrieves email templates by category
    /// </summary>
    /// <param name="category">模板类别 / Template category</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>指定类别的活跃邮件模板集合 / Collection of active email templates in specified category</returns>
    public async Task<IEnumerable<EmailTemplate>> GetTemplatesByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_templates.Values.Where(t => t.IsActive && 
            string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// 按名称获取指定的邮件模板 / Gets a specific email template by name
    /// </summary>
    /// <param name="templateName">模板名称 / Template name</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>邮件模板或null（如果未找到） / Email template or null if not found</returns>
    /// <exception cref="ArgumentException">当templateName为空时抛出 / Thrown when templateName is empty</exception>
    public async Task<EmailTemplate?> GetTemplateAsync(string templateName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(templateName))
            throw new ArgumentException("Template name cannot be null or empty", nameof(templateName));

        return await Task.FromResult(_templates.TryGetValue(templateName, out var template) ? template : null);
    }

    /// <summary>
    /// 使用提供的配置测试SMTP连接 / Tests the SMTP connection with the provided configuration
    /// 验证连接、身份验证和基本功能 / Verifies connection, authentication and basic functionality
    /// </summary>
    /// <param name="config">要测试的SMTP配置 / SMTP configuration to test</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>连接成功返回true，失败返回false / Returns true if connection successful, false if failed</returns>
    /// <exception cref="ArgumentNullException">当config为null时抛出 / Thrown when config is null</exception>
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
    /// 从模板创建邮件消息并进行变量替换 / Creates an email message from a template with variable substitution
    /// 支持必需变量验证和动态内容生成 / Supports required variable validation and dynamic content generation
    /// </summary>
    /// <param name="templateName">模板名称 / Template name</param>
    /// <param name="recipient">收件人 / Recipient</param>
    /// <param name="variables">模板变量字典 / Template variables dictionary</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>生成的邮件消息或null（如果失败） / Generated email message or null if failed</returns>
    /// <exception cref="ArgumentException">当templateName或recipient为空时抛出 / Thrown when templateName or recipient is empty</exception>
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

        // 检查必需变量 / Check for required variables
        var missingVariables = template.RequiredVariables.Where(v => !variables.ContainsKey(v)).ToList();
        if (missingVariables.Any())
        {
            _logger.LogError("Missing required variables for template '{TemplateName}': {MissingVariables}", 
                templateName, string.Join(", ", missingVariables));
            return null;
        }

        // 在主题和正文中替换变量 / Substitute variables in subject and body
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
    /// 获取通知投递统计信息 / Gets statistics about notification delivery
    /// 包括成功率、失败原因、投递时间分布等 / Includes success rates, failure reasons, delivery time distribution, etc.
    /// </summary>
    /// <param name="fromDate">统计开始日期（可选） / Statistics start date (optional)</param>
    /// <param name="toDate">统计结束日期（可选） / Statistics end date (optional)</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>通知统计信息 / Notification statistics</returns>
    public async Task<NotificationStatistics> GetStatisticsAsync(
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        CancellationToken cancellationToken = default)
    {
        var from = fromDate ?? DateTime.Now.AddDays(-30);
        var to = toDate ?? DateTime.Now;

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
            GeneratedAt = DateTime.Now
        };

        // 计算成功投递的平均投递时间 / Calculate average delivery time for successful deliveries
        var successfulWithTimes = relevantStatuses
            .Where(s => s.Status == NotificationDeliveryStatus.Sent && s.SentAt.HasValue)
            .ToList();

        if (successfulWithTimes.Any())
        {
            var totalDeliveryTime = successfulWithTimes
                .Sum(s => (s.SentAt!.Value - s.CreatedAt).TotalMilliseconds);
            statistics.AverageDeliveryTime = TimeSpan.FromMilliseconds(totalDeliveryTime / successfulWithTimes.Count);
        }

        // 分组失败原因 / Group failure reasons
        statistics.FailureReasons = relevantStatuses
            .Where(s => s.Status == NotificationDeliveryStatus.Failed && !string.IsNullOrEmpty(s.LastError))
            .GroupBy(s => s.LastError!)
            .ToDictionary(g => g.Key, g => g.Count());

        // 按小时分组投递 / Group deliveries by hour
        statistics.DeliveriesByHour = relevantStatuses
            .Where(s => s.SentAt.HasValue)
            .GroupBy(s => s.SentAt!.Value.Hour)
            .ToDictionary(g => g.Key, g => g.Count());

        return await Task.FromResult(statistics);
    }

    /// <summary>
    /// 从EmailMessage创建MimeMessage / Creates a MimeMessage from an EmailMessage
    /// 处理发件人、收件人、主题、正文、附件和优先级设置 / Handles sender, recipient, subject, body, attachments and priority settings
    /// </summary>
    /// <param name="message">源邮件消息 / Source email message</param>
    /// <returns>MimeMessage实例 / MimeMessage instance</returns>
    private MimeMessage CreateMimeMessage(EmailMessage message)
    {
        var mimeMessage = new MimeMessage();
        
        // 设置发件人 / Set sender
        mimeMessage.From.Add(new MailboxAddress(_configuration.FromName, _configuration.FromAddress));
        
        // 设置收件人 / Set recipient
        mimeMessage.To.Add(MailboxAddress.Parse(message.To));
        
        // 设置主题 / Set subject
        mimeMessage.Subject = message.Subject;

        // 添加自定义头部 / Add custom headers
        foreach (var header in message.Headers)
        {
            mimeMessage.Headers.Add(header.Key, header.Value);
        }

        // 设置优先级 / Set priority
        mimeMessage.Priority = message.Priority switch
        {
            EmailPriority.Low => MessagePriority.NonUrgent,
            EmailPriority.High => MessagePriority.Urgent,
            EmailPriority.Critical => MessagePriority.Urgent,
            _ => MessagePriority.Normal
        };

        // 创建正文 / Create body
        var bodyBuilder = new BodyBuilder();
        
        if (message.IsHtml)
        {
            bodyBuilder.HtmlBody = message.Body;
        }
        else
        {
            bodyBuilder.TextBody = message.Body;
        }

        // 添加附件 / Add attachments
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
    /// 在模板字符串中替换变量 / Substitutes variables in a template string
    /// 使用大括号语法进行变量替换 / Uses curly brace syntax for variable substitution
    /// </summary>
    /// <param name="template">模板字符串 / Template string</param>
    /// <param name="variables">变量字典 / Variables dictionary</param>
    /// <returns>替换后的字符串 / String with substituted variables</returns>
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
    /// 初始化默认邮件模板 / Initializes default email templates
    /// 创建备份成功和失败的标准模板 / Creates standard templates for backup success and failure
    /// </summary>
    private void InitializeDefaultTemplates()
    {
        // 备份成功模板 / Backup Success Template
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

        // 备份失败模板 / Backup Failure Template
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