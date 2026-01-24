namespace MySqlBackupTool.Client.Forms
{
    partial class ReportViewerForm
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
            this.txtReport = new TextBox();
            this.panelButtons = new Panel();
            this.btnExport = new Button();
            this.btnClose = new Button();

            this.panelButtons.SuspendLayout();
            this.SuspendLayout();

            // 
            // txtReport
            // 
            this.txtReport.Dock = DockStyle.Fill;
            this.txtReport.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.txtReport.Location = new Point(0, 0);
            this.txtReport.Multiline = true;
            this.txtReport.ReadOnly = true;
            this.txtReport.ScrollBars = ScrollBars.Both;
            this.txtReport.Size = new Size(784, 511);
            this.txtReport.TabIndex = 0;

            // 
            // panelButtons
            // 
            this.panelButtons.Controls.Add(this.btnExport);
            this.panelButtons.Controls.Add(this.btnClose);
            this.panelButtons.Dock = DockStyle.Bottom;
            this.panelButtons.Location = new Point(0, 511);
            this.panelButtons.Size = new Size(784, 50);
            this.panelButtons.TabIndex = 1;

            // 
            // btnExport
            // 
            this.btnExport.Location = new Point(612, 10);
            this.btnExport.Size = new Size(80, 30);
            this.btnExport.Text = "Export";
            this.btnExport.UseVisualStyleBackColor = true;
            this.btnExport.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnExport.Click += new EventHandler(this.btnExport_Click);

            // 
            // btnClose
            // 
            this.btnClose.Location = new Point(692, 10);
            this.btnClose.Size = new Size(80, 30);
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnClose.Click += new EventHandler(this.btnClose_Click);

            // 
            // ReportViewerForm
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(784, 561);
            this.Controls.Add(this.txtReport);
            this.Controls.Add(this.panelButtons);
            this.Name = "ReportViewerForm";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Backup Summary Report";
            this.panelButtons.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private TextBox txtReport;
        private Panel panelButtons;
        private Button btnExport;
        private Button btnClose;
    }
}