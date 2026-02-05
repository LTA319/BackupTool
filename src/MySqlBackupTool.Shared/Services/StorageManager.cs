using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Services;

/// <summary>
/// 备份文件存储和组织管理器 / Manages backup file storage and organization
/// 提供备份路径创建、存储空间验证、保留策略应用和目录管理功能 / Provides backup path creation, storage space validation, retention policy application, and directory management
/// </summary>
public class StorageManager : IStorageManager
{
    private readonly ILogger<StorageManager> _logger;
    private readonly string _baseStoragePath; // 基础存储路径 / Base storage path
    private readonly DirectoryOrganizer _directoryOrganizer; // 目录组织器 / Directory organizer
    private readonly DirectoryOrganizationStrategy _organizationStrategy; // 组织策略 / Organization strategy

    /// <summary>
    /// 构造函数，初始化存储管理器 / Constructor, initializes storage manager
    /// </summary>
    /// <param name="logger">日志服务 / Logger service</param>
    /// <param name="baseStoragePath">基础存储路径，为空时使用默认路径 / Base storage path, uses default if empty</param>
    /// <param name="organizationStrategy">目录组织策略，为空时使用默认策略 / Directory organization strategy, uses default if null</param>
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
    /// 为备份文件创建存储路径 / Creates a storage path for a backup file
    /// 使用目录组织器创建目录结构，生成唯一的文件名 / Uses directory organizer to create directory structure and generates unique filename
    /// </summary>
    /// <param name="metadata">备份元数据 / Backup metadata</param>
    /// <returns>创建的备份文件路径 / Created backup file path</returns>
    public async Task<string> CreateBackupPathAsync(BackupMetadata metadata)
    {
        return await CreateBackupPathAsync(metadata, null);
    }

    /// <summary>
    /// 为备份文件创建存储路径，支持自定义目标目录 / Creates a storage path for a backup file with custom target directory support
    /// 使用目录组织器创建目录结构，生成唯一的文件名 / Uses directory organizer to create directory structure and generates unique filename
    /// </summary>
    /// <param name="metadata">备份元数据 / Backup metadata</param>
    /// <param name="customTargetDirectory">自定义目标目录，为null时使用默认基础路径 / Custom target directory, uses default base path when null</param>
    /// <returns>创建的备份文件路径 / Created backup file path</returns>
    public async Task<string> CreateBackupPathAsync(BackupMetadata metadata, string? customTargetDirectory)
    {
        try
        {
            // Use custom target directory if provided, otherwise use base storage path
            var baseDirectory = !string.IsNullOrWhiteSpace(customTargetDirectory) 
                ? customTargetDirectory 
                : _baseStoragePath;

            // Ensure the base directory exists
            if (!Directory.Exists(baseDirectory))
            {
                Directory.CreateDirectory(baseDirectory);
                _logger.LogInformation("Created custom target directory: {Directory}", baseDirectory);
            }

            // Create directory structure using the organizer
            var targetDirectory = _directoryOrganizer.CreateDirectoryStructure(
                baseDirectory, 
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

            _logger.LogInformation("Created backup path: {Path} (using {DirectoryType} directory)", 
                targetPath, 
                !string.IsNullOrWhiteSpace(customTargetDirectory) ? "custom" : "default");
            return targetPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating backup path for server {ServerName}", metadata.ServerName);
            throw;
        }
    }

    /// <summary>
    /// 验证是否有足够的存储空间可用 / Validates that sufficient storage space is available
    /// 检查磁盘可用空间，并添加10%的缓冲区 / Checks available disk space and adds 10% buffer
    /// </summary>
    /// <param name="requiredSpace">所需空间（字节） / Required space in bytes</param>
    /// <returns>如果有足够空间返回true，否则返回false / Returns true if sufficient space available, false otherwise</returns>
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
    /// 应用保留策略来管理旧备份文件 / Applies retention policies to manage old backup files
    /// 根据年龄、数量和存储大小限制删除过期的备份文件 / Deletes expired backup files based on age, count, and storage size limits
    /// </summary>
    /// <param name="retentionPolicy">保留策略 / Retention policy</param>
    /// <param name="backupDirectory">备份目录 / Backup directory</param>
    /// <returns>删除的文件数量 / Number of files deleted</returns>
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
                var cutoffDate = DateTime.Now.AddDays(-retentionPolicy.MaxAgeDays.Value);
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
    /// 获取指定路径的可用存储空间（字节） / Gets available storage space in bytes for specified path
    /// </summary>
    /// <param name="path">路径 / Path</param>
    /// <returns>可用空间字节数 / Available space in bytes</returns>
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
    /// 确保存储目录存在且可访问 / Ensures the storage directory exists and is accessible
    /// 创建目录（如果不存在）并测试写入权限 / Creates directory if it doesn't exist and tests write permissions
    /// </summary>
    /// <param name="path">目录路径 / Directory path</param>
    /// <returns>如果目录可用返回true，否则返回false / Returns true if directory is available, false otherwise</returns>
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
    /// 获取存储目录中的所有备份文件
    /// Gets all backup files in the storage directory
    /// </summary>
    private List<System.IO.FileInfo> GetAllBackupFiles()
    {
        return GetBackupFilesInDirectory(_baseStoragePath);
    }

    /// <summary>
    /// 获取指定目录中的所有备份文件
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
    /// 通过添加后缀确保文件路径唯一
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
    /// 递归清理空目录
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