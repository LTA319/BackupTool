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
            this.menuStrip = new MenuStrip();
            this.fileToolStripMenuItem = new ToolStripMenuItem();
            this.exitToolStripMenuItem = new ToolStripMenuItem();
            this.toolsToolStripMenuItem = new ToolStripMenuItem();
            this.configurationToolStripMenuItem = new ToolStripMenuItem();
            this.backupMonitorToolStripMenuItem = new ToolStripMenuItem();
            this.logBrowserToolStripMenuItem = new ToolStripMenuItem();
            this.helpToolStripMenuItem = new ToolStripMenuItem();
            this.aboutToolStripMenuItem = new ToolStripMenuItem();
            this.statusStrip = new StatusStrip();
            this.toolStripStatusLabel = new ToolStripStatusLabel();
            this.lblWelcome = new Label();

            this.menuStrip.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();

            // 
            // menuStrip
            // 
            this.menuStrip.Items.AddRange(new ToolStripItem[] {
                this.fileToolStripMenuItem,
                this.toolsToolStripMenuItem,
                this.helpToolStripMenuItem});
            this.menuStrip.Location = new Point(0, 0);
            this.menuStrip.Size = new Size(800, 24);
            this.menuStrip.TabIndex = 0;
            this.menuStrip.Text = "menuStrip";

            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
                this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Size = new Size(37, 20);
            this.fileToolStripMenuItem.Text = "&File";

            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Size = new Size(93, 22);
            this.exitToolStripMenuItem.Text = "E&xit";
            this.exitToolStripMenuItem.Click += new EventHandler(this.exitToolStripMenuItem_Click);

            // 
            // toolsToolStripMenuItem
            // 
            this.toolsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
                this.configurationToolStripMenuItem,
                this.backupMonitorToolStripMenuItem,
                this.logBrowserToolStripMenuItem});
            this.toolsToolStripMenuItem.Size = new Size(46, 20);
            this.toolsToolStripMenuItem.Text = "&Tools";

            // 
            // configurationToolStripMenuItem
            // 
            this.configurationToolStripMenuItem.Size = new Size(180, 22);
            this.configurationToolStripMenuItem.Text = "&Configuration Management";
            this.configurationToolStripMenuItem.Click += new EventHandler(this.configurationToolStripMenuItem_Click);

            // 
            // backupMonitorToolStripMenuItem
            // 
            this.backupMonitorToolStripMenuItem.Size = new Size(180, 22);
            this.backupMonitorToolStripMenuItem.Text = "&Backup Monitor";
            this.backupMonitorToolStripMenuItem.Click += new EventHandler(this.backupMonitorToolStripMenuItem_Click);

            // 
            // logBrowserToolStripMenuItem
            // 
            this.logBrowserToolStripMenuItem.Size = new Size(180, 22);
            this.logBrowserToolStripMenuItem.Text = "&Log Browser";
            this.logBrowserToolStripMenuItem.Click += new EventHandler(this.logBrowserToolStripMenuItem_Click);

            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
                this.aboutToolStripMenuItem});
            this.helpToolStripMenuItem.Size = new Size(44, 20);
            this.helpToolStripMenuItem.Text = "&Help";

            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Size = new Size(107, 22);
            this.aboutToolStripMenuItem.Text = "&About";
            this.aboutToolStripMenuItem.Click += new EventHandler(this.aboutToolStripMenuItem_Click);

            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new ToolStripItem[] {
                this.toolStripStatusLabel});
            this.statusStrip.Location = new Point(0, 428);
            this.statusStrip.Size = new Size(800, 22);
            this.statusStrip.TabIndex = 1;
            this.statusStrip.Text = "statusStrip";

            // 
            // toolStripStatusLabel
            // 
            this.toolStripStatusLabel.Size = new Size(39, 17);
            this.toolStripStatusLabel.Text = "Ready";

            // 
            // lblWelcome
            // 
            this.lblWelcome.AutoSize = true;
            this.lblWelcome.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            this.lblWelcome.Location = new Point(50, 50);
            this.lblWelcome.Size = new Size(300, 21);
            this.lblWelcome.TabIndex = 2;
            this.lblWelcome.Text = "Welcome to MySQL Backup Tool";

            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(800, 450);
            this.Controls.Add(this.lblWelcome);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.menuStrip);
            this.MainMenuStrip = this.menuStrip;
            this.Name = "FormMain";
            this.Text = "MySQL Backup Tool - Client";
            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private MenuStrip menuStrip;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem exitToolStripMenuItem;
        private ToolStripMenuItem toolsToolStripMenuItem;
        private ToolStripMenuItem configurationToolStripMenuItem;
        private ToolStripMenuItem backupMonitorToolStripMenuItem;
        private ToolStripMenuItem logBrowserToolStripMenuItem;
        private ToolStripMenuItem helpToolStripMenuItem;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel toolStripStatusLabel;
        private Label lblWelcome;
    }
}