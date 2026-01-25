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
            // 移除固定大小和边框样式设置，允许用户调整窗体大小
            // this.Size = new Size(600, 700);
            // this.StartPosition = FormStartPosition.CenterParent;
            // this.FormBorderStyle = FormBorderStyle.FixedDialog;
            // this.MaximizeBox = false;
            // this.MinimizeBox = false;

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
            
            // Use BackupConfiguration level properties if available, otherwise fall back to MySQLConnection properties
            txtServiceName.Text = !string.IsNullOrEmpty(_currentConfiguration.ServiceName) 
                ? _currentConfiguration.ServiceName 
                : _currentConfiguration.MySQLConnection.ServiceName;
            txtDataDirectory.Text = !string.IsNullOrEmpty(_currentConfiguration.DataDirectoryPath) 
                ? _currentConfiguration.DataDirectoryPath 
                : _currentConfiguration.MySQLConnection.DataDirectoryPath;
                
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
            
            // Set MySQL connection properties
            _currentConfiguration.MySQLConnection.Username = txtMySqlUsername.Text.Trim();
            _currentConfiguration.MySQLConnection.Password = txtMySqlPassword.Text;
            _currentConfiguration.MySQLConnection.ServiceName = txtServiceName.Text.Trim();
            _currentConfiguration.MySQLConnection.DataDirectoryPath = txtDataDirectory.Text.Trim();
            _currentConfiguration.MySQLConnection.Host = txtMySqlHost.Text.Trim();
            _currentConfiguration.MySQLConnection.Port = (int)numMySqlPort.Value;

            // Also set the BackupConfiguration level properties (these are required for validation)
            _currentConfiguration.ServiceName = txtServiceName.Text.Trim();
            _currentConfiguration.DataDirectoryPath = txtDataDirectory.Text.Trim();

            // Set target server properties
            _currentConfiguration.TargetServer.IPAddress = txtServerIP.Text.Trim();
            _currentConfiguration.TargetServer.Port = (int)numServerPort.Value;
            _currentConfiguration.TargetServer.UseSSL = chkUseSSL.Checked;
            _currentConfiguration.TargetDirectory = txtTargetDirectory.Text.Trim();

            // Set naming strategy properties with defaults if empty
            _currentConfiguration.NamingStrategy.Pattern = !string.IsNullOrWhiteSpace(txtNamingPattern.Text.Trim()) 
                ? txtNamingPattern.Text.Trim() 
                : "{timestamp}_{database}_{server}.zip";
            _currentConfiguration.NamingStrategy.DateFormat = !string.IsNullOrWhiteSpace(txtDateFormat.Text.Trim()) 
                ? txtDateFormat.Text.Trim() 
                : "yyyyMMdd_HHmmss";
            _currentConfiguration.NamingStrategy.IncludeServerName = chkIncludeServerName.Checked;
            _currentConfiguration.NamingStrategy.IncludeDatabaseName = chkIncludeDatabaseName.Checked;

            _currentConfiguration.IsActive = chkIsActive.Checked;

            // Ensure all required fields have values
            if (string.IsNullOrWhiteSpace(_currentConfiguration.Name))
            {
                throw new InvalidOperationException("Configuration name is required");
            }
            if (string.IsNullOrWhiteSpace(_currentConfiguration.ServiceName))
            {
                throw new InvalidOperationException("Service name is required");
            }
            if (string.IsNullOrWhiteSpace(_currentConfiguration.DataDirectoryPath))
            {
                throw new InvalidOperationException("Data directory path is required");
            }
            if (string.IsNullOrWhiteSpace(_currentConfiguration.TargetDirectory))
            {
                throw new InvalidOperationException("Target directory is required");
            }
            if (string.IsNullOrWhiteSpace(_currentConfiguration.TargetServer.IPAddress))
            {
                throw new InvalidOperationException("Target server IP address is required");
            }
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
            // 禁用按钮防止重复点击
            btnBrowseDataDirectory.Enabled = false;
            btnBrowseDataDirectory.Text = "Browsing...";
            
            var selectedPath = ShowFolderDialogSafe("Select MySQL Data Directory", txtDataDirectory.Text);
            if (!string.IsNullOrEmpty(selectedPath))
            {
                txtDataDirectory.Text = selectedPath;
                _logger.LogInformation("Selected MySQL data directory: {Path}", selectedPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error browsing for data directory");
            MessageBox.Show($"Error browsing directory: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            // 恢复按钮状态
            btnBrowseDataDirectory.Enabled = true;
            btnBrowseDataDirectory.Text = "Browse...";
        }
    }

    /// <summary>
    /// 安全地显示文件夹选择对话框，使用多种备选方案避免卡死
    /// </summary>
    private string? ShowFolderDialogSafe(string description, string? initialPath = null)
    {
        // 方法1：尝试使用现代的SaveFileDialog方式
        try
        {
            using var dialog = new SaveFileDialog();
            dialog.Title = description;
            dialog.Filter = "Select Folder|*.folder";
            dialog.FileName = "Select this folder";
            dialog.CheckPathExists = true;
            dialog.CheckFileExists = false;
            dialog.CreatePrompt = false;
            dialog.OverwritePrompt = false;
            dialog.ValidateNames = false;
            dialog.AddExtension = false;
            
            // 设置初始目录
            if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath))
            {
                dialog.InitialDirectory = initialPath;
            }
            else
            {
                // 尝试常见的MySQL数据目录位置
                var commonPaths = new[]
                {
                    @"C:\ProgramData\MySQL\MySQL Server 8.0\Data",
                    @"C:\ProgramData\MySQL\MySQL Server 5.7\Data",
                    @"C:\Program Files\MySQL\MySQL Server 8.0\data",
                    @"C:\mysql\data",
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };
                
                foreach (var path in commonPaths)
                {
                    if (Directory.Exists(path))
                    {
                        dialog.InitialDirectory = path;
                        break;
                    }
                }
            }

            var result = dialog.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                var selectedDir = Path.GetDirectoryName(dialog.FileName);
                _logger.LogInformation("Folder selected via SaveFileDialog: {Path}", selectedDir);
                return selectedDir;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SaveFileDialog method failed, trying FolderBrowserDialog");
        }

        // 方法2：尝试传统的FolderBrowserDialog
        try
        {
            using var folderDialog = new FolderBrowserDialog();
            folderDialog.Description = description;
            folderDialog.UseDescriptionForTitle = true;
            folderDialog.ShowNewFolderButton = true;
            
            if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath))
            {
                folderDialog.SelectedPath = initialPath;
            }

            var result = folderDialog.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                _logger.LogInformation("Folder selected via FolderBrowserDialog: {Path}", folderDialog.SelectedPath);
                return folderDialog.SelectedPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FolderBrowserDialog also failed");
        }

        // 方法3：如果所有对话框都失败，提供手动输入选项
        var manualInput = MessageBox.Show(
            "Unable to open folder selection dialog. Would you like to enter the path manually?\n\n" +
            "Common MySQL data directory locations:\n" +
            "• C:\\ProgramData\\MySQL\\MySQL Server 8.0\\Data\n" +
            "• C:\\ProgramData\\MySQL\\MySQL Server 5.7\\Data\n" +
            "• C:\\Program Files\\MySQL\\MySQL Server 8.0\\data",
            "Folder Selection", 
            MessageBoxButtons.YesNo, 
            MessageBoxIcon.Question);

        if (manualInput == DialogResult.Yes)
        {
            // 简单的输入对话框实现
            var inputForm = new Form()
            {
                Text = "Enter Directory Path",
                Size = new Size(500, 150),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var textBox = new TextBox()
            {
                Text = initialPath ?? @"C:\ProgramData\MySQL\MySQL Server 8.0\Data",
                Location = new Point(10, 20),
                Size = new Size(460, 25)
            };

            var okButton = new Button()
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(310, 60),
                Size = new Size(75, 25)
            };

            var cancelButton = new Button()
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(395, 60),
                Size = new Size(75, 25)
            };

            inputForm.Controls.AddRange(new Control[] { textBox, okButton, cancelButton });
            inputForm.AcceptButton = okButton;
            inputForm.CancelButton = cancelButton;

            if (inputForm.ShowDialog(this) == DialogResult.OK)
            {
                var enteredPath = textBox.Text.Trim();
                if (!string.IsNullOrEmpty(enteredPath))
                {
                    if (Directory.Exists(enteredPath))
                    {
                        _logger.LogInformation("Manual path entered and validated: {Path}", enteredPath);
                        return enteredPath;
                    }
                    else
                    {
                        MessageBox.Show($"The directory '{enteredPath}' does not exist.", "Invalid Path", 
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }

        return null;
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

            // Debug: Log the configuration values before validation
            _logger.LogInformation("Validating configuration: Name='{Name}', ServiceName='{ServiceName}', DataDirectory='{DataDirectory}'", 
                _currentConfiguration.Name, _currentConfiguration.ServiceName, _currentConfiguration.DataDirectoryPath);

            // Validate configuration
            var validationContext = new ValidationContext(_currentConfiguration);
            var validationResults = new List<ValidationResult>();
            
            if (!Validator.TryValidateObject(_currentConfiguration, validationContext, validationResults, true))
            {
                var errorMessage = string.Join("\n", validationResults.Select(r => r.ErrorMessage));
                _logger.LogWarning("Configuration validation failed: {Errors}", errorMessage);
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
                _logger.LogWarning("Repository validation failed: {Errors}", errorMessage);
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