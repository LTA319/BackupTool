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
            this.panelMain = new System.Windows.Forms.Panel();
            this.panelCenter = new System.Windows.Forms.Panel();
            this.labelRecentActivity = new System.Windows.Forms.Label();
            this.labelQuickActions = new System.Windows.Forms.Label();
            this.labelWelcomeMessage = new System.Windows.Forms.Label();
            this.labelTitle = new System.Windows.Forms.Label();
            this.panelMain.SuspendLayout();
            this.panelCenter.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelMain
            // 
            this.panelMain.BackColor = System.Drawing.SystemColors.Control;
            this.panelMain.Controls.Add(this.panelCenter);
            this.panelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelMain.Location = new System.Drawing.Point(0, 0);
            this.panelMain.Name = "panelMain";
            this.panelMain.Padding = new System.Windows.Forms.Padding(20);
            this.panelMain.Size = new System.Drawing.Size(800, 600);
            this.panelMain.TabIndex = 0;
            // 
            // panelCenter
            // 
            this.panelCenter.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.panelCenter.Controls.Add(this.labelRecentActivity);
            this.panelCenter.Controls.Add(this.labelQuickActions);
            this.panelCenter.Controls.Add(this.labelWelcomeMessage);
            this.panelCenter.Controls.Add(this.labelTitle);
            this.panelCenter.Location = new System.Drawing.Point(150, 100);
            this.panelCenter.Name = "panelCenter";
            this.panelCenter.Size = new System.Drawing.Size(500, 400);
            this.panelCenter.TabIndex = 0;
            // 
            // labelRecentActivity
            // 
            this.labelRecentActivity.AutoSize = true;
            this.labelRecentActivity.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.labelRecentActivity.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.labelRecentActivity.Location = new System.Drawing.Point(20, 250);
            this.labelRecentActivity.Name = "labelRecentActivity";
            this.labelRecentActivity.Size = new System.Drawing.Size(460, 60);
            this.labelRecentActivity.TabIndex = 3;
            this.labelRecentActivity.Text = "Recent Activity:\r\n• No recent backups\r\n• Use the Tools menu to get started";
            // 
            // labelQuickActions
            // 
            this.labelQuickActions.AutoSize = true;
            this.labelQuickActions.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.labelQuickActions.Location = new System.Drawing.Point(20, 150);
            this.labelQuickActions.Name = "labelQuickActions";
            this.labelQuickActions.Size = new System.Drawing.Size(460, 80);
            this.labelQuickActions.TabIndex = 2;
            this.labelQuickActions.Text = "Quick Actions:\r\n• Tools → Configuration Management - Manage backup configurations\r\n• Tools → Schedule Management - Set up automated backups\r\n• Tools → Backup Monitor - Monitor backup progress";
            // 
            // labelWelcomeMessage
            // 
            this.labelWelcomeMessage.AutoSize = true;
            this.labelWelcomeMessage.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.labelWelcomeMessage.Location = new System.Drawing.Point(20, 80);
            this.labelWelcomeMessage.Name = "labelWelcomeMessage";
            this.labelWelcomeMessage.Size = new System.Drawing.Size(460, 38);
            this.labelWelcomeMessage.TabIndex = 1;
            this.labelWelcomeMessage.Text = "Welcome to MySQL Backup Tool!\r\nSelect a tool from the menu to get started.";
            // 
            // labelTitle
            // 
            this.labelTitle.AutoSize = true;
            this.labelTitle.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.labelTitle.Location = new System.Drawing.Point(20, 20);
            this.labelTitle.Name = "labelTitle";
            this.labelTitle.Size = new System.Drawing.Size(310, 32);
            this.labelTitle.TabIndex = 0;
            this.labelTitle.Text = "MySQL Backup Tool";
            // 
            // WelcomeControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panelMain);
            this.Name = "WelcomeControl";
            this.Size = new System.Drawing.Size(800, 600);
            this.panelMain.ResumeLayout(false);
            this.panelCenter.ResumeLayout(false);
            this.panelCenter.PerformLayout();
            this.ResumeLayout(false);
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
