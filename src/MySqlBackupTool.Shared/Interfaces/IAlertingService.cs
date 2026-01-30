using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 发送关键错误警报和通知的接口
/// Interface for sending critical error alerts and notifications
/// </summary>
public interface IAlertingService
{
    /// <summary>
    /// 通过所有配置的渠道发送关键错误警报
    /// Sends a critical error alert through all configured channels
    /// </summary>
    /// <param name="alert">要发送的警报 / The alert to send</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>如果警报至少通过一个渠道成功发送则返回true / True if alert was sent successfully through at least one channel</returns>
    Task<bool> SendCriticalErrorAlertAsync(CriticalErrorAlert alert, CancellationToken cancellationToken = default);

    /// <summary>
    /// 通过指定渠道发送通知
    /// Sends a notification through specified channels
    /// </summary>
    /// <param name="notification">要发送的通知 / The notification to send</param>
    /// <param name="channels">要使用的特定渠道（如果为null，则使用所有配置的渠道）/ Specific channels to use (if null, uses all configured channels)</param>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>通知尝试的结果 / Result of the notification attempt</returns>
    Task<NotificationResult> SendNotificationAsync(
        Notification notification, 
        IEnumerable<NotificationChannel>? channels = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 测试所有配置的通知渠道的连接性
    /// Tests connectivity to all configured notification channels
    /// </summary>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>每个渠道的测试结果 / Test results for each channel</returns>
    Task<Dictionary<NotificationChannel, bool>> TestNotificationChannelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前的警报配置
    /// Gets the current alerting configuration
    /// </summary>
    AlertingConfig Configuration { get; }

    /// <summary>
    /// 更新警报配置
    /// Updates the alerting configuration
    /// </summary>
    /// <param name="config">新配置 / New configuration</param>
    void UpdateConfiguration(AlertingConfig config);
}