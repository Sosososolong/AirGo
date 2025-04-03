using Dapper;
using Sylas.RemoteTasks.Common;
using Sylas.RemoteTasks.Database.Attributes;
using Sylas.RemoteTasks.Database.Dtos;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
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
        public readonly static Func<T, IEnumerable<ColumnInfo>, DynamicParameters> _getInsertSqlParameters;
        /// <summary>
        /// Update语句参数的获取函数
        /// </summary>
        public readonly static Func<T, IEnumerable<ColumnInfo>, DynamicParameters> _getUpdateSqlParameters;
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
            _getInsertSqlParameters = BuildGetterSqlExecutionParameters<T>(insertSqlFields);
            _getUpdateSqlParameters = BuildGetterSqlExecutionParameters<T>(allPropertyNames);
            #endregion
        }

        /// <summary>
        /// 构建一个 将指定对象转换为DynamicParameters 的lambda表达式
        /// </summary>
        /// <param name="parameterFields"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static Func<TEntity, IEnumerable<ColumnInfo>, DynamicParameters> BuildGetterSqlExecutionParameters<TEntity>(IEnumerable<string> parameterFields)
        {
            Type entityType = typeof(TEntity);

            if (parameterFields is null || !parameterFields.Any())
            {
                parameterFields = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(x => x.Name);
            }

            var entity = Expression.Parameter(entityType, "sp");
            // 创建 ParameterExpression 表示 IEnumerable<ColumnInfo> 类型的参数
            var cols = Expression.Parameter(typeof(IEnumerable<ColumnInfo>), "cols");

            var type = typeof(DynamicParameters);
            var result = Expression.Variable(type, "result");
            var init = Expression.Assign(result, Expression.New(type));
            var addMethod = type.GetMethod("Add", [typeof(string), typeof(object), typeof(DbType?), typeof(ParameterDirection?), typeof(int?)]) ?? throw new Exception($"{type.Name}没有找到Add方法");

            // 创建ColumnInfo.Name属性和ColumnType属性的访问表达式
            var columnInfoType = typeof(ColumnInfo);
            var codeProperty = columnInfoType.GetProperty(nameof(ColumnInfo.ColumnCode));
            var columnTypeProperty = columnInfoType.GetProperty(nameof(ColumnInfo.ColumnType));

            List<Expression> expressions = [init];
            foreach (var fieldName in parameterFields)
            {
                var property = Expression.Property(entity, fieldName);
                var propertyInfo = entityType.GetProperty(fieldName);
                Expression objValExp;
                if (propertyInfo?.PropertyType == typeof(bool) ||
                    propertyInfo?.PropertyType == typeof(bool?))
                {
                    #region 创建 Lambda 表达式 items => items.FirstOrDefault(c => c.ColumnCode.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                    ParameterExpression itemParam = Expression.Parameter(typeof(ColumnInfo), "c");
                    Expression condition = Expression.Call(
                        Expression.Property(itemParam, nameof(ColumnInfo.ColumnCode)),
                        "Equals",
                        null,
                        Expression.Constant(fieldName, typeof(string)),
                        Expression.Constant(StringComparison.OrdinalIgnoreCase, typeof(StringComparison))
                    );
                    // c => c.ColumnCode.Equals(fieldName, StringComparison.OrdinalIgnoreCase)
                    var checkColIsField = Expression.Lambda<Func<ColumnInfo, bool>>(condition, itemParam);
                    //getFieldColumn: Enumerable.FirstOrDefault(cols, (ColumnInfo c) => c.ColumnCode.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                    var getFieldColumn = Expression.Call(
                        typeof(Enumerable),
                        "FirstOrDefault",
                        [typeof(ColumnInfo)],
                        cols,
                        checkColIsField
                    );
                    #endregion

                    #region bool转int: true转换为1, false转换为0
                    // 1. 先获取bool值: bool?类型则获取Value属性再转换为object, 否则直接转换为object
                    Expression boolValue = propertyInfo.PropertyType == typeof(bool?)
                        ? Expression.Property(property, "Value")
                        : property;

                    // 2. 然后转换为int类型: 1或0
                    var boolConvertToInt = Expression.Condition(
                        boolValue,
                        Expression.Convert(Expression.Constant(1, typeof(int)), typeof(object)),
                        Expression.Convert(Expression.Constant(0, typeof(int)), typeof(object))
                    );
                    #endregion

                    #region 不是bit类型才需要"bool转int", 否则直接转换为object
                    // 1.获取ColumnType并检查是否是bit类型
                    var columnType = Expression.Property(getFieldColumn, columnTypeProperty);
                    var isNotBitType = Expression.NotEqual(
                        columnType,
                        Expression.Constant("bit", typeof(string))
                    );
                    // 2. 不是bit类型:"bool转int", 否则直接转换为object
                    objValExp = Expression.Condition(
                        isNotBitType,
                        boolConvertToInt,
                        Expression.Convert(property, typeof(object))
                    );
                    #endregion
                }
                else
                {
                    objValExp = Expression.Convert(property, typeof(object));
                }

                var call = Expression.Call(result,
                    addMethod,
                    Expression.Constant(fieldName),
                    objValExp,
                    Expression.Default(typeof(DbType?)), Expression.Default(typeof(ParameterDirection?)), Expression.Default(typeof(int?))
                );
                expressions.Add(call);
            }

            var returnLabel = Expression.Label(type);
            var returnExpression = Expression.Return(returnLabel, result, type);
            var returnLableTarget = Expression.Label(returnLabel, result);

            expressions.Add(returnExpression);
            expressions.Add(returnLableTarget);
            var block = Expression.Block([result], expressions);
            var lambda = Expression.Lambda<Func<TEntity, IEnumerable<ColumnInfo>, DynamicParameters>>(block, entity, cols).Compile();
            return lambda;
        }
    }
}
