using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.Database.SyncBase;

namespace Sylas.RemoteTasks.App.DataHandlers
{
    public class DataHandlerSyncDataToDb : IDataHandler
    {
        private readonly DatabaseInfo _databaseInfo;

        public DataHandlerSyncDataToDb(IServiceScopeFactory serviceScopeFactory)
        {
            var scope = serviceScopeFactory.CreateScope();
            _databaseInfo = scope.ServiceProvider.GetRequiredService<DatabaseInfo>();
        }
        
        static readonly string[] _connectionStringSubStrings = ["data source=", "initial catalog=", "server="];
        public async Task StartAsync(params object[] parameters)
        {
            if (parameters.Length < 2)
            {
                throw new Exception($"DataHandler: \"{nameof(DataHandlerSyncDataToDb)}\"参数不足(table, dataSource为空)");
            }
            string table = parameters[0]?.ToString() ?? throw new Exception(nameof(parameters) + "[0] - table为空");
            object dataSource = parameters[1];
            string targetDb = $"{parameters[2]}";
            string idField = "";
            if (parameters.Length >= 4)
            {
                idField = parameters[3]?.ToString() ?? "";
            }
            if (string.IsNullOrWhiteSpace(idField))
            {
                idField = "id";
            }

            IEnumerable<JToken> data = dataSource is IEnumerable<JToken> enumerableData ? enumerableData : new List<JToken>() { JToken.FromObject(dataSource) };

            // 数据库连接字符串中, sqlserver, oracle, sqite包含"Data Source=xxx"; mysql, mslocaldb包含"Server=xxx"
            if (_connectionStringSubStrings.Any(x => targetDb.Contains(x, StringComparison.OrdinalIgnoreCase)))
            {
                await _databaseInfo.SyncDataToDbWithTargetConnectionStringAsync(table, data, [], sourceIdField: idField, targetIdField: idField, targetDb);
            }
            else
            {
                await _databaseInfo.SyncDataToDbAsync(table, data, [], sourceIdField: idField, targetIdField: idField, targetDb);
            }
        }
    }
}
