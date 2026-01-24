namespace MySqlBackupTool.Client.Forms
{
    partial class ConfigurationListForm
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
            this.dgvConfigurations = new DataGridView();
            this.btnNew = new Button();
            this.btnEdit = new Button();
            this.btnDelete = new Button();
            this.btnActivate = new Button();
            this.btnDeactivate = new Button();
            this.btnRefresh = new Button();
            this.btnClose = new Button();
            this.lblStatus = new Label();
            this.panelButtons = new Panel();

            ((System.ComponentModel.ISupportInitialize)(this.dgvConfigurations)).BeginInit();
            this.panelButtons.SuspendLayout();
            this.SuspendLayout();

            // 
            // dgvConfigurations
            // 
            this.dgvConfigurations.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvConfigurations.Location = new Point(12, 12);
            this.dgvConfigurations.Size = new Size(760, 450);
            this.dgvConfigurations.TabIndex = 0;
            this.dgvConfigurations.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            // 
            // panelButtons
            // 
            this.panelButtons.Controls.Add(this.btnNew);
            this.panelButtons.Controls.Add(this.btnEdit);
            this.panelButtons.Controls.Add(this.btnDelete);
            this.panelButtons.Controls.Add(this.btnActivate);
            this.panelButtons.Controls.Add(this.btnDeactivate);
            this.panelButtons.Controls.Add(this.btnRefresh);
            this.panelButtons.Controls.Add(this.btnClose);
            this.panelButtons.Location = new Point(12, 480);
            this.panelButtons.Size = new Size(760, 50);
            this.panelButtons.TabIndex = 1;
            this.panelButtons.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            // 
            // btnNew
            // 
            this.btnNew.Location = new Point(0, 10);
            this.btnNew.Size = new Size(80, 30);
            this.btnNew.Text = "New";
            this.btnNew.UseVisualStyleBackColor = true;
            this.btnNew.Click += new EventHandler(this.btnNew_Click);

            // 
            // btnEdit
            // 
            this.btnEdit.Location = new Point(90, 10);
            this.btnEdit.Size = new Size(80, 30);
            this.btnEdit.Text = "Edit";
            this.btnEdit.UseVisualStyleBackColor = true;
            this.btnEdit.Enabled = false;
            this.btnEdit.Click += new EventHandler(this.btnEdit_Click);

            // 
            // btnDelete
            // 
            this.btnDelete.Location = new Point(180, 10);
            this.btnDelete.Size = new Size(80, 30);
            this.btnDelete.Text = "Delete";
            this.btnDelete.UseVisualStyleBackColor = true;
            this.btnDelete.Enabled = false;
            this.btnDelete.Click += new EventHandler(this.btnDelete_Click);

            // 
            // btnActivate
            // 
            this.btnActivate.Location = new Point(280, 10);
            this.btnActivate.Size = new Size(80, 30);
            this.btnActivate.Text = "Activate";
            this.btnActivate.UseVisualStyleBackColor = true;
            this.btnActivate.Enabled = false;
            this.btnActivate.Click += new EventHandler(this.btnActivate_Click);

            // 
            // btnDeactivate
            // 
            this.btnDeactivate.Location = new Point(370, 10);
            this.btnDeactivate.Size = new Size(80, 30);
            this.btnDeactivate.Text = "Deactivate";
            this.btnDeactivate.UseVisualStyleBackColor = true;
            this.btnDeactivate.Enabled = false;
            this.btnDeactivate.Click += new EventHandler(this.btnDeactivate_Click);

            // 
            // btnRefresh
            // 
            this.btnRefresh.Location = new Point(480, 10);
            this.btnRefresh.Size = new Size(80, 30);
            this.btnRefresh.Text = "Refresh";
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Click += new EventHandler(this.btnRefresh_Click);

            // 
            // btnClose
            // 
            this.btnClose.Location = new Point(680, 10);
            this.btnClose.Size = new Size(80, 30);
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnClose.Click += new EventHandler(this.btnClose_Click);

            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new Point(12, 545);
            this.lblStatus.Size = new Size(0, 15);
            this.lblStatus.TabIndex = 2;
            this.lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

            // 
            // ConfigurationListForm
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(784, 571);
            this.Controls.Add(this.dgvConfigurations);
            this.Controls.Add(this.panelButtons);
            this.Controls.Add(this.lblStatus);
            this.Name = "ConfigurationListForm";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Backup Configurations";
            ((System.ComponentModel.ISupportInitialize)(this.dgvConfigurations)).EndInit();
            this.panelButtons.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private DataGridView dgvConfigurations;
        private Button btnNew;
        private Button btnEdit;
        private Button btnDelete;
        private Button btnActivate;
        private Button btnDeactivate;
        private Button btnRefresh;
        private Button btnClose;
        private Label lblStatus;
        private Panel panelButtons;
    }
}