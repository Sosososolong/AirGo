namespace Sylas.RemoteTasks.Utils.CommandExecutor.Http.Models
{
    /// <summary>
    /// 变量提取描述
    /// </summary>
    public class ExtractorSpec
    {
        /// <summary>
        /// 提取出来的值存到变量中, 这个变量名称
        /// </summary>
        public string VarName { get; set; } = string.Empty;
        /// <summary>
        /// 数据路径, 如数据在响应对象的data中, 那么就是data
        /// </summary>
        public string DataPath { get; set; } = string.Empty;
        /// <summary>
        /// 过滤data中的数据的条件, 如通过name=xxxx(ExtractorFilter { FieldName = "name", MatchValue = "xxxx" })过滤出name为xxxx的那条数据, 然后取这条数据的某个字段(下面的Field属性指定)值
        /// </summary>
        public ExtractorFilter? Filter { get; set; }
        /// <summary>
        /// 提取字段名称, 如:id, name 即data中具体某条数据的id或name值
        /// </summary>
        public string Field { get; set; } = string.Empty;
    }
    /// <summary>
    /// 提取器过滤条件
    /// </summary>
    public class ExtractorFilter
    {
        /// <summary>
        /// 过滤字段名称, 如id, name等
        /// </summary>
        public string FieldName { get; set; } = string.Empty;
        /// <summary>
        /// 过滤字段的值, 如xxxx, 表示取FieldName字段值为xxxx的那条数据
        /// </summary>
        public string MatchValue { get; set; } = string.Empty;
    }
}
