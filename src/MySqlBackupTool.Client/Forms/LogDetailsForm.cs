using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlBackupTool.Shared.Interfaces;
using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Client.Forms;

/// <summary>
/// Form for displaying detailed log information
/// </summary>
public partial class LogDetailsForm : Form
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LogDetailsForm> _logger;
    private readonly BackupLog _log;

    public LogDetailsForm(IServiceProvider serviceProvider, BackupLog log)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<LogDetailsForm>>();
        _log = log;

        InitializeComponent();
        InitializeForm();
    }

    private void InitializeForm()
    {
        try
        {
            this.Text = $"Log Details - {_log.Id}";
            this.Size = new Size(600, 400);
            this.StartPosition = FormStartPosition.CenterParent;

            LoadLogDetails();
            
            _logger.LogInformation("Log details form initialized for log {LogId}", _log.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing log details form");
            MessageBox.Show($"Error initializing form: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadLogDetails()
    {
        txtDetails.Text = $"Detailed view for log {_log.Id} would be implemented here.\n\n" +
                         $"This form would show comprehensive log information including:\n" +
                         $"- Full configuration details\n" +
                         $"- Transfer progress charts\n" +
                         $"- Error analysis\n" +
                         $"- Performance metrics\n" +
                         $"- Related logs and dependencies";
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        this.Close();
    }
}