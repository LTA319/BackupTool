using System;
using System.Diagnostics;
using System.Security.Principal;

namespace MySqlBackupTool.Shared.Tools;

/// <summary>
/// Windows管理员权限辅助工具类
/// 提供检查和获取管理员权限的功能，主要用于需要管理员权限的操作
/// </summary>
/// <remarks>
/// 该类主要用于以下场景：
/// 1. 检查当前进程是否以管理员身份运行
/// 2. 在需要时请求管理员权限并重启应用程序
/// 3. 支持MySQL服务的启动和停止操作（需要管理员权限）
/// 
/// 注意：该类仅适用于Windows操作系统
/// </remarks>
public static class AdminHelper
{
    /// <summary>
    /// 检查当前进程是否以管理员身份运行
    /// </summary>
    /// <returns>如果当前进程具有管理员权限返回true，否则返回false</returns>
    /// <remarks>
    /// 该方法通过检查当前Windows身份是否属于管理员角色来判断权限级别
    /// 在Windows Vista及更高版本中，即使用户是管理员组成员，
    /// 程序也可能以标准用户权限运行（UAC机制）
    /// 
    /// 使用场景：
    /// - 在执行需要管理员权限的操作前进行检查
    /// - 决定是否显示需要管理员权限的功能
    /// - 在应用程序启动时验证权限级别
    /// </remarks>
    /// <example>
    /// <code>
    /// if (AdminHelper.IsRunningAsAdministrator())
    /// {
    ///     // 执行需要管理员权限的操作
    ///     StopMySQLService();
    /// }
    /// else
    /// {
    ///     // 提示用户需要管理员权限或请求权限提升
    ///     MessageBox.Show("此操作需要管理员权限");
    /// }
    /// </code>
    /// </example>
    public static bool IsRunningAsAdministrator()
    {
        // 获取当前Windows用户身份
        var identity = WindowsIdentity.GetCurrent();
        
        // 创建Windows主体对象
        var principal = new WindowsPrincipal(identity);
        
        // 检查是否属于管理员内置角色
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// 如果当前进程不是以管理员身份运行，则请求管理员权限并重启程序
    /// </summary>
    /// <remarks>
    /// 该方法执行以下操作：
    /// 1. 检查当前是否已经具有管理员权限
    /// 2. 如果没有管理员权限，则：
    ///    - 获取当前可执行文件路径
    ///    - 使用"runas"动词启动新的进程实例（触发UAC提示）
    ///    - 传递原始命令行参数到新进程
    ///    - 关闭当前非管理员进程
    /// 3. 如果用户拒绝UAC提示，则显示错误信息并退出
    /// 
    /// 注意事项：
    /// - 该方法会导致应用程序重启，当前进程的所有状态都会丢失
    /// - 用户可能会拒绝UAC提示，导致权限提升失败
    /// - 只有在确实需要管理员权限时才应该调用此方法
    /// </remarks>
    /// <example>
    /// <code>
    /// // 在需要管理员权限的操作前调用
    /// if (!AdminHelper.IsRunningAsAdministrator())
    /// {
    ///     AdminHelper.RestartAsAdministratorIfNeeded();
    ///     return; // 这行代码不会执行，因为进程会重启
    /// }
    /// 
    /// // 执行需要管理员权限的操作
    /// StopMySQLService();
    /// </code>
    /// </example>
    /// <exception cref="System.ComponentModel.Win32Exception">
    /// 当用户拒绝UAC提示或系统无法启动新进程时抛出
    /// </exception>
    public static void RestartAsAdministratorIfNeeded()
    {
        // 如果已经是管理员，则无需重启
        if (!IsRunningAsAdministrator())
        {
            // 获取当前可执行文件的完整路径
            var exePath = Process.GetCurrentProcess().MainModule.FileName;
            
            // 配置新进程的启动信息
            var startInfo = new ProcessStartInfo(exePath)
            {
                Verb = "runas", // 请求管理员权限的关键设置
                // 传递原始命令行参数（跳过第一个参数，因为它是程序路径）
                Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1)), 
                UseShellExecute = true // 必须为true才能使用"runas"动词
            };

            try
            {
                // 启动新的管理员权限进程
                Process.Start(startInfo);
                
                // 关闭当前非管理员进程
                // 退出代码0表示正常退出
                Environment.Exit(0);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // 用户拒绝了UAC提示或其他系统错误
                Console.WriteLine("需要管理员权限才能停止MySQL服务。");
                Console.WriteLine("请以管理员身份重新运行此程序。");
                Console.WriteLine("按任意键退出...");

                // 等待用户按键后退出
                // 退出代码1表示异常退出
                Environment.Exit(1);
            }
        }
    }
}
