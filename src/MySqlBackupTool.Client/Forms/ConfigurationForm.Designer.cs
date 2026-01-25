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
            tabControl = new TabControl();
            tabGeneral = new TabPage();
            lblConfigName = new Label();
            txtConfigName = new TextBox();
            chkIsActive = new CheckBox();
            tabMySQL = new TabPage();
            lblMySqlUsername = new Label();
            txtMySqlUsername = new TextBox();
            lblMySqlPassword = new Label();
            txtMySqlPassword = new TextBox();
            lblServiceName = new Label();
            txtServiceName = new TextBox();
            lblDataDirectory = new Label();
            txtDataDirectory = new TextBox();
            btnBrowseDataDirectory = new Button();
            lblMySqlHost = new Label();
            txtMySqlHost = new TextBox();
            lblMySqlPort = new Label();
            numMySqlPort = new NumericUpDown();
            btnTestMySqlConnection = new Button();
            lblMySqlConnectionStatus = new Label();
            tabServer = new TabPage();
            lblServerIP = new Label();
            txtServerIP = new TextBox();
            lblServerPort = new Label();
            numServerPort = new NumericUpDown();
            chkUseSSL = new CheckBox();
            lblTargetDirectory = new Label();
            txtTargetDirectory = new TextBox();
            btnTestServerConnection = new Button();
            lblServerConnectionStatus = new Label();
            tabNaming = new TabPage();
            lblNamingPattern = new Label();
            txtNamingPattern = new TextBox();
            lblDateFormat = new Label();
            txtDateFormat = new TextBox();
            chkIncludeServerName = new CheckBox();
            chkIncludeDatabaseName = new CheckBox();
            btnPreviewFileName = new Button();
            lblFileNamePreview = new Label();
            btnSave = new Button();
            btnValidateAndSave = new Button();
            btnCancel = new Button();
            openFileDialogDataDirectory = new OpenFileDialog();
            tabControl.SuspendLayout();
            tabGeneral.SuspendLayout();
            tabMySQL.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numMySqlPort).BeginInit();
            tabServer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numServerPort).BeginInit();
            tabNaming.SuspendLayout();
            SuspendLayout();
            // 
            // tabControl
            // 
            tabControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tabControl.Controls.Add(tabGeneral);
            tabControl.Controls.Add(tabMySQL);
            tabControl.Controls.Add(tabServer);
            tabControl.Controls.Add(tabNaming);
            tabControl.Location = new Point(19, 19);
            tabControl.Margin = new Padding(5);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(880, 928);
            tabControl.TabIndex = 0;
            // 
            // tabGeneral
            // 
            tabGeneral.Controls.Add(lblConfigName);
            tabGeneral.Controls.Add(txtConfigName);
            tabGeneral.Controls.Add(chkIsActive);
            tabGeneral.Location = new Point(4, 33);
            tabGeneral.Margin = new Padding(5);
            tabGeneral.Name = "tabGeneral";
            tabGeneral.Padding = new Padding(5);
            tabGeneral.Size = new Size(872, 891);
            tabGeneral.TabIndex = 0;
            tabGeneral.Text = "General";
            tabGeneral.UseVisualStyleBackColor = true;
            // 
            // lblConfigName
            // 
            lblConfigName.AutoSize = true;
            lblConfigName.Location = new Point(31, 48);
            lblConfigName.Margin = new Padding(5, 0, 5, 0);
            lblConfigName.Name = "lblConfigName";
            lblConfigName.Size = new Size(190, 24);
            lblConfigName.TabIndex = 0;
            lblConfigName.Text = "Configuration Name:";
            // 
            // txtConfigName
            // 
            txtConfigName.Location = new Point(31, 80);
            txtConfigName.Margin = new Padding(5);
            txtConfigName.MaxLength = 100;
            txtConfigName.Name = "txtConfigName";
            txtConfigName.Size = new Size(469, 30);
            txtConfigName.TabIndex = 1;
            // 
            // chkIsActive
            // 
            chkIsActive.AutoSize = true;
            chkIsActive.Checked = true;
            chkIsActive.CheckState = CheckState.Checked;
            chkIsActive.Location = new Point(31, 144);
            chkIsActive.Margin = new Padding(5);
            chkIsActive.Name = "chkIsActive";
            chkIsActive.Size = new Size(228, 28);
            chkIsActive.TabIndex = 2;
            chkIsActive.Text = "Configuration is active";
            chkIsActive.UseVisualStyleBackColor = true;
            // 
            // tabMySQL
            // 
            tabMySQL.Controls.Add(lblMySqlUsername);
            tabMySQL.Controls.Add(txtMySqlUsername);
            tabMySQL.Controls.Add(lblMySqlPassword);
            tabMySQL.Controls.Add(txtMySqlPassword);
            tabMySQL.Controls.Add(lblServiceName);
            tabMySQL.Controls.Add(txtServiceName);
            tabMySQL.Controls.Add(lblDataDirectory);
            tabMySQL.Controls.Add(txtDataDirectory);
            tabMySQL.Controls.Add(btnBrowseDataDirectory);
            tabMySQL.Controls.Add(lblMySqlHost);
            tabMySQL.Controls.Add(txtMySqlHost);
            tabMySQL.Controls.Add(lblMySqlPort);
            tabMySQL.Controls.Add(numMySqlPort);
            tabMySQL.Controls.Add(btnTestMySqlConnection);
            tabMySQL.Controls.Add(lblMySqlConnectionStatus);
            tabMySQL.Location = new Point(4, 33);
            tabMySQL.Margin = new Padding(5);
            tabMySQL.Name = "tabMySQL";
            tabMySQL.Padding = new Padding(5);
            tabMySQL.Size = new Size(872, 891);
            tabMySQL.TabIndex = 1;
            tabMySQL.Text = "MySQL";
            tabMySQL.UseVisualStyleBackColor = true;
            // 
            // lblMySqlUsername
            // 
            lblMySqlUsername.AutoSize = true;
            lblMySqlUsername.Location = new Point(31, 32);
            lblMySqlUsername.Margin = new Padding(5, 0, 5, 0);
            lblMySqlUsername.Name = "lblMySqlUsername";
            lblMySqlUsername.Size = new Size(100, 24);
            lblMySqlUsername.TabIndex = 0;
            lblMySqlUsername.Text = "Username:";
            // 
            // txtMySqlUsername
            // 
            txtMySqlUsername.Location = new Point(31, 64);
            txtMySqlUsername.Margin = new Padding(5);
            txtMySqlUsername.MaxLength = 100;
            txtMySqlUsername.Name = "txtMySqlUsername";
            txtMySqlUsername.Size = new Size(312, 30);
            txtMySqlUsername.TabIndex = 1;
            // 
            // lblMySqlPassword
            // 
            lblMySqlPassword.AutoSize = true;
            lblMySqlPassword.Location = new Point(31, 112);
            lblMySqlPassword.Margin = new Padding(5, 0, 5, 0);
            lblMySqlPassword.Name = "lblMySqlPassword";
            lblMySqlPassword.Size = new Size(95, 24);
            lblMySqlPassword.TabIndex = 2;
            lblMySqlPassword.Text = "Password:";
            // 
            // txtMySqlPassword
            // 
            txtMySqlPassword.Location = new Point(31, 144);
            txtMySqlPassword.Margin = new Padding(5);
            txtMySqlPassword.MaxLength = 100;
            txtMySqlPassword.Name = "txtMySqlPassword";
            txtMySqlPassword.Size = new Size(312, 30);
            txtMySqlPassword.TabIndex = 3;
            txtMySqlPassword.UseSystemPasswordChar = true;
            // 
            // lblServiceName
            // 
            lblServiceName.AutoSize = true;
            lblServiceName.Location = new Point(31, 192);
            lblServiceName.Margin = new Padding(5, 0, 5, 0);
            lblServiceName.Name = "lblServiceName";
            lblServiceName.Size = new Size(131, 24);
            lblServiceName.TabIndex = 4;
            lblServiceName.Text = "Service Name:";
            // 
            // txtServiceName
            // 
            txtServiceName.Location = new Point(31, 224);
            txtServiceName.Margin = new Padding(5);
            txtServiceName.MaxLength = 100;
            txtServiceName.Name = "txtServiceName";
            txtServiceName.Size = new Size(312, 30);
            txtServiceName.TabIndex = 5;
            txtServiceName.Text = "MySQL80";
            // 
            // lblDataDirectory
            // 
            lblDataDirectory.AutoSize = true;
            lblDataDirectory.Location = new Point(31, 272);
            lblDataDirectory.Margin = new Padding(5, 0, 5, 0);
            lblDataDirectory.Name = "lblDataDirectory";
            lblDataDirectory.Size = new Size(140, 24);
            lblDataDirectory.TabIndex = 6;
            lblDataDirectory.Text = "Data Directory:";
            // 
            // txtDataDirectory
            // 
            txtDataDirectory.Location = new Point(31, 304);
            txtDataDirectory.Margin = new Padding(5);
            txtDataDirectory.MaxLength = 500;
            txtDataDirectory.Name = "txtDataDirectory";
            txtDataDirectory.Size = new Size(548, 30);
            txtDataDirectory.TabIndex = 7;
            txtDataDirectory.Text = "C:\\ProgramData\\MySQL\\MySQL Server 8.0\\Data";
            // 
            // btnBrowseDataDirectory
            // 
            btnBrowseDataDirectory.Location = new Point(597, 304);
            btnBrowseDataDirectory.Margin = new Padding(5);
            btnBrowseDataDirectory.Name = "btnBrowseDataDirectory";
            btnBrowseDataDirectory.Size = new Size(129, 37);
            btnBrowseDataDirectory.TabIndex = 8;
            btnBrowseDataDirectory.Text = "Browse...";
            btnBrowseDataDirectory.UseVisualStyleBackColor = true;
            btnBrowseDataDirectory.Click += btnBrowseDataDirectory_Click;
            // 
            // lblMySqlHost
            // 
            lblMySqlHost.AutoSize = true;
            lblMySqlHost.Location = new Point(31, 352);
            lblMySqlHost.Margin = new Padding(5, 0, 5, 0);
            lblMySqlHost.Name = "lblMySqlHost";
            lblMySqlHost.Size = new Size(54, 24);
            lblMySqlHost.TabIndex = 9;
            lblMySqlHost.Text = "Host:";
            // 
            // txtMySqlHost
            // 
            txtMySqlHost.Location = new Point(31, 384);
            txtMySqlHost.Margin = new Padding(5);
            txtMySqlHost.MaxLength = 255;
            txtMySqlHost.Name = "txtMySqlHost";
            txtMySqlHost.Size = new Size(233, 30);
            txtMySqlHost.TabIndex = 10;
            // 
            // lblMySqlPort
            // 
            lblMySqlPort.AutoSize = true;
            lblMySqlPort.Location = new Point(314, 352);
            lblMySqlPort.Margin = new Padding(5, 0, 5, 0);
            lblMySqlPort.Name = "lblMySqlPort";
            lblMySqlPort.Size = new Size(50, 24);
            lblMySqlPort.TabIndex = 11;
            lblMySqlPort.Text = "Port:";
            // 
            // numMySqlPort
            // 
            numMySqlPort.Location = new Point(314, 384);
            numMySqlPort.Margin = new Padding(5);
            numMySqlPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            numMySqlPort.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numMySqlPort.Name = "numMySqlPort";
            numMySqlPort.Size = new Size(126, 30);
            numMySqlPort.TabIndex = 12;
            numMySqlPort.Value = new decimal(new int[] { 3306, 0, 0, 0 });
            // 
            // btnTestMySqlConnection
            // 
            btnTestMySqlConnection.Location = new Point(31, 432);
            btnTestMySqlConnection.Margin = new Padding(5);
            btnTestMySqlConnection.Name = "btnTestMySqlConnection";
            btnTestMySqlConnection.Size = new Size(189, 48);
            btnTestMySqlConnection.TabIndex = 13;
            btnTestMySqlConnection.Text = "Test Connection";
            btnTestMySqlConnection.UseVisualStyleBackColor = true;
            btnTestMySqlConnection.Click += btnTestMySqlConnection_Click;
            // 
            // lblMySqlConnectionStatus
            // 
            lblMySqlConnectionStatus.AutoSize = true;
            lblMySqlConnectionStatus.ForeColor = Color.Blue;
            lblMySqlConnectionStatus.Location = new Point(251, 445);
            lblMySqlConnectionStatus.Margin = new Padding(5, 0, 5, 0);
            lblMySqlConnectionStatus.Name = "lblMySqlConnectionStatus";
            lblMySqlConnectionStatus.Size = new Size(0, 24);
            lblMySqlConnectionStatus.TabIndex = 14;
            // 
            // tabServer
            // 
            tabServer.Controls.Add(lblServerIP);
            tabServer.Controls.Add(txtServerIP);
            tabServer.Controls.Add(lblServerPort);
            tabServer.Controls.Add(numServerPort);
            tabServer.Controls.Add(chkUseSSL);
            tabServer.Controls.Add(lblTargetDirectory);
            tabServer.Controls.Add(txtTargetDirectory);
            tabServer.Controls.Add(btnTestServerConnection);
            tabServer.Controls.Add(lblServerConnectionStatus);
            tabServer.Location = new Point(4, 33);
            tabServer.Margin = new Padding(5);
            tabServer.Name = "tabServer";
            tabServer.Padding = new Padding(5);
            tabServer.Size = new Size(872, 891);
            tabServer.TabIndex = 2;
            tabServer.Text = "Target Server";
            tabServer.UseVisualStyleBackColor = true;
            // 
            // lblServerIP
            // 
            lblServerIP.AutoSize = true;
            lblServerIP.Location = new Point(31, 32);
            lblServerIP.Margin = new Padding(5, 0, 5, 0);
            lblServerIP.Name = "lblServerIP";
            lblServerIP.Size = new Size(105, 24);
            lblServerIP.TabIndex = 0;
            lblServerIP.Text = "IP Address:";
            // 
            // txtServerIP
            // 
            txtServerIP.Location = new Point(31, 64);
            txtServerIP.Margin = new Padding(5);
            txtServerIP.MaxLength = 45;
            txtServerIP.Name = "txtServerIP";
            txtServerIP.Size = new Size(233, 30);
            txtServerIP.TabIndex = 1;
            // 
            // lblServerPort
            // 
            lblServerPort.AutoSize = true;
            lblServerPort.Location = new Point(314, 32);
            lblServerPort.Margin = new Padding(5, 0, 5, 0);
            lblServerPort.Name = "lblServerPort";
            lblServerPort.Size = new Size(50, 24);
            lblServerPort.TabIndex = 2;
            lblServerPort.Text = "Port:";
            // 
            // numServerPort
            // 
            numServerPort.Location = new Point(314, 64);
            numServerPort.Margin = new Padding(5);
            numServerPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            numServerPort.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numServerPort.Name = "numServerPort";
            numServerPort.Size = new Size(126, 30);
            numServerPort.TabIndex = 3;
            numServerPort.Value = new decimal(new int[] { 8080, 0, 0, 0 });
            // 
            // chkUseSSL
            // 
            chkUseSSL.AutoSize = true;
            chkUseSSL.Checked = true;
            chkUseSSL.CheckState = CheckState.Checked;
            chkUseSSL.Location = new Point(31, 112);
            chkUseSSL.Margin = new Padding(5);
            chkUseSSL.Name = "chkUseSSL";
            chkUseSSL.Size = new Size(101, 28);
            chkUseSSL.TabIndex = 4;
            chkUseSSL.Text = "Use SSL";
            chkUseSSL.UseVisualStyleBackColor = true;
            // 
            // lblTargetDirectory
            // 
            lblTargetDirectory.AutoSize = true;
            lblTargetDirectory.Location = new Point(31, 192);
            lblTargetDirectory.Margin = new Padding(5, 0, 5, 0);
            lblTargetDirectory.Name = "lblTargetDirectory";
            lblTargetDirectory.Size = new Size(155, 24);
            lblTargetDirectory.TabIndex = 5;
            lblTargetDirectory.Text = "Target Directory:";
            // 
            // txtTargetDirectory
            // 
            txtTargetDirectory.Location = new Point(31, 224);
            txtTargetDirectory.Margin = new Padding(5);
            txtTargetDirectory.MaxLength = 500;
            txtTargetDirectory.Name = "txtTargetDirectory";
            txtTargetDirectory.Size = new Size(626, 30);
            txtTargetDirectory.TabIndex = 6;
            // 
            // btnTestServerConnection
            // 
            btnTestServerConnection.Location = new Point(31, 272);
            btnTestServerConnection.Margin = new Padding(5);
            btnTestServerConnection.Name = "btnTestServerConnection";
            btnTestServerConnection.Size = new Size(189, 48);
            btnTestServerConnection.TabIndex = 7;
            btnTestServerConnection.Text = "Test Connection";
            btnTestServerConnection.UseVisualStyleBackColor = true;
            btnTestServerConnection.Click += btnTestServerConnection_Click;
            // 
            // lblServerConnectionStatus
            // 
            lblServerConnectionStatus.AutoSize = true;
            lblServerConnectionStatus.ForeColor = Color.Blue;
            lblServerConnectionStatus.Location = new Point(251, 285);
            lblServerConnectionStatus.Margin = new Padding(5, 0, 5, 0);
            lblServerConnectionStatus.Name = "lblServerConnectionStatus";
            lblServerConnectionStatus.Size = new Size(0, 24);
            lblServerConnectionStatus.TabIndex = 8;
            // 
            // tabNaming
            // 
            tabNaming.Controls.Add(lblNamingPattern);
            tabNaming.Controls.Add(txtNamingPattern);
            tabNaming.Controls.Add(lblDateFormat);
            tabNaming.Controls.Add(txtDateFormat);
            tabNaming.Controls.Add(chkIncludeServerName);
            tabNaming.Controls.Add(chkIncludeDatabaseName);
            tabNaming.Controls.Add(btnPreviewFileName);
            tabNaming.Controls.Add(lblFileNamePreview);
            tabNaming.Location = new Point(4, 33);
            tabNaming.Margin = new Padding(5);
            tabNaming.Name = "tabNaming";
            tabNaming.Padding = new Padding(5);
            tabNaming.Size = new Size(872, 891);
            tabNaming.TabIndex = 3;
            tabNaming.Text = "File Naming";
            tabNaming.UseVisualStyleBackColor = true;
            // 
            // lblNamingPattern
            // 
            lblNamingPattern.AutoSize = true;
            lblNamingPattern.Location = new Point(31, 32);
            lblNamingPattern.Margin = new Padding(5, 0, 5, 0);
            lblNamingPattern.Name = "lblNamingPattern";
            lblNamingPattern.Size = new Size(152, 24);
            lblNamingPattern.TabIndex = 0;
            lblNamingPattern.Text = "Naming Pattern:";
            // 
            // txtNamingPattern
            // 
            txtNamingPattern.Location = new Point(31, 64);
            txtNamingPattern.Margin = new Padding(5);
            txtNamingPattern.MaxLength = 200;
            txtNamingPattern.Name = "txtNamingPattern";
            txtNamingPattern.Size = new Size(626, 30);
            txtNamingPattern.TabIndex = 1;
            // 
            // lblDateFormat
            // 
            lblDateFormat.AutoSize = true;
            lblDateFormat.Location = new Point(31, 112);
            lblDateFormat.Margin = new Padding(5, 0, 5, 0);
            lblDateFormat.Name = "lblDateFormat";
            lblDateFormat.Size = new Size(122, 24);
            lblDateFormat.TabIndex = 2;
            lblDateFormat.Text = "Date Format:";
            // 
            // txtDateFormat
            // 
            txtDateFormat.Location = new Point(31, 144);
            txtDateFormat.Margin = new Padding(5);
            txtDateFormat.MaxLength = 50;
            txtDateFormat.Name = "txtDateFormat";
            txtDateFormat.Size = new Size(312, 30);
            txtDateFormat.TabIndex = 3;
            // 
            // chkIncludeServerName
            // 
            chkIncludeServerName.AutoSize = true;
            chkIncludeServerName.Checked = true;
            chkIncludeServerName.CheckState = CheckState.Checked;
            chkIncludeServerName.Location = new Point(31, 192);
            chkIncludeServerName.Margin = new Padding(5);
            chkIncludeServerName.Name = "chkIncludeServerName";
            chkIncludeServerName.Size = new Size(214, 28);
            chkIncludeServerName.TabIndex = 4;
            chkIncludeServerName.Text = "Include Server Name";
            chkIncludeServerName.UseVisualStyleBackColor = true;
            // 
            // chkIncludeDatabaseName
            // 
            chkIncludeDatabaseName.AutoSize = true;
            chkIncludeDatabaseName.Checked = true;
            chkIncludeDatabaseName.CheckState = CheckState.Checked;
            chkIncludeDatabaseName.Location = new Point(31, 240);
            chkIncludeDatabaseName.Margin = new Padding(5);
            chkIncludeDatabaseName.Name = "chkIncludeDatabaseName";
            chkIncludeDatabaseName.Size = new Size(242, 28);
            chkIncludeDatabaseName.TabIndex = 5;
            chkIncludeDatabaseName.Text = "Include Database Name";
            chkIncludeDatabaseName.UseVisualStyleBackColor = true;
            // 
            // btnPreviewFileName
            // 
            btnPreviewFileName.Location = new Point(31, 320);
            btnPreviewFileName.Margin = new Padding(5);
            btnPreviewFileName.Name = "btnPreviewFileName";
            btnPreviewFileName.Size = new Size(189, 48);
            btnPreviewFileName.TabIndex = 6;
            btnPreviewFileName.Text = "Preview Filename";
            btnPreviewFileName.UseVisualStyleBackColor = true;
            btnPreviewFileName.Click += btnPreviewFileName_Click;
            // 
            // lblFileNamePreview
            // 
            lblFileNamePreview.AutoSize = true;
            lblFileNamePreview.ForeColor = Color.Blue;
            lblFileNamePreview.Location = new Point(31, 384);
            lblFileNamePreview.Margin = new Padding(5, 0, 5, 0);
            lblFileNamePreview.Name = "lblFileNamePreview";
            lblFileNamePreview.Size = new Size(0, 24);
            lblFileNamePreview.TabIndex = 7;
            // 
            // btnSave
            // 
            btnSave.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnSave.Location = new Point(467, 976);
            btnSave.Margin = new Padding(5);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(141, 48);
            btnSave.TabIndex = 1;
            btnSave.Text = "Save";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += btnSave_Click;
            // 
            // btnValidateAndSave
            // 
            btnValidateAndSave.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnValidateAndSave.Location = new Point(624, 976);
            btnValidateAndSave.Margin = new Padding(5);
            btnValidateAndSave.Name = "btnValidateAndSave";
            btnValidateAndSave.Size = new Size(141, 48);
            btnValidateAndSave.TabIndex = 2;
            btnValidateAndSave.Text = "Validate && Save";
            btnValidateAndSave.UseVisualStyleBackColor = true;
            btnValidateAndSave.Click += btnValidateAndSave_Click;
            // 
            // btnCancel
            // 
            btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.Location = new Point(781, 976);
            btnCancel.Margin = new Padding(5);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(118, 48);
            btnCancel.TabIndex = 3;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;
            // 
            // openFileDialogDataDirectory
            // 
            openFileDialogDataDirectory.FileName = "openFileDialogDataDirectory";
            // 
            // ConfigurationForm
            // 
            AutoScaleDimensions = new SizeF(11F, 24F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(995, 1073);
            Controls.Add(tabControl);
            Controls.Add(btnSave);
            Controls.Add(btnValidateAndSave);
            Controls.Add(btnCancel);
            Margin = new Padding(5);
            MinimumSize = new Size(800, 600);
            Name = "ConfigurationForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Backup Configuration";
            tabControl.ResumeLayout(false);
            tabGeneral.ResumeLayout(false);
            tabGeneral.PerformLayout();
            tabMySQL.ResumeLayout(false);
            tabMySQL.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numMySqlPort).EndInit();
            tabServer.ResumeLayout(false);
            tabServer.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numServerPort).EndInit();
            tabNaming.ResumeLayout(false);
            tabNaming.PerformLayout();
            ResumeLayout(false);
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
        private OpenFileDialog openFileDialogDataDirectory;
    }
}