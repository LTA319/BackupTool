using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// Client credentials for authentication
/// </summary>
public class ClientCredentials
{
    [Required(ErrorMessage = "Client ID is required")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Client ID must be between 3 and 100 characters")]
    public string ClientId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Client secret is required")]
    [StringLength(256, MinimumLength = 8, ErrorMessage = "Client secret must be between 8 and 256 characters")]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Optional client name for identification
    /// </summary>
    [StringLength(200)]
    public string? ClientName { get; set; }

    /// <summary>
    /// Client permissions/roles
    /// </summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// Whether this client is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When this client was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this client expires (optional)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Validates if the client credentials are expired
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;

    /// <summary>
    /// Generates a secure hash of the client secret for storage
    /// </summary>
    public string HashSecret()
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(ClientSecret));
        return Convert.ToBase64String(hashedBytes);
    }

    /// <summary>
    /// Verifies a client secret against the stored hash
    /// </summary>
    public bool VerifySecret(string providedSecret, string storedHash)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(providedSecret));
        var providedHash = Convert.ToBase64String(hashedBytes);
        return string.Equals(providedHash, storedHash, StringComparison.Ordinal);
    }
}

/// <summary>
/// Authentication request from client
/// </summary>
public class AuthenticationRequest
{
    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of the request (for replay attack prevention)
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Nonce for request uniqueness
    /// </summary>
    public string Nonce { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Authentication response from server
/// </summary>
public class AuthenticationResponse
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Authentication token for subsequent requests
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Token expiration time
    /// </summary>
    public DateTime? TokenExpiresAt { get; set; }

    /// <summary>
    /// Client permissions granted
    /// </summary>
    public List<string> Permissions { get; set; } = new();
}

/// <summary>
/// Authentication token for session management
/// </summary>
public class AuthenticationToken
{
    [Required]
    public string TokenId { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string ClientId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);

    public List<string> Permissions { get; set; } = new();

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Last time this token was used
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// IP address where token was issued
    /// </summary>
    public string? IssuerIPAddress { get; set; }

    /// <summary>
    /// Validates if the token is expired
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    /// <summary>
    /// Validates if the token is valid for use
    /// </summary>
    public bool IsValid => IsActive && !IsExpired;

    /// <summary>
    /// Updates the last used timestamp
    /// </summary>
    public void UpdateLastUsed()
    {
        LastUsedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Secure credential storage configuration
/// </summary>
public class CredentialStorageConfig
{
    /// <summary>
    /// Path to the encrypted credentials file
    /// </summary>
    [Required]
    public string CredentialsFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Encryption key for credential storage (should be derived from user input or system key)
    /// </summary>
    [Required]
    public string EncryptionKey { get; set; } = string.Empty;

    /// <summary>
    /// Whether to use Windows DPAPI for additional encryption (Windows only)
    /// </summary>
    public bool UseWindowsDPAPI { get; set; } = true;

    /// <summary>
    /// Maximum number of authentication attempts before lockout
    /// </summary>
    public int MaxAuthenticationAttempts { get; set; } = 5;

    /// <summary>
    /// Lockout duration in minutes
    /// </summary>
    public int LockoutDurationMinutes { get; set; } = 15;
}

/// <summary>
/// Authorization context for requests
/// </summary>
public class AuthorizationContext
{
    public string ClientId { get; set; } = string.Empty;

    public List<string> Permissions { get; set; } = new();

    public string? IPAddress { get; set; }

    public DateTime RequestTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The operation being requested
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Additional context data for authorization decisions
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();

    /// <summary>
    /// Checks if the context has a specific permission
    /// </summary>
    public bool HasPermission(string permission)
    {
        return Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the context has any of the specified permissions
    /// </summary>
    public bool HasAnyPermission(params string[] permissions)
    {
        return permissions.Any(p => HasPermission(p));
    }

    /// <summary>
    /// Checks if the context has all of the specified permissions
    /// </summary>
    public bool HasAllPermissions(params string[] permissions)
    {
        return permissions.All(p => HasPermission(p));
    }
}

/// <summary>
/// Standard permissions for the backup system
/// </summary>
public static class BackupPermissions
{
    public const string UploadBackup = "backup.upload";
    public const string DownloadBackup = "backup.download";
    public const string DeleteBackup = "backup.delete";
    public const string ListBackups = "backup.list";
    public const string ViewLogs = "logs.view";
    public const string ManageClients = "clients.manage";
    public const string SystemAdmin = "system.admin";

    /// <summary>
    /// Gets all available permissions
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
    /// Gets default permissions for a backup client
    /// </summary>
    public static readonly string[] DefaultClientPermissions = 
    {
        UploadBackup,
        ListBackups,
        ViewLogs
    };
}