using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Client.Forms;

/// <summary>
/// Form for displaying backup summary reports
/// </summary>
public partial class ReportViewerForm : Form
{
    private readonly BackupSummaryReport _report;

    public ReportViewerForm(BackupSummaryReport report)
    {
        _report = report;
        InitializeComponent();
        InitializeForm();
    }

    private void InitializeForm()
    {
        this.Text = "Backup Summary Report";
        this.Size = new Size(800, 600);
        this.StartPosition = FormStartPosition.CenterParent;

        LoadReport();
    }

    private void LoadReport()
    {
        var reportText = $"BACKUP SUMMARY REPORT\n" +
                        $"Generated: {_report.GeneratedAt:yyyy-MM-dd HH:mm:ss}\n" +
                        $"Period: {_report.ReportStartDate:yyyy-MM-dd} to {_report.ReportEndDate:yyyy-MM-dd}\n\n" +
                        $"OVERALL STATISTICS:\n" +
                        $"Total Backups: {_report.OverallStatistics.TotalBackups}\n" +
                        $"Successful: {_report.OverallStatistics.SuccessfulBackups}\n" +
                        $"Failed: {_report.OverallStatistics.FailedBackups}\n" +
                        $"Cancelled: {_report.OverallStatistics.CancelledBackups}\n" +
                        $"Success Rate: {_report.OverallStatistics.SuccessRate:F1}%\n" +
                        $"Total Data Transferred: {FormatBytes(_report.OverallStatistics.TotalBytesTransferred)}\n" +
                        $"Average Backup Size: {FormatBytes((long)_report.OverallStatistics.AverageBackupSize)}\n" +
                        $"Total Duration: {_report.OverallStatistics.TotalDuration:hh\\:mm\\:ss}\n" +
                        $"Average Duration: {_report.OverallStatistics.AverageDuration:hh\\:mm\\:ss}\n\n";

        if (_report.ConfigurationStatistics.Any())
        {
            reportText += "CONFIGURATION BREAKDOWN:\n";
            foreach (var config in _report.ConfigurationStatistics)
            {
                reportText += $"\n{config.ConfigurationName}:\n" +
                             $"  Backups: {config.TotalBackups} (Success: {config.SuccessfulBackups}, Failed: {config.FailedBackups})\n" +
                             $"  Success Rate: {config.SuccessRate:F1}%\n" +
                             $"  Data Transferred: {FormatBytes(config.TotalBytesTransferred)}\n" +
                             $"  Average Duration: {config.AverageDuration:hh\\:mm\\:ss}\n";
                
                if (config.LastBackupTime.HasValue)
                {
                    reportText += $"  Last Backup: {config.LastBackupTime.Value:yyyy-MM-dd HH:mm:ss} ({config.LastBackupStatus})\n";
                }
            }
        }

        txtReport.Text = reportText;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";
        
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        this.Close();
    }

    private void btnExport_Click(object sender, EventArgs e)
    {
        try
        {
            using var saveDialog = new SaveFileDialog();
            saveDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
            saveDialog.FileName = $"backup_report_{_report.GeneratedAt:yyyyMMdd_HHmmss}.txt";

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                File.WriteAllText(saveDialog.FileName, txtReport.Text);
                MessageBox.Show($"Report exported successfully to:\n{saveDialog.FileName}", "Export Complete", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error exporting report: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}