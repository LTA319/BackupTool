using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MySqlBackupTool.Shared.Logging;

/// <summary>
/// Extension methods for configuring logging
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Adds backup tool logging configuration
    /// </summary>
    public static IServiceCollection AddBackupToolLogging(this IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddDebug();
            
            // Add file logging
            builder.AddProvider(new FileLoggerProvider("logs"));
            
            // Set minimum log levels
            builder.SetMinimumLevel(LogLevel.Information);
            
            // Configure specific log levels for different categories
            builder.AddFilter("MySqlBackupTool", LogLevel.Debug);
            builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
            builder.AddFilter("System.Net.Http", LogLevel.Warning);
        });

        return services;
    }
}

/// <summary>
/// File logger provider for writing logs to files
/// </summary>
public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;
    private readonly Dictionary<string, FileLogger> _loggers = new();

    public FileLoggerProvider(string logDirectory)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(_logDirectory);
    }

    public ILogger CreateLogger(string categoryName)
    {
        if (!_loggers.TryGetValue(categoryName, out var logger))
        {
            logger = new FileLogger(categoryName, _logDirectory);
            _loggers[categoryName] = logger;
        }
        return logger;
    }

    public void Dispose()
    {
        foreach (var logger in _loggers.Values)
        {
            logger.Dispose();
        }
        _loggers.Clear();
    }
}

/// <summary>
/// File logger implementation
/// </summary>
public class FileLogger : ILogger, IDisposable
{
    private readonly string _categoryName;
    private readonly string _logFilePath;
    private readonly object _lock = new();
    private bool _disposed = false;

    public FileLogger(string categoryName, string logDirectory)
    {
        _categoryName = categoryName;
        var safeFileName = string.Join("_", categoryName.Split(Path.GetInvalidFileNameChars()));
        var dateString = DateTime.Now.ToString("yyyyMMdd");
        _logFilePath = Path.Combine(logDirectory, $"{safeFileName}_{dateString}.log");
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel) || _disposed)
            return;

        var message = formatter(state, exception);
        var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel}] [{_categoryName}] {message}";
        
        if (exception != null)
        {
            logEntry += Environment.NewLine + exception.ToString();
        }

        lock (_lock)
        {
            try
            {
                File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
            }
            catch
            {
                // Ignore file write errors to prevent logging from breaking the application
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}