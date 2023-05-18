using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.Database.SyncBase;
using static Sylas.RemoteTasks.App.RemoteHostModule.StartupHelper;

namespace Sylas.RemoteTasks.App.Operations
{
    public class DataHandlerSyncDataToDb
    {
        private readonly DatabaseInfo _databaseInfo;

        public DataHandlerSyncDataToDb(IServiceScopeFactory serviceScopeFactory)
        {
            var scope = serviceScopeFactory.CreateScope();
            _databaseInfo = scope.ServiceProvider.GetRequiredService<DatabaseInfo>();
        }
        public async Task Start(string table, object dataSource, string db = "")
        {
            IEnumerable<JToken> data = dataSource is IEnumerable<JToken> ? (IEnumerable<JToken>)dataSource : new List<JToken>() { JToken.FromObject(dataSource) };
            await _databaseInfo.SyncDatabaseAsync(table, data, Array.Empty<string>(), sourceIdField: "Id", targetIdField: "Id", db: db);
        }
    }
}
