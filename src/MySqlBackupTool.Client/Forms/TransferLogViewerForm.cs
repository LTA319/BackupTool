using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using System.ComponentModel;
using System.Data;

namespace MySqlBackupTool.Client.Forms;

/// <summary>
/// 传输日志查看器窗体
/// Transfer log viewer form
/// </summary>
public partial class TransferLogViewerForm : Form
{
    /// <summary>
    /// 依赖注入服务提供者，用于获取各种服务实例
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    private readonly ITransferLogService _transferLogService;
    private readonly ILogger<TransferLogViewerForm> _logger;

    private int _currentBackupLogId;

    // UI 控件在设计文件中定义

    /// <summary>
    /// 构造函数
    /// Constructor
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    public TransferLogViewerForm(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _transferLogService = serviceProvider.GetRequiredService<ITransferLogService>();
        _logger = serviceProvider.GetRequiredService<ILogger<TransferLogViewerForm>>();
        
        InitializeComponent();
        InitializeEventHandlers();
        InitializeFormSettings();
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
    /// 初始化窗体设置
    /// Initialize form settings
    /// </summary>
    private void InitializeFormSettings()
    {
        // 设置状态过滤器默认值
        _statusFilter.SelectedIndex = 0;
        
        // 设置日期选择器默认值
        _startDatePicker.Value = DateTime.Today.AddDays(-7);
        _endDatePicker.Value = DateTime.Today.AddDays(1);
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