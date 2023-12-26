using Dapper;
using Sylas.RemoteTasks.App.Snippets;
using Sylas.RemoteTasks.App.Utils;
using System.Reflection;

namespace Sylas.RemoteTasks.App.Database.SyncBase
{
    public class DbTableInfo<T>
    {
        public readonly static string _tableName;
        public readonly static string _insertSql;
        public readonly static string _updateSql;
        public readonly static Func<T, DynamicParameters> _getInsertSqlParameters;
        public readonly static Func<T, DynamicParameters> _getUpdateSqlParameters;
        static DbTableInfo()
        {
            #region 反射获取实体类的基本信息
            var entityType = typeof(T);

            _tableName = entityType.Name;

            var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            // entityType.GetCustomAttribute 自定义特性获取主键信息或者其他信息
            var allPropertyNames = properties.Select(x => x.Name).ToList();
            #endregion

            #region 获取insert语句和update语句
            var primaryKeyProp = properties.FirstOrDefault(x => x.Name.ToLower() == "id") ?? throw new Exception($"{entityType.Name}没有找到Id字段");
            var otherPropertyNames = allPropertyNames.Where(x => !x.Equals("id", StringComparison.CurrentCultureIgnoreCase));

            // 自增主键的数据类型一般是int或者long
            var autoIncreasingPkTypeNames = new string[] { typeof(int).Name, typeof(long).Name };
            var insertSqlFields = autoIncreasingPkTypeNames.Contains(primaryKeyProp.PropertyType.Name) ? otherPropertyNames : allPropertyNames;
            var insertSqlFieldsStatement = string.Join(',', insertSqlFields);
            var insertSqlValuesStatement = string.Join(",", insertSqlFields.Select(x => $"@{x}"));
            _insertSql = $"insert into {entityType.Name} ({insertSqlFieldsStatement}) values ({insertSqlValuesStatement})";

            // Int32 String DateTime
            var updateSqlFields = otherPropertyNames;
            var updateFieldItems = updateSqlFields.Select(x => $"{x}=@{x}");
            var updateFieldsStatement = string.Join(",", updateFieldItems);
            _updateSql = $"update {entityType.Name} set {updateFieldsStatement};";
            #endregion

            #region 构建sql执行参数的lambda表达式目录树并缓存lambda表达式
            _getInsertSqlParameters = ExpressionBuilder.BuildGetterSqlExecutionParameters<T>(insertSqlFields);
            _getUpdateSqlParameters = ExpressionBuilder.BuildGetterSqlExecutionParameters<T>(allPropertyNames);
            #endregion
        }
    }
}
