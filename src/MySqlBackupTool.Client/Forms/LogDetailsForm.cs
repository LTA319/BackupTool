using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Client.Forms;

/// <summary>
/// 日志详情显示窗体
/// 提供备份日志的详细信息展示，包括配置详情、传输进度、错误分析等
/// </summary>
public partial class LogDetailsForm : Form
{
    #region 私有字段

    /// <summary>
    /// 依赖注入服务提供者，用于获取各种服务实例
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 日志记录器，用于记录日志详情窗体的操作和错误信息
    /// </summary>
    private readonly ILogger<LogDetailsForm> _logger;

    /// <summary>
    /// 要显示详情的备份日志对象
    /// </summary>
    private readonly BackupLog _log;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化LogDetailsForm类的新实例
    /// </summary>
    /// <param name="serviceProvider">依赖注入服务提供者</param>
    /// <param name="log">要显示详情的备份日志</param>
    public LogDetailsForm(IServiceProvider serviceProvider, BackupLog log)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<LogDetailsForm>>();
        _log = log;

        InitializeComponent();
        InitializeForm();
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 初始化窗体的基本设置和属性
    /// 设置窗体标题、大小、位置并加载日志详情
    /// </summary>
    private void InitializeForm()
    {
        try
        {
            this.Text = $"日志详情 - {_log.Id}";
            this.Size = new Size(600, 400);
            this.StartPosition = FormStartPosition.CenterParent;

            LoadLogDetails();
            
            _logger.LogInformation("日志详情窗体已为日志 {LogId} 初始化", _log.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化日志详情窗体时发生错误");
            MessageBox.Show($"初始化窗体时发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 加载并显示日志的详细信息
    /// 展示完整的配置详情、传输进度图表、错误分析、性能指标等
    /// </summary>
    private void LoadLogDetails()
    {
        txtDetails.Text = $"日志 {_log.Id} 的详细视图将在此处实现。\n\n" +
                         $"此窗体将显示全面的日志信息，包括:\n" +
                         $"- 完整的配置详情\n" +
                         $"- 传输进度图表\n" +
                         $"- 错误分析\n" +
                         $"- 性能指标\n" +
                         $"- 相关日志和依赖关系";
    }

    #endregion

    #region 事件处理程序

    /// <summary>
    /// 关闭按钮点击事件处理程序
    /// 关闭日志详情窗体
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void btnClose_Click(object sender, EventArgs e)
    {
        this.Close();
    }

    #endregion
}