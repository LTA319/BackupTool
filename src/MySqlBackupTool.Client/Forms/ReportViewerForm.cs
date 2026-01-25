using MySqlBackupTool.Shared.Models;

namespace MySqlBackupTool.Client.Forms;

/// <summary>
/// 备份摘要报告查看器窗体
/// 提供备份摘要报告的显示和导出功能
/// </summary>
public partial class ReportViewerForm : Form
{
    #region 私有字段

    /// <summary>
    /// 要显示的备份摘要报告对象
    /// </summary>
    private readonly BackupSummaryReport _report;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化ReportViewerForm类的新实例
    /// </summary>
    /// <param name="report">要显示的备份摘要报告</param>
    public ReportViewerForm(BackupSummaryReport report)
    {
        _report = report;
        InitializeComponent();
        InitializeForm();
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 初始化窗体的基本设置和属性
    /// 设置窗体标题、大小、位置并加载报告内容
    /// </summary>
    private void InitializeForm()
    {
        this.Text = "备份摘要报告";
        this.Size = new Size(800, 600);
        this.StartPosition = FormStartPosition.CenterParent;

        LoadReport();
    }

    /// <summary>
    /// 加载并格式化报告内容
    /// 将报告数据格式化为可读的文本格式并显示在文本框中
    /// </summary>
    private void LoadReport()
    {
        var reportText = $"备份摘要报告\n" +
                        $"生成时间: {_report.GeneratedAt:yyyy-MM-dd HH:mm:ss}\n" +
                        $"报告期间: {_report.ReportStartDate:yyyy-MM-dd} 至 {_report.ReportEndDate:yyyy-MM-dd}\n\n" +
                        $"总体统计:\n" +
                        $"总备份数: {_report.OverallStatistics.TotalBackups}\n" +
                        $"成功: {_report.OverallStatistics.SuccessfulBackups}\n" +
                        $"失败: {_report.OverallStatistics.FailedBackups}\n" +
                        $"取消: {_report.OverallStatistics.CancelledBackups}\n" +
                        $"成功率: {_report.OverallStatistics.SuccessRate:F1}%\n" +
                        $"总传输数据: {FormatBytes(_report.OverallStatistics.TotalBytesTransferred)}\n" +
                        $"平均备份大小: {FormatBytes((long)_report.OverallStatistics.AverageBackupSize)}\n" +
                        $"总持续时间: {_report.OverallStatistics.TotalDuration:hh\\:mm\\:ss}\n" +
                        $"平均持续时间: {_report.OverallStatistics.AverageDuration:hh\\:mm\\:ss}\n\n";

        if (_report.ConfigurationStatistics.Any())
        {
            reportText += "配置详细统计:\n";
            foreach (var config in _report.ConfigurationStatistics)
            {
                reportText += $"\n{config.ConfigurationName}:\n" +
                             $"  备份数: {config.TotalBackups} (成功: {config.SuccessfulBackups}, 失败: {config.FailedBackups})\n" +
                             $"  成功率: {config.SuccessRate:F1}%\n" +
                             $"  传输数据: {FormatBytes(config.TotalBytesTransferred)}\n" +
                             $"  平均持续时间: {config.AverageDuration:hh\\:mm\\:ss}\n";
                
                if (config.LastBackupTime.HasValue)
                {
                    reportText += $"  最后备份: {config.LastBackupTime.Value:yyyy-MM-dd HH:mm:ss} ({config.LastBackupStatus})\n";
                }
            }
        }

        txtReport.Text = reportText;
    }

    /// <summary>
    /// 格式化字节数为可读的文件大小格式
    /// </summary>
    /// <param name="bytes">字节数</param>
    /// <returns>格式化的文件大小字符串</returns>
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

    #endregion

    #region 事件处理程序

    /// <summary>
    /// 关闭按钮点击事件处理程序
    /// 关闭报告查看器窗体
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void btnClose_Click(object sender, EventArgs e)
    {
        this.Close();
    }

    /// <summary>
    /// 导出按钮点击事件处理程序
    /// 将报告内容导出到文本文件
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void btnExport_Click(object sender, EventArgs e)
    {
        try
        {
            using var saveDialog = new SaveFileDialog();
            saveDialog.Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
            saveDialog.FileName = $"backup_report_{_report.GeneratedAt:yyyyMMdd_HHmmss}.txt";

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                File.WriteAllText(saveDialog.FileName, txtReport.Text);
                MessageBox.Show($"报告导出成功到:\n{saveDialog.FileName}", "导出完成", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出报告时发生错误: {ex.Message}", "导出错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    #endregion
}