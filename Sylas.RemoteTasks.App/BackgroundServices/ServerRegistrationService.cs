using Microsoft.Extensions.DependencyInjection;
using MySqlX.XDevAPI.Relational;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.App.RemoteHostModule.Anything;
using Sylas.RemoteTasks.Common;
using Sylas.RemoteTasks.Database;
using Sylas.RemoteTasks.Database.Dtos;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils.CommandExecutor;
using System.Collections.Concurrent;
using System.DirectoryServices.ActiveDirectory;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Sylas.RemoteTasks.App.BackgroundServices
{
    /// <summary>
    /// 服务启动时, 将当前服务信息注册到服务列表中(StartAsync和StopAsync事件中注册注销服务)
    /// </summary>
    /// <param name="scopeFactory"></param>
    /// <param name="logger"></param>
    public class ServerRegistrationService(IServiceScopeFactory scopeFactory, ILogger<ServerRegistrationService> logger) : BackgroundService
    {
        string _hostName = string.Empty;
        List<string> _serverIpList = [];
        string _serverips = string.Empty;
        
        const string _table = "ServerNodes";
        string _idFieldName = string.Empty;
        string _idFieldValueStr = string.Empty;

        /// <summary>
        /// 后台任务的主要逻辑
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("ServerNode: {ServerNode}后台服务启动", _hostName);

            // AnythingFlow任务调度
            _ = AnythingFlowScheduleAsync();

            return Task.CompletedTask;
        }
        /// <summary>
        /// 服务启动时, 注册当前服务节点
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            // 使用数据表保存服务记录, 表不存在则创建表
            await CreateTableIfNotExistAsync();

            _hostName = Dns.GetHostName();
            IPHostEntry ipHost = Dns.GetHostEntry(_hostName);
            var ipAddresses = ipHost.AddressList.Select(x => x.ToString()).Where(x => Regex.IsMatch(x, @"\d+\.\d+\.\d+\.\d+")).ToList();
            _serverIpList = ipAddresses;
            _serverips = string.Join(",", ipAddresses);

            var idKeyAndIdStrValue = await QueryCurrentServerNode();
            if (idKeyAndIdStrValue is not null)
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IDatabaseProvider>();
                var success = await db.UpdateAsync(_table, new Dictionary<string, object> {
                        { idKeyAndIdStrValue.Item1, idKeyAndIdStrValue.Item2 },
                        { "state", 1 }
                    });
                logger.LogInformation("ServerNode 服务启动, 更新服务(host: {host}; serverips: {iplist})状态: {result}", _hostName, _serverips, success ? "更新成功" : "更新失败");
            }
            else
            {
                var newServerNode = new Dictionary<string, object?>{
                    { "host", _hostName },
                    { "iplist", _serverips },
                    { "state", 1 },
                };

                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IDatabaseProvider>();
                await db.InsertDataAsync(_table, [newServerNode]);
                logger.LogInformation("ServerNode 服务启动, 添加服务记录: host: {host}; serverips: {iplist}", _hostName, _serverips);
                _ = await QueryCurrentServerNode() ?? throw new Exception($"为查询到当前添加的ServerNode记录:[host={_hostName}, iplist={_serverips}]");
            }

            await base.StartAsync(cancellationToken);
        }

        /// <summary>
        /// 服务停止时, 注销服务
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDatabaseProvider>();
            var success = await db.UpdateAsync(_table, new Dictionary<string, object> {
                { _idFieldName, _idFieldValueStr },
                { "state", 0 }
            });
            logger.LogInformation("ServerNode 服务停止, 将服务状态改为0: {result}", success ? "操作成功" : "操作失败");
            await base.StopAsync(cancellationToken);
        }

        /// <summary>
        /// 查询当前服务节点的数据记录, 并将id字段名和id值保存到字段中
        /// </summary>
        /// <param name="serverNodeRecord"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        async Task<Tuple<string, string>?> QueryCurrentServerNode()
        {
            var search = new DataSearch
            {
                PageIndex = 1,
                PageSize = 100000,
                Filter = new DataFilter
                {
                    FilterItems = [
                        new("host", "=", _hostName)
                    ]
                }
            };

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDatabaseProvider>();
            var servers = await db.QueryPagedDataAsync<IDictionary<string, object>>(_table, search);

            foreach (var serverNodeRecord in servers.Data)
            {
                string hostNameKey = serverNodeRecord.Keys.FirstOrDefault(x => x.Equals("host", StringComparison.OrdinalIgnoreCase)) ?? throw new Exception("数据库nodes表缺少Host字段");

                var dbNodeHost = serverNodeRecord[hostNameKey]?.ToString();
                if (!string.IsNullOrEmpty(dbNodeHost) && dbNodeHost.Equals(_hostName))
                {
                    string ipListKey = serverNodeRecord.Keys.FirstOrDefault(x => x.Equals("iplist", StringComparison.OrdinalIgnoreCase)) ?? throw new Exception("数据库nodes表缺少IpList字段");

                    var dbNodeIpListStr = serverNodeRecord[ipListKey]?.ToString() ?? string.Empty;
                    if (dbNodeIpListStr.Length == _serverips.Length && _serverIpList.All(x => dbNodeIpListStr.Contains(x)))
                    {
                        // 找到了数据库中对应的node记录
                        string idKey = serverNodeRecord.Keys.FirstOrDefault(x => x.Equals("id", StringComparison.OrdinalIgnoreCase)) ?? throw new Exception("nodes表缺少Id字段");
                        _idFieldName = idKey;

                        string idValue = serverNodeRecord[idKey]?.ToString() ?? throw new Exception($"当前node记录[host={_hostName}, iplist={dbNodeIpListStr}]主键值为空");
                        _idFieldValueStr = idValue;

                        return Tuple.Create(idKey, idValue);
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// 表不存在则创建表
        /// </summary>
        /// <returns></returns>
        async Task CreateTableIfNotExistAsync()
        {
            List<ColumnInfo> columns =
            [
                new() { ColumnCode = "Id", IsPK = 1, ColumnCSharpType = "int", ColumnType = "int", IsNullable = 0 },
                new() { ColumnCode = "Host", IsPK = 0, ColumnCSharpType = "string", ColumnType = "varchar", ColumnLength = "50", IsNullable = 0 },
                new() { ColumnCode = "IpList", IsPK = 0, ColumnCSharpType = "string", ColumnType = "varchar", ColumnLength = "255", IsNullable = 0 },
                new() { ColumnCode = "State", IsPK = 0, ColumnCSharpType = "bool", ColumnType = "bit", IsNullable = 0 },
                new() { ColumnCode = "CreateTime", IsPK = 0, ColumnCSharpType = "datetime", ColumnType = "timestamp", IsNullable = 0 },
                new() { ColumnCode = "UpdateTime", IsPK = 0, ColumnCSharpType = "datetime", ColumnType = "timestamp", IsNullable = 0 }
            ];
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDatabaseProvider>();
            await db.CreateTableIfNotExistAsync(_table, columns);
        }
        readonly ConcurrentDictionary<int, (AnythingFlow, CancellationTokenSource)> runningSchedules = [];
        /// <summary>
        /// 处理AnythingFlow任务调度
        /// </summary>
        /// <returns></returns>
        async Task AnythingFlowScheduleAsync()
        {
            // 防止其他服务还没有完全注册到DI容器中
            await Task.Delay(TimeSpan.FromSeconds(5));

            while (true)
            {
                try
                {
                    // scope放到while循环里面, 每次循环完成释放scope(以及scope内产生的对象), 否则scope中的大对象可能很长时间不释放(内存泄漏)
                    IEnumerable<AnythingFlow> flowsQuery;
                    using (var scope = scopeFactory.CreateScope())
                    {
                        var flowRepo = scope.ServiceProvider.GetRequiredService<RepositoryBase<AnythingFlow>>();
                        flowsQuery = (await flowRepo.GetPageAsync(new DataSearch() { PageIndex = 1, PageSize = 99999 })).Data;
                    }
                    var scheduledFlows = flowsQuery.Where(x => !string.IsNullOrWhiteSpace(x.Schedule) && !string.IsNullOrWhiteSpace(x.ScheduleDomain) && x.ScheduleDomain == _hostName).ToArray();
                    logger.LogInformation("任务调度相关Flow共: {count}条记录", scheduledFlows.Length);
                    foreach (var flow in scheduledFlows)
                    {
                        // 为每个定时任务创建一个独立的执行线程, 支持任务取消
                        CancellationTokenSource tokenSource = new();
                        if (runningSchedules.TryGetValue(flow.Id, out (AnythingFlow, CancellationTokenSource) flowScheduleAndTokenSource))
                        {
                            if (flowScheduleAndTokenSource.Item1.Schedule == flow.Schedule && flowScheduleAndTokenSource.Item1.OnExecuted == flow.OnExecuted)
                            {
                                continue;
                            }
                            else
                            {
                                LoggerHelper.WriteLog($"[{flow.Title}]的任务调度发生变化, 发起新的任务调度处理器", logDirectory: nameof(AnythingFlow));
                                flowScheduleAndTokenSource.Item2.Cancel();
                                runningSchedules.TryRemove(flow.Id, out _);
                            }
                        }
                        runningSchedules.AddOrUpdate(flow.Id, (flow, tokenSource), (key, oldValue) => (flow, tokenSource));

                        // 子线程中执行任务调度
                        async Task scheduleExecutor()
                        {
                            DateTime? executeTime = null;
                            while (true)
                            {
                                if (tokenSource.IsCancellationRequested)
                                {
                                    LoggerHelper.WriteLog($"[{flow.Title}] Task canceled", logDirectory: nameof(AnythingFlow));
                                    break;
                                }
                                LoggerHelper.WriteLog($"[{flow.Title}]正在检查处理任务调度...({Environment.CurrentManagedThreadId})", logDirectory: nameof(AnythingFlow));
                                // 重置为1分钟, 后续两种情况: 1小于70s后, 改为剩余秒数 2任务执行完毕要马上计算新的任务了,改为1ms
                                TimeSpan waitTime = TimeSpan.FromMinutes(1);
                                DateTime time = DateTime.Now;
                                bool canExecute = false;
                                int secondsToWait = GetScheduleRestTime(flow.Schedule!);
                                executeTime ??= DateTime.Now.AddSeconds(secondsToWait);
                                LoggerHelper.WriteLog($"下次执行时间:{executeTime}", logDirectory: nameof(AnythingFlow));
                                // 只要剩余时间小于70s(到达执行时间的倒数第二次循环)就会重新计算剩余等待的时间, 除非剩余时间已经小于1s了
                                if (time < executeTime)
                                {
                                    double left = (executeTime.Value - time).TotalMilliseconds;
                                    // 只要剩余时间小于70s(到达执行时间的倒数第二次循环)就会重新计算剩余等待的时间, 除非剩余时间已经小于1s了
                                    if (left <= 1000)
                                    {
                                        canExecute = true;
                                    }
                                    else
                                    {
                                        if (left < 1000 * 70)
                                        {
                                            waitTime = TimeSpan.FromMilliseconds(left);
                                            LoggerHelper.WriteLog($"距离执行时间还剩下{left / 1000}/秒, {waitTime.TotalMilliseconds}/毫秒后将会执行", logDirectory: nameof(AnythingFlow));
                                        }
                                    }
                                }
                                else
                                {
                                    canExecute = true;
                                }

                                if (canExecute)
                                {
                                    bool success = false;
                                    LoggerHelper.WriteLog($"开始执行任务...", logDirectory: nameof(AnythingFlow));
                                    using var innerScope = scopeFactory.CreateScope();
                                    var anythingService = innerScope.ServiceProvider.GetRequiredService<AnythingService>();
                                    string result = string.Empty;
                                    try
                                    {
                                        var anythingIdArr = flow.AnythingIds.Split(',');
                                        List<string> messages = [];
                                        foreach (var id in anythingIdArr)
                                        {
                                            var anythingInfo = await anythingService.GetAnythingInfoBySettingIdAsync(Convert.ToInt32(id));
                                            foreach (var cmd in anythingInfo.Commands)
                                            {
                                                var commandResults = anythingService.ExecuteAsync(new CommandInfoInDto() { CommandId = cmd.Id });
                                                await foreach (CommandResult commandResult in commandResults)
                                                {
                                                    if (!string.IsNullOrWhiteSpace(commandResult.Message))
                                                    {
                                                        messages.Add($"{cmd.Name}: {commandResult.Message}");
                                                    }
                                                }
                                            }
                                        }
                                        result = JsonConvert.SerializeObject(messages);
                                        LoggerHelper.WriteLog(result, logDirectory: nameof(AnythingFlow));
                                        success = true;
                                        executeTime = null;
                                        waitTime = TimeSpan.FromMilliseconds(1);
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine(ex.Message);
                                    }
                                    finally
                                    {
                                        var now = DateTime.Now;
                                        LoggerHelper.WriteLog($"结束, 耗时: {(now - time).TotalSeconds}/s{Environment.NewLine}", logDirectory: nameof(AnythingFlow));
                                    }
                                    // 执行成功
                                    if (success)
                                    {
                                        // 执行完毕后, 处理结果
                                        if (!string.IsNullOrWhiteSpace(flow.OnExecuted))
                                        {
                                            LoggerHelper.WriteLog($"开始执行{flow.OnExecuted}:", logDirectory: nameof(AnythingFlow));
                                            string systemCmd = $"{flow.OnExecuted} -- \"{Convert.ToBase64String(Encoding.UTF8.GetBytes(result))}\"";
                                            try
                                            {
                                                await foreach (var item in new SystemCmd().ExecuteAsync(systemCmd))
                                                {
                                                    LoggerHelper.WriteLog($"{item.Message}{Environment.NewLine}", logDirectory: nameof(AnythingFlow));
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                logger.LogError("执行脚本失败: {err}", ex.Message);
                                            }
                                        }
                                    }
                                }
                                await Task.Delay(waitTime);
                            }
                        }
                        _ = scheduleExecutor();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                await Task.Delay(TimeSpan.FromSeconds(10), CancellationToken.None);
            }
        }

        //async int GetRestTime(string cron)
        //{
        //    var now = DateTime.UtcNow;
        //    // arr[0]秒 arr[1]分 arr[2]时
        //    var arr = cron.Split(' ');

        //    bool secondsHasValue = int.TryParse(arr[0], out int seconds);
        //    bool minutesHasValue = int.TryParse(arr[1], out int minutes);
        //    bool hoursHasValue = int.TryParse(arr[1], out int hours);
        //    if (secondsHasValue)
        //    {
        //        if (secondsHasValue && !minutesHasValue && !hoursHasValue)
        //        {
        //            // 每分钟的seconds秒执行一次
        //        }
        //    }

        //    throw new Exception($"暂未实现表达式{cron}");
        //}
        readonly ConcurrentDictionary<string, int> cronResolveCache = [];
        int GetScheduleRestTime(string cron)
        {
            if (cronResolveCache.TryGetValue(cron, out int value))
            {
                return value;
            }
            // 返回距下一次触发的秒数
            // 支持简单的三段 cron: "秒 分 时"
            // 每段支持: "*", "*/n", "a-b", "a-b/n", "x,y,z", 单个数字

            var now = DateTime.Now;
            var arr = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // 确保有三段
            if (arr.Length < 3)
            {
                throw new Exception($"无效的cron表达式:{cron}");
            }

            List<int> ParseField(string token, int min, int max)
            {
                var result = new HashSet<int>();
                var parts = token.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var rawPart in parts)
                {
                    var part = rawPart.Trim();
                    if (part == "*")
                    {
                        for (int i = min; i <= max; i++) result.Add(i);
                        continue;
                    }

                    // 支持 step "/"
                    if (part.Contains('/'))
                    {
                        var seg = part.Split('/');
                        if (seg.Length != 2) throw new Exception($"无效的cron段:{part}");
                        var basePart = seg[0];
                        if (!int.TryParse(seg[1], out int step) || step <= 0) throw new Exception($"无效的步长:{seg[1]}");
                        int rangeStart = min;
                        int rangeEnd = max;
                        if (string.IsNullOrWhiteSpace(basePart) || basePart == "*")
                        {
                            rangeStart = min; rangeEnd = max;
                        }
                        else if (basePart.Contains('-'))
                        {
                            var r = basePart.Split('-');
                            if (r.Length != 2) throw new Exception($"无效的范围:{basePart}");
                            rangeStart = int.Parse(r[0]); rangeEnd = int.Parse(r[1]);
                        }
                        else if (int.TryParse(basePart, out int baseNum))
                        {
                            rangeStart = baseNum; rangeEnd = max;
                        }
                        for (int i = rangeStart; i <= rangeEnd; i += step)
                        {
                            if (i >= min && i <= max) result.Add(i);
                        }
                        continue;
                    }

                    // 支持 range "a-b"
                    if (part.Contains('-'))
                    {
                        var r = part.Split('-');
                        if (r.Length != 2) throw new Exception($"无效的范围:{part}");
                        int s = int.Parse(r[0]); int e = int.Parse(r[1]);
                        for (int i = s; i <= e; i++) if (i >= min && i <= max) result.Add(i);
                        continue;
                    }

                    // 单个数字
                    if (int.TryParse(part, out int val))
                    {
                        if (val >= min && val <= max) result.Add(val);
                        else throw new Exception($"数值超出范围:{part}");
                        continue;
                    }

                    throw new Exception($"无法解析cron段:{part}");
                }

                var list = result.ToList();
                list.Sort();
                return list;
            }

            var secondsList = ParseField(arr[0], 0, 59);
            var minutesList = ParseField(arr[1], 0, 59);
            var hoursList = ParseField(arr[2], 0, 23);

            // 寻找未来 7 天内的第一个匹配时间
            for (int dayOffset = 0; dayOffset <= 7; dayOffset++)
            {
                var date = now.Date.AddDays(dayOffset);
                foreach (var hour in hoursList)
                {
                    if (dayOffset == 0 && hour < now.Hour) continue;
                    foreach (var minute in minutesList)
                    {
                        if (dayOffset == 0 && hour == now.Hour && minute < now.Minute) continue;
                        foreach (var second in secondsList)
                        {
                            if (dayOffset == 0 && hour == now.Hour && minute == now.Minute && second <= now.Second) continue;

                            DateTime candidate;
                            try
                            {
                                candidate = new DateTime(date.Year, date.Month, date.Day, hour, minute, second, DateTimeKind.Utc);
                            }
                            catch
                            {
                                // 非法时间跳过（理论上不会发生，因为 hour/minute/second 已被限制）
                                continue;
                            }

                            var diff = (candidate - now).TotalSeconds;
                            if (diff > 0)
                            {
                                return (int)Math.Ceiling(diff);
                            }
                        }
                    }
                }
            }

            throw new Exception($"未在可接受范围内找到下次触发时间, 表达式:{cron}");
        }
    }
}
