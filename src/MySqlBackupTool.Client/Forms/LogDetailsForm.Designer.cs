namespace MySqlBackupTool.Client.Forms
{
    partial class LogDetailsForm
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
            this.txtDetails = new TextBox();
            this.btnClose = new Button();

            this.SuspendLayout();

            // 
            // txtDetails
            // 
            this.txtDetails.Dock = DockStyle.Fill;
            this.txtDetails.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.txtDetails.Location = new Point(0, 0);
            this.txtDetails.Multiline = true;
            this.txtDetails.ReadOnly = true;
            this.txtDetails.ScrollBars = ScrollBars.Both;
            this.txtDetails.Size = new Size(584, 411);
            this.txtDetails.TabIndex = 0;

            // 
            // btnClose
            // 
            this.btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnClose.Location = new Point(497, 420);
            this.btnClose.Size = new Size(75, 23);
            this.btnClose.TabIndex = 1;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new EventHandler(this.btnClose_Click);

            // 
            // LogDetailsForm
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(584, 461);
            this.Controls.Add(this.txtDetails);
            this.Controls.Add(this.btnClose);
            this.Name = "LogDetailsForm";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Log Details";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private TextBox txtDetails;
        private Button btnClose;
    }
}