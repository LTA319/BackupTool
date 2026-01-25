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
            // using var monitorForm = new BackupMonitorForm(_serviceProvider);
            // monitorForm.ShowDialog();
            // Test database connection before opening monitor form
            _ = TestDatabaseAndOpenMonitor();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening backup monitor");
            MessageBox.Show($"Error opening backup monitor: {ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task TestDatabaseAndOpenMonitor()
    {
        try
        {
            // Show loading message
            this.Cursor = Cursors.WaitCursor;
            this.Text = "MySQL Backup Tool - Client (Testing database connection...)";

            // Test database connection
            var testResult = await DatabaseConnectionTest.TestDatabaseConnectionAsync();

            if (!testResult.Success)
            {
                _logger.LogWarning("Database connection test failed: {Error}", testResult.Message);

                var result = MessageBox.Show(
                    $"Database connection test failed:\n\n{testResult.Message}\n\n" +
                    "Would you like to attempt automatic repair?",
                    "Database Connection Issue",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    this.Text = "MySQL Backup Tool - Client (Repairing database...)";
                    var repairResult = await DatabaseConnectionTest.RepairDatabaseAsync();

                    MessageBox.Show(repairResult.ToString(),
                        repairResult.Success ? "Database Repair Complete" : "Database Repair Failed",
                        MessageBoxButtons.OK,
                        repairResult.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);

                    if (!repairResult.Success)
                        return;
                }
                else if (result == DialogResult.Cancel)
                {
                    return;
                }
                // If No, continue anyway
            }
            else
            {
                _logger.LogInformation("Database connection test passed in {Time}ms",
                    testResult.TotalTime.TotalMilliseconds);
            }

            // Open monitor form
            using var monitorForm = new BackupMonitorForm(_serviceProvider);
            monitorForm.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during database test or opening backup monitor");
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            this.Cursor = Cursors.Default;
            this.Text = "MySQL Backup Tool - Client";
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

    private async void testDatabaseConnectionToolStripMenuItem_Click(object sender, EventArgs e)
    {
        try
        {
            this.Cursor = Cursors.WaitCursor;
            toolStripStatusLabel.Text = "Testing database connection...";

            var testResult = await DatabaseConnectionTest.TestDatabaseConnectionAsync();

            var form = new Form
            {
                Text = testResult.Success ? "Database Test - Success" : "Database Test - Failed",
                Size = new Size(600, 500),
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = true,
                MinimizeBox = false
            };

            var textBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9),
                ReadOnly = true,
                Text = testResult.ToString()
            };

            var buttonPanel = new Panel
            {
                Height = 40,
                Dock = DockStyle.Bottom
            };

            var closeButton = new Button
            {
                Text = "Close",
                Size = new Size(75, 23),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Location = new Point(buttonPanel.Width - 85, 8),
                DialogResult = DialogResult.OK
            };

            if (!testResult.Success)
            {
                var repairButton = new Button
                {
                    Text = "Repair Database",
                    Size = new Size(100, 23),
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                    Location = new Point(buttonPanel.Width - 195, 8)
                };

                repairButton.Click += async (s, args) =>
                {
                    try
                    {
                        repairButton.Enabled = false;
                        repairButton.Text = "Repairing...";

                        var repairResult = await DatabaseConnectionTest.RepairDatabaseAsync();
                        textBox.Text = repairResult.ToString();

                        if (repairResult.Success)
                        {
                            form.Text = "Database Repair - Success";
                            repairButton.Visible = false;
                        }
                        else
                        {
                            repairButton.Text = "Repair Database";
                            repairButton.Enabled = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error during repair: {ex.Message}", "Repair Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        repairButton.Text = "Repair Database";
                        repairButton.Enabled = true;
                    }
                };

                buttonPanel.Controls.Add(repairButton);
            }

            buttonPanel.Controls.Add(closeButton);
            form.Controls.Add(textBox);
            form.Controls.Add(buttonPanel);
            form.AcceptButton = closeButton;

            form.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing database connection");
            MessageBox.Show($"Error testing database connection: {ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            this.Cursor = Cursors.Default;
            toolStripStatusLabel.Text = "Ready";
        }
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

    private void lblWelcome_Click(object sender, EventArgs e)
    {

    }
}