using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 用于身份验证令牌的内存令牌管理器 / In-memory token manager for authentication tokens
/// 提供令牌生成、验证、撤销和清理功能 / Provides token generation, validation, revocation and cleanup functionality
/// </summary>
public class TokenManager : ITokenManager
{
    private readonly ILogger<TokenManager> _logger;
    private readonly ConcurrentDictionary<string, AuthenticationToken> _tokens = new();
    private readonly Timer _cleanupTimer;
    private readonly object _lockObject = new();

    /// <summary>
    /// 初始化令牌管理器 / Initialize token manager
    /// 设置定期清理过期令牌的定时器 / Sets up timer for periodic cleanup of expired tokens
    /// </summary>
    /// <param name="logger">日志记录器 / Logger instance</param>
    public TokenManager(ILogger<TokenManager> logger)
    {
        _logger = logger;
        
        // 设置清理定时器每5分钟运行一次 / Setup cleanup timer to run every 5 minutes
        _cleanupTimer = new Timer(async _ => await CleanupExpiredTokensAsync(), 
            null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// 生成新的身份验证令牌 / Generates a new authentication token
    /// 创建具有指定权限和过期时间的安全令牌 / Creates secure token with specified permissions and expiration time
    /// </summary>
    /// <param name="clientId">客户端ID / Client ID</param>
    /// <param name="permissions">权限列表 / List of permissions</param>
    /// <param name="expirationHours">过期小时数（默认24小时） / Expiration hours (default 24 hours)</param>
    /// <returns>生成的身份验证令牌 / Generated authentication token</returns>
    /// <exception cref="ArgumentException">当clientId为空时抛出 / Thrown when clientId is empty</exception>
    /// <exception cref="ArgumentNullException">当permissions为null时抛出 / Thrown when permissions is null</exception>
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
    /// 验证并检索令牌 / Validates and retrieves a token
    /// 检查令牌是否存在且有效，自动移除无效令牌 / Checks if token exists and is valid, automatically removes invalid tokens
    /// </summary>
    /// <param name="tokenId">令牌ID / Token ID</param>
    /// <returns>有效的身份验证令牌或null / Valid authentication token or null</returns>
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
                    // 移除无效令牌 / Remove invalid token
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
    /// 更新令牌最后使用时间戳 / Updates token last used timestamp
    /// 用于跟踪令牌活动和使用情况 / Used to track token activity and usage
    /// </summary>
    /// <param name="tokenId">令牌ID / Token ID</param>
    /// <returns>更新成功返回true，失败返回false / Returns true if updated successfully, false if failed</returns>
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
    /// 撤销令牌 / Revokes a token
    /// 将令牌标记为非活跃状态 / Marks token as inactive
    /// </summary>
    /// <param name="tokenId">要撤销的令牌ID / Token ID to revoke</param>
    /// <returns>撤销成功返回true，失败返回false / Returns true if revoked successfully, false if failed</returns>
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
    /// 撤销客户端的所有令牌 / Revokes all tokens for a client
    /// 将指定客户端的所有活跃令牌标记为非活跃 / Marks all active tokens for specified client as inactive
    /// </summary>
    /// <param name="clientId">客户端ID / Client ID</param>
    /// <returns>撤销的令牌数量 / Number of tokens revoked</returns>
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
    /// 清理过期的令牌 / Cleans up expired tokens
    /// 从内存中移除所有过期的令牌以释放资源 / Removes all expired tokens from memory to free resources
    /// </summary>
    /// <returns>清理的令牌数量 / Number of tokens cleaned up</returns>
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
    /// 获取客户端的所有活跃令牌 / Gets all active tokens for a client
    /// 返回指定客户端的所有有效令牌列表 / Returns list of all valid tokens for specified client
    /// </summary>
    /// <param name="clientId">客户端ID / Client ID</param>
    /// <returns>客户端的活跃令牌列表 / List of active tokens for the client</returns>
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
    /// 生成加密安全的令牌ID / Generates a cryptographically secure token ID
    /// 使用随机数生成器创建256位的安全令牌ID / Uses random number generator to create 256-bit secure token ID
    /// </summary>
    /// <returns>URL安全的Base64编码令牌ID / URL-safe Base64 encoded token ID</returns>
    private string GenerateSecureTokenId()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32]; // 256位 / 256 bits
        rng.GetBytes(bytes);
        
        // 转换为base64并使其URL安全 / Convert to base64 and make URL-safe
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// 释放令牌管理器和清理定时器 / Disposes the token manager and cleanup timer
    /// 清理所有资源并停止定时器 / Cleans up all resources and stops timer
    /// </summary>
    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _tokens.Clear();
    }
}