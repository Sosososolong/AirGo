using Newtonsoft.Json;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.App.RemoteHostModule.Anything;
using Sylas.RemoteTasks.Common;
using Sylas.RemoteTasks.Common.Extensions;
using Sylas.RemoteTasks.Utils;
using Sylas.RemoteTasks.Utils.CommandExecutor;
using System.Collections.Concurrent;
using System.DirectoryServices.ActiveDirectory;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Sylas.RemoteTasks.App.BackgroundServices
{
    public class PublishService : BackgroundService
    {
        private const int _bufferSize = 1024 * 1024;
        private readonly byte _zeroByteValue = Encoding.UTF8.GetBytes("0").First();
        private const string _endFlag = "000000";
        private const string _heartbeatMsg = "keep-alive";
        private readonly string _centerServer;
        int _tcpPort = 8989;
        int _centerServerPort = 8989;
        private readonly string _domain = AppStatus.Domain;
        /// <summary>
        /// 同一台机器开启多个客户端时, 让服务端区分不同的进程(区分不同进程的socket连接, 不会错误释放相关socket连接)
        /// </summary>
        private readonly int _processId = AppStatus.ProcessId;
        private readonly ConcurrentDictionary<string, (Socket, string)> _childNodeSockets = new();
        /// <summary>
        /// 记录最后一次与中心服务器通讯的时间
        /// </summary>
        private DateTime _lastKeepAliveTime = DateTime.Now;
        /// <summary>
        /// 心跳频率(心跳包发送的频率);
        /// </summary>
        private const int _heartbeatFrequency = 10;
        /// <summary>
        /// 重连中心服务器的频率/s
        /// </summary>
        private const int _reconnectFrequency = 3;
        /// <summary>
        /// 心跳日志目录
        /// </summary>
        private readonly string _heartbeatLogsDirectory;

        static int _threadNumber = 0;

        private readonly IConfiguration _configuration;
        private readonly ILogger<PublishService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        /// <summary>
        /// 构造函数, 初始化服务与逻辑字段
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="logger"></param>
        /// <param name="scopeFactory"></param>
        /// <param name="hostEnvironment"></param>
        public PublishService(IConfiguration configuration, ILogger<PublishService> logger, IServiceScopeFactory scopeFactory, IHostEnvironment hostEnvironment)
        {
            _configuration = configuration;
            _centerServer = configuration.GetValue<string>("CenterServer") ?? string.Empty;

            var tcpPort = _configuration.GetValue<int>("TcpPort");
            if (tcpPort > 0)
            {
                _tcpPort = tcpPort;
            }

            var centerServerPort = _configuration.GetValue<int>("CenterServerPort");
            if (centerServerPort > 0)
            {
                _centerServerPort = centerServerPort;
            }

            _logger = logger;
            _scopeFactory = scopeFactory;

            string rootPath = hostEnvironment.ContentRootPath;
            _heartbeatLogsDirectory = Path.Combine(rootPath, "Logs", "Heartbeats");
            if (!Directory.Exists(_heartbeatLogsDirectory))
            {
                Directory.CreateDirectory(_heartbeatLogsDirectory);
            }
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _ = Task.Factory.StartNew(() =>
            {
                // 指定socket对象为IPV4的,流传输,TCP协议
                using var tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPAddress iPAddress = IPAddress.Parse("0.0.0.0");
                var iPEndPoint = new IPEndPoint(iPAddress, _tcpPort);
                _logger.LogInformation("TCP Service: 开始绑定端口{port}", _tcpPort);

                tcpSocket.Bind(iPEndPoint);
                tcpSocket.Listen(128);

                //var configure = ServiceLocator.Instance.GetService<IConfiguration>();
                var serverSaveFileDir = _configuration?["Upload:SaveDir"];

                int socketNumber = 0;
                while (true)
                {
                    // 无限循环等待客户端的连接
                    var socketForClient = tcpSocket.Accept();
                    _logger.LogInformation("接收到新的客户端请求");
                    socketNumber++;

                    #region 连接到客户端就交给子线程处理, 然后马上继续循环过来等待其他客户端连接
                    Task.Factory.StartNew(async () =>
                    {
                        _threadNumber++;
                        int threadNumber = _threadNumber;
                        string threadSocketNo = $"THREAD{threadNumber}_SOCKET{socketNumber}";
                        _logger.LogInformation("开启新线程服务于当前客户端, Thread Socket No: {threadSocketNo}", threadSocketNo);
                        byte[] buffer = new byte[_bufferSize];

                        // 参数协议: 第一次发的数据确定任务及参数
                        byte[] bufferParameters = new byte[1024];

                        var uploadedFileCount = 1;
                        bool stopThread = false;
                        while (true)
                        {
                            // 准备接收客户端新的消息, 先发送一条"ready_for_new"; 服务端已经准备好处理新文件了(或让指定的服务节点执行一个新的命令);
                            _logger.LogInformation("TCP Service[{thread}]: READY 准备接收新的任务(文件流或命令任务)", threadSocketNo);
                            await socketForClient.SendTextAsync("ready_for_new");

                            int realLength = await socketForClient.ReceiveAsync(bufferParameters, SocketFlags.None);
                            if (realLength <= 0)
                            {
                                _logger.LogInformation("TCP Service: 接收到字节数为0, 关闭并释放socket连接");
                                socketForClient.Close();
                                socketForClient.Dispose();
                                break;
                            }

                            if (await socketForClient.CheckIsCloseMsgAsync(realLength, bufferParameters))
                            {
                                _logger.LogCritical("TCP Service: 接收到客户端关闭连接通知, 关闭socket连接");
                                break;
                            }
                            // 获取客户端传过来的字节数组放到bufferParameters中, 字节数组bufferParameters剩余的位置将会用0填充, 0经UTF-8反编码为字符串即"\0"
                            var parametersContent = bufferParameters.GetText(realLength); //Encoding.UTF8.GetString(bufferParameters, 0, realLength).Replace("\0", string.Empty);
                            if (!parametersContent.Contains(_heartbeatMsg))
                            {
                                _logger.LogInformation("TCP Service: 接收到来自客户端的消息:{msg}", parametersContent);
                            }
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
                                    _logger.LogInformation($"TCP Service: ERROR 传输文件名为空, 文件传输提前结束");
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

                                _logger.LogInformation("TCP Service: 首先从第一个包获取文件名: {fileName}; 保存路径: {file}; 文件大小: {fileSize} READY 提醒客户端可以正式发送文件的字节流了", fileName, file, fileSize);

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
                                        _logger.LogInformation("TCP Service: 共接收: {allReceived}, 当前接收: {realLength}", allReceived, realLength);
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
                                            _logger.LogInformation("TCP Service: 当前文件上传结束: {end}", string.Join(',', endFlag));
                                            if (IsEndFlag(endFlag))
                                            {
                                                // 接收到000000, 表示当前文件已经上传完毕
                                                _logger.LogInformation("TCP Service: 收到客户端提醒: 第{fileNumber}文件数据已经全部上传完毕 {newLine}", uploadedFileCount++, Environment.NewLine);
                                                // 文件接收结束
                                                break;
                                            }
                                            else
                                            {
                                                _logger.LogError("TCP Service: 不是预期的文件上传结束标识");
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
                                //2;;;;DESKTOP-XXX;;;;{节点路径base64编码结果}-Socket1
                                string childServerNodeDomain = parametersArray[1]; // DESKTOP-XXX
                                string childServerNodeSocketNo = string.Empty;
                                string childServerNodePath = string.Empty;
                                if (parametersArray.Length >= 3)
                                {
                                    childServerNodeSocketNo = parametersArray[2]; // 节点路径base64编码结果-Socket1
                                    int index = childServerNodeSocketNo.LastIndexOf('-');
                                    if (index > 0)
                                    {
                                        childServerNodePath = childServerNodeSocketNo[..index];
                                    }
                                }

                                var centerServer = _configuration?.GetValue<string>("CenterServer");
                                if (string.IsNullOrWhiteSpace(centerServer))
                                {
                                    // **关键**: 当有新的子节点连接过来时, 需要将老的socket连接释放掉, 只保留最新的与子节点的连接的socket对象(老的socket不一定会检测到异常关闭, 可能会一直存在, 那么就会导致消息还是发送给子节点老的socket连接, 导致子节点收不到消息)
                                    _logger.LogInformation("Center Server[{thread}]: 接收到来自子节点{Domain}的连接{childNodeSocketNo}, 关闭并释放socket连接", threadSocketNo, childServerNodeDomain, childServerNodeSocketNo);
                                    string childServerNodeIdentityNo = $"{childServerNodeDomain}-{childServerNodePath}";
                                    if (_childNodeSockets.TryGetValue(childServerNodeIdentityNo, out var val))
                                    {
                                        _logger.LogInformation("Center Server[{thread}]: 已经存在与子节点{childServerNodeIdentityNo}的连接{oldSocketNo}, 关闭并释放socket连接", threadSocketNo, childServerNodeIdentityNo, val.Item2);
                                        val.Item1.Close();
                                        val.Item1.Dispose();
                                    }
                                    else if (_childNodeSockets.Keys.Any(x => x.StartsWith(childServerNodeDomain)))
                                    {
                                        // 当前子节点主机已经有实例连接过来了, 那么当前实例无需与中心服务器建立连接
                                        _logger.LogWarning("Center Server[{thread}]: 当前子节点{childServerNodeDomain}已经有实例({instancePath})连接过来了, 当前实例无需与中心服务器建立连接", threadSocketNo, childServerNodeDomain, childServerNodePath.FromBase64());
                                        return;
                                    }
                                    _childNodeSockets[childServerNodeIdentityNo] = (socketForClient, childServerNodeSocketNo);

                                    // 没有配置中心服务器地址, 说明就是中心服务器
                                    // 先将当前客户端添加到公共容器中, 供当前中心服务器调度
                                    string key = childServerNodeDomain;

                                    _logger.LogCritical("Center Server[{thread}]: 开启两个子线程, 担任命令发送器和命令接收器", threadSocketNo);

                                    CancellationTokenSource cts = new();
                                    cts.Token.Register(() => _logger.LogCritical("Center Server: Command Sender&Receiver Leaved!"));
                                    _threadNumber++;
                                    Task cmdSender = SendCommandTaskAsync(socketForClient, childServerNodeDomain, $"THREAD{_threadNumber}_SOCKET{socketNumber}", childServerNodeSocketNo, cts.Token);
                                    _threadNumber++;
                                    Task cmdReceiver = ReceiveCommandResultAsync(socketForClient, childServerNodeDomain, $"THREAD{_threadNumber}_SOCKET{socketNumber}", childServerNodeSocketNo, cts.Token);

                                    // 当因为socket连接问题导致cmdSender或者cmdReceiver其中一个异常结束了, 那么应该使两个都结束, 特别是cmdSender会干扰新的线程
                                    // 如果旧的cmdSender存在, 它会抢先读取命令任务队列中新的任务, 然后发送给客户端(socket已经释放发送会失败), 新的cmdSend则因为没有读取到队列中的任务导致新的任务发放失败
                                    // 旧的cmdSender读取发放任务会异常一次, 然后cmdSender会结束, 后面就会正常了
                                    await Task.WhenAny(cmdSender, cmdReceiver);
                                    cts.Cancel();
                                    _logger.LogCritical("Center Server[{thread}]: 命令发送器或者命令接收器已经停止!", threadSocketNo);
                                    stopThread = true;
                                }
                                else
                                {
                                    // socketForClient是中心服务器接收到子服务器(或其他客户端)主动链接过来的客户端专用socket, 如果当前是子节点服务器, 没有此socket, 无逻辑需要处理;
                                    // 当前子服务器应该使用程序启动时连接到服务端的socket, 对应中心服务器的socketForClient
                                }
                            }
                            _logger.LogCritical("TCP Service[{thread}]: need break thread: {stopThread};", threadSocketNo, stopThread);
                            if (stopThread)
                            {
                                break;
                            }
                        }
                        _logger.LogInformation($"Center Server: TCP Service - client closed");
                    });
                    #endregion
                }
            }, stoppingToken);
            _logger.LogInformation("TCP Service: 发布服务已经开启");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 命令任务发送器, 通知子节点执行指定的命令
        /// </summary>
        /// <returns></returns>
        async Task SendCommandTaskAsync(Socket socketForClient, string childServerNodeDomain, string threadNo = "", string childServerNodeSocketNo = "", CancellationToken cancellationToken = default)
        {
            try
            {
                while (true)
                {
                    // 一直不断地读取给当前domain的命令执行任务
                    var commandInfoTaskDto = await AnythingService.GetCommandTaskAsync(childServerNodeDomain, cancellationToken);
                    if (commandInfoTaskDto is null)
                    {
                        _logger.LogCritical("Center Server - Command Sender[{threadNo}] -> {childServerNodeDomain}_Node-{childServerNodeSocketNo}: 队列中读取到的命令任务为null, 终止当前命令发送器", threadNo, childServerNodeDomain, childServerNodeSocketNo);
                        break;
                    }
                    _logger.LogCritical("Center Server - Command Sender[{threadNo}] -> {childServerNodeDomain}_Node-{childServerNodeSocketNo}: 队列中读取到一条给自服务节点命令任务: {CommandName}", threadNo, childServerNodeDomain, childServerNodeSocketNo, commandInfoTaskDto.CommandName);
                    // 处理一条命令
                    var sendResult = await socketForClient.SendTextAsync($"{commandInfoTaskDto.CommandId};;;;{commandInfoTaskDto.CommandExecuteNo}{_endFlag}");
                    _logger.LogCritical("Center Server - Command Sender[{threadNo}] -> {childServerNodeDomain}_Node-{childServerNodeSocketNo}: 成功向子节点发送命令的执行任务({sendResult})", threadNo, childServerNodeDomain, childServerNodeSocketNo, sendResult);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Center Server: 命令发送器工作异常: {ex}", ex.ToString());
                throw;
            }
        }

        /// <summary>
        /// 命令结果接收器, 专门用于接收并处理命令的结果
        /// </summary>
        /// <param name="socketForClient"></param>
        /// <param name="childServerNodeDomain"></param>
        /// <returns></returns>
        async Task ReceiveCommandResultAsync(Socket socketForClient, string childServerNodeDomain, string threadNo = "", string childServerNodeSocketNo = "", CancellationToken cancellationToken = default)
        {
            byte[] buffer = new byte[_bufferSize];
            int logNo = 1;
            try
            {
                _logger.LogInformation("Center Server - CommandResult Receiver[{threadNo}] <- {childServerNodeDomain}_Node-{childServerNodeSocketNo}: 启动, 开始等待接收子节点{Domain}的消息", threadNo, childServerNodeDomain, childServerNodeSocketNo, childServerNodeDomain);
                while (true)
                {
                    logNo = 1; // 第二次循环过来logNo就不是1了, 初始化为1
                    string commandResultJson = string.Empty;

                    // 连接不通时会抛异常: SocketException (110): Connection timed out
                    // 客户端突然关闭时: SocketException (104): Connection reset by peer
                    (string msgFromNode, int receivedLength) = await socketForClient.ReceiveAllTextAsync(buffer, IsEndFlag, cancellationToken: cancellationToken);
                    if (await socketForClient.CheckIsCloseMsgAsync(receivedLength, buffer))
                    {
                        _logger.LogCritical("Center Server - CommandResult Receiver[{threadNo}] <- {childServerNodeDomain}_Node-{childServerNodeSocketNo}: {logNo}. 接收到来自子节点的关闭信号, 将关闭与子节点的socket连接, Received length:{length}", threadNo, childServerNodeDomain, childServerNodeSocketNo, logNo++, receivedLength);
                        break;
                    }
                    else if (SocketHelper.CheckIsKeepAliveMsg(receivedLength, buffer))
                    {
                        string logFileName = string.IsNullOrWhiteSpace(childServerNodeSocketNo) ? $"{DateTime.Now:yyyy-MM-dd}.log" : $"{DateTime.Now:yyyy-MM-dd}-{childServerNodeSocketNo}.log";
                        await LoggerHelper.RecordLogAsync($"Center Server - CommandResult Receiver[{threadNo}] <- {childServerNodeDomain}_Node-{childServerNodeSocketNo}: keep-alive", _heartbeatLogsDirectory, logFileName);
                        _lastKeepAliveTime = DateTime.Now;
                        await socketForClient.SendTextAsync(_heartbeatMsg);
                        continue;
                    }
                    else
                    {
                        // 对于长消息需考虑粘包问题, 接收的数据也可能不是完整的, 例如没有完整结束: {...14855"}000000{"Succeed":true,"Message":
                        // 也可能开头不完整: "xx消息内容","CommandExecuteNo":"xx编号"}0000
                        //commandResultJson += buffer.GetText(receivedLength);
                        commandResultJson += msgFromNode.Replace(_heartbeatMsg, string.Empty);
                        await LoggerHelper.RecordLogAsync($"Center Server - CommandResult Receiver[{threadNo}] <- {childServerNodeDomain}_Node-{childServerNodeSocketNo}: {logNo}. 成功获取子节点返回命令的执行结果文本: {msgFromNode}", "Commands", $"ReceiveFromChildNode{DateTime.Now:yyyyMMdd}.log");
                        if (receivedLength >= 6)
                        {
                            var last6Bytes = buffer.Skip(receivedLength - 6).Take(6).ToArray();
                            if (IsEndFlag(last6Bytes))
                            {
                                // commandResultJson可能包含多个命令执行结果(粘包:一次性接收到客户端的多次发送), 每个命令执行结果之间以结束标记"000000"分隔
                                foreach(var signleCommandResultJson in commandResultJson.Split(_endFlag, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    AnythingService.SetCommandResult(signleCommandResultJson);
                                }
                            }
                        }
                    }
                }
                _logger.LogInformation("Center Server - CommandResult Receiver[{threadNo}] <- {childServerNodeDomain}_Node-{childServerNodeSocketNo}: {logNo}. 命令结果接收器结束", threadNo, childServerNodeDomain, childServerNodeSocketNo, logNo++);
            }
            catch (Exception ex)
            {
                _logger.LogError("Center Server - CommandResult Receiver[{threadNo}] <- {childServerNodeDomain}_Node-{childServerNodeSocketNo}: {logNo}. 命令结果接收器工作异常, {msg}", threadNo, childServerNodeDomain, childServerNodeSocketNo, logNo++, ex.ToString());
                throw;
            }
        }
        Socket? _toCenterServerConn = null;
        CancellationTokenSource? _centerConnWorkersCts = null;
        int _socketNo = 0;
        /// <summary>
        /// 建立与中心服务器的长连接, 用于接收中心服务器的任务指令
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            // 不为空说明当前服务器为子服务器节点, 需要连接到中心服务器
            if (!string.IsNullOrWhiteSpace(_centerServer))
            {
                // TODO: Task.Factory.StartNew替换为Task.Run这里会失效(接收不到消息), 为什么?(服务端日志为"成功找到子节点{Domain}的连接"; 没有发送任务成功的日志:"成功向子节点{Domain}发送命令的执行任务", 是否是这里[客户端]意外关闭了)
                // 子节点, 子线程中一直尝试接收命令执行任务
                _ = Task.Factory.StartNew(async () =>
                {
                    byte[] buffer = new byte[_bufferSize];

                    while (true)
                    {
                        _socketNo++;
                        
                        // domain - node路径 节点实例标识, 使得可以区分哪台主机的哪个应用(同一台主机根据实例的路径)
                        string socketNo = $"{AppStatus.InstancePath}-Socket{_socketNo}";
                        
                        // 重新连接时需要初始化最后一次心跳时间, 否则会导致心跳检测线程检测到心跳超时
                        _lastKeepAliveTime = DateTime.Now;
                        _toCenterServerConn = null;
                        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        _centerConnWorkersCts = new();
                        _centerConnWorkersCts.Token.Register(() =>
                        {
                            // 当停止服务时, 如果此时StopAsync已经执行完毕, 那么这里_logger已经被释放, 这行代码会抛异常: Cannot access a disposed object. Object name: 'EventLogInternal'
                            // 所以停止服务时, 第一时间调用_toCenterConnWorkersCts.Cancel()来终止连接相关工作线程, 避免这里抛异常
                            _logger.LogCritical("Server Node({domain} - {socketNo}): 触发任务取消: 将终止连接相关的后台工作线程!", _domain, socketNo);
                        });

                        try
                        {
                            await socket.ConnectAsync(_centerServer, _centerServerPort);
                            _toCenterServerConn = socket;

                            string serverNodeConnectParams = $"2;;;;{_domain};;;;{socketNo}";
                            _logger.LogInformation("Server Node({domain} - {socketNo}): 子节点连接到中心服务器, 发送参数: {serverNodeConnectParams}", _domain, socketNo, serverNodeConnectParams);
                            await socket.SendTextAsync(serverNodeConnectParams);

                            #region 子线程发送心跳包
                            // 子线程发送心跳包
                            _ = Task.Factory.StartNew(async () =>
                            {
                                while (true)
                                {
                                    if (_centerConnWorkersCts.IsCancellationRequested)
                                    {
                                        _logger.LogCritical("Server Node({domain} - {socketNo}): 心跳包发送器因收到取消信号而终止!", _domain, socketNo);
                                        break;
                                    }
                                    await Task.Delay(1000 * _heartbeatFrequency);
                                    try
                                    {
                                        // 最后一次通讯时间到现在已经达到了心跳包的发送频率间隔(1s误差), 发送心跳包
                                        var timeSpanSeconds = (DateTime.Now - _lastKeepAliveTime).TotalSeconds;
                                        if (timeSpanSeconds >= _heartbeatFrequency - 1)
                                        {
                                            await socket.SendTextAsync(_heartbeatMsg);
                                            _lastKeepAliveTime = DateTime.Now;
                                            await LoggerHelper.RecordLogAsync($"Server Node({_domain}) - {socketNo}: keep-alive ->", _heartbeatLogsDirectory);
                                        }
                                        else
                                        {
                                            _logger.LogInformation("Server Node({domain} - {socketNo}): 最后一次通讯时间{lasttime}到现在间隔{seconds}/s, 无需发送心跳消息", _domain, socketNo, _lastKeepAliveTime.ToString("yyyy-MM-dd HH:mm:ss"), timeSpanSeconds);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError("Server Node({domain} - {socketNo}): 心跳包发送失败, 稍后将重新连接中心服务器: {ex}", _domain, socketNo, ex.ToString());
                                        _centerConnWorkersCts.Cancel();
                                        break;
                                    }
                                }
                            }, _centerConnWorkersCts.Token);
                            #endregion

                            #region 子线程检测心跳包频率是否正常
                            // 子线程检测心跳包频率是否正常
                            _ = Task.Factory.StartNew(async () =>
                            {
                                while (true)
                                {
                                    if (_centerConnWorkersCts.IsCancellationRequested)
                                    {
                                        _logger.LogCritical("Server Node({domain} - {socketNo}): 心跳包检测器因收到取消信号而终止!", _domain, socketNo);
                                        break;
                                    }
                                    if (_lastKeepAliveTime != DateTime.MinValue && (DateTime.Now - _lastKeepAliveTime).TotalSeconds > _heartbeatFrequency * 2)
                                    {
                                        _logger.LogWarning("Server Node({domain} - {socketNo}): 已经{seconds}/s(LastKeepAliveTime:{LastKeepAliveTime})没有收到心跳包, 将重新建立与中心服务器的连接", _domain, socketNo, _heartbeatFrequency * 2, _lastKeepAliveTime.ToString("yyyy-MM-dd HH:mm:ss"));

                                        // 释放socket连接, 否则ReceiveAsync会一直阻塞(释放了就会因抛异常而停止)
                                        socket.Close();
                                        socket.Dispose();
                                        _centerConnWorkersCts.Cancel();
                                        break;
                                    }
                                    await Task.Delay(1000 * 60);
                                }
                            }, _centerConnWorkersCts.Token);
                            #endregion

                            #region 子节点通过与中心服务器的TCP长连接不断地: 接收命令 -> 执行命令 -> 返回命令结果
                            // 通过与中心服务器的TCP长连接不断地: 接收命令 -> 执行命令 -> 返回命令结果
                            while (true)
                            {
                                (string msgFromCenter, int msgLength) = await socket.ReceiveAllTextAsync(buffer, IsEndFlag);
                                // 更新最后一次与中心服务器通讯时间
                                _lastKeepAliveTime = DateTime.Now;

                                if (msgLength == 0)
                                {
                                    _logger.LogError("Server Node({domain} - {socketNo}): 接收到中心服务器消息长度为0", _domain, socketNo);
                                    break;
                                }
                                else
                                {
                                    string[] arr = msgFromCenter.Split(";;;;");
                                    if (msgFromCenter == "ready_for_new")
                                    {
                                        _logger.LogCritical("Server Node({domain} - {socketNo}): 接收到中心服务器消息ready_for_new", _domain, socketNo);
                                    }
                                    else if (SocketHelper.CheckIsKeepAliveMsg(msgFromCenter))
                                    {
                                        await LoggerHelper.RecordLogAsync($"Server Node({_domain}) - {socketNo}: keep-alive <-", _heartbeatLogsDirectory);
                                        continue;
                                    }
                                    else if (arr.Length < 2)
                                    {
                                        _logger.LogError("Server Node({domain} - {socketNo}): 参数不足{msg}", _domain, socketNo, msgFromCenter);
                                    }
                                    else if (!int.TryParse(arr[0], out int commandId))
                                    {
                                        _logger.LogError("Server Node({domain} - {socketNo}): settingId({settingIdStr})无法转换为整数", _domain, socketNo, arr[0]);
                                    }
                                    else
                                    {
                                        _logger.LogCritical("Server Node({domain} - {socketNo}): 接收到来自中心服务器的命令任务: {msgFromCenter}", _domain, socketNo, msgFromCenter);
                                        string commandExecuteNo = arr[1];
                                        using var scope = _scopeFactory.CreateScope();
                                        var anythingService = scope.ServiceProvider.GetRequiredService<AnythingService>();
                                        // 执行命令
                                        IAsyncEnumerable<CommandResult> commandResults = anythingService.ExecuteAsync(new() { CommandId = commandId, CommandExecuteNo = commandExecuteNo });
                                        int batchNo = 0;
                                        await foreach (var commandResult in commandResults)
                                        {
                                            commandResult.CommandExecuteNo += $"-{batchNo}";
                                            string commandResultJson = JsonConvert.SerializeObject(commandResult);
                                            await LoggerHelper.RecordLogAsync($"Server Node({_domain} - {socketNo}): 向中心服务器响应命令执行结果:{commandResultJson}", "Commands", $"ResponseCommandResult{DateTime.Now:yyyyMMdd}");
                                            await socket.SendTextAsync($"{commandResultJson}{_endFlag}");
                                            batchNo++;
                                        }
                                        // 发送信号, 表示已经发送完所有执行结果CommandResult
                                        string endCommandResult = $@"{{""Succeed"":false,""Message"":"""",""CommandExecuteNo"":""{commandExecuteNo}-cmd-end""}}";
                                        await socket.SendTextAsync($"{endCommandResult}{_endFlag}");
                                        _lastKeepAliveTime = DateTime.Now;
                                    }
                                }
                            }
                            #endregion
                        }
                        catch (Exception ex)
                        {
                            if (!_centerConnWorkersCts.IsCancellationRequested)
                            {
                                _centerConnWorkersCts.Cancel();
                            }
                            _logger.LogError("Server Node({domain} - {socketNo}): 释放与中心服务器({server}:{port})的socket, 稍后将重新连接中心服务器: {message}", _domain, socketNo, _centerServer, _centerServerPort, ex.ToString());
                            await Task.Delay(1000 * _reconnectFrequency);
                        }
                        finally
                        {
                            _logger.LogCritical("Server Node({domain} - {socketNo}): finally 即将释放与中心服务器的连接", _domain, socketNo);
                        }
                    }
                });

                _logger.LogInformation("Server Node({domain}): 程序启动阶段, 开始连接中心服务器", _domain);
            }

            return base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_toCenterServerConn is not null)
            {
                await _toCenterServerConn.NotifyCloseAsync();
                byte[] buffer = new byte[1024];
                int received = await _toCenterServerConn.ReceiveAsync(buffer, SocketFlags.None);
                _logger.LogCritical("Server Node({domain}): 即将关闭应用, 发送断开连接的请求后, 收到服务端的返回信息({received}) - {byte0}{byte1}", _domain, received, buffer[0], buffer[1]);
            }
            _centerConnWorkersCts?.Cancel();
            await base.StopAsync(cancellationToken);
        }

        bool IsEndFlag(byte[] endFlag)
        {
            return endFlag.Length >= 6 && endFlag[0] == _zeroByteValue && endFlag[1] == _zeroByteValue && endFlag[2] == _zeroByteValue && endFlag[3] == _zeroByteValue && endFlag[4] == _zeroByteValue && endFlag[5] == _zeroByteValue;
        }
    }
}
