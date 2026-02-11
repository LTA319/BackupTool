namespace MySqlBackupTool.Client.EmbeddedForms
{
    partial class ConfigurationListControl
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
            dgvConfigurations = new DataGridView();
            colName = new DataGridViewTextBoxColumn();
            colMySQLHost = new DataGridViewTextBoxColumn();
            colTargetServer = new DataGridViewTextBoxColumn();
            colIsActive = new DataGridViewCheckBoxColumn();
            colCreatedAt = new DataGridViewTextBoxColumn();
            btnNew = new Button();
            btnEdit = new Button();
            btnDelete = new Button();
            btnActivate = new Button();
            btnDeactivate = new Button();
            btnRefresh = new Button();
            btnClose = new Button();
            lblStatus = new Label();
            panelButtons = new Panel();
            ((System.ComponentModel.ISupportInitialize)dgvConfigurations).BeginInit();
            panelButtons.SuspendLayout();
            SuspendLayout();
            // 
            // dgvConfigurations
            // 
            dgvConfigurations.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvConfigurations.AutoGenerateColumns = false;
            dgvConfigurations.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvConfigurations.Columns.AddRange(new DataGridViewColumn[] {
            colName,
            colMySQLHost,
            colTargetServer,
            colIsActive,
            colCreatedAt});
            dgvConfigurations.Location = new Point(19, 19);
            dgvConfigurations.Margin = new Padding(5);
            dgvConfigurations.MultiSelect = false;
            dgvConfigurations.Name = "dgvConfigurations";
            dgvConfigurations.ReadOnly = true;
            dgvConfigurations.RowHeadersWidth = 62;
            dgvConfigurations.RowTemplate.Height = 25;
            dgvConfigurations.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvConfigurations.Size = new Size(1419, 521);
            dgvConfigurations.TabIndex = 0;
            dgvConfigurations.AllowUserToAddRows = false;
            dgvConfigurations.AllowUserToDeleteRows = false;
            dgvConfigurations.CellFormatting += new DataGridViewCellFormattingEventHandler(this.DgvConfigurations_CellFormatting);
            dgvConfigurations.SelectionChanged += new EventHandler(this.DgvConfigurations_SelectionChanged);
            // 
            // colName
            // 
            colName.Name = "Name";
            colName.HeaderText = "配置名称";
            colName.DataPropertyName = "Name";
            colName.Width = 200;
            colName.ReadOnly = true;
            // 
            // colMySQLHost
            // 
            colMySQLHost.Name = "MySQLHost";
            colMySQLHost.HeaderText = "MySQL主机";
            colMySQLHost.DataPropertyName = "MySQLConnection.Host";
            colMySQLHost.Width = 120;
            colMySQLHost.ReadOnly = true;
            // 
            // colTargetServer
            // 
            colTargetServer.Name = "TargetServer";
            colTargetServer.HeaderText = "目标服务器";
            colTargetServer.DataPropertyName = "TargetServer.IPAddress";
            colTargetServer.Width = 120;
            colTargetServer.ReadOnly = true;
            // 
            // colIsActive
            // 
            colIsActive.Name = "IsActive";
            colIsActive.HeaderText = "激活";
            colIsActive.DataPropertyName = "IsActive";
            colIsActive.Width = 60;
            colIsActive.ReadOnly = true;
            // 
            // colCreatedAt
            // 
            colCreatedAt.Name = "CreatedAt";
            colCreatedAt.HeaderText = "创建时间";
            colCreatedAt.DataPropertyName = "CreatedAt";
            colCreatedAt.Width = 120;
            colCreatedAt.DefaultCellStyle.Format = "yyyy-MM-dd HH:mm";
            colCreatedAt.ReadOnly = true;
            // 
            // btnNew
            // 
            btnNew.Location = new Point(0, 16);
            btnNew.Margin = new Padding(5);
            btnNew.Name = "btnNew";
            btnNew.Size = new Size(126, 48);
            btnNew.TabIndex = 0;
            btnNew.Text = "New";
            btnNew.UseVisualStyleBackColor = true;
            btnNew.Click += btnNew_Click;
            // 
            // btnEdit
            // 
            btnEdit.Enabled = false;
            btnEdit.Location = new Point(141, 16);
            btnEdit.Margin = new Padding(5);
            btnEdit.Name = "btnEdit";
            btnEdit.Size = new Size(126, 48);
            btnEdit.TabIndex = 1;
            btnEdit.Text = "Edit";
            btnEdit.UseVisualStyleBackColor = true;
            btnEdit.Click += btnEdit_Click;
            // 
            // btnDelete
            // 
            btnDelete.Enabled = false;
            btnDelete.Location = new Point(283, 16);
            btnDelete.Margin = new Padding(5);
            btnDelete.Name = "btnDelete";
            btnDelete.Size = new Size(126, 48);
            btnDelete.TabIndex = 2;
            btnDelete.Text = "Delete";
            btnDelete.UseVisualStyleBackColor = true;
            btnDelete.Click += btnDelete_Click;
            // 
            // btnActivate
            // 
            btnActivate.Enabled = false;
            btnActivate.Location = new Point(440, 16);
            btnActivate.Margin = new Padding(5);
            btnActivate.Name = "btnActivate";
            btnActivate.Size = new Size(126, 48);
            btnActivate.TabIndex = 3;
            btnActivate.Text = "Activate";
            btnActivate.UseVisualStyleBackColor = true;
            btnActivate.Click += btnActivate_Click;
            // 
            // btnDeactivate
            // 
            btnDeactivate.Enabled = false;
            btnDeactivate.Location = new Point(581, 16);
            btnDeactivate.Margin = new Padding(5);
            btnDeactivate.Name = "btnDeactivate";
            btnDeactivate.Size = new Size(126, 48);
            btnDeactivate.TabIndex = 4;
            btnDeactivate.Text = "Deactivate";
            btnDeactivate.UseVisualStyleBackColor = true;
            btnDeactivate.Click += btnDeactivate_Click;
            // 
            // btnRefresh
            // 
            btnRefresh.Location = new Point(754, 16);
            btnRefresh.Margin = new Padding(5);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(126, 48);
            btnRefresh.TabIndex = 5;
            btnRefresh.Text = "Refresh";
            btnRefresh.UseVisualStyleBackColor = true;
            btnRefresh.Click += btnRefresh_Click;
            // 
            // btnClose
            // 
            btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Location = new Point(1294, 16);
            btnClose.Margin = new Padding(5);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(126, 48);
            btnClose.TabIndex = 6;
            btnClose.Text = "Close";
            btnClose.UseVisualStyleBackColor = true;
            btnClose.Click += btnClose_Click;
            // 
            // lblStatus
            // 
            lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(19, 673);
            lblStatus.Margin = new Padding(5, 0, 5, 0);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(0, 24);
            lblStatus.TabIndex = 2;
            // 
            // panelButtons
            // 
            panelButtons.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            panelButtons.Controls.Add(btnNew);
            panelButtons.Controls.Add(btnEdit);
            panelButtons.Controls.Add(btnDelete);
            panelButtons.Controls.Add(btnActivate);
            panelButtons.Controls.Add(btnDeactivate);
            panelButtons.Controls.Add(btnRefresh);
            panelButtons.Controls.Add(btnClose);
            panelButtons.Location = new Point(19, 569);
            panelButtons.Margin = new Padding(5);
            panelButtons.Name = "panelButtons";
            panelButtons.Size = new Size(1419, 80);
            panelButtons.TabIndex = 1;
            // 
            // ConfigurationListControl
            // 
            AutoScaleDimensions = new SizeF(11F, 24F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(dgvConfigurations);
            Controls.Add(panelButtons);
            Controls.Add(lblStatus);
            Margin = new Padding(5);
            Name = "ConfigurationListControl";
            Size = new Size(1457, 715);
            ((System.ComponentModel.ISupportInitialize)dgvConfigurations).EndInit();
            panelButtons.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();

            // Apply standard styling and optimize layout performance
            EmbeddedFormStyleManager.ApplyStandardStyling(this);
            EmbeddedFormStyleManager.OptimizeLayoutPerformance(this);
            
            // Apply DataGridView styling
            //EmbeddedFormStyleManager.ApplyDataGridViewStyling(dgvConfigurations);
        }

        #endregion

        private System.Windows.Forms.DataGridView dgvConfigurations;
        private System.Windows.Forms.DataGridViewTextBoxColumn colName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMySQLHost;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTargetServer;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colIsActive;
        private System.Windows.Forms.DataGridViewTextBoxColumn colCreatedAt;
        private System.Windows.Forms.Button btnNew;
        private System.Windows.Forms.Button btnEdit;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Button btnActivate;
        private System.Windows.Forms.Button btnDeactivate;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Panel panelButtons;
    }
}
