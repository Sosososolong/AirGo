using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.Database.SyncBase;
using static Sylas.RemoteTasks.App.RemoteHostModule.StartupHelper;

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
        public async Task StartAsync(params object[] parameters)
        {
            if (parameters.Length < 2)
            {
                throw new Exception($"DataHandler: \"{nameof(DataHandlerSyncDataToDb)}\"参数不足(table, dataSource为空)");
            }
            string table = parameters[0]?.ToString() ?? throw new Exception(nameof(parameters) + "[0] - table为空");
            object dataSource = parameters[1];
            string connectionString = $"{parameters[2]}";
            IEnumerable<JToken> data = dataSource is IEnumerable<JToken> enumerableData ? enumerableData : new List<JToken>() { JToken.FromObject(dataSource) };
            await _databaseInfo.SyncDatabaseWithTargetConnectionStringAsync(table, data, Array.Empty<string>(), connectionString, sourceIdField: "Id", targetIdField: "Id");
        }
    }
}
