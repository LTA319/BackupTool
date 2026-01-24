using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Authorization service implementation
/// </summary>
public class AuthorizationService : IAuthorizationService
{
    private readonly ILogger<AuthorizationService> _logger;
    private readonly ICredentialStorage _credentialStorage;
    private readonly IBackupLogRepository _logRepository;

    // Operation to permission mappings
    private readonly Dictionary<string, string[]> _operationPermissions = new()
    {
        { "upload_backup", new[] { BackupPermissions.UploadBackup } },
        { "download_backup", new[] { BackupPermissions.DownloadBackup } },
        { "delete_backup", new[] { BackupPermissions.DeleteBackup } },
        { "list_backups", new[] { BackupPermissions.ListBackups } },
        { "view_logs", new[] { BackupPermissions.ViewLogs } },
        { "manage_clients", new[] { BackupPermissions.ManageClients } },
        { "system_admin", new[] { BackupPermissions.SystemAdmin } },
        
        // Composite operations requiring multiple permissions
        { "backup_management", new[] { BackupPermissions.UploadBackup, BackupPermissions.ListBackups } },
        { "full_backup_access", new[] { BackupPermissions.UploadBackup, BackupPermissions.DownloadBackup, BackupPermissions.ListBackups } }
    };

    public AuthorizationService(
        ILogger<AuthorizationService> logger,
        ICredentialStorage credentialStorage,
        IBackupLogRepository logRepository)
    {
        _logger = logger;
        _credentialStorage = credentialStorage ?? throw new ArgumentNullException(nameof(credentialStorage));
        _logRepository = logRepository ?? throw new ArgumentNullException(nameof(logRepository));
    }

    /// <summary>
    /// Checks if a client is authorized to perform an operation
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

            // Get required permissions for the operation
            if (!_operationPermissions.TryGetValue(operation.ToLowerInvariant(), out var requiredPermissions))
            {
                _logger.LogWarning("Unknown operation requested: {Operation} by client {ClientId}", operation, context.ClientId);
                await LogAuthorizationAttemptAsync(context, operation, false, "Unknown operation");
                return false;
            }

            // Check if client has system admin permission (grants all access to known operations)
            if (context.HasPermission(BackupPermissions.SystemAdmin))
            {
                await LogAuthorizationAttemptAsync(context, operation, true, "System admin permission");
                return true;
            }

            // Check if client has all required permissions
            var hasAllPermissions = requiredPermissions.All(permission => context.HasPermission(permission));

            if (hasAllPermissions)
            {
                await LogAuthorizationAttemptAsync(context, operation, true, "All required permissions present");
                return true;
            }

            // Log which permissions are missing
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
    /// Checks if a client has a specific permission
    /// </summary>
    public async Task<bool> HasPermissionAsync(AuthorizationContext context, string permission)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        if (string.IsNullOrWhiteSpace(permission))
            throw new ArgumentException("Permission cannot be null or empty", nameof(permission));

        try
        {
            // System admin has all permissions
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
    /// Gets all permissions for a client
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
    /// Logs an authorization attempt
    /// </summary>
    public async Task LogAuthorizationAttemptAsync(AuthorizationContext context, string operation, bool success, string? reason = null)
    {
        if (context == null)
            return;

        try
        {
            // Create a log entry for the authorization attempt
            var logEntry = new BackupLog
            {
                BackupConfigId = 0, // Not applicable for authorization logs
                StartTime = context.RequestTime,
                EndTime = DateTime.UtcNow,
                Status = success ? BackupStatus.Completed : BackupStatus.Failed,
                ErrorMessage = success ? null : reason,
                FilePath = null // Not applicable
            };

            // Add additional context information
            var contextInfo = new Dictionary<string, object>
            {
                { "ClientId", context.ClientId },
                { "Operation", operation },
                { "Success", success },
                { "IPAddress", context.IPAddress ?? "Unknown" },
                { "Permissions", string.Join(", ", context.Permissions) }
            };

            if (!string.IsNullOrEmpty(reason))
            {
                contextInfo["Reason"] = reason;
            }

            // Store additional context in the log entry (this would need to be extended in the actual BackupLog model)
            // For now, we'll include it in the error message for failed attempts
            if (!success && !string.IsNullOrEmpty(reason))
            {
                logEntry.ErrorMessage = $"Authorization failed: {reason}. Client: {context.ClientId}, Operation: {operation}";
            }

            await _logRepository.AddAsync(logEntry);
            await _logRepository.SaveChangesAsync();

            _logger.LogInformation("Logged authorization attempt: Client {ClientId}, Operation {Operation}, Success {Success}", 
                context.ClientId, operation, success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log authorization attempt for client {ClientId}", context.ClientId);
            // Don't throw - logging failures shouldn't break authorization
        }
    }

    /// <summary>
    /// Validates that an operation is supported
    /// </summary>
    public bool IsOperationSupported(string operation)
    {
        return _operationPermissions.ContainsKey(operation.ToLowerInvariant());
    }

    /// <summary>
    /// Gets the required permissions for an operation
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
    /// Adds a new operation-permission mapping
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
    /// Removes an operation-permission mapping
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
    /// Gets all registered operations
    /// </summary>
    public Dictionary<string, string[]> GetAllOperations()
    {
        return new Dictionary<string, string[]>(_operationPermissions);
    }
}