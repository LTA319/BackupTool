using System.ComponentModel.DataAnnotations;
using MySqlBackupTool.Shared.Models;
using Xunit;

namespace MySqlBackupTool.Tests.Models;

public class ServerEndpointTests
{
    [Fact]
    public void ServerEndpoint_ValidEndpoint_PassesValidation()
    {
        // Arrange
        var endpoint = new ServerEndpoint
        {
            IPAddress = "192.168.1.100",
            Port = 8080,
            UseSSL = true
        };

        // Act
        var validationResults = ValidateModel(endpoint);

        // Assert
        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData("", "IP Address is required")]
    [InlineData("192.168.1.1", null)] // Valid IPv4
    [InlineData("::1", null)] // Valid IPv6
    [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334", null)] // Valid IPv6
    [InlineData("invalid.ip", "is not a valid IP address format")]
    [InlineData("999.999.999.999", "is not a valid IP address format")]
    public void ServerEndpoint_IPAddress_ValidationTests(string ipAddress, string? expectedErrorContains)
    {
        // Arrange
        var endpoint = CreateValidEndpoint();
        endpoint.IPAddress = ipAddress;

        // Act
        var validationResults = ValidateModel(endpoint);

        // Assert
        if (expectedErrorContains != null)
        {
            Assert.Contains(validationResults, vr => vr.ErrorMessage!.Contains(expectedErrorContains));
        }
        else
        {
            Assert.DoesNotContain(validationResults, vr => vr.MemberNames.Contains(nameof(ServerEndpoint.IPAddress)));
        }
    }

    [Theory]
    [InlineData(0, "Port must be between 1 and 65535")]
    [InlineData(65536, "Port must be between 1 and 65535")]
    [InlineData(80, "Port 80 is in the reserved range")]
    [InlineData(443, "Port 443 is in the reserved range")]
    [InlineData(1024, null)] // Should pass
    [InlineData(8080, null)] // Should pass
    [InlineData(65535, null)] // Should pass
    public void ServerEndpoint_Port_ValidationTests(int port, string? expectedErrorContains)
    {
        // Arrange
        var endpoint = CreateValidEndpoint();
        endpoint.Port = port;

        // Act
        var validationResults = ValidateModel(endpoint);

        // Assert
        if (expectedErrorContains != null)
        {
            Assert.Contains(validationResults, vr => vr.ErrorMessage!.Contains(expectedErrorContains));
        }
        else
        {
            Assert.DoesNotContain(validationResults, vr => vr.MemberNames.Contains(nameof(ServerEndpoint.Port)));
        }
    }

    [Fact]
    public void ServerEndpoint_CustomValidation_WildcardIPAddress_ReturnsError()
    {
        // Arrange
        var endpoint = CreateValidEndpoint();
        endpoint.IPAddress = "0.0.0.0";

        // Act
        var validationResults = ValidateModel(endpoint);

        // Assert
        Assert.Contains(validationResults, vr => 
            vr.ErrorMessage!.Contains("Cannot use wildcard IP address") && 
            vr.MemberNames.Contains(nameof(ServerEndpoint.IPAddress)));
    }

    [Fact]
    public void ServerEndpoint_CustomValidation_IPv6WildcardIPAddress_ReturnsError()
    {
        // Arrange
        var endpoint = CreateValidEndpoint();
        endpoint.IPAddress = "::";

        // Act
        var validationResults = ValidateModel(endpoint);

        // Assert
        Assert.Contains(validationResults, vr => 
            vr.ErrorMessage!.Contains("Cannot use wildcard IP address") && 
            vr.MemberNames.Contains(nameof(ServerEndpoint.IPAddress)));
    }

    [Theory]
    [InlineData("192.168.1.100", true)]
    [InlineData("::1", true)]
    [InlineData("invalid.ip", false)]
    [InlineData("999.999.999.999", false)]
    public void ServerEndpoint_IsValidIPAddress_ReturnsExpectedResult(string ipAddress, bool expectedResult)
    {
        // Arrange
        var endpoint = new ServerEndpoint { IPAddress = ipAddress };

        // Act
        var result = endpoint.IsValidIPAddress();

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [InlineData("192.168.1.100", 8080, true, "https://192.168.1.100:8080")]
    [InlineData("192.168.1.100", 8080, false, "http://192.168.1.100:8080")]
    [InlineData("::1", 443, true, "https://::1:443")]
    public void ServerEndpoint_GetEndpointAddress_ReturnsCorrectFormat(string ipAddress, int port, bool useSSL, string expectedAddress)
    {
        // Arrange
        var endpoint = new ServerEndpoint
        {
            IPAddress = ipAddress,
            Port = port,
            UseSSL = useSSL
        };

        // Act
        var address = endpoint.GetEndpointAddress();

        // Assert
        Assert.Equal(expectedAddress, address);
    }

    [Fact]
    public void ServerEndpoint_ValidateEndpoint_ValidEndpoint_ReturnsTrue()
    {
        // Arrange
        var endpoint = new ServerEndpoint
        {
            IPAddress = "192.168.1.100",
            Port = 8080
        };

        // Act
        var (isValid, errors) = endpoint.ValidateEndpoint();

        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void ServerEndpoint_ValidateEndpoint_InvalidIPAddress_ReturnsFalse()
    {
        // Arrange
        var endpoint = new ServerEndpoint
        {
            IPAddress = "invalid.ip",
            Port = 8080
        };

        // Act
        var (isValid, errors) = endpoint.ValidateEndpoint();

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("Invalid IP address format"));
    }

    [Fact]
    public void ServerEndpoint_ValidateEndpoint_InvalidPort_ReturnsFalse()
    {
        // Arrange
        var endpoint = new ServerEndpoint
        {
            IPAddress = "192.168.1.100",
            Port = 0
        };

        // Act
        var (isValid, errors) = endpoint.ValidateEndpoint();

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("Port 0 is outside valid range"));
    }

    [Fact]
    public async Task ServerEndpoint_TestConnectivityAsync_InvalidIP_ReturnsFalse()
    {
        // Arrange
        var endpoint = new ServerEndpoint
        {
            IPAddress = "192.168.255.255", // Likely unreachable IP
            Port = 8080
        };

        // Act
        var isConnectable = await endpoint.TestConnectivityAsync(1000); // 1 second timeout

        // Assert
        Assert.False(isConnectable);
    }

    [Fact]
    public async Task ServerEndpoint_TestPortAccessibilityAsync_InvalidEndpoint_ReturnsFalse()
    {
        // Arrange
        var endpoint = new ServerEndpoint
        {
            IPAddress = "192.168.255.255", // Likely unreachable IP
            Port = 8080
        };

        // Act
        var isAccessible = await endpoint.TestPortAccessibilityAsync(1000); // 1 second timeout

        // Assert
        Assert.False(isAccessible);
    }

    private static ServerEndpoint CreateValidEndpoint()
    {
        return new ServerEndpoint
        {
            IPAddress = "192.168.1.100",
            Port = 8080,
            UseSSL = true
        };
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }
}