namespace MySqlBackupTool.Client.EmbeddedForms
{
    partial class ScheduleListControl
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
            dgvSchedules = new DataGridView();
            colId = new DataGridViewTextBoxColumn();
            colBackupConfigName = new DataGridViewTextBoxColumn();
            colScheduleType = new DataGridViewTextBoxColumn();
            colScheduleTime = new DataGridViewTextBoxColumn();
            colIsEnabled = new DataGridViewCheckBoxColumn();
            colLastExecuted = new DataGridViewTextBoxColumn();
            colNextExecution = new DataGridViewTextBoxColumn();
            btnNew = new Button();
            btnEdit = new Button();
            btnDelete = new Button();
            btnEnable = new Button();
            btnDisable = new Button();
            btnRefresh = new Button();
            btnClose = new Button();
            lblStatus = new Label();
            panelButtons = new Panel();
            ((System.ComponentModel.ISupportInitialize)dgvSchedules).BeginInit();
            panelButtons.SuspendLayout();
            SuspendLayout();
            // 
            // dgvSchedules
            // 
            dgvSchedules.AllowUserToAddRows = false;
            dgvSchedules.AllowUserToDeleteRows = false;
            dgvSchedules.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvSchedules.AutoGenerateColumns = false;
            dgvSchedules.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvSchedules.Columns.AddRange(new DataGridViewColumn[] { 
                colId, 
                colBackupConfigName, 
                colScheduleType, 
                colScheduleTime, 
                colIsEnabled, 
                colLastExecuted, 
                colNextExecution 
            });
            dgvSchedules.Location = new Point(16, 16);
            dgvSchedules.Margin = new Padding(4, 4, 4, 4);
            dgvSchedules.MultiSelect = false;
            dgvSchedules.Name = "dgvSchedules";
            dgvSchedules.ReadOnly = true;
            dgvSchedules.RowHeadersWidth = 62;
            dgvSchedules.RowTemplate.Height = 25;
            dgvSchedules.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvSchedules.Size = new Size(1243, 434);
            dgvSchedules.TabIndex = 0;
            dgvSchedules.CellFormatting += DgvSchedules_CellFormatting;
            dgvSchedules.SelectionChanged += DgvSchedules_SelectionChanged;
            // 
            // colId
            // 
            colId.DataPropertyName = "Id";
            colId.HeaderText = "ID";
            colId.MinimumWidth = 6;
            colId.Name = "Id";
            colId.ReadOnly = true;
            colId.Width = 60;
            // 
            // colBackupConfigName
            // 
            colBackupConfigName.DataPropertyName = "BackupConfigName";
            colBackupConfigName.HeaderText = "备份配置";
            colBackupConfigName.MinimumWidth = 6;
            colBackupConfigName.Name = "BackupConfigName";
            colBackupConfigName.ReadOnly = true;
            colBackupConfigName.Width = 200;
            // 
            // colScheduleType
            // 
            colScheduleType.DataPropertyName = "ScheduleType";
            colScheduleType.HeaderText = "调度类型";
            colScheduleType.MinimumWidth = 6;
            colScheduleType.Name = "ScheduleType";
            colScheduleType.ReadOnly = true;
            colScheduleType.Width = 100;
            // 
            // colScheduleTime
            // 
            colScheduleTime.DataPropertyName = "ScheduleTime";
            colScheduleTime.HeaderText = "调度时间";
            colScheduleTime.MinimumWidth = 6;
            colScheduleTime.Name = "ScheduleTime";
            colScheduleTime.ReadOnly = true;
            colScheduleTime.Width = 100;
            // 
            // colIsEnabled
            // 
            colIsEnabled.DataPropertyName = "IsEnabled";
            colIsEnabled.HeaderText = "启用";
            colIsEnabled.MinimumWidth = 6;
            colIsEnabled.Name = "IsEnabled";
            colIsEnabled.ReadOnly = true;
            colIsEnabled.Width = 60;
            // 
            // colLastExecuted
            // 
            colLastExecuted.DataPropertyName = "LastExecuted";
            colLastExecuted.HeaderText = "上次执行";
            colLastExecuted.MinimumWidth = 6;
            colLastExecuted.Name = "LastExecuted";
            colLastExecuted.ReadOnly = true;
            colLastExecuted.Width = 150;
            // 
            // colNextExecution
            // 
            colNextExecution.DataPropertyName = "NextExecution";
            colNextExecution.HeaderText = "下次执行";
            colNextExecution.MinimumWidth = 6;
            colNextExecution.Name = "NextExecution";
            colNextExecution.ReadOnly = true;
            colNextExecution.Width = 150;
            // 
            // btnNew
            // 
            btnNew.Location = new Point(0, 13);
            btnNew.Margin = new Padding(4, 4, 4, 4);
            btnNew.Name = "btnNew";
            btnNew.Size = new Size(103, 40);
            btnNew.TabIndex = 0;
            btnNew.Text = "New";
            btnNew.UseVisualStyleBackColor = true;
            btnNew.Click += btnNew_Click;
            // 
            // btnEdit
            // 
            btnEdit.Enabled = false;
            btnEdit.Location = new Point(115, 13);
            btnEdit.Margin = new Padding(4, 4, 4, 4);
            btnEdit.Name = "btnEdit";
            btnEdit.Size = new Size(103, 40);
            btnEdit.TabIndex = 1;
            btnEdit.Text = "Edit";
            btnEdit.UseVisualStyleBackColor = true;
            btnEdit.Click += btnEdit_Click;
            // 
            // btnDelete
            // 
            btnDelete.Enabled = false;
            btnDelete.Location = new Point(232, 13);
            btnDelete.Margin = new Padding(4, 4, 4, 4);
            btnDelete.Name = "btnDelete";
            btnDelete.Size = new Size(103, 40);
            btnDelete.TabIndex = 2;
            btnDelete.Text = "Delete";
            btnDelete.UseVisualStyleBackColor = true;
            btnDelete.Click += btnDelete_Click;
            // 
            // btnEnable
            // 
            btnEnable.Enabled = false;
            btnEnable.Location = new Point(360, 13);
            btnEnable.Margin = new Padding(4, 4, 4, 4);
            btnEnable.Name = "btnEnable";
            btnEnable.Size = new Size(103, 40);
            btnEnable.TabIndex = 3;
            btnEnable.Text = "Activate";
            btnEnable.UseVisualStyleBackColor = true;
            btnEnable.Click += btnEnable_Click;
            // 
            // btnDisable
            // 
            btnDisable.Enabled = false;
            btnDisable.Location = new Point(475, 13);
            btnDisable.Margin = new Padding(4, 4, 4, 4);
            btnDisable.Name = "btnDisable";
            btnDisable.Size = new Size(103, 40);
            btnDisable.TabIndex = 4;
            btnDisable.Text = "Deactivate";
            btnDisable.UseVisualStyleBackColor = true;
            btnDisable.Click += btnDisable_Click;
            // 
            // btnRefresh
            // 
            btnRefresh.Location = new Point(617, 13);
            btnRefresh.Margin = new Padding(4, 4, 4, 4);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(103, 40);
            btnRefresh.TabIndex = 5;
            btnRefresh.Text = "Refresh";
            btnRefresh.UseVisualStyleBackColor = true;
            btnRefresh.Click += btnRefresh_Click;
            // 
            // btnClose
            // 
            btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Location = new Point(1141, 13);
            btnClose.Margin = new Padding(4, 4, 4, 4);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(103, 40);
            btnClose.TabIndex = 6;
            btnClose.Text = "Close";
            btnClose.UseVisualStyleBackColor = true;
            btnClose.Click += btnClose_Click;
            // 
            // lblStatus
            // 
            lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(16, 561);
            lblStatus.Margin = new Padding(4, 0, 4, 0);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(0, 20);
            lblStatus.TabIndex = 2;
            // 
            // panelButtons
            // 
            panelButtons.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            panelButtons.Controls.Add(btnNew);
            panelButtons.Controls.Add(btnEdit);
            panelButtons.Controls.Add(btnDelete);
            panelButtons.Controls.Add(btnEnable);
            panelButtons.Controls.Add(btnDisable);
            panelButtons.Controls.Add(btnRefresh);
            panelButtons.Controls.Add(btnClose);
            panelButtons.Location = new Point(16, 474);
            panelButtons.Margin = new Padding(4, 4, 4, 4);
            panelButtons.Name = "panelButtons";
            panelButtons.Size = new Size(1243, 67);
            panelButtons.TabIndex = 1;
            // 
            // ScheduleListControl
            // 
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(dgvSchedules);
            Controls.Add(panelButtons);
            Controls.Add(lblStatus);
            Margin = new Padding(4, 4, 4, 4);
            Name = "ScheduleListControl";
            Size = new Size(1274, 596);
            ((System.ComponentModel.ISupportInitialize)dgvSchedules).EndInit();
            panelButtons.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();

            // Apply DataGridView styling
            //EmbeddedFormStyleManager.ApplyDataGridViewStyling(dgvSchedules);
        }

        #endregion

        private DataGridView dgvSchedules;
        private Button btnNew;
        private Button btnEdit;
        private Button btnDelete;
        private Button btnEnable;
        private Button btnDisable;
        private Button btnRefresh;
        private Button btnClose;
        private Label lblStatus;
        private Panel panelButtons;
        private DataGridViewTextBoxColumn colId;
        private DataGridViewTextBoxColumn colBackupConfigName;
        private DataGridViewTextBoxColumn colScheduleType;
        private DataGridViewTextBoxColumn colScheduleTime;
        private DataGridViewCheckBoxColumn colIsEnabled;
        private DataGridViewTextBoxColumn colLastExecuted;
        private DataGridViewTextBoxColumn colNextExecution;
    }
}
