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
            this.splitContainer = new SplitContainer();
            this.panelFilters = new Panel();
            this.grpFilters = new GroupBox();
            this.lblStartDate = new Label();
            this.dtpStartDate = new DateTimePicker();
            this.lblEndDate = new Label();
            this.dtpEndDate = new DateTimePicker();
            this.lblStatusFilter = new Label();
            this.cmbStatus = new ComboBox();
            this.lblConfiguration = new Label();
            this.cmbConfiguration = new ComboBox();
            this.lblSearch = new Label();
            this.txtSearch = new TextBox();
            this.btnFilter = new Button();
            this.btnClearFilters = new Button();
            this.dgvLogs = new DataGridView();
            this.lblFilteredCount = new Label();
            this.grpLogDetails = new GroupBox();
            this.txtLogDetails = new TextBox();
            this.panelDetailsButtons = new Panel();
            this.btnViewDetails = new Button();
            this.btnExportLog = new Button();
            this.panelBottom = new Panel();
            this.btnGenerateReport = new Button();
            this.btnRefresh = new Button();
            this.btnClose = new Button();
            this.lblStatus = new Label();

            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.panelFilters.SuspendLayout();
            this.grpFilters.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvLogs)).BeginInit();
            this.grpLogDetails.SuspendLayout();
            this.panelDetailsButtons.SuspendLayout();
            this.panelBottom.SuspendLayout();
            this.SuspendLayout();

            // 
            // splitContainer
            // 
            this.splitContainer.Dock = DockStyle.Fill;
            this.splitContainer.Location = new Point(0, 0);
            this.splitContainer.Orientation = Orientation.Horizontal;
            this.splitContainer.Panel1.Controls.Add(this.dgvLogs);
            this.splitContainer.Panel1.Controls.Add(this.lblFilteredCount);
            this.splitContainer.Panel1.Controls.Add(this.panelFilters);
            this.splitContainer.Panel2.Controls.Add(this.grpLogDetails);
            this.splitContainer.Size = new Size(984, 600);
            this.splitContainer.SplitterDistance = 350;
            this.splitContainer.TabIndex = 0;

            // 
            // panelFilters
            // 
            this.panelFilters.Controls.Add(this.grpFilters);
            this.panelFilters.Dock = DockStyle.Top;
            this.panelFilters.Location = new Point(0, 0);
            this.panelFilters.Size = new Size(984, 120);
            this.panelFilters.TabIndex = 0;

            // 
            // grpFilters
            // 
            this.grpFilters.Controls.Add(this.lblStartDate);
            this.grpFilters.Controls.Add(this.dtpStartDate);
            this.grpFilters.Controls.Add(this.lblEndDate);
            this.grpFilters.Controls.Add(this.dtpEndDate);
            this.grpFilters.Controls.Add(this.lblStatusFilter);
            this.grpFilters.Controls.Add(this.cmbStatus);
            this.grpFilters.Controls.Add(this.lblConfiguration);
            this.grpFilters.Controls.Add(this.cmbConfiguration);
            this.grpFilters.Controls.Add(this.lblSearch);
            this.grpFilters.Controls.Add(this.txtSearch);
            this.grpFilters.Controls.Add(this.btnFilter);
            this.grpFilters.Controls.Add(this.btnClearFilters);
            this.grpFilters.Dock = DockStyle.Fill;
            this.grpFilters.Location = new Point(0, 0);
            this.grpFilters.Size = new Size(984, 120);
            this.grpFilters.TabIndex = 0;
            this.grpFilters.TabStop = false;
            this.grpFilters.Text = "Filters";

            // First row of filters
            int yPos = 25;
            int xPos = 15;
            int controlHeight = 23;
            int labelHeight = 15;
            int spacing = 120;

            // 
            // lblStartDate
            // 
            this.lblStartDate.AutoSize = true;
            this.lblStartDate.Location = new Point(xPos, yPos);
            this.lblStartDate.Size = new Size(65, labelHeight);
            this.lblStartDate.Text = "Start Date:";

            // 
            // dtpStartDate
            // 
            this.dtpStartDate.Format = DateTimePickerFormat.Short;
            this.dtpStartDate.Location = new Point(xPos, yPos + 18);
            this.dtpStartDate.Size = new Size(100, controlHeight);

            xPos += spacing;

            // 
            // lblEndDate
            // 
            this.lblEndDate.AutoSize = true;
            this.lblEndDate.Location = new Point(xPos, yPos);
            this.lblEndDate.Size = new Size(60, labelHeight);
            this.lblEndDate.Text = "End Date:";

            // 
            // dtpEndDate
            // 
            this.dtpEndDate.Format = DateTimePickerFormat.Short;
            this.dtpEndDate.Location = new Point(xPos, yPos + 18);
            this.dtpEndDate.Size = new Size(100, controlHeight);

            xPos += spacing;

            // 
            // lblStatusFilter
            // 
            this.lblStatusFilter.AutoSize = true;
            this.lblStatusFilter.Location = new Point(xPos, yPos);
            this.lblStatusFilter.Size = new Size(42, labelHeight);
            this.lblStatusFilter.Text = "Status:";

            // 
            // cmbStatus
            // 
            this.cmbStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbStatus.Location = new Point(xPos, yPos + 18);
            this.cmbStatus.Size = new Size(110, controlHeight);
            this.cmbStatus.SelectedIndexChanged += new EventHandler(this.cmbStatus_SelectedIndexChanged);

            xPos += spacing;

            // 
            // lblConfiguration
            // 
            this.lblConfiguration.AutoSize = true;
            this.lblConfiguration.Location = new Point(xPos, yPos);
            this.lblConfiguration.Size = new Size(85, labelHeight);
            this.lblConfiguration.Text = "Configuration:";

            // 
            // cmbConfiguration
            // 
            this.cmbConfiguration.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbConfiguration.Location = new Point(xPos, yPos + 18);
            this.cmbConfiguration.Size = new Size(150, controlHeight);
            this.cmbConfiguration.SelectedIndexChanged += new EventHandler(this.cmbConfiguration_SelectedIndexChanged);

            // Second row of filters
            yPos += 50;
            xPos = 15;

            // 
            // lblSearch
            // 
            this.lblSearch.AutoSize = true;
            this.lblSearch.Location = new Point(xPos, yPos);
            this.lblSearch.Size = new Size(45, labelHeight);
            this.lblSearch.Text = "Search:";

            // 
            // txtSearch
            // 
            this.txtSearch.Location = new Point(xPos, yPos + 18);
            this.txtSearch.Size = new Size(200, controlHeight);
            this.txtSearch.TextChanged += new EventHandler(this.txtSearch_TextChanged);

            xPos += 220;

            // 
            // btnFilter
            // 
            this.btnFilter.Location = new Point(xPos, yPos + 18);
            this.btnFilter.Size = new Size(80, controlHeight);
            this.btnFilter.Text = "Apply Filter";
            this.btnFilter.UseVisualStyleBackColor = true;
            this.btnFilter.Click += new EventHandler(this.btnFilter_Click);

            xPos += 90;

            // 
            // btnClearFilters
            // 
            this.btnClearFilters.Location = new Point(xPos, yPos + 18);
            this.btnClearFilters.Size = new Size(80, controlHeight);
            this.btnClearFilters.Text = "Clear Filters";
            this.btnClearFilters.UseVisualStyleBackColor = true;
            this.btnClearFilters.Click += new EventHandler(this.btnClearFilters_Click);

            // 
            // dgvLogs
            // 
            this.dgvLogs.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvLogs.Dock = DockStyle.Fill;
            this.dgvLogs.Location = new Point(0, 120);
            this.dgvLogs.Size = new Size(984, 205);
            this.dgvLogs.TabIndex = 1;

            // 
            // lblFilteredCount
            // 
            this.lblFilteredCount.AutoSize = true;
            this.lblFilteredCount.Dock = DockStyle.Bottom;
            this.lblFilteredCount.Location = new Point(0, 325);
            this.lblFilteredCount.Size = new Size(0, 15);
            this.lblFilteredCount.TabIndex = 2;
            this.lblFilteredCount.Padding = new Padding(5);

            // 
            // grpLogDetails
            // 
            this.grpLogDetails.Controls.Add(this.txtLogDetails);
            this.grpLogDetails.Controls.Add(this.panelDetailsButtons);
            this.grpLogDetails.Dock = DockStyle.Fill;
            this.grpLogDetails.Location = new Point(0, 0);
            this.grpLogDetails.Size = new Size(984, 246);
            this.grpLogDetails.TabIndex = 0;
            this.grpLogDetails.TabStop = false;
            this.grpLogDetails.Text = "Log Details";

            // 
            // txtLogDetails
            // 
            this.txtLogDetails.Dock = DockStyle.Fill;
            this.txtLogDetails.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.txtLogDetails.Location = new Point(3, 19);
            this.txtLogDetails.Multiline = true;
            this.txtLogDetails.ReadOnly = true;
            this.txtLogDetails.ScrollBars = ScrollBars.Both;
            this.txtLogDetails.Size = new Size(978, 184);
            this.txtLogDetails.TabIndex = 0;

            // 
            // panelDetailsButtons
            // 
            this.panelDetailsButtons.Controls.Add(this.btnViewDetails);
            this.panelDetailsButtons.Controls.Add(this.btnExportLog);
            this.panelDetailsButtons.Dock = DockStyle.Bottom;
            this.panelDetailsButtons.Location = new Point(3, 203);
            this.panelDetailsButtons.Size = new Size(978, 40);
            this.panelDetailsButtons.TabIndex = 1;

            // 
            // btnViewDetails
            // 
            this.btnViewDetails.Location = new Point(10, 5);
            this.btnViewDetails.Size = new Size(100, 30);
            this.btnViewDetails.Text = "View Details";
            this.btnViewDetails.UseVisualStyleBackColor = true;
            this.btnViewDetails.Enabled = false;
            this.btnViewDetails.Click += new EventHandler(this.btnViewDetails_Click);

            // 
            // btnExportLog
            // 
            this.btnExportLog.Location = new Point(120, 5);
            this.btnExportLog.Size = new Size(80, 30);
            this.btnExportLog.Text = "Export Log";
            this.btnExportLog.UseVisualStyleBackColor = true;
            this.btnExportLog.Enabled = false;
            this.btnExportLog.Click += new EventHandler(this.btnExportLog_Click);

            // 
            // panelBottom
            // 
            this.panelBottom.Controls.Add(this.btnGenerateReport);
            this.panelBottom.Controls.Add(this.btnRefresh);
            this.panelBottom.Controls.Add(this.btnClose);
            this.panelBottom.Controls.Add(this.lblStatus);
            this.panelBottom.Dock = DockStyle.Bottom;
            this.panelBottom.Location = new Point(0, 600);
            this.panelBottom.Size = new Size(984, 50);
            this.panelBottom.TabIndex = 1;

            // 
            // btnGenerateReport
            // 
            this.btnGenerateReport.Location = new Point(15, 10);
            this.btnGenerateReport.Size = new Size(110, 30);
            this.btnGenerateReport.Text = "Generate Report";
            this.btnGenerateReport.UseVisualStyleBackColor = true;
            this.btnGenerateReport.Click += new EventHandler(this.btnGenerateReport_Click);

            // 
            // btnRefresh
            // 
            this.btnRefresh.Location = new Point(789, 10);
            this.btnRefresh.Size = new Size(80, 30);
            this.btnRefresh.Text = "Refresh";
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnRefresh.Click += new EventHandler(this.btnRefresh_Click);

            // 
            // btnClose
            // 
            this.btnClose.Location = new Point(889, 10);
            this.btnClose.Size = new Size(80, 30);
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnClose.Click += new EventHandler(this.btnClose_Click);

            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new Point(150, 18);
            this.lblStatus.Size = new Size(0, 15);
            this.lblStatus.TabIndex = 0;
            this.lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

            // 
            // LogBrowserForm
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(984, 650);
            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.panelBottom);
            this.Name = "LogBrowserForm";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Backup Log Browser";
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel1.PerformLayout();
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.panelFilters.ResumeLayout(false);
            this.grpFilters.ResumeLayout(false);
            this.grpFilters.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvLogs)).EndInit();
            this.grpLogDetails.ResumeLayout(false);
            this.grpLogDetails.PerformLayout();
            this.panelDetailsButtons.ResumeLayout(false);
            this.panelBottom.ResumeLayout(false);
            this.panelBottom.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private SplitContainer splitContainer;
        private Panel panelFilters;
        private GroupBox grpFilters;
        private Label lblStartDate;
        private DateTimePicker dtpStartDate;
        private Label lblEndDate;
        private DateTimePicker dtpEndDate;
        private Label lblStatusFilter;
        private ComboBox cmbStatus;
        private Label lblConfiguration;
        private ComboBox cmbConfiguration;
        private Label lblSearch;
        private TextBox txtSearch;
        private Button btnFilter;
        private Button btnClearFilters;
        private DataGridView dgvLogs;
        private Label lblFilteredCount;
        private GroupBox grpLogDetails;
        private TextBox txtLogDetails;
        private Panel panelDetailsButtons;
        private Button btnViewDetails;
        private Button btnExportLog;
        private Panel panelBottom;
        private Button btnGenerateReport;
        private Button btnRefresh;
        private Button btnClose;
        private Label lblStatus;
    }
}