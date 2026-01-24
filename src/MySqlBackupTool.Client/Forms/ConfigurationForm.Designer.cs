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
            this.tabControl = new TabControl();
            this.tabGeneral = new TabPage();
            this.tabMySQL = new TabPage();
            this.tabServer = new TabPage();
            this.tabNaming = new TabPage();
            
            // General tab controls
            this.lblConfigName = new Label();
            this.txtConfigName = new TextBox();
            this.chkIsActive = new CheckBox();
            
            // MySQL tab controls
            this.lblMySqlUsername = new Label();
            this.txtMySqlUsername = new TextBox();
            this.lblMySqlPassword = new Label();
            this.txtMySqlPassword = new TextBox();
            this.lblServiceName = new Label();
            this.txtServiceName = new TextBox();
            this.lblDataDirectory = new Label();
            this.txtDataDirectory = new TextBox();
            this.btnBrowseDataDirectory = new Button();
            this.lblMySqlHost = new Label();
            this.txtMySqlHost = new TextBox();
            this.lblMySqlPort = new Label();
            this.numMySqlPort = new NumericUpDown();
            this.btnTestMySqlConnection = new Button();
            this.lblMySqlConnectionStatus = new Label();
            
            // Server tab controls
            this.lblServerIP = new Label();
            this.txtServerIP = new TextBox();
            this.lblServerPort = new Label();
            this.numServerPort = new NumericUpDown();
            this.chkUseSSL = new CheckBox();
            this.lblTargetDirectory = new Label();
            this.txtTargetDirectory = new TextBox();
            this.btnTestServerConnection = new Button();
            this.lblServerConnectionStatus = new Label();
            
            // Naming tab controls
            this.lblNamingPattern = new Label();
            this.txtNamingPattern = new TextBox();
            this.lblDateFormat = new Label();
            this.txtDateFormat = new TextBox();
            this.chkIncludeServerName = new CheckBox();
            this.chkIncludeDatabaseName = new CheckBox();
            this.btnPreviewFileName = new Button();
            this.lblFileNamePreview = new Label();
            
            // Bottom buttons
            this.btnSave = new Button();
            this.btnValidateAndSave = new Button();
            this.btnCancel = new Button();

            this.SuspendLayout();

            // 
            // tabControl
            // 
            this.tabControl.Controls.Add(this.tabGeneral);
            this.tabControl.Controls.Add(this.tabMySQL);
            this.tabControl.Controls.Add(this.tabServer);
            this.tabControl.Controls.Add(this.tabNaming);
            this.tabControl.Location = new Point(12, 12);
            this.tabControl.Size = new Size(560, 580);
            this.tabControl.SelectedIndex = 0;

            // 
            // tabGeneral
            // 
            this.tabGeneral.Controls.Add(this.lblConfigName);
            this.tabGeneral.Controls.Add(this.txtConfigName);
            this.tabGeneral.Controls.Add(this.chkIsActive);
            this.tabGeneral.Location = new Point(4, 24);
            this.tabGeneral.Size = new Size(552, 552);
            this.tabGeneral.Text = "General";
            this.tabGeneral.UseVisualStyleBackColor = true;

            // 
            // lblConfigName
            // 
            this.lblConfigName.AutoSize = true;
            this.lblConfigName.Location = new Point(20, 30);
            this.lblConfigName.Size = new Size(120, 15);
            this.lblConfigName.Text = "Configuration Name:";

            // 
            // txtConfigName
            // 
            this.txtConfigName.Location = new Point(20, 50);
            this.txtConfigName.Size = new Size(300, 23);
            this.txtConfigName.MaxLength = 100;

            // 
            // chkIsActive
            // 
            this.chkIsActive.AutoSize = true;
            this.chkIsActive.Location = new Point(20, 90);
            this.chkIsActive.Size = new Size(150, 19);
            this.chkIsActive.Text = "Configuration is active";
            this.chkIsActive.UseVisualStyleBackColor = true;
            this.chkIsActive.Checked = true;

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
            this.tabMySQL.Location = new Point(4, 24);
            this.tabMySQL.Size = new Size(552, 552);
            this.tabMySQL.Text = "MySQL";
            this.tabMySQL.UseVisualStyleBackColor = true;

            // MySQL controls layout
            int yPos = 20;
            int spacing = 50;

            // 
            // lblMySqlUsername
            // 
            this.lblMySqlUsername.AutoSize = true;
            this.lblMySqlUsername.Location = new Point(20, yPos);
            this.lblMySqlUsername.Size = new Size(65, 15);
            this.lblMySqlUsername.Text = "Username:";

            // 
            // txtMySqlUsername
            // 
            this.txtMySqlUsername.Location = new Point(20, yPos + 20);
            this.txtMySqlUsername.Size = new Size(200, 23);
            this.txtMySqlUsername.MaxLength = 100;

            yPos += spacing;

            // 
            // lblMySqlPassword
            // 
            this.lblMySqlPassword.AutoSize = true;
            this.lblMySqlPassword.Location = new Point(20, yPos);
            this.lblMySqlPassword.Size = new Size(60, 15);
            this.lblMySqlPassword.Text = "Password:";

            // 
            // txtMySqlPassword
            // 
            this.txtMySqlPassword.Location = new Point(20, yPos + 20);
            this.txtMySqlPassword.Size = new Size(200, 23);
            this.txtMySqlPassword.MaxLength = 100;
            this.txtMySqlPassword.UseSystemPasswordChar = true;

            yPos += spacing;

            // 
            // lblServiceName
            // 
            this.lblServiceName.AutoSize = true;
            this.lblServiceName.Location = new Point(20, yPos);
            this.lblServiceName.Size = new Size(85, 15);
            this.lblServiceName.Text = "Service Name:";

            // 
            // txtServiceName
            // 
            this.txtServiceName.Location = new Point(20, yPos + 20);
            this.txtServiceName.Size = new Size(200, 23);
            this.txtServiceName.MaxLength = 100;

            yPos += spacing;

            // 
            // lblDataDirectory
            // 
            this.lblDataDirectory.AutoSize = true;
            this.lblDataDirectory.Location = new Point(20, yPos);
            this.lblDataDirectory.Size = new Size(85, 15);
            this.lblDataDirectory.Text = "Data Directory:";

            // 
            // txtDataDirectory
            // 
            this.txtDataDirectory.Location = new Point(20, yPos + 20);
            this.txtDataDirectory.Size = new Size(350, 23);
            this.txtDataDirectory.MaxLength = 500;

            // 
            // btnBrowseDataDirectory
            // 
            this.btnBrowseDataDirectory.Location = new Point(380, yPos + 20);
            this.btnBrowseDataDirectory.Size = new Size(75, 23);
            this.btnBrowseDataDirectory.Text = "Browse...";
            this.btnBrowseDataDirectory.UseVisualStyleBackColor = true;
            this.btnBrowseDataDirectory.Click += new EventHandler(this.btnBrowseDataDirectory_Click);

            yPos += spacing;

            // 
            // lblMySqlHost
            // 
            this.lblMySqlHost.AutoSize = true;
            this.lblMySqlHost.Location = new Point(20, yPos);
            this.lblMySqlHost.Size = new Size(35, 15);
            this.lblMySqlHost.Text = "Host:";

            // 
            // txtMySqlHost
            // 
            this.txtMySqlHost.Location = new Point(20, yPos + 20);
            this.txtMySqlHost.Size = new Size(150, 23);
            this.txtMySqlHost.MaxLength = 255;

            // 
            // lblMySqlPort
            // 
            this.lblMySqlPort.AutoSize = true;
            this.lblMySqlPort.Location = new Point(200, yPos);
            this.lblMySqlPort.Size = new Size(32, 15);
            this.lblMySqlPort.Text = "Port:";

            // 
            // numMySqlPort
            // 
            this.numMySqlPort.Location = new Point(200, yPos + 20);
            this.numMySqlPort.Size = new Size(80, 23);
            this.numMySqlPort.Minimum = 1;
            this.numMySqlPort.Maximum = 65535;
            this.numMySqlPort.Value = 3306;

            yPos += spacing;

            // 
            // btnTestMySqlConnection
            // 
            this.btnTestMySqlConnection.Location = new Point(20, yPos);
            this.btnTestMySqlConnection.Size = new Size(120, 30);
            this.btnTestMySqlConnection.Text = "Test Connection";
            this.btnTestMySqlConnection.UseVisualStyleBackColor = true;
            this.btnTestMySqlConnection.Click += new EventHandler(this.btnTestMySqlConnection_Click);

            // 
            // lblMySqlConnectionStatus
            // 
            this.lblMySqlConnectionStatus.AutoSize = true;
            this.lblMySqlConnectionStatus.Location = new Point(160, yPos + 8);
            this.lblMySqlConnectionStatus.Size = new Size(0, 15);
            this.lblMySqlConnectionStatus.ForeColor = Color.Blue;

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
            this.tabServer.Location = new Point(4, 24);
            this.tabServer.Size = new Size(552, 552);
            this.tabServer.Text = "Target Server";
            this.tabServer.UseVisualStyleBackColor = true;

            // Server controls layout
            yPos = 20;

            // 
            // lblServerIP
            // 
            this.lblServerIP.AutoSize = true;
            this.lblServerIP.Location = new Point(20, yPos);
            this.lblServerIP.Size = new Size(70, 15);
            this.lblServerIP.Text = "IP Address:";

            // 
            // txtServerIP
            // 
            this.txtServerIP.Location = new Point(20, yPos + 20);
            this.txtServerIP.Size = new Size(150, 23);
            this.txtServerIP.MaxLength = 45;

            // 
            // lblServerPort
            // 
            this.lblServerPort.AutoSize = true;
            this.lblServerPort.Location = new Point(200, yPos);
            this.lblServerPort.Size = new Size(32, 15);
            this.lblServerPort.Text = "Port:";

            // 
            // numServerPort
            // 
            this.numServerPort.Location = new Point(200, yPos + 20);
            this.numServerPort.Size = new Size(80, 23);
            this.numServerPort.Minimum = 1;
            this.numServerPort.Maximum = 65535;
            this.numServerPort.Value = 8080;

            yPos += spacing;

            // 
            // chkUseSSL
            // 
            this.chkUseSSL.AutoSize = true;
            this.chkUseSSL.Location = new Point(20, yPos);
            this.chkUseSSL.Size = new Size(70, 19);
            this.chkUseSSL.Text = "Use SSL";
            this.chkUseSSL.UseVisualStyleBackColor = true;
            this.chkUseSSL.Checked = true;

            yPos += spacing;

            // 
            // lblTargetDirectory
            // 
            this.lblTargetDirectory.AutoSize = true;
            this.lblTargetDirectory.Location = new Point(20, yPos);
            this.lblTargetDirectory.Size = new Size(95, 15);
            this.lblTargetDirectory.Text = "Target Directory:";

            // 
            // txtTargetDirectory
            // 
            this.txtTargetDirectory.Location = new Point(20, yPos + 20);
            this.txtTargetDirectory.Size = new Size(400, 23);
            this.txtTargetDirectory.MaxLength = 500;

            yPos += spacing;

            // 
            // btnTestServerConnection
            // 
            this.btnTestServerConnection.Location = new Point(20, yPos);
            this.btnTestServerConnection.Size = new Size(120, 30);
            this.btnTestServerConnection.Text = "Test Connection";
            this.btnTestServerConnection.UseVisualStyleBackColor = true;
            this.btnTestServerConnection.Click += new EventHandler(this.btnTestServerConnection_Click);

            // 
            // lblServerConnectionStatus
            // 
            this.lblServerConnectionStatus.AutoSize = true;
            this.lblServerConnectionStatus.Location = new Point(160, yPos + 8);
            this.lblServerConnectionStatus.Size = new Size(0, 15);
            this.lblServerConnectionStatus.ForeColor = Color.Blue;

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
            this.tabNaming.Location = new Point(4, 24);
            this.tabNaming.Size = new Size(552, 552);
            this.tabNaming.Text = "File Naming";
            this.tabNaming.UseVisualStyleBackColor = true;

            // Naming controls layout
            yPos = 20;

            // 
            // lblNamingPattern
            // 
            this.lblNamingPattern.AutoSize = true;
            this.lblNamingPattern.Location = new Point(20, yPos);
            this.lblNamingPattern.Size = new Size(95, 15);
            this.lblNamingPattern.Text = "Naming Pattern:";

            // 
            // txtNamingPattern
            // 
            this.txtNamingPattern.Location = new Point(20, yPos + 20);
            this.txtNamingPattern.Size = new Size(400, 23);
            this.txtNamingPattern.MaxLength = 200;

            yPos += spacing;

            // 
            // lblDateFormat
            // 
            this.lblDateFormat.AutoSize = true;
            this.lblDateFormat.Location = new Point(20, yPos);
            this.lblDateFormat.Size = new Size(75, 15);
            this.lblDateFormat.Text = "Date Format:";

            // 
            // txtDateFormat
            // 
            this.txtDateFormat.Location = new Point(20, yPos + 20);
            this.txtDateFormat.Size = new Size(200, 23);
            this.txtDateFormat.MaxLength = 50;

            yPos += spacing;

            // 
            // chkIncludeServerName
            // 
            this.chkIncludeServerName.AutoSize = true;
            this.chkIncludeServerName.Location = new Point(20, yPos);
            this.chkIncludeServerName.Size = new Size(130, 19);
            this.chkIncludeServerName.Text = "Include Server Name";
            this.chkIncludeServerName.UseVisualStyleBackColor = true;
            this.chkIncludeServerName.Checked = true;

            yPos += 30;

            // 
            // chkIncludeDatabaseName
            // 
            this.chkIncludeDatabaseName.AutoSize = true;
            this.chkIncludeDatabaseName.Location = new Point(20, yPos);
            this.chkIncludeDatabaseName.Size = new Size(145, 19);
            this.chkIncludeDatabaseName.Text = "Include Database Name";
            this.chkIncludeDatabaseName.UseVisualStyleBackColor = true;
            this.chkIncludeDatabaseName.Checked = true;

            yPos += spacing;

            // 
            // btnPreviewFileName
            // 
            this.btnPreviewFileName.Location = new Point(20, yPos);
            this.btnPreviewFileName.Size = new Size(120, 30);
            this.btnPreviewFileName.Text = "Preview Filename";
            this.btnPreviewFileName.UseVisualStyleBackColor = true;
            this.btnPreviewFileName.Click += new EventHandler(this.btnPreviewFileName_Click);

            // 
            // lblFileNamePreview
            // 
            this.lblFileNamePreview.AutoSize = true;
            this.lblFileNamePreview.Location = new Point(20, yPos + 40);
            this.lblFileNamePreview.Size = new Size(0, 15);
            this.lblFileNamePreview.ForeColor = Color.Blue;

            // 
            // btnSave
            // 
            this.btnSave.Location = new Point(297, 610);
            this.btnSave.Size = new Size(90, 30);
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new EventHandler(this.btnSave_Click);

            // 
            // btnValidateAndSave
            // 
            this.btnValidateAndSave.Location = new Point(397, 610);
            this.btnValidateAndSave.Size = new Size(90, 30);
            this.btnValidateAndSave.Text = "Validate && Save";
            this.btnValidateAndSave.UseVisualStyleBackColor = true;
            this.btnValidateAndSave.Click += new EventHandler(this.btnValidateAndSave_Click);

            // 
            // btnCancel
            // 
            this.btnCancel.Location = new Point(497, 610);
            this.btnCancel.Size = new Size(75, 30);
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new EventHandler(this.btnCancel_Click);

            // 
            // ConfigurationForm
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(584, 661);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.btnValidateAndSave);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ConfigurationForm";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Backup Configuration";
            this.ResumeLayout(false);
        }

        #endregion

        private TabControl tabControl;
        private TabPage tabGeneral;
        private TabPage tabMySQL;
        private TabPage tabServer;
        private TabPage tabNaming;
        
        // General tab controls
        private Label lblConfigName;
        private TextBox txtConfigName;
        private CheckBox chkIsActive;
        
        // MySQL tab controls
        private Label lblMySqlUsername;
        private TextBox txtMySqlUsername;
        private Label lblMySqlPassword;
        private TextBox txtMySqlPassword;
        private Label lblServiceName;
        private TextBox txtServiceName;
        private Label lblDataDirectory;
        private TextBox txtDataDirectory;
        private Button btnBrowseDataDirectory;
        private Label lblMySqlHost;
        private TextBox txtMySqlHost;
        private Label lblMySqlPort;
        private NumericUpDown numMySqlPort;
        private Button btnTestMySqlConnection;
        private Label lblMySqlConnectionStatus;
        
        // Server tab controls
        private Label lblServerIP;
        private TextBox txtServerIP;
        private Label lblServerPort;
        private NumericUpDown numServerPort;
        private CheckBox chkUseSSL;
        private Label lblTargetDirectory;
        private TextBox txtTargetDirectory;
        private Button btnTestServerConnection;
        private Label lblServerConnectionStatus;
        
        // Naming tab controls
        private Label lblNamingPattern;
        private TextBox txtNamingPattern;
        private Label lblDateFormat;
        private TextBox txtDateFormat;
        private CheckBox chkIncludeServerName;
        private CheckBox chkIncludeDatabaseName;
        private Button btnPreviewFileName;
        private Label lblFileNamePreview;
        
        // Bottom buttons
        private Button btnSave;
        private Button btnValidateAndSave;
        private Button btnCancel;
    }
}