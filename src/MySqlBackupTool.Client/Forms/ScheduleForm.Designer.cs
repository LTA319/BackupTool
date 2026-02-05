namespace MySqlBackupTool.Client.Forms
{
    partial class ScheduleForm
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
            lblBackupConfig = new Label();
            cmbBackupConfig = new ComboBox();
            lblScheduleType = new Label();
            cmbScheduleType = new ComboBox();
            lblScheduleTime = new Label();
            txtScheduleTime = new TextBox();
            lblTimeHint = new Label();
            chkEnabled = new CheckBox();
            btnOK = new Button();
            btnCancel = new Button();
            SuspendLayout();
            // 
            // lblBackupConfig
            // 
            lblBackupConfig.AutoSize = true;
            lblBackupConfig.Location = new Point(30, 30);
            lblBackupConfig.Name = "lblBackupConfig";
            lblBackupConfig.Size = new Size(82, 24);
            lblBackupConfig.TabIndex = 0;
            lblBackupConfig.Text = "备份配置:";
            // 
            // cmbBackupConfig
            // 
            cmbBackupConfig.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbBackupConfig.FormattingEnabled = true;
            cmbBackupConfig.Location = new Point(130, 27);
            cmbBackupConfig.Name = "cmbBackupConfig";
            cmbBackupConfig.Size = new Size(300, 32);
            cmbBackupConfig.TabIndex = 1;
            // 
            // lblScheduleType
            // 
            lblScheduleType.AutoSize = true;
            lblScheduleType.Location = new Point(30, 80);
            lblScheduleType.Name = "lblScheduleType";
            lblScheduleType.Size = new Size(82, 24);
            lblScheduleType.TabIndex = 2;
            lblScheduleType.Text = "调度类型:";
            // 
            // cmbScheduleType
            // 
            cmbScheduleType.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbScheduleType.FormattingEnabled = true;
            cmbScheduleType.Location = new Point(130, 77);
            cmbScheduleType.Name = "cmbScheduleType";
            cmbScheduleType.Size = new Size(150, 32);
            cmbScheduleType.TabIndex = 3;
            // 
            // lblScheduleTime
            // 
            lblScheduleTime.AutoSize = true;
            lblScheduleTime.Location = new Point(30, 130);
            lblScheduleTime.Name = "lblScheduleTime";
            lblScheduleTime.Size = new Size(82, 24);
            lblScheduleTime.TabIndex = 4;
            lblScheduleTime.Text = "调度时间:";
            // 
            // txtScheduleTime
            // 
            txtScheduleTime.Location = new Point(130, 127);
            txtScheduleTime.Name = "txtScheduleTime";
            txtScheduleTime.Size = new Size(200, 30);
            txtScheduleTime.TabIndex = 5;
            // 
            // lblTimeHint
            // 
            lblTimeHint.AutoSize = true;
            lblTimeHint.ForeColor = Color.Gray;
            lblTimeHint.Location = new Point(130, 165);
            lblTimeHint.Name = "lblTimeHint";
            lblTimeHint.Size = new Size(0, 24);
            lblTimeHint.TabIndex = 6;
            // 
            // chkEnabled
            // 
            chkEnabled.AutoSize = true;
            chkEnabled.Location = new Point(130, 210);
            chkEnabled.Name = "chkEnabled";
            chkEnabled.Size = new Size(72, 28);
            chkEnabled.TabIndex = 7;
            chkEnabled.Text = "启用";
            chkEnabled.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            btnOK.Location = new Point(230, 280);
            btnOK.Name = "btnOK";
            btnOK.Size = new Size(100, 40);
            btnOK.TabIndex = 8;
            btnOK.Text = "确定";
            btnOK.UseVisualStyleBackColor = true;
            btnOK.Click += btnOK_Click;
            // 
            // btnCancel
            // 
            btnCancel.Location = new Point(350, 280);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(100, 40);
            btnCancel.TabIndex = 9;
            btnCancel.Text = "取消";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;
            // 
            // ScheduleForm
            // 
            AutoScaleDimensions = new SizeF(11F, 24F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(484, 361);
            Controls.Add(btnCancel);
            Controls.Add(btnOK);
            Controls.Add(chkEnabled);
            Controls.Add(lblTimeHint);
            Controls.Add(txtScheduleTime);
            Controls.Add(lblScheduleTime);
            Controls.Add(cmbScheduleType);
            Controls.Add(lblScheduleType);
            Controls.Add(cmbBackupConfig);
            Controls.Add(lblBackupConfig);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "ScheduleForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "调度配置";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lblBackupConfig;
        private ComboBox cmbBackupConfig;
        private Label lblScheduleType;
        private ComboBox cmbScheduleType;
        private Label lblScheduleTime;
        private TextBox txtScheduleTime;
        private Label lblTimeHint;
        private CheckBox chkEnabled;
        private Button btnOK;
        private Button btnCancel;
    }
}