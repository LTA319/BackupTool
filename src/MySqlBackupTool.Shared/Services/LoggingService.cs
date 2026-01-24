using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Application-specific logging service implementation that provides structured logging
/// with backup-specific methods for common operations
/// </summary>
public class LoggingService : ILoggingService
{
    private readonly ILogger<LoggingService> _logger;

    public LoggingService(ILogger<LoggingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void LogInformation(string message, params object[] args)
    {
        _logger.LogInformation(message, args);
    }

    public void LogDebug(string message, params object[] args)
    {
        _logger.LogDebug(message, args);
    }

    public void LogWarning(string message, params object[] args)
    {
        _logger.LogWarning(message, args);
    }

    public void LogError(string message, params object[] args)
    {
        _logger.LogError(message, args);
    }

    public void LogError(Exception exception, string message, params object[] args)
    {
        _logger.LogError(exception, message, args);
    }

    public void LogCritical(string message, params object[] args)
    {
        _logger.LogCritical(message, args);
    }

    public void LogCritical(Exception exception, string message, params object[] args)
    {
        _logger.LogCritical(exception, message, args);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _logger.IsEnabled(logLevel);
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return _logger.BeginScope(state) ?? throw new InvalidOperationException("Failed to create logging scope");
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _logger.Log(logLevel, eventId, state, exception, formatter);
    }

    /// <summary>
    /// Logs the start of a backup operation
    /// </summary>
    /// <param name="configurationId">The ID of the backup configuration being executed</param>
    /// <param name="resumeToken">Optional resume token if this is a resumed backup operation</param>
    public void LogBackupStart(int configurationId, string? resumeToken = null)
    {
        if (string.IsNullOrEmpty(resumeToken))
        {
            _logger.LogInformation("Starting backup operation for configuration {ConfigurationId}", configurationId);
        }
        else
        {
            _logger.LogInformation("Resuming backup operation for configuration {ConfigurationId} with token {ResumeToken}", 
                configurationId, resumeToken);
        }
    }

    /// <summary>
    /// Logs the completion of a backup operation
    /// </summary>
    /// <param name="backupLogId">The ID of the backup log entry</param>
    /// <param name="success">Whether the backup completed successfully</param>
    /// <param name="filePath">Path to the backup file (if successful)</param>
    /// <param name="fileSize">Size of the backup file in bytes (if successful)</param>
    /// <param name="errorMessage">Error message (if failed)</param>
    public void LogBackupComplete(int backupLogId, bool success, string? filePath = null, long? fileSize = null, string? errorMessage = null)
    {
        if (success)
        {
            _logger.LogInformation("Backup {BackupLogId} completed successfully. File: {FilePath}, Size: {FileSize} bytes", 
                backupLogId, filePath, fileSize);
        }
        else
        {
            _logger.LogError("Backup {BackupLogId} failed: {ErrorMessage}", backupLogId, errorMessage);
        }
    }

    public void LogTransferProgress(int backupLogId, int chunkIndex, long chunkSize, double progressPercentage)
    {
        _logger.LogDebug("Transfer progress for backup {BackupLogId}: chunk {ChunkIndex} ({ChunkSize} bytes) - {ProgressPercentage:F1}% complete", 
            backupLogId, chunkIndex, chunkSize, progressPercentage);
    }

    public void LogMySqlOperation(string operation, string serviceName, bool success, string? errorMessage = null)
    {
        if (success)
        {
            _logger.LogInformation("MySQL operation '{Operation}' completed successfully for service '{ServiceName}'", 
                operation, serviceName);
        }
        else
        {
            _logger.LogError("MySQL operation '{Operation}' failed for service '{ServiceName}': {ErrorMessage}", 
                operation, serviceName, errorMessage);
        }
    }

    public void LogCompressionOperation(string inputPath, string outputPath, long originalSize, long compressedSize, TimeSpan duration)
    {
        var compressionRatio = originalSize > 0 ? (double)compressedSize / originalSize : 0;
        var compressionPercentage = (1 - compressionRatio) * 100;

        _logger.LogInformation("Compression completed: {InputPath} -> {OutputPath}. " +
            "Original: {OriginalSize} bytes, Compressed: {CompressedSize} bytes " +
            "({CompressionPercentage:F1}% reduction), Duration: {Duration:F2}s",
            inputPath, outputPath, originalSize, compressedSize, compressionPercentage, duration.TotalSeconds);
    }

    public void LogNetworkOperation(string operation, string endpoint, bool success, string? errorMessage = null)
    {
        if (success)
        {
            _logger.LogInformation("Network operation '{Operation}' completed successfully for endpoint '{Endpoint}'", 
                operation, endpoint);
        }
        else
        {
            _logger.LogError("Network operation '{Operation}' failed for endpoint '{Endpoint}': {ErrorMessage}", 
                operation, endpoint, errorMessage);
        }
    }
}