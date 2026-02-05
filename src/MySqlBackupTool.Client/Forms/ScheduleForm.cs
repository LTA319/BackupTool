using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;
using System.ComponentModel.DataAnnotations;

namespace MySqlBackupTool.Client.Forms;

/// <summary>
/// 调度配置编辑窗体
/// 用于创建和编辑备份调度配置
/// </summary>
public partial class ScheduleForm : Form
{
    #region 私有字段

    /// <summary>
    /// 服务提供者，用于获取依赖注入的服务实例
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 日志记录器，用于记录窗体操作和错误信息
    /// </summary>
    private readonly ILogger<ScheduleForm> _logger;

    /// <summary>
    /// 调度配置仓储接口，用于数据库操作
    /// </summary>
    private readonly IScheduleConfigurationRepository _scheduleRepository;

    /// <summary>
    /// 备份配置仓储接口，用于获取备份配置列表
    /// </summary>
    private readonly IBackupConfigurationRepository _configRepository;

    /// <summary>
    /// 当前正在编辑的调度配置对象
    /// </summary>
    private ScheduleConfiguration _currentSchedule;

    /// <summary>
    /// 标识当前是否为编辑模式（true）还是新建模式（false）
    /// </summary>
    private bool _isEditing;

    /// <summary>
    /// 可用的备份配置列表
    /// </summary>
    private List<BackupConfiguration> _backupConfigurations = new();

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化调度配置窗体的新实例
    /// </summary>
    /// <param name="serviceProvider">服务提供者，用于依赖注入</param>
    /// <param name="schedule">要编辑的调度配置对象，如果为null则创建新配置</param>
    public ScheduleForm(IServiceProvider serviceProvider, ScheduleConfiguration? schedule = null)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<ScheduleForm>>();
        _scheduleRepository = serviceProvider.GetRequiredService<IScheduleConfigurationRepository>();
        _configRepository = serviceProvider.GetRequiredService<IBackupConfigurationRepository>();
        _currentSchedule = schedule ?? new ScheduleConfiguration();
        _isEditing = schedule != null;

        InitializeComponent();
        InitializeForm();
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 初始化窗体的基本设置和属性
    /// </summary>
    private async void InitializeForm()
    {
        try
        {
            this.Text = _isEditing ? "编辑调度配置" : "新建调度配置";
            this.Size = new Size(500, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            await LoadBackupConfigurations();
            SetupControls();
            LoadScheduleData();

            _logger.LogInformation("调度配置窗体初始化成功，模式: {Mode}", _isEditing ? "编辑" : "新建");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化调度配置窗体时发生错误");
            MessageBox.Show($"初始化窗体时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 加载可用的备份配置列表
    /// </summary>
    private async Task LoadBackupConfigurations()
    {
        try
        {
            _backupConfigurations = (await _configRepository.GetAllAsync()).ToList();
            
            cmbBackupConfig.DataSource = _backupConfigurations;
            cmbBackupConfig.DisplayMember = "Name";
            cmbBackupConfig.ValueMember = "Id";
            
            if (!_backupConfigurations.Any())
            {
                MessageBox.Show("没有可用的备份配置，请先创建备份配置。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载备份配置列表时发生错误");
            MessageBox.Show($"加载备份配置时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 设置控件的初始状态
    /// </summary>
    private void SetupControls()
    {
        // 设置调度类型下拉框
        cmbScheduleType.Items.Clear();
        cmbScheduleType.Items.Add(new { Text = "每日", Value = ScheduleType.Daily });
        cmbScheduleType.Items.Add(new { Text = "每周", Value = ScheduleType.Weekly });
        cmbScheduleType.Items.Add(new { Text = "每月", Value = ScheduleType.Monthly });
        cmbScheduleType.DisplayMember = "Text";
        cmbScheduleType.ValueMember = "Value";

        // 绑定事件
        cmbScheduleType.SelectedIndexChanged += CmbScheduleType_SelectedIndexChanged;
        
        // 设置默认值
        if (cmbScheduleType.Items.Count > 0)
            cmbScheduleType.SelectedIndex = 0;
    }

    /// <summary>
    /// 加载调度配置数据到界面控件
    /// </summary>
    private void LoadScheduleData()
    {
        if (_isEditing)
        {
            // 选择对应的备份配置
            cmbBackupConfig.SelectedValue = _currentSchedule.BackupConfigId;
            
            // 选择调度类型
            for (int i = 0; i < cmbScheduleType.Items.Count; i++)
            {
                var item = (dynamic)cmbScheduleType.Items[i];
                if (item.Value.Equals(_currentSchedule.ScheduleType))
                {
                    cmbScheduleType.SelectedIndex = i;
                    break;
                }
            }
            
            // 设置调度时间
            txtScheduleTime.Text = _currentSchedule.ScheduleTime;
            
            // 设置启用状态
            chkEnabled.Checked = _currentSchedule.IsEnabled;
        }
        else
        {
            // 新建模式的默认值
            chkEnabled.Checked = true;
        }
        
        UpdateScheduleTimeHint();
    }

    /// <summary>
    /// 根据调度类型更新时间格式提示
    /// </summary>
    private void UpdateScheduleTimeHint()
    {
        if (cmbScheduleType.SelectedItem == null) return;

        var selectedType = ((dynamic)cmbScheduleType.SelectedItem).Value;
        
        string hint = selectedType switch
        {
            ScheduleType.Daily => "格式: HH:mm (例如: 14:30)",
            ScheduleType.Weekly => "格式: DayOfWeek HH:mm (例如: Monday 14:30)",
            ScheduleType.Monthly => "格式: DD HH:mm (例如: 15 14:30)",
            _ => ""
        };
        
        lblTimeHint.Text = hint;
    }

    /// <summary>
    /// 验证输入数据
    /// </summary>
    /// <returns>验证是否通过</returns>
    private bool ValidateInput()
    {
        var errors = new List<string>();

        // 验证备份配置选择
        if (cmbBackupConfig.SelectedValue == null)
        {
            errors.Add("请选择备份配置");
        }

        // 验证调度类型选择
        if (cmbScheduleType.SelectedItem == null)
        {
            errors.Add("请选择调度类型");
        }

        // 验证调度时间
        if (string.IsNullOrWhiteSpace(txtScheduleTime.Text))
        {
            errors.Add("请输入调度时间");
        }
        else if (cmbScheduleType.SelectedItem != null)
        {
            var scheduleType = ((dynamic)cmbScheduleType.SelectedItem).Value;
            var tempSchedule = new ScheduleConfiguration
            {
                ScheduleType = scheduleType,
                ScheduleTime = txtScheduleTime.Text.Trim()
            };

            var validationResults = tempSchedule.Validate(new ValidationContext(tempSchedule));
            foreach (var result in validationResults)
            {
                errors.Add(result.ErrorMessage ?? "验证错误");
            }
        }

        if (errors.Any())
        {
            MessageBox.Show($"输入验证失败:\n\n{string.Join("\n", errors)}", "验证错误", 
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 保存调度配置
    /// </summary>
    private async Task<bool> SaveSchedule()
    {
        try
        {
            if (!ValidateInput())
                return false;

            // 更新配置对象
            _currentSchedule.BackupConfigId = (int)cmbBackupConfig.SelectedValue;
            _currentSchedule.ScheduleType = ((dynamic)cmbScheduleType.SelectedItem).Value;
            _currentSchedule.ScheduleTime = txtScheduleTime.Text.Trim();
            _currentSchedule.IsEnabled = chkEnabled.Checked;

            // 计算下次执行时间
            _currentSchedule.NextExecution = _currentSchedule.CalculateNextExecution();

            if (_isEditing)
            {
                await _scheduleRepository.UpdateAsync(_currentSchedule);
                _logger.LogInformation("调度配置更新成功: {ScheduleId}", _currentSchedule.Id);
            }
            else
            {
                _currentSchedule.CreatedAt = DateTime.Now;
                await _scheduleRepository.AddAsync(_currentSchedule);
                _logger.LogInformation("调度配置创建成功");
            }
            await _scheduleRepository.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存调度配置时发生错误");
            MessageBox.Show($"保存调度配置时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    #endregion

    #region 事件处理程序

    /// <summary>
    /// 调度类型选择变化事件处理程序
    /// </summary>
    private void CmbScheduleType_SelectedIndexChanged(object? sender, EventArgs e)
    {
        UpdateScheduleTimeHint();
    }

    /// <summary>
    /// 确定按钮点击事件处理程序
    /// </summary>
    private async void btnOK_Click(object sender, EventArgs e)
    {
        try
        {
            btnOK.Enabled = false;
            btnOK.Text = "保存中...";

            if (await SaveSchedule())
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }
        finally
        {
            btnOK.Enabled = true;
            btnOK.Text = "确定";
        }
    }

    /// <summary>
    /// 取消按钮点击事件处理程序
    /// </summary>
    private void btnCancel_Click(object sender, EventArgs e)
    {
        this.DialogResult = DialogResult.Cancel;
        this.Close();
    }

    #endregion
}