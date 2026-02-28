namespace MySqlBackupTool.Client.EmbeddedForms
{
    partial class WelcomeControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            panelMain = new Panel();
            panelCenter = new Panel();
            labelRecentActivity = new Label();
            labelQuickActions = new Label();
            labelWelcomeMessage = new Label();
            labelTitle = new Label();
            panelMain.SuspendLayout();
            panelCenter.SuspendLayout();
            SuspendLayout();
            // 
            // panelMain
            // 
            panelMain.BackColor = SystemColors.Control;
            panelMain.Controls.Add(panelCenter);
            panelMain.Dock = DockStyle.Fill;
            panelMain.Location = new Point(0, 0);
            panelMain.Margin = new Padding(4);
            panelMain.Name = "panelMain";
            panelMain.Padding = new Padding(26, 27, 26, 27);
            panelMain.Size = new Size(1029, 800);
            panelMain.TabIndex = 0;
            // 
            // panelCenter
            // 
            panelCenter.Anchor = AnchorStyles.None;
            panelCenter.Controls.Add(labelRecentActivity);
            panelCenter.Controls.Add(labelQuickActions);
            panelCenter.Controls.Add(labelWelcomeMessage);
            panelCenter.Controls.Add(labelTitle);
            panelCenter.Location = new Point(193, 211);
            panelCenter.Margin = new Padding(4);
            panelCenter.Name = "panelCenter";
            panelCenter.Size = new Size(643, 455);
            panelCenter.TabIndex = 0;
            // 
            // labelRecentActivity
            // 
            labelRecentActivity.AutoSize = true;
            labelRecentActivity.Font = new Font("Segoe UI", 9F);
            labelRecentActivity.ForeColor = SystemColors.ControlDarkDark;
            labelRecentActivity.Location = new Point(26, 333);
            labelRecentActivity.Margin = new Padding(4, 0, 4, 0);
            labelRecentActivity.MaximumSize = new Size(591, 0);
            labelRecentActivity.Name = "labelRecentActivity";
            labelRecentActivity.Size = new Size(242, 60);
            labelRecentActivity.TabIndex = 3;
            labelRecentActivity.Text = "Recent Activity:\r\n• No recent backups\r\n• Use the Tools menu to get started";
            // 
            // labelQuickActions
            // 
            labelQuickActions.AutoSize = true;
            labelQuickActions.Font = new Font("Segoe UI", 9F);
            labelQuickActions.Location = new Point(26, 200);
            labelQuickActions.Margin = new Padding(4, 0, 4, 0);
            labelQuickActions.MaximumSize = new Size(591, 0);
            labelQuickActions.Name = "labelQuickActions";
            labelQuickActions.Size = new Size(477, 80);
            labelQuickActions.TabIndex = 2;
            labelQuickActions.Text = "Quick Actions:\r\n• Tools → Configuration Management - Manage backup configurations\r\n• Tools → Schedule Management - Set up automated backups\r\n• Tools → Backup Monitor - Monitor backup progress";
            // 
            // labelWelcomeMessage
            // 
            labelWelcomeMessage.AutoSize = true;
            labelWelcomeMessage.Font = new Font("Segoe UI", 10F);
            labelWelcomeMessage.Location = new Point(26, 107);
            labelWelcomeMessage.Margin = new Padding(4, 0, 4, 0);
            labelWelcomeMessage.MaximumSize = new Size(591, 0);
            labelWelcomeMessage.Name = "labelWelcomeMessage";
            labelWelcomeMessage.Size = new Size(337, 46);
            labelWelcomeMessage.TabIndex = 1;
            labelWelcomeMessage.Text = "Welcome to MySQL Backup Tool!\r\nSelect a tool from the menu to get started.";
            // 
            // labelTitle
            // 
            labelTitle.AutoSize = true;
            labelTitle.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            labelTitle.Location = new Point(26, 27);
            labelTitle.Margin = new Padding(4, 0, 4, 0);
            labelTitle.MaximumSize = new Size(591, 0);
            labelTitle.Name = "labelTitle";
            labelTitle.Size = new Size(297, 41);
            labelTitle.TabIndex = 0;
            labelTitle.Text = "MySQL Backup Tool";
            // 
            // WelcomeControl
            // 
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(panelMain);
            Margin = new Padding(4);
            Name = "WelcomeControl";
            Size = new Size(1029, 800);
            panelMain.ResumeLayout(false);
            panelCenter.ResumeLayout(false);
            panelCenter.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel panelMain;
        private System.Windows.Forms.Panel panelCenter;
        private System.Windows.Forms.Label labelTitle;
        private System.Windows.Forms.Label labelWelcomeMessage;
        private System.Windows.Forms.Label labelQuickActions;
        private System.Windows.Forms.Label labelRecentActivity;
    }
}
