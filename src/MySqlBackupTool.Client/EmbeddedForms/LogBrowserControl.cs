using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Client.Forms;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using System.ComponentModel;
using System.Text;

namespace MySqlBackupTool.Client.EmbeddedForms;

/// <summary>
/// 备份日志浏览器控件
/// 提供备份日志的浏览、搜索、过滤、查看详情和导出功能
/// </summary>
public partial class LogBrowserControl : UserControl, IEmbeddedForm
{
    #region 私有字段

    /// <summary>
    /// 依赖注入服务提供者，用于获取各种服务实例
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 日志记录器，用于记录日志浏览器控件的操作和错误信息
    /// </summary>
    private readonly ILogger<LogBrowserControl> _logger;

    /// <summary>
    /// 备份日志仓储接口，用于获取备份日志数据
    /// </summary>
    private readonly IBackupLogRepository _logRepository;

    /// <summary>
    /// 备份配置仓储接口，用于获取配置信息
    /// </summary>
    private readonly IBackupConfigurationRepository _configRepository;

    /// <summary>
    /// 备份报告服务，用于生成备份报告
    /// </summary>
    private readonly BackupReportingService _reportingService;

    /// <summary>
    /// 所有备份日志列表，存储从数据库加载的完整日志数据
    /// </summary>
    private List<BackupLog> _allLogs = new();

    /// <summary>
    /// 过滤后的备份日志列表，根据用户设置的过滤条件筛选
    /// </summary>
    private List<BackupLog> _filteredLogs = new();

    /// <summary>
    /// 备份配置列表，用于显示配置名称和过滤
    /// </summary>
    private List<BackupConfiguration> _configurations = new();

    /// <summary>
    /// 当前选中的备份日志
    /// </summary>
    private BackupLog? _selectedLog;

    #endregion

    #region IEmbeddedForm 实现

    /// <summary>
    /// 获取嵌入式窗体的显示标题
    /// </summary>
    public string Title => "备份日志浏览器";

    /// <summary>
    /// 获取导航路径用于面包屑显示
    /// </summary>
    public string NavigationPath => "工具 > 日志浏览器";

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
            LoadData();
            _logger.LogInformation("日志浏览器控件已激活");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "激活日志浏览器控件时发生错误");
        }
    }

    /// <summary>
    /// 当窗体被停用（隐藏）时调用
    /// </summary>
    public void OnDeactivated()
    {
        try
        {
            _logger.LogInformation("日志浏览器控件已停用");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停用日志浏览器控件时发生错误");
        }
    }

    /// <summary>
    /// 检查窗体是否可以关闭
    /// </summary>
    /// <returns>如果窗体可以关闭则返回true，否则返回false</returns>
    public bool CanClose()
    {
        return true;
    }

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化LogBrowserControl类的新实例
    /// </summary>
    /// <param name="serviceProvider">依赖注入服务提供者</param>
    public LogBrowserControl(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<LogBrowserControl>>();
        _logRepository = serviceProvider.GetRequiredService<IBackupLogRepository>();
        _configRepository = serviceProvider.GetRequiredService<IBackupConfigurationRepository>();
        _reportingService = serviceProvider.GetRequiredService<BackupReportingService>();

        // 检查当前线程的STA状态（用于COM组件），但不尝试更改
        try
        {
            var apartmentState = Thread.CurrentThread.GetApartmentState();
            if (apartmentState != ApartmentState.STA)
            {
                _logger.LogWarning("Thread is in {ApartmentState} mode, not STA. File dialogs may have compatibility issues.", apartmentState);
            }
            else
            {
                _logger.LogInformation("Thread is in STA mode, file dialogs should work properly.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check thread apartment state");
        }

        InitializeComponent();
        InitializeControl();
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 初始化控件的基本设置和属性
    /// 设置控件大小、配置数据网格、过滤器并加载数据
    /// </summary>
    private void InitializeControl()
    {
        try
        {
            SetupDataGridView();
            SetupFilters();
            
            _logger.LogInformation("日志浏览器控件初始化成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化日志浏览器控件时发生错误");
            MessageBox.Show($"初始化控件时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 设置数据网格视图的列和属性
    /// 配置日志列表网格的显示格式和事件处理
    /// </summary>
    private void SetupDataGridView()
    {
        dgvLogs.AutoGenerateColumns = false;
        dgvLogs.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvLogs.MultiSelect = false;
        dgvLogs.ReadOnly = true;
        dgvLogs.AllowUserToAddRows = false;
        dgvLogs.AllowUserToDeleteRows = false;

        // 添加列
        dgvLogs.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ConfigName",
            HeaderText = "配置名称",
            Width = 120
        });

        dgvLogs.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Status",
            HeaderText = "状态",
            DataPropertyName = "Status",
            Width = 100
        });

        dgvLogs.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "StartTime",
            HeaderText = "开始时间",
            DataPropertyName = "StartTime",
            Width = 130,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm:ss" }
        });

        dgvLogs.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Duration",
            HeaderText = "持续时间",
            Width = 80
        });

        dgvLogs.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "FileSize",
            HeaderText = "文件大小",
            Width = 80
        });

        dgvLogs.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "FilePath",
            HeaderText = "文件路径",
            DataPropertyName = "FilePath",
            Width = 200
        });

        dgvLogs.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ErrorMessage",
            HeaderText = "错误信息",
            DataPropertyName = "ErrorMessage",
            Width = 150
        });

        // 处理单元格格式化和选择
        dgvLogs.CellFormatting += DgvLogs_CellFormatting;
        dgvLogs.SelectionChanged += DgvLogs_SelectionChanged;
        dgvLogs.RowPrePaint += DgvLogs_RowPrePaint;
    }

    /// <summary>
    /// 设置过滤器控件的初始值
    /// 配置日期范围、状态和配置过滤器的默认选项
    /// </summary>
    private void SetupFilters()
    {
        // 设置日期过滤器
        dtpStartDate.Value = DateTime.Now.AddDays(-30);
        dtpEndDate.Value = DateTime.Now;

        // 设置状态过滤器
        cmbStatus.Items.Add("所有状态");
        foreach (BackupStatus status in Enum.GetValues<BackupStatus>())
        {
            cmbStatus.Items.Add(status.ToString());
        }
        cmbStatus.SelectedIndex = 0;

        // 配置过滤器将在加载配置时填充
        cmbConfiguration.Items.Add("所有配置");
        cmbConfiguration.SelectedIndex = 0;
    }

    /// <summary>
    /// 异步加载所有数据
    /// 包括备份配置和日志数据
    /// </summary>
    private async void LoadData()
    {
        try
        {
            btnRefresh.Enabled = false;
            btnRefresh.Text = "加载中...";
            
            var statusMessage = "正在加载数据...";
            lblStatus.Text = statusMessage;
            lblStatus.ForeColor = Color.Blue;
            StatusChanged?.Invoke(this, statusMessage);

            // 加载配置
            _configurations = (await _configRepository.GetAllAsync()).ToList();
            
            // 更新配置过滤器
            cmbConfiguration.Items.Clear();
            cmbConfiguration.Items.Add("所有配置");
            foreach (var config in _configurations)
            {
                cmbConfiguration.Items.Add(config.Name);
            }
            cmbConfiguration.SelectedIndex = 0;

            // 加载日志
            await LoadLogs();

            statusMessage = $"已加载 {_allLogs.Count} 条日志记录";
            lblStatus.Text = statusMessage;
            lblStatus.ForeColor = Color.Green;
            StatusChanged?.Invoke(this, statusMessage);
            
            _logger.LogInformation("已加载 {Count} 条日志记录", _allLogs.Count);
        }
        catch (Exception ex)
        {
            var errorMessage = $"加载数据时发生错误: {ex.Message}";
            lblStatus.Text = errorMessage;
            lblStatus.ForeColor = Color.Red;
            StatusChanged?.Invoke(this, errorMessage);
            _logger.LogError(ex, "加载日志数据时发生错误");
        }
        finally
        {
            btnRefresh.Enabled = true;
            btnRefresh.Text = "刷新";
        }
    }

    /// <summary>
    /// 根据日期范围异步加载日志数据
    /// </summary>
    /// <returns>异步任务</returns>
    private async Task LoadLogs()
    {
        var startDate = dtpStartDate.Value.Date;
        var endDate = dtpEndDate.Value.Date.AddDays(1).AddTicks(-1);
        
        _allLogs = (await _logRepository.GetByDateRangeAsync(startDate, endDate)).ToList();
        ApplyFilters();
    }

    /// <summary>
    /// 应用用户设置的过滤条件
    /// 根据状态、配置和搜索文本过滤日志列表
    /// </summary>
    private void ApplyFilters()
    {
        _filteredLogs = _allLogs.Where(log =>
        {
            // 状态过滤
            if (cmbStatus.SelectedIndex > 0 && cmbStatus.SelectedItem != null)
            {
                var selectedStatus = (BackupStatus)Enum.Parse(typeof(BackupStatus), cmbStatus.SelectedItem.ToString()!);
                if (log.Status != selectedStatus)
                    return false;
            }

            // 配置过滤
            if (cmbConfiguration.SelectedIndex > 0 && cmbConfiguration.SelectedItem != null)
            {
                var selectedConfigName = cmbConfiguration.SelectedItem.ToString()!;
                var config = _configurations.FirstOrDefault(c => c.Name == selectedConfigName);
                if (config == null || log.BackupConfigId != config.Id)
                    return false;
            }

            // 搜索文本过滤
            if (!string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                var searchText = txtSearch.Text.ToLower();
                var config = _configurations.FirstOrDefault(c => c.Id == log.BackupConfigId);
                var configName = config?.Name?.ToLower() ?? "";
                var filePath = log.FilePath?.ToLower() ?? "";
                var errorMessage = log.ErrorMessage?.ToLower() ?? "";

                if (!configName.Contains(searchText) && 
                    !filePath.Contains(searchText) && 
                    !errorMessage.Contains(searchText))
                    return false;
            }

            return true;
        }).ToList();

        dgvLogs.DataSource = _filteredLogs;
        lblFilteredCount.Text = $"显示 {_filteredLogs.Count} / {_allLogs.Count} 条记录";
    }

    /// <summary>
    /// 异步加载选中日志的详细信息
    /// 包括传输日志和错误信息
    /// </summary>
    private async void LoadLogDetails()
    {
        if (_selectedLog == null)
        {
            txtLogDetails.Clear();
            return;
        }

        try
        {
            // 加载包含传输日志的详细日志
            var detailedLog = await _logRepository.GetWithTransferLogsAsync(_selectedLog.Id);
            if (detailedLog == null)
            {
                txtLogDetails.Text = "未找到日志详情。";
                return;
            }

            var sb = new StringBuilder();
            var config = _configurations.FirstOrDefault(c => c.Id == detailedLog.BackupConfigId);

            sb.AppendLine("=== 备份日志详情 ===");
            sb.AppendLine($"配置名称: {config?.Name ?? "未知"}");
            sb.AppendLine($"状态: {detailedLog.Status}");
            sb.AppendLine($"开始时间: {detailedLog.StartTime:yyyy-MM-dd HH:mm:ss}");
            
            if (detailedLog.EndTime.HasValue)
            {
                sb.AppendLine($"结束时间: {detailedLog.EndTime.Value:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"持续时间: {detailedLog.Duration?.ToString(@"hh\:mm\:ss")}");
            }

            if (!string.IsNullOrEmpty(detailedLog.FilePath))
            {
                sb.AppendLine($"文件路径: {detailedLog.FilePath}");
            }

            if (detailedLog.FileSize.HasValue)
            {
                sb.AppendLine($"文件大小: {FormatFileSize(detailedLog.FileSize.Value)}");
            }

            if (!string.IsNullOrEmpty(detailedLog.ResumeToken))
            {
                sb.AppendLine($"恢复令牌: {detailedLog.ResumeToken}");
            }

            if (!string.IsNullOrEmpty(detailedLog.ErrorMessage))
            {
                sb.AppendLine();
                sb.AppendLine("=== 错误信息 ===");
                sb.AppendLine(detailedLog.ErrorMessage);
            }

            if (detailedLog.TransferLogs.Any())
            {
                sb.AppendLine();
                sb.AppendLine("=== 传输日志 ===");
                sb.AppendLine($"{"块号",-8} {"大小",-12} {"时间",-20} {"状态",-10}");
                sb.AppendLine(new string('-', 60));

                foreach (var transferLog in detailedLog.TransferLogs.OrderBy(t => t.ChunkIndex))
                {
                    sb.AppendLine($"{transferLog.ChunkIndex,-8} {FormatFileSize(transferLog.ChunkSize),-12} " +
                                $"{transferLog.TransferTime:HH:mm:ss.fff},-20 {transferLog.Status,-10}");
                    
                    if (!string.IsNullOrEmpty(transferLog.ErrorMessage))
                    {
                        sb.AppendLine($"         错误: {transferLog.ErrorMessage}");
                    }
                }
            }

            txtLogDetails.Text = sb.ToString();
        }
        catch (Exception ex)
        {
            txtLogDetails.Text = $"加载日志详情时发生错误: {ex.Message}";
            _logger.LogError(ex, "加载日志 {LogId} 的详情时发生错误", _selectedLog.Id);
        }
    }

    /// <summary>
    /// 格式化文件大小显示
    /// 将字节数转换为可读的文件大小格式
    /// </summary>
    /// <param name="bytes">文件大小（字节）</param>
    /// <returns>格式化的文件大小字符串</returns>
    private static string FormatFileSize(long bytes)
    {
        if (bytes == 0) return "0 B";
        
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

    private string? ShowFolderDialogSafe() {
        // 确保在UI线程上执行
        if (InvokeRequired)
        {
            return Invoke(new Func<string?>(() =>
                ShowFolderDialogSafe()));
        }

        // 检查当前线程的单元状态，但不尝试更改
        var apartmentState = Thread.CurrentThread.GetApartmentState();
        if (apartmentState != ApartmentState.STA)
        {
            _logger.LogWarning("Current thread is in {ApartmentState} mode, not STA. File dialogs may have issues.", apartmentState);
        }

        #region 方法1：使用OpenFileDialog

        try
        {
            // 如果当前线程不是STA，尝试在STA线程中运行对话框
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                _logger.LogInformation("Running openFileDialogDataDirectory in STA thread");
                return RunDialogInSTAThread(() => ShowOpenFileDialog());
            }
            else
            {
                return ShowOpenFileDialog();
            }
        }
        catch (ThreadStateException ex)
        {
            _logger.LogError(ex, "STA thread state exception in openFileDialogDataDirectory, trying alternative methods");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "openFileDialogDataDirectory failed, trying FolderBrowserDialog");
        }
        return null;
        #endregion
    }

    /// <summary>
    /// 在STA线程中运行对话框
    /// </summary>
    /// <param name="dialogAction">对话框操作</param>
    /// <returns>选择的路径</returns>
    private string? RunDialogInSTAThread(Func<string?> dialogAction)
    {
        string? result = null;
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = dialogAction();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            throw exception;
        }

        return result;
    }

    private string? ShowOpenFileDialog()
    {
        // 配置对话框用于选择文件夹
        using var openDialog = new OpenFileDialog();
        openDialog.Filter = "All files (*.*)|*.*";
        openDialog.FileName = "Select any file in this folder";
        openDialog.CheckFileExists = false;
        openDialog.CheckPathExists = true;
        openDialog.Multiselect = false;

        var result = openDialog.ShowDialog();
        if (result == DialogResult.OK)
        {
            var selectedDir = Path.GetDirectoryName(openDialog.FileName);
            if (!string.IsNullOrEmpty(selectedDir))
            {
                _logger.LogInformation("Folder selected via openFileDialogDataDirectory: {Path}", selectedDir);
                return selectedDir;
            }
        }

        return null;
    }

    #endregion

    #region 事件处理程序

    /// <summary>
    /// 日志网格单元格格式化事件处理程序
    /// 自定义显示配置名称、持续时间和文件大小
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">单元格格式化事件参数</param>
    private void DgvLogs_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (dgvLogs.Rows[e.RowIndex].DataBoundItem is BackupLog log)
        {
            switch (dgvLogs.Columns[e.ColumnIndex].Name)
            {
                case "ConfigName":
                    var config = _configurations.FirstOrDefault(c => c.Id == log.BackupConfigId);
                    e.Value = config?.Name ?? "未知";
                    break;
                case "Duration":
                    var duration = log.Duration;
                    if (duration.HasValue)
                    {
                        e.Value = $"{duration.Value.Hours:D2}:{duration.Value.Minutes:D2}:{duration.Value.Seconds:D2}";
                    }
                    else if (log.IsRunning)
                    {
                        var elapsed = DateTime.Now - log.StartTime;
                        e.Value = $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
                    }
                    else
                    {
                        e.Value = "不可用";
                    }
                    break;
                case "FileSize":
                    if (log.FileSize.HasValue && log.FileSize.Value > 0)
                    {
                        e.Value = FormatFileSize(log.FileSize.Value);
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
    /// 日志网格行预绘制事件处理程序
    /// 根据备份状态设置行的背景颜色
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">行预绘制事件参数</param>
    private void DgvLogs_RowPrePaint(object? sender, DataGridViewRowPrePaintEventArgs e)
    {
        if (dgvLogs.Rows[e.RowIndex].DataBoundItem is BackupLog log)
        {
            var row = dgvLogs.Rows[e.RowIndex];
            
            switch (log.Status)
            {
                case BackupStatus.Completed:
                    row.DefaultCellStyle.BackColor = Color.LightGreen;
                    break;
                case BackupStatus.Failed:
                    row.DefaultCellStyle.BackColor = Color.LightCoral;
                    break;
                case BackupStatus.Cancelled:
                    row.DefaultCellStyle.BackColor = Color.LightYellow;
                    break;
                case BackupStatus.Queued:
                case BackupStatus.StoppingMySQL:
                case BackupStatus.Compressing:
                case BackupStatus.Transferring:
                case BackupStatus.StartingMySQL:
                case BackupStatus.Verifying:
                    row.DefaultCellStyle.BackColor = Color.LightBlue;
                    break;
            }
        }
    }

    /// <summary>
    /// 日志网格选择变化事件处理程序
    /// 更新选中的日志并加载详细信息
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void DgvLogs_SelectionChanged(object? sender, EventArgs e)
    {
        if (dgvLogs.SelectedRows.Count > 0)
        {
            _selectedLog = dgvLogs.SelectedRows[0].DataBoundItem as BackupLog;
            LoadLogDetails();
            btnViewDetails.Enabled = true;
            btnExportLog.Enabled = true;
        }
        else
        {
            _selectedLog = null;
            txtLogDetails.Clear();
            btnViewDetails.Enabled = false;
            btnExportLog.Enabled = false;
        }
    }

    /// <summary>
    /// 过滤按钮点击事件处理程序
    /// 根据设置的日期范围重新加载日志
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private async void btnFilter_Click(object sender, EventArgs e)
    {
        try
        {
            await LoadLogs();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "应用过滤器时发生错误");
            MessageBox.Show($"应用过滤器时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 清除过滤器按钮点击事件处理程序
    /// 重置所有过滤条件到默认值
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void btnClearFilters_Click(object sender, EventArgs e)
    {
        dtpStartDate.Value = DateTime.Now.AddDays(-30);
        dtpEndDate.Value = DateTime.Now;
        cmbStatus.SelectedIndex = 0;
        cmbConfiguration.SelectedIndex = 0;
        txtSearch.Clear();
        ApplyFilters();
    }

    /// <summary>
    /// 搜索文本框文本变化事件处理程序
    /// 实时应用搜索过滤
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void txtSearch_TextChanged(object sender, EventArgs e)
    {
        ApplyFilters();
    }

    /// <summary>
    /// 状态下拉框选择变化事件处理程序
    /// 应用状态过滤
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void cmbStatus_SelectedIndexChanged(object sender, EventArgs e)
    {
        ApplyFilters();
    }

    /// <summary>
    /// 配置下拉框选择变化事件处理程序
    /// 应用配置过滤
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void cmbConfiguration_SelectedIndexChanged(object sender, EventArgs e)
    {
        ApplyFilters();
    }

    /// <summary>
    /// 查看详情按钮点击事件处理程序
    /// 打开日志详情窗体显示完整的日志信息
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void btnViewDetails_Click(object sender, EventArgs e)
    {
        if (_selectedLog == null)
            return;

        try
        {
            using var detailsForm = new LogDetailsForm(_serviceProvider, _selectedLog);
            detailsForm.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "显示日志详情时发生错误");
            MessageBox.Show($"显示详情时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 生成报告按钮点击事件处理程序
    /// 根据当前过滤条件生成备份报告
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private async void btnGenerateReport_Click(object sender, EventArgs e)
    {
        try
        {
            btnGenerateReport.Enabled = false;
            btnGenerateReport.Text = "生成中...";

            var criteria = new ReportCriteria
            {
                StartDate = dtpStartDate.Value.Date,
                EndDate = dtpEndDate.Value.Date.AddDays(1).AddTicks(-1),
                ConfigurationId = cmbConfiguration.SelectedIndex > 0 && cmbConfiguration.SelectedItem != null ? 
                    _configurations.FirstOrDefault(c => c.Name == cmbConfiguration.SelectedItem.ToString()!)?.Id : null
            };

            var report = await _reportingService.GenerateReportAsync(criteria);
            
            using var reportForm = new ReportViewerForm(report);
            reportForm.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成报告时发生错误");
            MessageBox.Show($"生成报告时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnGenerateReport.Enabled = true;
            btnGenerateReport.Text = "生成报告";
        }
    }

    /// <summary>
    /// 导出日志按钮点击事件处理程序
    /// 将选中的日志详情导出到文本文件
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void btnExportLog_Click(object sender, EventArgs e)
    {
        if (_selectedLog == null)
            return;

        try
        {
            var selectedPath = ShowFolderDialogSafe();
            if (selectedPath == null) return;

            // 构建完整的文件路径
            string fileName = $"backup_log_{_selectedLog.Id}_{_selectedLog.StartTime:yyyyMMdd_HHmmss}.txt";
            string fullPath = Path.Combine(selectedPath, fileName);

            // 确保目录存在
            Directory.CreateDirectory(selectedPath);

            File.WriteAllText(fullPath, txtLogDetails.Text);
            MessageBox.Show($"日志导出成功到:\n{fullPath}", "导出完成",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出日志时发生错误");
            MessageBox.Show($"导出日志时发生错误: {ex.Message}", "导出错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 刷新按钮点击事件处理程序
    /// 重新加载所有数据
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void btnRefresh_Click(object sender, EventArgs e)
    {
        LoadData();
    }

    /// <summary>
    /// 关闭按钮点击事件处理程序
    /// 触发CloseRequested事件以请求关闭控件
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void btnClose_Click(object sender, EventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion
}
