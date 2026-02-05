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
        // 检查当前线程的STA状态（用于COM组件），但不尝试更改
        try
        {
            var apartmentState = Thread.CurrentThread.GetApartmentState();
            if (apartmentState != ApartmentState.STA)
            {
                _logger.LogWarning("Thread is in {ApartmentState} mode, not STA. File dialogs may have compatibility issues.", apartmentState);
                // 不尝试更改线程状态，因为这在运行时通常会失败
                // 文件对话框仍然可能工作，如果不工作会自动回退到备选方案
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
        InitializeEventHandlers();
        InitializeFormSettings();

        _ = LoadTransferLogsAsync();
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

            IEnumerable<TransferLog> transferLogs;
            
            // 根据状态过滤器获取不同的传输日志
            if (_statusFilter.SelectedIndex == 0) // "全部"
            {
                // 获取所有传输日志
                if (_currentBackupLogId == 0)
                {
                    transferLogs = await _transferLogService.GetAllTransferLogsAsync(null, _startDatePicker.Value, _endDatePicker.Value);
                }
                else 
                {
                    transferLogs = await _transferLogService.GetAllTransferLogsAsync(_currentBackupLogId, _startDatePicker.Value, _endDatePicker.Value);
                }

            }
            else
            {
                // 获取特定状态的传输日志
                var selectedStatus = _statusFilter.SelectedItem.ToString();
                if (selectedStatus == "Failed")
                {
                    transferLogs = await _transferLogService.GetFailedTransferChunksAsync();
                }
                else
                {
                    transferLogs = await _transferLogService.GetTransferLogsByStatusAsync(null, selectedStatus, _startDatePicker.Value, _endDatePicker.Value);
                }
            }

            // 转换为显示用的数据
            var displayData = transferLogs.Select(tl => new
            {
                tl.Id,
                tl.ChunkIndex,
                ChunkSizeFormatted = FormatFileSize(tl.ChunkSize),
                tl.Status,
                TransferTime = tl.TransferTime.ToString("yyyy-MM-dd HH:mm:ss"),
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
            var selectedRows = _transferLogGrid.SelectedRows.Cast<DataGridViewRow>().ToList();
            
            // 如果没有选择行，提示用户
            if (!selectedRows.Any())
            {
                MessageBox.Show("请选择要导出的传输记录", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 询问用户导出格式
            var formatResult = MessageBox.Show("选择导出格式:\n\n是 = CSV格式\n否 = JSON格式", 
                "选择导出格式", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            
            if (formatResult == DialogResult.Cancel)
            {
                return;
            }

            var format = formatResult == DialogResult.Yes ? "CSV" : "JSON";
            var extension = format.ToLowerInvariant();

            // 选择保存位置
            var selectedPath = ShowFolderDialogSafe();
            if (selectedPath == null)
            {
                return; // 用户取消了选择
            }

            ShowProgress("正在导出选中的传输日志...");

            // 获取选中行的传输日志ID
            var selectedTransferLogIds = new List<int>();
            foreach (DataGridViewRow row in selectedRows)
            {
                if (row.DataBoundItem != null)
                {
                    // 从绑定的数据对象中获取ID
                    var dataItem = row.DataBoundItem;
                    var idProperty = dataItem.GetType().GetProperty("Id");
                    if (idProperty != null)
                    {
                        var id = (int)idProperty.GetValue(dataItem)!;
                        selectedTransferLogIds.Add(id);
                    }
                }
            }
            
            // 从服务获取完整的传输日志数据
            var allTransferLogs = await _transferLogService.GetAllTransferLogsAsync();
            var selectedTransferLogs = allTransferLogs.Where(tl => selectedTransferLogIds.Contains(tl.Id)).ToList();

            var exportData = ExportSelectedTransferLogs(selectedTransferLogs, format);

            // 构建完整的文件路径
            var fileName = $"selected_transfer_logs_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}";
            var filePath = Path.Combine(selectedPath, fileName);

            // 确保目录存在
            Directory.CreateDirectory(selectedPath);

            await File.WriteAllBytesAsync(filePath, exportData);

            HideProgress();
            
            // 询问是否打开文件位置
            var openResult = MessageBox.Show($"已成功导出 {selectedTransferLogs.Count} 条传输日志到:\n{filePath}\n\n是否打开文件位置？", 
                "导出成功", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            
            if (openResult == DialogResult.Yes)
            {
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to open file location");
                    MessageBox.Show($"无法打开文件位置: {ex.Message}", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            HideProgress();
            _logger.LogError(ex, "Failed to export selected transfer logs");
            MessageBox.Show($"导出传输日志失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 安全地显示文件夹选择对话框
    /// Show folder dialog safely
    /// </summary>
    private string? ShowFolderDialogSafe()
    {
        // 确保在UI线程上执行
        if (InvokeRequired)
        {
            return Invoke(new Func<string?>(() => ShowFolderDialogSafe()));
        }

        // 检查当前线程的单元状态，但不尝试更改
        var apartmentState = Thread.CurrentThread.GetApartmentState();
        if (apartmentState != ApartmentState.STA)
        {
            _logger.LogWarning("Current thread is in {ApartmentState} mode, not STA. File dialogs may have issues.", apartmentState);
        }

        try
        {
            // 如果当前线程不是STA，尝试在STA线程中运行对话框
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                _logger.LogInformation("Running folder dialog in STA thread");
                return RunDialogInSTAThread(() => ShowOpenFileDialog());
            }
            else
            {
                return ShowOpenFileDialog();
            }
        }
        catch (ThreadStateException ex)
        {
            _logger.LogError(ex, "STA thread state exception in folder dialog, using desktop as fallback");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Folder dialog failed, using desktop as fallback");
        }
        return null;
    }

    /// <summary>
    /// 在STA线程中运行对话框
    /// Run dialog in STA thread
    /// </summary>
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

    /// <summary>
    /// 显示文件选择对话框来选择文件夹
    /// Show file dialog to select folder
    /// </summary>
    private string? ShowOpenFileDialog()
    {
        using var openDialog = new OpenFileDialog();
        openDialog.Filter = "All files (*.*)|*.*";
        openDialog.FileName = "选择此文件夹中的任意文件";
        openDialog.CheckFileExists = false;
        openDialog.CheckPathExists = true;
        openDialog.Multiselect = false;
        openDialog.Title = "选择导出文件夹";

        var result = openDialog.ShowDialog(this);
        if (result == DialogResult.OK)
        {
            var selectedDir = Path.GetDirectoryName(openDialog.FileName);
            if (!string.IsNullOrEmpty(selectedDir))
            {
                _logger.LogInformation("Folder selected: {Path}", selectedDir);
                return selectedDir;
            }
        }

        return null;
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

            var transferLogIds = new List<int>();
            foreach (DataGridViewRow row in selectedRows)
            {
                if (row.DataBoundItem != null)
                {
                    // 从绑定的数据对象中获取ID
                    var dataItem = row.DataBoundItem;
                    var idProperty = dataItem.GetType().GetProperty("Id");
                    if (idProperty != null)
                    {
                        var id = (int)idProperty.GetValue(dataItem)!;
                        transferLogIds.Add(id);
                    }
                }
            }
            
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

    /// <summary>
    /// 导出选中的传输日志
    /// Export selected transfer logs
    /// </summary>
    private byte[] ExportSelectedTransferLogs(IEnumerable<TransferLog> transferLogs, string format)
    {
        switch (format.ToUpperInvariant())
        {
            case "CSV":
                return ExportToCsv(transferLogs);
            case "JSON":
                return ExportToJson(transferLogs);
            default:
                throw new ArgumentException($"不支持的导出格式: {format}");
        }
    }

    /// <summary>
    /// 导出为CSV格式
    /// Export to CSV format
    /// </summary>
    private byte[] ExportToCsv(IEnumerable<TransferLog> transferLogs)
    {
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("ID,备份日志ID,分块索引,分块大小,传输时间,状态,错误消息");

        foreach (var log in transferLogs)
        {
            csv.AppendLine($"{log.Id},{log.BackupLogId},{log.ChunkIndex},{log.ChunkSize}," +
                          $"{log.TransferTime:yyyy-MM-dd HH:mm:ss},{log.Status}," +
                          $"\"{log.ErrorMessage?.Replace("\"", "\"\"")}\"");
        }

        return System.Text.Encoding.UTF8.GetBytes(csv.ToString());
    }

    /// <summary>
    /// 导出为JSON格式
    /// Export to JSON format
    /// </summary>
    private byte[] ExportToJson(IEnumerable<TransferLog> transferLogs)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        var json = System.Text.Json.JsonSerializer.Serialize(transferLogs, options);
        return System.Text.Encoding.UTF8.GetBytes(json);
    }
}