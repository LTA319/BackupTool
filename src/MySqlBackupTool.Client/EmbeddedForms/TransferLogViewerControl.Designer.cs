namespace MySqlBackupTool.Client.EmbeddedForms
{
    partial class TransferLogViewerControl
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
            this.components = new System.ComponentModel.Container();
            
            // 主面板
            this.mainPanel = new System.Windows.Forms.TableLayoutPanel();
            
            // 控制面板
            this._controlPanel = new System.Windows.Forms.Panel();
            this._refreshButton = new System.Windows.Forms.Button();
            this._exportButton = new System.Windows.Forms.Button();
            this._retryFailedButton = new System.Windows.Forms.Button();
            this.statusLabel = new System.Windows.Forms.Label();
            this._statusFilter = new System.Windows.Forms.ComboBox();
            this.dateLabel = new System.Windows.Forms.Label();
            this._startDatePicker = new System.Windows.Forms.DateTimePicker();
            this.toLabel = new System.Windows.Forms.Label();
            this._endDatePicker = new System.Windows.Forms.DateTimePicker();
            this._filterButton = new System.Windows.Forms.Button();
            
            // 统计信息面板
            this._statisticsGroup = new System.Windows.Forms.GroupBox();
            this._totalTransfersLabel = new System.Windows.Forms.Label();
            this._successfulTransfersLabel = new System.Windows.Forms.Label();
            this._failedTransfersLabel = new System.Windows.Forms.Label();
            this._successRateLabel = new System.Windows.Forms.Label();
            
            // 数据网格
            this._transferLogGrid = new System.Windows.Forms.DataGridView();
            this.idColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.chunkIndexColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.chunkSizeColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.statusColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.transferTimeColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.errorMessageColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            
            // 进度面板
            this.progressPanel = new System.Windows.Forms.Panel();
            this._progressBar = new System.Windows.Forms.ProgressBar();
            this._progressLabel = new System.Windows.Forms.Label();
            
            this.mainPanel.SuspendLayout();
            this._controlPanel.SuspendLayout();
            this._statisticsGroup.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._transferLogGrid)).BeginInit();
            this.progressPanel.SuspendLayout();
            this.SuspendLayout();
            
            // 
            // mainPanel
            // 
            this.mainPanel.ColumnCount = 1;
            this.mainPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.mainPanel.Controls.Add(this._controlPanel, 0, 0);
            this.mainPanel.Controls.Add(this._statisticsGroup, 0, 1);
            this.mainPanel.Controls.Add(this._transferLogGrid, 0, 2);
            this.mainPanel.Controls.Add(this.progressPanel, 0, 3);
            this.mainPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainPanel.Location = new System.Drawing.Point(0, 0);
            this.mainPanel.Name = "mainPanel";
            this.mainPanel.RowCount = 4;
            this.mainPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.mainPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.mainPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.mainPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.mainPanel.Size = new System.Drawing.Size(1000, 700);
            this.mainPanel.TabIndex = 0;
            
            // 
            // _controlPanel
            // 
            this._controlPanel.Controls.Add(this._refreshButton);
            this._controlPanel.Controls.Add(this._exportButton);
            this._controlPanel.Controls.Add(this._retryFailedButton);
            this._controlPanel.Controls.Add(this.statusLabel);
            this._controlPanel.Controls.Add(this._statusFilter);
            this._controlPanel.Controls.Add(this.dateLabel);
            this._controlPanel.Controls.Add(this._startDatePicker);
            this._controlPanel.Controls.Add(this.toLabel);
            this._controlPanel.Controls.Add(this._endDatePicker);
            this._controlPanel.Controls.Add(this._filterButton);
            this._controlPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._controlPanel.Location = new System.Drawing.Point(3, 3);
            this._controlPanel.Name = "_controlPanel";
            this._controlPanel.Size = new System.Drawing.Size(994, 74);
            this._controlPanel.TabIndex = 0;
            
            // 
            // _refreshButton
            // 
            this._refreshButton.Location = new System.Drawing.Point(10, 10);
            this._refreshButton.Name = "_refreshButton";
            this._refreshButton.Size = new System.Drawing.Size(80, 30);
            this._refreshButton.TabIndex = 0;
            this._refreshButton.Text = "刷新";
            this._refreshButton.UseVisualStyleBackColor = true;
            
            // 
            // _exportButton
            // 
            this._exportButton.Location = new System.Drawing.Point(100, 10);
            this._exportButton.Name = "_exportButton";
            this._exportButton.Size = new System.Drawing.Size(80, 30);
            this._exportButton.TabIndex = 1;
            this._exportButton.Text = "导出";
            this._exportButton.UseVisualStyleBackColor = true;
            
            // 
            // _retryFailedButton
            // 
            this._retryFailedButton.Location = new System.Drawing.Point(190, 10);
            this._retryFailedButton.Name = "_retryFailedButton";
            this._retryFailedButton.Size = new System.Drawing.Size(80, 30);
            this._retryFailedButton.TabIndex = 2;
            this._retryFailedButton.Text = "重试失败";
            this._retryFailedButton.UseVisualStyleBackColor = true;
            
            // 
            // statusLabel
            // 
            this.statusLabel.AutoSize = true;
            this.statusLabel.Location = new System.Drawing.Point(300, 18);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(35, 15);
            this.statusLabel.TabIndex = 3;
            this.statusLabel.Text = "状态:";
            
            // 
            // _statusFilter
            // 
            this._statusFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._statusFilter.FormattingEnabled = true;
            this._statusFilter.Items.AddRange(new object[] {
            "全部",
            "Pending",
            "InProgress",
            "Completed",
            "Failed"});
            this._statusFilter.Location = new System.Drawing.Point(350, 15);
            this._statusFilter.Name = "_statusFilter";
            this._statusFilter.Size = new System.Drawing.Size(100, 23);
            this._statusFilter.TabIndex = 4;
            
            // 
            // dateLabel
            // 
            this.dateLabel.AutoSize = true;
            this.dateLabel.Location = new System.Drawing.Point(470, 18);
            this.dateLabel.Name = "dateLabel";
            this.dateLabel.Size = new System.Drawing.Size(35, 15);
            this.dateLabel.TabIndex = 5;
            this.dateLabel.Text = "日期:";
            
            // 
            // _startDatePicker
            // 
            this._startDatePicker.Location = new System.Drawing.Point(520, 15);
            this._startDatePicker.Name = "_startDatePicker";
            this._startDatePicker.Size = new System.Drawing.Size(120, 23);
            this._startDatePicker.TabIndex = 6;
            
            // 
            // toLabel
            // 
            this.toLabel.AutoSize = true;
            this.toLabel.Location = new System.Drawing.Point(650, 18);
            this.toLabel.Name = "toLabel";
            this.toLabel.Size = new System.Drawing.Size(20, 15);
            this.toLabel.TabIndex = 7;
            this.toLabel.Text = "至";
            
            // 
            // _endDatePicker
            // 
            this._endDatePicker.Location = new System.Drawing.Point(680, 15);
            this._endDatePicker.Name = "_endDatePicker";
            this._endDatePicker.Size = new System.Drawing.Size(120, 23);
            this._endDatePicker.TabIndex = 8;
            
            // 
            // _filterButton
            // 
            this._filterButton.Location = new System.Drawing.Point(810, 10);
            this._filterButton.Name = "_filterButton";
            this._filterButton.Size = new System.Drawing.Size(60, 30);
            this._filterButton.TabIndex = 9;
            this._filterButton.Text = "筛选";
            this._filterButton.UseVisualStyleBackColor = true;
            
            // 
            // _statisticsGroup
            // 
            this._statisticsGroup.Controls.Add(this._totalTransfersLabel);
            this._statisticsGroup.Controls.Add(this._successfulTransfersLabel);
            this._statisticsGroup.Controls.Add(this._failedTransfersLabel);
            this._statisticsGroup.Controls.Add(this._successRateLabel);
            this._statisticsGroup.Dock = System.Windows.Forms.DockStyle.Fill;
            this._statisticsGroup.Location = new System.Drawing.Point(3, 83);
            this._statisticsGroup.Name = "_statisticsGroup";
            this._statisticsGroup.Size = new System.Drawing.Size(994, 94);
            this._statisticsGroup.TabIndex = 1;
            this._statisticsGroup.TabStop = false;
            this._statisticsGroup.Text = "传输统计";
            
            // 
            // _totalTransfersLabel
            // 
            this._totalTransfersLabel.AutoSize = true;
            this._totalTransfersLabel.Location = new System.Drawing.Point(20, 25);
            this._totalTransfersLabel.Name = "_totalTransfersLabel";
            this._totalTransfersLabel.Size = new System.Drawing.Size(56, 15);
            this._totalTransfersLabel.TabIndex = 0;
            this._totalTransfersLabel.Text = "总传输: 0";
            
            // 
            // _successfulTransfersLabel
            // 
            this._successfulTransfersLabel.AutoSize = true;
            this._successfulTransfersLabel.Location = new System.Drawing.Point(150, 25);
            this._successfulTransfersLabel.Name = "_successfulTransfersLabel";
            this._successfulTransfersLabel.Size = new System.Drawing.Size(44, 15);
            this._successfulTransfersLabel.TabIndex = 1;
            this._successfulTransfersLabel.Text = "成功: 0";
            
            // 
            // _failedTransfersLabel
            // 
            this._failedTransfersLabel.AutoSize = true;
            this._failedTransfersLabel.Location = new System.Drawing.Point(260, 25);
            this._failedTransfersLabel.Name = "_failedTransfersLabel";
            this._failedTransfersLabel.Size = new System.Drawing.Size(44, 15);
            this._failedTransfersLabel.TabIndex = 2;
            this._failedTransfersLabel.Text = "失败: 0";
            
            // 
            // _successRateLabel
            // 
            this._successRateLabel.AutoSize = true;
            this._successRateLabel.Location = new System.Drawing.Point(370, 25);
            this._successRateLabel.Name = "_successRateLabel";
            this._successRateLabel.Size = new System.Drawing.Size(68, 15);
            this._successRateLabel.TabIndex = 3;
            this._successRateLabel.Text = "成功率: 0%";
            
            // 
            // _transferLogGrid
            // 
            this._transferLogGrid.AllowUserToAddRows = false;
            this._transferLogGrid.AllowUserToDeleteRows = false;
            this._transferLogGrid.AutoGenerateColumns = false;
            this._transferLogGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this._transferLogGrid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.idColumn,
            this.chunkIndexColumn,
            this.chunkSizeColumn,
            this.statusColumn,
            this.transferTimeColumn,
            this.errorMessageColumn});
            this._transferLogGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this._transferLogGrid.Location = new System.Drawing.Point(3, 183);
            this._transferLogGrid.MultiSelect = true;
            this._transferLogGrid.Name = "_transferLogGrid";
            this._transferLogGrid.ReadOnly = true;
            this._transferLogGrid.RowTemplate.Height = 25;
            this._transferLogGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this._transferLogGrid.Size = new System.Drawing.Size(994, 484);
            this._transferLogGrid.TabIndex = 2;
            
            // 
            // idColumn
            // 
            this.idColumn.DataPropertyName = "Id";
            this.idColumn.HeaderText = "ID";
            this.idColumn.Name = "idColumn";
            this.idColumn.ReadOnly = true;
            this.idColumn.Width = 60;
            
            // 
            // chunkIndexColumn
            // 
            this.chunkIndexColumn.DataPropertyName = "ChunkIndex";
            this.chunkIndexColumn.HeaderText = "分块索引";
            this.chunkIndexColumn.Name = "chunkIndexColumn";
            this.chunkIndexColumn.ReadOnly = true;
            this.chunkIndexColumn.Width = 80;
            
            // 
            // chunkSizeColumn
            // 
            this.chunkSizeColumn.DataPropertyName = "ChunkSizeFormatted";
            this.chunkSizeColumn.HeaderText = "分块大小";
            this.chunkSizeColumn.Name = "chunkSizeColumn";
            this.chunkSizeColumn.ReadOnly = true;
            
            // 
            // statusColumn
            // 
            this.statusColumn.DataPropertyName = "Status";
            this.statusColumn.HeaderText = "状态";
            this.statusColumn.Name = "statusColumn";
            this.statusColumn.ReadOnly = true;
            this.statusColumn.Width = 80;
            
            // 
            // transferTimeColumn
            // 
            this.transferTimeColumn.DataPropertyName = "TransferTime";
            this.transferTimeColumn.HeaderText = "开始时间";
            this.transferTimeColumn.Name = "transferTimeColumn";
            this.transferTimeColumn.ReadOnly = true;
            this.transferTimeColumn.Width = 150;
            
            // 
            // errorMessageColumn
            // 
            this.errorMessageColumn.DataPropertyName = "ErrorMessage";
            this.errorMessageColumn.HeaderText = "错误消息";
            this.errorMessageColumn.Name = "errorMessageColumn";
            this.errorMessageColumn.ReadOnly = true;
            this.errorMessageColumn.Width = 300;
            
            // 
            // progressPanel
            // 
            this.progressPanel.Controls.Add(this._progressBar);
            this.progressPanel.Controls.Add(this._progressLabel);
            this.progressPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.progressPanel.Location = new System.Drawing.Point(3, 673);
            this.progressPanel.Name = "progressPanel";
            this.progressPanel.Size = new System.Drawing.Size(994, 24);
            this.progressPanel.TabIndex = 3;
            
            // 
            // _progressBar
            // 
            this._progressBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this._progressBar.Location = new System.Drawing.Point(0, 0);
            this._progressBar.Name = "_progressBar";
            this._progressBar.Size = new System.Drawing.Size(994, 24);
            this._progressBar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this._progressBar.TabIndex = 0;
            this._progressBar.Visible = false;
            
            // 
            // _progressLabel
            // 
            this._progressLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._progressLabel.Location = new System.Drawing.Point(0, 0);
            this._progressLabel.Name = "_progressLabel";
            this._progressLabel.Size = new System.Drawing.Size(994, 24);
            this._progressLabel.TabIndex = 1;
            this._progressLabel.Text = "准备就绪";
            this._progressLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            
            // 
            // TransferLogViewerControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.mainPanel);
            this.Name = "TransferLogViewerControl";
            this.Size = new System.Drawing.Size(1000, 700);
            this.mainPanel.ResumeLayout(false);
            this._controlPanel.ResumeLayout(false);
            this._controlPanel.PerformLayout();
            this._statisticsGroup.ResumeLayout(false);
            this._statisticsGroup.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._transferLogGrid)).EndInit();
            this.progressPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel mainPanel;
        private System.Windows.Forms.Panel _controlPanel;
        private System.Windows.Forms.Button _refreshButton;
        private System.Windows.Forms.Button _exportButton;
        private System.Windows.Forms.Button _retryFailedButton;
        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.ComboBox _statusFilter;
        private System.Windows.Forms.Label dateLabel;
        private System.Windows.Forms.DateTimePicker _startDatePicker;
        private System.Windows.Forms.Label toLabel;
        private System.Windows.Forms.DateTimePicker _endDatePicker;
        private System.Windows.Forms.Button _filterButton;
        private System.Windows.Forms.GroupBox _statisticsGroup;
        private System.Windows.Forms.Label _totalTransfersLabel;
        private System.Windows.Forms.Label _successfulTransfersLabel;
        private System.Windows.Forms.Label _failedTransfersLabel;
        private System.Windows.Forms.Label _successRateLabel;
        private System.Windows.Forms.DataGridView _transferLogGrid;
        private System.Windows.Forms.DataGridViewTextBoxColumn idColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn chunkIndexColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn chunkSizeColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn statusColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn transferTimeColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn errorMessageColumn;
        private System.Windows.Forms.Panel progressPanel;
        private System.Windows.Forms.ProgressBar _progressBar;
        private System.Windows.Forms.Label _progressLabel;
    }
}
