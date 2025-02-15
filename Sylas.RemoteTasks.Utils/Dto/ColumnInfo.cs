namespace Sylas.RemoteTasks.Utils.Dto
{
    /// <summary>
    /// 数据表字段信息
    /// </summary>
    public class ColumnInfo
    {
        /// <summary>
        /// 是否主键, 是为1, 否为0
        /// </summary>
        public int IsPrimarykey { get; set; }
        /// <summary>
        /// 字段名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 字段类型
        /// </summary>
        public string ColumnType { get; set; } = string.Empty;
        /// <summary>
        /// 字段长度
        /// </summary>
        public int Length { get; set; }
        /// <summary>
        /// 字段描述
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }
}
