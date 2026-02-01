using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// 客户端身份验证凭据
/// 用于存储和管理客户端的身份验证信息，包括客户端ID、密钥、权限等
/// </summary>
public class ClientCredentials
{
    /// <summary>
    /// 客户端唯一标识符，必填项
    /// 长度限制：3-100个字符
    /// </summary>
    [Required(ErrorMessage = "Client ID is required")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Client ID must be between 3 and 100 characters")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 客户端密钥，必填项
    /// 长度限制：8-256个字符
    /// </summary>
    [Required(ErrorMessage = "Client secret is required")]
    [StringLength(256, MinimumLength = 8, ErrorMessage = "Client secret must be between 8 and 256 characters")]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// 可选的客户端名称，用于标识和显示
    /// 最大长度：200个字符
    /// </summary>
    [StringLength(200)]
    public string? ClientName { get; set; }

    /// <summary>
    /// 客户端权限/角色列表
    /// 定义该客户端可以执行的操作
    /// </summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// 客户端是否处于活跃状态
    /// 非活跃客户端无法进行身份验证
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 客户端创建时间
    /// 默认为当前UTC时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 客户端过期时间（可选）
    /// 如果设置，客户端将在此时间后失效
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// 验证客户端凭据是否已过期
    /// </summary>
    /// <returns>如果已过期返回true，否则返回false</returns>
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;

    /// <summary>
    /// 生成客户端密钥的安全哈希值用于存储
    /// 使用SHA256算法进行哈希计算
    /// </summary>
    /// <returns>Base64编码的哈希值</returns>
    public string HashSecret()
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(ClientSecret));
        return Convert.ToBase64String(hashedBytes);
    }

    /// <summary>
    /// 验证提供的密钥是否与存储的哈希值匹配
    /// </summary>
    /// <param name="providedSecret">用户提供的密钥</param>
    /// <param name="storedHash">存储的哈希值</param>
    /// <returns>如果匹配返回true，否则返回false</returns>
    public bool VerifySecret(string providedSecret, string storedHash)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(providedSecret));
        var providedHash = Convert.ToBase64String(hashedBytes);
        return string.Equals(providedHash, storedHash, StringComparison.Ordinal);
    }
}

/// <summary>
/// 来自客户端的身份验证请求
/// 包含客户端凭据和防重放攻击的安全信息
/// </summary>
public class AuthenticationRequest
{
    /// <summary>
    /// 客户端标识符，必填项
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 客户端密钥，必填项
    /// </summary>
    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// 请求时间戳（用于防止重放攻击）
    /// 默认为当前UTC时间
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 请求唯一性随机数
    /// 用于确保每个请求的唯一性，防止重放攻击
    /// </summary>
    public string Nonce { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// 来自服务器的身份验证响应
/// 包含验证结果、令牌和权限信息
/// </summary>
public class AuthenticationResponse
{
    /// <summary>
    /// 身份验证是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 错误消息（如果验证失败）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 用于后续请求的身份验证令牌
    /// 只有在验证成功时才会提供
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// 令牌过期时间
    /// 指定令牌的有效期限
    /// </summary>
    public DateTime? TokenExpiresAt { get; set; }

    /// <summary>
    /// 授予客户端的权限列表
    /// 定义客户端可以执行的操作
    /// </summary>
    public List<string> Permissions { get; set; } = new();
}

/// <summary>
/// 用于会话管理的身份验证令牌
/// 包含令牌的完整生命周期信息和使用统计
/// </summary>
public class AuthenticationToken
{
    /// <summary>
    /// 令牌唯一标识符
    /// 默认生成新的GUID
    /// </summary>
    [Required]
    public string TokenId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 关联的客户端标识符
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 令牌创建时间
    /// 默认为当前UTC时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 令牌过期时间
    /// 默认为创建后24小时
    /// </summary>
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);

    /// <summary>
    /// 令牌关联的权限列表
    /// 定义使用此令牌可以执行的操作
    /// </summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// 令牌是否处于活跃状态
    /// 非活跃令牌无法使用
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 令牌最后使用时间
    /// 用于跟踪令牌的使用情况
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// 令牌颁发时的IP地址
    /// 用于安全审计和追踪
    /// </summary>
    public string? IssuerIPAddress { get; set; }

    /// <summary>
    /// 验证令牌是否已过期
    /// </summary>
    /// <returns>如果已过期返回true，否则返回false</returns>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    /// <summary>
    /// 验证令牌是否有效可用
    /// 同时检查活跃状态和过期时间
    /// </summary>
    /// <returns>如果有效返回true，否则返回false</returns>
    public bool IsValid => IsActive && !IsExpired;

    /// <summary>
    /// 更新令牌的最后使用时间戳
    /// 在每次使用令牌时调用
    /// </summary>
    public void UpdateLastUsed()
    {
        LastUsedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// 安全凭据存储配置
/// 定义如何安全地存储和管理客户端凭据
/// </summary>
public class CredentialStorageConfig
{
    /// <summary>
    /// 加密凭据文件的路径
    /// 必须指定有效的文件路径
    /// </summary>
    [Required]
    public string CredentialsFilePath { get; set; } = string.Empty;

    /// <summary>
    /// 凭据存储的加密密钥
    /// 应该从用户输入或系统密钥派生
    /// </summary>
    [Required]
    public string EncryptionKey { get; set; } = string.Empty;

    /// <summary>
    /// 是否使用Windows DPAPI进行额外加密（仅限Windows）
    /// 提供额外的安全保护层
    /// </summary>
    public bool UseWindowsDPAPI { get; set; } = true;

    /// <summary>
    /// 锁定前的最大身份验证尝试次数
    /// 防止暴力破解攻击
    /// </summary>
    public int MaxAuthenticationAttempts { get; set; } = 5;

    /// <summary>
    /// 锁定持续时间（分钟）
    /// 达到最大尝试次数后的锁定时间
    /// </summary>
    public int LockoutDurationMinutes { get; set; } = 15;
}

/// <summary>
/// 请求的授权上下文
/// 包含授权决策所需的所有信息
/// </summary>
public class AuthorizationContext
{
    /// <summary>
    /// 发起请求的客户端标识符
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 客户端拥有的权限列表
    /// 用于授权检查
    /// </summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// 请求来源的IP地址
    /// 用于安全审计和地理位置限制
    /// </summary>
    public string? IPAddress { get; set; }

    /// <summary>
    /// 请求时间
    /// 默认为当前UTC时间
    /// </summary>
    public DateTime RequestTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 请求的操作类型
    /// 描述客户端想要执行的具体操作
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// 授权决策的附加上下文数据
    /// 可以包含任何有助于授权决策的额外信息
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();

    /// <summary>
    /// 检查上下文是否具有特定权限
    /// </summary>
    /// <param name="permission">要检查的权限</param>
    /// <returns>如果具有权限返回true，否则返回false</returns>
    public bool HasPermission(string permission)
    {
        return Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 检查上下文是否具有指定权限中的任意一个
    /// </summary>
    /// <param name="permissions">要检查的权限数组</param>
    /// <returns>如果具有任意一个权限返回true，否则返回false</returns>
    public bool HasAnyPermission(params string[] permissions)
    {
        return permissions.Any(p => HasPermission(p));
    }

    /// <summary>
    /// 检查上下文是否具有所有指定的权限
    /// </summary>
    /// <param name="permissions">要检查的权限数组</param>
    /// <returns>如果具有所有权限返回true，否则返回false</returns>
    public bool HasAllPermissions(params string[] permissions)
    {
        return permissions.All(p => HasPermission(p));
    }
}

/// <summary>
/// 身份验证操作的结果
/// 用于表示身份验证操作的成功或失败状态
/// </summary>
public class AuthenticationResult
{
    /// <summary>
    /// 身份验证是否成功
    /// </summary>
    public bool IsSuccess { get; private set; }

    /// <summary>
    /// 验证成功时的客户端标识符
    /// </summary>
    public string? ClientId { get; private set; }

    /// <summary>
    /// 验证失败时的错误消息
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// 验证操作的时间戳
    /// </summary>
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// 私有构造函数，强制使用静态工厂方法
    /// </summary>
    private AuthenticationResult() { }

    /// <summary>
    /// 创建成功的身份验证结果
    /// </summary>
    /// <param name="clientId">验证成功的客户端标识符</param>
    /// <returns>表示成功的AuthenticationResult实例</returns>
    public static AuthenticationResult Success(string clientId)
    {
        return new AuthenticationResult
        {
            IsSuccess = true,
            ClientId = clientId,
            ErrorMessage = null
        };
    }

    /// <summary>
    /// 创建失败的身份验证结果
    /// </summary>
    /// <param name="errorMessage">描述失败原因的错误消息</param>
    /// <returns>表示失败的AuthenticationResult实例</returns>
    public static AuthenticationResult Failure(string errorMessage)
    {
        return new AuthenticationResult
        {
            IsSuccess = false,
            ClientId = null,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// 身份验证错误的标准化错误响应类
/// 提供统一的错误格式和常见错误类型的工厂方法
/// </summary>
public class AuthenticationError
{
    /// <summary>
    /// 错误代码，用于程序化处理
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// 用户友好的错误消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 详细的错误描述或解决建议
    /// </summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>
    /// 错误发生的时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 私有构造函数，强制使用静态工厂方法
    /// </summary>
    private AuthenticationError() { }

    /// <summary>
    /// 创建客户端凭据缺失或无效的错误
    /// </summary>
    /// <returns>标准化的身份验证错误实例</returns>
    public static AuthenticationError MissingCredentials()
    {
        return new AuthenticationError
        {
            ErrorCode = "AUTH_001",
            Message = "Client credentials are missing or invalid",
            Details = "Please check your backup configuration settings and ensure valid ClientId and ClientSecret are provided",
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 创建身份验证令牌格式错误的错误
    /// </summary>
    /// <returns>标准化的身份验证错误实例</returns>
    public static AuthenticationError InvalidToken()
    {
        return new AuthenticationError
        {
            ErrorCode = "AUTH_002",
            Message = "Authentication token is malformed",
            Details = "Token must be base64-encoded in the format 'clientId:clientSecret'",
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 创建凭据验证失败的错误
    /// </summary>
    /// <returns>标准化的身份验证错误实例</returns>
    public static AuthenticationError InvalidCredentials()
    {
        return new AuthenticationError
        {
            ErrorCode = "AUTH_003",
            Message = "Authentication failed",
            Details = "The provided credentials are not valid. Please verify your ClientId and ClientSecret",
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 创建令牌过期的错误
    /// </summary>
    /// <returns>标准化的身份验证错误实例</returns>
    public static AuthenticationError TokenExpired()
    {
        return new AuthenticationError
        {
            ErrorCode = "AUTH_004",
            Message = "Authentication token has expired",
            Details = "Please obtain a new authentication token and retry the operation",
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 创建权限不足的错误
    /// </summary>
    /// <param name="requiredPermission">所需的权限</param>
    /// <returns>标准化的身份验证错误实例</returns>
    public static AuthenticationError InsufficientPermissions(string requiredPermission)
    {
        return new AuthenticationError
        {
            ErrorCode = "AUTH_005",
            Message = "Insufficient permissions",
            Details = $"This operation requires the '{requiredPermission}' permission",
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 创建服务器内部错误
    /// </summary>
    /// <returns>标准化的身份验证错误实例</returns>
    public static AuthenticationError ServerError()
    {
        return new AuthenticationError
        {
            ErrorCode = "AUTH_006",
            Message = "Authentication service error",
            Details = "An internal error occurred during authentication. Please try again later",
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 创建客户端被锁定的错误
    /// </summary>
    /// <param name="lockoutDuration">锁定持续时间</param>
    /// <returns>标准化的身份验证错误实例</returns>
    public static AuthenticationError ClientLocked(TimeSpan lockoutDuration)
    {
        return new AuthenticationError
        {
            ErrorCode = "AUTH_007",
            Message = "Client account is temporarily locked",
            Details = $"Too many failed authentication attempts. Please try again in {lockoutDuration.TotalMinutes:F0} minutes",
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 创建自定义身份验证错误
    /// </summary>
    /// <param name="errorCode">错误代码</param>
    /// <param name="message">错误消息</param>
    /// <param name="details">错误详情</param>
    /// <returns>标准化的身份验证错误实例</returns>
    public static AuthenticationError Custom(string errorCode, string message, string details)
    {
        return new AuthenticationError
        {
            ErrorCode = errorCode,
            Message = message,
            Details = details,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 将错误转换为字符串表示形式
    /// </summary>
    /// <returns>格式化的错误字符串</returns>
    public override string ToString()
    {
        return $"[{ErrorCode}] {Message}: {Details}";
    }
}

/// <summary>
/// 身份验证审计日志条目
/// 记录所有身份验证操作的详细信息用于安全审计
/// </summary>
public class AuthenticationAuditLog
{
    /// <summary>
    /// 审计日志条目的唯一标识符
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 身份验证事件的时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 客户端标识符（如果可用）
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// 身份验证操作类型
    /// </summary>
    public AuthenticationOperation Operation { get; set; }

    /// <summary>
    /// 操作结果（成功或失败）
    /// </summary>
    public AuthenticationOutcome Outcome { get; set; }

    /// <summary>
    /// 错误消息（如果操作失败）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 错误代码（如果操作失败）
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// 客户端IP地址
    /// </summary>
    public string? ClientIPAddress { get; set; }

    /// <summary>
    /// 用户代理字符串
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// 会话标识符（如果适用）
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// 操作持续时间（毫秒）
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// 附加的上下文数据
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();

    /// <summary>
    /// 创建成功的身份验证审计日志
    /// </summary>
    /// <param name="clientId">客户端标识符</param>
    /// <param name="operation">身份验证操作</param>
    /// <param name="durationMs">操作持续时间</param>
    /// <param name="clientIP">客户端IP地址</param>
    /// <returns>审计日志条目</returns>
    public static AuthenticationAuditLog Success(string clientId, AuthenticationOperation operation, long durationMs, string? clientIP = null)
    {
        return new AuthenticationAuditLog
        {
            ClientId = clientId,
            Operation = operation,
            Outcome = AuthenticationOutcome.Success,
            DurationMs = durationMs,
            ClientIPAddress = clientIP
        };
    }

    /// <summary>
    /// 创建失败的身份验证审计日志
    /// </summary>
    /// <param name="clientId">客户端标识符（如果可用）</param>
    /// <param name="operation">身份验证操作</param>
    /// <param name="errorCode">错误代码</param>
    /// <param name="errorMessage">错误消息</param>
    /// <param name="durationMs">操作持续时间</param>
    /// <param name="clientIP">客户端IP地址</param>
    /// <returns>审计日志条目</returns>
    public static AuthenticationAuditLog Failure(string? clientId, AuthenticationOperation operation, string errorCode, string errorMessage, long durationMs, string? clientIP = null)
    {
        return new AuthenticationAuditLog
        {
            ClientId = clientId,
            Operation = operation,
            Outcome = AuthenticationOutcome.Failure,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            DurationMs = durationMs,
            ClientIPAddress = clientIP
        };
    }
}

/// <summary>
/// 身份验证操作类型枚举
/// </summary>
public enum AuthenticationOperation
{
    /// <summary>
    /// 令牌创建操作
    /// </summary>
    TokenCreation,

    /// <summary>
    /// 令牌验证操作
    /// </summary>
    TokenValidation,

    /// <summary>
    /// 客户端身份验证操作
    /// </summary>
    ClientAuthentication,

    /// <summary>
    /// 凭据验证操作
    /// </summary>
    CredentialValidation,

    /// <summary>
    /// 令牌撤销操作
    /// </summary>
    TokenRevocation,

    /// <summary>
    /// 权限检查操作
    /// </summary>
    PermissionCheck,

    /// <summary>
    /// 会话初始化操作
    /// </summary>
    SessionInitialization,

    /// <summary>
    /// 会话终止操作
    /// </summary>
    SessionTermination
}

/// <summary>
/// 身份验证操作结果枚举
/// </summary>
public enum AuthenticationOutcome
{
    /// <summary>
    /// 操作成功
    /// </summary>
    Success,

    /// <summary>
    /// 操作失败
    /// </summary>
    Failure
}

/// <summary>
/// 备份系统的标准权限定义
/// 定义了系统中所有可用的权限常量
/// </summary>
public static class BackupPermissions
{
    /// <summary>
    /// 上传备份文件的权限
    /// </summary>
    public const string UploadBackup = "backup.upload";
    
    /// <summary>
    /// 下载备份文件的权限
    /// </summary>
    public const string DownloadBackup = "backup.download";
    
    /// <summary>
    /// 删除备份文件的权限
    /// </summary>
    public const string DeleteBackup = "backup.delete";
    
    /// <summary>
    /// 列出备份文件的权限
    /// </summary>
    public const string ListBackups = "backup.list";
    
    /// <summary>
    /// 查看日志的权限
    /// </summary>
    public const string ViewLogs = "logs.view";
    
    /// <summary>
    /// 管理客户端的权限
    /// </summary>
    public const string ManageClients = "clients.manage";
    
    /// <summary>
    /// 系统管理员权限
    /// </summary>
    public const string SystemAdmin = "system.admin";

    /// <summary>
    /// 获取所有可用权限的数组
    /// </summary>
    public static readonly string[] AllPermissions = 
    {
        UploadBackup,
        DownloadBackup,
        DeleteBackup,
        ListBackups,
        ViewLogs,
        ManageClients,
        SystemAdmin
    };

    /// <summary>
    /// 获取备份客户端的默认权限
    /// 包含基本的备份操作权限
    /// </summary>
    public static readonly string[] DefaultClientPermissions = 
    {
        UploadBackup,
        ListBackups,
        ViewLogs
    };
}