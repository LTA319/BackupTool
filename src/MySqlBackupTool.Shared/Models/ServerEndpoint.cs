using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography.X509Certificates;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// Server endpoint configuration with SSL/TLS support
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
    /// Path to the SSL certificate file (.pfx or .p12)
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Password for the SSL certificate file
    /// </summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Certificate thumbprint for validation (optional)
    /// </summary>
    public string? CertificateThumbprint { get; set; }

    /// <summary>
    /// Whether to validate the server certificate (client-side)
    /// </summary>
    public bool ValidateServerCertificate { get; set; } = true;

    /// <summary>
    /// Whether to allow self-signed certificates
    /// </summary>
    public bool AllowSelfSignedCertificates { get; set; } = false;

    /// <summary>
    /// Subject name expected in the server certificate
    /// </summary>
    public string? ExpectedCertificateSubject { get; set; }

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
    /// Loads the SSL certificate from the configured path
    /// </summary>
    /// <returns>X509Certificate2 instance or null if not configured</returns>
    public X509Certificate2? LoadCertificate()
    {
        if (!UseSSL || string.IsNullOrEmpty(CertificatePath))
            return null;

        try
        {
            if (!File.Exists(CertificatePath))
                throw new FileNotFoundException($"Certificate file not found: {CertificatePath}");

            return string.IsNullOrEmpty(CertificatePassword)
                ? new X509Certificate2(CertificatePath)
                : new X509Certificate2(CertificatePath, CertificatePassword);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load certificate from {CertificatePath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates the SSL certificate configuration
    /// </summary>
    /// <returns>True if certificate is valid, false otherwise</returns>
    public bool ValidateCertificate()
    {
        if (!UseSSL)
            return true; // No certificate needed for non-SSL

        try
        {
            using var cert = LoadCertificate();
            if (cert == null)
                return false;

            // Check if certificate is expired
            if (DateTime.Now < cert.NotBefore || DateTime.Now > cert.NotAfter)
                return false;

            // Validate thumbprint if specified
            if (!string.IsNullOrEmpty(CertificateThumbprint))
            {
                return string.Equals(cert.Thumbprint, CertificateThumbprint, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }
        catch
        {
            return false;
        }
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

        // Validate SSL configuration
        if (UseSSL)
        {
            if (string.IsNullOrEmpty(CertificatePath))
            {
                results.Add(new ValidationResult(
                    "Certificate path is required when SSL is enabled",
                    new[] { nameof(CertificatePath) }));
            }
            else if (!File.Exists(CertificatePath))
            {
                results.Add(new ValidationResult(
                    $"Certificate file not found: {CertificatePath}",
                    new[] { nameof(CertificatePath) }));
            }
            else
            {
                // Validate certificate can be loaded
                try
                {
                    using var cert = LoadCertificate();
                    if (cert == null)
                    {
                        results.Add(new ValidationResult(
                            "Failed to load SSL certificate",
                            new[] { nameof(CertificatePath) }));
                    }
                    else
                    {
                        // Check certificate validity period
                        if (DateTime.Now < cert.NotBefore)
                        {
                            results.Add(new ValidationResult(
                                $"Certificate is not yet valid (valid from {cert.NotBefore})",
                                new[] { nameof(CertificatePath) }));
                        }
                        else if (DateTime.Now > cert.NotAfter)
                        {
                            results.Add(new ValidationResult(
                                $"Certificate has expired (expired on {cert.NotAfter})",
                                new[] { nameof(CertificatePath) }));
                        }

                        // Validate thumbprint if specified
                        if (!string.IsNullOrEmpty(CertificateThumbprint) &&
                            !string.Equals(cert.Thumbprint, CertificateThumbprint, StringComparison.OrdinalIgnoreCase))
                        {
                            results.Add(new ValidationResult(
                                "Certificate thumbprint does not match expected value",
                                new[] { nameof(CertificateThumbprint) }));
                        }
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new ValidationResult(
                        $"Certificate validation failed: {ex.Message}",
                        new[] { nameof(CertificatePath) }));
                }
            }
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

        // Validate SSL configuration if enabled
        if (UseSSL)
        {
            if (string.IsNullOrEmpty(CertificatePath))
            {
                errors.Add("Certificate path is required when SSL is enabled");
            }
            else if (!ValidateCertificate())
            {
                errors.Add("SSL certificate validation failed");
            }
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

    /// <summary>
    /// Tests SSL/TLS connectivity to the endpoint
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds for the SSL test</param>
    /// <returns>True if SSL connection can be established, false otherwise</returns>
    public async Task<bool> TestSslConnectivityAsync(int timeoutMs = 5000)
    {
        if (!UseSSL)
            return await TestPortAccessibilityAsync(timeoutMs);

        try
        {
            if (!IsValidIPAddress())
                return false;

            using var tcpClient = new System.Net.Sockets.TcpClient();
            var connectTask = tcpClient.ConnectAsync(IPAddress, Port);
            var timeoutTask = Task.Delay(timeoutMs);
            
            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            
            if (completedTask != connectTask || !tcpClient.Connected)
                return false;

            // Test SSL handshake
            using var sslStream = new System.Net.Security.SslStream(tcpClient.GetStream());
            var sslTask = sslStream.AuthenticateAsClientAsync(ExpectedCertificateSubject ?? IPAddress);
            var sslTimeoutTask = Task.Delay(timeoutMs);
            
            var sslCompletedTask = await Task.WhenAny(sslTask, sslTimeoutTask);
            
            return sslCompletedTask == sslTask && sslStream.IsAuthenticated;
        }
        catch
        {
            return false;
        }
    }
}