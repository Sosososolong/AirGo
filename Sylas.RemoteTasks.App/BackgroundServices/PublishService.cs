using Sylas.RemoteTasks.App.Utils;
using System.Net;
using System.Net.Sockets;
using System.Text;

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
                        var uploadedFileCount = 1;
                        while (!socketForClientClosed)
                        {
                            // 服务端已经准备好处理文件字节; 提醒客户端可以发送文件字节过来了
                            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} READY 准备接收新的文件");
                            await socketForClient.SendAsync(Encoding.UTF8.GetBytes("ready").AsMemory(), SocketFlags.None);
                            int realLength = await socketForClient.ReceiveAsync(bufferParameters, SocketFlags.None);
                            if (realLength <= 0)
                            {
                                socketForClient.Close();
                                socketForClient.Dispose();
                                break;
                            }

                            if (realLength == 2 && bufferParameters[0] == 45 && bufferParameters[1] == 49)
                            {
                                // 客户端先关闭socket可能会抛异常; 所以客户端通知服务端关闭连接(-1)后, 服务端发送最后一条消息然后先关闭socket连接, 客户端等待此消息后再关闭连接
                                await socketForClient.SendAsync(Encoding.UTF8.GetBytes("-1").AsMemory(), SocketFlags.None);
                                socketForClient.Close();
                                socketForClient.Dispose();
                                break;
                            }
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
                                        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} ERROR 传输文件名为空, 文件传输提前结束");
                                        socketForClient.Close();
                                        socketForClient.Dispose();
                                        return;
                                    }

                                    // 优先取客户端配置
                                    fileSaveDir = parametersArray[2];
                                    if (string.IsNullOrWhiteSpace(fileSaveDir))
                                    {
                                        fileSaveDir = serverSaveFileDir;
                                    }

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

                                    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 首先从第一个包获取文件名: {fileName}; 保存路径: {file}; READY 提醒客户端可以正式发送文件的字节流了");

                                    // 任务参数解析完毕, 客户端可以开始发送文件的字节了
                                    await socketForClient.SendAsync(Encoding.UTF8.GetBytes("ready").AsMemory(), SocketFlags.None); // SocketFlags.Broadcast
                                    using (var fileStream = new FileStream(file, FileMode.Create))
                                    {
                                        while (true)
                                        {
                                            // 阻塞等待, 一直等到有字节发送过来或者客户端关闭socket连接
                                            realLength = socketForClient.Receive(buffer);
                                            if (realLength > 0)
                                            {
                                                if (realLength == 6 && buffer[0] == ZeroByteValue && buffer[1] == ZeroByteValue && buffer[2] == ZeroByteValue && buffer[3] == ZeroByteValue && buffer[4] == ZeroByteValue && buffer[5] == ZeroByteValue)
                                                {
                                                    // 接收到000000, 表示当前文件已经上传完毕
                                                    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 收到客户端提醒: 第{uploadedFileCount++}文件数据已经全部上传完毕 {Environment.NewLine}");
                                                    break;
                                                }
                                                else
                                                {
                                                    // 正在上传文件的字节
                                                    await fileStream.WriteAsync(buffer.AsMemory(0, realLength));

                                                    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 接收到{realLength}字节保存到{file}; READY!");
                                                    // 处理完告诉客户端继续发送
                                                    await socketForClient.SendAsync(Encoding.UTF8.GetBytes("ready"));
                                                }
                                            }
                                        }
                                    }
                                    break;
                                default:
                                    break;
                            }
                        }
                        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} client closed");
                    });
                    #endregion
                }


            }, stoppingToken);
            Console.WriteLine("发布服务已经开启");
            return Task.CompletedTask;
        }
    }
}
