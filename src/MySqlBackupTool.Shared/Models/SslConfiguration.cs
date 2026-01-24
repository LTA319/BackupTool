namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// Configuration options for SSL/TLS services
/// </summary>
public class SslConfiguration
{
    /// <summary>
    /// Whether to use SSL/TLS for network communications
    /// </summary>
    public bool UseSSL { get; set; } = true;

    /// <summary>
    /// Path to the server certificate file
    /// </summary>
    public string? ServerCertificatePath { get; set; }

    /// <summary>
    /// Password for the server certificate file
    /// </summary>
    public string? ServerCertificatePassword { get; set; }

    /// <summary>
    /// Whether to require client certificates
    /// </summary>
    public bool RequireClientCertificate { get; set; } = false;

    /// <summary>
    /// Whether to validate server certificates on the client side
    /// </summary>
    public bool ValidateServerCertificate { get; set; } = true;

    /// <summary>
    /// Whether to allow self-signed certificates
    /// </summary>
    public bool AllowSelfSignedCertificates { get; set; } = false;

    /// <summary>
    /// Expected certificate subject name for validation
    /// </summary>
    public string? ExpectedCertificateSubject { get; set; }

    /// <summary>
    /// Certificate thumbprint for validation
    /// </summary>
    public string? CertificateThumbprint { get; set; }
}