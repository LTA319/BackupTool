using Microsoft.Extensions.Logging;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Application-specific logging service interface
/// </summary>
public interface ILoggingService
{
    /// <summary>
    /// Logs an information message
    /// </summary>
    void LogInformation(string message, params object[] args);

    /// <summary>
    /// Logs a debug message
    /// </summary>
    void LogDebug(string message, params object[] args);

    /// <summary>
    /// Logs a warning message
    /// </summary>
    void LogWarning(string message, params object[] args);

    /// <summary>
    /// Logs an error message
    /// </summary>
    void LogError(string message, params object[] args);

    /// <summary>
    /// Logs an error message with exception
    /// </summary>
    void LogError(Exception exception, string message, params object[] args);

    /// <summary>
    /// Logs a critical message
    /// </summary>
    void LogCritical(string message, params object[] args);

    /// <summary>
    /// Logs a critical message with exception
    /// </summary>
    void LogCritical(Exception exception, string message, params object[] args);

    /// <summary>
    /// Checks if a log level is enabled
    /// </summary>
    bool IsEnabled(LogLevel logLevel);

    /// <summary>
    /// Creates a logging scope
    /// </summary>
    IDisposable BeginScope<TState>(TState state) where TState : notnull;

    /// <summary>
    /// Logs a structured message with event ID
    /// </summary>
    void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter);

    /// <summary>
    /// Logs backup operation start
    /// </summary>
    void LogBackupStart(int configurationId, string? resumeToken = null);

    /// <summary>
    /// Logs backup operation completion
    /// </summary>
    void LogBackupComplete(int backupLogId, bool success, string? filePath = null, long? fileSize = null, string? errorMessage = null);

    /// <summary>
    /// Logs file transfer progress
    /// </summary>
    void LogTransferProgress(int backupLogId, int chunkIndex, long chunkSize, double progressPercentage);

    /// <summary>
    /// Logs MySQL service operation
    /// </summary>
    void LogMySqlOperation(string operation, string serviceName, bool success, string? errorMessage = null);

    /// <summary>
    /// Logs compression operation
    /// </summary>
    void LogCompressionOperation(string inputPath, string outputPath, long originalSize, long compressedSize, TimeSpan duration);

    /// <summary>
    /// Logs network operation
    /// </summary>
    void LogNetworkOperation(string operation, string endpoint, bool success, string? errorMessage = null);
}