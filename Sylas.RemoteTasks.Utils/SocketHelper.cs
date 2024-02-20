using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Net;
using System.Text.RegularExpressions;
using System.Linq;

namespace Sylas.RemoteTasks.Utils
{
    /// <summary>
    /// Socket助手
    /// </summary>
    public static class SocketHelper
    {
        #region TCP扫描器
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
        #endregion

        #region 远程唤醒
        /// <summary>
        /// 唤醒指定机器
        /// </summary>
        /// <param name="macAddress"></param>
        /// <param name="ipAddress"></param>
        /// <returns></returns>
        public static async Task WakeOnLan(string macAddress, string ipAddress)
        {
            byte[] magicPacket = BuildMagicPacket(macAddress);
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces().Where((n) =>
                n.NetworkInterfaceType != NetworkInterfaceType.Loopback && n.OperationalStatus == OperationalStatus.Up))
            {
                IPInterfaceProperties iPInterfaceProperties = networkInterface.GetIPProperties();
                foreach (MulticastIPAddressInformation multicastIPAddressInformation in iPInterfaceProperties.MulticastAddresses)
                {
                    IPAddress multicastIpAddress = multicastIPAddressInformation.Address;
                    if (multicastIpAddress.ToString().StartsWith("ff02::1%", StringComparison.OrdinalIgnoreCase)) // Ipv6: All hosts on LAN (with zone index)
                    {
                        UnicastIPAddressInformation unicastIPAddressInformation = iPInterfaceProperties
                            .UnicastAddresses
                            .Where((u) => u.Address.AddressFamily == AddressFamily.InterNetworkV6 && !u.Address.IsIPv6LinkLocal)
                            .FirstOrDefault();
                        if (unicastIPAddressInformation != null)
                        {
                            await SendWakeOnLan(unicastIPAddressInformation.Address, multicastIpAddress, magicPacket);
                            break;
                        }
                    }
                    else if (multicastIpAddress.ToString().Equals(ipAddress)) // Ipv4: All hosts on LAN
                    {
                        UnicastIPAddressInformation unicastIPAddressInformation = iPInterfaceProperties
                            .UnicastAddresses
                            .Where((u) => u.Address.AddressFamily == AddressFamily.InterNetwork && !iPInterfaceProperties.GetIPv4Properties().IsAutomaticPrivateAddressingActive)
                            .FirstOrDefault();
                        if (unicastIPAddressInformation != null)
                        {
                            await SendWakeOnLan(unicastIPAddressInformation.Address, multicastIpAddress, magicPacket);
                            break;
                        }
                    }
                }
            }
        }
        static byte[] BuildMagicPacket(string macAddress) // MacAddress in any standard HEX format
        {
            macAddress = Regex.Replace(macAddress, "[: -]", "");
            byte[] macBytes = new byte[6];
            for (int i = 0; i < 6; i++)
            {
                macBytes[i] = Convert.ToByte(macAddress.Substring(i * 2, 2), 16);
            }
            using MemoryStream ms = new();
            using (BinaryWriter bw = new(ms))
            {
                for (int i = 0; i < 6; i++)  //First 6 times 0xff
                {
                    bw.Write((byte)0xff);
                }
                for (int i = 0; i < 16; i++) // then 16 times MacAddress
                {
                    bw.Write(macBytes);
                }
            }
            return ms.ToArray(); // 102 bytes magic packet
        }
        static async Task SendWakeOnLan(IPAddress localIpAddress, IPAddress multicastIpAddress, byte[] magicPacket)
        {
            using UdpClient client = new(new IPEndPoint(localIpAddress, 0));
            await client.SendAsync(magicPacket, magicPacket.Length, multicastIpAddress.ToString(), 9);
        }
        #endregion
    }
}
