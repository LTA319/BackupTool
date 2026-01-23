using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;

namespace MySqlBackupTool.Server;

/// <summary>
/// Background service that runs the file receiver server
/// </summary>
public class FileReceiverService : BackgroundService
{
    private readonly ILogger<FileReceiverService> _logger;
    private readonly IFileReceiver _fileReceiver;
    private readonly int _port;

    public FileReceiverService(
        ILogger<FileReceiverService> logger,
        IFileReceiver fileReceiver,
        int port = 8080)
    {
        _logger = logger;
        _fileReceiver = fileReceiver;
        _port = port;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("File Receiver Service starting on port {Port}", _port);

        try
        {
            await _fileReceiver.StartListeningAsync(_port);
            
            // Keep the service running until cancellation is requested
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("File Receiver Service stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in File Receiver Service");
            throw;
        }
        finally
        {
            try
            {
                await _fileReceiver.StopListeningAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping file receiver");
            }
            
            _logger.LogInformation("File Receiver Service stopped");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("File Receiver Service stop requested");
        
        try
        {
            await _fileReceiver.StopListeningAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping file receiver during service shutdown");
        }
        
        await base.StopAsync(cancellationToken);
    }
}