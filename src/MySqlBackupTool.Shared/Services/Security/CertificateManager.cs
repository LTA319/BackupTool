using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 管理SSL/TLS证书的服务 / Service for managing SSL/TLS certificates
/// </summary>
public class CertificateManager
{
    /// <summary>
    /// 日志记录器 / Logger
    /// </summary>
    private readonly ILogger<CertificateManager> _logger;

    /// <summary>
    /// 初始化证书管理器 / Initializes the certificate manager
    /// </summary>
    /// <param name="logger">日志记录器 / Logger</param>
    /// <exception cref="ArgumentNullException">当日志记录器为null时抛出 / Thrown when logger is null</exception>
    public CertificateManager(ILogger<CertificateManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 为测试目的创建自签名证书 / Creates a self-signed certificate for testing purposes
    /// </summary>
    /// <param name="subjectName">证书的主题名称 / Subject name for the certificate</param>
    /// <param name="validityPeriod">证书有效期 / How long the certificate should be valid</param>
    /// <param name="keySize">RSA密钥大小（默认2048）/ RSA key size (default 2048)</param>
    /// <returns>自签名X509Certificate2 / Self-signed X509Certificate2</returns>
    public X509Certificate2 CreateSelfSignedCertificate(string subjectName, TimeSpan validityPeriod, int keySize = 2048)
    {
        try
        {
            _logger.LogInformation("Creating self-signed certificate for subject: {Subject}", subjectName);

            using var rsa = RSA.Create(keySize);
            var request = new CertificateRequest($"CN={subjectName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            // 为服务器身份验证添加扩展 / Add extensions for server authentication
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false));

            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, // 服务器身份验证 / Server Authentication
                    false));

            // 如果是IP地址或主机名，添加主题备用名称 / Add Subject Alternative Name if it's an IP address or hostname
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
    /// 将证书以PFX格式保存到文件 / Saves a certificate to a file in PFX format
    /// </summary>
    /// <param name="certificate">要保存的证书 / Certificate to save</param>
    /// <param name="filePath">保存证书的路径 / Path where to save the certificate</param>
    /// <param name="password">保护证书文件的密码 / Password to protect the certificate file</param>
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
    /// 从文件加载证书 / Loads a certificate from a file
    /// </summary>
    /// <param name="filePath">证书文件路径 / Path to the certificate file</param>
    /// <param name="password">证书文件密码 / Password for the certificate file</param>
    /// <returns>加载的X509Certificate2 / Loaded X509Certificate2</returns>
    /// <exception cref="FileNotFoundException">当证书文件不存在时抛出 / Thrown when certificate file is not found</exception>
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
    /// 验证证书的基本属性 / Validates a certificate's basic properties
    /// </summary>
    /// <param name="certificate">要验证的证书 / Certificate to validate</param>
    /// <returns>包含详细信息的验证结果 / Validation result with details</returns>
    public CertificateValidationResult ValidateCertificate(X509Certificate2 certificate)
    {
        var result = new CertificateValidationResult();

        try
        {
            _logger.LogDebug("Validating certificate: {Subject}", certificate.Subject);

            // 检查证书是否过期 / Check if certificate is expired
            var now = DateTime.Now;
            if (now < certificate.NotBefore)
            {
                result.Errors.Add($"Certificate is not yet valid (valid from {certificate.NotBefore})");
            }
            else if (now > certificate.NotAfter)
            {
                result.Errors.Add($"Certificate has expired (expired on {certificate.NotAfter})");
            }

            // 检查证书是否有私钥 / Check if certificate has a private key
            if (!certificate.HasPrivateKey)
            {
                result.Warnings.Add("Certificate does not have a private key");
            }

            // 检查密钥用法 / Check key usage
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

            // 检查证书链 / Check certificate chain
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; // 跳过吊销检查进行基本验证 / Skip revocation check for basic validation
            
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
    /// 获取证书信息用于显示 / Gets certificate information for display purposes
    /// </summary>
    /// <param name="certificate">要获取信息的证书 / Certificate to get information from</param>
    /// <returns>证书信息 / Certificate information</returns>
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
    /// 在系统证书存储中查找证书 / Finds certificates in the system certificate store
    /// </summary>
    /// <param name="storeName">证书存储名称 / Certificate store name</param>
    /// <param name="storeLocation">证书存储位置 / Certificate store location</param>
    /// <param name="findType">要执行的搜索类型 / Type of search to perform</param>
    /// <param name="findValue">要搜索的值 / Value to search for</param>
    /// <returns>匹配的证书集合 / Collection of matching certificates</returns>
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
    /// 将证书安装到系统证书存储 / Installs a certificate to the system certificate store
    /// </summary>
    /// <param name="certificate">要安装的证书 / Certificate to install</param>
    /// <param name="storeName">目标存储名称 / Target store name</param>
    /// <param name="storeLocation">目标存储位置 / Target store location</param>
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

    /// <summary>
    /// 验证是否为有效的IP地址 / Validates if input is a valid IP address
    /// </summary>
    /// <param name="input">要验证的输入 / Input to validate</param>
    /// <returns>是否为有效IP地址 / Whether it's a valid IP address</returns>
    private bool IsValidIPAddress(string input)
    {
        return System.Net.IPAddress.TryParse(input, out _);
    }

    /// <summary>
    /// 验证是否为有效的主机名 / Validates if input is a valid hostname
    /// </summary>
    /// <param name="input">要验证的输入 / Input to validate</param>
    /// <returns>是否为有效主机名 / Whether it's a valid hostname</returns>
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
/// 证书验证结果 / Result of certificate validation
/// </summary>
public class CertificateValidationResult
{
    /// <summary>
    /// 是否有效 / Whether it's valid
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// 错误列表 / List of errors
    /// </summary>
    public List<string> Errors { get; set; } = new();
    
    /// <summary>
    /// 警告列表 / List of warnings
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// 用于显示的证书信息 / Certificate information for display
/// </summary>
public class CertificateInfo
{
    /// <summary>
    /// 主题 / Subject
    /// </summary>
    public string Subject { get; set; } = string.Empty;
    
    /// <summary>
    /// 颁发者 / Issuer
    /// </summary>
    public string Issuer { get; set; } = string.Empty;
    
    /// <summary>
    /// 指纹 / Thumbprint
    /// </summary>
    public string Thumbprint { get; set; } = string.Empty;
    
    /// <summary>
    /// 序列号 / Serial number
    /// </summary>
    public string SerialNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// 生效时间 / Valid from
    /// </summary>
    public DateTime NotBefore { get; set; }
    
    /// <summary>
    /// 过期时间 / Valid until
    /// </summary>
    public DateTime NotAfter { get; set; }
    
    /// <summary>
    /// 是否有私钥 / Whether it has private key
    /// </summary>
    public bool HasPrivateKey { get; set; }
    
    /// <summary>
    /// 密钥算法 / Key algorithm
    /// </summary>
    public string KeyAlgorithm { get; set; } = string.Empty;
    
    /// <summary>
    /// 签名算法 / Signature algorithm
    /// </summary>
    public string SignatureAlgorithm { get; set; } = string.Empty;
    
    /// <summary>
    /// 版本 / Version
    /// </summary>
    public int Version { get; set; }
}