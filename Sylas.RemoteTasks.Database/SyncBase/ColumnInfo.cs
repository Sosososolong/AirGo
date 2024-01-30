namespace Sylas.RemoteTasks.Database.SyncBase
{
    /// <summary>
    /// 数据表字段信息
    /// </summary>
    public class ColumnInfo
    {
        /// <summary>
        /// 字段代码
        /// </summary>
        public string? ColumnCode { get; set; }
        /// <summary>
        /// 字段名
        /// </summary>
        public string? ColumnName { get; set; }
        /// <summary>
        /// 字段类型
        /// </summary>
        public string? ColumnType { get; set; }
        /// <summary>
        /// 字段类型名(对应CSharp中的)
        /// </summary>
        public string? ColumnCSharpType { get; set; }
        /// <summary>
        /// 字段长度
        /// </summary>
        public string? ColumnLength { get; set; }
        /// <summary>
        /// 是否主键
        /// </summary>
        public int IsPK { get; set; }
        /// <summary>
        /// 是否允许为空
        /// </summary>
        public int IsNullable { get; set; }
        /// <summary>
        /// 排序
        /// </summary>
        public int OrderNo { get; set; }
        /// <summary>
        /// 备注
        /// </summary>
        public string? Remark { get; set; }
        /// <summary>
        /// 默认值
        /// </summary>
        public string? DefaultValue { get; set; }

    }
}
