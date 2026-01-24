using Microsoft.Extensions.Logging;
using Moq;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace MySqlBackupTool.Tests.Services;

public class SecureFileTransferTests
{
    private readonly Mock<ILogger<SecureFileTransferClient>> _mockLogger;
    private readonly Mock<IChecksumService> _mockChecksumService;
    private readonly SecureFileTransferClient _secureClient;
    private readonly CertificateManager _certificateManager;

    public SecureFileTransferTests()
    {
        _mockLogger = new Mock<ILogger<SecureFileTransferClient>>();
        _mockChecksumService = new Mock<IChecksumService>();
        _secureClient = new SecureFileTransferClient(_mockLogger.Object, _mockChecksumService.Object);
        _certificateManager = new CertificateManager(new Mock<ILogger<CertificateManager>>().Object);
    }

    [Fact]
    public void SecureFileTransferClient_Constructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var client = new SecureFileTransferClient(_mockLogger.Object, _mockChecksumService.Object);

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void CertificateManager_CreateSelfSignedCertificate_ShouldCreateValidCertificate()
    {
        // Arrange
        var subjectName = "localhost";
        var validityPeriod = TimeSpan.FromDays(30);

        // Act
        using var certificate = _certificateManager.CreateSelfSignedCertificate(subjectName, validityPeriod);

        // Assert
        Assert.NotNull(certificate);
        Assert.True(certificate.HasPrivateKey);
        Assert.Contains(subjectName, certificate.Subject);
        Assert.True(certificate.NotAfter > DateTime.Now);
        Assert.True(certificate.NotBefore <= DateTime.Now);
    }

    [Fact]
    public void CertificateManager_ValidateCertificate_ShouldReturnValidForGoodCertificate()
    {
        // Arrange
        using var certificate = _certificateManager.CreateSelfSignedCertificate("test", TimeSpan.FromDays(1));

        // Act
        var result = _certificateManager.ValidateCertificate(certificate);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void CertificateManager_ValidateCertificate_ShouldReturnInvalidForExpiredCertificate()
    {
        // Arrange - Create a certificate that will be expired by setting a past validity period
        var notBefore = DateTimeOffset.UtcNow.AddDays(-2);
        var notAfter = DateTimeOffset.UtcNow.AddDays(-1);
        
        // We need to create an expired certificate manually since CreateSelfSignedCertificate doesn't allow negative periods
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(notBefore, notAfter);

        // Act
        var result = _certificateManager.ValidateCertificate(certificate);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("expired"));
    }

    [Fact]
    public void CertificateManager_GetCertificateInfo_ShouldReturnCorrectInfo()
    {
        // Arrange
        var subjectName = "testhost";
        using var certificate = _certificateManager.CreateSelfSignedCertificate(subjectName, TimeSpan.FromDays(1));

        // Act
        var info = _certificateManager.GetCertificateInfo(certificate);

        // Assert
        Assert.NotNull(info);
        Assert.Contains(subjectName, info.Subject);
        Assert.True(info.HasPrivateKey);
        Assert.NotNull(info.Thumbprint);
        Assert.True(info.NotAfter > DateTime.Now);
    }

    [Fact]
    public void ServerEndpoint_ValidateEndpoint_ShouldValidateSSLConfiguration()
    {
        // Arrange
        var tempCertPath = Path.GetTempFileName();
        try
        {
            // Create a test certificate
            using var certificate = _certificateManager.CreateSelfSignedCertificate("localhost", TimeSpan.FromDays(1));
            _certificateManager.SaveCertificateToFile(certificate, tempCertPath, "testpass");

            var endpoint = new ServerEndpoint
            {
                IPAddress = "127.0.0.1",
                Port = 8443,
                UseSSL = true,
                CertificatePath = tempCertPath,
                CertificatePassword = "testpass"
            };

            // Act
            var (isValid, errors) = endpoint.ValidateEndpoint();

            // Assert
            Assert.True(isValid, $"Validation failed: {string.Join(", ", errors)}");
            Assert.Empty(errors);
        }
        finally
        {
            if (File.Exists(tempCertPath))
                File.Delete(tempCertPath);
        }
    }

    [Fact]
    public void ServerEndpoint_ValidateEndpoint_ShouldFailWithMissingCertificate()
    {
        // Arrange
        var endpoint = new ServerEndpoint
        {
            IPAddress = "127.0.0.1",
            Port = 8443,
            UseSSL = true,
            CertificatePath = "nonexistent.pfx"
        };

        // Act
        var (isValid, errors) = endpoint.ValidateEndpoint();

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("SSL certificate validation failed") || e.Contains("Certificate validation failed"));
    }

    [Fact]
    public void ServerEndpoint_LoadCertificate_ShouldLoadValidCertificate()
    {
        // Arrange
        var tempCertPath = Path.GetTempFileName();
        try
        {
            // Create and save a test certificate
            using var originalCert = _certificateManager.CreateSelfSignedCertificate("localhost", TimeSpan.FromDays(1));
            _certificateManager.SaveCertificateToFile(originalCert, tempCertPath, "testpass");

            var endpoint = new ServerEndpoint
            {
                IPAddress = "127.0.0.1",
                Port = 8443,
                UseSSL = true,
                CertificatePath = tempCertPath,
                CertificatePassword = "testpass"
            };

            // Act
            using var loadedCert = endpoint.LoadCertificate();

            // Assert
            Assert.NotNull(loadedCert);
            Assert.Equal(originalCert.Thumbprint, loadedCert.Thumbprint);
            Assert.True(loadedCert.HasPrivateKey);
        }
        finally
        {
            if (File.Exists(tempCertPath))
                File.Delete(tempCertPath);
        }
    }

    [Fact]
    public async Task SecureFileTransferClient_TransferFileAsync_ShouldFailWithMissingFile()
    {
        // Arrange
        var nonExistentFile = "nonexistent.txt";
        var config = new TransferConfig
        {
            FileName = "test.txt",
            TargetServer = new ServerEndpoint
            {
                IPAddress = "127.0.0.1",
                Port = 8443,
                UseSSL = true
            },
            ChunkingStrategy = new ChunkingStrategy(),
            MaxRetries = 1,
            TimeoutSeconds = 30
        };

        // Act
        var result = await _secureClient.TransferFileAsync(nonExistentFile, config);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("File not found", result.ErrorMessage ?? "");
    }

    [Fact]
    public async Task SecureFileTransferClient_TransferFileAsync_ShouldFailWithInvalidEndpoint()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "test content");

            var config = new TransferConfig
            {
                FileName = "test.txt",
                TargetServer = new ServerEndpoint
                {
                    IPAddress = "invalid-ip",
                    Port = 8443,
                    UseSSL = true
                },
                ChunkingStrategy = new ChunkingStrategy(),
                MaxRetries = 1,
                TimeoutSeconds = 30
            };

            // Act
            var result = await _secureClient.TransferFileAsync(tempFile, config);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Invalid server endpoint", result.ErrorMessage ?? "");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}