namespace Sylas.RemoteTasks.Database.Dtos
{
    /// <summary>
    /// 从json文件同步数据Dto参数
    /// </summary>
    public class SyncFromJsonsDto
    {
        /// <summary>
        /// 目标数据库连接字符串
        /// </summary>
        public string TargetConnectionString { get; set; } = string.Empty;
        /// <summary>
        /// 目标数据库的数据表
        /// </summary>
        public string TargetTables { get; set; } = string.Empty;
    }
}
