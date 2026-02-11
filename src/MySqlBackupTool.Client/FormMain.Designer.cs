namespace MySqlBackupTool.Client
{
    partial class FormMain
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormMain));
            menuStrip = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            exitToolStripMenuItem = new ToolStripMenuItem();
            toolsToolStripMenuItem = new ToolStripMenuItem();
            configurationToolStripMenuItem = new ToolStripMenuItem();
            scheduleManagementToolStripMenuItem = new ToolStripMenuItem();
            backupMonitorToolStripMenuItem = new ToolStripMenuItem();
            logBrowserToolStripMenuItem = new ToolStripMenuItem();
            transferLogViewToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            testDatabaseConnectionToolStripMenuItem = new ToolStripMenuItem();
            helpToolStripMenuItem = new ToolStripMenuItem();
            aboutToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator3 = new ToolStripSeparator();
            systemTrayHelpToolStripMenuItem = new ToolStripMenuItem();
            statusStrip = new StatusStrip();
            toolStripStatusLabel = new ToolStripStatusLabel();
            lblWelcome = new Label();
            notifyIcon = new NotifyIcon(components);
            trayContextMenu = new ContextMenuStrip(components);
            showToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator2 = new ToolStripSeparator();
            exitTrayToolStripMenuItem = new ToolStripMenuItem();
            contentPanel = new Panel();
            navigationPanel = new Panel();
            menuStrip.SuspendLayout();
            statusStrip.SuspendLayout();
            trayContextMenu.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip
            // 
            menuStrip.ImageScalingSize = new Size(24, 24);
            menuStrip.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, toolsToolStripMenuItem, helpToolStripMenuItem });
            menuStrip.Location = new Point(0, 0);
            menuStrip.Name = "menuStrip";
            menuStrip.Padding = new Padding(7, 2, 0, 2);
            menuStrip.Size = new Size(1096, 28);
            menuStrip.TabIndex = 0;
            menuStrip.Text = "menuStrip";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { exitToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(48, 24);
            fileToolStripMenuItem.Text = "&File";
            // 
            // exitToolStripMenuItem
            // 
            exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            exitToolStripMenuItem.Size = new Size(118, 26);
            exitToolStripMenuItem.Text = "E&xit";
            exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;
            // 
            // toolsToolStripMenuItem
            // 
            toolsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { configurationToolStripMenuItem, scheduleManagementToolStripMenuItem, backupMonitorToolStripMenuItem, logBrowserToolStripMenuItem, transferLogViewToolStripMenuItem, toolStripSeparator1, testDatabaseConnectionToolStripMenuItem });
            toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            toolsToolStripMenuItem.Size = new Size(63, 24);
            toolsToolStripMenuItem.Text = "&Tools";
            // 
            // configurationToolStripMenuItem
            // 
            configurationToolStripMenuItem.Name = "configurationToolStripMenuItem";
            configurationToolStripMenuItem.Size = new Size(293, 26);
            configurationToolStripMenuItem.Text = "&Configuration Management";
            configurationToolStripMenuItem.Click += configurationToolStripMenuItem_Click;
            // 
            // scheduleManagementToolStripMenuItem
            // 
            scheduleManagementToolStripMenuItem.Name = "scheduleManagementToolStripMenuItem";
            scheduleManagementToolStripMenuItem.Size = new Size(293, 26);
            scheduleManagementToolStripMenuItem.Text = "&Schedule Management";
            scheduleManagementToolStripMenuItem.Click += scheduleManagementToolStripMenuItem_Click;
            // 
            // backupMonitorToolStripMenuItem
            // 
            backupMonitorToolStripMenuItem.Name = "backupMonitorToolStripMenuItem";
            backupMonitorToolStripMenuItem.Size = new Size(293, 26);
            backupMonitorToolStripMenuItem.Text = "&Backup Monitor";
            backupMonitorToolStripMenuItem.Click += backupMonitorToolStripMenuItem_Click;
            // 
            // logBrowserToolStripMenuItem
            // 
            logBrowserToolStripMenuItem.Name = "logBrowserToolStripMenuItem";
            logBrowserToolStripMenuItem.Size = new Size(293, 26);
            logBrowserToolStripMenuItem.Text = "&Log Browser";
            logBrowserToolStripMenuItem.Click += logBrowserToolStripMenuItem_Click;
            // 
            // transferLogViewToolStripMenuItem
            // 
            transferLogViewToolStripMenuItem.Name = "transferLogViewToolStripMenuItem";
            transferLogViewToolStripMenuItem.Size = new Size(293, 26);
            transferLogViewToolStripMenuItem.Text = "Transfer Log";
            transferLogViewToolStripMenuItem.Click += transferLogViewToolStripMenuItem_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(290, 6);
            // 
            // testDatabaseConnectionToolStripMenuItem
            // 
            testDatabaseConnectionToolStripMenuItem.Name = "testDatabaseConnectionToolStripMenuItem";
            testDatabaseConnectionToolStripMenuItem.Size = new Size(293, 26);
            testDatabaseConnectionToolStripMenuItem.Text = "&Test Database Connection";
            testDatabaseConnectionToolStripMenuItem.Click += testDatabaseConnectionToolStripMenuItem_Click;
            // 
            // helpToolStripMenuItem
            // 
            helpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { aboutToolStripMenuItem, toolStripSeparator3, systemTrayHelpToolStripMenuItem });
            helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            helpToolStripMenuItem.Size = new Size(58, 24);
            helpToolStripMenuItem.Text = "&Help";
            // 
            // aboutToolStripMenuItem
            // 
            aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            aboutToolStripMenuItem.Size = new Size(182, 26);
            aboutToolStripMenuItem.Text = "&About";
            aboutToolStripMenuItem.Click += aboutToolStripMenuItem_Click;
            // 
            // toolStripSeparator3
            // 
            toolStripSeparator3.Name = "toolStripSeparator3";
            toolStripSeparator3.Size = new Size(179, 6);
            // 
            // systemTrayHelpToolStripMenuItem
            // 
            systemTrayHelpToolStripMenuItem.Name = "systemTrayHelpToolStripMenuItem";
            systemTrayHelpToolStripMenuItem.Size = new Size(182, 26);
            systemTrayHelpToolStripMenuItem.Text = "系统托盘帮助";
            systemTrayHelpToolStripMenuItem.Click += systemTrayHelpToolStripMenuItem_Click;
            // 
            // statusStrip
            // 
            statusStrip.ImageScalingSize = new Size(24, 24);
            statusStrip.Items.AddRange(new ToolStripItem[] { toolStripStatusLabel });
            statusStrip.Location = new Point(0, 587);
            statusStrip.Name = "statusStrip";
            statusStrip.Padding = new Padding(2, 0, 18, 0);
            statusStrip.Size = new Size(1096, 26);
            statusStrip.TabIndex = 1;
            statusStrip.Text = "statusStrip";
            // 
            // toolStripStatusLabel
            // 
            toolStripStatusLabel.Name = "toolStripStatusLabel";
            toolStripStatusLabel.Size = new Size(54, 20);
            toolStripStatusLabel.Text = "Ready";
            // 
            // lblWelcome
            // 
            lblWelcome.AutoSize = true;
            lblWelcome.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            lblWelcome.Location = new Point(218, 245);
            lblWelcome.Margin = new Padding(5, 0, 5, 0);
            lblWelcome.Name = "lblWelcome";
            lblWelcome.Size = new Size(382, 32);
            lblWelcome.TabIndex = 2;
            lblWelcome.Text = "Welcome to MySQL Backup Tool";
            lblWelcome.Click += lblWelcome_Click;
            // 
            // notifyIcon
            // 
            notifyIcon.ContextMenuStrip = trayContextMenu;
            notifyIcon.Text = "MySQL Backup Tool";
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += notifyIcon_DoubleClick;
            // 
            // trayContextMenu
            // 
            trayContextMenu.ImageScalingSize = new Size(24, 24);
            trayContextMenu.Items.AddRange(new ToolStripItem[] { showToolStripMenuItem, toolStripSeparator2, exitTrayToolStripMenuItem });
            trayContextMenu.Name = "trayContextMenu";
            trayContextMenu.Size = new Size(154, 58);
            // 
            // showToolStripMenuItem
            // 
            showToolStripMenuItem.Name = "showToolStripMenuItem";
            showToolStripMenuItem.Size = new Size(153, 24);
            showToolStripMenuItem.Text = "显示主窗口";
            showToolStripMenuItem.Click += showToolStripMenuItem_Click;
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new Size(150, 6);
            // 
            // exitTrayToolStripMenuItem
            // 
            exitTrayToolStripMenuItem.Name = "exitTrayToolStripMenuItem";
            exitTrayToolStripMenuItem.Size = new Size(153, 24);
            exitTrayToolStripMenuItem.Text = "退出";
            exitTrayToolStripMenuItem.Click += exitTrayToolStripMenuItem_Click;
            // 
            // contentPanel
            // 
            contentPanel.BackColor = SystemColors.Window;
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.Location = new Point(0, 73);
            contentPanel.Margin = new Padding(2);
            contentPanel.Name = "contentPanel";
            contentPanel.Size = new Size(1096, 514);
            contentPanel.TabIndex = 4;
            // 
            // navigationPanel
            // 
            navigationPanel.BackColor = SystemColors.Control;
            navigationPanel.BorderStyle = BorderStyle.FixedSingle;
            navigationPanel.Dock = DockStyle.Top;
            navigationPanel.Location = new Point(0, 28);
            navigationPanel.Margin = new Padding(2);
            navigationPanel.Name = "navigationPanel";
            navigationPanel.Padding = new Padding(10, 8, 10, 8);
            navigationPanel.Size = new Size(1096, 45);
            navigationPanel.TabIndex = 3;
            // 
            // FormMain
            // 
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1096, 613);
            Controls.Add(contentPanel);
            Controls.Add(navigationPanel);
            Controls.Add(statusStrip);
            Controls.Add(menuStrip);
            MainMenuStrip = menuStrip;
            Margin = new Padding(4);
            Name = "FormMain";
            Text = resources.GetString("$this.Text");
            menuStrip.ResumeLayout(false);
            menuStrip.PerformLayout();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            trayContextMenu.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem configurationToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem scheduleManagementToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem backupMonitorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem logBrowserToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem testDatabaseConnectionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel;
        private System.Windows.Forms.Label lblWelcome;
        private System.Windows.Forms.NotifyIcon notifyIcon;
        private System.Windows.Forms.ContextMenuStrip trayContextMenu;
        private System.Windows.Forms.ToolStripMenuItem showToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem exitTrayToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem systemTrayHelpToolStripMenuItem;
        private ToolStripMenuItem transferLogViewToolStripMenuItem;
        private System.Windows.Forms.Panel contentPanel;
        private System.Windows.Forms.Panel navigationPanel;
    }
}