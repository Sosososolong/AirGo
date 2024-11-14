namespace Sylas.RemoteTasks.Database.SyncBase
{
    /// <summary>
    /// 动态查询参数
    /// </summary>
    public class DataSearchDynamic : DataSearch
    {
        /// <summary>
        /// 要查询的表
        /// </summary>
        public string TableName { get; set; } = string.Empty;
    }
}
