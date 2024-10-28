using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Sylas.RemoteTasks.Database.SyncBase
{
    /// <summary>
    /// 分页数据
    /// </summary>
    public class PagedData
    {
        /// <summary>
        /// 记录数
        /// </summary>
        public int Count { get; set; }
        /// <summary>
        /// 数据
        /// </summary>
        public IEnumerable<object> Data { get; set; } = [];
        /// <summary>
        /// 数据读取器
        /// </summary>
        public IDataReader? DataReader { get; set; }
    }

    /// <summary>
    /// 分页数据
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PagedData<T>
    {
        /// <summary>
        /// 记录数
        /// </summary>
        public int Count { get; set; }
        /// <summary>
        /// 总页数
        /// </summary>
        public int TotalPages { get; set; }
        /// <summary>
        /// 数据
        /// </summary>
        public IEnumerable<T> Data { get; set; } = [];
    }
}
