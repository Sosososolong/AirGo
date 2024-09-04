using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.Database;
using Sylas.RemoteTasks.Database.SyncBase;

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
        /// 根据Id查询数据库连接字符串信息
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
            return await _db.ExecuteSqlAsync(sql, parameters);
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
}
