namespace MySqlBackupTool.Client.Forms
{
    partial class LogBrowserForm
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
            splitContainer = new SplitContainer();
            dgvLogs = new DataGridView();
            lblFilteredCount = new Label();
            panelFilters = new Panel();
            grpFilters = new GroupBox();
            lblStartDate = new Label();
            dtpStartDate = new DateTimePicker();
            lblEndDate = new Label();
            dtpEndDate = new DateTimePicker();
            lblStatusFilter = new Label();
            cmbStatus = new ComboBox();
            lblConfiguration = new Label();
            cmbConfiguration = new ComboBox();
            lblSearch = new Label();
            txtSearch = new TextBox();
            btnFilter = new Button();
            btnClearFilters = new Button();
            grpLogDetails = new GroupBox();
            txtLogDetails = new TextBox();
            panelDetailsButtons = new Panel();
            btnViewDetails = new Button();
            btnExportLog = new Button();
            panelBottom = new Panel();
            btnGenerateReport = new Button();
            btnRefresh = new Button();
            btnClose = new Button();
            lblStatus = new Label();
            ((System.ComponentModel.ISupportInitialize)splitContainer).BeginInit();
            splitContainer.Panel1.SuspendLayout();
            splitContainer.Panel2.SuspendLayout();
            splitContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvLogs).BeginInit();
            panelFilters.SuspendLayout();
            grpFilters.SuspendLayout();
            grpLogDetails.SuspendLayout();
            panelDetailsButtons.SuspendLayout();
            panelBottom.SuspendLayout();
            SuspendLayout();
            // 
            // splitContainer
            // 
            splitContainer.Dock = DockStyle.Fill;
            splitContainer.Location = new Point(0, 0);
            splitContainer.Margin = new Padding(5, 5, 5, 5);
            splitContainer.Name = "splitContainer";
            splitContainer.Orientation = Orientation.Horizontal;
            // 
            // splitContainer.Panel1
            // 
            splitContainer.Panel1.Controls.Add(dgvLogs);
            splitContainer.Panel1.Controls.Add(lblFilteredCount);
            splitContainer.Panel1.Controls.Add(panelFilters);
            // 
            // splitContainer.Panel2
            // 
            splitContainer.Panel2.Controls.Add(grpLogDetails);
            splitContainer.Size = new Size(1546, 960);
            splitContainer.SplitterDistance = 560;
            splitContainer.SplitterWidth = 6;
            splitContainer.TabIndex = 0;
            // 
            // dgvLogs
            // 
            dgvLogs.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvLogs.Dock = DockStyle.Fill;
            dgvLogs.Location = new Point(0, 192);
            dgvLogs.Margin = new Padding(5, 5, 5, 5);
            dgvLogs.Name = "dgvLogs";
            dgvLogs.RowHeadersWidth = 62;
            dgvLogs.Size = new Size(1546, 328);
            dgvLogs.TabIndex = 1;
            // 
            // lblFilteredCount
            // 
            lblFilteredCount.AutoSize = true;
            lblFilteredCount.Dock = DockStyle.Bottom;
            lblFilteredCount.Location = new Point(0, 520);
            lblFilteredCount.Margin = new Padding(5, 0, 5, 0);
            lblFilteredCount.Name = "lblFilteredCount";
            lblFilteredCount.Padding = new Padding(8, 8, 8, 8);
            lblFilteredCount.Size = new Size(16, 40);
            lblFilteredCount.TabIndex = 2;
            // 
            // panelFilters
            // 
            panelFilters.Controls.Add(grpFilters);
            panelFilters.Dock = DockStyle.Top;
            panelFilters.Location = new Point(0, 0);
            panelFilters.Margin = new Padding(5, 5, 5, 5);
            panelFilters.Name = "panelFilters";
            panelFilters.Size = new Size(1546, 192);
            panelFilters.TabIndex = 0;
            // 
            // grpFilters
            // 
            grpFilters.Controls.Add(lblStartDate);
            grpFilters.Controls.Add(dtpStartDate);
            grpFilters.Controls.Add(lblEndDate);
            grpFilters.Controls.Add(dtpEndDate);
            grpFilters.Controls.Add(lblStatusFilter);
            grpFilters.Controls.Add(cmbStatus);
            grpFilters.Controls.Add(lblConfiguration);
            grpFilters.Controls.Add(cmbConfiguration);
            grpFilters.Controls.Add(lblSearch);
            grpFilters.Controls.Add(txtSearch);
            grpFilters.Controls.Add(btnFilter);
            grpFilters.Controls.Add(btnClearFilters);
            grpFilters.Dock = DockStyle.Fill;
            grpFilters.Location = new Point(0, 0);
            grpFilters.Margin = new Padding(5, 5, 5, 5);
            grpFilters.Name = "grpFilters";
            grpFilters.Padding = new Padding(5, 5, 5, 5);
            grpFilters.Size = new Size(1546, 192);
            grpFilters.TabIndex = 0;
            grpFilters.TabStop = false;
            grpFilters.Text = "Filters";
            // 
            // lblStartDate
            // 
            lblStartDate.AutoSize = true;
            lblStartDate.Location = new Point(24, 40);
            lblStartDate.Margin = new Padding(5, 0, 5, 0);
            lblStartDate.Name = "lblStartDate";
            lblStartDate.Size = new Size(101, 24);
            lblStartDate.TabIndex = 0;
            lblStartDate.Text = "Start Date:";
            // 
            // dtpStartDate
            // 
            dtpStartDate.Format = DateTimePickerFormat.Short;
            dtpStartDate.Location = new Point(24, 69);
            dtpStartDate.Margin = new Padding(5, 5, 5, 5);
            dtpStartDate.Name = "dtpStartDate";
            dtpStartDate.Size = new Size(155, 30);
            dtpStartDate.TabIndex = 1;
            // 
            // lblEndDate
            // 
            lblEndDate.AutoSize = true;
            lblEndDate.Location = new Point(212, 40);
            lblEndDate.Margin = new Padding(5, 0, 5, 0);
            lblEndDate.Name = "lblEndDate";
            lblEndDate.Size = new Size(93, 24);
            lblEndDate.TabIndex = 2;
            lblEndDate.Text = "End Date:";
            // 
            // dtpEndDate
            // 
            dtpEndDate.Format = DateTimePickerFormat.Short;
            dtpEndDate.Location = new Point(212, 69);
            dtpEndDate.Margin = new Padding(5, 5, 5, 5);
            dtpEndDate.Name = "dtpEndDate";
            dtpEndDate.Size = new Size(155, 30);
            dtpEndDate.TabIndex = 3;
            // 
            // lblStatusFilter
            // 
            lblStatusFilter.AutoSize = true;
            lblStatusFilter.Location = new Point(401, 40);
            lblStatusFilter.Margin = new Padding(5, 0, 5, 0);
            lblStatusFilter.Name = "lblStatusFilter";
            lblStatusFilter.Size = new Size(67, 24);
            lblStatusFilter.TabIndex = 4;
            lblStatusFilter.Text = "Status:";
            // 
            // cmbStatus
            // 
            cmbStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbStatus.Location = new Point(401, 69);
            cmbStatus.Margin = new Padding(5, 5, 5, 5);
            cmbStatus.Name = "cmbStatus";
            cmbStatus.Size = new Size(171, 32);
            cmbStatus.TabIndex = 5;
            cmbStatus.SelectedIndexChanged += cmbStatus_SelectedIndexChanged;
            // 
            // lblConfiguration
            // 
            lblConfiguration.AutoSize = true;
            lblConfiguration.Location = new Point(589, 40);
            lblConfiguration.Margin = new Padding(5, 0, 5, 0);
            lblConfiguration.Name = "lblConfiguration";
            lblConfiguration.Size = new Size(133, 24);
            lblConfiguration.TabIndex = 6;
            lblConfiguration.Text = "Configuration:";
            // 
            // cmbConfiguration
            // 
            cmbConfiguration.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbConfiguration.Location = new Point(589, 69);
            cmbConfiguration.Margin = new Padding(5, 5, 5, 5);
            cmbConfiguration.Name = "cmbConfiguration";
            cmbConfiguration.Size = new Size(233, 32);
            cmbConfiguration.TabIndex = 7;
            cmbConfiguration.SelectedIndexChanged += cmbConfiguration_SelectedIndexChanged;
            // 
            // lblSearch
            // 
            lblSearch.AutoSize = true;
            lblSearch.Location = new Point(24, 120);
            lblSearch.Margin = new Padding(5, 0, 5, 0);
            lblSearch.Name = "lblSearch";
            lblSearch.Size = new Size(71, 24);
            lblSearch.TabIndex = 8;
            lblSearch.Text = "Search:";
            // 
            // txtSearch
            // 
            txtSearch.Location = new Point(24, 149);
            txtSearch.Margin = new Padding(5, 5, 5, 5);
            txtSearch.Name = "txtSearch";
            txtSearch.Size = new Size(312, 30);
            txtSearch.TabIndex = 9;
            txtSearch.TextChanged += txtSearch_TextChanged;
            // 
            // btnFilter
            // 
            btnFilter.Location = new Point(369, 149);
            btnFilter.Margin = new Padding(5, 5, 5, 5);
            btnFilter.Name = "btnFilter";
            btnFilter.Size = new Size(126, 37);
            btnFilter.TabIndex = 10;
            btnFilter.Text = "Apply Filter";
            btnFilter.UseVisualStyleBackColor = true;
            btnFilter.Click += btnFilter_Click;
            // 
            // btnClearFilters
            // 
            btnClearFilters.Location = new Point(511, 149);
            btnClearFilters.Margin = new Padding(5, 5, 5, 5);
            btnClearFilters.Name = "btnClearFilters";
            btnClearFilters.Size = new Size(126, 37);
            btnClearFilters.TabIndex = 11;
            btnClearFilters.Text = "Clear Filters";
            btnClearFilters.UseVisualStyleBackColor = true;
            btnClearFilters.Click += btnClearFilters_Click;
            // 
            // grpLogDetails
            // 
            grpLogDetails.Controls.Add(txtLogDetails);
            grpLogDetails.Controls.Add(panelDetailsButtons);
            grpLogDetails.Dock = DockStyle.Fill;
            grpLogDetails.Location = new Point(0, 0);
            grpLogDetails.Margin = new Padding(5, 5, 5, 5);
            grpLogDetails.Name = "grpLogDetails";
            grpLogDetails.Padding = new Padding(5, 5, 5, 5);
            grpLogDetails.Size = new Size(1546, 394);
            grpLogDetails.TabIndex = 0;
            grpLogDetails.TabStop = false;
            grpLogDetails.Text = "Log Details";
            // 
            // txtLogDetails
            // 
            txtLogDetails.Dock = DockStyle.Fill;
            txtLogDetails.Font = new Font("Consolas", 9F);
            txtLogDetails.Location = new Point(5, 28);
            txtLogDetails.Margin = new Padding(5, 5, 5, 5);
            txtLogDetails.Multiline = true;
            txtLogDetails.Name = "txtLogDetails";
            txtLogDetails.ReadOnly = true;
            txtLogDetails.ScrollBars = ScrollBars.Both;
            txtLogDetails.Size = new Size(1536, 297);
            txtLogDetails.TabIndex = 0;
            // 
            // panelDetailsButtons
            // 
            panelDetailsButtons.Controls.Add(btnViewDetails);
            panelDetailsButtons.Controls.Add(btnExportLog);
            panelDetailsButtons.Dock = DockStyle.Bottom;
            panelDetailsButtons.Location = new Point(5, 325);
            panelDetailsButtons.Margin = new Padding(5, 5, 5, 5);
            panelDetailsButtons.Name = "panelDetailsButtons";
            panelDetailsButtons.Size = new Size(1536, 64);
            panelDetailsButtons.TabIndex = 1;
            // 
            // btnViewDetails
            // 
            btnViewDetails.Enabled = false;
            btnViewDetails.Location = new Point(16, 8);
            btnViewDetails.Margin = new Padding(5, 5, 5, 5);
            btnViewDetails.Name = "btnViewDetails";
            btnViewDetails.Size = new Size(157, 48);
            btnViewDetails.TabIndex = 0;
            btnViewDetails.Text = "View Details";
            btnViewDetails.UseVisualStyleBackColor = true;
            btnViewDetails.Click += btnViewDetails_Click;
            // 
            // btnExportLog
            // 
            btnExportLog.Enabled = false;
            btnExportLog.Location = new Point(189, 8);
            btnExportLog.Margin = new Padding(5, 5, 5, 5);
            btnExportLog.Name = "btnExportLog";
            btnExportLog.Size = new Size(126, 48);
            btnExportLog.TabIndex = 1;
            btnExportLog.Text = "Export Log";
            btnExportLog.UseVisualStyleBackColor = true;
            btnExportLog.Click += btnExportLog_Click;
            // 
            // panelBottom
            // 
            panelBottom.Controls.Add(btnGenerateReport);
            panelBottom.Controls.Add(btnRefresh);
            panelBottom.Controls.Add(btnClose);
            panelBottom.Controls.Add(lblStatus);
            panelBottom.Dock = DockStyle.Bottom;
            panelBottom.Location = new Point(0, 960);
            panelBottom.Margin = new Padding(5, 5, 5, 5);
            panelBottom.Name = "panelBottom";
            panelBottom.Size = new Size(1546, 80);
            panelBottom.TabIndex = 1;
            // 
            // btnGenerateReport
            // 
            btnGenerateReport.Location = new Point(24, 16);
            btnGenerateReport.Margin = new Padding(5, 5, 5, 5);
            btnGenerateReport.Name = "btnGenerateReport";
            btnGenerateReport.Size = new Size(173, 48);
            btnGenerateReport.TabIndex = 0;
            btnGenerateReport.Text = "Generate Report";
            btnGenerateReport.UseVisualStyleBackColor = true;
            btnGenerateReport.Click += btnGenerateReport_Click;
            // 
            // btnRefresh
            // 
            btnRefresh.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnRefresh.Location = new Point(1240, 16);
            btnRefresh.Margin = new Padding(5, 5, 5, 5);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(126, 48);
            btnRefresh.TabIndex = 1;
            btnRefresh.Text = "Refresh";
            btnRefresh.UseVisualStyleBackColor = true;
            btnRefresh.Click += btnRefresh_Click;
            // 
            // btnClose
            // 
            btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Location = new Point(1397, 16);
            btnClose.Margin = new Padding(5, 5, 5, 5);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(126, 48);
            btnClose.TabIndex = 2;
            btnClose.Text = "Close";
            btnClose.UseVisualStyleBackColor = true;
            btnClose.Click += btnClose_Click;
            // 
            // lblStatus
            // 
            lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(236, 29);
            lblStatus.Margin = new Padding(5, 0, 5, 0);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(0, 24);
            lblStatus.TabIndex = 3;
            // 
            // LogBrowserForm
            // 
            AutoScaleDimensions = new SizeF(11F, 24F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1546, 1040);
            Controls.Add(splitContainer);
            Controls.Add(panelBottom);
            Margin = new Padding(5, 5, 5, 5);
            Name = "LogBrowserForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Backup Log Browser";
            splitContainer.Panel1.ResumeLayout(false);
            splitContainer.Panel1.PerformLayout();
            splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer).EndInit();
            splitContainer.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvLogs).EndInit();
            panelFilters.ResumeLayout(false);
            grpFilters.ResumeLayout(false);
            grpFilters.PerformLayout();
            grpLogDetails.ResumeLayout(false);
            grpLogDetails.PerformLayout();
            panelDetailsButtons.ResumeLayout(false);
            panelBottom.ResumeLayout(false);
            panelBottom.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.Panel panelFilters;
        private System.Windows.Forms.GroupBox grpFilters;
        private System.Windows.Forms.Label lblStartDate;
        private System.Windows.Forms.DateTimePicker dtpStartDate;
        private System.Windows.Forms.Label lblEndDate;
        private System.Windows.Forms.DateTimePicker dtpEndDate;
        private System.Windows.Forms.Label lblStatusFilter;
        private System.Windows.Forms.ComboBox cmbStatus;
        private System.Windows.Forms.Label lblConfiguration;
        private System.Windows.Forms.ComboBox cmbConfiguration;
        private System.Windows.Forms.Label lblSearch;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.Button btnFilter;
        private System.Windows.Forms.Button btnClearFilters;
        private System.Windows.Forms.DataGridView dgvLogs;
        private System.Windows.Forms.Label lblFilteredCount;
        private System.Windows.Forms.GroupBox grpLogDetails;
        private System.Windows.Forms.TextBox txtLogDetails;
        private System.Windows.Forms.Panel panelDetailsButtons;
        private System.Windows.Forms.Button btnViewDetails;
        private System.Windows.Forms.Button btnExportLog;
        private System.Windows.Forms.Panel panelBottom;
        private System.Windows.Forms.Button btnGenerateReport;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Label lblStatus;
    }
}