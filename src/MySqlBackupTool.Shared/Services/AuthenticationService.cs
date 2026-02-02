using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.Collections.Concurrent;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 身份验证服务实现 / Authentication service implementation
/// 提供客户端身份验证、令牌验证、授权上下文管理和速率限制功能
/// Provides client authentication, token validation, authorization context management and rate limiting functionality
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly ILogger<AuthenticationService> _logger;
    private readonly ICredentialStorage _credentialStorage;
    private readonly ITokenManager _tokenManager;
    private readonly CredentialStorageConfig _config;
    
    // 跟踪身份验证尝试以进行速率限制 / Track authentication attempts for rate limiting
    private readonly ConcurrentDictionary<string, AuthenticationAttempts> _authenticationAttempts = new();
    private readonly Timer _cleanupTimer;

    /// <summary>
    /// 初始化身份验证服务 / Initializes authentication service
    /// </summary>
    /// <param name="logger">日志记录器 / Logger instance</param>
    /// <param name="credentialStorage">凭据存储服务 / Credential storage service</param>
    /// <param name="tokenManager">令牌管理器 / Token manager</param>
    /// <param name="config">凭据存储配置 / Credential storage configuration</param>
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

        // 设置身份验证尝试的清理定时器 / Setup cleanup timer for authentication attempts
        _cleanupTimer = new Timer(CleanupAuthenticationAttempts, 
            null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// 使用凭据对客户端进行身份验证 / Authenticates a client using credentials
    /// 验证客户端凭据，检查速率限制，生成访问令牌
    /// Validates client credentials, checks rate limiting, generates access token
    /// </summary>
    public async Task<AuthenticationResponse> AuthenticateAsync(AuthenticationRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var clientId = request.ClientId;
        
        try
        {
            _logger.LogInformation("Authentication attempt for client {ClientId}", clientId);

            // 检查速率限制 / Check for rate limiting
            if (IsClientLocked(clientId))
            {
                _logger.LogWarning("Authentication blocked for client {ClientId} due to too many failed attempts", clientId);
                return new AuthenticationResponse
                {
                    Success = false,
                    ErrorMessage = "Account temporarily locked due to too many failed authentication attempts"
                };
            }

            // 验证请求时间戳以防止重放攻击 / Validate request timestamp to prevent replay attacks
            var requestAge = DateTime.Now - request.Timestamp;
            if (Math.Abs(requestAge.TotalMinutes) > 5) // 允许5分钟的时钟偏差 / Allow 5 minutes clock skew
            {
                _logger.LogWarning("Authentication request rejected for client {ClientId} due to invalid timestamp", clientId);
                RecordFailedAttempt(clientId);
                return new AuthenticationResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid request timestamp"
                };
            }

            // 检索存储的凭据 / Retrieve stored credentials
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

            // 检查客户端是否处于活动状态且未过期 / Check if client is active and not expired
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

            // 验证客户端密钥 / Verify the client secret
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

            // 身份验证成功 - 生成令牌 / Authentication successful - generate token
            var token = await _tokenManager.GenerateTokenAsync(clientId, storedCredentials.Permissions);
            
            // 成功身份验证时清除失败尝试 / Clear failed attempts on successful authentication
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
    /// 验证身份验证令牌 / Validates an authentication token
    /// 检查令牌的有效性并更新使用情况
    /// Checks token validity and updates usage
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
                // 更新令牌使用情况 / Update token usage
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
    /// 获取令牌的授权上下文 / Gets the authorization context for a token
    /// 返回令牌关联的客户端ID、权限和请求时间
    /// Returns client ID, permissions and request time associated with the token
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
                // 更新令牌使用情况 / Update token usage
                await _tokenManager.UpdateTokenUsageAsync(token);

                return new AuthorizationContext
                {
                    ClientId = authToken.ClientId,
                    Permissions = new List<string>(authToken.Permissions),
                    RequestTime = DateTime.Now
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
    /// 撤销身份验证令牌 / Revokes an authentication token
    /// 使指定的令牌无效，防止进一步使用
    /// Invalidates the specified token, preventing further use
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
    /// 清理过期的令牌 / Cleans up expired tokens
    /// 删除所有过期的令牌以释放存储空间
    /// Removes all expired tokens to free storage space
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
    /// 记录失败的身份验证尝试 / Records a failed authentication attempt
    /// 用于速率限制和安全监控
    /// Used for rate limiting and security monitoring
    /// </summary>
    private void RecordFailedAttempt(string clientId)
    {
        var attempts = _authenticationAttempts.GetOrAdd(clientId, _ => new AuthenticationAttempts());
        attempts.RecordFailedAttempt();
        
        _logger.LogDebug("Recorded failed authentication attempt for client {ClientId}. Total attempts: {Count}", 
            clientId, attempts.FailedAttempts);
    }

    /// <summary>
    /// 检查客户端是否因过多失败尝试而被锁定 / Checks if a client is locked due to too many failed attempts
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
    /// 清理旧的身份验证尝试记录 / Cleans up old authentication attempt records
    /// 定期清理过期的尝试记录以防止内存泄漏
    /// Periodically cleans expired attempt records to prevent memory leaks
    /// </summary>
    private void CleanupAuthenticationAttempts(object? state)
    {
        try
        {
            var cutoffTime = DateTime.Now.AddMinutes(-_config.LockoutDurationMinutes * 2);
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
    /// 释放身份验证服务资源 / Disposes the authentication service
    /// </summary>
    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _authenticationAttempts.Clear();
    }

    /// <summary>
    /// 跟踪身份验证尝试以进行速率限制 / Tracks authentication attempts for rate limiting
    /// 内部类用于管理每个客户端的失败尝试计数和时间戳
    /// Internal class for managing failed attempt counts and timestamps per client
    /// </summary>
    private class AuthenticationAttempts
    {
        /// <summary>
        /// 失败尝试次数 / Number of failed attempts
        /// </summary>
        public int FailedAttempts { get; private set; }
        
        /// <summary>
        /// 第一次尝试时间 / Time of first attempt
        /// </summary>
        public DateTime FirstAttemptTime { get; private set; } = DateTime.Now;
        
        /// <summary>
        /// 最后一次尝试时间 / Time of last attempt
        /// </summary>
        public DateTime LastAttemptTime { get; private set; } = DateTime.Now;

        /// <summary>
        /// 记录失败尝试 / Records a failed attempt
        /// </summary>
        public void RecordFailedAttempt()
        {
            FailedAttempts++;
            LastAttemptTime = DateTime.Now;
        }

        /// <summary>
        /// 检查是否被锁定 / Checks if locked
        /// </summary>
        /// <param name="maxAttempts">最大尝试次数 / Maximum attempts</param>
        /// <param name="lockoutDuration">锁定持续时间 / Lockout duration</param>
        /// <returns>如果被锁定返回true / True if locked</returns>
        public bool IsLocked(int maxAttempts, TimeSpan lockoutDuration)
        {
            if (FailedAttempts < maxAttempts)
                return false;

            // 检查锁定期是否已过期 / Check if lockout period has expired
            return DateTime.Now - LastAttemptTime < lockoutDuration;
        }
    }
}