using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Client.Forms;
using MySqlBackupTool.Client.Tools;
using MySqlBackupTool.Client.EmbeddedForms;

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

    /// <summary>
    /// 标记是否真正退出应用程序，用于区分隐藏到托盘和真正退出
    /// </summary>
    private bool _isReallyClosing = false;

    /// <summary>
    /// 嵌入式窗体主机，用于管理嵌入式窗体的生命周期
    /// </summary>
    private EmbeddedFormHost? _embeddedFormHost;

    /// <summary>
    /// 导航面板控件，用于显示面包屑导航
    /// </summary>
    private NavigationPanel? _navigationPanelControl;

    /// <summary>
    /// 保存的导航状态，用于从系统托盘恢复时使用
    /// </summary>
    private NavigationState? _savedNavigationState;

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

            // Enable keyboard preview for global shortcuts
            this.KeyPreview = true;

            // 初始化系统托盘
            InitializeSystemTray();

            // 初始化嵌入式窗体主机
            InitializeEmbeddedFormHost();

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
    /// 初始化系统托盘功能
    /// 设置托盘图标、提示文本和相关事件
    /// </summary>
    private void InitializeSystemTray()
    {
        try
        {
            // 设置托盘图标（使用应用程序图标或默认图标）
            if (this.Icon != null)
            {
                notifyIcon.Icon = this.Icon;
            }
            else
            {
                // 如果没有应用程序图标，使用系统默认图标
                notifyIcon.Icon = SystemIcons.Application;
            }

            notifyIcon.Text = "MySQL Backup Tool - 点击显示主窗口";
            notifyIcon.Visible = false; // 初始时不显示托盘图标

            _logger.LogInformation("系统托盘初始化成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化系统托盘时发生错误");
        }
    }

    /// <summary>
    /// 初始化嵌入式窗体主机
    /// 创建EmbeddedFormHost实例并连接事件处理程序
    /// </summary>
    private void InitializeEmbeddedFormHost()
    {
        try
        {
            _logger.LogInformation("正在初始化嵌入式窗体主机");

            // Initialize NavigationPanel control
            _navigationPanelControl = new NavigationPanel();
            
            // Clear existing controls in navigationPanel and add the NavigationPanel control
            navigationPanel.Controls.Clear();
            _navigationPanelControl.Dock = DockStyle.Fill;
            navigationPanel.Controls.Add(_navigationPanelControl);

            // 创建EmbeddedFormHost实例
            var embeddedFormLogger = _serviceProvider.GetRequiredService<ILogger<EmbeddedFormHost>>();
            _embeddedFormHost = new EmbeddedFormHost(
                contentPanel,
                _serviceProvider,
                embeddedFormLogger);

            // 连接事件处理程序
            _embeddedFormHost.ActiveFormChanged += OnActiveFormChanged;
            _embeddedFormHost.FormTitleChanged += OnFormTitleChanged;
            _embeddedFormHost.FormStatusChanged += OnFormStatusChanged;

            // 显示欢迎屏幕
            ShowWelcomeScreen();

            _logger.LogInformation("嵌入式窗体主机初始化成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化嵌入式窗体主机时发生错误");
            throw;
        }
    }
    
    /// <summary>
    /// 显示欢迎屏幕
    /// </summary>
    private void ShowWelcomeScreen()
    {
        try
        {
            _embeddedFormHost?.ShowForm<WelcomeControl>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "显示欢迎屏幕时发生错误");
        }
    }

    /// <summary>
    /// 处理活动窗体更改事件
    /// 更新标题栏和导航面板
    /// </summary>
    private void OnActiveFormChanged(object? sender, IEmbeddedForm? form)
    {
        try
        {
            if (form != null)
            {
                // 更新标题栏
                this.Text = $"MySQL Backup Tool - {form.Title}";

                // 更新导航面板
                if (_navigationPanelControl != null)
                {
                    _navigationPanelControl.NavigationPath = form.NavigationPath;
                }

                // 更新状态栏
                toolStripStatusLabel.Text = $"当前视图: {form.Title}";
            }
            else
            {
                // 显示默认标题
                this.Text = "MySQL Backup Tool - Client";

                // 清除导航面板
                if (_navigationPanelControl != null)
                {
                    _navigationPanelControl.NavigationPath = "Home";
                }

                // 更新状态栏
                toolStripStatusLabel.Text = "就绪";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理活动窗体更改时发生错误");
        }
    }

    /// <summary>
    /// 处理窗体标题更改事件
    /// 更新主窗体标题栏以反映嵌入式窗体的标题变化
    /// </summary>
    private void OnFormTitleChanged(object? sender, string newTitle)
    {
        try
        {
            if (!string.IsNullOrEmpty(newTitle))
            {
                // 更新标题栏，使用标准格式
                this.Text = $"MySQL Backup Tool - {newTitle}";
                
                _logger.LogDebug("主窗体标题已更新为: {Title}", this.Text);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理窗体标题更改时发生错误");
        }
    }

    /// <summary>
    /// 处理窗体状态更改事件
    /// 更新主窗体状态栏以显示嵌入式窗体的状态消息
    /// </summary>
    private void OnFormStatusChanged(object? sender, string statusMessage)
    {
        try
        {
            if (!string.IsNullOrEmpty(statusMessage))
            {
                // 更新状态栏
                toolStripStatusLabel.Text = statusMessage;
                
                _logger.LogDebug("状态栏已更新为: {Status}", statusMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理窗体状态更改时发生错误");
        }
    }

    #endregion

    #region 事件处理程序

    /// <summary>
    /// 配置管理菜单项点击事件处理程序
    /// 显示配置列表嵌入式窗体，允许用户管理备份配置
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void configurationToolStripMenuItem_Click(object sender, EventArgs e)
    {
        try
        {
            // using var configListForm = new ConfigurationListForm(_serviceProvider);
            // configListForm.ShowDialog();
            _embeddedFormHost?.ShowForm<ConfigurationListControl>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打开配置管理时发生错误");
            MessageBox.Show($"打开配置管理时发生错误: {ex.Message}",
                "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 调度管理菜单项点击事件处理程序
    /// 显示调度配置管理嵌入式窗体
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void scheduleManagementToolStripMenuItem_Click(object sender, EventArgs e)
    {
        try
        {
            // using var scheduleListForm = new ScheduleListForm(_serviceProvider);
            // scheduleListForm.ShowDialog();
            _embeddedFormHost?.ShowForm<ScheduleListControl>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打开调度管理时发生错误");
            MessageBox.Show($"打开调度管理时发生错误: {ex.Message}",
                "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// 备份监控菜单项点击事件处理程序
    /// 显示备份监控嵌入式窗体
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void backupMonitorToolStripMenuItem_Click(object sender, EventArgs e)
    {
        try
        {
            // 测试数据库连接后打开监控窗体
            // _ = TestDatabaseAndOpenMonitor();
            _embeddedFormHost?.ShowForm<BackupMonitorControl>();
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
    /// 显示日志浏览器嵌入式窗体，允许用户查看系统日志
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void logBrowserToolStripMenuItem_Click(object sender, EventArgs e)
    {
        try
        {
            // using var logBrowserForm = new LogBrowserForm(_serviceProvider);
            // logBrowserForm.ShowDialog();
            _embeddedFormHost?.ShowForm<LogBrowserControl>();
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
        _isReallyClosing = true;
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

    /// <summary>
    /// 系统托盘图标双击事件处理程序
    /// 双击托盘图标时显示主窗体
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void notifyIcon_DoubleClick(object sender, EventArgs e)
    {
        ShowMainWindow();
    }

    /// <summary>
    /// 托盘右键菜单"显示主窗口"点击事件处理程序
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void showToolStripMenuItem_Click(object sender, EventArgs e)
    {
        ShowMainWindow();
    }

    /// <summary>
    /// 托盘右键菜单"退出"点击事件处理程序
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void exitTrayToolStripMenuItem_Click(object sender, EventArgs e)
    {
        _isReallyClosing = true;
        this.Close();
    }

    /// <summary>
    /// 系统托盘帮助菜单项点击事件处理程序
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void systemTrayHelpToolStripMenuItem_Click(object sender, EventArgs e)
    {
        try
        {
            // 显示系统托盘功能帮助信息
            var helpMessage = @"MySQL Backup Tool 系统托盘功能帮助

                                基本操作：
                                • 点击关闭按钮 (X) → 隐藏到系统托盘
                                • 双击托盘图标 → 恢复主窗体
                                • 右键托盘图标 → 显示菜单

                                完全退出：
                                • 菜单栏：File → Exit
                                • 托盘右键：退出

                                特性：
                                • 首次隐藏时显示提示气球
                                • 后台持续运行
                                • 快速访问和恢复

                                注意：
                                • 关闭按钮默认隐藏到托盘，不会退出程序
                                • 使用菜单或托盘右键菜单才能完全退出";

            MessageBox.Show(helpMessage, "系统托盘功能帮助",
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            // 同时在日志中记录帮助信息的显示
            SystemTrayExample.ShowSystemTrayHelp(_logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "显示系统托盘帮助时发生错误");
            MessageBox.Show($"显示帮助信息时发生错误: {ex.Message}",
                "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void transferLogViewToolStripMenuItem_Click(object sender, EventArgs e)
    {
        try
        {
            // using var transferLogViewerForm = new TransferLogViewerForm(_serviceProvider);
            // transferLogViewerForm.ShowDialog();
            _embeddedFormHost?.ShowForm<TransferLogViewerControl>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打开传输日记时发生错误");
            MessageBox.Show($"打开传输日记时发生错误: {ex.Message}",
                "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    #endregion

    #region 重写方法

    /// <summary>
    /// 重写窗体关闭事件
    /// 在窗体关闭时记录日志并执行清理操作
    /// 如果不是真正退出，则隐藏到系统托盘
    /// </summary>
    /// <param name="e">窗体关闭事件参数</param>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        try
        {
            if (!_isReallyClosing)
            {
                // 如果不是真正退出，则隐藏到系统托盘
                e.Cancel = true;
                HideToSystemTray();
                return;
            }

            _logger.LogInformation("应用程序正在关闭");

            // Properly dispose embedded form host and all forms
            if (_embeddedFormHost != null)
            {
                try
                {
                    _logger.LogInformation("正在清理嵌入式窗体资源");
                    _embeddedFormHost.Dispose();
                    _embeddedFormHost = null;
                    _logger.LogInformation("已清理嵌入式窗体资源");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "清理嵌入式窗体资源时发生错误");
                }
            }

            // 隐藏托盘图标
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
            }

            base.OnFormClosing(e);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "应用程序关闭过程中发生错误");
        }
    }

    /// <summary>
    /// 处理键盘快捷键
    /// 实现全局快捷键处理，包括Escape键返回欢迎屏幕
    /// </summary>
    /// <param name="msg">Windows消息</param>
    /// <param name="keyData">按键数据</param>
    /// <returns>如果键已处理则返回true，否则返回false</returns>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        try
        {
            // Handle Escape key - return to welcome screen
            if (keyData == Keys.Escape)
            {
                _logger.LogDebug("Escape key pressed, returning to welcome screen");
                
                // Check if current form can be closed
                if (_embeddedFormHost?.CurrentForm != null)
                {
                    if (_embeddedFormHost.CurrentForm.CanClose())
                    {
                        ShowWelcomeScreen();
                        return true;
                    }
                    else
                    {
                        _logger.LogDebug("Current form cannot be closed, Escape key ignored");
                        return true; // Still consume the key to prevent default behavior
                    }
                }
                
                return true;
            }

            // Handle menu shortcuts - these should work regardless of active form
            // Alt+F for File menu
            if (keyData == (Keys.Alt | Keys.F))
            {
                fileToolStripMenuItem.ShowDropDown();
                return true;
            }

            // Alt+T for Tools menu
            if (keyData == (Keys.Alt | Keys.T))
            {
                toolsToolStripMenuItem.ShowDropDown();
                return true;
            }

            // Alt+H for Help menu
            if (keyData == (Keys.Alt | Keys.H))
            {
                helpToolStripMenuItem.ShowDropDown();
                return true;
            }

            // Forward other keyboard events to the active embedded form
            // The embedded form's controls will handle their own shortcuts naturally
            // through the standard Windows Forms event chain
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理键盘快捷键时发生错误");
        }

        // Let the base class handle the key if we didn't process it
        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>
    /// 处理按键事件
    /// 提供额外的键盘事件处理，用于调试和日志记录
    /// </summary>
    /// <param name="e">按键事件参数</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        try
        {
            // Log keyboard events for debugging (only in debug builds)
            #if DEBUG
            _logger.LogTrace("Key pressed: {Key}, Modifiers: {Modifiers}", e.KeyCode, e.Modifiers);
            #endif

            // Allow the event to propagate to child controls
            base.OnKeyDown(e);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理按键事件时发生错误");
        }
    }

    #endregion

    #region 私有辅助方法

    /// <summary>
    /// 隐藏窗体到系统托盘
    /// 保存当前嵌入式窗体的导航状态，以便恢复时使用
    /// </summary>
    private void HideToSystemTray()
    {
        try
        {
            // Store navigation state before hiding to tray
            _savedNavigationState = _embeddedFormHost?.GetCurrentStateForPreservation();
            
            if (_savedNavigationState != null)
            {
                _logger.LogInformation("保存导航状态: {FormType}", _savedNavigationState.FormType);
            }
            else
            {
                _logger.LogInformation("当前在欢迎屏幕，无需保存导航状态");
            }

            this.Hide();
            notifyIcon.Visible = true;

            // 显示托盘提示
            notifyIcon.ShowBalloonTip(2000,
                "MySQL Backup Tool",
                "应用程序已最小化到系统托盘。双击图标或右键选择\"显示主窗口\"来恢复窗口。",
                ToolTipIcon.Info);

            _logger.LogInformation("应用程序已隐藏到系统托盘");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "隐藏到系统托盘时发生错误");
        }
    }

    /// <summary>
    /// 从系统托盘显示主窗体
    /// 恢复之前保存的嵌入式窗体导航状态
    /// </summary>
    private void ShowMainWindow()
    {
        try
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
            this.Activate();
            notifyIcon.Visible = false;

            // Restore navigation state when showing from tray
            if (_savedNavigationState != null)
            {
                _logger.LogInformation("恢复导航状态: {FormType}", _savedNavigationState.FormType);
                
                var restored = _embeddedFormHost?.RestoreNavigationState(_savedNavigationState);
                
                if (restored == true)
                {
                    _logger.LogInformation("成功恢复导航状态");
                }
                else
                {
                    _logger.LogWarning("无法恢复导航状态，显示欢迎屏幕");
                }
                
                // Clear the saved state after restoration attempt
                _savedNavigationState = null;
            }
            else
            {
                _logger.LogInformation("无保存的导航状态，保持当前状态");
            }

            _logger.LogInformation("主窗体已从系统托盘恢复显示");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从系统托盘恢复窗体时发生错误");
        }
    }

    #endregion


}