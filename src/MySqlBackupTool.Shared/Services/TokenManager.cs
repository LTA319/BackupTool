using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// In-memory token manager for authentication tokens
/// </summary>
public class TokenManager : ITokenManager
{
    private readonly ILogger<TokenManager> _logger;
    private readonly ConcurrentDictionary<string, AuthenticationToken> _tokens = new();
    private readonly Timer _cleanupTimer;
    private readonly object _lockObject = new();

    public TokenManager(ILogger<TokenManager> logger)
    {
        _logger = logger;
        
        // Setup cleanup timer to run every 5 minutes
        _cleanupTimer = new Timer(async _ => await CleanupExpiredTokensAsync(), 
            null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Generates a new authentication token
    /// </summary>
    public async Task<AuthenticationToken> GenerateTokenAsync(string clientId, List<string> permissions, int expirationHours = 24)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID cannot be null or empty", nameof(clientId));

        if (permissions == null)
            throw new ArgumentNullException(nameof(permissions));

        try
        {
            var token = new AuthenticationToken
            {
                TokenId = GenerateSecureTokenId(),
                ClientId = clientId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(expirationHours),
                Permissions = new List<string>(permissions),
                IsActive = true
            };

            _tokens[token.TokenId] = token;

            _logger.LogInformation("Generated new authentication token for client {ClientId}, expires at {ExpiresAt}", 
                clientId, token.ExpiresAt);

            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate authentication token for client {ClientId}", clientId);
            throw;
        }
    }

    /// <summary>
    /// Validates and retrieves a token
    /// </summary>
    public async Task<AuthenticationToken?> GetTokenAsync(string tokenId)
    {
        if (string.IsNullOrWhiteSpace(tokenId))
            return null;

        try
        {
            if (_tokens.TryGetValue(tokenId, out var token))
            {
                if (token.IsValid)
                {
                    return token;
                }
                else
                {
                    // Remove invalid token
                    _tokens.TryRemove(tokenId, out _);
                    _logger.LogDebug("Removed invalid token {TokenId} for client {ClientId}", tokenId, token.ClientId);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving token {TokenId}", tokenId);
            return null;
        }
    }

    /// <summary>
    /// Updates token last used timestamp
    /// </summary>
    public async Task<bool> UpdateTokenUsageAsync(string tokenId)
    {
        if (string.IsNullOrWhiteSpace(tokenId))
            return false;

        try
        {
            if (_tokens.TryGetValue(tokenId, out var token) && token.IsValid)
            {
                token.UpdateLastUsed();
                _logger.LogDebug("Updated last used timestamp for token {TokenId}", tokenId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update token usage for {TokenId}", tokenId);
            return false;
        }
    }

    /// <summary>
    /// Revokes a token
    /// </summary>
    public async Task<bool> RevokeTokenAsync(string tokenId)
    {
        if (string.IsNullOrWhiteSpace(tokenId))
            return false;

        try
        {
            if (_tokens.TryGetValue(tokenId, out var token))
            {
                token.IsActive = false;
                _logger.LogInformation("Revoked token {TokenId} for client {ClientId}", tokenId, token.ClientId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke token {TokenId}", tokenId);
            return false;
        }
    }

    /// <summary>
    /// Revokes all tokens for a client
    /// </summary>
    public async Task<int> RevokeClientTokensAsync(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return 0;

        try
        {
            var revokedCount = 0;
            
            foreach (var kvp in _tokens)
            {
                var token = kvp.Value;
                if (token.ClientId == clientId && token.IsActive)
                {
                    token.IsActive = false;
                    revokedCount++;
                }
            }

            if (revokedCount > 0)
            {
                _logger.LogInformation("Revoked {Count} tokens for client {ClientId}", revokedCount, clientId);
            }

            return revokedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke tokens for client {ClientId}", clientId);
            return 0;
        }
    }

    /// <summary>
    /// Cleans up expired tokens
    /// </summary>
    public async Task<int> CleanupExpiredTokensAsync()
    {
        try
        {
            var expiredTokens = new List<string>();
            var now = DateTime.UtcNow;

            foreach (var kvp in _tokens)
            {
                var token = kvp.Value;
                if (token.IsExpired)
                {
                    expiredTokens.Add(kvp.Key);
                }
            }

            var cleanedCount = 0;
            foreach (var tokenId in expiredTokens)
            {
                if (_tokens.TryRemove(tokenId, out var removedToken))
                {
                    cleanedCount++;
                    _logger.LogDebug("Cleaned up expired token {TokenId} for client {ClientId}", 
                        tokenId, removedToken.ClientId);
                }
            }

            if (cleanedCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired tokens", cleanedCount);
            }

            return cleanedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token cleanup");
            return 0;
        }
    }

    /// <summary>
    /// Gets all active tokens for a client
    /// </summary>
    public async Task<List<AuthenticationToken>> GetClientTokensAsync(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return new List<AuthenticationToken>();

        try
        {
            var clientTokens = _tokens.Values
                .Where(t => t.ClientId == clientId && t.IsValid)
                .ToList();

            return clientTokens;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tokens for client {ClientId}", clientId);
            return new List<AuthenticationToken>();
        }
    }

    /// <summary>
    /// Generates a cryptographically secure token ID
    /// </summary>
    private string GenerateSecureTokenId()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32]; // 256 bits
        rng.GetBytes(bytes);
        
        // Convert to base64 and make URL-safe
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// Disposes the token manager and cleanup timer
    /// </summary>
    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _tokens.Clear();
    }
}