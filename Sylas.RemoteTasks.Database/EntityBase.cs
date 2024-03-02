using System;

namespace Sylas.RemoteTasks.App.Database
{
    /// <summary>
    /// 数据实体基类
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class EntityBase<T>
    {
        /// <summary>
        /// 默认主键字段都是Id
        /// </summary>
        public T? Id { get; set; }
        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }
        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdateTime { get; set; }
    }
}
