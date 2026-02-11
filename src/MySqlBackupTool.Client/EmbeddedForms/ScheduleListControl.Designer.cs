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
            dgvSchedules.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvSchedules.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvSchedules.Location = new Point(19, 19);
            dgvSchedules.Margin = new Padding(5, 5, 5, 5);
            dgvSchedules.Name = "dgvSchedules";
            dgvSchedules.RowHeadersWidth = 62;
            dgvSchedules.Size = new Size(1519, 521);
            dgvSchedules.TabIndex = 0;
            // 
            // btnNew
            // 
            btnNew.Location = new Point(0, 16);
            btnNew.Margin = new Padding(5, 5, 5, 5);
            btnNew.Name = "btnNew";
            btnNew.Size = new Size(126, 48);
            btnNew.TabIndex = 0;
            btnNew.Text = "新建";
            btnNew.UseVisualStyleBackColor = true;
            btnNew.Click += btnNew_Click;
            // 
            // btnEdit
            // 
            btnEdit.Enabled = false;
            btnEdit.Location = new Point(141, 16);
            btnEdit.Margin = new Padding(5, 5, 5, 5);
            btnEdit.Name = "btnEdit";
            btnEdit.Size = new Size(126, 48);
            btnEdit.TabIndex = 1;
            btnEdit.Text = "编辑";
            btnEdit.UseVisualStyleBackColor = true;
            btnEdit.Click += btnEdit_Click;
            // 
            // btnDelete
            // 
            btnDelete.Enabled = false;
            btnDelete.Location = new Point(283, 16);
            btnDelete.Margin = new Padding(5, 5, 5, 5);
            btnDelete.Name = "btnDelete";
            btnDelete.Size = new Size(126, 48);
            btnDelete.TabIndex = 2;
            btnDelete.Text = "删除";
            btnDelete.UseVisualStyleBackColor = true;
            btnDelete.Click += btnDelete_Click;
            // 
            // btnEnable
            // 
            btnEnable.Enabled = false;
            btnEnable.Location = new Point(440, 16);
            btnEnable.Margin = new Padding(5, 5, 5, 5);
            btnEnable.Name = "btnEnable";
            btnEnable.Size = new Size(126, 48);
            btnEnable.TabIndex = 3;
            btnEnable.Text = "启用";
            btnEnable.UseVisualStyleBackColor = true;
            btnEnable.Click += btnEnable_Click;
            // 
            // btnDisable
            // 
            btnDisable.Enabled = false;
            btnDisable.Location = new Point(581, 16);
            btnDisable.Margin = new Padding(5, 5, 5, 5);
            btnDisable.Name = "btnDisable";
            btnDisable.Size = new Size(126, 48);
            btnDisable.TabIndex = 4;
            btnDisable.Text = "禁用";
            btnDisable.UseVisualStyleBackColor = true;
            btnDisable.Click += btnDisable_Click;
            // 
            // btnRefresh
            // 
            btnRefresh.Location = new Point(754, 16);
            btnRefresh.Margin = new Padding(5, 5, 5, 5);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(126, 48);
            btnRefresh.TabIndex = 5;
            btnRefresh.Text = "刷新";
            btnRefresh.UseVisualStyleBackColor = true;
            btnRefresh.Click += btnRefresh_Click;
            // 
            // btnClose
            // 
            btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Location = new Point(1394, 16);
            btnClose.Margin = new Padding(5, 5, 5, 5);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(126, 48);
            btnClose.TabIndex = 6;
            btnClose.Text = "关闭";
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
            panelButtons.Controls.Add(btnEnable);
            panelButtons.Controls.Add(btnDisable);
            panelButtons.Controls.Add(btnRefresh);
            panelButtons.Controls.Add(btnClose);
            panelButtons.Location = new Point(19, 569);
            panelButtons.Margin = new Padding(5, 5, 5, 5);
            panelButtons.Name = "panelButtons";
            panelButtons.Size = new Size(1519, 80);
            panelButtons.TabIndex = 1;
            // 
            // ScheduleListControl
            // 
            AutoScaleDimensions = new SizeF(11F, 24F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(dgvSchedules);
            Controls.Add(panelButtons);
            Controls.Add(lblStatus);
            Margin = new Padding(5, 5, 5, 5);
            Name = "ScheduleListControl";
            Size = new Size(1557, 715);
            ((System.ComponentModel.ISupportInitialize)dgvSchedules).EndInit();
            panelButtons.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();

            // Apply standard styling and optimize layout performance
            EmbeddedFormStyleManager.ApplyStandardStyling(this);
            EmbeddedFormStyleManager.OptimizeLayoutPerformance(this);
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
    }
}
