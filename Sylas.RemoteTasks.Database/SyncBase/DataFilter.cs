using System;
using System.Collections.Generic;

namespace Sylas.RemoteTasks.Database.SyncBase
{
    /// <summary>
    /// 描述一个过滤条件
    /// </summary>
    public class FilterItem
    {
        /// <summary>
        /// 字段名称
        /// </summary>
        public string FieldName { get; set; } = string.Empty;
        /// <summary>
        /// 比较类型 大于, 小于, =, 大于等于, 小于等于, !=, include
        /// </summary>
        public string CompareType { get; set; } = string.Empty;
        /// <summary>
        /// 比较的值
        /// </summary>
        public string Value { get; set; } = string.Empty;
    }
    /// <summary>
    /// 关键字
    /// </summary>
    public class Keywords
    {
        /// <summary>
        /// 关键字搜索字段
        /// </summary>
        public string[] Fields { get; set; } = [];
        /// <summary>
        /// 关键字的值
        /// </summary>
        public string Value { get; set; } = string.Empty;
    }
    /// <summary>
    /// 数据过滤器
    /// </summary>
    public class DataFilter
    {
        /// <summary>
        /// 过滤参数集合
        /// </summary>
        public List<FilterItem> FilterItems { get; set; } = [];
        /// <summary>
        /// 关键字
        /// </summary>
        public Keywords Keywords { get; set; } = new Keywords();
    }
}
