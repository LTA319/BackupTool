using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
            
            // TODO: Initialize UI components in later tasks
            
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