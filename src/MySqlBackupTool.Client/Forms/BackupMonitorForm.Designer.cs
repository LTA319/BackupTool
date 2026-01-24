namespace MySqlBackupTool.Client.Forms
{
    partial class BackupMonitorForm
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
            this.splitContainer = new SplitContainer();
            this.grpConfigurations = new GroupBox();
            this.dgvConfigurations = new DataGridView();
            this.panelConfigButtons = new Panel();
            this.btnStartBackup = new Button();
            this.grpRunningBackups = new GroupBox();
            this.dgvRunningBackups = new DataGridView();
            this.panelBackupButtons = new Panel();
            this.btnCancelBackup = new Button();
            this.grpProgress = new GroupBox();
            this.progressBar = new ProgressBar();
            this.lblProgressDetails = new Label();
            this.panelBottom = new Panel();
            this.btnRefresh = new Button();
            this.btnClose = new Button();
            this.lblStatus = new Label();

            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.grpConfigurations.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvConfigurations)).BeginInit();
            this.panelConfigButtons.SuspendLayout();
            this.grpRunningBackups.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvRunningBackups)).BeginInit();
            this.panelBackupButtons.SuspendLayout();
            this.grpProgress.SuspendLayout();
            this.panelBottom.SuspendLayout();
            this.SuspendLayout();

            // 
            // splitContainer
            // 
            this.splitContainer.Dock = DockStyle.Fill;
            this.splitContainer.Location = new Point(0, 0);
            this.splitContainer.Orientation = Orientation.Horizontal;
            this.splitContainer.Panel1.Controls.Add(this.grpConfigurations);
            this.splitContainer.Panel2.Controls.Add(this.grpRunningBackups);
            this.splitContainer.Size = new Size(884, 500);
            this.splitContainer.SplitterDistance = 240;
            this.splitContainer.TabIndex = 0;

            // 
            // grpConfigurations
            // 
            this.grpConfigurations.Controls.Add(this.dgvConfigurations);
            this.grpConfigurations.Controls.Add(this.panelConfigButtons);
            this.grpConfigurations.Dock = DockStyle.Fill;
            this.grpConfigurations.Location = new Point(0, 0);
            this.grpConfigurations.Size = new Size(884, 240);
            this.grpConfigurations.TabIndex = 0;
            this.grpConfigurations.TabStop = false;
            this.grpConfigurations.Text = "Backup Configurations";

            // 
            // dgvConfigurations
            // 
            this.dgvConfigurations.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvConfigurations.Dock = DockStyle.Fill;
            this.dgvConfigurations.Location = new Point(3, 19);
            this.dgvConfigurations.Size = new Size(878, 178);
            this.dgvConfigurations.TabIndex = 0;

            // 
            // panelConfigButtons
            // 
            this.panelConfigButtons.Controls.Add(this.btnStartBackup);
            this.panelConfigButtons.Dock = DockStyle.Bottom;
            this.panelConfigButtons.Location = new Point(3, 197);
            this.panelConfigButtons.Size = new Size(878, 40);
            this.panelConfigButtons.TabIndex = 1;

            // 
            // btnStartBackup
            // 
            this.btnStartBackup.Location = new Point(10, 5);
            this.btnStartBackup.Size = new Size(100, 30);
            this.btnStartBackup.Text = "Start Backup";
            this.btnStartBackup.UseVisualStyleBackColor = true;
            this.btnStartBackup.Enabled = false;
            this.btnStartBackup.Click += new EventHandler(this.btnStartBackup_Click);

            // 
            // grpRunningBackups
            // 
            this.grpRunningBackups.Controls.Add(this.dgvRunningBackups);
            this.grpRunningBackups.Controls.Add(this.panelBackupButtons);
            this.grpRunningBackups.Dock = DockStyle.Fill;
            this.grpRunningBackups.Location = new Point(0, 0);
            this.grpRunningBackups.Size = new Size(884, 256);
            this.grpRunningBackups.TabIndex = 0;
            this.grpRunningBackups.TabStop = false;
            this.grpRunningBackups.Text = "Running Backups";

            // 
            // dgvRunningBackups
            // 
            this.dgvRunningBackups.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvRunningBackups.Dock = DockStyle.Fill;
            this.dgvRunningBackups.Location = new Point(3, 19);
            this.dgvRunningBackups.Size = new Size(878, 194);
            this.dgvRunningBackups.TabIndex = 0;

            // 
            // panelBackupButtons
            // 
            this.panelBackupButtons.Controls.Add(this.btnCancelBackup);
            this.panelBackupButtons.Dock = DockStyle.Bottom;
            this.panelBackupButtons.Location = new Point(3, 213);
            this.panelBackupButtons.Size = new Size(878, 40);
            this.panelBackupButtons.TabIndex = 1;

            // 
            // btnCancelBackup
            // 
            this.btnCancelBackup.Location = new Point(10, 5);
            this.btnCancelBackup.Size = new Size(100, 30);
            this.btnCancelBackup.Text = "Cancel Backup";
            this.btnCancelBackup.UseVisualStyleBackColor = true;
            this.btnCancelBackup.Enabled = false;
            this.btnCancelBackup.Click += new EventHandler(this.btnCancelBackup_Click);

            // 
            // grpProgress
            // 
            this.grpProgress.Controls.Add(this.progressBar);
            this.grpProgress.Controls.Add(this.lblProgressDetails);
            this.grpProgress.Dock = DockStyle.Bottom;
            this.grpProgress.Location = new Point(0, 500);
            this.grpProgress.Size = new Size(884, 80);
            this.grpProgress.TabIndex = 1;
            this.grpProgress.TabStop = false;
            this.grpProgress.Text = "Backup Progress";

            // 
            // progressBar
            // 
            this.progressBar.Location = new Point(15, 25);
            this.progressBar.Size = new Size(854, 23);
            this.progressBar.TabIndex = 0;
            this.progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            // 
            // lblProgressDetails
            // 
            this.lblProgressDetails.AutoSize = true;
            this.lblProgressDetails.Location = new Point(15, 55);
            this.lblProgressDetails.Size = new Size(0, 15);
            this.lblProgressDetails.TabIndex = 1;

            // 
            // panelBottom
            // 
            this.panelBottom.Controls.Add(this.btnRefresh);
            this.panelBottom.Controls.Add(this.btnClose);
            this.panelBottom.Controls.Add(this.lblStatus);
            this.panelBottom.Dock = DockStyle.Bottom;
            this.panelBottom.Location = new Point(0, 580);
            this.panelBottom.Size = new Size(884, 50);
            this.panelBottom.TabIndex = 2;

            // 
            // btnRefresh
            // 
            this.btnRefresh.Location = new Point(693, 10);
            this.btnRefresh.Size = new Size(80, 30);
            this.btnRefresh.Text = "Refresh";
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnRefresh.Click += new EventHandler(this.btnRefresh_Click);

            // 
            // btnClose
            // 
            this.btnClose.Location = new Point(789, 10);
            this.btnClose.Size = new Size(80, 30);
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnClose.Click += new EventHandler(this.btnClose_Click);

            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new Point(15, 18);
            this.lblStatus.Size = new Size(0, 15);
            this.lblStatus.TabIndex = 0;
            this.lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

            // 
            // BackupMonitorForm
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(884, 630);
            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.grpProgress);
            this.Controls.Add(this.panelBottom);
            this.Name = "BackupMonitorForm";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Backup Monitor & Control";
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.grpConfigurations.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvConfigurations)).EndInit();
            this.panelConfigButtons.ResumeLayout(false);
            this.grpRunningBackups.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvRunningBackups)).EndInit();
            this.panelBackupButtons.ResumeLayout(false);
            this.grpProgress.ResumeLayout(false);
            this.grpProgress.PerformLayout();
            this.panelBottom.ResumeLayout(false);
            this.panelBottom.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private SplitContainer splitContainer;
        private GroupBox grpConfigurations;
        private DataGridView dgvConfigurations;
        private Panel panelConfigButtons;
        private Button btnStartBackup;
        private GroupBox grpRunningBackups;
        private DataGridView dgvRunningBackups;
        private Panel panelBackupButtons;
        private Button btnCancelBackup;
        private GroupBox grpProgress;
        private ProgressBar progressBar;
        private Label lblProgressDetails;
        private Panel panelBottom;
        private Button btnRefresh;
        private Button btnClose;
        private Label lblStatus;
    }
}