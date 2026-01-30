using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MySqlBackupTool.Shared.Logging;

/// <summary>
/// 日志记录配置扩展方法类
/// 为MySQL备份工具提供统一的日志记录配置和自定义日志提供程序
/// </summary>
/// <remarks>
/// 该类提供以下功能：
/// 1. 统一的日志记录配置
/// 2. 多种日志输出目标（控制台、调试、文件）
/// 3. 分级日志记录控制
/// 4. 自定义文件日志提供程序
/// </remarks>
public static class LoggingExtensions
{
    /// <summary>
    /// 为依赖注入容器添加MySQL备份工具的日志记录配置
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>配置后的服务集合，支持链式调用</returns>
    /// <remarks>
    /// 该方法配置以下日志记录功能：
    /// 1. 控制台日志输出 - 用于开发和调试
    /// 2. 调试输出日志 - 用于Visual Studio调试窗口
    /// 3. 文件日志输出 - 用于生产环境日志持久化
    /// 4. 分级日志控制 - 不同组件使用不同的日志级别
    /// 
    /// 日志级别配置：
    /// - 默认级别：Information
    /// - MySqlBackupTool组件：Debug级别（详细调试信息）
    /// - Entity Framework：Warning级别（减少数据库查询日志）
    /// - HTTP客户端：Warning级别（减少网络请求日志）
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddBackupToolLogging();
    /// 
    /// // 在类中使用日志记录
    /// public class MyService
    /// {
    ///     private readonly ILogger&lt;MyService&gt; _logger;
    ///     
    ///     public MyService(ILogger&lt;MyService&gt; logger)
    ///     {
    ///         _logger = logger;
    ///     }
    ///     
    ///     public void DoWork()
    ///     {
    ///         _logger.LogInformation("开始执行工作");
    ///         // ... 工作逻辑
    ///         _logger.LogInformation("工作执行完成");
    ///     }
    /// }
    /// </code>
    /// </example>
    public static IServiceCollection AddBackupToolLogging(this IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            // 添加控制台日志输出
            // 在开发环境和容器环境中显示日志信息
            builder.AddConsole();
            
            // 添加调试输出日志
            // 在Visual Studio调试时在输出窗口显示日志
            builder.AddDebug();
            
            // 添加自定义文件日志提供程序
            // 将日志持久化到文件系统，便于生产环境问题排查
            builder.AddProvider(new FileLoggerProvider("logs"));
            
            // 设置全局最低日志级别
            // Information级别包含重要的业务操作信息
            builder.SetMinimumLevel(LogLevel.Information);
            
            // 为不同的命名空间配置特定的日志级别
            
            // MySQL备份工具相关组件使用Debug级别
            // 记录详细的操作步骤和状态信息，便于问题诊断
            builder.AddFilter("MySqlBackupTool", LogLevel.Debug);
            
            // Entity Framework Core使用Warning级别
            // 减少数据库查询和连接的详细日志，只记录警告和错误
            builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
            
            // HTTP客户端使用Warning级别
            // 减少HTTP请求的详细日志，只记录重要的网络问题
            builder.AddFilter("System.Net.Http", LogLevel.Warning);
        });

        return services;
    }
}

/// <summary>
/// 自定义文件日志提供程序
/// 负责创建和管理文件日志记录器实例，将日志信息写入到文件系统
/// </summary>
/// <remarks>
/// 该提供程序的特性：
/// 1. 支持多个日志类别，每个类别对应一个日志文件
/// 2. 自动创建日志目录
/// 3. 按日期分割日志文件
/// 4. 线程安全的日志写入
/// 5. 自动处理文件名中的非法字符
/// 
/// 日志文件命名规则：{类别名}_{日期}.log
/// 例如：MySqlBackupTool.Services.BackupService_20240130.log
/// </remarks>
public class FileLoggerProvider : ILoggerProvider
{
    #region 私有字段

    /// <summary>
    /// 日志文件存储目录路径
    /// </summary>
    private readonly string _logDirectory;
    
    /// <summary>
    /// 日志记录器实例缓存
    /// 键为类别名称，值为对应的文件日志记录器
    /// </summary>
    private readonly Dictionary<string, FileLogger> _loggers = new();

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化FileLoggerProvider类的新实例
    /// </summary>
    /// <param name="logDirectory">日志文件存储目录路径</param>
    /// <remarks>
    /// 如果指定的目录不存在，构造函数会自动创建该目录
    /// </remarks>
    public FileLoggerProvider(string logDirectory)
    {
        _logDirectory = logDirectory;
        
        // 确保日志目录存在
        Directory.CreateDirectory(_logDirectory);
    }

    #endregion

    #region ILoggerProvider实现

    /// <summary>
    /// 为指定的类别名称创建或获取日志记录器实例
    /// </summary>
    /// <param name="categoryName">日志类别名称，通常是类的完全限定名</param>
    /// <returns>对应类别的文件日志记录器实例</returns>
    /// <remarks>
    /// 该方法使用缓存机制，同一个类别名称只会创建一个日志记录器实例
    /// 这样可以避免多个相同类别的日志记录器同时写入同一个文件
    /// </remarks>
    public ILogger CreateLogger(string categoryName)
    {
        // 检查缓存中是否已存在该类别的日志记录器
        if (!_loggers.TryGetValue(categoryName, out var logger))
        {
            // 创建新的文件日志记录器实例
            logger = new FileLogger(categoryName, _logDirectory);
            _loggers[categoryName] = logger;
        }
        return logger;
    }

    /// <summary>
    /// 释放所有日志记录器资源
    /// </summary>
    /// <remarks>
    /// 该方法会关闭所有打开的日志文件并清理缓存
    /// 通常在应用程序关闭时由依赖注入容器自动调用
    /// </remarks>
    public void Dispose()
    {
        // 释放所有日志记录器实例
        foreach (var logger in _loggers.Values)
        {
            logger.Dispose();
        }
        
        // 清空缓存
        _loggers.Clear();
    }

    #endregion
}

/// <summary>
/// 文件日志记录器实现
/// 将日志消息写入到指定的日志文件中，支持多线程安全写入
/// </summary>
/// <remarks>
/// 该日志记录器的特性：
/// 1. 线程安全的文件写入操作
/// 2. 自动处理文件名中的非法字符
/// 3. 按日期分割日志文件
/// 4. 结构化的日志格式
/// 5. 异常信息的完整记录
/// 6. 静默处理文件写入错误，避免影响应用程序运行
/// 
/// 日志格式：[时间戳] [级别] [类别] 消息内容
/// 例如：[2024-01-30 14:30:22.123] [Information] [MySqlBackupTool.Services.BackupService] 开始执行备份操作
/// </remarks>
public class FileLogger : ILogger, IDisposable
{
    #region 私有字段

    /// <summary>
    /// 日志类别名称，通常是类的完全限定名
    /// </summary>
    private readonly string _categoryName;
    
    /// <summary>
    /// 日志文件的完整路径
    /// </summary>
    private readonly string _logFilePath;
    
    /// <summary>
    /// 线程同步锁对象，确保多线程环境下的文件写入安全
    /// </summary>
    private readonly object _lock = new();
    
    /// <summary>
    /// 标识对象是否已被释放
    /// </summary>
    private bool _disposed = false;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化FileLogger类的新实例
    /// </summary>
    /// <param name="categoryName">日志类别名称</param>
    /// <param name="logDirectory">日志文件存储目录</param>
    /// <remarks>
    /// 构造函数会根据类别名称和当前日期生成日志文件路径
    /// 文件名格式：{安全的类别名}_{日期}.log
    /// </remarks>
    public FileLogger(string categoryName, string logDirectory)
    {
        _categoryName = categoryName;
        
        // 处理类别名称中的非法文件名字符
        // 将路径分隔符和其他非法字符替换为下划线
        var safeFileName = string.Join("_", categoryName.Split(Path.GetInvalidFileNameChars()));
        
        // 生成基于当前日期的文件名
        var dateString = DateTime.Now.ToString("yyyyMMdd");
        
        // 构建完整的日志文件路径
        _logFilePath = Path.Combine(logDirectory, $"{safeFileName}_{dateString}.log");
    }

    #endregion

    #region ILogger实现

    /// <summary>
    /// 开始日志作用域（当前实现返回空作用域）
    /// </summary>
    /// <typeparam name="TState">状态类型</typeparam>
    /// <param name="state">作用域状态</param>
    /// <returns>空作用域实例</returns>
    /// <remarks>
    /// 日志作用域用于在一系列相关的日志条目中添加上下文信息
    /// 当前实现返回空作用域，不添加额外的上下文信息
    /// </remarks>
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    /// <summary>
    /// 检查指定的日志级别是否启用
    /// </summary>
    /// <param name="logLevel">要检查的日志级别</param>
    /// <returns>如果日志级别启用返回true，否则返回false</returns>
    /// <remarks>
    /// 当前实现只记录Information级别及以上的日志
    /// 这样可以过滤掉过于详细的Debug和Trace级别日志
    /// </remarks>
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    /// <summary>
    /// 记录日志消息到文件
    /// </summary>
    /// <typeparam name="TState">状态类型</typeparam>
    /// <param name="logLevel">日志级别</param>
    /// <param name="eventId">事件ID</param>
    /// <param name="state">日志状态</param>
    /// <param name="exception">异常信息（可选）</param>
    /// <param name="formatter">消息格式化函数</param>
    /// <remarks>
    /// 该方法执行以下操作：
    /// 1. 检查日志级别是否启用
    /// 2. 格式化日志消息
    /// 3. 构建完整的日志条目
    /// 4. 线程安全地写入文件
    /// 5. 静默处理文件写入错误
    /// 
    /// 日志条目格式：
    /// [时间戳] [级别] [类别] 消息内容
    /// 异常信息（如果存在）
    /// </remarks>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // 检查日志级别和对象状态
        if (!IsEnabled(logLevel) || _disposed)
            return;

        // 使用格式化函数生成日志消息
        var message = formatter(state, exception);
        
        // 构建完整的日志条目
        // 格式：[时间戳] [级别] [类别] 消息
        var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel}] [{_categoryName}] {message}";
        
        // 如果存在异常信息，添加到日志条目中
        if (exception != null)
        {
            logEntry += Environment.NewLine + exception.ToString();
        }

        // 线程安全地写入日志文件
        lock (_lock)
        {
            try
            {
                // 追加写入日志文件
                File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
            }
            catch
            {
                // 静默忽略文件写入错误，防止日志记录影响应用程序正常运行
                // 可能的错误原因：
                // 1. 磁盘空间不足
                // 2. 文件被其他进程锁定
                // 3. 权限不足
                // 4. 网络驱动器不可用
            }
        }
    }

    #endregion

    #region IDisposable实现

    /// <summary>
    /// 释放文件日志记录器资源
    /// </summary>
    /// <remarks>
    /// 标记对象为已释放状态，停止后续的日志写入操作
    /// 由于使用File.AppendAllText方法，不需要显式关闭文件句柄
    /// </remarks>
    public void Dispose()
    {
        _disposed = true;
    }

    #endregion

    #region 内部类

    /// <summary>
    /// 空日志作用域实现
    /// 用于BeginScope方法的返回值，表示不添加任何上下文信息的作用域
    /// </summary>
    private class NullScope : IDisposable
    {
        /// <summary>
        /// 单例实例，避免重复创建对象
        /// </summary>
        public static NullScope Instance { get; } = new();
        
        /// <summary>
        /// 空的释放方法实现
        /// </summary>
        public void Dispose() { }
    }

    #endregion
}