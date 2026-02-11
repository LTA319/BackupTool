namespace MySqlBackupTool.Shared.Models;

/// <summary>
/// 数据库初始化配置选项
/// 用于从配置文件中读取默认的数据库初始化设置
/// </summary>
public class DatabaseInitializationOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "DatabaseInitialization";

    /// <summary>
    /// 默认保留策略配置
    /// </summary>
    public RetentionPolicy? DefaultRetentionPolicy { get; set; }

    /// <summary>
    /// 默认备份配置
    /// </summary>
    public BackupConfiguration? DefaultBackupConfiguration { get; set; }

    /// <summary>
    /// 默认调度配置（BackupConfigId 将在创建时自动设置）
    /// </summary>
    public ScheduleConfiguration? DefaultScheduleConfiguration { get; set; }

    /// <summary>
    /// 默认客户端凭据配置
    /// </summary>
    public ClientCredentials? DefaultClientCredentials { get; set; }
}
