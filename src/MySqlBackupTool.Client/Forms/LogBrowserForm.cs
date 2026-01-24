using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using MySqlBackupTool.Shared.Services;
using System.ComponentModel;
using System.Text;

namespace MySqlBackupTool.Client.Forms;

/// <summary>
/// Form for browsing and searching backup logs
/// </summary>
public partial class LogBrowserForm : Form
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LogBrowserForm> _logger;
    private readonly IBackupLogRepository _logRepository;
    private readonly IBackupConfigurationRepository _configRepository;
    private readonly BackupReportingService _reportingService;
    
    private List<BackupLog> _allLogs = new();
    private List<BackupLog> _filteredLogs = new();
    private List<BackupConfiguration> _configurations = new();
    private BackupLog? _selectedLog;

    public LogBrowserForm(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<LogBrowserForm>>();
        _logRepository = serviceProvider.GetRequiredService<IBackupLogRepository>();
        _configRepository = serviceProvider.GetRequiredService<IBackupConfigurationRepository>();
        _reportingService = serviceProvider.GetRequiredService<BackupReportingService>();

        InitializeComponent();
        InitializeForm();
    }

    private void InitializeForm()
    {
        try
        {
            this.Text = "Backup Log Browser";
            this.Size = new Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterParent;

            SetupDataGridView();
            SetupFilters();
            LoadData();
            
            _logger.LogInformation("Log browser form initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing log browser form");
            MessageBox.Show($"Error initializing form: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetupDataGridView()
    {
        dgvLogs.AutoGenerateColumns = false;
        dgvLogs.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvLogs.MultiSelect = false;
        dgvLogs.ReadOnly = true;
        dgvLogs.AllowUserToAddRows = false;
        dgvLogs.AllowUserToDeleteRows = false;

        // Add columns
        dgvLogs.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ConfigName",
            HeaderText = "Configuration",
            Width = 120
        });

        dgvLogs.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Status",
            HeaderText = "Status",
            DataPropertyName = "Status",
            Width = 100
        });

        dgvLogs.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "StartTime",
            HeaderText = "Start Time",
            DataPropertyName = "StartTime",
            Width = 130,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm:ss" }
        });

        dgvLogs.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Duration",
            HeaderText = "Duration",
            Width = 80
        });

        dgvLogs.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "FileSize",
            HeaderText = "File Size",
            Width = 80
        });

        dgvLogs.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "FilePath",
            HeaderText = "File Path",
            DataPropertyName = "FilePath",
            Width = 200
        });

        dgvLogs.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ErrorMessage",
            HeaderText = "Error",
            DataPropertyName = "ErrorMessage",
            Width = 150
        });

        // Handle cell formatting and selection
        dgvLogs.CellFormatting += DgvLogs_CellFormatting;
        dgvLogs.SelectionChanged += DgvLogs_SelectionChanged;
        dgvLogs.RowPrePaint += DgvLogs_RowPrePaint;
    }

    private void SetupFilters()
    {
        // Setup date filters
        dtpStartDate.Value = DateTime.Now.AddDays(-30);
        dtpEndDate.Value = DateTime.Now;

        // Setup status filter
        cmbStatus.Items.Add("All Statuses");
        foreach (BackupStatus status in Enum.GetValues<BackupStatus>())
        {
            cmbStatus.Items.Add(status.ToString());
        }
        cmbStatus.SelectedIndex = 0;

        // Configuration filter will be populated when configurations are loaded
        cmbConfiguration.Items.Add("All Configurations");
        cmbConfiguration.SelectedIndex = 0;
    }

    private void DgvLogs_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (dgvLogs.Rows[e.RowIndex].DataBoundItem is BackupLog log)
        {
            switch (dgvLogs.Columns[e.ColumnIndex].Name)
            {
                case "ConfigName":
                    var config = _configurations.FirstOrDefault(c => c.Id == log.BackupConfigId);
                    e.Value = config?.Name ?? "Unknown";
                    break;
                case "Duration":
                    var duration = log.Duration;
                    if (duration.HasValue)
                    {
                        e.Value = $"{duration.Value.Hours:D2}:{duration.Value.Minutes:D2}:{duration.Value.Seconds:D2}";
                    }
                    else if (log.IsRunning)
                    {
                        var elapsed = DateTime.UtcNow - log.StartTime;
                        e.Value = $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
                    }
                    else
                    {
                        e.Value = "N/A";
                    }
                    break;
                case "FileSize":
                    if (log.FileSize.HasValue && log.FileSize.Value > 0)
                    {
                        e.Value = FormatFileSize(log.FileSize.Value);
                    }
                    else
                    {
                        e.Value = "N/A";
                    }
                    break;
            }
        }
    }

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

    private async void LoadData()
    {
        try
        {
            btnRefresh.Enabled = false;
            btnRefresh.Text = "Loading...";
            lblStatus.Text = "Loading data...";
            lblStatus.ForeColor = Color.Blue;

            // Load configurations
            _configurations = (await _configRepository.GetAllAsync()).ToList();
            
            // Update configuration filter
            cmbConfiguration.Items.Clear();
            cmbConfiguration.Items.Add("All Configurations");
            foreach (var config in _configurations)
            {
                cmbConfiguration.Items.Add(config.Name);
            }
            cmbConfiguration.SelectedIndex = 0;

            // Load logs
            await LoadLogs();

            lblStatus.Text = $"Loaded {_allLogs.Count} log entries";
            lblStatus.ForeColor = Color.Green;
            
            _logger.LogInformation("Loaded {Count} log entries", _allLogs.Count);
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"Error loading data: {ex.Message}";
            lblStatus.ForeColor = Color.Red;
            _logger.LogError(ex, "Error loading log data");
        }
        finally
        {
            btnRefresh.Enabled = true;
            btnRefresh.Text = "Refresh";
        }
    }

    private async Task LoadLogs()
    {
        var startDate = dtpStartDate.Value.Date;
        var endDate = dtpEndDate.Value.Date.AddDays(1).AddTicks(-1);
        
        _allLogs = (await _logRepository.GetByDateRangeAsync(startDate, endDate)).ToList();
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        _filteredLogs = _allLogs.Where(log =>
        {
            // Status filter
            if (cmbStatus.SelectedIndex > 0 && cmbStatus.SelectedItem != null)
            {
                var selectedStatus = (BackupStatus)Enum.Parse(typeof(BackupStatus), cmbStatus.SelectedItem.ToString()!);
                if (log.Status != selectedStatus)
                    return false;
            }

            // Configuration filter
            if (cmbConfiguration.SelectedIndex > 0 && cmbConfiguration.SelectedItem != null)
            {
                var selectedConfigName = cmbConfiguration.SelectedItem.ToString()!;
                var config = _configurations.FirstOrDefault(c => c.Name == selectedConfigName);
                if (config == null || log.BackupConfigId != config.Id)
                    return false;
            }

            // Search text filter
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
        lblFilteredCount.Text = $"Showing {_filteredLogs.Count} of {_allLogs.Count} entries";
    }

    private async void LoadLogDetails()
    {
        if (_selectedLog == null)
        {
            txtLogDetails.Clear();
            return;
        }

        try
        {
            // Load detailed log with transfer logs
            var detailedLog = await _logRepository.GetWithTransferLogsAsync(_selectedLog.Id);
            if (detailedLog == null)
            {
                txtLogDetails.Text = "Log details not found.";
                return;
            }

            var sb = new StringBuilder();
            var config = _configurations.FirstOrDefault(c => c.Id == detailedLog.BackupConfigId);

            sb.AppendLine("=== BACKUP LOG DETAILS ===");
            sb.AppendLine($"Configuration: {config?.Name ?? "Unknown"}");
            sb.AppendLine($"Status: {detailedLog.Status}");
            sb.AppendLine($"Start Time: {detailedLog.StartTime:yyyy-MM-dd HH:mm:ss}");
            
            if (detailedLog.EndTime.HasValue)
            {
                sb.AppendLine($"End Time: {detailedLog.EndTime.Value:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Duration: {detailedLog.Duration?.ToString(@"hh\:mm\:ss")}");
            }

            if (!string.IsNullOrEmpty(detailedLog.FilePath))
            {
                sb.AppendLine($"File Path: {detailedLog.FilePath}");
            }

            if (detailedLog.FileSize.HasValue)
            {
                sb.AppendLine($"File Size: {FormatFileSize(detailedLog.FileSize.Value)}");
            }

            if (!string.IsNullOrEmpty(detailedLog.ResumeToken))
            {
                sb.AppendLine($"Resume Token: {detailedLog.ResumeToken}");
            }

            if (!string.IsNullOrEmpty(detailedLog.ErrorMessage))
            {
                sb.AppendLine();
                sb.AppendLine("=== ERROR MESSAGE ===");
                sb.AppendLine(detailedLog.ErrorMessage);
            }

            if (detailedLog.TransferLogs.Any())
            {
                sb.AppendLine();
                sb.AppendLine("=== TRANSFER LOGS ===");
                sb.AppendLine($"{"Chunk",-8} {"Size",-12} {"Time",-20} {"Status",-10}");
                sb.AppendLine(new string('-', 60));

                foreach (var transferLog in detailedLog.TransferLogs.OrderBy(t => t.ChunkIndex))
                {
                    sb.AppendLine($"{transferLog.ChunkIndex,-8} {FormatFileSize(transferLog.ChunkSize),-12} " +
                                $"{transferLog.TransferTime:HH:mm:ss.fff},-20 {transferLog.Status,-10}");
                    
                    if (!string.IsNullOrEmpty(transferLog.ErrorMessage))
                    {
                        sb.AppendLine($"         Error: {transferLog.ErrorMessage}");
                    }
                }
            }

            txtLogDetails.Text = sb.ToString();
        }
        catch (Exception ex)
        {
            txtLogDetails.Text = $"Error loading log details: {ex.Message}";
            _logger.LogError(ex, "Error loading log details for log {LogId}", _selectedLog.Id);
        }
    }

    private async void btnFilter_Click(object sender, EventArgs e)
    {
        try
        {
            await LoadLogs();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying filters");
            MessageBox.Show($"Error applying filters: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btnClearFilters_Click(object sender, EventArgs e)
    {
        dtpStartDate.Value = DateTime.Now.AddDays(-30);
        dtpEndDate.Value = DateTime.Now;
        cmbStatus.SelectedIndex = 0;
        cmbConfiguration.SelectedIndex = 0;
        txtSearch.Clear();
        ApplyFilters();
    }

    private void txtSearch_TextChanged(object sender, EventArgs e)
    {
        ApplyFilters();
    }

    private void cmbStatus_SelectedIndexChanged(object sender, EventArgs e)
    {
        ApplyFilters();
    }

    private void cmbConfiguration_SelectedIndexChanged(object sender, EventArgs e)
    {
        ApplyFilters();
    }

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
            _logger.LogError(ex, "Error showing log details");
            MessageBox.Show($"Error showing details: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void btnGenerateReport_Click(object sender, EventArgs e)
    {
        try
        {
            btnGenerateReport.Enabled = false;
            btnGenerateReport.Text = "Generating...";

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
            _logger.LogError(ex, "Error generating report");
            MessageBox.Show($"Error generating report: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnGenerateReport.Enabled = true;
            btnGenerateReport.Text = "Generate Report";
        }
    }

    private void btnExportLog_Click(object sender, EventArgs e)
    {
        if (_selectedLog == null)
            return;

        try
        {
            using var saveDialog = new SaveFileDialog();
            saveDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
            saveDialog.FileName = $"backup_log_{_selectedLog.Id}_{_selectedLog.StartTime:yyyyMMdd_HHmmss}.txt";

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                File.WriteAllText(saveDialog.FileName, txtLogDetails.Text);
                MessageBox.Show($"Log exported successfully to:\n{saveDialog.FileName}", "Export Complete", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting log");
            MessageBox.Show($"Error exporting log: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btnRefresh_Click(object sender, EventArgs e)
    {
        LoadData();
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        this.Close();
    }

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
}