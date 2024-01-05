namespace Sylas.RemoteTasks.Database.SyncBase
{
    public class DbConnectionDetial
    {
        public DbConnectionDetial()
        {
            
        }
        public DbConnectionDetial(string db, DatabaseType databaseType)
        {
            Database = db;
            DatabaseType = databaseType;
        }
        public string Host { get; set; } = "";
        public int Port { get; set; }
        public string Database { get; set; } = "";
        public string Account { get; set; } = "";
        public string Password { get; set; } = "";
        public string InstanceName { get; set; } = "";
        public DatabaseType DatabaseType { get; set; }
    }
}
