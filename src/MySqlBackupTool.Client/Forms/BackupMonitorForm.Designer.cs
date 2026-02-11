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
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.grpConfigurations = new System.Windows.Forms.GroupBox();
            this.dgvConfigurations = new System.Windows.Forms.DataGridView();
            this.panelConfigButtons = new System.Windows.Forms.Panel();
            this.btnStartBackup = new System.Windows.Forms.Button();
            this.grpRunningBackups = new System.Windows.Forms.GroupBox();
            this.dgvRunningBackups = new System.Windows.Forms.DataGridView();
            this.panelBackupButtons = new System.Windows.Forms.Panel();
            this.btnCancelBackup = new System.Windows.Forms.Button();
            this.grpProgress = new System.Windows.Forms.GroupBox();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.lblProgressDetails = new System.Windows.Forms.Label();
            this.panelBottom = new System.Windows.Forms.Panel();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();

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
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.Location = new System.Drawing.Point(0, 0);
            this.splitContainer.Name = "splitContainer";
            this.splitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.splitContainer.Panel1.Controls.Add(this.grpConfigurations);
            this.splitContainer.Panel2.Controls.Add(this.grpRunningBackups);
            this.splitContainer.Size = new System.Drawing.Size(884, 500);
            this.splitContainer.SplitterDistance = 240;
            this.splitContainer.TabIndex = 0;

            // 
            // grpConfigurations
            // 
            this.grpConfigurations.Controls.Add(this.dgvConfigurations);
            this.grpConfigurations.Controls.Add(this.panelConfigButtons);
            this.grpConfigurations.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpConfigurations.Location = new System.Drawing.Point(0, 0);
            this.grpConfigurations.Name = "grpConfigurations";
            this.grpConfigurations.Size = new System.Drawing.Size(884, 240);
            this.grpConfigurations.TabIndex = 0;
            this.grpConfigurations.TabStop = false;
            this.grpConfigurations.Text = "Backup Configurations";

            // 
            // dgvConfigurations
            // 
            this.dgvConfigurations.AllowUserToAddRows = false;
            this.dgvConfigurations.AllowUserToDeleteRows = false;
            this.dgvConfigurations.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvConfigurations.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvConfigurations.Location = new System.Drawing.Point(3, 19);
            this.dgvConfigurations.MultiSelect = false;
            this.dgvConfigurations.Name = "dgvConfigurations";
            this.dgvConfigurations.ReadOnly = true;
            this.dgvConfigurations.RowTemplate.Height = 25;
            this.dgvConfigurations.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvConfigurations.Size = new System.Drawing.Size(878, 178);
            this.dgvConfigurations.TabIndex = 0;

            // 
            // panelConfigButtons
            // 
            this.panelConfigButtons.Controls.Add(this.btnStartBackup);
            this.panelConfigButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelConfigButtons.Location = new System.Drawing.Point(3, 197);
            this.panelConfigButtons.Name = "panelConfigButtons";
            this.panelConfigButtons.Size = new System.Drawing.Size(878, 40);
            this.panelConfigButtons.TabIndex = 1;

            // 
            // btnStartBackup
            // 
            this.btnStartBackup.Enabled = false;
            this.btnStartBackup.Location = new System.Drawing.Point(10, 5);
            this.btnStartBackup.Name = "btnStartBackup";
            this.btnStartBackup.Size = new System.Drawing.Size(100, 30);
            this.btnStartBackup.TabIndex = 0;
            this.btnStartBackup.Text = "Start Backup";
            this.btnStartBackup.UseVisualStyleBackColor = true;
            this.btnStartBackup.Click += new System.EventHandler(this.btnStartBackup_Click);

            // 
            // grpRunningBackups
            // 
            this.grpRunningBackups.Controls.Add(this.dgvRunningBackups);
            this.grpRunningBackups.Controls.Add(this.panelBackupButtons);
            this.grpRunningBackups.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpRunningBackups.Location = new System.Drawing.Point(0, 0);
            this.grpRunningBackups.Name = "grpRunningBackups";
            this.grpRunningBackups.Size = new System.Drawing.Size(884, 256);
            this.grpRunningBackups.TabIndex = 0;
            this.grpRunningBackups.TabStop = false;
            this.grpRunningBackups.Text = "Running Backups";

            // 
            // dgvRunningBackups
            // 
            this.dgvRunningBackups.AllowUserToAddRows = false;
            this.dgvRunningBackups.AllowUserToDeleteRows = false;
            this.dgvRunningBackups.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvRunningBackups.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvRunningBackups.Location = new System.Drawing.Point(3, 19);
            this.dgvRunningBackups.MultiSelect = false;
            this.dgvRunningBackups.Name = "dgvRunningBackups";
            this.dgvRunningBackups.ReadOnly = true;
            this.dgvRunningBackups.RowTemplate.Height = 25;
            this.dgvRunningBackups.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvRunningBackups.Size = new System.Drawing.Size(878, 194);
            this.dgvRunningBackups.TabIndex = 0;

            // 
            // panelBackupButtons
            // 
            this.panelBackupButtons.Controls.Add(this.btnCancelBackup);
            this.panelBackupButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelBackupButtons.Location = new System.Drawing.Point(3, 213);
            this.panelBackupButtons.Name = "panelBackupButtons";
            this.panelBackupButtons.Size = new System.Drawing.Size(878, 40);
            this.panelBackupButtons.TabIndex = 1;

            // 
            // btnCancelBackup
            // 
            this.btnCancelBackup.Enabled = false;
            this.btnCancelBackup.Location = new System.Drawing.Point(10, 5);
            this.btnCancelBackup.Name = "btnCancelBackup";
            this.btnCancelBackup.Size = new System.Drawing.Size(100, 30);
            this.btnCancelBackup.TabIndex = 0;
            this.btnCancelBackup.Text = "Cancel Backup";
            this.btnCancelBackup.UseVisualStyleBackColor = true;
            this.btnCancelBackup.Click += new System.EventHandler(this.btnCancelBackup_Click);

            // 
            // grpProgress
            // 
            this.grpProgress.Controls.Add(this.progressBar);
            this.grpProgress.Controls.Add(this.lblProgressDetails);
            this.grpProgress.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.grpProgress.Location = new System.Drawing.Point(0, 500);
            this.grpProgress.Name = "grpProgress";
            this.grpProgress.Size = new System.Drawing.Size(884, 80);
            this.grpProgress.TabIndex = 1;
            this.grpProgress.TabStop = false;
            this.grpProgress.Text = "Backup Progress";

            // 
            // progressBar
            // 
            this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar.Location = new System.Drawing.Point(15, 25);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(854, 23);
            this.progressBar.TabIndex = 0;

            // 
            // lblProgressDetails
            // 
            this.lblProgressDetails.AutoSize = true;
            this.lblProgressDetails.Location = new System.Drawing.Point(15, 55);
            this.lblProgressDetails.Name = "lblProgressDetails";
            this.lblProgressDetails.Size = new System.Drawing.Size(0, 15);
            this.lblProgressDetails.TabIndex = 1;

            // 
            // panelBottom
            // 
            this.panelBottom.Controls.Add(this.btnRefresh);
            this.panelBottom.Controls.Add(this.btnClose);
            this.panelBottom.Controls.Add(this.lblStatus);
            this.panelBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelBottom.Location = new System.Drawing.Point(0, 580);
            this.panelBottom.Name = "panelBottom";
            this.panelBottom.Size = new System.Drawing.Size(884, 50);
            this.panelBottom.TabIndex = 2;

            // 
            // btnRefresh
            // 
            this.btnRefresh.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRefresh.Location = new System.Drawing.Point(693, 10);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(80, 30);
            this.btnRefresh.TabIndex = 0;
            this.btnRefresh.Text = "Refresh";
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);

            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Location = new System.Drawing.Point(789, 10);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(80, 30);
            this.btnClose.TabIndex = 1;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);

            // 
            // lblStatus
            // 
            this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(15, 18);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(0, 15);
            this.lblStatus.TabIndex = 2;

            // 
            // BackupMonitorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(884, 630);
            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.grpProgress);
            this.Controls.Add(this.panelBottom);
            this.Name = "BackupMonitorForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
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

        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.GroupBox grpConfigurations;
        private System.Windows.Forms.DataGridView dgvConfigurations;
        private System.Windows.Forms.Panel panelConfigButtons;
        private System.Windows.Forms.Button btnStartBackup;
        private System.Windows.Forms.GroupBox grpRunningBackups;
        private System.Windows.Forms.DataGridView dgvRunningBackups;
        private System.Windows.Forms.Panel panelBackupButtons;
        private System.Windows.Forms.Button btnCancelBackup;
        private System.Windows.Forms.GroupBox grpProgress;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label lblProgressDetails;
        private System.Windows.Forms.Panel panelBottom;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Label lblStatus;
    }
}