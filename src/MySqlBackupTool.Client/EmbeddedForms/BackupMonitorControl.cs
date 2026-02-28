using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.ComponentModel;

namespace MySqlBackupTool.Client.EmbeddedForms;

/// <summary>
/// 备份监控和控制控件
/// 提供备份操作的实时监控、启动、取消等功能
/// </summary>
public partial class BackupMonitorControl : UserControl, IEmbeddedForm
{
    #region 私有字段

    /// <summary>
    /// 依赖注入服务提供者，用于获取各种服务实例
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 日志记录器，用于记录备份监控控件的操作和错误信息
    /// </summary>
    private readonly ILogger<BackupMonitorControl> _logger;

    /// <summary>
    /// 备份配置仓储接口，用于获取备份配置信息
    /// </summary>
    private readonly IBackupConfigurationRepository _configRepository;

    /// <summary>
    /// 备份日志仓储接口，用于获取备份日志信息
    /// </summary>
    private readonly IBackupLogRepository _logRepository;

    /// <summary>
    /// 备份编排器接口，用于执行备份操作（可能在某些配置中不可用）
    /// </summary>
    private readonly IBackupOrchestrator? _backupOrchestrator;

    /// <summary>
    /// 备份配置列表，存储所有可用的备份配置
    /// </summary>
    private List<BackupConfiguration> _configurations = new();

    /// <summary>
    /// 正在运行的备份列表，存储当前正在执行的备份操作
    /// </summary>
    private List<BackupLog> _runningBackups = new();

    /// <summary>
    /// 当前备份操作的取消令牌源，用于取消正在进行的备份
    /// </summary>
    private CancellationTokenSource? _currentBackupCancellation;

    /// <summary>
    /// 当前备份进度信息，包含进度百分比、操作描述等
    /// </summary>
    private BackupProgress? _currentProgress;

    /// <summary>
    /// 刷新定时器，用于定期更新界面数据
    /// </summary>
    private System.Windows.Forms.Timer? _refreshTimer;

    /// <summary>
    /// 刷新操作进行中标志，防止重叠的刷新操作
    /// </summary>
    private bool _refreshInProgress = false;

    #endregion

    #region IEmbeddedForm 实现

    /// <summary>
    /// 获取嵌入式窗体的显示标题
    /// </summary>
    public string Title => "备份监控与控制";

    /// <summary>
    /// 获取导航路径用于面包屑显示
    /// </summary>
    public string NavigationPath => "工具 > 备份监控";

    /// <summary>
    /// 当窗体请求关闭时触发的事件
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// 当窗体标题改变时触发的事件
    /// </summary>
    public event EventHandler<string>? TitleChanged;

    /// <summary>
    /// 当窗体状态消息改变时触发的事件
    /// </summary>
    public event EventHandler<string>? StatusChanged;

    /// <summary>
    /// 当窗体被激活（显示）时调用
    /// </summary>
    public void OnActivated()
    {
        try
        {
            SetupRefreshTimer();
            _ = LoadDataAsync();
            _logger.LogInformation("备份监控控件已激活");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "激活备份监控控件时发生错误");
        }
    }

    /// <summary>
    /// 当窗体被停用（隐藏）时调用
    /// </summary>
    public void OnDeactivated()
    {
        try
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            _refreshTimer = null;
            _logger.LogInformation("备份监控控件已停用");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停用备份监控控件时发生错误");
        }
    }

    /// <summary>
    /// 检查窗体是否可以关闭
    /// </summary>
    /// <returns>如果窗体可以关闭则返回true，否则返回false</returns>
    public bool CanClose()
    {
        // 如果有正在进行的备份，询问用户是否确认关闭
        if (_currentBackupCancellation != null)
        {
            var result = MessageBox.Show(
                "有备份操作正在进行中。关闭将取消备份操作。\n\n确定要关闭吗？",
                "确认关闭",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                _currentBackupCancellation?.Cancel();
                return true;
            }
            return false;
        }
        return true;
    }

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化BackupMonitorControl类的新实例
    /// </summary>
    /// <param name="serviceProvider">依赖注入服务提供者</param>
    public BackupMonitorControl(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<BackupMonitorControl>>();
        _configRepository = serviceProvider.GetRequiredService<IBackupConfigurationRepository>();
        _logRepository = serviceProvider.GetRequiredService<IBackupLogRepository>();
        
        // 尝试获取备份编排器（在某些配置中可能不可用）
        _backupOrchestrator = serviceProvider.GetService<IBackupOrchestrator>();

        InitializeComponent();
        InitializeControl();
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 初始化控件的基本设置和属性
    /// </summary>
    private void InitializeControl()
    {
        try
        {
            this.Dock = DockStyle.Fill;
            
            _logger.LogInformation("备份监控控件初始化成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化备份监控控件时发生错误");
            MessageBox.Show($"初始化控件时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 设置刷新定时器
    /// 配置定时器每2秒刷新一次数据
    /// </summary>
    private void SetupRefreshTimer()
    {
        if (_refreshTimer != null)
            return;

        _refreshTimer = new System.Windows.Forms.Timer();
        _refreshTimer.Interval = 10000; // 每2秒刷新一次
        _refreshTimer.Tick += RefreshTimer_Tick;
        _refreshTimer.Start();
    }

    /// <summary>
    /// 异步加载所有数据
    /// 包括备份配置和正在运行的备份信息
    /// </summary>
    /// <returns>异步任务</returns>
    private async Task LoadDataAsync()
    {
        try
        {
            // 显示加载状态
            if (InvokeRequired)
            {
                Invoke(new Action(() => {
                    lblStatus.Text = "正在加载数据...";
                    lblStatus.ForeColor = Color.Blue;
                    StatusChanged?.Invoke(this, "正在加载数据...");
                }));
            }
            else
            {
                lblStatus.Text = "正在加载数据...";
                lblStatus.ForeColor = Color.Blue;
                StatusChanged?.Invoke(this, "正在加载数据...");
            }

            await LoadConfigurations();
            await LoadRunningBackups();
            
            // 在主线程上更新UI
            if (InvokeRequired)
            {
                Invoke(new Action(UpdateStatus));
            }
            else
            {
                UpdateStatus();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载数据时发生错误");
            
            // 在主线程上更新UI
            if (InvokeRequired)
            {
                Invoke(new Action(() => {
                    lblStatus.Text = $"加载数据时发生错误: {ex.Message}";
                    lblStatus.ForeColor = Color.Red;
                    StatusChanged?.Invoke(this, $"加载数据时发生错误: {ex.Message}");
                }));
            }
            else
            {
                lblStatus.Text = $"加载数据时发生错误: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
                StatusChanged?.Invoke(this, $"加载数据时发生错误: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 同步加载数据的包装方法
    /// </summary>
    private async void LoadData()
    {
        await LoadDataAsync();
    }

    /// <summary>
    /// 从数据库加载备份配置列表
    /// 获取所有激活的备份配置并更新界面
    /// </summary>
    /// <returns>异步任务</returns>
    private async Task LoadConfigurations()
    {
        try
        {
            // 添加超时以防止挂起
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            _configurations = (await _configRepository.GetActiveConfigurationsAsync()).ToList();
            
            // 在主线程上更新UI
            if (InvokeRequired)
            {
                Invoke(new Action(() => dgvConfigurations.DataSource = _configurations));
            }
            else
            {
                dgvConfigurations.DataSource = _configurations;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("加载配置超时（10秒）");
            throw new TimeoutException("加载配置超时。请检查数据库连接。");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从数据库加载配置时发生错误");
            throw;
        }
    }

    /// <summary>
    /// 从数据库加载正在运行的备份列表
    /// 获取所有正在进行的备份操作并更新界面
    /// </summary>
    /// <returns>异步任务</returns>
    private async Task LoadRunningBackups()
    {
        try
        {
            // 添加超时以防止挂起
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            _runningBackups = (await _logRepository.GetRunningBackupsAsync()).ToList();
            
            // 在主线程上更新UI
            if (InvokeRequired)
            {
                Invoke(new Action(() => dgvRunningBackups.DataSource = _runningBackups));
            }
            else
            {
                dgvRunningBackups.DataSource = _runningBackups;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("加载正在运行的备份超时（10秒）");
            throw new TimeoutException("加载正在运行的备份超时。请检查数据库连接。");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从数据库加载正在运行的备份时发生错误");
            throw;
        }
    }

    /// <summary>
    /// 更新状态栏和进度显示
    /// 显示激活配置数量、正在运行的备份数量和当前进度
    /// </summary>
    private void UpdateStatus()
    {
        var activeConfigs = _configurations.Count(c => c.IsActive);
        var runningBackups = _runningBackups.Count;

        lblStatus.Text = $"激活配置: {activeConfigs} | 正在运行的备份: {runningBackups}";
        lblStatus.ForeColor = runningBackups > 0 ? Color.Blue : Color.Green;
        StatusChanged?.Invoke(this, lblStatus.Text);

        // 更新进度显示
        if (_currentProgress != null)
        {
            progressBar.Value = (int)Math.Min(100, Math.Max(0, _currentProgress.PercentComplete));
            lblProgressDetails.Text = $"{_currentProgress.CurrentOperation} - {_currentProgress.PercentComplete:F1}% " +
                                    $"({_currentProgress.TransferRateString})";
            
            if (_currentProgress.EstimatedTimeRemaining.HasValue)
            {
                var eta = _currentProgress.EstimatedTimeRemaining.Value;
                lblProgressDetails.Text += $" - 预计剩余时间: {eta.Hours:D2}:{eta.Minutes:D2}:{eta.Seconds:D2}";
            }
        }
        else
        {
            progressBar.Value = 0;
            lblProgressDetails.Text = "没有正在进行的备份操作";
        }
    }

    /// <summary>
    /// 获取当前备份日志ID
    /// 返回最近的正在运行的备份的ID
    /// </summary>
    /// <returns>备份日志ID</returns>
    private int GetCurrentBackupLogId()
    {
        // 这需要根据备份日志的跟踪方式来实现
        // 目前返回最近的正在运行的备份
        return _runningBackups.FirstOrDefault()?.Id ?? 0;
    }

    /// <summary>
    /// 格式化文件大小显示
    /// 将字节数转换为可读的文件大小格式（B, KB, MB, GB, TB）
    /// </summary>
    /// <param name="bytes">文件大小（字节）</param>
    /// <returns>格式化的文件大小字符串</returns>
    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// 将异常转换为用户友好的错误消息
    /// 特别处理身份验证相关的错误，提供清晰的解决建议
    /// </summary>
    /// <param name="exception">发生的异常</param>
    /// <returns>用户友好的错误消息</returns>
    private string GetUserFriendlyErrorMessage(Exception exception)
    {
        // Check for authentication-related error messages
        var message = exception.Message;
        var innerMessage = exception.InnerException?.Message ?? "";
        var fullMessage = $"{message} {innerMessage}".ToLower();

        // Handle authentication token failures
        if (fullMessage.Contains("authentication token") || 
            fullMessage.Contains("failed to obtain authentication token") ||
            fullMessage.Contains("备份失败，failed to obtain authentication token"))
        {
            return "身份验证失败：无法获取身份验证令牌。\n\n" +
                   "可能的原因：\n" +
                   "• 客户端凭据配置不正确\n" +
                   "• 服务器连接问题\n" +
                   "• 默认凭据未正确初始化\n\n" +
                   "建议解决方案：\n" +
                   "• 检查备份配置中的客户端ID和密钥设置\n" +
                   "• 确认服务器正在运行并可访问\n" +
                   "• 重新启动应用程序以重新初始化默认凭据";
        }

        // Handle invalid credentials
        if (fullMessage.Contains("invalid credentials") || 
            fullMessage.Contains("authentication failed") ||
            fullMessage.Contains("凭据无效") ||
            fullMessage.Contains("身份验证失败"))
        {
            return "身份验证失败：提供的凭据无效。\n\n" +
                   "可能的原因：\n" +
                   "• 客户端ID或密钥不正确\n" +
                   "• 凭据已过期或被禁用\n" +
                   "• 服务器端凭据存储问题\n\n" +
                   "建议解决方案：\n" +
                   "• 验证备份配置中的客户端凭据\n" +
                   "• 联系系统管理员检查服务器端凭据设置\n" +
                   "• 尝试使用默认凭据重新配置";
        }

        // Handle malformed token errors
        if (fullMessage.Contains("malformed") || 
            fullMessage.Contains("invalid token format") ||
            fullMessage.Contains("token format") ||
            fullMessage.Contains("令牌格式"))
        {
            return "身份验证失败：令牌格式错误。\n\n" +
                   "这是一个系统内部错误，可能的原因：\n" +
                   "• 凭据编码过程中出现问题\n" +
                   "• 客户端和服务器版本不兼容\n\n" +
                   "建议解决方案：\n" +
                   "• 重新启动客户端应用程序\n" +
                   "• 检查客户端和服务器是否为相同版本\n" +
                   "• 如果问题持续，请联系技术支持";
        }

        // Handle connection/network errors
        if (fullMessage.Contains("connection") || 
            fullMessage.Contains("network") ||
            fullMessage.Contains("timeout") ||
            fullMessage.Contains("连接") ||
            fullMessage.Contains("网络") ||
            fullMessage.Contains("超时"))
        {
            return "连接错误：无法连接到备份服务器。\n\n" +
                   "可能的原因：\n" +
                   "• 服务器未运行或不可访问\n" +
                   "• 网络连接问题\n" +
                   "• 防火墙阻止连接\n\n" +
                   "建议解决方案：\n" +
                   "• 确认备份服务器正在运行\n" +
                   "• 检查网络连接和服务器地址配置\n" +
                   "• 验证防火墙设置允许连接";
        }

        // Handle permission errors
        if (fullMessage.Contains("permission") || 
            fullMessage.Contains("unauthorized") ||
            fullMessage.Contains("access denied") ||
            fullMessage.Contains("权限") ||
            fullMessage.Contains("未授权") ||
            fullMessage.Contains("访问被拒绝"))
        {
            return "权限错误：没有执行此操作的权限。\n\n" +
                   "可能的原因：\n" +
                   "• 客户端权限不足\n" +
                   "• 目标目录访问权限问题\n" +
                   "• 服务器端权限配置错误\n\n" +
                   "建议解决方案：\n" +
                   "• 联系系统管理员检查客户端权限\n" +
                   "• 确认目标备份目录的访问权限\n" +
                   "• 尝试以管理员身份运行应用程序";
        }

        // For other errors, return the original message with some context
        return $"操作失败：{exception.Message}\n\n" +
               "如果问题持续存在，请：\n" +
               "• 检查应用程序日志获取详细信息\n" +
               "• 确认所有服务正常运行\n" +
               "• 联系技术支持获取帮助";
    }

    /// <summary>
    /// 将备份结果错误消息转换为用户友好的错误消息
    /// 特别处理身份验证相关的错误，提供清晰的解决建议
    /// </summary>
    /// <param name="errorMessage">备份操作的错误消息</param>
    /// <returns>用户友好的错误消息</returns>
    private string GetUserFriendlyBackupErrorMessage(string? errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
        {
            return "备份操作失败，但未提供具体错误信息。\n\n" +
                   "建议解决方案：\n" +
                   "• 检查应用程序日志获取详细信息\n" +
                   "• 确认备份服务器正常运行\n" +
                   "• 验证备份配置设置";
        }

        var message = errorMessage.ToLower();

        // Handle authentication token failures
        if (message.Contains("authentication token") || 
            message.Contains("failed to obtain authentication token") ||
            message.Contains("备份失败，failed to obtain authentication token"))
        {
            return "身份验证失败：无法获取身份验证令牌。\n\n" +
                   "可能的原因：\n" +
                   "• 客户端凭据配置不正确\n" +
                   "• 服务器连接问题\n" +
                   "• 默认凭据未正确初始化\n\n" +
                   "建议解决方案：\n" +
                   "• 检查备份配置中的客户端ID和密钥设置\n" +
                   "• 确认服务器正在运行并可访问\n" +
                   "• 重新启动应用程序以重新初始化默认凭据";
        }

        // Handle invalid credentials
        if (message.Contains("invalid credentials") || 
            message.Contains("authentication failed") ||
            message.Contains("凭据无效") ||
            message.Contains("身份验证失败"))
        {
            return "身份验证失败：提供的凭据无效。\n\n" +
                   "可能的原因：\n" +
                   "• 客户端ID或密钥不正确\n" +
                   "• 凭据已过期或被禁用\n" +
                   "• 服务器端凭据存储问题\n\n" +
                   "建议解决方案：\n" +
                   "• 验证备份配置中的客户端凭据\n" +
                   "• 联系系统管理员检查服务器端凭据设置\n" +
                   "• 尝试使用默认凭据重新配置";
        }

        // Handle malformed token errors
        if (message.Contains("malformed") || 
            message.Contains("invalid token format") ||
            message.Contains("token format") ||
            message.Contains("令牌格式"))
        {
            return "身份验证失败：令牌格式错误。\n\n" +
                   "这是一个系统内部错误，可能的原因：\n" +
                   "• 凭据编码过程中出现问题\n" +
                   "• 客户端和服务器版本不兼容\n\n" +
                   "建议解决方案：\n" +
                   "• 重新启动客户端应用程序\n" +
                   "• 检查客户端和服务器是否为相同版本\n" +
                   "• 如果问题持续，请联系技术支持";
        }

        // Handle connection/network errors
        if (message.Contains("connection") || 
            message.Contains("network") ||
            message.Contains("timeout") ||
            message.Contains("连接") ||
            message.Contains("网络") ||
            message.Contains("超时"))
        {
            return "连接错误：无法连接到备份服务器。\n\n" +
                   "可能的原因：\n" +
                   "• 服务器未运行或不可访问\n" +
                   "• 网络连接问题\n" +
                   "• 防火墙阻止连接\n\n" +
                   "建议解决方案：\n" +
                   "• 确认备份服务器正在运行\n" +
                   "• 检查网络连接和服务器地址配置\n" +
                   "• 验证防火墙设置允许连接";
        }

        // Handle permission errors
        if (message.Contains("permission") || 
            message.Contains("unauthorized") ||
            message.Contains("access denied") ||
            message.Contains("权限") ||
            message.Contains("未授权") ||
            message.Contains("访问被拒绝"))
        {
            return "权限错误：没有执行此操作的权限。\n\n" +
                   "可能的原因：\n" +
                   "• 客户端权限不足\n" +
                   "• 目标目录访问权限问题\n" +
                   "• 服务器端权限配置错误\n\n" +
                   "建议解决方案：\n" +
                   "• 联系系统管理员检查客户端权限\n" +
                   "• 确认目标备份目录的访问权限\n" +
                   "• 尝试以管理员身份运行应用程序";
        }

        // For other errors, return the original message with some context
        return $"备份失败：{errorMessage}\n\n" +
               "如果问题持续存在，请：\n" +
               "• 检查应用程序日志获取详细信息\n" +
               "• 确认所有服务正常运行\n" +
               "• 联系技术支持获取帮助";
    }

    #endregion

    #region 事件处理程序

    /// <summary>
    /// 配置网格单元格格式化事件处理程序
    /// 自定义显示MySQL主机和目标服务器信息
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">单元格格式化事件参数</param>
    private void DgvConfigurations_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (dgvConfigurations.Rows[e.RowIndex].DataBoundItem is BackupConfiguration config)
        {
            switch (dgvConfigurations.Columns[e.ColumnIndex].Name)
            {
                case "MySQLHost":
                    e.Value = config.MySQLConnection?.Host ?? "";
                    break;
                case "TargetServer":
                    e.Value = config.TargetServer?.IPAddress ?? "";
                    break;
            }
        }
    }

    /// <summary>
    /// 正在运行的备份网格单元格格式化事件处理程序
    /// 自定义显示配置名称、持续时间和进度信息
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">单元格格式化事件参数</param>
    private void DgvRunningBackups_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (dgvRunningBackups.Rows[e.RowIndex].DataBoundItem is BackupLog log)
        {
            switch (dgvRunningBackups.Columns[e.ColumnIndex].Name)
            {
                case "ConfigName":
                    var config = _configurations.FirstOrDefault(c => c.Id == log.BackupConfigId);
                    e.Value = config?.Name ?? "未知";
                    break;
                case "Duration":
                    var duration = log.Duration ?? (DateTime.Now - log.StartTime);
                    e.Value = $"{duration.Hours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
                    break;
                case "Progress":
                    if (_currentProgress != null && log.Id == GetCurrentBackupLogId())
                    {
                        e.Value = $"{_currentProgress.PercentComplete:F1}%";
                    }
                    else
                    {
                        e.Value = "不可用";
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// 配置网格选择变化事件处理程序
    /// 根据选择的配置更新开始备份按钮的可用状态
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void DgvConfigurations_SelectionChanged(object? sender, EventArgs e)
    {
        var hasSelection = dgvConfigurations.SelectedRows.Count > 0;
        var hasActiveSelection = false;

        if (hasSelection && dgvConfigurations.SelectedRows[0].DataBoundItem is BackupConfiguration config)
        {
            hasActiveSelection = config.IsActive;
        }

        btnStartBackup.Enabled = hasActiveSelection && _backupOrchestrator != null && _currentBackupCancellation == null;
    }

    /// <summary>
    /// 正在运行的备份网格选择变化事件处理程序
    /// 根据选择更新取消备份按钮的可用状态
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void DgvRunningBackups_SelectionChanged(object? sender, EventArgs e)
    {
        var hasSelection = dgvRunningBackups.SelectedRows.Count > 0;
        btnCancelBackup.Enabled = hasSelection && _currentBackupCancellation != null;
    }

    /// <summary>
    /// 刷新定时器触发事件处理程序
    /// 定期刷新正在运行的备份数据和状态显示
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        // 防止重叠的刷新操作
        if (_refreshInProgress)
            return;
            
        _refreshInProgress = true;
        
        try
        {
            await LoadRunningBackups();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新数据时发生错误");
            // 在自动刷新期间不显示错误消息框以避免干扰
            lblStatus.Text = $"刷新错误: {ex.Message}";
            lblStatus.ForeColor = Color.Red;
            StatusChanged?.Invoke(this, $"刷新错误: {ex.Message}");
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    /// <summary>
    /// 开始备份按钮点击事件处理程序
    /// 启动选定配置的备份操作
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private async void btnStartBackup_Click(object sender, EventArgs e)
    {
        try
        {
            if (dgvConfigurations.SelectedRows.Count == 0)
                return;

            var selectedConfig = dgvConfigurations.SelectedRows[0].DataBoundItem as BackupConfiguration;
            if (selectedConfig == null || _backupOrchestrator == null)
                return;

            if (_currentBackupCancellation != null)
            {
                MessageBox.Show("备份操作已在进行中。", "备份进行中", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 确认开始备份
            var result = MessageBox.Show(
                $"开始配置 '{selectedConfig.Name}' 的备份?",
                "确认备份",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            // 开始备份操作
            _currentBackupCancellation = new CancellationTokenSource();
            btnStartBackup.Enabled = false;
            btnCancelBackup.Enabled = true;

            var progress = new Progress<BackupProgress>(OnBackupProgress);

            try
            {
                lblStatus.Text = $"正在启动 '{selectedConfig.Name}' 的备份...";
                lblStatus.ForeColor = Color.Blue;
                StatusChanged?.Invoke(this, lblStatus.Text);

                var backupResult = await _backupOrchestrator.ExecuteBackupAsync(
                    selectedConfig, 
                    progress, 
                    _currentBackupCancellation.Token);

                if (backupResult.Success)
                {
                    lblStatus.Text = $"'{selectedConfig.Name}' 备份成功完成";
                    lblStatus.ForeColor = Color.Green;
                    StatusChanged?.Invoke(this, lblStatus.Text);
                    
                    MessageBox.Show($"备份成功完成!\n\n文件: {backupResult.BackupFilePath}\n大小: {FormatFileSize(backupResult.FileSize)}\n持续时间: {backupResult.Duration:hh\\:mm\\:ss}", 
                        "备份完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    lblStatus.Text = $"'{selectedConfig.Name}' 备份失败: {backupResult.ErrorMessage}";
                    lblStatus.ForeColor = Color.Red;
                    StatusChanged?.Invoke(this, lblStatus.Text);
                    
                    // Handle authentication-specific errors in backup result
                    var errorMessage = GetUserFriendlyBackupErrorMessage(backupResult.ErrorMessage);
                    MessageBox.Show($"备份失败:\n\n{errorMessage}", 
                        "备份失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (OperationCanceledException)
            {
                lblStatus.Text = $"'{selectedConfig.Name}' 备份已取消";
                lblStatus.ForeColor = Color.Orange;
                StatusChanged?.Invoke(this, lblStatus.Text);
                
                MessageBox.Show("备份操作已取消。", "备份已取消", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"'{selectedConfig.Name}' 备份错误: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
                StatusChanged?.Invoke(this, lblStatus.Text);
                
                _logger.LogError(ex, "备份操作过程中发生错误");
                
                // Handle authentication-specific errors with user-friendly messages
                var errorMessage = GetUserFriendlyErrorMessage(ex);
                MessageBox.Show($"备份错误:\n\n{errorMessage}", "备份错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _currentBackupCancellation?.Dispose();
                _currentBackupCancellation = null;
                _currentProgress = null;
                
                btnStartBackup.Enabled = true;
                btnCancelBackup.Enabled = false;
                
                await LoadRunningBackups();
                UpdateStatus();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动备份时发生错误");
            MessageBox.Show($"启动备份时发生错误: {ex.Message}", "错误", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 取消备份按钮点击事件处理程序
    /// 取消当前正在进行的备份操作
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void btnCancelBackup_Click(object sender, EventArgs e)
    {
        try
        {
            if (_currentBackupCancellation == null)
                return;

            var result = MessageBox.Show(
                "确定要取消当前的备份操作吗?",
                "确认取消",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                _currentBackupCancellation.Cancel();
                btnCancelBackup.Enabled = false;
                lblStatus.Text = "正在取消备份操作...";
                lblStatus.ForeColor = Color.Orange;
                StatusChanged?.Invoke(this, lblStatus.Text);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消备份时发生错误");
            MessageBox.Show($"取消备份时发生错误: {ex.Message}", "错误", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 刷新按钮点击事件处理程序
    /// 手动刷新所有数据
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void btnRefresh_Click(object sender, EventArgs e)
    {
        LoadData();
    }

    /// <summary>
    /// 备份进度更新回调方法
    /// 在UI线程上更新备份进度显示
    /// </summary>
    /// <param name="progress">备份进度信息</param>
    private void OnBackupProgress(BackupProgress progress)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<BackupProgress>(OnBackupProgress), progress);
            return;
        }

        _currentProgress = progress;
        UpdateStatus();
    }

    #endregion

    #region 重写方法

    /// <summary>
    /// 重写控件释放方法
    /// 在控件释放时停止定时器并释放资源
    /// </summary>
    /// <param name="disposing">是否释放托管资源</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            _currentBackupCancellation?.Dispose();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    #endregion
}
