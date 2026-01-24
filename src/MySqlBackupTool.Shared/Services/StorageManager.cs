using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// Manages backup file storage and organization
/// </summary>
public class StorageManager : IStorageManager
{
    private readonly ILogger<StorageManager> _logger;
    private readonly string _baseStoragePath;
    private readonly DirectoryOrganizer _directoryOrganizer;
    private readonly DirectoryOrganizationStrategy _organizationStrategy;

    public StorageManager(
        ILogger<StorageManager> logger, 
        string baseStoragePath = "",
        DirectoryOrganizationStrategy? organizationStrategy = null)
    {
        _logger = logger;
        _baseStoragePath = string.IsNullOrEmpty(baseStoragePath) 
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MySqlBackupTool", "Backups")
            : baseStoragePath;

        // Ensure base storage directory exists
        Directory.CreateDirectory(_baseStoragePath);

        var organizerLogger = new LoggerFactory().CreateLogger<DirectoryOrganizer>();
        _directoryOrganizer = new DirectoryOrganizer(organizerLogger);
        _organizationStrategy = organizationStrategy ?? new DirectoryOrganizationStrategy();
    }

    /// <summary>
    /// Creates a storage path for a backup file
    /// </summary>
    public async Task<string> CreateBackupPathAsync(BackupMetadata metadata)
    {
        try
        {
            // Create directory structure using the organizer
            var targetDirectory = _directoryOrganizer.CreateDirectoryStructure(
                _baseStoragePath, 
                metadata, 
                _organizationStrategy);

            // Generate filename using naming strategy
            var namingStrategy = new FileNamingStrategy();
            var fileName = namingStrategy.GenerateFileName(
                metadata.ServerName,
                metadata.DatabaseName,
                metadata.BackupTime);

            var targetPath = Path.Combine(targetDirectory, fileName);

            // Ensure filename is unique
            targetPath = await EnsureUniqueFilePathAsync(targetPath);

            _logger.LogInformation("Created backup path: {Path}", targetPath);
            return targetPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating backup path for server {ServerName}", metadata.ServerName);
            throw;
        }
    }

    /// <summary>
    /// Validates that sufficient storage space is available
    /// </summary>
    public async Task<bool> ValidateStorageSpaceAsync(long requiredSpace)
    {
        try
        {
            var driveInfo = new DriveInfo(Path.GetPathRoot(_baseStoragePath) ?? "C:");
            var availableSpace = driveInfo.AvailableFreeSpace;

            // Add 10% buffer to required space
            var requiredWithBuffer = (long)(requiredSpace * 1.1);

            var hasSpace = availableSpace >= requiredWithBuffer;

            _logger.LogInformation("Storage validation: Required {RequiredGB:F2} GB, Available {AvailableGB:F2} GB, HasSpace: {HasSpace}",
                requiredWithBuffer / (1024.0 * 1024.0 * 1024.0),
                availableSpace / (1024.0 * 1024.0 * 1024.0),
                hasSpace);

            return hasSpace;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating storage space");
            return false;
        }
    }

    /// <summary>
    /// Applies retention policies to manage old backup files
    /// </summary>
    public async Task<int> ApplyRetentionPolicyAsync(RetentionPolicy retentionPolicy, string backupDirectory)
    {
        try
        {
            if (!retentionPolicy.IsEnabled)
            {
                _logger.LogDebug("Retention policy {PolicyName} is disabled", retentionPolicy.Name);
                return 0;
            }

            _logger.LogInformation("Applying retention policy: {PolicyName} to directory: {Directory}", retentionPolicy.Name, backupDirectory);

            var backupFiles = GetBackupFilesInDirectory(backupDirectory);
            var filesToDelete = new List<FileInfo>();

            // Apply age-based retention
            if (retentionPolicy.MaxAgeDays.HasValue)
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-retentionPolicy.MaxAgeDays.Value);
                var oldFiles = backupFiles.Where(f => f.CreationTimeUtc < cutoffDate).ToList();
                filesToDelete.AddRange(oldFiles);
                
                _logger.LogInformation("Found {Count} files older than {Days} days", oldFiles.Count, retentionPolicy.MaxAgeDays.Value);
            }

            // Apply count-based retention
            if (retentionPolicy.MaxCount.HasValue)
            {
                var sortedFiles = backupFiles.OrderByDescending(f => f.CreationTimeUtc).ToList();
                if (sortedFiles.Count > retentionPolicy.MaxCount.Value)
                {
                    var excessFiles = sortedFiles.Skip(retentionPolicy.MaxCount.Value).ToList();
                    filesToDelete.AddRange(excessFiles);
                    
                    _logger.LogInformation("Found {Count} excess files beyond max count of {MaxCount}", 
                        excessFiles.Count, retentionPolicy.MaxCount.Value);
                }
            }

            // Apply storage-based retention
            if (retentionPolicy.MaxStorageBytes.HasValue)
            {
                var sortedFiles = backupFiles.OrderByDescending(f => f.CreationTimeUtc).ToList();
                long totalSize = 0;
                var filesToKeep = new List<FileInfo>();

                foreach (var file in sortedFiles)
                {
                    if (totalSize + file.Length <= retentionPolicy.MaxStorageBytes.Value)
                    {
                        filesToKeep.Add(file);
                        totalSize += file.Length;
                    }
                    else
                    {
                        filesToDelete.Add(file);
                    }
                }

                _logger.LogInformation("Storage-based retention: keeping {KeepCount} files ({SizeGB:F2} GB), deleting {DeleteCount} files",
                    filesToKeep.Count, totalSize / (1024.0 * 1024.0 * 1024.0), 
                    backupFiles.Count - filesToKeep.Count);
            }

            // Remove duplicates and delete files
            var uniqueFilesToDelete = filesToDelete.Distinct().ToList();
            var deletedCount = 0;
            var deletedSize = 0L;

            foreach (var file in uniqueFilesToDelete)
            {
                try
                {
                    deletedSize += file.Length;
                    file.Delete();
                    deletedCount++;
                    _logger.LogDebug("Deleted backup file: {FilePath}", file.FullName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete backup file: {FilePath}", file.FullName);
                }
            }

            // Clean up empty directories
            await CleanupEmptyDirectoriesAsync(backupDirectory);

            _logger.LogInformation("Retention policy {PolicyName} completed: deleted {Count} files ({SizeGB:F2} GB)",
                retentionPolicy.Name, deletedCount, deletedSize / (1024.0 * 1024.0 * 1024.0));

            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying retention policy {PolicyName}", retentionPolicy.Name);
            throw;
        }
    }

    /// <summary>
    /// Gets available storage space in bytes
    /// </summary>
    public async Task<long> GetAvailableSpaceAsync(string path)
    {
        try
        {
            var driveInfo = new DriveInfo(Path.GetPathRoot(path) ?? "C:");
            return driveInfo.AvailableFreeSpace;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available space for path: {Path}", path);
            return 0;
        }
    }

    /// <summary>
    /// Ensures the storage directory exists and is accessible
    /// </summary>
    public async Task<bool> EnsureDirectoryAsync(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                _logger.LogInformation("Created directory: {Path}", path);
            }

            // Test write access
            var testFile = Path.Combine(path, $"test_{Guid.NewGuid()}.tmp");
            await File.WriteAllTextAsync(testFile, "test");
            File.Delete(testFile);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring directory: {Path}", path);
            return false;
        }
    }

    /// <summary>
    /// Gets all backup files in the storage directory
    /// </summary>
    private List<System.IO.FileInfo> GetAllBackupFiles()
    {
        return GetBackupFilesInDirectory(_baseStoragePath);
    }

    /// <summary>
    /// Gets all backup files in a specific directory
    /// </summary>
    private List<System.IO.FileInfo> GetBackupFilesInDirectory(string directory)
    {
        var backupFiles = new List<System.IO.FileInfo>();

        if (!Directory.Exists(directory))
        {
            return backupFiles;
        }

        try
        {
            var files = Directory.GetFiles(directory, "*.zip", SearchOption.AllDirectories);
            backupFiles.AddRange(files.Select(f => new FileInfo(f)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error enumerating backup files in {Path}", directory);
        }

        return backupFiles;
    }

    /// <summary>
    /// Ensures the file path is unique by adding a suffix if necessary
    /// </summary>
    private async Task<string> EnsureUniqueFilePathAsync(string originalPath)
    {
        if (!File.Exists(originalPath))
        {
            return originalPath;
        }

        var directory = Path.GetDirectoryName(originalPath) ?? "";
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalPath);
        var extension = Path.GetExtension(originalPath);

        var counter = 1;
        string uniquePath;

        do
        {
            var uniqueFileName = $"{fileNameWithoutExtension}_{counter:D3}{extension}";
            uniquePath = Path.Combine(directory, uniqueFileName);
            counter++;
        }
        while (File.Exists(uniquePath) && counter < 1000); // Prevent infinite loop

        if (counter >= 1000)
        {
            throw new InvalidOperationException($"Unable to create unique filename for {originalPath}");
        }

        return uniquePath;
    }

    /// <summary>
    /// Recursively cleans up empty directories
    /// </summary>
    private async Task CleanupEmptyDirectoriesAsync(string rootPath)
    {
        try
        {
            var directories = Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories)
                .OrderByDescending(d => d.Length) // Process deepest directories first
                .ToList();

            foreach (var directory in directories)
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        Directory.Delete(directory);
                        _logger.LogDebug("Deleted empty directory: {Directory}", directory);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not delete directory: {Directory}", directory);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error cleaning up empty directories in {RootPath}", rootPath);
        }
    }
}