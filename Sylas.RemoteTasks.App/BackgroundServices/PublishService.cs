using System.Net.Sockets;
using System.Net;
using System.Text;
using System;
using Sylas.RemoteTasks.App.Utils;

namespace Sylas.RemoteTasks.App.BackgroundServices
{
    public class PublishService : BackgroundService
    {
        private const int bufferSize = 1024 * 1024;
        private readonly byte ZeroByteValue = Encoding.UTF8.GetBytes("0").First();
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _ = Task.Factory.StartNew(() =>
            {
                // 指定socket对象为IPV4的,流传输,TCP协议
                var tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPAddress iPAddress = IPAddress.Parse("0.0.0.0");
                var iPEndPoint = new IPEndPoint(iPAddress, 8989);

                tcpSocket.Bind(iPEndPoint);
                tcpSocket.Listen(128);

                var configure = ServiceLocator.Instance.GetService<IConfiguration>();
                var serverSaveFileDir = configure?["Upload:SaveDir"];

                while (true)
                {
                    // 无限循环等待客户端的连接
                    var socketForClient = tcpSocket.Accept();
                    
                    #region 连接到客户端就交给子线程处理, 然后马上继续循环过来等待其他客户端连接
                    Task.Factory.StartNew(async () =>
                    {
                        byte[] buffer = new byte[bufferSize];

                        // 参数协议: 第一次发的数据确定任务及参数
                        byte[] bufferParameters = new byte[1024];

                        var socketForClientClosed = false;
                        while (!socketForClientClosed)
                        {
                            // 服务端已经准备好处理文件字节; 提醒客户端可以发送文件字节过来了
                            await socketForClient.SendAsync(Encoding.UTF8.GetBytes("111111").AsMemory(), SocketFlags.None);
                            int realLength = socketForClient.Receive(bufferParameters);
                            // 获取客户端传过来的字节数组放到bufferParameters中, 字节数组bufferParameters剩余的位置将会用0填充, 0经UTF-8反编码为字符串即"\0"
                            var parametersContent = Encoding.UTF8.GetString(bufferParameters, 0, realLength).Replace("\0", string.Empty);
                            var parametersArray = parametersContent.Split(";;;;");
                            var type = parametersArray[0];
                            string fileName = string.Empty;
                            var fileSaveDir = string.Empty;
                            switch (type)
                            {
                                // type为1: 发送文件; 获取第二个参数即文件名; 第三个参数表示当前服务器保存文件的目录(不传的话服务器自己需要配置Upload:SaveDir)
                                case "1":
                                    fileName = parametersArray[1];
                                    if (string.IsNullOrWhiteSpace(fileName))
                                    {
                                        Console.WriteLine($"传输文件名为空, 文件传输提前结束");
                                        socketForClient.Close();
                                        socketForClient.Dispose();
                                        return;
                                    }
                                    Console.WriteLine($"即将传输文件: {fileName}");
                                    // 优先取客户端配置
                                    fileSaveDir = parametersArray[2];
                                    if (string.IsNullOrWhiteSpace(fileSaveDir))
                                    {
                                        fileSaveDir = serverSaveFileDir;
                                    }

                                    // 任务参数解析完毕, 客户端可以开始发送文件的字节了
                                    await socketForClient.SendAsync(Encoding.UTF8.GetBytes("000000").AsMemory(), SocketFlags.None); // SocketFlags.Broadcast

                                    if (string.IsNullOrWhiteSpace(fileSaveDir))
                                    {
                                        // 客户端和服务端都没有配置服务器保存文件的地址则抛出异常
                                        throw new Exception("请配置上传文件保存的位置");
                                    }
                                    var file = Path.GetFullPath(Path.Combine(fileSaveDir, fileName));
                                    var fileDir = Path.GetDirectoryName(file);
                                    if (string.IsNullOrWhiteSpace(fileDir))
                                    {
                                        throw new Exception($"获取文件[{file}]的目录失败");
                                    }
                                    if (!Directory.Exists(fileDir))
                                    {
                                        Directory.CreateDirectory(fileDir);
                                    }
                                    using (var fileStream = new FileStream(file, FileMode.Create))
                                    {
                                        while (true)
                                        {
                                            // 阻塞等待, 一直等到有字节发送过来或者客户端关闭socket连接
                                            realLength = socketForClient.Receive(buffer);
                                            Console.WriteLine($"接收到字节: {realLength}");
                                            if (realLength > 0)
                                            {
                                                if (realLength == 6 && buffer[0] == ZeroByteValue && buffer[1] == ZeroByteValue && buffer[2] == ZeroByteValue && buffer[3] == ZeroByteValue && buffer[4] == ZeroByteValue && buffer[5] == ZeroByteValue)
                                                {
                                                    // 一个文件上传完毕, 即将上传新的文件
                                                    break;
                                                }
                                                else
                                                {
                                                    // 正在上传文件的字节
                                                    await fileStream.WriteAsync(buffer.AsMemory(0, realLength));
                                                    // 处理完告诉客户端继续发送
                                                    await socketForClient.SendAsync(Encoding.UTF8.GetBytes("111111"));
                                                }
                                            }
                                            else
                                            {
                                                // 客户端所有文件上传完毕后关闭socket连接, 结束
                                                Console.WriteLine($"接收到字节: {realLength}; 上传完毕!");
                                                socketForClient.Close();
                                                socketForClient.Dispose();
                                                socketForClientClosed = true;
                                                break;
                                            }
                                        }
                                    }
                                    break;
                                default:
                                    break;
                            }
                        }
                    });
                    #endregion
                }


            }, stoppingToken);
            Console.WriteLine("发布服务已经开启");
            return Task.CompletedTask;
        }
    }
}
