using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Shared.Interfaces;

/// <summary>
/// 管理备份文件存储的接口 / Interface for managing backup file storage
/// 提供备份文件的存储路径管理、空间验证、保留策略应用等功能
/// Provides backup file storage path management, space validation, retention policy application and other functionality
/// </summary>
public interface IStorageManager
{
    /// <summary>
    /// 为备份文件创建存储路径 / Creates a storage path for a backup file
    /// 根据备份元数据生成合适的文件存储路径，包括目录结构和文件命名
    /// Generates appropriate file storage path based on backup metadata, including directory structure and file naming
    /// </summary>
    /// <param name="metadata">备份的元数据信息 / Metadata about the backup</param>
    /// <returns>备份应该存储的路径 / Path where the backup should be stored</returns>
    Task<string> CreateBackupPathAsync(BackupMetadata metadata);

    /// <summary>
    /// 为备份文件创建存储路径，支持自定义目标目录 / Creates a storage path for a backup file with custom target directory support
    /// 根据备份元数据和自定义目标目录生成合适的文件存储路径
    /// Generates appropriate file storage path based on backup metadata and custom target directory
    /// </summary>
    /// <param name="metadata">备份的元数据信息 / Metadata about the backup</param>
    /// <param name="customTargetDirectory">自定义目标目录，为null时使用默认基础路径 / Custom target directory, uses default base path when null</param>
    /// <returns>备份应该存储的路径 / Path where the backup should be stored</returns>
    Task<string> CreateBackupPathAsync(BackupMetadata metadata, string? customTargetDirectory);

    /// <summary>
    /// 验证是否有足够的存储空间可用 / Validates that sufficient storage space is available
    /// 检查指定大小的存储空间是否可用，确保备份操作不会因空间不足而失败
    /// Checks if storage space of specified size is available to ensure backup operations won't fail due to insufficient space
    /// </summary>
    /// <param name="requiredSpace">所需空间大小（字节） / Amount of space required in bytes</param>
    /// <returns>如果有足够空间返回true，否则返回false / True if sufficient space is available, false otherwise</returns>
    Task<bool> ValidateStorageSpaceAsync(long requiredSpace);

    /// <summary>
    /// 应用保留策略管理旧备份文件 / Applies retention policies to manage old backup files
    /// 根据指定的保留策略删除过期的备份文件，释放存储空间
    /// Deletes expired backup files according to specified retention policy to free storage space
    /// </summary>
    /// <param name="retentionPolicy">要应用的保留策略 / The retention policy to apply</param>
    /// <param name="backupDirectory">包含备份文件的目录 / Directory containing backup files</param>
    /// <returns>清理的文件数量 / Number of files cleaned up</returns>
    Task<int> ApplyRetentionPolicyAsync(RetentionPolicy retentionPolicy, string backupDirectory);

    /// <summary>
    /// 获取可用存储空间（字节） / Gets available storage space in bytes
    /// 查询指定路径的可用磁盘空间大小
    /// Queries available disk space size for specified path
    /// </summary>
    /// <param name="path">要检查存储空间的路径 / Path to check storage space for</param>
    /// <returns>可用空间大小（字节） / Available space in bytes</returns>
    Task<long> GetAvailableSpaceAsync(string path);

    /// <summary>
    /// 确保存储目录存在且可访问 / Ensures the storage directory exists and is accessible
    /// 检查并创建必要的目录结构，验证读写权限
    /// Checks and creates necessary directory structure, validates read/write permissions
    /// </summary>
    /// <param name="path">要确保存在的目录路径 / Directory path to ensure</param>
    /// <returns>如果目录可访问返回true，否则返回false / True if directory is accessible, false otherwise</returns>
    Task<bool> EnsureDirectoryAsync(string path);
}