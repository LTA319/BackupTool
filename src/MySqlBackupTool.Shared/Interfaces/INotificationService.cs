using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 提供基于SMTP的邮件传递的电子邮件通知服务接口 / Interface for email notification service providing SMTP-based email delivery
/// 提供单个和批量邮件发送、模板管理、状态跟踪和统计功能
/// Provides single and bulk email sending, template management, status tracking and statistics functionality
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// 异步发送单个电子邮件消息 / Sends a single email message asynchronously
    /// 使用配置的SMTP服务器发送电子邮件，支持HTML和纯文本格式
    /// Sends email using configured SMTP server, supports both HTML and plain text formats
    /// </summary>
    /// <param name="message">要发送的电子邮件消息 / The email message to send</param>
    /// <param name="cancellationToken">操作的取消令牌 / Cancellation token for the operation</param>
    /// <returns>如果邮件发送成功返回true，否则返回false / True if the email was sent successfully, false otherwise</returns>
    /// <exception cref="ArgumentNullException">当消息参数为null时抛出 / Thrown when message is null</exception>
    /// <exception cref="InvalidOperationException">当SMTP配置无效时抛出 / Thrown when SMTP configuration is invalid</exception>
    Task<bool> SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量发送多个电子邮件消息 / Sends multiple email messages in bulk
    /// 高效地发送多个邮件，提供每个邮件的发送状态反馈
    /// Efficiently sends multiple emails with individual send status feedback for each message
    /// </summary>
    /// <param name="messages">要发送的电子邮件消息集合 / Collection of email messages to send</param>
    /// <param name="cancellationToken">操作的取消令牌 / Cancellation token for the operation</param>
    /// <returns>将消息ID映射到其发送成功状态的字典 / Dictionary mapping message IDs to their send success status</returns>
    /// <exception cref="ArgumentNullException">当消息集合为null时抛出 / Thrown when messages collection is null</exception>
    Task<Dictionary<string, bool>> SendBulkEmailAsync(IEnumerable<EmailMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取特定通知的传递状态 / Gets the delivery status of a specific notification
    /// 查询指定通知的当前状态，包括发送中、已发送、失败等状态
    /// Queries current status of specified notification including sending, sent, failed and other statuses
    /// </summary>
    /// <param name="notificationId">通知的唯一标识符 / Unique identifier of the notification</param>
    /// <param name="cancellationToken">操作的取消令牌 / Cancellation token for the operation</param>
    /// <returns>通知的当前状态，如果未找到则返回null / The current status of the notification, or null if not found</returns>
    /// <exception cref="ArgumentException">当通知ID为null或空时抛出 / Thrown when notificationId is null or empty</exception>
    Task<NotificationStatus?> GetStatusAsync(string notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检索所有可用的电子邮件模板 / Retrieves all available email templates
    /// 获取系统中定义的所有邮件模板，用于创建标准化的邮件内容
    /// Gets all email templates defined in the system for creating standardized email content
    /// </summary>
    /// <param name="cancellationToken">操作的取消令牌 / Cancellation token for the operation</param>
    /// <returns>可用电子邮件模板的集合 / Collection of available email templates</returns>
    Task<IEnumerable<EmailTemplate>> GetTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 按类别检索电子邮件模板 / Retrieves email templates by category
    /// 根据指定的类别筛选邮件模板，如警报、报告、通知等
    /// Filters email templates by specified category such as alerts, reports, notifications, etc.
    /// </summary>
    /// <param name="category">要筛选的模板类别 / Template category to filter by</param>
    /// <param name="cancellationToken">操作的取消令牌 / Cancellation token for the operation</param>
    /// <returns>指定类别中的电子邮件模板集合 / Collection of email templates in the specified category</returns>
    Task<IEnumerable<EmailTemplate>> GetTemplatesByCategoryAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据名称获取特定的电子邮件模板 / Gets a specific email template by name
    /// 通过模板名称查找并返回对应的邮件模板
    /// Finds and returns corresponding email template by template name
    /// </summary>
    /// <param name="templateName">要检索的模板名称 / Name of the template to retrieve</param>
    /// <param name="cancellationToken">操作的取消令牌 / Cancellation token for the operation</param>
    /// <returns>电子邮件模板，如果未找到则返回null / The email template, or null if not found</returns>
    /// <exception cref="ArgumentException">当模板名称为null或空时抛出 / Thrown when templateName is null or empty</exception>
    Task<EmailTemplate?> GetTemplateAsync(string templateName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用提供的配置测试SMTP连接 / Tests the SMTP connection with the provided configuration
    /// 验证SMTP服务器配置是否正确，包括服务器地址、端口、认证信息等
    /// Validates SMTP server configuration including server address, port, authentication credentials, etc.
    /// </summary>
    /// <param name="config">要测试的SMTP配置 / SMTP configuration to test</param>
    /// <param name="cancellationToken">操作的取消令牌 / Cancellation token for the operation</param>
    /// <returns>如果连接测试成功返回true，否则返回false / True if the connection test succeeds, false otherwise</returns>
    /// <exception cref="ArgumentNullException">当配置参数为null时抛出 / Thrown when config is null</exception>
    Task<bool> TestSmtpConnectionAsync(SmtpConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从模板创建电子邮件消息并进行变量替换 / Creates an email message from a template with variable substitution
    /// 使用指定的模板和变量字典创建个性化的邮件消息
    /// Creates personalized email message using specified template and variable dictionary
    /// </summary>
    /// <param name="templateName">要使用的模板名称 / Name of the template to use</param>
    /// <param name="recipient">收件人电子邮件地址 / Recipient email address</param>
    /// <param name="variables">要在模板中替换的变量字典 / Dictionary of variables to substitute in the template</param>
    /// <param name="cancellationToken">操作的取消令牌 / Cancellation token for the operation</param>
    /// <returns>创建的电子邮件消息，如果模板未找到则返回null / The created email message, or null if template not found</returns>
    /// <exception cref="ArgumentException">当模板名称或收件人为null或空时抛出 / Thrown when templateName or recipient is null or empty</exception>
    Task<EmailMessage?> CreateEmailFromTemplateAsync(
        string templateName, 
        string recipient, 
        Dictionary<string, object> variables, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前的SMTP配置 / Gets the current SMTP configuration
    /// 返回当前使用的SMTP服务器配置信息
    /// Returns currently used SMTP server configuration information
    /// </summary>
    SmtpConfig Configuration { get; }

    /// <summary>
    /// 更新SMTP配置 / Updates the SMTP configuration
    /// 设置新的SMTP服务器配置，立即生效并应用于后续的邮件发送操作
    /// Sets new SMTP server configuration, takes effect immediately and applies to subsequent email sending operations
    /// </summary>
    /// <param name="config">新的SMTP配置 / New SMTP configuration</param>
    /// <exception cref="ArgumentNullException">当配置参数为null时抛出 / Thrown when config is null</exception>
    void UpdateConfiguration(SmtpConfig config);

    /// <summary>
    /// 获取关于通知传递的统计信息 / Gets statistics about notification delivery
    /// 提供指定时间范围内的邮件发送统计，包括成功率、失败率等指标
    /// Provides email sending statistics within specified time range including success rate, failure rate and other metrics
    /// </summary>
    /// <param name="fromDate">统计的开始日期，可选 / Start date for statistics (optional)</param>
    /// <param name="toDate">统计的结束日期，可选 / End date for statistics (optional)</param>
    /// <param name="cancellationToken">操作的取消令牌 / Cancellation token for the operation</param>
    /// <returns>通知传递统计信息 / Notification delivery statistics</returns>
    Task<NotificationStatistics> GetStatisticsAsync(
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        CancellationToken cancellationToken = default);
}