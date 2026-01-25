using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Client.Forms;

namespace MySqlBackupTool.Client;

/// <summary>
/// MySQL备份工具客户端的主窗体
/// 提供应用程序的主要入口点和导航功能
/// </summary>
public partial class FormMain : Form
{
    #region 私有字段

    /// <summary>
    /// 依赖注入服务提供者，用于获取各种服务实例
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 日志记录器，用于记录主窗体的操作和错误信息
    /// </summary>
    private readonly ILogger<FormMain> _logger;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化FormMain类的新实例
    /// </summary>
    /// <param name="serviceProvider">依赖注入服务提供者</param>
    public FormMain(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<FormMain>>();

        InitializeComponent();
        InitializeApplication();
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 初始化应用程序的基本设置和属性
    /// 设置窗体标题、大小、位置等基本属性
    /// </summary>
    private void InitializeApplication()
    {
        try
        {
            _logger.LogInformation("正在初始化主窗体");

            // 设置窗体属性
            this.Text = "MySQL Backup Tool - Client";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            _logger.LogInformation("主窗体初始化成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化主窗体时发生错误");
            MessageBox.Show($"初始化应用程序时发生错误: {ex.Message}",
                "初始化错误",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 测试数据库连接并打开备份监控窗体
    /// 在打开监控窗体前先测试数据库连接，如果连接失败则提供修复选项
    /// </summary>
    /// <returns>异步任务</returns>
    private async Task TestDatabaseAndOpenMonitor()
    {
        try
        {
            // 显示加载状态
            this.Cursor = Cursors.WaitCursor;
            this.Text = "MySQL Backup Tool - Client (正在测试数据库连接...)";

            // 测试数据库连接
            var testResult = await DatabaseConnectionTest.TestDatabaseConnectionAsync();

            if (!testResult.Success)
            {
                _logger.LogWarning("数据库连接测试失败: {Error}", testResult.Message);

                var result = MessageBox.Show(
                    $"数据库连接测试失败:\n\n{testResult.Message}\n\n" +
                    "是否要尝试自动修复?",
                    "数据库连接问题",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    this.Text = "MySQL Backup Tool - Client (正在修复数据库...)";
                    var repairResult = await DatabaseConnectionTest.RepairDatabaseAsync();

                    MessageBox.Show(repairResult.ToString(),
                        repairResult.Success ? "数据库修复完成" : "数据库修复失败",
                        MessageBoxButtons.OK,
                        repairResult.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);

                    if (!repairResult.Success)
                        return;
                }
                else if (result == DialogResult.Cancel)
                {
                    return;
                }
                // 如果选择"否"，则继续执行
            }
            else
            {
                _logger.LogInformation("数据库连接测试通过，耗时 {Time}ms",
                    testResult.TotalTime.TotalMilliseconds);
            }

            // 打开监控窗体
            using var monitorForm = new BackupMonitorForm(_serviceProvider);
            monitorForm.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据库测试或打开备份监控时发生错误");
            MessageBox.Show($"错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            this.Cursor = Cursors.Default;
            this.Text = "MySQL Backup Tool - Client";
        }
    }

    #endregion

    #region 事件处理程序

    /// <summary>
    /// 配置管理菜单项点击事件处理程序
    /// 打开配置列表窗体，允许用户管理备份配置
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void configurationToolStripMenuItem_Click(object sender, EventArgs e)
    {
        try
        {
            using var configListForm = new ConfigurationListForm(_serviceProvider);
            configListForm.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打开配置管理时发生错误");
            MessageBox.Show($"打开配置管理时发生错误: {ex.Message}",
                "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 备份监控菜单项点击事件处理程序
    /// 测试数据库连接后打开备份监控窗体
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void backupMonitorToolStripMenuItem_Click(object sender, EventArgs e)
    {
        try
        {
            // 测试数据库连接后打开监控窗体
            _ = TestDatabaseAndOpenMonitor();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打开备份监控时发生错误");
            MessageBox.Show($"打开备份监控时发生错误: {ex.Message}",
                "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 日志浏览器菜单项点击事件处理程序
    /// 打开日志浏览器窗体，允许用户查看系统日志
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void logBrowserToolStripMenuItem_Click(object sender, EventArgs e)
    {
        try
        {
            using var logBrowserForm = new LogBrowserForm(_serviceProvider);
            logBrowserForm.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打开日志浏览器时发生错误");
            MessageBox.Show($"打开日志浏览器时发生错误: {ex.Message}",
                "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 退出菜单项点击事件处理程序
    /// 关闭主窗体，退出应用程序
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void exitToolStripMenuItem_Click(object sender, EventArgs e)
    {
        this.Close();
    }

    /// <summary>
    /// 测试数据库连接菜单项点击事件处理程序
    /// 执行数据库连接测试并显示详细结果，提供修复选项
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private async void testDatabaseConnectionToolStripMenuItem_Click(object sender, EventArgs e)
    {
        try
        {
            this.Cursor = Cursors.WaitCursor;
            toolStripStatusLabel.Text = "正在测试数据库连接...";

            var testResult = await DatabaseConnectionTest.TestDatabaseConnectionAsync();

            // 创建结果显示窗体
            var form = new Form
            {
                Text = testResult.Success ? "数据库测试 - 成功" : "数据库测试 - 失败",
                Size = new Size(600, 500),
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = true,
                MinimizeBox = false
            };

            // 创建文本框显示测试结果
            var textBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9),
                ReadOnly = true,
                Text = testResult.ToString()
            };

            // 创建按钮面板
            var buttonPanel = new Panel
            {
                Height = 40,
                Dock = DockStyle.Bottom
            };

            // 创建关闭按钮
            var closeButton = new Button
            {
                Text = "关闭",
                Size = new Size(75, 23),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Location = new Point(buttonPanel.Width - 85, 8),
                DialogResult = DialogResult.OK
            };

            // 如果测试失败，添加修复按钮
            if (!testResult.Success)
            {
                var repairButton = new Button
                {
                    Text = "修复数据库",
                    Size = new Size(100, 23),
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                    Location = new Point(buttonPanel.Width - 195, 8)
                };

                repairButton.Click += async (s, args) =>
                {
                    try
                    {
                        repairButton.Enabled = false;
                        repairButton.Text = "正在修复...";

                        var repairResult = await DatabaseConnectionTest.RepairDatabaseAsync();
                        textBox.Text = repairResult.ToString();

                        if (repairResult.Success)
                        {
                            form.Text = "数据库修复 - 成功";
                            repairButton.Visible = false;
                        }
                        else
                        {
                            repairButton.Text = "修复数据库";
                            repairButton.Enabled = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"修复过程中发生错误: {ex.Message}", "修复错误",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        repairButton.Text = "修复数据库";
                        repairButton.Enabled = true;
                    }
                };

                buttonPanel.Controls.Add(repairButton);
            }

            buttonPanel.Controls.Add(closeButton);
            form.Controls.Add(textBox);
            form.Controls.Add(buttonPanel);
            form.AcceptButton = closeButton;

            form.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试数据库连接时发生错误");
            MessageBox.Show($"测试数据库连接时发生错误: {ex.Message}",
                "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            this.Cursor = Cursors.Default;
            toolStripStatusLabel.Text = "就绪";
        }
    }

    /// <summary>
    /// 关于菜单项点击事件处理程序
    /// 显示应用程序的版本和信息
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
    {
        MessageBox.Show("MySQL Backup Tool - Client\n\nMySQL数据库的分布式备份解决方案。\n\n版本 1.0",
            "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>
    /// 欢迎标签点击事件处理程序
    /// 当前为空实现，可用于添加欢迎页面的交互功能
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void lblWelcome_Click(object sender, EventArgs e)
    {
        // 当前为空实现
    }

    #endregion

    #region 重写方法

    /// <summary>
    /// 重写窗体关闭事件
    /// 在窗体关闭时记录日志并执行清理操作
    /// </summary>
    /// <param name="e">窗体关闭事件参数</param>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        try
        {
            _logger.LogInformation("应用程序正在关闭");
            base.OnFormClosing(e);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "应用程序关闭过程中发生错误");
        }
    }

    #endregion
}