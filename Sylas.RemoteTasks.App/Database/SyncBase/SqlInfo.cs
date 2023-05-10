using Dapper;

namespace Sylas.RemoteTasks.App.Database.SyncBase
{
    public class SqlInfo
    {
        public string Sql { get; set; }
        public DynamicParameters Parameters { get; set; }
        public SqlInfo(string sql, DynamicParameters parameters)
        {
            Sql = sql;
            Parameters = parameters;
        }
    }
}
