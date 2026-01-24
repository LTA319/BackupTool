using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Service for managing SSL/TLS certificates
/// </summary>
public class CertificateManager
{
    private readonly ILogger<CertificateManager> _logger;

    public CertificateManager(ILogger<CertificateManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a self-signed certificate for testing purposes
    /// </summary>
    /// <param name="subjectName">Subject name for the certificate</param>
    /// <param name="validityPeriod">How long the certificate should be valid</param>
    /// <param name="keySize">RSA key size (default 2048)</param>
    /// <returns>Self-signed X509Certificate2</returns>
    public X509Certificate2 CreateSelfSignedCertificate(string subjectName, TimeSpan validityPeriod, int keySize = 2048)
    {
        try
        {
            _logger.LogInformation("Creating self-signed certificate for subject: {Subject}", subjectName);

            using var rsa = RSA.Create(keySize);
            var request = new CertificateRequest($"CN={subjectName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            // Add extensions for server authentication
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false));

            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, // Server Authentication
                    false));

            // Add Subject Alternative Name if it's an IP address or hostname
            if (IsValidIPAddress(subjectName) || IsValidHostname(subjectName))
            {
                var sanBuilder = new SubjectAlternativeNameBuilder();
                if (IsValidIPAddress(subjectName))
                {
                    sanBuilder.AddIpAddress(System.Net.IPAddress.Parse(subjectName));
                }
                else
                {
                    sanBuilder.AddDnsName(subjectName);
                }
                request.CertificateExtensions.Add(sanBuilder.Build());
            }

            var notBefore = DateTimeOffset.UtcNow;
            var notAfter = notBefore.Add(validityPeriod);

            var certificate = request.CreateSelfSigned(notBefore, notAfter);

            _logger.LogInformation("Self-signed certificate created successfully. Thumbprint: {Thumbprint}, Valid until: {NotAfter}",
                certificate.Thumbprint, certificate.NotAfter);

            return certificate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create self-signed certificate for subject: {Subject}", subjectName);
            throw;
        }
    }

    /// <summary>
    /// Saves a certificate to a file in PFX format
    /// </summary>
    /// <param name="certificate">Certificate to save</param>
    /// <param name="filePath">Path where to save the certificate</param>
    /// <param name="password">Password to protect the certificate file</param>
    public void SaveCertificateToFile(X509Certificate2 certificate, string filePath, string? password = null)
    {
        try
        {
            _logger.LogInformation("Saving certificate to file: {FilePath}", filePath);

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            byte[] certificateBytes;
            if (string.IsNullOrEmpty(password))
            {
                certificateBytes = certificate.Export(X509ContentType.Pfx);
            }
            else
            {
                certificateBytes = certificate.Export(X509ContentType.Pfx, password);
            }

            File.WriteAllBytes(filePath, certificateBytes);

            _logger.LogInformation("Certificate saved successfully to: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save certificate to file: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Loads a certificate from a file
    /// </summary>
    /// <param name="filePath">Path to the certificate file</param>
    /// <param name="password">Password for the certificate file</param>
    /// <returns>Loaded X509Certificate2</returns>
    public X509Certificate2 LoadCertificateFromFile(string filePath, string? password = null)
    {
        try
        {
            _logger.LogInformation("Loading certificate from file: {FilePath}", filePath);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Certificate file not found: {filePath}");
            }

            X509Certificate2 certificate;
            if (string.IsNullOrEmpty(password))
            {
                certificate = new X509Certificate2(filePath);
            }
            else
            {
                certificate = new X509Certificate2(filePath, password);
            }

            _logger.LogInformation("Certificate loaded successfully. Subject: {Subject}, Thumbprint: {Thumbprint}",
                certificate.Subject, certificate.Thumbprint);

            return certificate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load certificate from file: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Validates a certificate's basic properties
    /// </summary>
    /// <param name="certificate">Certificate to validate</param>
    /// <returns>Validation result with details</returns>
    public CertificateValidationResult ValidateCertificate(X509Certificate2 certificate)
    {
        var result = new CertificateValidationResult();

        try
        {
            _logger.LogDebug("Validating certificate: {Subject}", certificate.Subject);

            // Check if certificate is expired
            var now = DateTime.Now;
            if (now < certificate.NotBefore)
            {
                result.Errors.Add($"Certificate is not yet valid (valid from {certificate.NotBefore})");
            }
            else if (now > certificate.NotAfter)
            {
                result.Errors.Add($"Certificate has expired (expired on {certificate.NotAfter})");
            }

            // Check if certificate has a private key
            if (!certificate.HasPrivateKey)
            {
                result.Warnings.Add("Certificate does not have a private key");
            }

            // Check key usage
            foreach (var extension in certificate.Extensions)
            {
                if (extension is X509KeyUsageExtension keyUsage)
                {
                    if (!keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.KeyEncipherment) &&
                        !keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature))
                    {
                        result.Warnings.Add("Certificate may not be suitable for SSL/TLS (missing key encipherment or digital signature usage)");
                    }
                }
            }

            // Check certificate chain
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; // Skip revocation check for basic validation
            
            if (!chain.Build(certificate))
            {
                result.Warnings.Add("Certificate chain validation failed");
                foreach (var status in chain.ChainStatus)
                {
                    result.Warnings.Add($"Chain status: {status.Status} - {status.StatusInformation}");
                }
            }

            result.IsValid = result.Errors.Count == 0;

            _logger.LogDebug("Certificate validation completed. Valid: {IsValid}, Errors: {ErrorCount}, Warnings: {WarningCount}",
                result.IsValid, result.Errors.Count, result.Warnings.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during certificate validation");
            result.Errors.Add($"Validation error: {ex.Message}");
            result.IsValid = false;
            return result;
        }
    }

    /// <summary>
    /// Gets certificate information for display purposes
    /// </summary>
    /// <param name="certificate">Certificate to get information from</param>
    /// <returns>Certificate information</returns>
    public CertificateInfo GetCertificateInfo(X509Certificate2 certificate)
    {
        try
        {
            return new CertificateInfo
            {
                Subject = certificate.Subject,
                Issuer = certificate.Issuer,
                Thumbprint = certificate.Thumbprint,
                SerialNumber = certificate.SerialNumber,
                NotBefore = certificate.NotBefore,
                NotAfter = certificate.NotAfter,
                HasPrivateKey = certificate.HasPrivateKey,
                KeyAlgorithm = certificate.GetKeyAlgorithm(),
                SignatureAlgorithm = certificate.SignatureAlgorithm.FriendlyName ?? "Unknown",
                Version = certificate.Version
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting certificate information");
            throw;
        }
    }

    /// <summary>
    /// Finds certificates in the system certificate store
    /// </summary>
    /// <param name="storeName">Certificate store name</param>
    /// <param name="storeLocation">Certificate store location</param>
    /// <param name="findType">Type of search to perform</param>
    /// <param name="findValue">Value to search for</param>
    /// <returns>Collection of matching certificates</returns>
    public X509Certificate2Collection FindCertificates(StoreName storeName, StoreLocation storeLocation, X509FindType findType, object findValue)
    {
        try
        {
            _logger.LogDebug("Searching for certificates in {StoreLocation}\\{StoreName} by {FindType}: {FindValue}",
                storeLocation, storeName, findType, findValue);

            using var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadOnly);

            var certificates = store.Certificates.Find(findType, findValue, false);

            _logger.LogDebug("Found {Count} certificates matching the criteria", certificates.Count);

            return certificates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for certificates");
            throw;
        }
    }

    /// <summary>
    /// Installs a certificate to the system certificate store
    /// </summary>
    /// <param name="certificate">Certificate to install</param>
    /// <param name="storeName">Target store name</param>
    /// <param name="storeLocation">Target store location</param>
    public void InstallCertificate(X509Certificate2 certificate, StoreName storeName, StoreLocation storeLocation)
    {
        try
        {
            _logger.LogInformation("Installing certificate to {StoreLocation}\\{StoreName}. Subject: {Subject}",
                storeLocation, storeName, certificate.Subject);

            using var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadWrite);
            store.Add(certificate);

            _logger.LogInformation("Certificate installed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install certificate to store");
            throw;
        }
    }

    private bool IsValidIPAddress(string input)
    {
        return System.Net.IPAddress.TryParse(input, out _);
    }

    private bool IsValidHostname(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 255)
            return false;

        return input.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-') &&
               !input.StartsWith('-') && !input.EndsWith('-') &&
               !input.StartsWith('.') && !input.EndsWith('.');
    }
}

/// <summary>
/// Result of certificate validation
/// </summary>
public class CertificateValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Certificate information for display
/// </summary>
public class CertificateInfo
{
    public string Subject { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Thumbprint { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public DateTime NotBefore { get; set; }
    public DateTime NotAfter { get; set; }
    public bool HasPrivateKey { get; set; }
    public string KeyAlgorithm { get; set; } = string.Empty;
    public string SignatureAlgorithm { get; set; } = string.Empty;
    public int Version { get; set; }
}