namespace MySqlBackupTool.Client.Forms
{
    partial class ConfigurationForm
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
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabGeneral = new System.Windows.Forms.TabPage();
            this.tabMySQL = new System.Windows.Forms.TabPage();
            this.tabServer = new System.Windows.Forms.TabPage();
            this.tabNaming = new System.Windows.Forms.TabPage();
            
            // General tab controls
            this.lblConfigName = new System.Windows.Forms.Label();
            this.txtConfigName = new System.Windows.Forms.TextBox();
            this.chkIsActive = new System.Windows.Forms.CheckBox();
            
            // MySQL tab controls
            this.lblMySqlUsername = new System.Windows.Forms.Label();
            this.txtMySqlUsername = new System.Windows.Forms.TextBox();
            this.lblMySqlPassword = new System.Windows.Forms.Label();
            this.txtMySqlPassword = new System.Windows.Forms.TextBox();
            this.lblServiceName = new System.Windows.Forms.Label();
            this.txtServiceName = new System.Windows.Forms.TextBox();
            this.lblDataDirectory = new System.Windows.Forms.Label();
            this.txtDataDirectory = new System.Windows.Forms.TextBox();
            this.btnBrowseDataDirectory = new System.Windows.Forms.Button();
            this.lblMySqlHost = new System.Windows.Forms.Label();
            this.txtMySqlHost = new System.Windows.Forms.TextBox();
            this.lblMySqlPort = new System.Windows.Forms.Label();
            this.numMySqlPort = new System.Windows.Forms.NumericUpDown();
            this.btnTestMySqlConnection = new System.Windows.Forms.Button();
            this.lblMySqlConnectionStatus = new System.Windows.Forms.Label();
            
            // Server tab controls
            this.lblServerIP = new System.Windows.Forms.Label();
            this.txtServerIP = new System.Windows.Forms.TextBox();
            this.lblServerPort = new System.Windows.Forms.Label();
            this.numServerPort = new System.Windows.Forms.NumericUpDown();
            this.chkUseSSL = new System.Windows.Forms.CheckBox();
            this.lblTargetDirectory = new System.Windows.Forms.Label();
            this.txtTargetDirectory = new System.Windows.Forms.TextBox();
            this.btnTestServerConnection = new System.Windows.Forms.Button();
            this.lblServerConnectionStatus = new System.Windows.Forms.Label();
            
            // Naming tab controls
            this.lblNamingPattern = new System.Windows.Forms.Label();
            this.txtNamingPattern = new System.Windows.Forms.TextBox();
            this.lblDateFormat = new System.Windows.Forms.Label();
            this.txtDateFormat = new System.Windows.Forms.TextBox();
            this.chkIncludeServerName = new System.Windows.Forms.CheckBox();
            this.chkIncludeDatabaseName = new System.Windows.Forms.CheckBox();
            this.btnPreviewFileName = new System.Windows.Forms.Button();
            this.lblFileNamePreview = new System.Windows.Forms.Label();
            
            // Bottom buttons
            this.btnSave = new System.Windows.Forms.Button();
            this.btnValidateAndSave = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();

            ((System.ComponentModel.ISupportInitialize)(this.numMySqlPort)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numServerPort)).BeginInit();
            this.tabControl.SuspendLayout();
            this.tabGeneral.SuspendLayout();
            this.tabMySQL.SuspendLayout();
            this.tabServer.SuspendLayout();
            this.tabNaming.SuspendLayout();
            this.SuspendLayout();

            // 
            // tabControl
            // 
            this.tabControl.Controls.Add(this.tabGeneral);
            this.tabControl.Controls.Add(this.tabMySQL);
            this.tabControl.Controls.Add(this.tabServer);
            this.tabControl.Controls.Add(this.tabNaming);
            this.tabControl.Location = new System.Drawing.Point(12, 12);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(560, 580);
            this.tabControl.TabIndex = 0;

            // 
            // tabGeneral
            // 
            this.tabGeneral.Controls.Add(this.lblConfigName);
            this.tabGeneral.Controls.Add(this.txtConfigName);
            this.tabGeneral.Controls.Add(this.chkIsActive);
            this.tabGeneral.Location = new System.Drawing.Point(4, 24);
            this.tabGeneral.Name = "tabGeneral";
            this.tabGeneral.Padding = new System.Windows.Forms.Padding(3);
            this.tabGeneral.Size = new System.Drawing.Size(552, 552);
            this.tabGeneral.TabIndex = 0;
            this.tabGeneral.Text = "General";
            this.tabGeneral.UseVisualStyleBackColor = true;

            // 
            // lblConfigName
            // 
            this.lblConfigName.AutoSize = true;
            this.lblConfigName.Location = new System.Drawing.Point(20, 30);
            this.lblConfigName.Name = "lblConfigName";
            this.lblConfigName.Size = new System.Drawing.Size(120, 15);
            this.lblConfigName.TabIndex = 0;
            this.lblConfigName.Text = "Configuration Name:";

            // 
            // txtConfigName
            // 
            this.txtConfigName.Location = new System.Drawing.Point(20, 50);
            this.txtConfigName.MaxLength = 100;
            this.txtConfigName.Name = "txtConfigName";
            this.txtConfigName.Size = new System.Drawing.Size(300, 23);
            this.txtConfigName.TabIndex = 1;

            // 
            // chkIsActive
            // 
            this.chkIsActive.AutoSize = true;
            this.chkIsActive.Checked = true;
            this.chkIsActive.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkIsActive.Location = new System.Drawing.Point(20, 90);
            this.chkIsActive.Name = "chkIsActive";
            this.chkIsActive.Size = new System.Drawing.Size(150, 19);
            this.chkIsActive.TabIndex = 2;
            this.chkIsActive.Text = "Configuration is active";
            this.chkIsActive.UseVisualStyleBackColor = true;

            // 
            // tabMySQL
            // 
            this.tabMySQL.Controls.Add(this.lblMySqlUsername);
            this.tabMySQL.Controls.Add(this.txtMySqlUsername);
            this.tabMySQL.Controls.Add(this.lblMySqlPassword);
            this.tabMySQL.Controls.Add(this.txtMySqlPassword);
            this.tabMySQL.Controls.Add(this.lblServiceName);
            this.tabMySQL.Controls.Add(this.txtServiceName);
            this.tabMySQL.Controls.Add(this.lblDataDirectory);
            this.tabMySQL.Controls.Add(this.txtDataDirectory);
            this.tabMySQL.Controls.Add(this.btnBrowseDataDirectory);
            this.tabMySQL.Controls.Add(this.lblMySqlHost);
            this.tabMySQL.Controls.Add(this.txtMySqlHost);
            this.tabMySQL.Controls.Add(this.lblMySqlPort);
            this.tabMySQL.Controls.Add(this.numMySqlPort);
            this.tabMySQL.Controls.Add(this.btnTestMySqlConnection);
            this.tabMySQL.Controls.Add(this.lblMySqlConnectionStatus);
            this.tabMySQL.Location = new System.Drawing.Point(4, 24);
            this.tabMySQL.Name = "tabMySQL";
            this.tabMySQL.Padding = new System.Windows.Forms.Padding(3);
            this.tabMySQL.Size = new System.Drawing.Size(552, 552);
            this.tabMySQL.TabIndex = 1;
            this.tabMySQL.Text = "MySQL";
            this.tabMySQL.UseVisualStyleBackColor = true;

            // 
            // lblMySqlUsername
            // 
            this.lblMySqlUsername.AutoSize = true;
            this.lblMySqlUsername.Location = new System.Drawing.Point(20, 20);
            this.lblMySqlUsername.Name = "lblMySqlUsername";
            this.lblMySqlUsername.Size = new System.Drawing.Size(65, 15);
            this.lblMySqlUsername.TabIndex = 0;
            this.lblMySqlUsername.Text = "Username:";

            // 
            // txtMySqlUsername
            // 
            this.txtMySqlUsername.Location = new System.Drawing.Point(20, 40);
            this.txtMySqlUsername.MaxLength = 100;
            this.txtMySqlUsername.Name = "txtMySqlUsername";
            this.txtMySqlUsername.Size = new System.Drawing.Size(200, 23);
            this.txtMySqlUsername.TabIndex = 1;

            // 
            // lblMySqlPassword
            // 
            this.lblMySqlPassword.AutoSize = true;
            this.lblMySqlPassword.Location = new System.Drawing.Point(20, 70);
            this.lblMySqlPassword.Name = "lblMySqlPassword";
            this.lblMySqlPassword.Size = new System.Drawing.Size(60, 15);
            this.lblMySqlPassword.TabIndex = 2;
            this.lblMySqlPassword.Text = "Password:";

            // 
            // txtMySqlPassword
            // 
            this.txtMySqlPassword.Location = new System.Drawing.Point(20, 90);
            this.txtMySqlPassword.MaxLength = 100;
            this.txtMySqlPassword.Name = "txtMySqlPassword";
            this.txtMySqlPassword.Size = new System.Drawing.Size(200, 23);
            this.txtMySqlPassword.TabIndex = 3;
            this.txtMySqlPassword.UseSystemPasswordChar = true;

            // 
            // lblServiceName
            // 
            this.lblServiceName.AutoSize = true;
            this.lblServiceName.Location = new System.Drawing.Point(20, 120);
            this.lblServiceName.Name = "lblServiceName";
            this.lblServiceName.Size = new System.Drawing.Size(85, 15);
            this.lblServiceName.TabIndex = 4;
            this.lblServiceName.Text = "Service Name:";

            // 
            // txtServiceName
            // 
            this.txtServiceName.Location = new System.Drawing.Point(20, 140);
            this.txtServiceName.MaxLength = 100;
            this.txtServiceName.Name = "txtServiceName";
            this.txtServiceName.Size = new System.Drawing.Size(200, 23);
            this.txtServiceName.TabIndex = 5;

            // 
            // lblDataDirectory
            // 
            this.lblDataDirectory.AutoSize = true;
            this.lblDataDirectory.Location = new System.Drawing.Point(20, 170);
            this.lblDataDirectory.Name = "lblDataDirectory";
            this.lblDataDirectory.Size = new System.Drawing.Size(85, 15);
            this.lblDataDirectory.TabIndex = 6;
            this.lblDataDirectory.Text = "Data Directory:";

            // 
            // txtDataDirectory
            // 
            this.txtDataDirectory.Location = new System.Drawing.Point(20, 190);
            this.txtDataDirectory.MaxLength = 500;
            this.txtDataDirectory.Name = "txtDataDirectory";
            this.txtDataDirectory.Size = new System.Drawing.Size(350, 23);
            this.txtDataDirectory.TabIndex = 7;

            // 
            // btnBrowseDataDirectory
            // 
            this.btnBrowseDataDirectory.Location = new System.Drawing.Point(380, 190);
            this.btnBrowseDataDirectory.Name = "btnBrowseDataDirectory";
            this.btnBrowseDataDirectory.Size = new System.Drawing.Size(75, 23);
            this.btnBrowseDataDirectory.TabIndex = 8;
            this.btnBrowseDataDirectory.Text = "Browse...";
            this.btnBrowseDataDirectory.UseVisualStyleBackColor = true;
            this.btnBrowseDataDirectory.Click += new System.EventHandler(this.btnBrowseDataDirectory_Click);

            // 
            // lblMySqlHost
            // 
            this.lblMySqlHost.AutoSize = true;
            this.lblMySqlHost.Location = new System.Drawing.Point(20, 220);
            this.lblMySqlHost.Name = "lblMySqlHost";
            this.lblMySqlHost.Size = new System.Drawing.Size(35, 15);
            this.lblMySqlHost.TabIndex = 9;
            this.lblMySqlHost.Text = "Host:";

            // 
            // txtMySqlHost
            // 
            this.txtMySqlHost.Location = new System.Drawing.Point(20, 240);
            this.txtMySqlHost.MaxLength = 255;
            this.txtMySqlHost.Name = "txtMySqlHost";
            this.txtMySqlHost.Size = new System.Drawing.Size(150, 23);
            this.txtMySqlHost.TabIndex = 10;

            // 
            // lblMySqlPort
            // 
            this.lblMySqlPort.AutoSize = true;
            this.lblMySqlPort.Location = new System.Drawing.Point(200, 220);
            this.lblMySqlPort.Name = "lblMySqlPort";
            this.lblMySqlPort.Size = new System.Drawing.Size(32, 15);
            this.lblMySqlPort.TabIndex = 11;
            this.lblMySqlPort.Text = "Port:";

            // 
            // numMySqlPort
            // 
            this.numMySqlPort.Location = new System.Drawing.Point(200, 240);
            this.numMySqlPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            this.numMySqlPort.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numMySqlPort.Name = "numMySqlPort";
            this.numMySqlPort.Size = new System.Drawing.Size(80, 23);
            this.numMySqlPort.TabIndex = 12;
            this.numMySqlPort.Value = new decimal(new int[] { 3306, 0, 0, 0 });

            // 
            // btnTestMySqlConnection
            // 
            this.btnTestMySqlConnection.Location = new System.Drawing.Point(20, 270);
            this.btnTestMySqlConnection.Name = "btnTestMySqlConnection";
            this.btnTestMySqlConnection.Size = new System.Drawing.Size(120, 30);
            this.btnTestMySqlConnection.TabIndex = 13;
            this.btnTestMySqlConnection.Text = "Test Connection";
            this.btnTestMySqlConnection.UseVisualStyleBackColor = true;
            this.btnTestMySqlConnection.Click += new System.EventHandler(this.btnTestMySqlConnection_Click);

            // 
            // lblMySqlConnectionStatus
            // 
            this.lblMySqlConnectionStatus.AutoSize = true;
            this.lblMySqlConnectionStatus.ForeColor = System.Drawing.Color.Blue;
            this.lblMySqlConnectionStatus.Location = new System.Drawing.Point(160, 278);
            this.lblMySqlConnectionStatus.Name = "lblMySqlConnectionStatus";
            this.lblMySqlConnectionStatus.Size = new System.Drawing.Size(0, 15);
            this.lblMySqlConnectionStatus.TabIndex = 14;

            // 
            // tabServer
            // 
            this.tabServer.Controls.Add(this.lblServerIP);
            this.tabServer.Controls.Add(this.txtServerIP);
            this.tabServer.Controls.Add(this.lblServerPort);
            this.tabServer.Controls.Add(this.numServerPort);
            this.tabServer.Controls.Add(this.chkUseSSL);
            this.tabServer.Controls.Add(this.lblTargetDirectory);
            this.tabServer.Controls.Add(this.txtTargetDirectory);
            this.tabServer.Controls.Add(this.btnTestServerConnection);
            this.tabServer.Controls.Add(this.lblServerConnectionStatus);
            this.tabServer.Location = new System.Drawing.Point(4, 24);
            this.tabServer.Name = "tabServer";
            this.tabServer.Padding = new System.Windows.Forms.Padding(3);
            this.tabServer.Size = new System.Drawing.Size(552, 552);
            this.tabServer.TabIndex = 2;
            this.tabServer.Text = "Target Server";
            this.tabServer.UseVisualStyleBackColor = true;

            // 
            // lblServerIP
            // 
            this.lblServerIP.AutoSize = true;
            this.lblServerIP.Location = new System.Drawing.Point(20, 20);
            this.lblServerIP.Name = "lblServerIP";
            this.lblServerIP.Size = new System.Drawing.Size(70, 15);
            this.lblServerIP.TabIndex = 0;
            this.lblServerIP.Text = "IP Address:";

            // 
            // txtServerIP
            // 
            this.txtServerIP.Location = new System.Drawing.Point(20, 40);
            this.txtServerIP.MaxLength = 45;
            this.txtServerIP.Name = "txtServerIP";
            this.txtServerIP.Size = new System.Drawing.Size(150, 23);
            this.txtServerIP.TabIndex = 1;

            // 
            // lblServerPort
            // 
            this.lblServerPort.AutoSize = true;
            this.lblServerPort.Location = new System.Drawing.Point(200, 20);
            this.lblServerPort.Name = "lblServerPort";
            this.lblServerPort.Size = new System.Drawing.Size(32, 15);
            this.lblServerPort.TabIndex = 2;
            this.lblServerPort.Text = "Port:";

            // 
            // numServerPort
            // 
            this.numServerPort.Location = new System.Drawing.Point(200, 40);
            this.numServerPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            this.numServerPort.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numServerPort.Name = "numServerPort";
            this.numServerPort.Size = new System.Drawing.Size(80, 23);
            this.numServerPort.TabIndex = 3;
            this.numServerPort.Value = new decimal(new int[] { 8080, 0, 0, 0 });

            // 
            // chkUseSSL
            // 
            this.chkUseSSL.AutoSize = true;
            this.chkUseSSL.Checked = true;
            this.chkUseSSL.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkUseSSL.Location = new System.Drawing.Point(20, 70);
            this.chkUseSSL.Name = "chkUseSSL";
            this.chkUseSSL.Size = new System.Drawing.Size(70, 19);
            this.chkUseSSL.TabIndex = 4;
            this.chkUseSSL.Text = "Use SSL";
            this.chkUseSSL.UseVisualStyleBackColor = true;

            // 
            // lblTargetDirectory
            // 
            this.lblTargetDirectory.AutoSize = true;
            this.lblTargetDirectory.Location = new System.Drawing.Point(20, 120);
            this.lblTargetDirectory.Name = "lblTargetDirectory";
            this.lblTargetDirectory.Size = new System.Drawing.Size(95, 15);
            this.lblTargetDirectory.TabIndex = 5;
            this.lblTargetDirectory.Text = "Target Directory:";

            // 
            // txtTargetDirectory
            // 
            this.txtTargetDirectory.Location = new System.Drawing.Point(20, 140);
            this.txtTargetDirectory.MaxLength = 500;
            this.txtTargetDirectory.Name = "txtTargetDirectory";
            this.txtTargetDirectory.Size = new System.Drawing.Size(400, 23);
            this.txtTargetDirectory.TabIndex = 6;

            // 
            // btnTestServerConnection
            // 
            this.btnTestServerConnection.Location = new System.Drawing.Point(20, 170);
            this.btnTestServerConnection.Name = "btnTestServerConnection";
            this.btnTestServerConnection.Size = new System.Drawing.Size(120, 30);
            this.btnTestServerConnection.TabIndex = 7;
            this.btnTestServerConnection.Text = "Test Connection";
            this.btnTestServerConnection.UseVisualStyleBackColor = true;
            this.btnTestServerConnection.Click += new System.EventHandler(this.btnTestServerConnection_Click);

            // 
            // lblServerConnectionStatus
            // 
            this.lblServerConnectionStatus.AutoSize = true;
            this.lblServerConnectionStatus.ForeColor = System.Drawing.Color.Blue;
            this.lblServerConnectionStatus.Location = new System.Drawing.Point(160, 178);
            this.lblServerConnectionStatus.Name = "lblServerConnectionStatus";
            this.lblServerConnectionStatus.Size = new System.Drawing.Size(0, 15);
            this.lblServerConnectionStatus.TabIndex = 8;

            // 
            // tabNaming
            // 
            this.tabNaming.Controls.Add(this.lblNamingPattern);
            this.tabNaming.Controls.Add(this.txtNamingPattern);
            this.tabNaming.Controls.Add(this.lblDateFormat);
            this.tabNaming.Controls.Add(this.txtDateFormat);
            this.tabNaming.Controls.Add(this.chkIncludeServerName);
            this.tabNaming.Controls.Add(this.chkIncludeDatabaseName);
            this.tabNaming.Controls.Add(this.btnPreviewFileName);
            this.tabNaming.Controls.Add(this.lblFileNamePreview);
            this.tabNaming.Location = new System.Drawing.Point(4, 24);
            this.tabNaming.Name = "tabNaming";
            this.tabNaming.Padding = new System.Windows.Forms.Padding(3);
            this.tabNaming.Size = new System.Drawing.Size(552, 552);
            this.tabNaming.TabIndex = 3;
            this.tabNaming.Text = "File Naming";
            this.tabNaming.UseVisualStyleBackColor = true;

            // 
            // lblNamingPattern
            // 
            this.lblNamingPattern.AutoSize = true;
            this.lblNamingPattern.Location = new System.Drawing.Point(20, 20);
            this.lblNamingPattern.Name = "lblNamingPattern";
            this.lblNamingPattern.Size = new System.Drawing.Size(95, 15);
            this.lblNamingPattern.TabIndex = 0;
            this.lblNamingPattern.Text = "Naming Pattern:";

            // 
            // txtNamingPattern
            // 
            this.txtNamingPattern.Location = new System.Drawing.Point(20, 40);
            this.txtNamingPattern.MaxLength = 200;
            this.txtNamingPattern.Name = "txtNamingPattern";
            this.txtNamingPattern.Size = new System.Drawing.Size(400, 23);
            this.txtNamingPattern.TabIndex = 1;

            // 
            // lblDateFormat
            // 
            this.lblDateFormat.AutoSize = true;
            this.lblDateFormat.Location = new System.Drawing.Point(20, 70);
            this.lblDateFormat.Name = "lblDateFormat";
            this.lblDateFormat.Size = new System.Drawing.Size(75, 15);
            this.lblDateFormat.TabIndex = 2;
            this.lblDateFormat.Text = "Date Format:";

            // 
            // txtDateFormat
            // 
            this.txtDateFormat.Location = new System.Drawing.Point(20, 90);
            this.txtDateFormat.MaxLength = 50;
            this.txtDateFormat.Name = "txtDateFormat";
            this.txtDateFormat.Size = new System.Drawing.Size(200, 23);
            this.txtDateFormat.TabIndex = 3;

            // 
            // chkIncludeServerName
            // 
            this.chkIncludeServerName.AutoSize = true;
            this.chkIncludeServerName.Checked = true;
            this.chkIncludeServerName.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkIncludeServerName.Location = new System.Drawing.Point(20, 120);
            this.chkIncludeServerName.Name = "chkIncludeServerName";
            this.chkIncludeServerName.Size = new System.Drawing.Size(130, 19);
            this.chkIncludeServerName.TabIndex = 4;
            this.chkIncludeServerName.Text = "Include Server Name";
            this.chkIncludeServerName.UseVisualStyleBackColor = true;

            // 
            // chkIncludeDatabaseName
            // 
            this.chkIncludeDatabaseName.AutoSize = true;
            this.chkIncludeDatabaseName.Checked = true;
            this.chkIncludeDatabaseName.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkIncludeDatabaseName.Location = new System.Drawing.Point(20, 150);
            this.chkIncludeDatabaseName.Name = "chkIncludeDatabaseName";
            this.chkIncludeDatabaseName.Size = new System.Drawing.Size(145, 19);
            this.chkIncludeDatabaseName.TabIndex = 5;
            this.chkIncludeDatabaseName.Text = "Include Database Name";
            this.chkIncludeDatabaseName.UseVisualStyleBackColor = true;

            // 
            // btnPreviewFileName
            // 
            this.btnPreviewFileName.Location = new System.Drawing.Point(20, 200);
            this.btnPreviewFileName.Name = "btnPreviewFileName";
            this.btnPreviewFileName.Size = new System.Drawing.Size(120, 30);
            this.btnPreviewFileName.TabIndex = 6;
            this.btnPreviewFileName.Text = "Preview Filename";
            this.btnPreviewFileName.UseVisualStyleBackColor = true;
            this.btnPreviewFileName.Click += new System.EventHandler(this.btnPreviewFileName_Click);

            // 
            // lblFileNamePreview
            // 
            this.lblFileNamePreview.AutoSize = true;
            this.lblFileNamePreview.ForeColor = System.Drawing.Color.Blue;
            this.lblFileNamePreview.Location = new System.Drawing.Point(20, 240);
            this.lblFileNamePreview.Name = "lblFileNamePreview";
            this.lblFileNamePreview.Size = new System.Drawing.Size(0, 15);
            this.lblFileNamePreview.TabIndex = 7;

            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(297, 610);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(90, 30);
            this.btnSave.TabIndex = 1;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);

            // 
            // btnValidateAndSave
            // 
            this.btnValidateAndSave.Location = new System.Drawing.Point(397, 610);
            this.btnValidateAndSave.Name = "btnValidateAndSave";
            this.btnValidateAndSave.Size = new System.Drawing.Size(90, 30);
            this.btnValidateAndSave.TabIndex = 2;
            this.btnValidateAndSave.Text = "Validate && Save";
            this.btnValidateAndSave.UseVisualStyleBackColor = true;
            this.btnValidateAndSave.Click += new System.EventHandler(this.btnValidateAndSave_Click);

            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(497, 610);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 30);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);

            // 
            // ConfigurationForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 661);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.btnValidateAndSave);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ConfigurationForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Backup Configuration";
            ((System.ComponentModel.ISupportInitialize)(this.numMySqlPort)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numServerPort)).EndInit();
            this.tabControl.ResumeLayout(false);
            this.tabGeneral.ResumeLayout(false);
            this.tabGeneral.PerformLayout();
            this.tabMySQL.ResumeLayout(false);
            this.tabMySQL.PerformLayout();
            this.tabServer.ResumeLayout(false);
            this.tabServer.PerformLayout();
            this.tabNaming.ResumeLayout(false);
            this.tabNaming.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabGeneral;
        private System.Windows.Forms.TabPage tabMySQL;
        private System.Windows.Forms.TabPage tabServer;
        private System.Windows.Forms.TabPage tabNaming;
        
        // General tab controls
        private System.Windows.Forms.Label lblConfigName;
        private System.Windows.Forms.TextBox txtConfigName;
        private System.Windows.Forms.CheckBox chkIsActive;
        
        // MySQL tab controls
        private System.Windows.Forms.Label lblMySqlUsername;
        private System.Windows.Forms.TextBox txtMySqlUsername;
        private System.Windows.Forms.Label lblMySqlPassword;
        private System.Windows.Forms.TextBox txtMySqlPassword;
        private System.Windows.Forms.Label lblServiceName;
        private System.Windows.Forms.TextBox txtServiceName;
        private System.Windows.Forms.Label lblDataDirectory;
        private System.Windows.Forms.TextBox txtDataDirectory;
        private System.Windows.Forms.Button btnBrowseDataDirectory;
        private System.Windows.Forms.Label lblMySqlHost;
        private System.Windows.Forms.TextBox txtMySqlHost;
        private System.Windows.Forms.Label lblMySqlPort;
        private System.Windows.Forms.NumericUpDown numMySqlPort;
        private System.Windows.Forms.Button btnTestMySqlConnection;
        private System.Windows.Forms.Label lblMySqlConnectionStatus;
        
        // Server tab controls
        private System.Windows.Forms.Label lblServerIP;
        private System.Windows.Forms.TextBox txtServerIP;
        private System.Windows.Forms.Label lblServerPort;
        private System.Windows.Forms.NumericUpDown numServerPort;
        private System.Windows.Forms.CheckBox chkUseSSL;
        private System.Windows.Forms.Label lblTargetDirectory;
        private System.Windows.Forms.TextBox txtTargetDirectory;
        private System.Windows.Forms.Button btnTestServerConnection;
        private System.Windows.Forms.Label lblServerConnectionStatus;
        
        // Naming tab controls
        private System.Windows.Forms.Label lblNamingPattern;
        private System.Windows.Forms.TextBox txtNamingPattern;
        private System.Windows.Forms.Label lblDateFormat;
        private System.Windows.Forms.TextBox txtDateFormat;
        private System.Windows.Forms.CheckBox chkIncludeServerName;
        private System.Windows.Forms.CheckBox chkIncludeDatabaseName;
        private System.Windows.Forms.Button btnPreviewFileName;
        private System.Windows.Forms.Label lblFileNamePreview;
        
        // Bottom buttons
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnValidateAndSave;
        private System.Windows.Forms.Button btnCancel;
    }
}