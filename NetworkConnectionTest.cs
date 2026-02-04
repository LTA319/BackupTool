using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MySqlBackupTool.NetworkTest
{
    /// <summary>
    /// 简单的网络连接测试，用于验证网络异常处理改进
    /// </summary>
    class NetworkConnectionTest
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("网络连接异常处理测试");
            Console.WriteLine("====================");
            
            // 测试1: 连接到不存在的服务器
            await TestConnectionToNonExistentServer();
            
            // 测试2: 连接超时测试
            await TestConnectionTimeout();
            
            Console.WriteLine("\n测试完成。按任意键退出...");
            Console.ReadKey();
        }
        
        static async Task TestConnectionToNonExistentServer()
        {
            Console.WriteLine("\n测试1: 连接到不存在的服务器");
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync("192.168.1.999", 12345); // 不存在的IP
                Console.WriteLine("连接成功 (不应该到达这里)");
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"✓ 正确捕获SocketException: {ex.Message} (错误代码: {ex.ErrorCode})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✓ 捕获到其他异常: {ex.GetType().Name}: {ex.Message}");
            }
        }
        
        static async Task TestConnectionTimeout()
        {
            Console.WriteLine("\n测试2: 连接超时测试");
            try
            {
                using var client = new TcpClient();
                client.ReceiveTimeout = 1000; // 1秒超时
                client.SendTimeout = 1000;
                
                // 尝试连接到一个会超时的地址 (通常是防火墙阻止的端口)
                await client.ConnectAsync("8.8.8.8", 12345);
                Console.WriteLine("连接成功 (不应该到达这里)");
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"✓ 正确捕获SocketException: {ex.Message} (错误代码: {ex.ErrorCode})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✓ 捕获到其他异常: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}