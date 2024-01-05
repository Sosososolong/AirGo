using System;
using System.Buffers;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Utils
{
    public static class SocketHelper
    {
        public static async Task ProcessLinesAsyncV1(NetworkStream stream)
        {
            // 1.定义一个1024缓冲区
            var buffer = new byte[1024];
            // 2.从网络流读取1024个字节到缓冲区buffer中
            // warning: 一次ReadAsync调用可能没有收到整个消息(网络发送1000字节数据也可能分两次到达)
            // warning: ReadAsync读取的字节长度可能不到1024, 所以不应该用buffer.Length
            // warning: 一次读取了1024字节数据, 可能不是完整的一条或者多条数据
            await stream.ReadAsync(buffer, 0, buffer.Length);
            // 3.在buffer中处理一行数据
        }

        public static async Task ProcessLinesAsyncV2(NetworkStream stream)
        {
            // 1. 定义一个1024缓冲区
            var buffer = new byte[1024];
            // 2. 记录已经缓冲的数据(字节长度), 可以在网络流读取数据时作为偏移值
            var bytesBuffered = 0;
            var bytesConsumed = 0;

            // 3. 不要一次读取固定长度, 没有什么意义; 既然我们需要的是行数据, 所以我们一直去读取数据, 直到换行符号
            while (true)
            {
                var bytesRead = await stream.ReadAsync(buffer, bytesBuffered, buffer.Length - bytesBuffered);
                if (bytesRead == 0)
                {
                    // EOF 已经到行尾
                    break;
                }

                // 跟踪已缓冲的字节数
                bytesBuffered += bytesRead;
                var linePosition = -1;

                // 缓冲中的数据bytesBuffered里面可能有很多行, 每次我们只处理一行数据这中方式去不断地消费缓冲数据(bytesBuffered中的数据)
                do
                {
                    // 在缓冲数据中查找换行符
                    linePosition = Array.IndexOf(buffer, (byte)'\n', bytesConsumed, bytesBuffered - bytesConsumed);
                    if (linePosition >= 0)
                    {
                        // 根据偏移量计算一行的长度
                        var lineLength = linePosition - bytesConsumed;
                        // 处理这一行
                        // ProcessLine(buffer, bytesConsumed, lineLength);
                        
                        // 移动bytesConsumed为了跳过我们已经处理的行(包括\n)
                        bytesConsumed += lineLength + 1;
                    }
                } while (linePosition >= 0);
            }
        }

        /// <summary>
        /// 解决数据超过1024字节的情况
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static async Task ProcessLinesAsyncV3(NetworkStream stream)
        {
            // 1. 定义一个1024缓冲区
            var buffer = ArrayPool<byte>.Shared.Rent(1024);
            // 2. 记录已经缓冲的数据(字节长度), 可以在网络流读取数据时作为偏移值
            var bytesBuffered = 0;
            var bytesConsumed = 0;

            // 3. 不要一次读取固定长度, 既然我们需要的是行数据, 所以我们一直去读取数据, 直到换行符号
            while (true)
            {
                // 在buffer中计算剩余的字节数
                var bytesRemaining = buffer.Length - bytesBuffered;
                if (bytesRemaining == 0)
                {
                    // 将buffer size翻倍, 并且将之前缓冲的数据复制到新的缓冲区
                    var newBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                    Buffer.BlockCopy(buffer, 0, newBuffer, 0, buffer.Length);
                    // 将旧的buffer丢回池中
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = newBuffer;
                    bytesRemaining = buffer.Length - bytesConsumed;
                }

                var bytesRead = await stream.ReadAsync(buffer, bytesBuffered, buffer.Length - bytesBuffered);
                if (bytesRead == 0)
                {
                    // EOF 已经到行尾
                    break;
                }

                // 跟踪已缓冲的字节数
                bytesBuffered += bytesRead;
                var linePosition = -1;

                // 缓冲中的数据bytesBuffered里面可能有很多行, 每次我们只处理一行数据这中方式去不断地消费缓冲数据(bytesBuffered中的数据)
                do
                {
                    // 在缓冲数据中查找换行符
                    linePosition = Array.IndexOf(buffer, (byte)'\n', bytesConsumed, bytesBuffered - bytesConsumed);
                    if (linePosition >= 0)
                    {
                        // 根据偏移量计算一行的长度
                        var lineLength = linePosition - bytesConsumed;
                        // 处理这一行
                        // ProcessLine(buffer, bytesConsumed, lineLength);

                        // 移动bytesConsumed为了跳过我们已经处理的行(包括\n)
                        bytesConsumed += lineLength + 1;
                    }
                } while (linePosition >= 0);
            }
        }

        public static async Task MyProcessLinesAsync()
        {
            byte[] buffer = new byte[4096];
            var server = new TcpListener(IPAddress.Any, 5666);
            server.Start();
            while (true)
            {
                var socketLocal = await server.AcceptTcpClientAsync();
                Console.WriteLine("client connected");
                _ = Task.Factory.StartNew(async () => {
                    try
                    {
                        var localStream = socketLocal.GetStream();
                        var read = await localStream.ReadAsync(buffer, 0, buffer.Length);
                        if (read == 0)
                        {
                            return;
                        }
                        while (true)
                        {
                            var position = Array.IndexOf(buffer, (byte)'\n');
                            if (position == -1)
                            {
                                break;
                            }
                            var line = buffer.Skip(0).Take(position + 1).ToArray();
                            ProcessLine(line);
                            buffer = buffer.Skip(position + 1).ToArray();
                            if (buffer.Length == 0)
                            {
                                break;
                            }
                        }
                        Console.WriteLine("已经处理完所有行");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("异常: " + ex.ToString());
                        throw;
                    }
                });
            }
        }
        static void ProcessLine(byte[] line)
        {
            var lineTxt = Encoding.UTF8.GetString(line);
            Console.WriteLine(lineTxt);
        }
    }
}
