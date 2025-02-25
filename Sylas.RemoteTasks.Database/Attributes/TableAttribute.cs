using System;
using System.Reflection;

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
        /// <summary>
        /// 获取Entity的表名配置
        /// </summary>
        /// <param name="entityType"></param>
        /// <returns></returns>
        public static string GetTableName(Type entityType)
        {
            var tableAttribute = entityType.GetCustomAttribute<TableAttribute>(true);
            var tableName = tableAttribute is not null && !string.IsNullOrWhiteSpace(tableAttribute.TableName) ? tableAttribute.TableName : entityType.Name;
            return tableName;
        }
    }
}
