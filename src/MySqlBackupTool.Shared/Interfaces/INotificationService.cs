using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Interface for email notification service providing SMTP-based email delivery
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a single email message asynchronously
    /// </summary>
    /// <param name="message">The email message to send</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>True if the email was sent successfully, false otherwise</returns>
    /// <exception cref="ArgumentNullException">Thrown when message is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when SMTP configuration is invalid</exception>
    Task<bool> SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends multiple email messages in bulk
    /// </summary>
    /// <param name="messages">Collection of email messages to send</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Dictionary mapping message IDs to their send success status</returns>
    /// <exception cref="ArgumentNullException">Thrown when messages collection is null</exception>
    Task<Dictionary<string, bool>> SendBulkEmailAsync(IEnumerable<EmailMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the delivery status of a specific notification
    /// </summary>
    /// <param name="notificationId">Unique identifier of the notification</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The current status of the notification, or null if not found</returns>
    /// <exception cref="ArgumentException">Thrown when notificationId is null or empty</exception>
    Task<NotificationStatus?> GetStatusAsync(string notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all available email templates
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Collection of available email templates</returns>
    Task<IEnumerable<EmailTemplate>> GetTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves email templates by category
    /// </summary>
    /// <param name="category">Template category to filter by</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Collection of email templates in the specified category</returns>
    Task<IEnumerable<EmailTemplate>> GetTemplatesByCategoryAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific email template by name
    /// </summary>
    /// <param name="templateName">Name of the template to retrieve</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The email template, or null if not found</returns>
    /// <exception cref="ArgumentException">Thrown when templateName is null or empty</exception>
    Task<EmailTemplate?> GetTemplateAsync(string templateName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests the SMTP connection with the provided configuration
    /// </summary>
    /// <param name="config">SMTP configuration to test</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>True if the connection test succeeds, false otherwise</returns>
    /// <exception cref="ArgumentNullException">Thrown when config is null</exception>
    Task<bool> TestSmtpConnectionAsync(SmtpConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an email message from a template with variable substitution
    /// </summary>
    /// <param name="templateName">Name of the template to use</param>
    /// <param name="recipient">Recipient email address</param>
    /// <param name="variables">Dictionary of variables to substitute in the template</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The created email message, or null if template not found</returns>
    /// <exception cref="ArgumentException">Thrown when templateName or recipient is null or empty</exception>
    Task<EmailMessage?> CreateEmailFromTemplateAsync(
        string templateName, 
        string recipient, 
        Dictionary<string, object> variables, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current SMTP configuration
    /// </summary>
    SmtpConfig Configuration { get; }

    /// <summary>
    /// Updates the SMTP configuration
    /// </summary>
    /// <param name="config">New SMTP configuration</param>
    /// <exception cref="ArgumentNullException">Thrown when config is null</exception>
    void UpdateConfiguration(SmtpConfig config);

    /// <summary>
    /// Gets statistics about notification delivery
    /// </summary>
    /// <param name="fromDate">Start date for statistics (optional)</param>
    /// <param name="toDate">End date for statistics (optional)</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Notification delivery statistics</returns>
    Task<NotificationStatistics> GetStatisticsAsync(
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        CancellationToken cancellationToken = default);
}