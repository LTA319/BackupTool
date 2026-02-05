using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Client.Forms;

/// <summary>
/// 调度配置管理窗体
/// 提供调度配置的列表显示、创建、编辑、删除、启用和禁用功能
/// </summary>
public partial class ScheduleListForm : Form
{
    #region 私有字段

    /// <summary>
    /// 依赖注入服务提供者，用于获取各种服务实例
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 日志记录器，用于记录调度列表窗体的操作和错误信息
    /// </summary>
    private readonly ILogger<ScheduleListForm> _logger;

    /// <summary>
    /// 调度配置仓储接口，用于调度配置的CRUD操作
    /// </summary>
    private readonly IScheduleConfigurationRepository _scheduleRepository;

    /// <summary>
    /// 备份配置仓储接口，用于获取备份配置信息
    /// </summary>
    private readonly IBackupConfigurationRepository _configRepository;

    /// <summary>
    /// 调度配置列表，存储从数据库加载的所有调度配置
    /// </summary>
    private List<ScheduleConfiguration> _schedules = new();

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化ScheduleListForm类的新实例
    /// </summary>
    /// <param name="serviceProvider">依赖注入服务提供者</param>
    public ScheduleListForm(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<ScheduleListForm>>();
        _scheduleRepository = serviceProvider.GetRequiredService<IScheduleConfigurationRepository>();
        _configRepository = serviceProvider.GetRequiredService<IBackupConfigurationRepository>();

        InitializeComponent();
        InitializeForm();
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 初始化窗体的基本设置和属性
    /// 设置窗体标题、大小、位置，配置数据网格并加载调度配置数据
    /// </summary>
    private void InitializeForm()
    {
        try
        {
            SetupDataGridView();
            LoadSchedules();
            
            _logger.LogInformation("调度配置列表窗体初始化成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化调度配置列表窗体时发生错误");
            MessageBox.Show($"初始化窗体时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 设置数据网格视图的列和属性
    /// 配置调度配置列表网格的显示格式和事件处理
    /// </summary>
    private void SetupDataGridView()
    {
        dgvSchedules.AutoGenerateColumns = false;
        dgvSchedules.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvSchedules.MultiSelect = false;
        dgvSchedules.ReadOnly = true;
        dgvSchedules.AllowUserToAddRows = false;
        dgvSchedules.AllowUserToDeleteRows = false;

        // 添加列
        dgvSchedules.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "BackupConfigName",
            HeaderText = "备份配置",
            DataPropertyName = "BackupConfiguration.Name",
            Width = 200
        });

        dgvSchedules.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ScheduleType",
            HeaderText = "调度类型",
            DataPropertyName = "ScheduleType",
            Width = 100
        });

        dgvSchedules.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ScheduleTime",
            HeaderText = "调度时间",
            DataPropertyName = "ScheduleTime",
            Width = 150
        });

        dgvSchedules.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "IsEnabled",
            HeaderText = "启用",
            DataPropertyName = "IsEnabled",
            Width = 60
        });

        dgvSchedules.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "LastExecuted",
            HeaderText = "最后执行",
            DataPropertyName = "LastExecuted",
            Width = 120,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm" }
        });

        dgvSchedules.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "NextExecution",
            HeaderText = "下次执行",
            DataPropertyName = "NextExecution",
            Width = 120,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm" }
        });

        // 处理嵌套属性的单元格格式化
        dgvSchedules.CellFormatting += DgvSchedules_CellFormatting;
        dgvSchedules.SelectionChanged += DgvSchedules_SelectionChanged;
    }

    /// <summary>
    /// 异步加载所有调度配置
    /// 从数据库获取调度配置列表并更新界面显示
    /// </summary>
    private async void LoadSchedules()
    {
        try
        {
            btnRefresh.Enabled = false;
            btnRefresh.Text = "加载中...";

            _schedules = (await _scheduleRepository.GetAllAsync()).ToList();
            
            // 加载关联的备份配置信息
            foreach (var schedule in _schedules)
            {
                if (schedule.BackupConfigId > 0)
                {
                    schedule.BackupConfiguration = await _configRepository.GetByIdAsync(schedule.BackupConfigId);
                }
            }

            dgvSchedules.DataSource = _schedules;

            lblStatus.Text = $"已加载 {_schedules.Count} 个调度配置";
            lblStatus.ForeColor = Color.Green;

            _logger.LogInformation("已加载 {Count} 个调度配置", _schedules.Count);
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"加载调度配置时发生错误: {ex.Message}";
            lblStatus.ForeColor = Color.Red;
            _logger.LogError(ex, "加载调度配置时发生错误");
        }
        finally
        {
            btnRefresh.Enabled = true;
            btnRefresh.Text = "刷新";
        }
    }

    #endregion

    #region 事件处理程序

    /// <summary>
    /// 调度配置网格单元格格式化事件处理程序
    /// 自定义显示嵌套属性的值和调度类型的本地化显示
    /// </summary>
    private void DgvSchedules_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (dgvSchedules.Rows[e.RowIndex].DataBoundItem is ScheduleConfiguration schedule)
        {
            switch (dgvSchedules.Columns[e.ColumnIndex].Name)
            {
                case "BackupConfigName":
                    e.Value = schedule.BackupConfiguration?.Name ?? "未知配置";
                    break;
                case "ScheduleType":
                    e.Value = schedule.ScheduleType switch
                    {
                        ScheduleType.Daily => "每日",
                        ScheduleType.Weekly => "每周",
                        ScheduleType.Monthly => "每月",
                        _ => schedule.ScheduleType.ToString()
                    };
                    break;
                case "LastExecuted":
                    e.Value = schedule.LastExecuted?.ToString("yyyy-MM-dd HH:mm") ?? "从未执行";
                    break;
                case "NextExecution":
                    e.Value = schedule.NextExecution?.ToString("yyyy-MM-dd HH:mm") ?? "未计划";
                    break;
            }
        }
    }

    /// <summary>
    /// 调度配置网格选择变化事件处理程序
    /// 根据选择状态和调度配置的启用状态更新按钮的可用性
    /// </summary>
    private void DgvSchedules_SelectionChanged(object? sender, EventArgs e)
    {
        var hasSelection = dgvSchedules.SelectedRows.Count > 0;
        btnEdit.Enabled = hasSelection;
        btnDelete.Enabled = hasSelection;
        btnEnable.Enabled = hasSelection;
        btnDisable.Enabled = hasSelection;

        if (hasSelection && dgvSchedules.SelectedRows[0].DataBoundItem is ScheduleConfiguration schedule)
        {
            btnEnable.Enabled = !schedule.IsEnabled;
            btnDisable.Enabled = schedule.IsEnabled;
        }
    }

    /// <summary>
    /// 新建按钮点击事件处理程序
    /// </summary>
    private void btnNew_Click(object? sender, EventArgs e)
    {
        try
        {
            using var scheduleForm = new ScheduleForm(_serviceProvider);
            if (scheduleForm.ShowDialog() == DialogResult.OK)
            {
                LoadSchedules();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建新调度配置时发生错误");
            MessageBox.Show($"创建调度配置时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 编辑按钮点击事件处理程序
    /// </summary>
    private void btnEdit_Click(object? sender, EventArgs e)
    {
        try
        {
            if (dgvSchedules.SelectedRows.Count == 0)
                return;

            var selectedSchedule = dgvSchedules.SelectedRows[0].DataBoundItem as ScheduleConfiguration;
            if (selectedSchedule == null)
                return;

            using var scheduleForm = new ScheduleForm(_serviceProvider, selectedSchedule);
            if (scheduleForm.ShowDialog() == DialogResult.OK)
            {
                LoadSchedules();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "编辑调度配置时发生错误");
            MessageBox.Show($"编辑调度配置时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 删除按钮点击事件处理程序
    /// </summary>
    private async void btnDelete_Click(object? sender, EventArgs e)
    {
        try
        {
            if (dgvSchedules.SelectedRows.Count == 0)
                return;

            var selectedSchedule = dgvSchedules.SelectedRows[0].DataBoundItem as ScheduleConfiguration;
            if (selectedSchedule == null)
                return;

            var configName = selectedSchedule.BackupConfiguration?.Name ?? "未知配置";
            var result = MessageBox.Show(
                $"确定要删除调度配置吗?\n\n备份配置: {configName}\n调度类型: {GetScheduleTypeText(selectedSchedule.ScheduleType)}\n调度时间: {selectedSchedule.ScheduleTime}\n\n此操作无法撤销。",
                "确认删除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                await _scheduleRepository.DeleteAsync(selectedSchedule.Id);
                LoadSchedules();
                
                lblStatus.Text = $"调度配置删除成功";
                lblStatus.ForeColor = Color.Green;
                
                _logger.LogInformation("已删除调度配置: BackupConfig={BackupConfig}, Type={Type}, Time={Time}", 
                    configName, selectedSchedule.ScheduleType, selectedSchedule.ScheduleTime);
            }
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"删除调度配置时发生错误: {ex.Message}";
            lblStatus.ForeColor = Color.Red;
            _logger.LogError(ex, "删除调度配置时发生错误");
        }
    }

    /// <summary>
    /// 启用按钮点击事件处理程序
    /// </summary>
    private async void btnEnable_Click(object? sender, EventArgs e)
    {
        try
        {
            if (dgvSchedules.SelectedRows.Count == 0)
                return;

            var selectedSchedule = dgvSchedules.SelectedRows[0].DataBoundItem as ScheduleConfiguration;
            if (selectedSchedule == null)
                return;

            await _scheduleRepository.SetEnabledAsync(selectedSchedule.Id, true);
            
            // 计算并更新下次执行时间
            var nextExecution = selectedSchedule.CalculateNextExecution();
            if (nextExecution.HasValue)
            {
                await _scheduleRepository.UpdateNextExecutionAsync(selectedSchedule.Id, nextExecution.Value);
            }

            LoadSchedules();
            lblStatus.Text = $"调度配置已启用";
            lblStatus.ForeColor = Color.Green;
            _logger.LogInformation("已启用调度配置: {ScheduleId}", selectedSchedule.Id);
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"启用调度配置时发生错误: {ex.Message}";
            lblStatus.ForeColor = Color.Red;
            _logger.LogError(ex, "启用调度配置时发生错误");
        }
    }

    /// <summary>
    /// 禁用按钮点击事件处理程序
    /// </summary>
    private async void btnDisable_Click(object? sender, EventArgs e)
    {
        try
        {
            if (dgvSchedules.SelectedRows.Count == 0)
                return;

            var selectedSchedule = dgvSchedules.SelectedRows[0].DataBoundItem as ScheduleConfiguration;
            if (selectedSchedule == null)
                return;

            await _scheduleRepository.SetEnabledAsync(selectedSchedule.Id, false);
            LoadSchedules();
            lblStatus.Text = $"调度配置已禁用";
            lblStatus.ForeColor = Color.Green;
            _logger.LogInformation("已禁用调度配置: {ScheduleId}", selectedSchedule.Id);
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"禁用调度配置时发生错误: {ex.Message}";
            lblStatus.ForeColor = Color.Red;
            _logger.LogError(ex, "禁用调度配置时发生错误");
        }
    }

    /// <summary>
    /// 刷新按钮点击事件处理程序
    /// </summary>
    private void btnRefresh_Click(object? sender, EventArgs e)
    {
        LoadSchedules();
    }

    /// <summary>
    /// 关闭按钮点击事件处理程序
    /// </summary>
    private void btnClose_Click(object? sender, EventArgs e)
    {
        this.Close();
    }

    /// <summary>
    /// 获取调度类型的中文显示文本
    /// </summary>
    private static string GetScheduleTypeText(ScheduleType scheduleType)
    {
        return scheduleType switch
        {
            ScheduleType.Daily => "每日",
            ScheduleType.Weekly => "每周",
            ScheduleType.Monthly => "每月",
            _ => scheduleType.ToString()
        };
    }

    #endregion
}