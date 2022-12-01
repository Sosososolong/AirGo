using System.Net.Sockets;
using System.Net;
using System.Text;

namespace Sylas.RemoteTasks.App.BackgroundServices
{
    public class PublishService : BackgroundService
    {
        private const int bufferSize = 1024 * 1024;
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

                while (true)
                {
                    // 无限循环等待客户端的连接, 连接到客户端就交给子线程处理, 然后马上继续循环过来等待其他客户端连接
                    var socketForClient = tcpSocket.Accept();
                    Task.Factory.StartNew(async () =>
                    {
                        byte[] buffer = new byte[bufferSize];

                        #region 第一次发的数据确定任务及参数
                        byte[] bufferParameters = new byte[1024];
                        int realLength = socketForClient.Receive(bufferParameters);
                        var parameters = Encoding.UTF8.GetString(bufferParameters);
                        var parametersArray = parameters.Split(";;;;");
                        var type = parametersArray[0];
                        string fileName = string.Empty;
                        switch (type)
                        {
                            // type为1: 发送文件, 获取第二个参数即文件名
                            case "1":
                                // 获取客户端传过来的字节数组放到bufferParameters中, 字节数组bufferParameters剩余的位置将会用0填充, 0经UTF-8反编码为字符串即"\0"
                                fileName = parametersArray[1].Replace("\0", "");
                                if (string.IsNullOrWhiteSpace(fileName))
                                {
                                    Console.WriteLine($"传输文件名为空, 文件传输提前结束");
                                    socketForClient.Close();
                                    socketForClient.Dispose();
                                    return;
                                }
                                Console.WriteLine($"传输文件名: {fileName}");
                                break;
                            default:
                                break;
                        }

                        // ReadOnlyMemory<byte> memory = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("0000"));
                        await socketForClient.SendAsync(Encoding.UTF8.GetBytes("0000").AsMemory(), SocketFlags.None); // SocketFlags.Broadcast
                        #endregion

                        using var fileStream = new FileStream($@"D:\Seafile\Seafile\Publish\v5.0配置中心+日志中心版本\{fileName}", FileMode.Create);
                        while (true)
                        {
                            realLength = socketForClient.Receive(buffer);
                            System.Console.WriteLine($"接收到字节: {realLength}");
                            if (realLength > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, realLength);
                            }
                            else
                            {
                                // 结束
                                Console.WriteLine($"接收到字节: {realLength}; 结束");
                                socketForClient.Close();
                                socketForClient.Dispose();
                                break;
                            }
                        }
                    });
                }


            }, stoppingToken);
            Console.WriteLine("发布服务已经开启");
            return Task.CompletedTask;
        }
    }
}
