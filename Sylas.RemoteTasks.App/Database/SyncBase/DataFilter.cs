namespace Sylas.RemoteTasks.App.Database.SyncBase
{
    public class FilterItem
    {
        /// <summary>
        /// 字段名称
        /// </summary>
        public string FieldName { get; set; }
        /// <summary>
        /// 比较类型 ">", "<", "=", ">=", "<=", "!=", "include"
        /// </summary>
        public string CompareType { get; set; }
        /// <summary>
        /// 比较的值
        /// </summary>
        public string Value { get; set; }
    }
    public class Keywords
    {
        /// <summary>
        /// 关键字搜索字段
        /// </summary>
        public string[] Fields { get; set; }
        /// <summary>
        /// 关键字的值
        /// </summary>
        public string Value { get; set; }
    }
    public class DataFilter
    {
        public List<FilterItem> FilterItems { get; set; }
        public Keywords Keywords { get; set; }
    }
}
