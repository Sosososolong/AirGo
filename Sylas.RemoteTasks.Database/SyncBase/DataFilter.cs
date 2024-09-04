using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Sylas.RemoteTasks.Database.SyncBase
{
    /// <summary>
    /// 描述一个过滤条件
    /// </summary>
    public class FilterItem
    {
        /// <summary>
        /// 默认值初始化实例
        /// </summary>
        public FilterItem()
        {
            
        }
        /// <summary>
        /// 指定属性值初始化实例
        /// </summary>
        /// <param name="field"></param>
        /// <param name="compareType"></param>
        /// <param name="value"></param>
        public FilterItem(string field, string compareType, object value)
        {
            FieldName = field;
            CompareType = compareType;
            Value = value;
        }
        /// <summary>
        /// 字段名称
        /// </summary>
        public string FieldName { get; set; } = string.Empty;
        /// <summary>
        /// 比较类型 大于, 小于, =, 大于等于, 小于等于, !=, include
        /// </summary>
        public string CompareType { get; set; } = string.Empty;

        object? _value = null;
        /// <summary>
        /// 比较的值
        /// </summary>
        public object? Value
        {
            get
            {
                return _value;
            }
            set
            {
                if (value is JsonElement jeVal)
                {
                    if (jeVal.ValueKind == JsonValueKind.True)
                    {
                        _value = true;
                    }
                    else if (jeVal.ValueKind == JsonValueKind.False)
                    {
                        _value = false;
                    }
                    else if (jeVal.ValueKind == JsonValueKind.Number)
                    {
                        _value = FieldName.Equals("id", StringComparison.OrdinalIgnoreCase) ? jeVal.GetInt64() : jeVal.GetInt32();
                    }
                    else if (jeVal.ValueKind == JsonValueKind.Null || jeVal.ValueKind == JsonValueKind.Undefined)
                    {
                        _value = null;
                    }
                    else
                    {
                        _value = jeVal.GetString() ?? string.Empty;
                    }
                }
                else
                {
                    _value = value;
                }
            }
        }
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
    /// <summary>
    /// 数据查询参数
    /// </summary>
    public class DataSearch
    {
        /// <summary>
        /// 使用默认值初始化分页查询参数
        /// </summary>
        public DataSearch()
        {
            
        }
        /// <summary>
        /// 使用指定值初始化分页查询参数
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="filter"></param>
        /// <param name="rules"></param>
        public DataSearch(int pageIndex, int pageSize, DataFilter? filter = null, List<OrderRule>? rules = null)
        {
            PageIndex = pageIndex == 0 ? 1 : pageIndex;
            PageSize = pageSize == 0 ? 20 : pageSize;
            Filter = filter is null ? new() : filter;
            Rules = rules is null ? [new()] : rules;
        }
        /// <summary>
        /// 分页当前页码
        /// </summary>
        public int PageIndex { get; set; } = 1;
        /// <summary>
        /// 分页每页多少记录
        /// </summary>
        public int PageSize { get; set; } = 20;
        /// <summary>
        /// 过滤条件
        /// </summary>
        public DataFilter Filter { get; set; } = new();
        /// <summary>
        /// 排序规则
        /// </summary>
        public List<OrderRule>? Rules { get; set; } = null;
    }
}
