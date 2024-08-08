namespace Sylas.RemoteTasks.Database.CodeGenerator
{
    /// <summary>
    /// 需要展示的字段
    /// </summary>
    public class ShowingColumn
    {
        /// <summary>
        /// 字段表达式
        /// </summary>
        public string ColumnCode { get; set; } = string.Empty;
        /// <summary>
        /// 字段别名
        /// </summary>
        public string ColumnAlias { get; set; } = string.Empty;
        /// <summary>
        /// 字段显示名称
        /// </summary>
        public string ColumnDisplayName { get; set; } = string.Empty;
        /// <summary>
        /// 字段CSharp类型
        /// </summary>
        public string ColumnCSharpType { get; set; } = string.Empty;
    }
}
