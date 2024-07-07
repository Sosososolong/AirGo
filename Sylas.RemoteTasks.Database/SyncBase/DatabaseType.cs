namespace Sylas.RemoteTasks.Database.SyncBase
{
    /// <summary>
    /// 数据库类型
    /// </summary>
    public enum DatabaseType
    {
        /// <summary>
        /// MySql数据库
        /// </summary>
        MySql,
        /// <summary>
        /// SqlServer数据库
        /// </summary>
        SqlServer,
        /// <summary>
        /// Oracle数据库
        /// </summary>
        Oracle,
        /// <summary>
        /// postgresql
        /// </summary>
        Pg,
        /// <summary>
        /// 达梦数据库
        /// </summary>
        Dm,
        /// <summary>
        /// Sqlite数据库
        /// </summary>
        Sqlite,
        /// <summary>
        /// 本地SqlServer
        /// </summary>
        MsSqlLocalDb
    }
}
