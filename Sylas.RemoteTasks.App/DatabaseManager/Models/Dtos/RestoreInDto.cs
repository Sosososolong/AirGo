namespace Sylas.RemoteTasks.App.DatabaseManager.Models.Dtos
{
    /// <summary>
    /// 还原数据库入参
    /// </summary>
    public class RestoreInDto
    {
        /// <summary>
        /// 备份记录Id
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 要还原的数据库连接字符串Id
        /// </summary>
        public int RestoreConnectionId { get; set; }
        /// <summary>
        /// 要同步的表, 多个表用逗号分隔
        /// </summary>
        public string Tables { get; set; } = string.Empty;
    }
}
