using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Client.Forms;

namespace MySqlBackupTool.Client;

/// <summary>
/// Main form for the MySQL Backup Tool Client
/// </summary>
public partial class FormMain : Form
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FormMain> _logger;

    public FormMain(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<FormMain>>();
        
        InitializeComponent();
        InitializeApplication();
    }

    private void InitializeApplication()
    {
        try
        {
            _logger.LogInformation("Initializing main form");
            
            // Set form properties
            this.Text = "MySQL Backup Tool - Client";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            
            _logger.LogInformation("Main form initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing main form");
            MessageBox.Show($"Error initializing application: {ex.Message}", 
                "Initialization Error", 
                MessageBoxButtons.OK, 
                MessageBoxIcon.Error);
        }
    }

    private void configurationToolStripMenuItem_Click(object sender, EventArgs e)
    {
        try
        {
            using var configListForm = new ConfigurationListForm(_serviceProvider);
            configListForm.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening configuration management");
            MessageBox.Show($"Error opening configuration management: {ex.Message}", 
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void backupMonitorToolStripMenuItem_Click(object sender, EventArgs e)
    {
        try
        {
            using var monitorForm = new BackupMonitorForm(_serviceProvider);
            monitorForm.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening backup monitor");
            MessageBox.Show($"Error opening backup monitor: {ex.Message}", 
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void logBrowserToolStripMenuItem_Click(object sender, EventArgs e)
    {
        try
        {
            using var logBrowserForm = new LogBrowserForm(_serviceProvider);
            logBrowserForm.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening log browser");
            MessageBox.Show($"Error opening log browser: {ex.Message}", 
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void exitToolStripMenuItem_Click(object sender, EventArgs e)
    {
        this.Close();
    }

    private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
    {
        MessageBox.Show("MySQL Backup Tool - Client\n\nA distributed backup solution for MySQL databases.\n\nVersion 1.0", 
            "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        try
        {
            _logger.LogInformation("Application closing");
            base.OnFormClosing(e);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during application shutdown");
        }
    }
}