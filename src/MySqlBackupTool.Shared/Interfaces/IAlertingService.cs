using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Interface for sending critical error alerts and notifications
/// </summary>
public interface IAlertingService
{
    /// <summary>
    /// Sends a critical error alert through all configured channels
    /// </summary>
    /// <param name="alert">The alert to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if alert was sent successfully through at least one channel</returns>
    Task<bool> SendCriticalErrorAlertAsync(CriticalErrorAlert alert, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification through specified channels
    /// </summary>
    /// <param name="notification">The notification to send</param>
    /// <param name="channels">Specific channels to use (if null, uses all configured channels)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the notification attempt</returns>
    Task<NotificationResult> SendNotificationAsync(
        Notification notification, 
        IEnumerable<NotificationChannel>? channels = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests connectivity to all configured notification channels
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Test results for each channel</returns>
    Task<Dictionary<NotificationChannel, bool>> TestNotificationChannelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current alerting configuration
    /// </summary>
    AlertingConfig Configuration { get; }

    /// <summary>
    /// Updates the alerting configuration
    /// </summary>
    /// <param name="config">New configuration</param>
    void UpdateConfiguration(AlertingConfig config);
}