using System;

namespace Sylas.RemoteTasks.Database.Attributes
{
    /// <summary>
    /// 自定义标签, 用于指定数据表的表名
    /// </summary>
    /// <remarks>
    /// 添加标签时指定表名
    /// </remarks>
    /// <param name="tableName"></param>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class TableAttribute(string tableName) : Attribute
    {
        /// <summary>
        /// 数据表表名
        /// </summary>
        public string TableName { get; set; } = tableName;
    }
}
