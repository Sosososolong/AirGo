using Dapper;
using Sylas.RemoteTasks.Common;
using Sylas.RemoteTasks.Database.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Sylas.RemoteTasks.Database.SyncBase
{
    /// <summary>
    /// 一个描述数据表的对象, T为实体类
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DbTableInfo<T>
    {
        /// <summary>
        /// 数据库类型
        /// </summary>
        public readonly static DatabaseType DbType;
        /// <summary>
        /// 数据表名
        /// </summary>
        public readonly static string _tableName;
        /// <summary>
        /// Insert数据的SQL语句, 参数化形式
        /// </summary>
        public readonly static string _insertSql;
        /// <summary>
        /// 所有可能更新的字段
        /// </summary>
        public readonly static string[] _allFields;
        /// <summary>
        /// Update数据的SQL语句, 参数化形式
        /// </summary>
        public readonly static string _updateSql;
        /// <summary>
        /// 数据表的更新时间字段, 一般是UpdateTime
        /// </summary>
        public readonly static string _updateTimeField;
        /// <summary>
        /// Insert语句参数的获取函数
        /// </summary>
        public readonly static Func<T, DynamicParameters> _getInsertSqlParameters;
        /// <summary>
        /// Update语句参数的获取函数
        /// </summary>
        public readonly static Func<T, DynamicParameters> _getUpdateSqlParameters;
        /// <summary>
        /// 非字符串属性和转换器(将给定字符串转换为属性的类型, 如属性Id类型为int, 转换器将给定的"1"转换为1)
        /// </summary>
        public readonly static Dictionary<string, Func<string, object>> _propertyConverterMappers;
        static DbTableInfo()
        {
            #region 反射获取实体类的基本信息
            var entityType = typeof(T);

            //var tableAttribute = entityType.GetCustomAttribute<TableAttribute>(true);
            //_tableName = tableAttribute is not null && !string.IsNullOrWhiteSpace(tableAttribute.TableName) ? tableAttribute.TableName : entityType.Name;
            _tableName = TableAttribute.GetTableName(entityType);

            var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            // entityType.GetCustomAttribute 自定义特性获取主键信息或者其他信息
            var allPropertyNames = properties.Select(x => x.Name).ToList();
            _allFields = [.. allPropertyNames];

            _updateTimeField = allPropertyNames.FirstOrDefault(x => x.Equals("UpdateTime", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;

            // 缓存不是字符串类型的属性信息和将字符串转换为属性类型的lambda表达式对象缓存到字典中(前端传递过来要进行类型转换)
            _propertyConverterMappers = [];
            foreach (var p in properties)
            {
                if (p.PropertyType.Name == typeof(string).Name)
                {
                    continue;
                }
                var converter = ExpressionBuilder.CreateStringConverter(p.PropertyType);
                _propertyConverterMappers.Add(p.Name, converter);
            }
            #endregion

            #region 获取insert语句和update语句
            // 这里参数标识符暂时就使用'@'符号(这里不确定数据库类型), 真正执行的时候会根据数据库类型进行替换
            // TODO: 根据特性标签获取一个或者多个主键字段
            var primaryKeyProp = properties.FirstOrDefault(x => x.Name.ToLower() == "id") ?? throw new Exception($"{entityType.Name}没有找到Id字段");
            var otherPropertyNames = allPropertyNames.Where(x => !x.Equals("id", StringComparison.CurrentCultureIgnoreCase));

            // 自增主键的数据类型一般是int或者long
            var autoIncreasingPkTypeNames = new string[] { typeof(int).Name, typeof(long).Name };
            var insertSqlFields = autoIncreasingPkTypeNames.Contains(primaryKeyProp.PropertyType.Name) ? otherPropertyNames : allPropertyNames;
            var insertSqlFieldsStatement = string.Join(',', insertSqlFields);
            var insertSqlValuesStatement = string.Join(",", insertSqlFields.Select(x => $"@{x}"));
            _insertSql = $"insert into {_tableName} ({insertSqlFieldsStatement}) values ({insertSqlValuesStatement})";

            // Int32 String DateTime
            var updateSqlFields = otherPropertyNames;
            var updateFieldItems = updateSqlFields.Where(x => !x.Equals("CreateTime", StringComparison.OrdinalIgnoreCase)).Select(x => $"{x}=@{x}");
            var updateFieldsStatement = string.Join(",", updateFieldItems);
            _updateSql = $"update {_tableName} set {updateFieldsStatement} where {primaryKeyProp.Name}=@{primaryKeyProp.Name};";
            #endregion

            #region 构建sql执行参数的lambda表达式目录树并缓存lambda表达式
            _getInsertSqlParameters = ExpressionBuilder.BuildGetterSqlExecutionParameters<T>(insertSqlFields);
            _getUpdateSqlParameters = ExpressionBuilder.BuildGetterSqlExecutionParameters<T>(allPropertyNames);
            #endregion
        }
    }
}
