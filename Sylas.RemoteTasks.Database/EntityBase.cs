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
    }
}
