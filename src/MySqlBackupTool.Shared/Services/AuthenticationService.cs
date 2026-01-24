using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Collections.Concurrent;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Authentication service implementation
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly ILogger<AuthenticationService> _logger;
    private readonly ICredentialStorage _credentialStorage;
    private readonly ITokenManager _tokenManager;
    private readonly CredentialStorageConfig _config;
    
    // Track authentication attempts for rate limiting
    private readonly ConcurrentDictionary<string, AuthenticationAttempts> _authenticationAttempts = new();
    private readonly Timer _cleanupTimer;

    public AuthenticationService(
        ILogger<AuthenticationService> logger,
        ICredentialStorage credentialStorage,
        ITokenManager tokenManager,
        CredentialStorageConfig config)
    {
        _logger = logger;
        _credentialStorage = credentialStorage ?? throw new ArgumentNullException(nameof(credentialStorage));
        _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
        _config = config ?? throw new ArgumentNullException(nameof(config));

        // Setup cleanup timer for authentication attempts
        _cleanupTimer = new Timer(CleanupAuthenticationAttempts, 
            null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Authenticates a client using credentials
    /// </summary>
    public async Task<AuthenticationResponse> AuthenticateAsync(AuthenticationRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var clientId = request.ClientId;
        
        try
        {
            _logger.LogInformation("Authentication attempt for client {ClientId}", clientId);

            // Check for rate limiting
            if (IsClientLocked(clientId))
            {
                _logger.LogWarning("Authentication blocked for client {ClientId} due to too many failed attempts", clientId);
                return new AuthenticationResponse
                {
                    Success = false,
                    ErrorMessage = "Account temporarily locked due to too many failed authentication attempts"
                };
            }

            // Validate request timestamp to prevent replay attacks
            var requestAge = DateTime.UtcNow - request.Timestamp;
            if (Math.Abs(requestAge.TotalMinutes) > 5) // Allow 5 minutes clock skew
            {
                _logger.LogWarning("Authentication request rejected for client {ClientId} due to invalid timestamp", clientId);
                RecordFailedAttempt(clientId);
                return new AuthenticationResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid request timestamp"
                };
            }

            // Retrieve stored credentials
            var storedCredentials = await _credentialStorage.GetCredentialsAsync(clientId);
            if (storedCredentials == null)
            {
                _logger.LogWarning("Authentication failed for client {ClientId}: client not found", clientId);
                RecordFailedAttempt(clientId);
                return new AuthenticationResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid client credentials"
                };
            }

            // Check if client is active and not expired
            if (!storedCredentials.IsActive)
            {
                _logger.LogWarning("Authentication failed for client {ClientId}: client is inactive", clientId);
                RecordFailedAttempt(clientId);
                return new AuthenticationResponse
                {
                    Success = false,
                    ErrorMessage = "Client account is inactive"
                };
            }

            if (storedCredentials.IsExpired)
            {
                _logger.LogWarning("Authentication failed for client {ClientId}: client credentials expired", clientId);
                RecordFailedAttempt(clientId);
                return new AuthenticationResponse
                {
                    Success = false,
                    ErrorMessage = "Client credentials have expired"
                };
            }

            // Verify the client secret
            if (!storedCredentials.VerifySecret(request.ClientSecret, storedCredentials.ClientSecret))
            {
                _logger.LogWarning("Authentication failed for client {ClientId}: invalid secret", clientId);
                RecordFailedAttempt(clientId);
                return new AuthenticationResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid client credentials"
                };
            }

            // Authentication successful - generate token
            var token = await _tokenManager.GenerateTokenAsync(clientId, storedCredentials.Permissions);
            
            // Clear failed attempts on successful authentication
            _authenticationAttempts.TryRemove(clientId, out _);

            _logger.LogInformation("Authentication successful for client {ClientId}", clientId);

            return new AuthenticationResponse
            {
                Success = true,
                Token = token.TokenId,
                TokenExpiresAt = token.ExpiresAt,
                Permissions = new List<string>(storedCredentials.Permissions)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication for client {ClientId}", clientId);
            RecordFailedAttempt(clientId);
            return new AuthenticationResponse
            {
                Success = false,
                ErrorMessage = "Authentication service error"
            };
        }
    }

    /// <summary>
    /// Validates an authentication token
    /// </summary>
    public async Task<bool> ValidateTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var authToken = await _tokenManager.GetTokenAsync(token);
            if (authToken != null && authToken.IsValid)
            {
                // Update token usage
                await _tokenManager.UpdateTokenUsageAsync(token);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token");
            return false;
        }
    }

    /// <summary>
    /// Gets the authorization context for a token
    /// </summary>
    public async Task<AuthorizationContext?> GetAuthorizationContextAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            var authToken = await _tokenManager.GetTokenAsync(token);
            if (authToken != null && authToken.IsValid)
            {
                // Update token usage
                await _tokenManager.UpdateTokenUsageAsync(token);

                return new AuthorizationContext
                {
                    ClientId = authToken.ClientId,
                    Permissions = new List<string>(authToken.Permissions),
                    RequestTime = DateTime.UtcNow
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting authorization context for token");
            return null;
        }
    }

    /// <summary>
    /// Revokes an authentication token
    /// </summary>
    public async Task<bool> RevokeTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var result = await _tokenManager.RevokeTokenAsync(token);
            if (result)
            {
                _logger.LogInformation("Token revoked successfully");
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking token");
            return false;
        }
    }

    /// <summary>
    /// Cleans up expired tokens
    /// </summary>
    public async Task<int> CleanupExpiredTokensAsync()
    {
        try
        {
            return await _tokenManager.CleanupExpiredTokensAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired tokens");
            return 0;
        }
    }

    /// <summary>
    /// Records a failed authentication attempt
    /// </summary>
    private void RecordFailedAttempt(string clientId)
    {
        var attempts = _authenticationAttempts.GetOrAdd(clientId, _ => new AuthenticationAttempts());
        attempts.RecordFailedAttempt();
        
        _logger.LogDebug("Recorded failed authentication attempt for client {ClientId}. Total attempts: {Count}", 
            clientId, attempts.FailedAttempts);
    }

    /// <summary>
    /// Checks if a client is locked due to too many failed attempts
    /// </summary>
    private bool IsClientLocked(string clientId)
    {
        if (_authenticationAttempts.TryGetValue(clientId, out var attempts))
        {
            return attempts.IsLocked(_config.MaxAuthenticationAttempts, TimeSpan.FromMinutes(_config.LockoutDurationMinutes));
        }

        return false;
    }

    /// <summary>
    /// Cleans up old authentication attempt records
    /// </summary>
    private void CleanupAuthenticationAttempts(object? state)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(-_config.LockoutDurationMinutes * 2);
            var keysToRemove = new List<string>();

            foreach (var kvp in _authenticationAttempts)
            {
                if (kvp.Value.FirstAttemptTime < cutoffTime)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _authenticationAttempts.TryRemove(key, out _);
            }

            if (keysToRemove.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} old authentication attempt records", keysToRemove.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up authentication attempts");
        }
    }

    /// <summary>
    /// Disposes the authentication service
    /// </summary>
    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _authenticationAttempts.Clear();
    }

    /// <summary>
    /// Tracks authentication attempts for rate limiting
    /// </summary>
    private class AuthenticationAttempts
    {
        public int FailedAttempts { get; private set; }
        public DateTime FirstAttemptTime { get; private set; } = DateTime.UtcNow;
        public DateTime LastAttemptTime { get; private set; } = DateTime.UtcNow;

        public void RecordFailedAttempt()
        {
            FailedAttempts++;
            LastAttemptTime = DateTime.UtcNow;
        }

        public bool IsLocked(int maxAttempts, TimeSpan lockoutDuration)
        {
            if (FailedAttempts < maxAttempts)
                return false;

            // Check if lockout period has expired
            return DateTime.UtcNow - LastAttemptTime < lockoutDuration;
        }
    }
}