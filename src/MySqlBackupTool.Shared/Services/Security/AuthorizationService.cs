using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 授权服务实现 / Authorization service implementation
/// 提供基于权限的访问控制，包括操作授权检查、权限验证和授权日志记录
/// Provides permission-based access control including operation authorization checks, permission validation and authorization logging
/// </summary>
public class AuthorizationService : IAuthorizationService
{
    private readonly ILogger<AuthorizationService> _logger;
    private readonly ICredentialStorage _credentialStorage;
    private readonly IAuthenticationAuditService _auditService;

    // 操作到权限的映射 / Operation to permission mappings
    private readonly Dictionary<string, string[]> _operationPermissions = new()
    {
        { "upload_backup", new[] { BackupPermissions.UploadBackup } },
        { "download_backup", new[] { BackupPermissions.DownloadBackup } },
        { "delete_backup", new[] { BackupPermissions.DeleteBackup } },
        { "list_backups", new[] { BackupPermissions.ListBackups } },
        { "view_logs", new[] { BackupPermissions.ViewLogs } },
        { "manage_clients", new[] { BackupPermissions.ManageClients } },
        { "system_admin", new[] { BackupPermissions.SystemAdmin } },
        
        // 需要多个权限的复合操作 / Composite operations requiring multiple permissions
        { "backup_management", new[] { BackupPermissions.UploadBackup, BackupPermissions.ListBackups } },
        { "full_backup_access", new[] { BackupPermissions.UploadBackup, BackupPermissions.DownloadBackup, BackupPermissions.ListBackups } }
    };

    /// <summary>
    /// 初始化授权服务 / Initializes authorization service
    /// </summary>
    /// <param name="logger">日志记录器 / Logger instance</param>
    /// <param name="credentialStorage">凭据存储服务 / Credential storage service</param>
    /// <param name="auditService">审计服务 / Authentication audit service</param>
    public AuthorizationService(
        ILogger<AuthorizationService> logger,
        ICredentialStorage credentialStorage,
        IAuthenticationAuditService auditService)
    {
        _logger = logger;
        _credentialStorage = credentialStorage ?? throw new ArgumentNullException(nameof(credentialStorage));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    /// <summary>
    /// 检查客户端是否有权执行操作 / Checks if a client is authorized to perform an operation
    /// 验证客户端权限，检查系统管理员权限，记录授权尝试
    /// Validates client permissions, checks system admin privileges, logs authorization attempts
    /// </summary>
    public async Task<bool> IsAuthorizedAsync(AuthorizationContext context, string operation)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("Operation cannot be null or empty", nameof(operation));

        try
        {
            _logger.LogDebug("Checking authorization for client {ClientId} to perform operation {Operation}", 
                context.ClientId, operation);

            // 获取操作所需的权限 / Get required permissions for the operation
            if (!_operationPermissions.TryGetValue(operation.ToLowerInvariant(), out var requiredPermissions))
            {
                _logger.LogWarning("Unknown operation requested: {Operation} by client {ClientId}", operation, context.ClientId);
                await LogAuthorizationAttemptAsync(context, operation, false, "Unknown operation");
                return false;
            }

            // 检查客户端是否具有系统管理员权限（授予对已知操作的所有访问权限） / Check if client has system admin permission (grants all access to known operations)
            if (context.HasPermission(BackupPermissions.SystemAdmin))
            {
                await LogAuthorizationAttemptAsync(context, operation, true, "System admin permission");
                return true;
            }

            // 检查客户端是否具有所有必需的权限 / Check if client has all required permissions
            var hasAllPermissions = requiredPermissions.All(permission => context.HasPermission(permission));

            if (hasAllPermissions)
            {
                await LogAuthorizationAttemptAsync(context, operation, true, "All required permissions present");
                return true;
            }

            // 记录缺少哪些权限 / Log which permissions are missing
            var missingPermissions = requiredPermissions.Where(p => !context.HasPermission(p)).ToList();
            var reason = $"Missing permissions: {string.Join(", ", missingPermissions)}";
            
            _logger.LogWarning("Authorization denied for client {ClientId} to perform operation {Operation}. {Reason}", 
                context.ClientId, operation, reason);
            
            await LogAuthorizationAttemptAsync(context, operation, false, reason);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking authorization for client {ClientId} operation {Operation}", 
                context.ClientId, operation);
            
            await LogAuthorizationAttemptAsync(context, operation, false, $"Authorization error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 检查客户端是否具有特定权限 / Checks if a client has a specific permission
    /// 验证单个权限，考虑系统管理员的特殊权限
    /// Validates individual permission, considering system admin special privileges
    /// </summary>
    public async Task<bool> HasPermissionAsync(AuthorizationContext context, string permission)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        if (string.IsNullOrWhiteSpace(permission))
            throw new ArgumentException("Permission cannot be null or empty", nameof(permission));

        try
        {
            // 系统管理员拥有所有权限 / System admin has all permissions
            if (context.HasPermission(BackupPermissions.SystemAdmin))
            {
                return true;
            }

            var hasPermission = context.HasPermission(permission);
            
            _logger.LogDebug("Permission check for client {ClientId}, permission {Permission}: {Result}", 
                context.ClientId, permission, hasPermission);

            return hasPermission;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission {Permission} for client {ClientId}", 
                permission, context.ClientId);
            return false;
        }
    }

    /// <summary>
    /// 获取客户端的所有权限 / Gets all permissions for a client
    /// 从凭据存储中检索客户端的权限列表
    /// Retrieves client's permission list from credential storage
    /// </summary>
    public async Task<List<string>> GetClientPermissionsAsync(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID cannot be null or empty", nameof(clientId));

        try
        {
            var credentials = await _credentialStorage.GetCredentialsAsync(clientId);
            if (credentials == null)
            {
                _logger.LogWarning("Attempted to get permissions for non-existent client {ClientId}", clientId);
                return new List<string>();
            }

            return new List<string>(credentials.Permissions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting permissions for client {ClientId}", clientId);
            return new List<string>();
        }
    }

    /// <summary>
    /// 记录授权尝试 / Logs an authorization attempt
    /// 将授权尝试记录到审计日志中，用于安全审计和监控
    /// Records authorization attempts to audit log for security auditing and monitoring
    /// </summary>
    public async Task LogAuthorizationAttemptAsync(AuthorizationContext context, string operation, bool success, string? reason = null)
    {
        if (context == null)
            return;

        try
        {
            // 创建审计日志条目 / Create audit log entry
            var auditLog = new AuthenticationAuditLog
            {
                Id = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                ClientId = context.ClientId,
                ClientIPAddress = context.IPAddress,
                Operation = AuthenticationOperation.PermissionCheck,
                Outcome = success ? AuthenticationOutcome.Success : AuthenticationOutcome.Failure,
                ErrorCode = success ? null : "AUTHORIZATION_FAILED",
                ErrorMessage = success ? null : reason,
                DurationMs = (int)(DateTime.Now - context.RequestTime).TotalMilliseconds,
                AdditionalData = new Dictionary<string, object>
                {
                    { "AuthorizedOperation", operation },
                    { "Permissions", string.Join(", ", context.Permissions) },
                    { "RequestTime", context.RequestTime.ToString("O") }
                }
            };

            // 记录到审计服务 / Log to audit service
            await _auditService.LogAuthenticationEventAsync(auditLog);

            _logger.LogInformation("Logged authorization attempt: Client {ClientId}, Operation {Operation}, Success {Success}", 
                context.ClientId, operation, success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log authorization attempt for client {ClientId}", context.ClientId);
            // 不抛出异常 - 日志记录失败不应破坏授权 / Don't throw - logging failures shouldn't break authorization
        }
    }

    /// <summary>
    /// 验证操作是否受支持 / Validates that an operation is supported
    /// </summary>
    public bool IsOperationSupported(string operation)
    {
        return _operationPermissions.ContainsKey(operation.ToLowerInvariant());
    }

    /// <summary>
    /// 获取操作所需的权限 / Gets the required permissions for an operation
    /// </summary>
    public string[] GetRequiredPermissions(string operation)
    {
        if (_operationPermissions.TryGetValue(operation.ToLowerInvariant(), out var permissions))
        {
            return permissions;
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// 添加新的操作-权限映射 / Adds a new operation-permission mapping
    /// 允许动态注册新的操作和其所需权限
    /// Allows dynamic registration of new operations and their required permissions
    /// </summary>
    public void RegisterOperation(string operation, params string[] requiredPermissions)
    {
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("Operation cannot be null or empty", nameof(operation));

        if (requiredPermissions == null || requiredPermissions.Length == 0)
            throw new ArgumentException("At least one permission is required", nameof(requiredPermissions));

        _operationPermissions[operation.ToLowerInvariant()] = requiredPermissions;
        
        _logger.LogInformation("Registered operation {Operation} with permissions: {Permissions}", 
            operation, string.Join(", ", requiredPermissions));
    }

    /// <summary>
    /// 移除操作-权限映射 / Removes an operation-permission mapping
    /// 允许动态取消注册操作
    /// Allows dynamic unregistration of operations
    /// </summary>
    public bool UnregisterOperation(string operation)
    {
        if (string.IsNullOrWhiteSpace(operation))
            return false;

        var removed = _operationPermissions.Remove(operation.ToLowerInvariant());
        
        if (removed)
        {
            _logger.LogInformation("Unregistered operation {Operation}", operation);
        }

        return removed;
    }

    /// <summary>
    /// 获取所有已注册的操作 / Gets all registered operations
    /// 返回所有操作及其所需权限的副本
    /// Returns a copy of all operations and their required permissions
    /// </summary>
    public Dictionary<string, string[]> GetAllOperations()
    {
        return new Dictionary<string, string[]>(_operationPermissions);
    }
}