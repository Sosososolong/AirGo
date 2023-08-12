using Sylas.RemoteTasks.App.Utils;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Sylas.RemoteTasks.App.BackgroundServices
{
    public class PublishService : BackgroundService
    {
        public PublishService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        private const int bufferSize = 1024 * 1024;
        private readonly byte ZeroByteValue = Encoding.UTF8.GetBytes("0").First();
        private readonly IConfiguration _configuration;

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

                //var configure = ServiceLocator.Instance.GetService<IConfiguration>();
                var serverSaveFileDir = _configuration?["Upload:SaveDir"];

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

                        var uploadedFileCount = 1;
                        while (true)
                        {
                            // 服务端已经准备好处理新文件了
                            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} READY 准备接收新的文件");
                            await socketForClient.SendAsync(Encoding.UTF8.GetBytes("ready_for_new").AsMemory(), SocketFlags.None);
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
                            var fileName = string.Empty;
                            var fileSaveDir = string.Empty;
                            switch (type)
                            {
                                // type为1: 发送文件; 获取第二个参数为文件大小; 第三个参数即文件名; 第四个参数表示当前服务器保存文件的目录(不传的话服务器自己需要配置Upload:SaveDir)
                                case "1":
                                    var fileSize = Convert.ToInt64(parametersArray[1]);
                                    fileName = parametersArray[2];
                                    if (string.IsNullOrWhiteSpace(fileName))
                                    {
                                        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} ERROR 传输文件名为空, 文件传输提前结束");
                                        socketForClient.Close();
                                        socketForClient.Dispose();
                                        return;
                                    }

                                    // 优先取客户端配置
                                    fileSaveDir = parametersArray[3];
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

                                    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 首先从第一个包获取文件名: {fileName}; 保存路径: {file}; 文件大小: {fileSize} READY 提醒客户端可以正式发送文件的字节流了");

                                    // 任务参数解析完毕, 客户端可以开始发送文件的字节了
                                    await socketForClient.SendAsync(Encoding.UTF8.GetBytes("ready_for_file_content").AsMemory(), SocketFlags.None);
                                    using (var fileStream = new FileStream(file, FileMode.Create))
                                    {
                                        var allReceived = 0;
                                        while (true)
                                        {
                                            // 阻塞等待, 一直等到有字节发送过来或者客户端关闭socket连接
                                            realLength = socketForClient.Receive(buffer);

                                            if (realLength > 0)
                                            {
                                                allReceived += realLength;
                                                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 共接收: {allReceived}, 当前接收: {realLength}");
                                                if (allReceived > fileSize)
                                                {
                                                    var extraLength = allReceived - fileSize;
                                                    var validLength = Convert.ToInt32(realLength - extraLength);
                                                    // 正在上传文件的字节
                                                    await fileStream.WriteAsync(buffer.AsMemory(0, validLength));

                                                    byte[] endFlag = new byte[6];
                                                    if (extraLength >= 6)
                                                    {
                                                        endFlag = buffer.Skip(validLength).Take(6).ToArray();
                                                    }
                                                    else
                                                    {
                                                        for (int i = 0; i < extraLength; i++)
                                                        {
                                                            endFlag[i] = buffer[validLength + i];
                                                        }
                                                        realLength = socketForClient.Receive(buffer);
                                                        for (int i = 0; i < 6 - extraLength; i++)
                                                        {
                                                            endFlag[6 - extraLength + i] = buffer[i];
                                                        }
                                                    }
                                                    Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 当前文件上传结束: {string.Join(',', endFlag)}");
                                                    if (endFlag[0] == ZeroByteValue && endFlag[1] == ZeroByteValue && endFlag[2] == ZeroByteValue && endFlag[3] == ZeroByteValue && endFlag[4] == ZeroByteValue && endFlag[5] == ZeroByteValue)
                                                    {
                                                        // 接收到000000, 表示当前文件已经上传完毕
                                                        Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 收到客户端提醒: 第{uploadedFileCount++}文件数据已经全部上传完毕 {Environment.NewLine}");
                                                        break;
                                                    }
                                                    else
                                                    {
                                                        Console.WriteLine("不是预期的文件上传结束标识");
                                                        throw new Exception("不是预期的文件上传结束标识");
                                                    }
                                                }
                                                else
                                                {
                                                    // 正在上传文件的字节
                                                    await fileStream.WriteAsync(buffer.AsMemory(0, realLength));
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
