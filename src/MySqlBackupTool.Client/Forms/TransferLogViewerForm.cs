using System.ComponentModel;
using System.Data;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using Microsoft.Extensions.Logging;

namespace MySqlBackupTool.Client.Forms;

/// <summary>
/// 传输日志查看器窗体
/// Transfer log viewer form
/// </summary>
public partial class TransferLogViewerForm : Form
{
    private readonly ITransferLogService _transferLogService;
    private readonly ILogger<TransferLogViewerForm> _logger;
    private int _currentBackupLogId;

    // UI 控件
    private DataGridView _transferLogGrid;
    private Panel _controlPanel;
    private Button _refreshButton;
    private Button _exportButton;
    private Button _retryFailedButton;
    private Label _progressLabel;
    private ProgressBar _progressBar;
    private ComboBox _statusFilter;
    private DateTimePicker _startDatePicker;
    private DateTimePicker _endDatePicker;
    private Button _filterButton;
    private GroupBox _statisticsGroup;
    private Label _totalTransfersLabel;
    private Label _successfulTransfersLabel;
    private Label _failedTransfersLabel;
    private Label _successRateLabel;

    /// <summary>
    /// 构造函数
    /// Constructor
    /// </summary>
    /// <param name="transferLogService">传输日志服务</param>
    /// <param name="logger">日志记录器</param>
    public TransferLogViewerForm(ITransferLogService transferLogService, ILogger<TransferLogViewerForm> logger)
    {
        _transferLogService = transferLogService ?? throw new ArgumentNullException(nameof(transferLogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        InitializeComponent();
        InitializeEventHandlers();
    }

    /// <summary>
    /// 设置要查看的备份日志ID
    /// Sets the backup log ID to view
    /// </summary>
    /// <param name="backupLogId">备份日志ID</param>
    public void SetBackupLogId(int backupLogId)
    {
        _currentBackupLogId = backupLogId;
        this.Text = $"传输日志查看器 - 备份 #{backupLogId}";
        _ = LoadTransferLogsAsync();
        _ = LoadStatisticsAsync();
    }

    /// <summary>
    /// 初始化UI组件
    /// Initialize UI components
    /// </summary>
    private void InitializeComponent()
    {
        this.Size = new Size(1000, 700);
        this.Text = "传输日志查看器";
        this.StartPosition = FormStartPosition.CenterParent;

        // 创建主面板
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };

        // 控制面板
        _controlPanel = CreateControlPanel();
        mainPanel.Controls.Add(_controlPanel, 0, 0);
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));

        // 统计信息面板
        _statisticsGroup = CreateStatisticsPanel();
        mainPanel.Controls.Add(_statisticsGroup, 0, 1);
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));

        // 数据网格
        _transferLogGrid = CreateTransferLogGrid();
        mainPanel.Controls.Add(_transferLogGrid, 0, 2);
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // 进度条
        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Visible = false
        };
        _progressLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "准备就绪"
        };

        var progressPanel = new Panel { Height = 30, Dock = DockStyle.Fill };
        progressPanel.Controls.Add(_progressBar);
        progressPanel.Controls.Add(_progressLabel);
        
        mainPanel.Controls.Add(progressPanel, 0, 3);
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        this.Controls.Add(mainPanel);
    }

    /// <summary>
    /// 创建控制面板
    /// Create control panel
    /// </summary>
    private Panel CreateControlPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill };

        // 刷新按钮
        _refreshButton = new Button
        {
            Text = "刷新",
            Size = new Size(80, 30),
            Location = new Point(10, 10)
        };

        // 导出按钮
        _exportButton = new Button
        {
            Text = "导出",
            Size = new Size(80, 30),
            Location = new Point(100, 10)
        };

        // 重试失败按钮
        _retryFailedButton = new Button
        {
            Text = "重试失败",
            Size = new Size(80, 30),
            Location = new Point(190, 10)
        };

        // 状态过滤器
        var statusLabel = new Label
        {
            Text = "状态:",
            Size = new Size(40, 20),
            Location = new Point(300, 15)
        };

        _statusFilter = new ComboBox
        {
            Size = new Size(100, 25),
            Location = new Point(350, 12),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _statusFilter.Items.AddRange(new[] { "全部", "Pending", "InProgress", "Completed", "Failed" });
        _statusFilter.SelectedIndex = 0;

        // 日期过滤器
        var dateLabel = new Label
        {
            Text = "日期:",
            Size = new Size(40, 20),
            Location = new Point(470, 15)
        };

        _startDatePicker = new DateTimePicker
        {
            Size = new Size(120, 25),
            Location = new Point(520, 12),
            Value = DateTime.Today.AddDays(-7)
        };

        var toLabel = new Label
        {
            Text = "至",
            Size = new Size(20, 20),
            Location = new Point(650, 15)
        };

        _endDatePicker = new DateTimePicker
        {
            Size = new Size(120, 25),
            Location = new Point(680, 12),
            Value = DateTime.Today.AddDays(1)
        };

        _filterButton = new Button
        {
            Text = "筛选",
            Size = new Size(60, 30),
            Location = new Point(810, 10)
        };

        panel.Controls.AddRange(new Control[]
        {
            _refreshButton, _exportButton, _retryFailedButton,
            statusLabel, _statusFilter,
            dateLabel, _startDatePicker, toLabel, _endDatePicker, _filterButton
        });

        return panel;
    }

    /// <summary>
    /// 创建统计信息面板
    /// Create statistics panel
    /// </summary>
    private GroupBox CreateStatisticsPanel()
    {
        var groupBox = new GroupBox
        {
            Text = "传输统计",
            Dock = DockStyle.Fill
        };

        _totalTransfersLabel = new Label
        {
            Text = "总传输: 0",
            Location = new Point(20, 25),
            Size = new Size(120, 20)
        };

        _successfulTransfersLabel = new Label
        {
            Text = "成功: 0",
            Location = new Point(150, 25),
            Size = new Size(100, 20)
        };

        _failedTransfersLabel = new Label
        {
            Text = "失败: 0",
            Location = new Point(260, 25),
            Size = new Size(100, 20)
        };

        _successRateLabel = new Label
        {
            Text = "成功率: 0%",
            Location = new Point(370, 25),
            Size = new Size(120, 20)
        };

        groupBox.Controls.AddRange(new Control[]
        {
            _totalTransfersLabel, _successfulTransfersLabel, _failedTransfersLabel, _successRateLabel
        });

        return groupBox;
    }

    /// <summary>
    /// 创建传输日志数据网格
    /// Create transfer log data grid
    /// </summary>
    private DataGridView CreateTransferLogGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true
        };

        // 添加列
        grid.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "ID", Width = 60, DataPropertyName = "Id" },
            new DataGridViewTextBoxColumn { Name = "ChunkIndex", HeaderText = "分块索引", Width = 80, DataPropertyName = "ChunkIndex" },
            new DataGridViewTextBoxColumn { Name = "ChunkSize", HeaderText = "分块大小", Width = 100, DataPropertyName = "ChunkSizeFormatted" },
            new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "状态", Width = 80, DataPropertyName = "Status" },
            new DataGridViewTextBoxColumn { Name = "TransferTime", HeaderText = "传输时间", Width = 150, DataPropertyName = "TransferTime" },
            new DataGridViewTextBoxColumn { Name = "ErrorMessage", HeaderText = "错误消息", Width = 300, DataPropertyName = "ErrorMessage" }
        });

        return grid;
    }

    /// <summary>
    /// 初始化事件处理器
    /// Initialize event handlers
    /// </summary>
    private void InitializeEventHandlers()
    {
        _refreshButton.Click += async (s, e) => await LoadTransferLogsAsync();
        _exportButton.Click += async (s, e) => await ExportTransferLogsAsync();
        _retryFailedButton.Click += async (s, e) => await RetryFailedTransfersAsync();
        _filterButton.Click += async (s, e) => await LoadTransferLogsAsync();
    }

    /// <summary>
    /// 加载传输日志
    /// Load transfer logs
    /// </summary>
    private async Task LoadTransferLogsAsync()
    {
        try
        {
            ShowProgress("正在加载传输日志...");

            var transferLogs = await _transferLogService.GetFailedTransferChunksAsync(_currentBackupLogId);
            
            // 如果有状态过滤器，应用过滤
            if (_statusFilter.SelectedIndex > 0)
            {
                var selectedStatus = _statusFilter.SelectedItem.ToString();
                transferLogs = transferLogs.Where(tl => tl.Status == selectedStatus);
            }

            // 转换为显示用的数据
            var displayData = transferLogs.Select(tl => new
            {
                tl.Id,
                tl.ChunkIndex,
                ChunkSizeFormatted = FormatFileSize(tl.ChunkSize),
                tl.Status,
                tl.TransferTime,
                tl.ErrorMessage
            }).ToList();

            _transferLogGrid.DataSource = displayData;
            
            HideProgress();
            _logger.LogInformation("Loaded {Count} transfer logs for backup {BackupLogId}", 
                displayData.Count, _currentBackupLogId);
        }
        catch (Exception ex)
        {
            HideProgress();
            _logger.LogError(ex, "Failed to load transfer logs for backup {BackupLogId}", _currentBackupLogId);
            MessageBox.Show($"加载传输日志失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 加载统计信息
    /// Load statistics
    /// </summary>
    private async Task LoadStatisticsAsync()
    {
        try
        {
            var statistics = await _transferLogService.GetTransferStatisticsAsync(_currentBackupLogId);
            
            _totalTransfersLabel.Text = $"总传输: {statistics.TotalTransfers}";
            _successfulTransfersLabel.Text = $"成功: {statistics.SuccessfulTransfers}";
            _failedTransfersLabel.Text = $"失败: {statistics.FailedTransfers}";
            _successRateLabel.Text = $"成功率: {statistics.SuccessRate:F1}%";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load transfer statistics for backup {BackupLogId}", _currentBackupLogId);
        }
    }

    /// <summary>
    /// 导出传输日志
    /// Export transfer logs
    /// </summary>
    private async Task ExportTransferLogsAsync()
    {
        try
        {
            using var saveDialog = new SaveFileDialog
            {
                Filter = "CSV文件|*.csv|JSON文件|*.json",
                DefaultExt = "csv",
                FileName = $"transfer_logs_backup_{_currentBackupLogId}_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                ShowProgress("正在导出传输日志...");

                var format = Path.GetExtension(saveDialog.FileName).ToLowerInvariant() == ".json" ? "JSON" : "CSV";
                var exportData = await _transferLogService.ExportTransferLogsAsync(_currentBackupLogId, format);

                await File.WriteAllBytesAsync(saveDialog.FileName, exportData);

                HideProgress();
                MessageBox.Show($"传输日志已成功导出到: {saveDialog.FileName}", "导出成功", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            HideProgress();
            _logger.LogError(ex, "Failed to export transfer logs for backup {BackupLogId}", _currentBackupLogId);
            MessageBox.Show($"导出传输日志失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 重试失败的传输
    /// Retry failed transfers
    /// </summary>
    private async Task RetryFailedTransfersAsync()
    {
        try
        {
            var selectedRows = _transferLogGrid.SelectedRows.Cast<DataGridViewRow>().ToList();
            if (!selectedRows.Any())
            {
                MessageBox.Show("请选择要重试的传输记录", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var transferLogIds = selectedRows.Select(row => (int)row.Cells["Id"].Value).ToList();
            
            ShowProgress("正在重置失败的传输...");

            var retryCount = await _transferLogService.RetryFailedTransferChunksAsync(transferLogIds);

            HideProgress();
            MessageBox.Show($"已重置 {retryCount} 个失败的传输记录", "重试成功", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            await LoadTransferLogsAsync();
        }
        catch (Exception ex)
        {
            HideProgress();
            _logger.LogError(ex, "Failed to retry failed transfers for backup {BackupLogId}", _currentBackupLogId);
            MessageBox.Show($"重试失败的传输时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 显示进度
    /// Show progress
    /// </summary>
    private void ShowProgress(string message)
    {
        _progressLabel.Text = message;
        _progressBar.Visible = true;
        _progressBar.Style = ProgressBarStyle.Marquee;
        this.Enabled = false;
    }

    /// <summary>
    /// 隐藏进度
    /// Hide progress
    /// </summary>
    private void HideProgress()
    {
        _progressBar.Visible = false;
        _progressLabel.Text = "准备就绪";
        this.Enabled = true;
    }

    /// <summary>
    /// 格式化文件大小
    /// Format file size
    /// </summary>
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
}