using System;
using System.Diagnostics;
using System.Security.Principal;
namespace MySqlBackupTool.Shared.Tools;

public static class AdminHelper
{
    /// <summary>
    /// 检查当前进程是否以管理员身份运行
    /// </summary>
    public static bool IsRunningAsAdministrator()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// 如果当前不是管理员，则重启程序并请求管理员权限
    /// </summary>
    public static void RestartAsAdministratorIfNeeded()
    {
        if (!IsRunningAsAdministrator())
        {
            var exePath = Process.GetCurrentProcess().MainModule.FileName;
            var startInfo = new ProcessStartInfo(exePath)
            {
                Verb = "runas", // 请求管理员权限
                Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1)), // 传递原始参数
                UseShellExecute = true
            };

            try
            {
                Process.Start(startInfo);
                Environment.Exit(0); // 关闭当前非管理员进程
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // 用户拒绝了UAC提示
                Console.WriteLine("需要管理员权限才能停止MySQL服务。");
                Console.WriteLine("请以管理员身份重新运行此程序。");
                Console.WriteLine("按任意键退出...");

                Environment.Exit(1);
            }
        }
    }

}
