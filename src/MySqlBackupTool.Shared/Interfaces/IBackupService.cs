using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// Interface for backup service operations including MySQL instance management
/// This interface provides the same functionality as IMySQLManager but with a name
/// that aligns with test expectations for backup service operations.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Stops a MySQL service instance
    /// </summary>
    /// <param name="serviceName">Name of the MySQL service to stop</param>
    /// <returns>True if the service was successfully stopped, false otherwise</returns>
    Task<bool> StopInstanceAsync(string serviceName);

    /// <summary>
    /// Starts a MySQL service instance
    /// </summary>
    /// <param name="serviceName">Name of the MySQL service to start</param>
    /// <returns>True if the service was successfully started, false otherwise</returns>
    Task<bool> StartInstanceAsync(string serviceName);

    /// <summary>
    /// Verifies that a MySQL instance is available and accepting connections
    /// </summary>
    /// <param name="connection">Connection information for the MySQL instance</param>
    /// <returns>True if the instance is available and accepting connections, false otherwise</returns>
    Task<bool> VerifyInstanceAvailabilityAsync(MySQLConnectionInfo connection);

    /// <summary>
    /// Verifies that a MySQL instance is available and accepting connections with configurable timeout
    /// </summary>
    /// <param name="connection">Connection information for the MySQL instance</param>
    /// <param name="timeoutSeconds">Connection timeout in seconds</param>
    /// <returns>True if the instance is available and accepting connections, false otherwise</returns>
    Task<bool> VerifyInstanceAvailabilityAsync(MySQLConnectionInfo connection, int timeoutSeconds);
}