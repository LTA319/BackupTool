using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.ComponentModel;

namespace MySqlBackupTool.Client.Forms;

/// <summary>
/// 备份监控和控制窗体
/// 提供备份操作的实时监控、启动、取消等功能
/// </summary>
public partial class BackupMonitorForm : Form
{
    #region 私有字段

    /// <summary>
    /// 依赖注入服务提供者，用于获取各种服务实例
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 日志记录器，用于记录备份监控窗体的操作和错误信息
    /// </summary>
    private readonly ILogger<BackupMonitorForm> _logger;

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

    #region 构造函数

    /// <summary>
    /// 初始化BackupMonitorForm类的新实例
    /// </summary>
    /// <param name="serviceProvider">依赖注入服务提供者</param>
    public BackupMonitorForm(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<BackupMonitorForm>>();
        _configRepository = serviceProvider.GetRequiredService<IBackupConfigurationRepository>();
        _logRepository = serviceProvider.GetRequiredService<IBackupLogRepository>();
        
        // 尝试获取备份编排器（在某些配置中可能不可用）
        _backupOrchestrator = serviceProvider.GetService<IBackupOrchestrator>();

        InitializeComponent();
        InitializeForm();
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 初始化窗体的基本设置和属性
    /// 设置窗体标题、大小、位置，配置数据网格和定时器
    /// </summary>
    private void InitializeForm()
    {
        try
        {
            this.Text = "备份监控与控制";
            this.Size = new Size(900, 700);
            this.StartPosition = FormStartPosition.CenterParent;

            SetupDataGridViews();
            SetupRefreshTimer();
            
            // 异步加载数据以避免阻塞UI线程
            _ = LoadDataAsync();
            
            _logger.LogInformation("备份监控窗体初始化成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化备份监控窗体时发生错误");
            MessageBox.Show($"初始化窗体时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 设置数据网格视图的列和属性
    /// 配置备份配置网格和正在运行的备份网格的显示格式
    /// </summary>
    private void SetupDataGridViews()
    {
        // 设置配置网格
        dgvConfigurations.AutoGenerateColumns = false;
        dgvConfigurations.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvConfigurations.MultiSelect = false;
        dgvConfigurations.ReadOnly = true;
        dgvConfigurations.AllowUserToAddRows = false;

        dgvConfigurations.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Name",
            HeaderText = "配置名称",
            DataPropertyName = "Name",
            Width = 150
        });

        dgvConfigurations.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "IsActive",
            HeaderText = "激活",
            DataPropertyName = "IsActive",
            Width = 60
        });

        dgvConfigurations.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "MySQLHost",
            HeaderText = "MySQL主机",
            Width = 100
        });

        dgvConfigurations.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "TargetServer",
            HeaderText = "目标服务器",
            Width = 100
        });

        // 设置正在运行的备份网格
        dgvRunningBackups.AutoGenerateColumns = false;
        dgvRunningBackups.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvRunningBackups.MultiSelect = false;
        dgvRunningBackups.ReadOnly = true;
        dgvRunningBackups.AllowUserToAddRows = false;

        dgvRunningBackups.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ConfigName",
            HeaderText = "配置名称",
            Width = 120
        });

        dgvRunningBackups.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Status",
            HeaderText = "状态",
            DataPropertyName = "Status",
            Width = 100
        });

        dgvRunningBackups.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "StartTime",
            HeaderText = "开始时间",
            DataPropertyName = "StartTime",
            Width = 120,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "HH:mm:ss" }
        });

        dgvRunningBackups.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Duration",
            HeaderText = "持续时间",
            Width = 80
        });

        dgvRunningBackups.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Progress",
            HeaderText = "进度",
            Width = 80
        });

        // 处理单元格格式化
        dgvConfigurations.CellFormatting += DgvConfigurations_CellFormatting;
        dgvRunningBackups.CellFormatting += DgvRunningBackups_CellFormatting;
        
        // 处理选择变化
        dgvConfigurations.SelectionChanged += DgvConfigurations_SelectionChanged;
        dgvRunningBackups.SelectionChanged += DgvRunningBackups_SelectionChanged;
    }

    /// <summary>
    /// 设置刷新定时器
    /// 配置定时器每2秒刷新一次数据
    /// </summary>
    private void SetupRefreshTimer()
    {
        _refreshTimer = new System.Windows.Forms.Timer();
        _refreshTimer.Interval = 2000; // 每2秒刷新一次
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
                }));
            }
            else
            {
                lblStatus.Text = "正在加载数据...";
                lblStatus.ForeColor = Color.Blue;
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
                }));
            }
            else
            {
                lblStatus.Text = $"加载数据时发生错误: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
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
                    var duration = log.Duration ?? (DateTime.UtcNow - log.StartTime);
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

                var backupResult = await _backupOrchestrator.ExecuteBackupAsync(
                    selectedConfig, 
                    progress, 
                    _currentBackupCancellation.Token);

                if (backupResult.Success)
                {
                    lblStatus.Text = $"'{selectedConfig.Name}' 备份成功完成";
                    lblStatus.ForeColor = Color.Green;
                    
                    MessageBox.Show($"备份成功完成!\n\n文件: {backupResult.BackupFilePath}\n大小: {FormatFileSize(backupResult.FileSize)}\n持续时间: {backupResult.Duration:hh\\:mm\\:ss}", 
                        "备份完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    lblStatus.Text = $"'{selectedConfig.Name}' 备份失败: {backupResult.ErrorMessage}";
                    lblStatus.ForeColor = Color.Red;
                    
                    MessageBox.Show($"备份失败:\n\n{backupResult.ErrorMessage}", 
                        "备份失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (OperationCanceledException)
            {
                lblStatus.Text = $"'{selectedConfig.Name}' 备份已取消";
                lblStatus.ForeColor = Color.Orange;
                
                MessageBox.Show("备份操作已取消。", "备份已取消", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"'{selectedConfig.Name}' 备份错误: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
                
                _logger.LogError(ex, "备份操作过程中发生错误");
                MessageBox.Show($"备份错误:\n\n{ex.Message}", "备份错误", 
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
    /// 关闭按钮点击事件处理程序
    /// 停止定时器、取消备份操作并关闭窗体
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void btnClose_Click(object sender, EventArgs e)
    {
        _refreshTimer?.Stop();
        _currentBackupCancellation?.Cancel();
        this.Close();
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
    /// 重写窗体关闭事件
    /// 在窗体关闭时停止定时器并释放资源
    /// </summary>
    /// <param name="e">窗体关闭事件参数</param>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
        _currentBackupCancellation?.Dispose();
        base.OnFormClosing(e);
    }

    #endregion
}