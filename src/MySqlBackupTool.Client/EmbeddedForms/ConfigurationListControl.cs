using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Client.Forms;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Client.EmbeddedForms;

/// <summary>
/// 备份配置管理控件
/// 提供备份配置的列表显示、创建、编辑、删除、激活和停用功能
/// </summary>
public partial class ConfigurationListControl : UserControl, IEmbeddedForm
{
    #region 私有字段

    /// <summary>
    /// 依赖注入服务提供者，用于获取各种服务实例
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 日志记录器，用于记录配置列表控件的操作和错误信息
    /// </summary>
    private readonly ILogger<ConfigurationListControl> _logger;

    /// <summary>
    /// 备份配置仓储接口，用于配置的CRUD操作
    /// </summary>
    private readonly IBackupConfigurationRepository _configRepository;

    /// <summary>
    /// 备份配置列表，存储从数据库加载的所有配置
    /// </summary>
    private List<BackupConfiguration> _configurations = new();

    #endregion

    #region IEmbeddedForm 实现

    /// <summary>
    /// 获取嵌入式窗体的显示标题
    /// </summary>
    public string Title => "备份配置管理";

    /// <summary>
    /// 获取导航路径用于面包屑显示
    /// </summary>
    public string NavigationPath => "工具 > 配置管理";

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
            LoadConfigurations();
            _logger.LogInformation("配置列表控件已激活");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "激活配置列表控件时发生错误");
        }
    }

    /// <summary>
    /// 当窗体被停用（隐藏）时调用
    /// </summary>
    public void OnDeactivated()
    {
        try
        {
            _logger.LogInformation("配置列表控件已停用");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停用配置列表控件时发生错误");
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
    /// 初始化ConfigurationListControl类的新实例
    /// </summary>
    /// <param name="serviceProvider">依赖注入服务提供者</param>
    public ConfigurationListControl(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<ConfigurationListControl>>();
        _configRepository = serviceProvider.GetRequiredService<IBackupConfigurationRepository>();

        InitializeComponent();
        InitializeControl();
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 初始化控件的基本设置和属性
    /// 配置数据网格并加载配置数据
    /// </summary>
    private void InitializeControl()
    {
        try
        {
            SetupDataGridView();
            
            _logger.LogInformation("配置列表控件初始化成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化配置列表控件时发生错误");
            MessageBox.Show($"初始化控件时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 设置数据网格视图的列和属性
    /// 配置配置列表网格的显示格式和事件处理
    /// </summary>
    private void SetupDataGridView()
    {
        dgvConfigurations.AutoGenerateColumns = false;
        dgvConfigurations.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvConfigurations.MultiSelect = false;
        dgvConfigurations.ReadOnly = true;
        dgvConfigurations.AllowUserToAddRows = false;
        dgvConfigurations.AllowUserToDeleteRows = false;

        // 添加列
        dgvConfigurations.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Name",
            HeaderText = "配置名称",
            DataPropertyName = "Name",
            Width = 200
        });

        dgvConfigurations.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "MySQLHost",
            HeaderText = "MySQL主机",
            DataPropertyName = "MySQLConnection.Host",
            Width = 120
        });

        dgvConfigurations.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "TargetServer",
            HeaderText = "目标服务器",
            DataPropertyName = "TargetServer.IPAddress",
            Width = 120
        });

        dgvConfigurations.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "IsActive",
            HeaderText = "激活",
            DataPropertyName = "IsActive",
            Width = 60
        });

        dgvConfigurations.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "CreatedAt",
            HeaderText = "创建时间",
            DataPropertyName = "CreatedAt",
            Width = 120,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm" }
        });

        // 处理嵌套属性的单元格格式化
        dgvConfigurations.CellFormatting += DgvConfigurations_CellFormatting;
        dgvConfigurations.SelectionChanged += DgvConfigurations_SelectionChanged;
    }

    /// <summary>
    /// 异步加载所有备份配置
    /// 从数据库获取配置列表并更新界面显示
    /// </summary>
    private async void LoadConfigurations()
    {
        try
        {
            btnRefresh.Enabled = false;
            btnRefresh.Text = "加载中...";

            _configurations = (await _configRepository.GetAllAsync()).ToList();
            dgvConfigurations.DataSource = _configurations;

            var statusMessage = $"已加载 {_configurations.Count} 个配置";
            lblStatus.Text = statusMessage;
            lblStatus.ForeColor = Color.Green;
            
            // 触发状态改变事件
            StatusChanged?.Invoke(this, statusMessage);

            _logger.LogInformation("已加载 {Count} 个配置", _configurations.Count);
        }
        catch (Exception ex)
        {
            var errorMessage = $"加载配置时发生错误: {ex.Message}";
            lblStatus.Text = errorMessage;
            lblStatus.ForeColor = Color.Red;
            
            // 触发状态改变事件
            StatusChanged?.Invoke(this, errorMessage);
            
            _logger.LogError(ex, "加载配置时发生错误");
        }
        finally
        {
            btnRefresh.Enabled = true;
            btnRefresh.Text = "刷新";
        }
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 获取当前选中的备份配置
    /// </summary>
    /// <returns>选中的备份配置，如果没有选中则返回null</returns>
    public BackupConfiguration? GetSelectedConfiguration()
    {
        if (dgvConfigurations.SelectedRows.Count > 0)
        {
            return dgvConfigurations.SelectedRows[0].DataBoundItem as BackupConfiguration;
        }
        return null;
    }

    #endregion

    #region 事件处理程序

    /// <summary>
    /// 配置网格单元格格式化事件处理程序
    /// 自定义显示嵌套属性的值（如MySQL主机和目标服务器）
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">单元格格式化事件参数</param>
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

    /// <summary>
    /// 配置网格选择变化事件处理程序
    /// 根据选择状态和配置的激活状态更新按钮的可用性
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
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

    /// <summary>
    /// 新建按钮点击事件处理程序
    /// 打开配置窗体创建新的备份配置
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
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
            _logger.LogError(ex, "创建新配置时发生错误");
            MessageBox.Show($"创建配置时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 编辑按钮点击事件处理程序
    /// 打开配置窗体编辑选中的备份配置
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
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
            _logger.LogError(ex, "编辑配置时发生错误");
            MessageBox.Show($"编辑配置时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 删除按钮点击事件处理程序
    /// 删除选中的备份配置（需要用户确认）
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
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
                $"确定要删除配置 '{selectedConfig.Name}' 吗?\n\n此操作无法撤销。",
                "确认删除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                await _configRepository.DeleteAsync(selectedConfig.Id);
                LoadConfigurations();
                
                var statusMessage = $"配置 '{selectedConfig.Name}' 删除成功";
                lblStatus.Text = statusMessage;
                lblStatus.ForeColor = Color.Green;
                
                // 触发状态改变事件
                StatusChanged?.Invoke(this, statusMessage);
                
                _logger.LogInformation("已删除配置: {Name}", selectedConfig.Name);
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"删除配置时发生错误: {ex.Message}";
            lblStatus.Text = errorMessage;
            lblStatus.ForeColor = Color.Red;
            
            // 触发状态改变事件
            StatusChanged?.Invoke(this, errorMessage);
            
            _logger.LogError(ex, "删除配置时发生错误");
        }
    }

    /// <summary>
    /// 激活按钮点击事件处理程序
    /// 激活选中的备份配置
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
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
                var statusMessage = $"配置 '{selectedConfig.Name}' 已激活";
                lblStatus.Text = statusMessage;
                lblStatus.ForeColor = Color.Green;
                
                // 触发状态改变事件
                StatusChanged?.Invoke(this, statusMessage);
                
                _logger.LogInformation("已激活配置: {Name}", selectedConfig.Name);
            }
            else
            {
                var errorMessage = "激活配置失败";
                lblStatus.Text = errorMessage;
                lblStatus.ForeColor = Color.Red;
                
                // 触发状态改变事件
                StatusChanged?.Invoke(this, errorMessage);
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"激活配置时发生错误: {ex.Message}";
            lblStatus.Text = errorMessage;
            lblStatus.ForeColor = Color.Red;
            
            // 触发状态改变事件
            StatusChanged?.Invoke(this, errorMessage);
            
            _logger.LogError(ex, "激活配置时发生错误");
        }
    }

    /// <summary>
    /// 停用按钮点击事件处理程序
    /// 停用选中的备份配置
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
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
                var statusMessage = $"配置 '{selectedConfig.Name}' 已停用";
                lblStatus.Text = statusMessage;
                lblStatus.ForeColor = Color.Green;
                
                // 触发状态改变事件
                StatusChanged?.Invoke(this, statusMessage);
                
                _logger.LogInformation("已停用配置: {Name}", selectedConfig.Name);
            }
            else
            {
                var errorMessage = "停用配置失败";
                lblStatus.Text = errorMessage;
                lblStatus.ForeColor = Color.Red;
                
                // 触发状态改变事件
                StatusChanged?.Invoke(this, errorMessage);
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"停用配置时发生错误: {ex.Message}";
            lblStatus.Text = errorMessage;
            lblStatus.ForeColor = Color.Red;
            
            // 触发状态改变事件
            StatusChanged?.Invoke(this, errorMessage);
            
            _logger.LogError(ex, "停用配置时发生错误");
        }
    }

    /// <summary>
    /// 刷新按钮点击事件处理程序
    /// 重新加载配置列表
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void btnRefresh_Click(object sender, EventArgs e)
    {
        LoadConfigurations();
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
