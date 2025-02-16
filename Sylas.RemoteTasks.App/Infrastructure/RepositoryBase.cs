using Dapper;
using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.Database;
using Sylas.RemoteTasks.Database.SyncBase;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.App.Infrastructure
{
    public class RepositoryBase<T>(IDatabaseProvider databaseProvider) where T : EntityBase<int>, new()
    {
        private readonly IDatabaseProvider _db = databaseProvider;

        #region 管理数据
        /// <summary>
        /// 分页查询多个数据库连接字符串信息
        /// </summary>
        /// <param name="search">分页查询参数</param>
        /// <returns></returns>
        public async Task<PagedData<T>> GetPageAsync(DataSearch? search)
        {
            search ??= new();
            var pages = await _db.QueryPagedDataAsync<T>(DbTableInfo<T>._tableName, search);
            return pages;
        }
        /// <summary>
        /// 根据Id查询数据
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<T?> GetByIdAsync(int id)
        {
            var pages = await _db.QueryPagedDataAsync<T>(DbTableInfo<T>._tableName, new(1, 1, new DataFilter { FilterItems = [new FilterItem("id", "=", id)] }, [new("id", true)]));
            var connectionString = pages.Data.FirstOrDefault();
            if (connectionString is null)
            {
                return null;
            }
            return connectionString;
        }
        /// <summary>
        /// 从字典中解析出Id字段, 然后根据Id查询对应的数据
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<T?> GetByIdAsync(Dictionary<string, string> biz)
        {
            string? idField = biz.Keys.FirstOrDefault(x => x.Equals("id", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(idField))
            {
                throw new Exception("主键字段不能为空");
            }
            string id = biz[idField];
            string? idConvertKey = DbTableInfo<T>._propertyConverterMappers.Keys.FirstOrDefault(x => x.Equals(idField, StringComparison.OrdinalIgnoreCase));
            object idValue = string.IsNullOrWhiteSpace(idConvertKey) ? id : DbTableInfo<T>._propertyConverterMappers[idConvertKey](id);

            var pages = await _db.QueryPagedDataAsync<T>(DbTableInfo<T>._tableName, new(1, 1, new DataFilter { FilterItems = [new FilterItem("id", "=", idValue)] }, [new("id", true)]));
            var connectionString = pages.Data.FirstOrDefault();
            if (connectionString is null)
            {
                return null;
            }
            return connectionString;
        }
        /// <summary>
        /// 添加一个新的数据库连接字符串信息
        /// </summary>
        /// <param name="dbConnectionString"></param>
        /// <returns></returns>
        public async Task<int> AddAsync(T t)
        {
            var start = DateTime.Now;
            string sql = DbTableInfo<T>._insertSql;
            var parameters = DbTableInfo<T>._getInsertSqlParameters(t);
            await Console.Out.WriteLineAsync($"仓储获取Insert语句信息耗时: {(DateTime.Now - start).TotalMilliseconds}/ms");
            if (_db.DbType == DatabaseType.Pg)
            {
                sql = $"{sql} RETURNING id";
            }
            return await _db.ExecuteScalarAsync(sql, parameters);
        }

        /// <summary>
        /// 更新数据库连接字符串信息
        /// </summary>
        /// <param name="dbConnectionString"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<int> UpdateAsync(T t)
        {
            var start = DateTime.Now;
            var sql = DbTableInfo<T>._updateSql;
            var parameters = DbTableInfo<T>._getUpdateSqlParameters(t);
            await Console.Out.WriteLineAsync($"仓储获取Update语句信息耗时: {(DateTime.Now - start).TotalMilliseconds}/ms");
            return await _db.ExecuteSqlAsync(sql, parameters);
        }

        /// <summary>
        /// 更新记录信息 - 局部更新
        /// </summary>
        /// <param name="dbConnectionString"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<int> UpdateAsync(Dictionary<string, string> updatingFieldsAndId)
        {
            var start = DateTime.Now;
            var sql = DbTableInfo<T>._updateSql;
            if (!updatingFieldsAndId.Keys.Any(x => x.Equals("id", StringComparison.OrdinalIgnoreCase)))
            {
                throw new Exception("更新操作缺少Id字段");
            }
            var excludedFields = DbTableInfo<T>._allFields.Except(updatingFieldsAndId.Keys, StringComparer.OrdinalIgnoreCase);

            // 从sql语句中去掉excludeFields
            foreach (var field in excludedFields)
            {
                if (!string.IsNullOrWhiteSpace(DbTableInfo<T>._updateTimeField) && field.Equals(DbTableInfo<T>._updateTimeField, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                sql = Regex.Replace(sql, $@"{field}=[@:]{field},{{0,1}}", string.Empty);
            }

            var converterKeys = DbTableInfo<T>._propertyConverterMappers.Keys;
            DynamicParameters parameters = new();
            List<string> parameterKeys = [];
            foreach (var updatingField in updatingFieldsAndId)
            {
                string? originalFieldName = DbTableInfo<T>._allFields.FirstOrDefault(x => x.Equals(updatingField.Key, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrWhiteSpace(originalFieldName))
                {
                    continue;
                }
                var converterKey = converterKeys.FirstOrDefault(x => x.Equals(updatingField.Key, StringComparison.OrdinalIgnoreCase));
                if (converterKey is not null)
                {
                    var converterMapper = DbTableInfo<T>._propertyConverterMappers[converterKey];
                    var converteredValue = converterMapper(updatingField.Value);
                    parameters.Add(originalFieldName, converteredValue);
                }
                else
                {
                    parameters.Add(originalFieldName, updatingField.Value);
                }
                parameterKeys.Add(originalFieldName);
            }
            if (!string.IsNullOrWhiteSpace(DbTableInfo<T>._updateTimeField) && !parameterKeys.Contains(DbTableInfo<T>._updateTimeField, StringComparer.OrdinalIgnoreCase))
            {
                parameters.Add(DbTableInfo<T>._updateTimeField, DateTime.Now);
                parameterKeys.Add(DbTableInfo<T>._updateTimeField);
            }

            await Console.Out.WriteLineAsync($"仓储获取局部更新Update语句信息耗时: {(DateTime.Now - start).TotalMilliseconds}/ms");
            sql = Regex.Replace(sql, @",\s+where", " where", RegexOptions.IgnoreCase);
            return await _db.ExecuteSqlAsync(sql, parameters);
        }

        /// <summary>
        /// 删除数据库连接字符串信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<int> DeleteAsync(int id)
        {
            string sql = $"delete from {DbTableInfo<T>._tableName} where id=@id";
            return await _db.ExecuteSqlAsync(sql, new Dictionary<string, object> { { "id", id } });
        }
        #endregion
    }

    /// <summary>
    /// 局部更新Dto
    /// </summary>
    public class PatchDto
    {
        /// <summary>
        /// 要更新的表名
        /// </summary>
        public string Target { get; set; } = string.Empty;
        /// <summary>
        /// 要更新的字段
        /// </summary>
        public Dictionary<string, object> Fields { get; set; } = [];
    }
    public class PostDto
    {
        /// <summary>
        /// 指定要插入数据的表
        /// </summary>
        public string Target { get; set; } = string.Empty;
        /// <summary>
        /// 要插入的数据
        /// </summary>
        public IEnumerable<Dictionary<string, object>> Records { get; set; } = [];
    }
    public class DeleteDto
    {
        /// <summary>
        /// 指定要插入数据的表
        /// </summary>
        public string Target { get; set; } = string.Empty;
        /// <summary>
        /// 要插入的数据
        /// </summary>
        public IEnumerable<string> Ids { get; set; } = [];
    }
}
