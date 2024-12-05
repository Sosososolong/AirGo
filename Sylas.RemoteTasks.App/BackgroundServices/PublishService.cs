﻿using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Sylas.RemoteTasks.App.RemoteHostModule;
using Sylas.RemoteTasks.App.RemoteHostModule.Anything;
using Sylas.RemoteTasks.Utils;
using Sylas.RemoteTasks.Utils.CommandExecutor;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Sylas.RemoteTasks.App.BackgroundServices
{
    public class PublishService(IConfiguration configuration, ILogger<PublishService> logger, IServiceScopeFactory scopeFactory) : BackgroundService
    {
        private const int _bufferSize = 1024 * 1024;
        private readonly byte _zeroByteValue = Encoding.UTF8.GetBytes("0").First();
        private const string _endFlag = "000000";
        const int _serverPort = 8989;

        private readonly Dictionary<string, Socket> _serverNodeSockets = [];

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _ = Task.Factory.StartNew(() =>
            {
                // 指定socket对象为IPV4的,流传输,TCP协议
                var tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPAddress iPAddress = IPAddress.Parse("0.0.0.0");
                var iPEndPoint = new IPEndPoint(iPAddress, _serverPort);

                tcpSocket.Bind(iPEndPoint);
                tcpSocket.Listen(128);

                //var configure = ServiceLocator.Instance.GetService<IConfiguration>();
                var serverSaveFileDir = configuration?["Upload:SaveDir"];

                while (true)
                {
                    // 无限循环等待客户端的连接
                    var socketForClient = tcpSocket.Accept();

                    #region 连接到客户端就交给子线程处理, 然后马上继续循环过来等待其他客户端连接
                    Task.Factory.StartNew(async () =>
                    {
                        byte[] buffer = new byte[_bufferSize];

                        // 参数协议: 第一次发的数据确定任务及参数
                        byte[] bufferParameters = new byte[1024];

                        var uploadedFileCount = 1;
                        while (true)
                        {
                            // 服务端已经准备好处理新文件了(或让指定的服务节点执行一个新的命令);
                            logger.LogInformation($"TCP Service: READY 准备接收新的文件(或任务)");

                            await socketForClient.SendTextAsync("ready_for_new");

                            int realLength = await socketForClient.ReceiveAsync(bufferParameters, SocketFlags.None);
                            if (realLength <= 0)
                            {
                                logger.LogInformation("接收到字节数为0, 关闭并释放socket连接");
                                socketForClient.Close();
                                socketForClient.Dispose();
                                break;
                            }

                            // '-': 45; '1': 49.    "-1": [45,49]
                            if (realLength == 2 && bufferParameters[0] == 45 && bufferParameters[1] == 49)
                            {
                                // 收到"-1", 发送"-1"(通知客户端可关闭连接)后关闭连接(客户端先关闭socket可能会抛异常; 所以客户端通知服务端关闭连接(-1)后, 服务端发送最后一条消息然后先关闭socket连接, 客户端等待此消息后再关闭连接)
                                await socketForClient.SendTextAsync("-1");
                                socketForClient.Close();
                                socketForClient.Dispose();
                                break;
                            }
                            // 获取客户端传过来的字节数组放到bufferParameters中, 字节数组bufferParameters剩余的位置将会用0填充, 0经UTF-8反编码为字符串即"\0"
                            var parametersContent = bufferParameters.GetText(realLength); //Encoding.UTF8.GetString(bufferParameters, 0, realLength).Replace("\0", string.Empty);
                            var parametersArray = parametersContent.Split(";;;;");
                            var type = parametersArray[0];
                            var fileName = string.Empty;
                            var fileSaveDir = string.Empty;
                            if (type == "1")
                            {
                                // type为1: 发送文件; 获取第二个参数为文件大小; 第三个参数即文件名; 第四个参数表示当前服务器保存文件的目录(不传的话服务器自己需要配置Upload:SaveDir)
                                var fileSize = Convert.ToInt64(parametersArray[1]);
                                fileName = parametersArray[2];
                                if (string.IsNullOrWhiteSpace(fileName))
                                {
                                    logger.LogInformation($"TCP Service: ERROR 传输文件名为空, 文件传输提前结束");
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

                                logger.LogInformation("TCP Service: 首先从第一个包获取文件名: {fileName}; 保存路径: {file}; 文件大小: {fileSize} READY 提醒客户端可以正式发送文件的字节流了", fileName, file, fileSize);

                                // 任务参数解析完毕, 客户端可以开始发送文件的字节了
                                await socketForClient.SendTextAsync("ready_for_file_content");

                                #region 从客户端接收文件的所有字节写入本地文件中
                                using var fileStream = new FileStream(file, FileMode.Create);
                                var allReceived = 0;
                                while (true)
                                {
                                    // 阻塞等待, 一直等到有字节发送过来或者客户端关闭socket连接
                                    realLength = socketForClient.Receive(buffer);

                                    if (realLength > 0)
                                    {
                                        allReceived += realLength;
                                        logger.LogInformation("TCP Service: 共接收: {allReceived}, 当前接收: {realLength}", allReceived, realLength);
                                        // 文件已经全部接受到了, 检查结束符:"000000"
                                        if (allReceived <= fileSize)
                                        {
                                            // 上传的文件字节都写入文件
                                            await fileStream.WriteAsync(buffer.AsMemory(0, realLength));
                                        }
                                        else
                                        {
                                            // 表示接收到的字节数超过文件大小的部分
                                            var extraLength = allReceived - fileSize;
                                            // 属于文件的有效字节数(其他的属于结束符)
                                            var validLength = Convert.ToInt32(realLength - extraLength);
                                            // 将最后一部分真正属于文件的字节(前validLength个字节)写入文件
                                            await fileStream.WriteAsync(buffer.AsMemory(0, validLength));

                                            byte[] endFlag = new byte[6];
                                            if (extraLength >= 6)
                                            {
                                                // 当前已经接收到了完整的结束符, 提取结束符
                                                endFlag = buffer.Skip(validLength).Take(6).ToArray();
                                            }
                                            else
                                            {
                                                // 当前没有接收到完整的结束符, 获取已经接收到的结束符的一部分(validLength后面的长度为extraLength的部分)
                                                for (int i = 0; i < extraLength; i++)
                                                {
                                                    endFlag[i] = buffer[validLength + i];
                                                }

                                                // 获取剩下的(已经获取到了extraLength, 剩下6-extraLength)结束符(结束符只有6位, 剩下的部分不足6位了, 肯定可以一次性获取)
                                                realLength = socketForClient.Receive(buffer);
                                                for (int i = 0; i < 6 - extraLength; i++)
                                                {
                                                    endFlag[6 - extraLength + i] = buffer[i];
                                                }
                                            }
                                            logger.LogInformation("TCP Service: 当前文件上传结束: {end}", string.Join(',', endFlag));
                                            if (IsEndFlag(endFlag))
                                            {
                                                // 接收到000000, 表示当前文件已经上传完毕
                                                logger.LogInformation("TCP Service: 收到客户端提醒: 第{fileNumber}文件数据已经全部上传完毕 {newLine}", uploadedFileCount++, Environment.NewLine);
                                                // 文件接收结束
                                                break;
                                            }
                                            else
                                            {
                                                logger.LogError("TCP Service: 不是预期的文件上传结束标识");
                                                // 子线程将关闭
                                                throw new Exception("不是预期的文件上传结束标识");
                                            }
                                        }
                                    }
                                }
                                #endregion
                            }
                            else if (type == "2")
                            {
                                string serverNodeDomain = parametersArray[1];

                                var centerServer = configuration?.GetValue<string>("CenterServer");
                                if (string.IsNullOrWhiteSpace(centerServer))
                                {
                                    // 没有配置中心服务器地址, 说明就是中心服务器
                                    // 先将当前客户端添加到公共容器中, 供当前中心服务器调度
                                    string key = serverNodeDomain;
                                    _serverNodeSockets[key] = socketForClient;

                                    int readQueueCount = 0;
                                    while (true)
                                    {
                                        if (readQueueCount % 100 == 0)
                                        {
                                            logger.LogInformation("不断读取队列中的任务信息, 第{readQueueCount}次", readQueueCount);
                                        }
                                        readQueueCount++;
                                        if (AnythingService.CommandTasks.TryDequeue(out var commandInfoTaskDto))
                                        {
                                            logger.LogCritical("队列中读取到一条命令任务: {CommandName}", commandInfoTaskDto.CommandName);
                                            // 处理一条命令
                                            if (!_serverNodeSockets.TryGetValue(commandInfoTaskDto.Domain, out var serverNodeSocket))
                                            {
                                                logger.LogError("没有找到子节点{domain}对应的socket连接", commandInfoTaskDto.Domain);
                                            }
                                            else
                                            {
                                                logger.LogCritical("成功找到子节点{Domain}的连接", commandInfoTaskDto.Domain);
                                                await serverNodeSocket.SendTextAsync($"{commandInfoTaskDto.SettingId};;;;{commandInfoTaskDto.CommandName}{_endFlag}");
                                                logger.LogCritical("成功向子节点{Domain}发送命令的执行任务", commandInfoTaskDto.Domain);
                                                string commandResultJson = string.Empty;
                                                while (true)
                                                {
                                                    int receivedLength = 0;
                                                    try
                                                    {
                                                        logger.LogCritical("等待子节点{Domain}返回命令的执行结果", commandInfoTaskDto.Domain);
                                                        receivedLength = await serverNodeSocket.ReceiveAsync(buffer, SocketFlags.None);
                                                        logger.LogCritical("成功获取子节点{Domain}返回命令的执行结果, 长度{length}", commandInfoTaskDto.Domain, receivedLength);
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        logger.LogError("等待服务节点返回命令执行结果时异常: {Message}", ex.Message);
                                                        throw;
                                                    }

                                                    try
                                                    {
                                                        if (receivedLength == 0)
                                                        {
                                                            logger.LogError("等待客户端返回命令处理结果的过程中, 接收到了0个字节!");
                                                            serverNodeSocket.Close();
                                                            serverNodeSocket.Dispose();
                                                            _serverNodeSockets.Remove(commandInfoTaskDto.Domain);
                                                            logger.LogCritical("移除{domain}节点的socket连接", commandInfoTaskDto.Domain);
                                                            AnythingService.RemoteCommandResults[commandInfoTaskDto.RequestId] = new CommandResult(false, "等待节点返回命令执行结果时收到了0字节");
                                                            break;
                                                        }
                                                        else
                                                        {
                                                            commandResultJson += buffer.GetText(receivedLength);
                                                            logger.LogCritical("成功获取子节点{Domain}返回命令的执行结果文本: {text}", commandInfoTaskDto.Domain, commandResultJson);
                                                            if (receivedLength >= 6)
                                                            {
                                                                var last6Bytes = buffer.Skip(receivedLength - 6).Take(6).ToArray();
                                                                if (IsEndFlag(last6Bytes))
                                                                {
                                                                    commandResultJson = commandResultJson[..^6];
                                                                    logger.LogCritical("命令执行结果json字符串去除结束标记:{result}", commandResultJson);
                                                                    var commandResult = JsonConvert.DeserializeObject<CommandResult>(commandResultJson) ?? new CommandResult(false, commandResultJson);
                                                                    AnythingService.RemoteCommandResults[commandInfoTaskDto.RequestId] = commandResult;
                                                                    break;
                                                                }
                                                            }
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        logger.LogError("处理服务节点{domain}返回的命令结果异常: {Message}", commandInfoTaskDto.Domain, ex.Message);
                                                        throw;
                                                    }
                                                    
                                                }
                                            }
                                        }
                                        else
                                        {
                                            await Task.Delay(1000);
                                        }
                                    }
                                }
                                else
                                {
                                    // socketForClient是中心服务器接收到子服务器(或其他客户端)主动链接过来的客户端专用socket, 当前是子节点服务器, 没有此socket;
                                    // 当前子服务器应该使用程序启动时连接到服务端的socket, 对应中心服务器的socketForClient
                                }
                            }
                        }
                        logger.LogInformation($"TCP Service: client closed");
                    });
                    #endregion
                }
            }, stoppingToken);
            logger.LogInformation("TCP Service: 发布服务已经开启");
            return Task.CompletedTask;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            var centerServer = configuration?.GetValue<string>("CenterServer");

            if (!string.IsNullOrWhiteSpace(centerServer))
            {
                _ = Task.Factory.StartNew(async () =>
                {
                    byte[] buffer = new byte[_bufferSize];

                    // 不为空说明当前服务器为子服务器节点, 需要连接到中心服务器
                    Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    try
                    {
                        await socket.ConnectAsync(centerServer, _serverPort);
                        
                        string serverNodeConnectParams = $"2;;;;{Dns.GetHostName()}";
                        logger.LogInformation("子节点连接到中新服务器, 发送参数: {serverNodeConnectParams}", serverNodeConnectParams);
                        await socket.SendTextAsync(serverNodeConnectParams);
                        while (true)
                        {
                            string anythingCommand = await socket.ReceiveAllTextAsync(buffer, IsEndFlag, $"接收到来自中心服务器的数据字节数为0, 关闭释放连接中心服务器的Socket连接!", logger);
                            anythingCommand = anythingCommand.Replace("ready_for_new", string.Empty);
                            logger.LogCritical("当前主机{host}接收到来自中心服务器的命令任务: {anythingCommand}", Dns.GetHostName(), anythingCommand);
                            string[] arr = anythingCommand.Split(";;;;");
                            if (arr.Length < 2)
                            {
                                logger.LogError("参数不足{msg}", anythingCommand);
                            }
                            else if (!int.TryParse(arr[0], out int settingId))
                            {
                                logger.LogError("settingId:{settingIdStr}无法转换为整数", arr[0]);
                            }
                            else
                            {
                                string commandName = arr[1];
                                using var scope = scopeFactory.CreateScope();
                                var anythingService = scope.ServiceProvider.GetRequiredService<AnythingService>();
                                var commandResult = await anythingService.ExecuteAsync(new() { SettingId = settingId, CommandName = commandName });
                                string commandResultJson = JsonConvert.SerializeObject(commandResult);
                                await socket.SendTextAsync($"{commandResultJson}{_endFlag}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex.Message);
                        throw;
                    }
                });

                logger.LogInformation("程序启动阶段, 已连接中心服务器");
            }
            
            return base.StartAsync(cancellationToken);
        }

        bool IsEndFlag(byte[] endFlag)
        {
            return endFlag.Length >= 6 && endFlag[0] == _zeroByteValue && endFlag[1] == _zeroByteValue && endFlag[2] == _zeroByteValue && endFlag[3] == _zeroByteValue && endFlag[4] == _zeroByteValue && endFlag[5] == _zeroByteValue;
        }
    }
}
