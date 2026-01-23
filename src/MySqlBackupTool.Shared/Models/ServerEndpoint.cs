using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.NetworkInformation;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// Server endpoint configuration
/// </summary>
public class ServerEndpoint : IValidatableObject
{
    [Required(ErrorMessage = "IP Address is required")]
    [StringLength(45, ErrorMessage = "IP Address must be no more than 45 characters")] // IPv6 addresses can be up to 45 characters
    public string IPAddress { get; set; } = string.Empty;

    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    public int Port { get; set; } = 8080;

    public bool UseSSL { get; set; } = true;

    /// <summary>
    /// Validates that the IP address is in a valid format
    /// </summary>
    public bool IsValidIPAddress()
    {
        return System.Net.IPAddress.TryParse(IPAddress, out _);
    }

    /// <summary>
    /// Gets the full endpoint address
    /// </summary>
    public string GetEndpointAddress()
    {
        var protocol = UseSSL ? "https" : "http";
        return $"{protocol}://{IPAddress}:{Port}";
    }

    /// <summary>
    /// Performs custom validation logic for the server endpoint
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();

        // Validate IP address format
        if (!string.IsNullOrWhiteSpace(IPAddress))
        {
            if (!IsValidIPAddress())
            {
                results.Add(new ValidationResult(
                    $"'{IPAddress}' is not a valid IP address format",
                    new[] { nameof(IPAddress) }));
            }
            else
            {
                // Additional validation for IP address types
                if (System.Net.IPAddress.TryParse(IPAddress, out var parsedIP))
                {
                    // Check for reserved/invalid IP ranges
                    if (parsedIP.Equals(System.Net.IPAddress.Any) || parsedIP.Equals(System.Net.IPAddress.IPv6Any))
                    {
                        results.Add(new ValidationResult(
                            "Cannot use wildcard IP address (0.0.0.0 or ::) as target endpoint",
                            new[] { nameof(IPAddress) }));
                    }
                }
            }
        }

        // Validate port ranges for common reserved ports
        if (Port > 0 && Port < 1024)
        {
            results.Add(new ValidationResult(
                $"Port {Port} is in the reserved range (1-1023). Consider using a port above 1024.",
                new[] { nameof(Port) }));
        }

        return results;
    }

    /// <summary>
    /// Validates the endpoint by checking connectivity and accessibility
    /// </summary>
    /// <returns>Tuple indicating if endpoint is valid and any error messages</returns>
    public (bool IsValid, List<string> Errors) ValidateEndpoint()
    {
        var errors = new List<string>();

        // Validate IP address format
        if (!IsValidIPAddress())
        {
            errors.Add($"Invalid IP address format: {IPAddress}");
            return (false, errors);
        }

        // Validate port range
        if (Port < 1 || Port > 65535)
        {
            errors.Add($"Port {Port} is outside valid range (1-65535)");
            return (false, errors);
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// Tests connectivity to the endpoint
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds for the connectivity test</param>
    /// <returns>True if endpoint is reachable, false otherwise</returns>
    public async Task<bool> TestConnectivityAsync(int timeoutMs = 5000)
    {
        try
        {
            if (!IsValidIPAddress())
                return false;

            using var ping = new Ping();
            var reply = await ping.SendPingAsync(IPAddress, timeoutMs);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tests if the specific port is accessible on the endpoint
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds for the port test</param>
    /// <returns>True if port is accessible, false otherwise</returns>
    public async Task<bool> TestPortAccessibilityAsync(int timeoutMs = 5000)
    {
        try
        {
            if (!IsValidIPAddress())
                return false;

            using var tcpClient = new System.Net.Sockets.TcpClient();
            var connectTask = tcpClient.ConnectAsync(IPAddress, Port);
            var timeoutTask = Task.Delay(timeoutMs);
            
            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            
            if (completedTask == connectTask && tcpClient.Connected)
            {
                return true;
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }
}