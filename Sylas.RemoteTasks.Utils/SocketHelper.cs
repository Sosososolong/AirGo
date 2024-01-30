using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using System;

namespace Sylas.RemoteTasks.Utils
{
    /// <summary>
    /// Socket助手
    /// </summary>
    public static class SocketHelper
    {
        /// <summary>
        /// TCP扫描器, 检查服务器哪些端口是开放的
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="portStart"></param>
        /// <param name="portEnd"></param>
        /// <returns></returns>
        public static async Task ScanServerPortsAsync(string ip, int portStart, int portEnd)
        {
            var start = DateTime.Now;
            List<Task> tasks = [];

            for (int port = portStart; port < portEnd; port++)
            {
                tasks.Add(ConnectAsync(ip, port));
            }
            await Task.WhenAll(tasks);
            Console.WriteLine($"检测完毕, 耗时: {(DateTime.Now - start).TotalSeconds}/s");
        }
        static async Task ConnectAsync(string ip, int port)
        {
            var tcpClient = new TcpClient();
            try
            {
                await tcpClient.ConnectAsync(ip, port);
                Console.WriteLine($"\n!!!{port}-Open!!!\n");
            }
            catch (Exception)
            {
                //Console.WriteLine($"{port}-Close");
            }
        }
    }
}
