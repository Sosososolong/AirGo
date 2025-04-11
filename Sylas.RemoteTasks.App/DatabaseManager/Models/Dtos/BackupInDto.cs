namespace Sylas.RemoteTasks.App.DatabaseManager.Models.Dtos
{
    /// <summary>
    /// 数据库备份信息入参
    /// </summary>
    public class BackupInDto
    {
        /// <summary>
        /// 要备份的数据库连接信息Id
        /// </summary>
        public int DbConnectionInfoId { get; set; }
        /// <summary>
        /// 备份名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 备份备注
        /// </summary>
        public string Remark { get; set; } = string.Empty;
        /// <summary>
        /// 备份的表
        /// </summary>
        public string Tables { get; set; } = string.Empty;
    }
}
