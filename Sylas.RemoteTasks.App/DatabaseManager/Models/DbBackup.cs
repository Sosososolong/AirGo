using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.Database.Attributes;

namespace Sylas.RemoteTasks.App.DatabaseManager.Models
{
    /// <summary>
    /// 数据库备份
    /// </summary>
    [Table("DbBackups")]
    public class DbBackup : EntityBase<int>
    {
        public DbBackup() { }
        public DbBackup(int connInfoId, string domain, string name, string backupDir, string remark, long size)
        {
            DbConnectionInfoId = connInfoId;
            Name = name;
            Domain = domain;
            BackupDir = backupDir;
            Remark = remark;
            Size = size;
        }
        /// <summary>
        /// 备份的数据库连接信息Id
        /// </summary>
        public int DbConnectionInfoId { get; set; }
        /// <summary>
        /// 备份名称
        /// </summary>
        public string Name { get; set; } = "";
        /// <summary>
        /// 备份的主机的域名
        /// </summary>
        public string Domain { get; set; } = "";
        /// <summary>
        /// 备份文件路径
        /// </summary>
        public string BackupDir { get; set; } = "";
        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; } = "";
        /// <summary>
        /// 备份目录大小
        /// </summary>
        public long? Size { get; set; } = 0;
    }
}
