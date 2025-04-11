namespace Sylas.RemoteTasks.Database.Dtos
{
    /// <summary>
    /// 数据传输Dto
    /// </summary>
    public class TransferDataDto
    {
        /// <summary>
        /// 源数据库连接字符串
        /// </summary>
        public string SourceConnectionString { get; set; } = string.Empty;
        /// <summary>
        /// 目标数据库连接Id
        /// </summary>
        public string TargetConnectionString { get; set; } = string.Empty;
        /// <summary>
        /// 源表名
        /// </summary>
        public string SourceTable { get; set; } = string.Empty;
        /// <summary>
        /// 目标表名
        /// </summary>
        public string TargetTable { get; set; } = string.Empty;
        /// <summary>
        /// 是否不删除只插入数据(不会插入重复数据)
        /// </summary>
        public bool InsertOnly { get; set; } = false;
    }
}
