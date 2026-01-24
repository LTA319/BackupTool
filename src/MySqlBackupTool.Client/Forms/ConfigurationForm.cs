using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Client.Forms;

/// <summary>
/// Form for managing backup configurations
/// </summary>
public partial class ConfigurationForm : Form
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ConfigurationForm> _logger;
    private readonly IBackupConfigurationRepository _configRepository;
    private BackupConfiguration _currentConfiguration;
    private bool _isEditing;

    public ConfigurationForm(IServiceProvider serviceProvider, BackupConfiguration? configuration = null)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<ConfigurationForm>>();
        _configRepository = serviceProvider.GetRequiredService<IBackupConfigurationRepository>();
        _currentConfiguration = configuration ?? new BackupConfiguration();
        _isEditing = configuration != null;

        InitializeComponent();
        InitializeForm();
    }

    private void InitializeForm()
    {
        try
        {
            this.Text = _isEditing ? "Edit Backup Configuration" : "New Backup Configuration";
            this.Size = new Size(600, 700);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            LoadConfigurationData();
            
            _logger.LogInformation("Configuration form initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing configuration form");
            MessageBox.Show($"Error initializing form: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadConfigurationData()
    {
        try
        {
            // Load configuration data into form controls
            txtConfigName.Text = _currentConfiguration.Name;
            txtMySqlUsername.Text = _currentConfiguration.MySQLConnection.Username;
            txtMySqlPassword.Text = _currentConfiguration.MySQLConnection.Password;
            txtServiceName.Text = _currentConfiguration.MySQLConnection.ServiceName;
            txtDataDirectory.Text = _currentConfiguration.MySQLConnection.DataDirectoryPath;
            txtMySqlHost.Text = _currentConfiguration.MySQLConnection.Host;
            numMySqlPort.Value = _currentConfiguration.MySQLConnection.Port;

            txtServerIP.Text = _currentConfiguration.TargetServer.IPAddress;
            numServerPort.Value = _currentConfiguration.TargetServer.Port;
            chkUseSSL.Checked = _currentConfiguration.TargetServer.UseSSL;
            txtTargetDirectory.Text = _currentConfiguration.TargetDirectory;

            txtNamingPattern.Text = _currentConfiguration.NamingStrategy.Pattern;
            txtDateFormat.Text = _currentConfiguration.NamingStrategy.DateFormat;
            chkIncludeServerName.Checked = _currentConfiguration.NamingStrategy.IncludeServerName;
            chkIncludeDatabaseName.Checked = _currentConfiguration.NamingStrategy.IncludeDatabaseName;

            chkIsActive.Checked = _currentConfiguration.IsActive;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration data");
            MessageBox.Show($"Error loading configuration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveConfigurationData()
    {
        try
        {
            // Save form data to configuration object
            _currentConfiguration.Name = txtConfigName.Text.Trim();
            _currentConfiguration.MySQLConnection.Username = txtMySqlUsername.Text.Trim();
            _currentConfiguration.MySQLConnection.Password = txtMySqlPassword.Text;
            _currentConfiguration.MySQLConnection.ServiceName = txtServiceName.Text.Trim();
            _currentConfiguration.MySQLConnection.DataDirectoryPath = txtDataDirectory.Text.Trim();
            _currentConfiguration.MySQLConnection.Host = txtMySqlHost.Text.Trim();
            _currentConfiguration.MySQLConnection.Port = (int)numMySqlPort.Value;

            _currentConfiguration.TargetServer.IPAddress = txtServerIP.Text.Trim();
            _currentConfiguration.TargetServer.Port = (int)numServerPort.Value;
            _currentConfiguration.TargetServer.UseSSL = chkUseSSL.Checked;
            _currentConfiguration.TargetDirectory = txtTargetDirectory.Text.Trim();

            _currentConfiguration.NamingStrategy.Pattern = txtNamingPattern.Text.Trim();
            _currentConfiguration.NamingStrategy.DateFormat = txtDateFormat.Text.Trim();
            _currentConfiguration.NamingStrategy.IncludeServerName = chkIncludeServerName.Checked;
            _currentConfiguration.NamingStrategy.IncludeDatabaseName = chkIncludeDatabaseName.Checked;

            _currentConfiguration.IsActive = chkIsActive.Checked;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration data");
            throw;
        }
    }

    private async void btnTestMySqlConnection_Click(object sender, EventArgs e)
    {
        try
        {
            btnTestMySqlConnection.Enabled = false;
            btnTestMySqlConnection.Text = "Testing...";
            lblMySqlConnectionStatus.Text = "Testing connection...";
            lblMySqlConnectionStatus.ForeColor = Color.Blue;

            // Create temporary connection info for testing
            var connectionInfo = new MySQLConnectionInfo
            {
                Username = txtMySqlUsername.Text.Trim(),
                Password = txtMySqlPassword.Text,
                ServiceName = txtServiceName.Text.Trim(),
                DataDirectoryPath = txtDataDirectory.Text.Trim(),
                Host = txtMySqlHost.Text.Trim(),
                Port = (int)numMySqlPort.Value
            };

            var (isValid, errors) = await connectionInfo.ValidateConnectionAsync();

            if (isValid)
            {
                lblMySqlConnectionStatus.Text = "Connection successful!";
                lblMySqlConnectionStatus.ForeColor = Color.Green;
                _logger.LogInformation("MySQL connection test successful");
            }
            else
            {
                lblMySqlConnectionStatus.Text = $"Connection failed: {string.Join(", ", errors)}";
                lblMySqlConnectionStatus.ForeColor = Color.Red;
                _logger.LogWarning("MySQL connection test failed: {Errors}", string.Join(", ", errors));
            }
        }
        catch (Exception ex)
        {
            lblMySqlConnectionStatus.Text = $"Test error: {ex.Message}";
            lblMySqlConnectionStatus.ForeColor = Color.Red;
            _logger.LogError(ex, "Error testing MySQL connection");
        }
        finally
        {
            btnTestMySqlConnection.Enabled = true;
            btnTestMySqlConnection.Text = "Test Connection";
        }
    }

    private async void btnTestServerConnection_Click(object sender, EventArgs e)
    {
        try
        {
            btnTestServerConnection.Enabled = false;
            btnTestServerConnection.Text = "Testing...";
            lblServerConnectionStatus.Text = "Testing connection...";
            lblServerConnectionStatus.ForeColor = Color.Blue;

            // Create temporary server endpoint for testing
            var serverEndpoint = new ServerEndpoint
            {
                IPAddress = txtServerIP.Text.Trim(),
                Port = (int)numServerPort.Value,
                UseSSL = chkUseSSL.Checked
            };

            var (isValid, errors) = serverEndpoint.ValidateEndpoint();

            if (!isValid)
            {
                lblServerConnectionStatus.Text = $"Validation failed: {string.Join(", ", errors)}";
                lblServerConnectionStatus.ForeColor = Color.Red;
                return;
            }

            // Test connectivity
            var isReachable = await serverEndpoint.TestConnectivityAsync();
            if (!isReachable)
            {
                lblServerConnectionStatus.Text = "Server is not reachable";
                lblServerConnectionStatus.ForeColor = Color.Orange;
                return;
            }

            // Test port accessibility
            var isPortAccessible = await serverEndpoint.TestPortAccessibilityAsync();
            if (isPortAccessible)
            {
                lblServerConnectionStatus.Text = "Connection successful!";
                lblServerConnectionStatus.ForeColor = Color.Green;
                _logger.LogInformation("Server connection test successful");
            }
            else
            {
                lblServerConnectionStatus.Text = "Port is not accessible";
                lblServerConnectionStatus.ForeColor = Color.Orange;
                _logger.LogWarning("Server port accessibility test failed");
            }
        }
        catch (Exception ex)
        {
            lblServerConnectionStatus.Text = $"Test error: {ex.Message}";
            lblServerConnectionStatus.ForeColor = Color.Red;
            _logger.LogError(ex, "Error testing server connection");
        }
        finally
        {
            btnTestServerConnection.Enabled = true;
            btnTestServerConnection.Text = "Test Connection";
        }
    }

    private void btnBrowseDataDirectory_Click(object sender, EventArgs e)
    {
        try
        {
            using var dialog = new FolderBrowserDialog();
            dialog.Description = "Select MySQL Data Directory";
            dialog.UseDescriptionForTitle = true;
            
            if (!string.IsNullOrEmpty(txtDataDirectory.Text))
            {
                dialog.SelectedPath = txtDataDirectory.Text;
            }

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                txtDataDirectory.Text = dialog.SelectedPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error browsing for data directory");
            MessageBox.Show($"Error browsing directory: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btnPreviewFileName_Click(object sender, EventArgs e)
    {
        try
        {
            var namingStrategy = new FileNamingStrategy
            {
                Pattern = txtNamingPattern.Text.Trim(),
                DateFormat = txtDateFormat.Text.Trim(),
                IncludeServerName = chkIncludeServerName.Checked,
                IncludeDatabaseName = chkIncludeDatabaseName.Checked
            };

            var (isValid, errors) = namingStrategy.ValidateStrategy();
            if (!isValid)
            {
                MessageBox.Show($"Naming strategy validation failed:\n{string.Join("\n", errors)}", 
                    "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var sampleFileName = namingStrategy.GenerateFileName("SampleServer", "SampleDB", DateTime.Now);
            lblFileNamePreview.Text = $"Preview: {sampleFileName}";
            lblFileNamePreview.ForeColor = Color.Green;
        }
        catch (Exception ex)
        {
            lblFileNamePreview.Text = $"Preview error: {ex.Message}";
            lblFileNamePreview.ForeColor = Color.Red;
            _logger.LogError(ex, "Error generating filename preview");
        }
    }

    private async void btnSave_Click(object sender, EventArgs e)
    {
        try
        {
            btnSave.Enabled = false;
            SaveConfigurationData();

            // Validate configuration
            var validationContext = new ValidationContext(_currentConfiguration);
            var validationResults = new List<ValidationResult>();
            
            if (!Validator.TryValidateObject(_currentConfiguration, validationContext, validationResults, true))
            {
                var errorMessage = string.Join("\n", validationResults.Select(r => r.ErrorMessage));
                MessageBox.Show($"Validation failed:\n{errorMessage}", "Validation Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Save to database
            var (success, errors, savedConfig) = await _configRepository.ValidateAndSaveAsync(_currentConfiguration, false);
            
            if (success && savedConfig != null)
            {
                _currentConfiguration = savedConfig;
                _isEditing = true;
                
                MessageBox.Show("Configuration saved successfully!", "Success", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                var errorMessage = string.Join("\n", errors);
                MessageBox.Show($"Failed to save configuration:\n{errorMessage}", "Save Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration");
            MessageBox.Show($"Error saving configuration: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnSave.Enabled = true;
        }
    }

    private async void btnValidateAndSave_Click(object sender, EventArgs e)
    {
        try
        {
            btnValidateAndSave.Enabled = false;
            btnValidateAndSave.Text = "Validating...";
            
            SaveConfigurationData();

            // Validate configuration with connection testing
            var (success, errors, savedConfig) = await _configRepository.ValidateAndSaveAsync(_currentConfiguration, true);
            
            if (success && savedConfig != null)
            {
                _currentConfiguration = savedConfig;
                _isEditing = true;
                
                MessageBox.Show("Configuration validated and saved successfully!", "Success", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                var errorMessage = string.Join("\n", errors);
                MessageBox.Show($"Validation failed:\n{errorMessage}", "Validation Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating and saving configuration");
            MessageBox.Show($"Error validating configuration: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnValidateAndSave.Enabled = true;
            btnValidateAndSave.Text = "Validate && Save";
        }
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        this.DialogResult = DialogResult.Cancel;
        this.Close();
    }

    public BackupConfiguration? GetConfiguration()
    {
        return this.DialogResult == DialogResult.OK ? _currentConfiguration : null;
    }
}