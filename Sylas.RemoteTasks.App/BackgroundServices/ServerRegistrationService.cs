using Sylas.RemoteTasks.Database;
using Sylas.RemoteTasks.Database.SyncBase;
using System.Net;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.App.BackgroundServices
{
    /// <summary>
    /// 服务启动时, 将当前服务信息注册到服务列表中(StartAsync和StopAsync事件中注册注销服务)
    /// </summary>
    /// <param name="scopeFactory"></param>
    /// <param name="logger"></param>
    public class ServerRegistrationService(IServiceScopeFactory scopeFactory, ILogger<ServerRegistrationService> logger) : BackgroundService
    {
        const string _key = "serverips:mywebapp";
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
            logger.LogInformation($"ServerNode 后台服务启动");
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
                var newServerNode = new Dictionary<string, object>{
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
    }
}
