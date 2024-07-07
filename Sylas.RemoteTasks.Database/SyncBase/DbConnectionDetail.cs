namespace Sylas.RemoteTasks.Database.SyncBase
{
    /// <summary>
    /// 描述数据库连接信息
    /// </summary>
    public class DbConnectionDetail
    {
        /// <summary>
        /// 默认值初始化
        /// </summary>
        public DbConnectionDetail()
        {
            
        }
        /// <summary>
        /// 使用数据库和数据库类型初始化数据库连接信息
        /// </summary>
        /// <param name="db"></param>
        /// <param name="databaseType"></param>
        public DbConnectionDetail(string db, DatabaseType databaseType)
        {
            Database = db;
            DatabaseType = databaseType;
        }
        /// <summary>
        /// 服务器地址
        /// </summary>
        public string Host { get; set; } = "";
        /// <summary>
        /// 端口
        /// </summary>
        public int Port { get; set; }
        /// <summary>
        /// 数据库
        /// </summary>
        public string Database { get; set; } = "";
        /// <summary>
        /// 账号
        /// </summary>
        public string Account { get; set; } = "";
        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get; set; } = "";
        /// <summary>
        /// 实例名
        /// </summary>
        public string InstanceName { get; set; } = "";
        /// <summary>
        /// 数据库类型
        /// </summary>
        public DatabaseType DatabaseType { get; set; }
    }
}
