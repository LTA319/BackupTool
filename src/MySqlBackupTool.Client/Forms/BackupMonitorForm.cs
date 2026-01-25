using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.ComponentModel;

namespace MySqlBackupTool.Client.Forms;

/// <summary>
/// Form for monitoring and controlling backup operations
/// </summary>
public partial class BackupMonitorForm : Form
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackupMonitorForm> _logger;
    private readonly IBackupConfigurationRepository _configRepository;
    private readonly IBackupLogRepository _logRepository;
    private readonly IBackupOrchestrator? _backupOrchestrator;
    
    private List<BackupConfiguration> _configurations = new();
    private List<BackupLog> _runningBackups = new();
    private CancellationTokenSource? _currentBackupCancellation;
    private BackupProgress? _currentProgress;
    private System.Windows.Forms.Timer? _refreshTimer;
    private bool _refreshInProgress = false;

    public BackupMonitorForm(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<BackupMonitorForm>>();
        _configRepository = serviceProvider.GetRequiredService<IBackupConfigurationRepository>();
        _logRepository = serviceProvider.GetRequiredService<IBackupLogRepository>();
        
        // Try to get backup orchestrator (may not be available in all configurations)
        _backupOrchestrator = serviceProvider.GetService<IBackupOrchestrator>();

        InitializeComponent();
        InitializeForm();
    }

    private void InitializeForm()
    {
        try
        {
            this.Text = "Backup Monitor & Control";
            this.Size = new Size(900, 700);
            this.StartPosition = FormStartPosition.CenterParent;

            SetupDataGridViews();
            SetupRefreshTimer();
            
            // Load data asynchronously to avoid blocking UI thread
            _ = LoadDataAsync();
            
            _logger.LogInformation("Backup monitor form initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing backup monitor form");
            MessageBox.Show($"Error initializing form: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetupDataGridViews()
    {
        // Setup configurations grid
        dgvConfigurations.AutoGenerateColumns = false;
        dgvConfigurations.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvConfigurations.MultiSelect = false;
        dgvConfigurations.ReadOnly = true;
        dgvConfigurations.AllowUserToAddRows = false;

        dgvConfigurations.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Name",
            HeaderText = "Configuration",
            DataPropertyName = "Name",
            Width = 150
        });

        dgvConfigurations.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "IsActive",
            HeaderText = "Active",
            DataPropertyName = "IsActive",
            Width = 60
        });

        dgvConfigurations.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "MySQLHost",
            HeaderText = "MySQL Host",
            Width = 100
        });

        dgvConfigurations.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "TargetServer",
            HeaderText = "Target Server",
            Width = 100
        });

        // Setup running backups grid
        dgvRunningBackups.AutoGenerateColumns = false;
        dgvRunningBackups.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvRunningBackups.MultiSelect = false;
        dgvRunningBackups.ReadOnly = true;
        dgvRunningBackups.AllowUserToAddRows = false;

        dgvRunningBackups.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ConfigName",
            HeaderText = "Configuration",
            Width = 120
        });

        dgvRunningBackups.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Status",
            HeaderText = "Status",
            DataPropertyName = "Status",
            Width = 100
        });

        dgvRunningBackups.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "StartTime",
            HeaderText = "Started",
            DataPropertyName = "StartTime",
            Width = 120,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "HH:mm:ss" }
        });

        dgvRunningBackups.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Duration",
            HeaderText = "Duration",
            Width = 80
        });

        dgvRunningBackups.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Progress",
            HeaderText = "Progress",
            Width = 80
        });

        // Handle cell formatting
        dgvConfigurations.CellFormatting += DgvConfigurations_CellFormatting;
        dgvRunningBackups.CellFormatting += DgvRunningBackups_CellFormatting;
        
        // Handle selection changes
        dgvConfigurations.SelectionChanged += DgvConfigurations_SelectionChanged;
        dgvRunningBackups.SelectionChanged += DgvRunningBackups_SelectionChanged;
    }

    private void SetupRefreshTimer()
    {
        _refreshTimer = new System.Windows.Forms.Timer();
        _refreshTimer.Interval = 2000; // Refresh every 2 seconds
        _refreshTimer.Tick += RefreshTimer_Tick;
        _refreshTimer.Start();
    }

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

    private void DgvRunningBackups_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (dgvRunningBackups.Rows[e.RowIndex].DataBoundItem is BackupLog log)
        {
            switch (dgvRunningBackups.Columns[e.ColumnIndex].Name)
            {
                case "ConfigName":
                    var config = _configurations.FirstOrDefault(c => c.Id == log.BackupConfigId);
                    e.Value = config?.Name ?? "Unknown";
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
                        e.Value = "N/A";
                    }
                    break;
            }
        }
    }

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

    private void DgvRunningBackups_SelectionChanged(object? sender, EventArgs e)
    {
        var hasSelection = dgvRunningBackups.SelectedRows.Count > 0;
        btnCancelBackup.Enabled = hasSelection && _currentBackupCancellation != null;
    }

    private async Task LoadDataAsync()
    {
        try
        {
            // Show loading status
            if (InvokeRequired)
            {
                Invoke(new Action(() => {
                    lblStatus.Text = "Loading data...";
                    lblStatus.ForeColor = Color.Blue;
                }));
            }
            else
            {
                lblStatus.Text = "Loading data...";
                lblStatus.ForeColor = Color.Blue;
            }

            await LoadConfigurations();
            await LoadRunningBackups();
            
            // Update UI on main thread
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
            _logger.LogError(ex, "Error loading data");
            
            // Update UI on main thread
            if (InvokeRequired)
            {
                Invoke(new Action(() => {
                    lblStatus.Text = $"Error loading data: {ex.Message}";
                    lblStatus.ForeColor = Color.Red;
                }));
            }
            else
            {
                lblStatus.Text = $"Error loading data: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
            }
        }
    }

    private async void LoadData()
    {
        await LoadDataAsync();
    }

    private async Task LoadConfigurations()
    {
        try
        {
            // Add timeout to prevent hanging
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            _configurations = (await _configRepository.GetActiveConfigurationsAsync()).ToList();
            
            // Update UI on main thread
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
            _logger.LogWarning("Loading configurations timed out after 10 seconds");
            throw new TimeoutException("Loading configurations timed out. Please check database connection.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configurations from database");
            throw;
        }
    }

    private async Task LoadRunningBackups()
    {
        try
        {
            // Add timeout to prevent hanging
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            _runningBackups = (await _logRepository.GetRunningBackupsAsync()).ToList();
            
            // Update UI on main thread
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
            _logger.LogWarning("Loading running backups timed out after 10 seconds");
            throw new TimeoutException("Loading running backups timed out. Please check database connection.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading running backups from database");
            throw;
        }
    }

    private void UpdateStatus()
    {
        var activeConfigs = _configurations.Count(c => c.IsActive);
        var runningBackups = _runningBackups.Count;

        lblStatus.Text = $"Active Configurations: {activeConfigs} | Running Backups: {runningBackups}";
        lblStatus.ForeColor = runningBackups > 0 ? Color.Blue : Color.Green;

        // Update progress display
        if (_currentProgress != null)
        {
            progressBar.Value = (int)Math.Min(100, Math.Max(0, _currentProgress.PercentComplete));
            lblProgressDetails.Text = $"{_currentProgress.CurrentOperation} - {_currentProgress.PercentComplete:F1}% " +
                                    $"({_currentProgress.TransferRateString})";
            
            if (_currentProgress.EstimatedTimeRemaining.HasValue)
            {
                var eta = _currentProgress.EstimatedTimeRemaining.Value;
                lblProgressDetails.Text += $" - ETA: {eta.Hours:D2}:{eta.Minutes:D2}:{eta.Seconds:D2}";
            }
        }
        else
        {
            progressBar.Value = 0;
            lblProgressDetails.Text = "No backup operation in progress";
        }
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        // Prevent overlapping refresh operations
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
            _logger.LogError(ex, "Error refreshing data");
            // Don't show error message box during automatic refresh to avoid spam
            lblStatus.Text = $"Refresh error: {ex.Message}";
            lblStatus.ForeColor = Color.Red;
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

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
                MessageBox.Show("A backup operation is already in progress.", "Backup In Progress", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Confirm backup start
            var result = MessageBox.Show(
                $"Start backup for configuration '{selectedConfig.Name}'?",
                "Confirm Backup",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            // Start backup operation
            _currentBackupCancellation = new CancellationTokenSource();
            btnStartBackup.Enabled = false;
            btnCancelBackup.Enabled = true;

            var progress = new Progress<BackupProgress>(OnBackupProgress);

            try
            {
                lblStatus.Text = $"Starting backup for '{selectedConfig.Name}'...";
                lblStatus.ForeColor = Color.Blue;

                var backupResult = await _backupOrchestrator.ExecuteBackupAsync(
                    selectedConfig, 
                    progress, 
                    _currentBackupCancellation.Token);

                if (backupResult.Success)
                {
                    lblStatus.Text = $"Backup completed successfully for '{selectedConfig.Name}'";
                    lblStatus.ForeColor = Color.Green;
                    
                    MessageBox.Show($"Backup completed successfully!\n\nFile: {backupResult.BackupFilePath}\nSize: {FormatFileSize(backupResult.FileSize)}\nDuration: {backupResult.Duration:hh\\:mm\\:ss}", 
                        "Backup Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    lblStatus.Text = $"Backup failed for '{selectedConfig.Name}': {backupResult.ErrorMessage}";
                    lblStatus.ForeColor = Color.Red;
                    
                    MessageBox.Show($"Backup failed:\n\n{backupResult.ErrorMessage}", 
                        "Backup Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (OperationCanceledException)
            {
                lblStatus.Text = $"Backup cancelled for '{selectedConfig.Name}'";
                lblStatus.ForeColor = Color.Orange;
                
                MessageBox.Show("Backup operation was cancelled.", "Backup Cancelled", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Backup error for '{selectedConfig.Name}': {ex.Message}";
                lblStatus.ForeColor = Color.Red;
                
                _logger.LogError(ex, "Error during backup operation");
                MessageBox.Show($"Backup error:\n\n{ex.Message}", "Backup Error", 
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
            _logger.LogError(ex, "Error starting backup");
            MessageBox.Show($"Error starting backup: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btnCancelBackup_Click(object sender, EventArgs e)
    {
        try
        {
            if (_currentBackupCancellation == null)
                return;

            var result = MessageBox.Show(
                "Are you sure you want to cancel the current backup operation?",
                "Confirm Cancel",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                _currentBackupCancellation.Cancel();
                btnCancelBackup.Enabled = false;
                lblStatus.Text = "Cancelling backup operation...";
                lblStatus.ForeColor = Color.Orange;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling backup");
            MessageBox.Show($"Error cancelling backup: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

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

    private int GetCurrentBackupLogId()
    {
        // This would need to be implemented based on how backup logs are tracked
        // For now, return the most recent running backup
        return _runningBackups.FirstOrDefault()?.Id ?? 0;
    }

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

    private void btnRefresh_Click(object sender, EventArgs e)
    {
        LoadData();
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        _refreshTimer?.Stop();
        _currentBackupCancellation?.Cancel();
        this.Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
        _currentBackupCancellation?.Dispose();
        base.OnFormClosing(e);
    }
}