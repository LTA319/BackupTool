using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Client.Forms;

/// <summary>
/// Form for managing the list of backup configurations
/// </summary>
public partial class ConfigurationListForm : Form
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ConfigurationListForm> _logger;
    private readonly IBackupConfigurationRepository _configRepository;
    private List<BackupConfiguration> _configurations = new();

    public ConfigurationListForm(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<ConfigurationListForm>>();
        _configRepository = serviceProvider.GetRequiredService<IBackupConfigurationRepository>();

        InitializeComponent();
        InitializeForm();
    }

    private void InitializeForm()
    {
        try
        {
            this.Text = "Backup Configurations";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterParent;

            SetupDataGridView();
            LoadConfigurations();
            
            _logger.LogInformation("Configuration list form initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing configuration list form");
            MessageBox.Show($"Error initializing form: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetupDataGridView()
    {
        dgvConfigurations.AutoGenerateColumns = false;
        dgvConfigurations.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvConfigurations.MultiSelect = false;
        dgvConfigurations.ReadOnly = true;
        dgvConfigurations.AllowUserToAddRows = false;
        dgvConfigurations.AllowUserToDeleteRows = false;

        // Add columns
        dgvConfigurations.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Name",
            HeaderText = "Configuration Name",
            DataPropertyName = "Name",
            Width = 200
        });

        dgvConfigurations.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "MySQLHost",
            HeaderText = "MySQL Host",
            DataPropertyName = "MySQLConnection.Host",
            Width = 120
        });

        dgvConfigurations.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "TargetServer",
            HeaderText = "Target Server",
            DataPropertyName = "TargetServer.IPAddress",
            Width = 120
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
            Name = "CreatedAt",
            HeaderText = "Created",
            DataPropertyName = "CreatedAt",
            Width = 120,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm" }
        });

        // Handle cell formatting for nested properties
        dgvConfigurations.CellFormatting += DgvConfigurations_CellFormatting;
        dgvConfigurations.SelectionChanged += DgvConfigurations_SelectionChanged;
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

    private void DgvConfigurations_SelectionChanged(object? sender, EventArgs e)
    {
        var hasSelection = dgvConfigurations.SelectedRows.Count > 0;
        btnEdit.Enabled = hasSelection;
        btnDelete.Enabled = hasSelection;
        btnActivate.Enabled = hasSelection;
        btnDeactivate.Enabled = hasSelection;

        if (hasSelection && dgvConfigurations.SelectedRows[0].DataBoundItem is BackupConfiguration config)
        {
            btnActivate.Enabled = !config.IsActive;
            btnDeactivate.Enabled = config.IsActive;
        }
    }

    private async void LoadConfigurations()
    {
        try
        {
            btnRefresh.Enabled = false;
            btnRefresh.Text = "Loading...";

            _configurations = (await _configRepository.GetAllAsync()).ToList();
            dgvConfigurations.DataSource = _configurations;

            lblStatus.Text = $"Loaded {_configurations.Count} configuration(s)";
            lblStatus.ForeColor = Color.Green;

            _logger.LogInformation("Loaded {Count} configurations", _configurations.Count);
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"Error loading configurations: {ex.Message}";
            lblStatus.ForeColor = Color.Red;
            _logger.LogError(ex, "Error loading configurations");
        }
        finally
        {
            btnRefresh.Enabled = true;
            btnRefresh.Text = "Refresh";
        }
    }

    private void btnNew_Click(object sender, EventArgs e)
    {
        try
        {
            using var configForm = new ConfigurationForm(_serviceProvider);
            if (configForm.ShowDialog() == DialogResult.OK)
            {
                LoadConfigurations();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating new configuration");
            MessageBox.Show($"Error creating configuration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btnEdit_Click(object sender, EventArgs e)
    {
        try
        {
            if (dgvConfigurations.SelectedRows.Count == 0)
                return;

            var selectedConfig = dgvConfigurations.SelectedRows[0].DataBoundItem as BackupConfiguration;
            if (selectedConfig == null)
                return;

            using var configForm = new ConfigurationForm(_serviceProvider, selectedConfig);
            if (configForm.ShowDialog() == DialogResult.OK)
            {
                LoadConfigurations();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing configuration");
            MessageBox.Show($"Error editing configuration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void btnDelete_Click(object sender, EventArgs e)
    {
        try
        {
            if (dgvConfigurations.SelectedRows.Count == 0)
                return;

            var selectedConfig = dgvConfigurations.SelectedRows[0].DataBoundItem as BackupConfiguration;
            if (selectedConfig == null)
                return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete the configuration '{selectedConfig.Name}'?\n\nThis action cannot be undone.",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                await _configRepository.DeleteAsync(selectedConfig.Id);
                LoadConfigurations();
                
                lblStatus.Text = $"Configuration '{selectedConfig.Name}' deleted successfully";
                lblStatus.ForeColor = Color.Green;
                
                _logger.LogInformation("Deleted configuration: {Name}", selectedConfig.Name);
            }
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"Error deleting configuration: {ex.Message}";
            lblStatus.ForeColor = Color.Red;
            _logger.LogError(ex, "Error deleting configuration");
        }
    }

    private async void btnActivate_Click(object sender, EventArgs e)
    {
        try
        {
            if (dgvConfigurations.SelectedRows.Count == 0)
                return;

            var selectedConfig = dgvConfigurations.SelectedRows[0].DataBoundItem as BackupConfiguration;
            if (selectedConfig == null)
                return;

            var success = await _configRepository.ActivateConfigurationAsync(selectedConfig.Id);
            if (success)
            {
                LoadConfigurations();
                lblStatus.Text = $"Configuration '{selectedConfig.Name}' activated";
                lblStatus.ForeColor = Color.Green;
                _logger.LogInformation("Activated configuration: {Name}", selectedConfig.Name);
            }
            else
            {
                lblStatus.Text = "Failed to activate configuration";
                lblStatus.ForeColor = Color.Red;
            }
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"Error activating configuration: {ex.Message}";
            lblStatus.ForeColor = Color.Red;
            _logger.LogError(ex, "Error activating configuration");
        }
    }

    private async void btnDeactivate_Click(object sender, EventArgs e)
    {
        try
        {
            if (dgvConfigurations.SelectedRows.Count == 0)
                return;

            var selectedConfig = dgvConfigurations.SelectedRows[0].DataBoundItem as BackupConfiguration;
            if (selectedConfig == null)
                return;

            var success = await _configRepository.DeactivateConfigurationAsync(selectedConfig.Id);
            if (success)
            {
                LoadConfigurations();
                lblStatus.Text = $"Configuration '{selectedConfig.Name}' deactivated";
                lblStatus.ForeColor = Color.Green;
                _logger.LogInformation("Deactivated configuration: {Name}", selectedConfig.Name);
            }
            else
            {
                lblStatus.Text = "Failed to deactivate configuration";
                lblStatus.ForeColor = Color.Red;
            }
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"Error deactivating configuration: {ex.Message}";
            lblStatus.ForeColor = Color.Red;
            _logger.LogError(ex, "Error deactivating configuration");
        }
    }

    private void btnRefresh_Click(object sender, EventArgs e)
    {
        LoadConfigurations();
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        this.Close();
    }

    public BackupConfiguration? GetSelectedConfiguration()
    {
        if (dgvConfigurations.SelectedRows.Count > 0)
        {
            return dgvConfigurations.SelectedRows[0].DataBoundItem as BackupConfiguration;
        }
        return null;
    }
}