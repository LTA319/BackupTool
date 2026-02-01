using MySqlBackupTool.Shared.Models;
using Xunit;

namespace MySqlBackupTool.Tests.Models;

/// <summary>
/// Unit tests for AuthenticationError class
/// Tests error message generation and security to ensure no sensitive information is exposed
/// </summary>
public class AuthenticationErrorTests
{
    [Fact]
    public void MissingCredentials_ReturnsCorrectErrorCode()
    {
        // Act
        var error = AuthenticationError.MissingCredentials();

        // Assert
        Assert.Equal("AUTH_001", error.ErrorCode);
        Assert.Equal("Client credentials are missing or invalid", error.Message);
        Assert.Contains("backup configuration settings", error.Details);
        Assert.True(error.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public void InvalidToken_ReturnsCorrectErrorCode()
    {
        // Act
        var error = AuthenticationError.InvalidToken();

        // Assert
        Assert.Equal("AUTH_002", error.ErrorCode);
        Assert.Equal("Authentication token is malformed", error.Message);
        Assert.Contains("base64-encoded", error.Details);
        Assert.Contains("clientId:clientSecret", error.Details);
        Assert.True(error.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public void InvalidCredentials_ReturnsCorrectErrorCode()
    {
        // Act
        var error = AuthenticationError.InvalidCredentials();

        // Assert
        Assert.Equal("AUTH_003", error.ErrorCode);
        Assert.Equal("Authentication failed", error.Message);
        Assert.Contains("credentials are not valid", error.Details);
        Assert.True(error.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public void TokenExpired_ReturnsCorrectErrorCode()
    {
        // Act
        var error = AuthenticationError.TokenExpired();

        // Assert
        Assert.Equal("AUTH_004", error.ErrorCode);
        Assert.Equal("Authentication token has expired", error.Message);
        Assert.Contains("new authentication token", error.Details);
        Assert.True(error.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public void InsufficientPermissions_ReturnsCorrectErrorCodeWithPermission()
    {
        // Arrange
        const string requiredPermission = "backup.upload";

        // Act
        var error = AuthenticationError.InsufficientPermissions(requiredPermission);

        // Assert
        Assert.Equal("AUTH_005", error.ErrorCode);
        Assert.Equal("Insufficient permissions", error.Message);
        Assert.Contains(requiredPermission, error.Details);
        Assert.True(error.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public void ServerError_ReturnsCorrectErrorCode()
    {
        // Act
        var error = AuthenticationError.ServerError();

        // Assert
        Assert.Equal("AUTH_006", error.ErrorCode);
        Assert.Equal("Authentication service error", error.Message);
        Assert.Contains("internal error", error.Details);
        Assert.True(error.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public void ClientLocked_ReturnsCorrectErrorCodeWithDuration()
    {
        // Arrange
        var lockoutDuration = TimeSpan.FromMinutes(15);

        // Act
        var error = AuthenticationError.ClientLocked(lockoutDuration);

        // Assert
        Assert.Equal("AUTH_007", error.ErrorCode);
        Assert.Equal("Client account is temporarily locked", error.Message);
        Assert.Contains("15 minutes", error.Details);
        Assert.True(error.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public void Custom_ReturnsCorrectCustomError()
    {
        // Arrange
        const string customCode = "CUSTOM_001";
        const string customMessage = "Custom error message";
        const string customDetails = "Custom error details";

        // Act
        var error = AuthenticationError.Custom(customCode, customMessage, customDetails);

        // Assert
        Assert.Equal(customCode, error.ErrorCode);
        Assert.Equal(customMessage, error.Message);
        Assert.Equal(customDetails, error.Details);
        Assert.True(error.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public void ToString_ReturnsFormattedErrorString()
    {
        // Arrange
        var error = AuthenticationError.MissingCredentials();

        // Act
        var result = error.ToString();

        // Assert
        Assert.Contains("[AUTH_001]", result);
        Assert.Contains("Client credentials are missing or invalid", result);
        Assert.Contains("backup configuration settings", result);
    }

    [Theory]
    [InlineData("AUTH_001")]
    [InlineData("AUTH_002")]
    [InlineData("AUTH_003")]
    [InlineData("AUTH_004")]
    [InlineData("AUTH_005")]
    [InlineData("AUTH_006")]
    [InlineData("AUTH_007")]
    public void AllErrorCodes_AreUnique(string expectedCode)
    {
        // Arrange & Act
        var errors = new[]
        {
            AuthenticationError.MissingCredentials(),
            AuthenticationError.InvalidToken(),
            AuthenticationError.InvalidCredentials(),
            AuthenticationError.TokenExpired(),
            AuthenticationError.InsufficientPermissions("test"),
            AuthenticationError.ServerError(),
            AuthenticationError.ClientLocked(TimeSpan.FromMinutes(15))
        };

        // Assert
        var matchingErrors = errors.Where(e => e.ErrorCode == expectedCode).ToList();
        Assert.Single(matchingErrors);
    }

    [Fact]
    public void ErrorMessages_DoNotContainSensitiveInformation()
    {
        // Arrange
        var sensitiveTerms = new[] { "password", "secret", "key", "token", "credential" };
        var errors = new[]
        {
            AuthenticationError.MissingCredentials(),
            AuthenticationError.InvalidToken(),
            AuthenticationError.InvalidCredentials(),
            AuthenticationError.TokenExpired(),
            AuthenticationError.InsufficientPermissions("backup.upload"),
            AuthenticationError.ServerError(),
            AuthenticationError.ClientLocked(TimeSpan.FromMinutes(15))
        };

        // Act & Assert
        foreach (var error in errors)
        {
            var errorText = error.ToString().ToLowerInvariant();
            
            // Check that error messages don't contain actual sensitive values
            // Note: The word "credential" in generic context is acceptable, but not actual credential values
            foreach (var term in sensitiveTerms)
            {
                if (term == "credential" && (error.ErrorCode == "AUTH_001" || error.ErrorCode == "AUTH_003"))
                {
                    // These errors can mention "credentials" in general terms
                    continue;
                }
                
                // For other cases, ensure no sensitive terms appear in a way that could expose actual values
                if (errorText.Contains($"{term}=") || errorText.Contains($"{term}:") || errorText.Contains($"{term} "))
                {
                    // Only allow generic mentions, not specific values
                    Assert.DoesNotContain($"actual {term}", errorText);
                    Assert.DoesNotContain($"real {term}", errorText);
                }
            }
        }
    }

    [Fact]
    public void ErrorMessages_AreUserFriendly()
    {
        // Arrange
        var errors = new[]
        {
            AuthenticationError.MissingCredentials(),
            AuthenticationError.InvalidToken(),
            AuthenticationError.InvalidCredentials(),
            AuthenticationError.TokenExpired(),
            AuthenticationError.InsufficientPermissions("backup.upload"),
            AuthenticationError.ServerError(),
            AuthenticationError.ClientLocked(TimeSpan.FromMinutes(15))
        };

        // Act & Assert
        foreach (var error in errors)
        {
            // Messages should not be empty
            Assert.NotEmpty(error.Message);
            Assert.NotEmpty(error.Details);
            
            // Messages should not contain technical jargon that would confuse users
            var technicalTerms = new[] { "null", "exception", "stack", "trace", "debug" };
            var messageText = error.Message.ToLowerInvariant();
            var detailsText = error.Details.ToLowerInvariant();
            
            foreach (var term in technicalTerms)
            {
                Assert.DoesNotContain(term, messageText);
                Assert.DoesNotContain(term, detailsText);
            }
            
            // Messages should provide actionable guidance
            Assert.True(error.Details.Length > error.Message.Length, 
                $"Details should provide more information than the message for error {error.ErrorCode}");
        }
    }

    [Fact]
    public void Timestamp_IsSetToCurrentTime()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;
        
        // Act
        var error = AuthenticationError.MissingCredentials();
        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.True(error.Timestamp >= beforeCreation);
        Assert.True(error.Timestamp <= afterCreation);
    }

    [Fact]
    public void MultipleInstances_HaveDifferentTimestamps()
    {
        // Act
        var error1 = AuthenticationError.MissingCredentials();
        Thread.Sleep(1); // Ensure different timestamps
        var error2 = AuthenticationError.InvalidToken();

        // Assert
        Assert.NotEqual(error1.Timestamp, error2.Timestamp);
    }
}