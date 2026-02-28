namespace MySqlBackupTool.Client.EmbeddedForms
{
    partial class BackupMonitorControl
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            splitContainer = new SplitContainer();
            grpConfigurations = new GroupBox();
            dgvConfigurations = new DataGridView();
            panelConfigButtons = new Panel();
            btnStartBackup = new Button();
            dataGridViewTextBoxColumn1 = new DataGridViewTextBoxColumn();
            dataGridViewCheckBoxColumn1 = new DataGridViewCheckBoxColumn();
            dataGridViewTextBoxColumn2 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn3 = new DataGridViewTextBoxColumn();
            grpRunningBackups = new GroupBox();
            dgvRunningBackups = new DataGridView();
            panelBackupButtons = new Panel();
            btnCancelBackup = new Button();
            grpProgress = new GroupBox();
            progressBar = new ProgressBar();
            lblProgressDetails = new Label();
            panelBottom = new Panel();
            btnRefresh = new Button();
            lblStatus = new Label();
            dataGridViewTextBoxColumn4 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn5 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn6 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn7 = new DataGridViewTextBoxColumn();
            dataGridViewTextBoxColumn8 = new DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)splitContainer).BeginInit();
            splitContainer.Panel1.SuspendLayout();
            splitContainer.Panel2.SuspendLayout();
            splitContainer.SuspendLayout();
            grpConfigurations.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvConfigurations).BeginInit();
            panelConfigButtons.SuspendLayout();
            grpRunningBackups.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvRunningBackups).BeginInit();
            panelBackupButtons.SuspendLayout();
            grpProgress.SuspendLayout();
            panelBottom.SuspendLayout();
            SuspendLayout();
            // 
            // splitContainer
            // 
            splitContainer.Dock = DockStyle.Fill;
            splitContainer.Location = new Point(0, 0);
            splitContainer.Margin = new Padding(4, 4, 4, 4);
            splitContainer.Name = "splitContainer";
            splitContainer.Orientation = Orientation.Horizontal;
            // 
            // splitContainer.Panel1
            // 
            splitContainer.Panel1.Controls.Add(grpConfigurations);
            // 
            // splitContainer.Panel2
            // 
            splitContainer.Panel2.Controls.Add(grpRunningBackups);
            splitContainer.Size = new Size(1137, 666);
            splitContainer.SplitterDistance = 319;
            splitContainer.SplitterWidth = 5;
            splitContainer.TabIndex = 0;
            // 
            // grpConfigurations
            // 
            grpConfigurations.Controls.Add(dgvConfigurations);
            grpConfigurations.Controls.Add(panelConfigButtons);
            grpConfigurations.Dock = DockStyle.Fill;
            grpConfigurations.Location = new Point(0, 0);
            grpConfigurations.Margin = new Padding(4, 4, 4, 4);
            grpConfigurations.Name = "grpConfigurations";
            grpConfigurations.Padding = new Padding(4, 4, 4, 4);
            grpConfigurations.Size = new Size(1137, 319);
            grpConfigurations.TabIndex = 0;
            grpConfigurations.TabStop = false;
            grpConfigurations.Text = "Backup Configurations";
            // 
            // dgvConfigurations
            // 
            dgvConfigurations.AllowUserToAddRows = false;
            dgvConfigurations.AllowUserToDeleteRows = false;
            dgvConfigurations.AutoGenerateColumns = false;
            dgvConfigurations.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvConfigurations.Columns.AddRange(new DataGridViewColumn[] { dataGridViewTextBoxColumn1, dataGridViewCheckBoxColumn1, dataGridViewTextBoxColumn2, dataGridViewTextBoxColumn3 });
            dgvConfigurations.Dock = DockStyle.Fill;
            dgvConfigurations.Location = new Point(4, 24);
            dgvConfigurations.Margin = new Padding(4, 4, 4, 4);
            dgvConfigurations.MultiSelect = false;
            dgvConfigurations.Name = "dgvConfigurations";
            dgvConfigurations.ReadOnly = true;
            dgvConfigurations.RowHeadersWidth = 51;
            dgvConfigurations.RowTemplate.Height = 25;
            dgvConfigurations.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvConfigurations.Size = new Size(1129, 238);
            dgvConfigurations.TabIndex = 0;
            dgvConfigurations.CellFormatting += DgvConfigurations_CellFormatting;
            dgvConfigurations.SelectionChanged += DgvConfigurations_SelectionChanged;
            // 
            // panelConfigButtons
            // 
            panelConfigButtons.Controls.Add(btnStartBackup);
            panelConfigButtons.Dock = DockStyle.Bottom;
            panelConfigButtons.Location = new Point(4, 262);
            panelConfigButtons.Margin = new Padding(4, 4, 4, 4);
            panelConfigButtons.Name = "panelConfigButtons";
            panelConfigButtons.Size = new Size(1129, 53);
            panelConfigButtons.TabIndex = 1;
            // 
            // btnStartBackup
            // 
            btnStartBackup.Enabled = false;
            btnStartBackup.Location = new Point(13, 7);
            btnStartBackup.Margin = new Padding(4, 4, 4, 4);
            btnStartBackup.Name = "btnStartBackup";
            btnStartBackup.Size = new Size(129, 40);
            btnStartBackup.TabIndex = 0;
            btnStartBackup.Text = "Start Backup";
            btnStartBackup.UseVisualStyleBackColor = true;
            btnStartBackup.Click += btnStartBackup_Click;
            // 
            // dataGridViewTextBoxColumn1
            // 
            dataGridViewTextBoxColumn1.DataPropertyName = "Name";
            dataGridViewTextBoxColumn1.HeaderText = "配置名称";
            dataGridViewTextBoxColumn1.MinimumWidth = 6;
            dataGridViewTextBoxColumn1.Name = "Name";
            dataGridViewTextBoxColumn1.ReadOnly = true;
            dataGridViewTextBoxColumn1.Width = 200;
            // 
            // dataGridViewCheckBoxColumn1
            // 
            dataGridViewCheckBoxColumn1.DataPropertyName = "IsActive";
            dataGridViewCheckBoxColumn1.HeaderText = "激活";
            dataGridViewCheckBoxColumn1.MinimumWidth = 6;
            dataGridViewCheckBoxColumn1.Name = "IsActive";
            dataGridViewCheckBoxColumn1.ReadOnly = true;
            dataGridViewCheckBoxColumn1.Width = 60;
            // 
            // dataGridViewTextBoxColumn2
            // 
            dataGridViewTextBoxColumn2.DataPropertyName = "MySQLHost";
            dataGridViewTextBoxColumn2.HeaderText = "MySQL主机";
            dataGridViewTextBoxColumn2.MinimumWidth = 6;
            dataGridViewTextBoxColumn2.Name = "MySQLHost";
            dataGridViewTextBoxColumn2.ReadOnly = true;
            dataGridViewTextBoxColumn2.Width = 150;
            // 
            // dataGridViewTextBoxColumn3
            // 
            dataGridViewTextBoxColumn3.DataPropertyName = "TargetServer";
            dataGridViewTextBoxColumn3.HeaderText = "目标服务器";
            dataGridViewTextBoxColumn3.MinimumWidth = 6;
            dataGridViewTextBoxColumn3.Name = "TargetServer";
            dataGridViewTextBoxColumn3.ReadOnly = true;
            dataGridViewTextBoxColumn3.Width = 150;
            // 
            // grpRunningBackups
            // 
            grpRunningBackups.Controls.Add(dgvRunningBackups);
            grpRunningBackups.Controls.Add(panelBackupButtons);
            grpRunningBackups.Dock = DockStyle.Fill;
            grpRunningBackups.Location = new Point(0, 0);
            grpRunningBackups.Margin = new Padding(4, 4, 4, 4);
            grpRunningBackups.Name = "grpRunningBackups";
            grpRunningBackups.Padding = new Padding(4, 4, 4, 4);
            grpRunningBackups.Size = new Size(1137, 342);
            grpRunningBackups.TabIndex = 0;
            grpRunningBackups.TabStop = false;
            grpRunningBackups.Text = "Running Backups";
            // 
            // dgvRunningBackups
            // 
            dgvRunningBackups.AllowUserToAddRows = false;
            dgvRunningBackups.AllowUserToDeleteRows = false;
            dgvRunningBackups.AutoGenerateColumns = false;
            dgvRunningBackups.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvRunningBackups.Columns.AddRange(new DataGridViewColumn[] { dataGridViewTextBoxColumn4, dataGridViewTextBoxColumn5, dataGridViewTextBoxColumn6, dataGridViewTextBoxColumn7, dataGridViewTextBoxColumn8 });
            dgvRunningBackups.Dock = DockStyle.Fill;
            dgvRunningBackups.Location = new Point(4, 24);
            dgvRunningBackups.Margin = new Padding(4, 4, 4, 4);
            dgvRunningBackups.MultiSelect = false;
            dgvRunningBackups.Name = "dgvRunningBackups";
            dgvRunningBackups.ReadOnly = true;
            dgvRunningBackups.RowHeadersWidth = 51;
            dgvRunningBackups.RowTemplate.Height = 25;
            dgvRunningBackups.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvRunningBackups.Size = new Size(1129, 261);
            dgvRunningBackups.TabIndex = 0;
            dgvRunningBackups.CellFormatting += DgvRunningBackups_CellFormatting;
            dgvRunningBackups.SelectionChanged += DgvRunningBackups_SelectionChanged;
            // 
            // panelBackupButtons
            // 
            panelBackupButtons.Controls.Add(btnCancelBackup);
            panelBackupButtons.Dock = DockStyle.Bottom;
            panelBackupButtons.Location = new Point(4, 285);
            panelBackupButtons.Margin = new Padding(4, 4, 4, 4);
            panelBackupButtons.Name = "panelBackupButtons";
            panelBackupButtons.Size = new Size(1129, 53);
            panelBackupButtons.TabIndex = 1;
            // 
            // btnCancelBackup
            // 
            btnCancelBackup.Enabled = false;
            btnCancelBackup.Location = new Point(13, 7);
            btnCancelBackup.Margin = new Padding(4, 4, 4, 4);
            btnCancelBackup.Name = "btnCancelBackup";
            btnCancelBackup.Size = new Size(129, 40);
            btnCancelBackup.TabIndex = 0;
            btnCancelBackup.Text = "Cancel Backup";
            btnCancelBackup.UseVisualStyleBackColor = true;
            btnCancelBackup.Click += btnCancelBackup_Click;
            // 
            // grpProgress
            // 
            grpProgress.Controls.Add(progressBar);
            grpProgress.Controls.Add(lblProgressDetails);
            grpProgress.Dock = DockStyle.Bottom;
            grpProgress.Location = new Point(0, 666);
            grpProgress.Margin = new Padding(4, 4, 4, 4);
            grpProgress.Name = "grpProgress";
            grpProgress.Padding = new Padding(4, 4, 4, 4);
            grpProgress.Size = new Size(1137, 107);
            grpProgress.TabIndex = 1;
            grpProgress.TabStop = false;
            grpProgress.Text = "Backup Progress";
            // 
            // progressBar
            // 
            progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            progressBar.Location = new Point(19, 33);
            progressBar.Margin = new Padding(4, 4, 4, 4);
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(1098, 31);
            progressBar.TabIndex = 0;
            // 
            // lblProgressDetails
            // 
            lblProgressDetails.AutoSize = true;
            lblProgressDetails.Location = new Point(19, 73);
            lblProgressDetails.Margin = new Padding(4, 0, 4, 0);
            lblProgressDetails.Name = "lblProgressDetails";
            lblProgressDetails.Size = new Size(0, 20);
            lblProgressDetails.TabIndex = 1;
            // 
            // panelBottom
            // 
            panelBottom.Controls.Add(btnRefresh);
            panelBottom.Controls.Add(lblStatus);
            panelBottom.Dock = DockStyle.Bottom;
            panelBottom.Location = new Point(0, 773);
            panelBottom.Margin = new Padding(4, 4, 4, 4);
            panelBottom.Name = "panelBottom";
            panelBottom.Size = new Size(1137, 67);
            panelBottom.TabIndex = 2;
            // 
            // btnRefresh
            // 
            btnRefresh.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnRefresh.Location = new Point(1014, 13);
            btnRefresh.Margin = new Padding(4, 4, 4, 4);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(103, 40);
            btnRefresh.TabIndex = 0;
            btnRefresh.Text = "Refresh";
            btnRefresh.UseVisualStyleBackColor = true;
            btnRefresh.Click += btnRefresh_Click;
            // 
            // lblStatus
            // 
            lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(19, 24);
            lblStatus.Margin = new Padding(4, 0, 4, 0);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(0, 20);
            lblStatus.TabIndex = 1;
            // 
            // dataGridViewTextBoxColumn4
            // 
            dataGridViewTextBoxColumn4.DataPropertyName = "ConfigName";
            dataGridViewTextBoxColumn4.HeaderText = "配置名称";
            dataGridViewTextBoxColumn4.MinimumWidth = 6;
            dataGridViewTextBoxColumn4.Name = "ConfigName";
            dataGridViewTextBoxColumn4.ReadOnly = true;
            dataGridViewTextBoxColumn4.Width = 200;
            // 
            // dataGridViewTextBoxColumn5
            // 
            dataGridViewTextBoxColumn5.DataPropertyName = "Status";
            dataGridViewTextBoxColumn5.HeaderText = "状态";
            dataGridViewTextBoxColumn5.MinimumWidth = 6;
            dataGridViewTextBoxColumn5.Name = "Status";
            dataGridViewTextBoxColumn5.ReadOnly = true;
            dataGridViewTextBoxColumn5.Width = 100;
            // 
            // dataGridViewTextBoxColumn6
            // 
            dataGridViewTextBoxColumn6.DataPropertyName = "StartTime";
            dataGridViewTextBoxColumn6.HeaderText = "开始时间";
            dataGridViewTextBoxColumn6.MinimumWidth = 6;
            dataGridViewTextBoxColumn6.Name = "StartTime";
            dataGridViewTextBoxColumn6.ReadOnly = true;
            dataGridViewTextBoxColumn6.Width = 150;
            // 
            // dataGridViewTextBoxColumn7
            // 
            dataGridViewTextBoxColumn7.DataPropertyName = "Duration";
            dataGridViewTextBoxColumn7.HeaderText = "持续时间";
            dataGridViewTextBoxColumn7.MinimumWidth = 6;
            dataGridViewTextBoxColumn7.Name = "Duration";
            dataGridViewTextBoxColumn7.ReadOnly = true;
            dataGridViewTextBoxColumn7.Width = 100;
            // 
            // dataGridViewTextBoxColumn8
            // 
            dataGridViewTextBoxColumn8.DataPropertyName = "Progress";
            dataGridViewTextBoxColumn8.HeaderText = "进度";
            dataGridViewTextBoxColumn8.MinimumWidth = 6;
            dataGridViewTextBoxColumn8.Name = "Progress";
            dataGridViewTextBoxColumn8.ReadOnly = true;
            dataGridViewTextBoxColumn8.Width = 100;
            // 
            // BackupMonitorControl
            // 
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(splitContainer);
            Controls.Add(grpProgress);
            Controls.Add(panelBottom);
            Margin = new Padding(4, 4, 4, 4);
            Name = "BackupMonitorControl";
            Size = new Size(1137, 840);
            splitContainer.Panel1.ResumeLayout(false);
            splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer).EndInit();
            splitContainer.ResumeLayout(false);
            grpConfigurations.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvConfigurations).EndInit();
            panelConfigButtons.ResumeLayout(false);
            grpRunningBackups.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvRunningBackups).EndInit();
            panelBackupButtons.ResumeLayout(false);
            grpProgress.ResumeLayout(false);
            grpProgress.PerformLayout();
            panelBottom.ResumeLayout(false);
            panelBottom.PerformLayout();
            ResumeLayout(false);

            // Apply DataGridView styling
            //EmbeddedFormStyleManager.ApplyDataGridViewStyling(this.dgvConfigurations);
            //EmbeddedFormStyleManager.ApplyDataGridViewStyling(this.dgvRunningBackups);
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
        private System.Windows.Forms.Label lblStatus;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
        private DataGridViewCheckBoxColumn dataGridViewCheckBoxColumn1;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn2;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn3;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn4;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn5;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn6;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn7;
        private DataGridViewTextBoxColumn dataGridViewTextBoxColumn8;
    }
}
