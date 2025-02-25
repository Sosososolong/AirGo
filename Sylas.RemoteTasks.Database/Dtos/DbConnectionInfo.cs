using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.Database.Attributes;

namespace Sylas.RemoteTasks.Database.Dtos
{
    /// <summary>
    /// 数据库连接信息
    /// </summary>
    [Table("DbConnectionInfos")]
    public class DbConnectionInfo : EntityBase<int>
    {
        /// <summary>
        /// 数据库连接名称
        /// </summary>
        public string Name { get; set; } = "";
        /// <summary>
        /// 数据库连接别名
        /// </summary>
        public string Alias { get; set; } = "";
        /// <summary>
        /// 数据库连接字符串
        /// </summary>
        public string ConnectionString { get; set; } = "";
        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; } = "";
        /// <summary>
        /// 排序
        /// </summary>
        public int OrderNo { get; set; }
    }
}
