using Dapper;

namespace Sylas.RemoteTasks.App.Database.SyncBase
{
    public class SqlInfo
    {
        public string Sql { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public SqlInfo(string sql, Dictionary<string, object> parameters)
        {
            Sql = sql;
            Parameters = parameters;
        }
    }
}
