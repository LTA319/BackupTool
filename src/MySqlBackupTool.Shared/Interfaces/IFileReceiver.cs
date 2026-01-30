using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 服务器端文件接收操作的接口 / Interface for server-side file reception operations
/// 提供启动监听、停止监听和接收文件的功能，用于服务器端接收客户端传输的文件
/// Provides functionality for starting listening, stopping listening and receiving files, used for server-side reception of client-transmitted files
/// </summary>
public interface IFileReceiver
{
    /// <summary>
    /// 开始监听传入的文件传输连接 / Starts listening for incoming file transfer connections
    /// 在指定端口上启动TCP监听器，等待客户端的文件传输请求
    /// Starts TCP listener on specified port, waiting for client file transfer requests
    /// </summary>
    /// <param name="port">要监听的端口号 / Port number to listen on</param>
    /// <exception cref="ArgumentException">当端口号无效时抛出 / Thrown when port number is invalid</exception>
    /// <exception cref="SocketException">当端口已被占用时抛出 / Thrown when port is already in use</exception>
    Task StartListeningAsync(int port);

    /// <summary>
    /// 停止监听传入的连接 / Stops listening for incoming connections
    /// 关闭TCP监听器，停止接受新的文件传输连接，但不影响正在进行的传输
    /// Closes TCP listener, stops accepting new file transfer connections, but doesn't affect ongoing transfers
    /// </summary>
    Task StopListeningAsync();

    /// <summary>
    /// 从客户端接收文件 / Receives a file from a client
    /// 处理客户端的文件传输请求，接收文件数据并保存到指定位置
    /// Processes client file transfer request, receives file data and saves to specified location
    /// </summary>
    /// <param name="request">文件接收请求的详细信息 / File reception request details</param>
    /// <returns>文件接收操作的结果 / Result of the file reception operation</returns>
    /// <exception cref="ArgumentNullException">当请求参数为null时抛出 / Thrown when request parameter is null</exception>
    /// <exception cref="UnauthorizedAccessException">当没有文件写入权限时抛出 / Thrown when file write access is denied</exception>
    /// <exception cref="IOException">当文件IO操作失败时抛出 / Thrown when file IO operation fails</exception>
    Task<ReceiveResult> ReceiveFileAsync(ReceiveRequest request);
}

