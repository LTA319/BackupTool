using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Collections.Concurrent;
using System.Text.Json;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 身份验证审计服务实现
/// 提供身份验证事件的审计日志记录、查询和管理功能
/// </summary>
public class AuthenticationAuditService : IAuthenticationAuditService, IDisposable
{
    private readonly ILogger<AuthenticationAuditService> _logger;
    private readonly string _auditLogPath;
    private readonly ConcurrentQueue<AuthenticationAuditLog> _pendingLogs = new();
    private readonly Timer _flushTimer;
    private readonly SemaphoreSlim _flushSemaphore = new(1, 1);
    private readonly object _lockObject = new();
    private bool _disposed = false;

    /// <summary>
    /// 初始化身份验证审计服务
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="auditLogPath">审计日志文件路径</param>
    public AuthenticationAuditService(ILogger<AuthenticationAuditService> logger, string? auditLogPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditLogPath = auditLogPath ?? Path.Combine("logs", "authentication_audit.log");
        
        // 确保审计日志目录存在
        var logDirectory = Path.GetDirectoryName(_auditLogPath);
        if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        // 设置定时器，每30秒刷新一次待处理的日志
        _flushTimer = new Timer(FlushPendingLogs, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        
        _logger.LogInformation("Authentication audit service initialized with log path: {LogPath}", _auditLogPath);
    }

    /// <summary>
    /// 记录身份验证审计日志
    /// </summary>
    /// <param name="auditLog">审计日志条目</param>
    /// <returns>异步任务</returns>
    public async Task LogAuthenticationEventAsync(AuthenticationAuditLog auditLog)
    {
        if (auditLog == null)
        {
            _logger.LogWarning("Attempted to log null authentication audit event");
            return;
        }

        try
        {
            // 添加到待处理队列以进行批量写入
            _pendingLogs.Enqueue(auditLog);

            // 记录结构化日志用于实时监控
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["AuditId"] = auditLog.Id,
                ["ClientId"] = auditLog.ClientId ?? "unknown",
                ["Operation"] = auditLog.Operation.ToString(),
                ["Outcome"] = auditLog.Outcome.ToString(),
                ["DurationMs"] = auditLog.DurationMs,
                ["ClientIP"] = auditLog.ClientIPAddress ?? "unknown"
            });

            if (auditLog.Outcome == AuthenticationOutcome.Success)
            {
                _logger.LogInformation("Authentication audit: {Operation} succeeded for client {ClientId} in {Duration}ms",
                    auditLog.Operation, auditLog.ClientId ?? "unknown", auditLog.DurationMs);
            }
            else
            {
                _logger.LogWarning("Authentication audit: {Operation} failed for client {ClientId} - {ErrorCode}: {ErrorMessage} (Duration: {Duration}ms)",
                    auditLog.Operation, auditLog.ClientId ?? "unknown", auditLog.ErrorCode, auditLog.ErrorMessage, auditLog.DurationMs);
            }

            // 如果队列中有太多待处理的日志，立即刷新
            if (_pendingLogs.Count >= 100)
            {
                _ = Task.Run(async () => await FlushPendingLogsAsync());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging authentication audit event for operation {Operation}", auditLog.Operation);
        }
    }

    /// <summary>
    /// 获取指定时间范围内的审计日志
    /// </summary>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="clientId">可选的客户端ID过滤</param>
    /// <returns>审计日志列表</returns>
    public async Task<List<AuthenticationAuditLog>> GetAuditLogsAsync(DateTime startTime, DateTime endTime, string? clientId = null)
    {
        var auditLogs = new List<AuthenticationAuditLog>();

        try
        {
            // 首先刷新待处理的日志
            await FlushPendingLogsAsync();

            if (!File.Exists(_auditLogPath))
            {
                _logger.LogInformation("Audit log file does not exist: {LogPath}", _auditLogPath);
                return auditLogs;
            }

            var lines = await File.ReadAllLinesAsync(_auditLogPath);
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var auditLog = JsonSerializer.Deserialize<AuthenticationAuditLog>(line);
                    if (auditLog == null)
                        continue;

                    // 应用时间范围过滤
                    if (auditLog.Timestamp < startTime || auditLog.Timestamp > endTime)
                        continue;

                    // 应用客户端ID过滤（如果指定）
                    if (!string.IsNullOrEmpty(clientId) && 
                        !string.Equals(auditLog.ClientId, clientId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    auditLogs.Add(auditLog);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize audit log line: {Line}", line);
                }
            }

            _logger.LogDebug("Retrieved {Count} audit logs for time range {StartTime} to {EndTime}", 
                auditLogs.Count, startTime, endTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs from {LogPath}", _auditLogPath);
        }

        return auditLogs.OrderBy(log => log.Timestamp).ToList();
    }

    /// <summary>
    /// 清理过期的审计日志
    /// </summary>
    /// <param name="retentionDays">保留天数</param>
    /// <returns>清理的日志条目数量</returns>
    public async Task<int> CleanupExpiredLogsAsync(int retentionDays)
    {
        if (retentionDays <= 0)
        {
            _logger.LogWarning("Invalid retention days: {RetentionDays}. Must be greater than 0", retentionDays);
            return 0;
        }

        var cleanedCount = 0;

        try
        {
            // 首先刷新待处理的日志
            await FlushPendingLogsAsync();

            if (!File.Exists(_auditLogPath))
            {
                _logger.LogInformation("Audit log file does not exist: {LogPath}", _auditLogPath);
                return 0;
            }

            var cutoffDate = DateTime.Now.AddDays(-retentionDays);
            var lines = await File.ReadAllLinesAsync(_auditLogPath);
            var retainedLines = new List<string>();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var auditLog = JsonSerializer.Deserialize<AuthenticationAuditLog>(line);
                    if (auditLog == null)
                        continue;

                    if (auditLog.Timestamp >= cutoffDate)
                    {
                        retainedLines.Add(line);
                    }
                    else
                    {
                        cleanedCount++;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize audit log line during cleanup: {Line}", line);
                    // 保留无法解析的行以避免数据丢失
                    retainedLines.Add(line);
                }
            }

            // 如果有日志被清理，重写文件
            if (cleanedCount > 0)
            {
                await File.WriteAllLinesAsync(_auditLogPath, retainedLines);
                _logger.LogInformation("Cleaned up {CleanedCount} expired audit log entries older than {CutoffDate}", 
                    cleanedCount, cutoffDate);
            }
            else
            {
                _logger.LogDebug("No expired audit log entries found for cleanup");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired audit logs from {LogPath}", _auditLogPath);
        }

        return cleanedCount;
    }

    /// <summary>
    /// 定时器回调方法，刷新待处理的日志
    /// </summary>
    /// <param name="state">定时器状态</param>
    private void FlushPendingLogs(object? state)
    {
        _ = Task.Run(async () => await FlushPendingLogsAsync());
    }

    /// <summary>
    /// 异步刷新待处理的日志到文件
    /// </summary>
    /// <returns>异步任务</returns>
    private async Task FlushPendingLogsAsync()
    {
        if (_disposed || _pendingLogs.IsEmpty)
            return;

        await _flushSemaphore.WaitAsync();
        try
        {
            var logsToFlush = new List<AuthenticationAuditLog>();
            
            // 从队列中取出所有待处理的日志
            while (_pendingLogs.TryDequeue(out var log))
            {
                logsToFlush.Add(log);
            }

            if (logsToFlush.Count == 0)
                return;

            // 将日志序列化为JSON行并写入文件
            var jsonLines = logsToFlush.Select(log => JsonSerializer.Serialize(log));
            
            lock (_lockObject)
            {
                File.AppendAllLines(_auditLogPath, jsonLines);
            }

            _logger.LogDebug("Flushed {Count} authentication audit logs to {LogPath}", 
                logsToFlush.Count, _auditLogPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing pending audit logs to {LogPath}", _auditLogPath);
        }
        finally
        {
            _flushSemaphore.Release();
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            // 停止定时器
            _flushTimer?.Dispose();

            // 刷新所有待处理的日志
            FlushPendingLogsAsync().GetAwaiter().GetResult();

            // 释放信号量
            _flushSemaphore?.Dispose();

            _logger.LogInformation("Authentication audit service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing authentication audit service");
        }
    }
}