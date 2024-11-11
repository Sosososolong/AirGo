using Newtonsoft.Json;
using Sylas.RemoteTasks.Database.SyncBase;

namespace Sylas.RemoteTasks.App.DataHandlers
{
    public class DataHandlerCreateTable : IDataHandler
    {
        private readonly DatabaseInfo _databaseInfo;

        public DataHandlerCreateTable(IServiceScopeFactory serviceScopeFactory)
        {
            var scope = serviceScopeFactory.CreateScope();
            _databaseInfo = scope.ServiceProvider.GetRequiredService<DatabaseInfo>();
        }

        public async Task StartAsync(params object[] parameters)
        {
            if (parameters.Length < 3)
            {
                throw new Exception($"DataHandler\"{nameof(DataHandlerCreateTable)}\"参数不足");
            }
            string db = parameters[0]?.ToString() ?? throw new Exception(nameof(parameters) + "[0] - db为空");
            string table = parameters[1]?.ToString() ?? throw new Exception(nameof(parameters) + "[1] - table为空");
            object colInfos = parameters[2];
            object? tableRecords = parameters.Length == 4 ? parameters[3] : null;

            var json = JsonConvert.SerializeObject(colInfos);
            var columnInfos = JsonConvert.DeserializeObject<List<ColumnInfo>>(json) ?? throw new Exception($"数据库{db}数据表{table}字段集合获取失败");
            await _databaseInfo.CreateTableIfNotExistAsync(table, columnInfos, db);
        }
    }
}
