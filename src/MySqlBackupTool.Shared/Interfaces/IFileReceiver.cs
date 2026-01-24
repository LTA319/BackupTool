using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Interface for server-side file reception operations
/// </summary>
public interface IFileReceiver
{
    /// <summary>
    /// Starts listening for incoming file transfer connections
    /// </summary>
    /// <param name="port">Port number to listen on</param>
    Task StartListeningAsync(int port);

    /// <summary>
    /// Stops listening for incoming connections
    /// </summary>
    Task StopListeningAsync();

    /// <summary>
    /// Receives a file from a client
    /// </summary>
    /// <param name="request">File reception request details</param>
    /// <returns>Result of the file reception operation</returns>
    Task<ReceiveResult> ReceiveFileAsync(ReceiveRequest request);
}

