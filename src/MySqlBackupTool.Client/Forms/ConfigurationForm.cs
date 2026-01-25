using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Client.Forms;

/// <summary>
/// 备份配置管理窗体
/// 用于创建、编辑和管理MySQL数据库备份配置
/// </summary>
/// <remarks>
/// 该窗体提供以下功能：
/// 1. 创建新的备份配置
/// 2. 编辑现有的备份配置
/// 3. 测试MySQL数据库连接
/// 4. 测试目标服务器连接
/// 5. 配置文件命名策略
/// 6. 验证和保存配置信息
/// 7. 浏览和选择目录路径
/// 
/// 支持的配置项包括：
/// - MySQL连接信息（主机、端口、用户名、密码等）
/// - 目标服务器信息（IP地址、端口、SSL设置等）
/// - 文件命名策略（模式、日期格式等）
/// - 备份目录和数据目录路径
/// </remarks>
public partial class ConfigurationForm : Form
{
    #region 私有字段

    /// <summary>
    /// 服务提供者，用于获取依赖注入的服务实例
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 日志记录器，用于记录窗体操作和错误信息
    /// </summary>
    private readonly ILogger<ConfigurationForm> _logger;

    /// <summary>
    /// 备份配置仓储接口，用于数据库操作
    /// </summary>
    private readonly IBackupConfigurationRepository _configRepository;

    /// <summary>
    /// 当前正在编辑的备份配置对象
    /// </summary>
    private BackupConfiguration _currentConfiguration;

    /// <summary>
    /// 标识当前是否为编辑模式（true）还是新建模式（false）
    /// </summary>
    private bool _isEditing;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化配置窗体的新实例
    /// </summary>
    /// <param name="serviceProvider">服务提供者，用于依赖注入</param>
    /// <param name="configuration">要编辑的配置对象，如果为null则创建新配置</param>
    /// <remarks>
    /// 构造函数执行以下操作：
    /// 1. 初始化依赖注入的服务
    /// 2. 设置当前配置对象和编辑模式
    /// 3. 初始化窗体组件
    /// 4. 加载配置数据到界面控件
    /// </remarks>
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

    #endregion

    #region 窗体初始化

    /// <summary>
    /// 初始化窗体设置和加载数据
    /// </summary>
    /// <remarks>
    /// 该方法执行以下操作：
    /// 1. 根据编辑模式设置窗体标题
    /// 2. 加载配置数据到界面控件
    /// 3. 记录初始化日志
    /// 4. 处理初始化过程中的异常
    /// </remarks>
    private void InitializeForm()
    {
        try
        {
            // 根据是否为编辑模式设置窗体标题
            this.Text = _isEditing ? "Edit Backup Configuration" : "New Backup Configuration";
            
            // 注释：移除固定大小和边框样式设置，允许用户调整窗体大小
            // 这些设置已在Designer文件中配置为可调整大小
            // this.Size = new Size(600, 700);
            // this.StartPosition = FormStartPosition.CenterParent;
            // this.FormBorderStyle = FormBorderStyle.FixedDialog;
            // this.MaximizeBox = false;
            // this.MinimizeBox = false;

            // 加载配置数据到界面控件
            LoadConfigurationData();
            
            _logger.LogInformation("Configuration form initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing configuration form");
            MessageBox.Show($"Error initializing form: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    #endregion

    #region 数据加载和保存

    /// <summary>
    /// 将配置对象的数据加载到窗体控件中
    /// </summary>
    /// <remarks>
    /// 该方法执行以下操作：
    /// 1. 加载基本配置信息（名称、MySQL连接信息等）
    /// 2. 处理双重属性设置（BackupConfiguration和MySQLConnection级别）
    /// 3. 加载目标服务器配置
    /// 4. 加载文件命名策略配置
    /// 5. 设置激活状态
    /// 6. 处理加载过程中的异常
    /// </remarks>
    private void LoadConfigurationData()
    {
        try
        {
            // 加载基本配置信息
            txtConfigName.Text = _currentConfiguration.Name;
            txtMySqlUsername.Text = _currentConfiguration.MySQLConnection.Username;
            txtMySqlPassword.Text = _currentConfiguration.MySQLConnection.Password;
            
            // 优先使用BackupConfiguration级别的属性，如果为空则使用MySQLConnection级别的属性
            // 这种设计是为了兼容不同版本的配置结构
            txtServiceName.Text = !string.IsNullOrEmpty(_currentConfiguration.ServiceName) 
                ? _currentConfiguration.ServiceName 
                : _currentConfiguration.MySQLConnection.ServiceName;
            txtDataDirectory.Text = !string.IsNullOrEmpty(_currentConfiguration.DataDirectoryPath) 
                ? _currentConfiguration.DataDirectoryPath 
                : _currentConfiguration.MySQLConnection.DataDirectoryPath;
                
            // 加载MySQL连接信息
            txtMySqlHost.Text = _currentConfiguration.MySQLConnection.Host;
            numMySqlPort.Value = _currentConfiguration.MySQLConnection.Port;

            // 加载目标服务器配置
            txtServerIP.Text = _currentConfiguration.TargetServer.IPAddress;
            numServerPort.Value = _currentConfiguration.TargetServer.Port;
            chkUseSSL.Checked = _currentConfiguration.TargetServer.UseSSL;
            txtTargetDirectory.Text = _currentConfiguration.TargetDirectory;

            // 加载文件命名策略配置
            txtNamingPattern.Text = _currentConfiguration.NamingStrategy.Pattern;
            txtDateFormat.Text = _currentConfiguration.NamingStrategy.DateFormat;
            chkIncludeServerName.Checked = _currentConfiguration.NamingStrategy.IncludeServerName;
            chkIncludeDatabaseName.Checked = _currentConfiguration.NamingStrategy.IncludeDatabaseName;

            // 设置配置激活状态
            chkIsActive.Checked = _currentConfiguration.IsActive;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading configuration data");
            MessageBox.Show($"Error loading configuration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 将窗体控件中的数据保存到配置对象
    /// </summary>
    /// <remarks>
    /// 该方法执行以下操作：
    /// 1. 从界面控件获取用户输入的数据
    /// 2. 设置MySQL连接属性
    /// 3. 同时设置BackupConfiguration级别的属性（用于验证）
    /// 4. 设置目标服务器属性
    /// 5. 设置文件命名策略属性（包含默认值）
    /// 6. 验证必填字段
    /// 7. 处理保存过程中的异常
    /// </remarks>
    /// <exception cref="InvalidOperationException">当必填字段为空时抛出</exception>
    private void SaveConfigurationData()
    {
        try
        {
            // 保存基本配置信息
            _currentConfiguration.Name = txtConfigName.Text.Trim();
            
            // 设置MySQL连接属性
            _currentConfiguration.MySQLConnection.Username = txtMySqlUsername.Text.Trim();
            _currentConfiguration.MySQLConnection.Password = txtMySqlPassword.Text;
            _currentConfiguration.MySQLConnection.ServiceName = txtServiceName.Text.Trim();
            _currentConfiguration.MySQLConnection.DataDirectoryPath = txtDataDirectory.Text.Trim();
            _currentConfiguration.MySQLConnection.Host = txtMySqlHost.Text.Trim();
            _currentConfiguration.MySQLConnection.Port = (int)numMySqlPort.Value;

            // 同时设置BackupConfiguration级别的属性（这些属性用于验证）
            _currentConfiguration.ServiceName = txtServiceName.Text.Trim();
            _currentConfiguration.DataDirectoryPath = txtDataDirectory.Text.Trim();

            // 设置目标服务器属性
            _currentConfiguration.TargetServer.IPAddress = txtServerIP.Text.Trim();
            _currentConfiguration.TargetServer.Port = (int)numServerPort.Value;
            _currentConfiguration.TargetServer.UseSSL = chkUseSSL.Checked;
            _currentConfiguration.TargetDirectory = txtTargetDirectory.Text.Trim();

            // 设置文件命名策略属性，如果为空则使用默认值
            _currentConfiguration.NamingStrategy.Pattern = !string.IsNullOrWhiteSpace(txtNamingPattern.Text.Trim()) 
                ? txtNamingPattern.Text.Trim() 
                : "{timestamp}_{database}_{server}.zip";
            _currentConfiguration.NamingStrategy.DateFormat = !string.IsNullOrWhiteSpace(txtDateFormat.Text.Trim()) 
                ? txtDateFormat.Text.Trim() 
                : "yyyyMMdd_HHmmss";
            _currentConfiguration.NamingStrategy.IncludeServerName = chkIncludeServerName.Checked;
            _currentConfiguration.NamingStrategy.IncludeDatabaseName = chkIncludeDatabaseName.Checked;

            // 设置激活状态
            _currentConfiguration.IsActive = chkIsActive.Checked;

            // 验证必填字段
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

    #endregion

    #region 连接测试事件处理

    /// <summary>
    /// MySQL连接测试按钮点击事件处理程序
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    /// <remarks>
    /// 该方法执行以下操作：
    /// 1. 禁用测试按钮并更新状态显示
    /// 2. 创建临时的MySQL连接信息对象
    /// 3. 执行异步连接验证
    /// 4. 根据测试结果更新状态标签
    /// 5. 记录测试结果日志
    /// 6. 恢复按钮状态
    /// </remarks>
    private async void btnTestMySqlConnection_Click(object sender, EventArgs e)
    {
        try
        {
            // 禁用按钮并更新状态，防止重复点击
            btnTestMySqlConnection.Enabled = false;
            btnTestMySqlConnection.Text = "Testing...";
            lblMySqlConnectionStatus.Text = "Testing connection...";
            lblMySqlConnectionStatus.ForeColor = Color.Blue;

            // 创建临时的MySQL连接信息对象用于测试
            var connectionInfo = new MySQLConnectionInfo
            {
                Username = txtMySqlUsername.Text.Trim(),
                Password = txtMySqlPassword.Text,
                ServiceName = txtServiceName.Text.Trim(),
                DataDirectoryPath = txtDataDirectory.Text.Trim(),
                Host = txtMySqlHost.Text.Trim(),
                Port = (int)numMySqlPort.Value
            };

            // 执行异步连接验证
            var (isValid, errors) = await connectionInfo.ValidateConnectionAsync();

            // 根据验证结果更新界面状态
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
            // 处理测试过程中的异常
            lblMySqlConnectionStatus.Text = $"Test error: {ex.Message}";
            lblMySqlConnectionStatus.ForeColor = Color.Red;
            _logger.LogError(ex, "Error testing MySQL connection");
        }
        finally
        {
            // 恢复按钮状态
            btnTestMySqlConnection.Enabled = true;
            btnTestMySqlConnection.Text = "Test Connection";
        }
    }

    /// <summary>
    /// 服务器连接测试按钮点击事件处理程序
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    /// <remarks>
    /// 该方法执行以下操作：
    /// 1. 禁用测试按钮并更新状态显示
    /// 2. 创建临时的服务器端点对象
    /// 3. 执行端点验证
    /// 4. 测试服务器可达性
    /// 5. 测试端口可访问性
    /// 6. 根据测试结果更新状态标签
    /// 7. 记录测试结果日志
    /// 8. 恢复按钮状态
    /// </remarks>
    private async void btnTestServerConnection_Click(object sender, EventArgs e)
    {
        try
        {
            // 禁用按钮并更新状态，防止重复点击
            btnTestServerConnection.Enabled = false;
            btnTestServerConnection.Text = "Testing...";
            lblServerConnectionStatus.Text = "Testing connection...";
            lblServerConnectionStatus.ForeColor = Color.Blue;

            // 创建临时的服务器端点对象用于测试
            var serverEndpoint = new ServerEndpoint
            {
                IPAddress = txtServerIP.Text.Trim(),
                Port = (int)numServerPort.Value,
                UseSSL = chkUseSSL.Checked
            };

            // 执行端点验证
            var (isValid, errors) = serverEndpoint.ValidateEndpoint();

            if (!isValid)
            {
                lblServerConnectionStatus.Text = $"Validation failed: {string.Join(", ", errors)}";
                lblServerConnectionStatus.ForeColor = Color.Red;
                return;
            }

            // 测试服务器可达性
            var isReachable = await serverEndpoint.TestConnectivityAsync();
            if (!isReachable)
            {
                lblServerConnectionStatus.Text = "Server is not reachable";
                lblServerConnectionStatus.ForeColor = Color.Orange;
                return;
            }

            // 测试端口可访问性
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
            // 处理测试过程中的异常
            lblServerConnectionStatus.Text = $"Test error: {ex.Message}";
            lblServerConnectionStatus.ForeColor = Color.Red;
            _logger.LogError(ex, "Error testing server connection");
        }
        finally
        {
            // 恢复按钮状态
            btnTestServerConnection.Enabled = true;
            btnTestServerConnection.Text = "Test Connection";
        }
    }

    #endregion

    #region 目录浏览功能

    /// <summary>
    /// 数据目录浏览按钮点击事件处理程序
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    /// <remarks>
    /// 该方法执行以下操作：
    /// 1. 禁用按钮防止重复点击
    /// 2. 调用安全的文件夹选择对话框
    /// 3. 更新数据目录文本框
    /// 4. 记录选择结果日志
    /// 5. 处理浏览过程中的异常
    /// 6. 恢复按钮状态
    /// </remarks>
    private void btnBrowseDataDirectory_Click(object sender, EventArgs e)
    {
        try
        {
            // 禁用按钮防止重复点击
            btnBrowseDataDirectory.Enabled = false;
            btnBrowseDataDirectory.Text = "Browsing...";
            
            // 调用安全的文件夹选择对话框
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
    /// <param name="description">对话框描述文本</param>
    /// <param name="initialPath">初始路径，可选</param>
    /// <returns>用户选择的文件夹路径，如果取消则返回null</returns>
    /// <remarks>
    /// 该方法实现了三层备选方案：
    /// 1. 方法1：使用现代的SaveFileDialog方式（推荐）
    /// 2. 方法2：使用传统的FolderBrowserDialog（备选）
    /// 3. 方法3：手动输入对话框（最后备选）
    /// 
    /// 这种设计是为了解决某些系统环境下文件夹对话框可能卡死的问题
    /// </remarks>
    private string? ShowFolderDialogSafe(string description, string? initialPath = null)
    {
        #region 方法1：使用SaveFileDialog方式

        // 尝试使用现代的SaveFileDialog方式
        // 这种方法在大多数系统上更稳定，不容易卡死
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

        #endregion

        #region 方法2：使用FolderBrowserDialog

        // 尝试传统的FolderBrowserDialog
        // 如果SaveFileDialog失败，则使用这个备选方案
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

        #endregion

        #region 方法3：手动输入对话框

        // 如果所有对话框都失败，提供手动输入选项
        // 这是最后的备选方案，确保用户始终能够输入路径
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
            // 创建简单的输入对话框
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

        #endregion

        return null;
    }

    #endregion

    #region 文件命名预览功能

    /// <summary>
    /// 文件名预览按钮点击事件处理程序
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    /// <remarks>
    /// 该方法执行以下操作：
    /// 1. 创建文件命名策略对象
    /// 2. 验证命名策略的有效性
    /// 3. 生成示例文件名
    /// 4. 在预览标签中显示结果
    /// 5. 处理预览过程中的异常
    /// </remarks>
    private void btnPreviewFileName_Click(object sender, EventArgs e)
    {
        try
        {
            // 创建文件命名策略对象
            var namingStrategy = new FileNamingStrategy
            {
                Pattern = txtNamingPattern.Text.Trim(),
                DateFormat = txtDateFormat.Text.Trim(),
                IncludeServerName = chkIncludeServerName.Checked,
                IncludeDatabaseName = chkIncludeDatabaseName.Checked
            };

            // 验证命名策略的有效性
            var (isValid, errors) = namingStrategy.ValidateStrategy();
            if (!isValid)
            {
                MessageBox.Show($"Naming strategy validation failed:\n{string.Join("\n", errors)}", 
                    "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 生成示例文件名并显示预览
            var sampleFileName = namingStrategy.GenerateFileName("SampleServer", "SampleDB", DateTime.Now);
            lblFileNamePreview.Text = $"Preview: {sampleFileName}";
            lblFileNamePreview.ForeColor = Color.Green;
        }
        catch (Exception ex)
        {
            // 处理预览过程中的异常
            lblFileNamePreview.Text = $"Preview error: {ex.Message}";
            lblFileNamePreview.ForeColor = Color.Red;
            _logger.LogError(ex, "Error generating filename preview");
        }
    }

    #endregion

    #region 保存和验证功能

    /// <summary>
    /// 保存按钮点击事件处理程序
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    /// <remarks>
    /// 该方法执行以下操作：
    /// 1. 禁用保存按钮防止重复点击
    /// 2. 保存窗体数据到配置对象
    /// 3. 记录调试信息
    /// 4. 执行配置验证
    /// 5. 保存到数据库
    /// 6. 根据结果显示消息并关闭窗体
    /// 7. 处理保存过程中的异常
    /// 8. 恢复按钮状态
    /// </remarks>
    private async void btnSave_Click(object sender, EventArgs e)
    {
        try
        {
            // 禁用保存按钮防止重复点击
            btnSave.Enabled = false;
            
            // 保存窗体数据到配置对象
            SaveConfigurationData();

            // 记录调试信息，用于排查验证问题
            _logger.LogInformation("Validating configuration: Name='{Name}', ServiceName='{ServiceName}', DataDirectory='{DataDirectory}'", 
                _currentConfiguration.Name, _currentConfiguration.ServiceName, _currentConfiguration.DataDirectoryPath);

            // 执行配置验证
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

            // 保存到数据库（不进行连接测试）
            var (success, errors, savedConfig) = await _configRepository.ValidateAndSaveAsync(_currentConfiguration, false);
            
            if (success && savedConfig != null)
            {
                // 保存成功，更新当前配置并关闭窗体
                _currentConfiguration = savedConfig;
                _isEditing = true;
                
                MessageBox.Show("Configuration saved successfully!", "Success", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                // 保存失败，显示错误信息
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
            // 恢复按钮状态
            btnSave.Enabled = true;
        }
    }

    /// <summary>
    /// 验证并保存按钮点击事件处理程序
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    /// <remarks>
    /// 该方法执行以下操作：
    /// 1. 禁用按钮并更新状态文本
    /// 2. 保存窗体数据到配置对象
    /// 3. 执行完整的配置验证（包括连接测试）
    /// 4. 保存到数据库
    /// 5. 根据结果显示消息并关闭窗体
    /// 6. 处理验证和保存过程中的异常
    /// 7. 恢复按钮状态
    /// </remarks>
    private async void btnValidateAndSave_Click(object sender, EventArgs e)
    {
        try
        {
            // 禁用按钮并更新状态文本
            btnValidateAndSave.Enabled = false;
            btnValidateAndSave.Text = "Validating...";
            
            // 保存窗体数据到配置对象
            SaveConfigurationData();

            // 执行完整的配置验证（包括连接测试）
            var (success, errors, savedConfig) = await _configRepository.ValidateAndSaveAsync(_currentConfiguration, true);
            
            if (success && savedConfig != null)
            {
                // 验证和保存成功
                _currentConfiguration = savedConfig;
                _isEditing = true;
                
                MessageBox.Show("Configuration validated and saved successfully!", "Success", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                // 验证失败，显示错误信息
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
            // 恢复按钮状态
            btnValidateAndSave.Enabled = true;
            btnValidateAndSave.Text = "Validate && Save";
        }
    }

    /// <summary>
    /// 取消按钮点击事件处理程序
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    /// <remarks>
    /// 设置对话框结果为取消并关闭窗体
    /// </remarks>
    private void btnCancel_Click(object sender, EventArgs e)
    {
        this.DialogResult = DialogResult.Cancel;
        this.Close();
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 获取当前配置对象
    /// </summary>
    /// <returns>如果对话框结果为OK则返回配置对象，否则返回null</returns>
    /// <remarks>
    /// 该方法用于在窗体关闭后获取用户编辑的配置信息
    /// 只有在用户点击保存按钮成功保存后才会返回配置对象
    /// </remarks>
    public BackupConfiguration? GetConfiguration()
    {
        return this.DialogResult == DialogResult.OK ? _currentConfiguration : null;
    }

    #endregion
}