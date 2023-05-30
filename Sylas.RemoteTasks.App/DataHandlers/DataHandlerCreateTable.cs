using Newtonsoft.Json;
using Sylas.RemoteTasks.App.Database.SyncBase;

namespace Sylas.RemoteTasks.App.DataHandlers
{
    public class DataHandlerCreateTable
    {
        private readonly DatabaseInfo _databaseInfo;

        public DataHandlerCreateTable(IServiceScopeFactory serviceScopeFactory)
        {
            var scope = serviceScopeFactory.CreateScope();
            _databaseInfo = scope.ServiceProvider.GetRequiredService<DatabaseInfo>();
        }
        public async Task Start(string db, string table, object colInfos, object? tableRecords = null)
        {
            var json = JsonConvert.SerializeObject(colInfos);
            var columnInfos = JsonConvert.DeserializeObject<List<ColumnInfo>>(json) ?? throw new Exception($"数据库{db}数据表{table}字段集合获取失败");
            await _databaseInfo.CreateTableIfNotExistAsync(db, table, columnInfos, tableRecords);
        }
    }
}
