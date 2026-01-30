using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;

namespace MySqlBackupTool.Server;

/// <summary>
/// 文件接收服务后台服务类
/// 作为托管服务运行，负责启动和管理文件接收服务器
/// 处理来自客户端的备份文件传输请求
/// </summary>
public class FileReceiverService : BackgroundService
{
    #region 私有字段

    /// <summary>
    /// 日志记录器，用于记录服务运行状态和错误信息
    /// </summary>
    private readonly ILogger<FileReceiverService> _logger;
    
    /// <summary>
    /// 文件接收器接口，负责实际的文件接收逻辑
    /// </summary>
    private readonly IFileReceiver _fileReceiver;
    
    /// <summary>
    /// 服务器监听端口号，默认为8080
    /// </summary>
    private readonly int _port;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化文件接收服务实例
    /// </summary>
    /// <param name="logger">日志记录器实例</param>
    /// <param name="fileReceiver">文件接收器实例</param>
    /// <param name="port">服务器监听端口，默认为8080</param>
    public FileReceiverService(
        ILogger<FileReceiverService> logger,
        IFileReceiver fileReceiver,
        int port = 8080)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fileReceiver = fileReceiver ?? throw new ArgumentNullException(nameof(fileReceiver));
        _port = port;
    }

    #endregion

    #region 受保护的方法

    /// <summary>
    /// 执行后台服务的主要逻辑
    /// 启动文件接收器并保持服务运行直到收到取消请求
    /// </summary>
    /// <param name="stoppingToken">取消令牌，用于优雅地停止服务</param>
    /// <returns>异步任务</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("File Receiver Service starting on port {Port}", _port);

        try
        {
            // 启动文件接收器，开始监听指定端口
            await _fileReceiver.StartListeningAsync(_port);
            
            // 保持服务运行直到收到取消请求
            // 每秒检查一次取消令牌状态
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常的取消操作，记录信息日志
            _logger.LogInformation("File Receiver Service stopping due to cancellation");
        }
        catch (Exception ex)
        {
            // 记录服务运行过程中的错误并重新抛出异常
            _logger.LogError(ex, "Error in File Receiver Service");
            throw;
        }
        finally
        {
            // 确保在服务停止时清理资源
            try
            {
                await _fileReceiver.StopListeningAsync();
            }
            catch (Exception ex)
            {
                // 记录停止文件接收器时的警告，但不阻止服务关闭
                _logger.LogWarning(ex, "Error stopping file receiver");
            }
            
            _logger.LogInformation("File Receiver Service stopped");
        }
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 停止后台服务
    /// 重写基类方法以提供自定义的停止逻辑
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("File Receiver Service stop requested");
        
        try
        {
            // 停止文件接收器
            await _fileReceiver.StopListeningAsync();
        }
        catch (Exception ex)
        {
            // 记录停止过程中的警告，但继续执行基类的停止逻辑
            _logger.LogWarning(ex, "Error stopping file receiver during service shutdown");
        }
        
        // 调用基类的停止方法
        await base.StopAsync(cancellationToken);
    }

    #endregion
}