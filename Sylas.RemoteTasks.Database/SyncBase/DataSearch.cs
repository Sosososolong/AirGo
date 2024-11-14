using System.Collections.Generic;

namespace Sylas.RemoteTasks.Database.SyncBase
{
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
        public DataSearch(int pageIndex, int pageSize, DataFilter? filter = null, List<OrderField>? rules = null)
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
        public List<OrderField>? Rules { get; set; } = null;
    }
}
