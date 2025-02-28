using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Net;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Threading;
using Sylas.RemoteTasks.Common;

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

        #region 发送数据
        private const string _heartbeatMsg = "keep-alive";
        /// <summary>
        /// 发送文本数据
        /// </summary>
        /// <param name="source"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static async Task<int> SendTextAsync(this Socket source, string msg)
        {
            return await source.SendAsync(Encoding.UTF8.GetBytes(msg).AsMemory(), SocketFlags.None);
        }
        /// <summary>
        /// 通知服务端关闭并释放socket
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static async Task<int> NotifyCloseAsync(this Socket source)
        {
            return await source.SendTextAsync("-1");
        }

        /// <summary>
        /// 将字节数组还原为文本
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <param name="byteCount">字节数组中有效的字节长度</param>
        /// <returns></returns>
        public static string GetText(this byte[] bytes, int byteCount)
        {
            return Encoding.UTF8.GetString(bytes, 0, byteCount).Replace("\0", string.Empty);
        }

        /// <summary>
        /// socket接收所有文本
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="buffer"></param>
        /// <param name="isEndFlag"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="afterSocketDisposed">关闭socket后的逻辑</param>
        /// <returns></returns>
        public static async Task<(string, int)> ReceiveAllTextAsync(this Socket socket, byte[] buffer, Func<byte[], bool> isEndFlag, Action? afterSocketDisposed = null, CancellationToken cancellationToken = default)
        {
            string text = string.Empty;
            byte[]? lastBytes = null;
            int readCount = 0;
            int allReceivedLength = 0;
            while (true)
            {
                readCount++;
                if (readCount % 100 == 0)
                {
                    LoggerHelper.LogCritical($"接受文本信息: 第{readCount}次");
                }
                // 当cancellationToken被设为取消状态时, 这里会抛出OperationCanceledException
                int receivedLength = await socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
                lastBytes = buffer.Take(receivedLength).ToArray();
                allReceivedLength += receivedLength;
                if (receivedLength == 0)
                {
                    if (afterSocketDisposed is not null)
                    {
                        afterSocketDisposed();
                    }
                    return (string.Empty, 0);
                }
                else if (readCount == 1 && receivedLength < 100)
                {
                    text = buffer.GetText(receivedLength);
                    if (text.EndsWith("000000"))
                    {
                        text = text[..^6];
                    }
                    text = RemoveMsgReadyPart(text);
                    return (text, allReceivedLength);
                }
                else
                {
                    // 比如数据为: "data1000000data2000000data3000000data4000000...datan000000"
                    if (receivedLength < 6)
                    {
                        // 上一次已经传过来了prefixPartLength位结束符
                        int prefixPartLength = 6 - receivedLength;
                        var prefixPart = lastBytes.Skip(lastBytes.Length - prefixPartLength).Take(prefixPartLength);
                        var last6Bytes = prefixPart.Concat(buffer.Take(receivedLength)).ToArray();
                        if (isEndFlag(last6Bytes))
                        {
                            // 去掉上一次传过来的结束符
                            text = text[..^prefixPartLength];
                            text = RemoveMsgReadyPart(text);
                            return (text, allReceivedLength);
                        }
                        else
                        {
                            LoggerHelper.LogCritical($"最后6个字节{string.Join(',', last6Bytes)}不是结束符");
                            text += buffer.GetText(receivedLength);
                        }
                    }
                    else
                    {
                        var last6Bytes = buffer.Skip(receivedLength - 6).Take(6).ToArray();
                        if (isEndFlag(last6Bytes))
                        {
                            text += buffer.GetText(receivedLength - 6);
                            text = RemoveMsgReadyPart(text);
                            return (text, allReceivedLength);
                        }
                        else
                        {
                            text += buffer.GetText(receivedLength);
                        }
                    }
                }
            }
        }
        static string RemoveMsgReadyPart(string msg)
        {
            const string readyMsg = "ready_for_new";
            if (msg != readyMsg && msg.Length > readyMsg.Length && msg.StartsWith(readyMsg))
            {
                return msg.Replace(readyMsg, string.Empty);
            }
            return msg;
        }
        /// <summary>
        /// 检查客户端是否请求关闭连接
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="realLength"></param>
        /// <param name="bufferParameters"></param>
        /// <returns></returns>
        public static async Task<bool> CheckIsCloseMsgAsync(this Socket socket, int realLength, byte[] bufferParameters)
        {
            if (realLength == 0)
            {
                socket.Close();
                socket.Dispose();
                return true;
            }
            // '-': 45; '1': 49.    "-1": [45,49]
            if (realLength == 2 && bufferParameters[0] == 45 && bufferParameters[1] == 49)
            {
                // 收到"-1", 发送"-1"(通知客户端可关闭连接)后关闭连接(客户端先关闭socket可能会抛异常; 所以客户端通知服务端关闭连接(-1)后, 服务端发送最后一条消息然后先关闭socket连接, 客户端等待此消息后再关闭连接)
                await socket.SendTextAsync("-1");
                socket.Close();
                socket.Dispose();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 是否是心跳消息
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static bool CheckIsKeepAliveMsg(string msg)
        {
            return msg == _heartbeatMsg || msg.Replace(_heartbeatMsg, string.Empty).Length == 0;
        }
        /// <summary>
        /// 是否是心跳消息
        /// </summary>
        /// <param name="realLength"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public static bool CheckIsKeepAliveMsg(int realLength, byte[] buffer)
        {
            return realLength == 10
                && buffer[0] == 107 && buffer[1] == 101 && buffer[2] == 101 && buffer[3] == 112 && buffer[4] == 45
                && buffer[5] == 97 && buffer[6] == 108 && buffer[7] == 105 && buffer[8] == 118 && buffer[9] == 101;
        }
        #endregion

        #region Common
        static void Log(ILogger? logger, string msg, LogLevel logLevel)
        {
            if (logger is null)
            {
                switch (logLevel)
                {
                    //case LogLevel.Trace:
                    //    break;
                    //case LogLevel.Debug:
                    //    break;
                    case LogLevel.Information:
                        LoggerHelper.LogInformation(msg);
                        break;
                    //case LogLevel.Warning:
                    //    break;
                    case LogLevel.Error:
                        LoggerHelper.LogError(msg);
                        break;
                    case LogLevel.Critical:
                        LoggerHelper.LogCritical(msg);
                        break;
                    //case LogLevel.None:
                    //    break;
                    default:
                        LoggerHelper.LogInformation(msg);
                        break;
                }
            }
            else
            {
                switch (logLevel)
                {
                    //case LogLevel.Trace:
                    //    break;
                    //case LogLevel.Debug:
                    //    break;
                    case LogLevel.Information:
                        logger.LogInformation(msg);
                        break;
                    //case LogLevel.Warning:
                    //    break;
                    case LogLevel.Error:
                        logger.LogError(msg);
                        break;
                    case LogLevel.Critical:
                        logger.LogCritical(msg);
                        break;
                    //case LogLevel.None:
                    //    break;
                    default:
                        logger.LogInformation(msg);
                        break;
                }
            }
        }
        #endregion
    }
}
