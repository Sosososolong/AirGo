using Sylas.RemoteTasks.App.Database.SyncBase;
using Sylas.RemoteTasks.App.Database;

namespace Sylas.RemoteTasks.App.Repositories
{
    public class RepositoryBase<T> where T : EntityBase<int>, new()
    {
        private readonly IDatabaseProvider _db;

        public RepositoryBase(IDatabaseProvider databaseProvider)
        {
            _db = databaseProvider;
        }

        #region 管理数据
        /// <summary>
        /// 分页查询多个数据库连接字符串信息
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="orderField"></param>
        /// <param name="isAsc"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public async Task<PagedData<T>> GetPageAsync(int pageIndex, int pageSize, string orderField, bool isAsc, DataFilter filter)
        {
            var pages = await _db.QueryPagedDataAsync<T>(nameof(T), pageIndex, pageSize, orderField, isAsc, filter);
            return pages;
        }
        /// <summary>
        /// 根据Id查询数据库连接字符串信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<T?> GetByIdAsync(int id)
        {
            var pages = await _db.QueryPagedDataAsync<T>(nameof(T), 1, 1, "id", true, new DataFilter { FilterItems = new List<FilterItem> { new FilterItem { CompareType = "=", FieldName = "id", Value = id.ToString() } } });
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
            var recordCount = await _db.ExecuteSqlAsync($"select count(*) from {nameof(T)} where id=@id", new Dictionary<string, object> { { "id", t.Id } });
            if (recordCount == 0)
            {
                throw new Exception($"{nameof(T)}不存在");
            }
            var start = DateTime.Now;
            var sql = DbTableInfo<T>._updateSql;
            var parameters = DbTableInfo<T>._getUpdateSqlParameters(t);
            await Console.Out.WriteLineAsync($"仓储获取Insert语句信息耗时: {(DateTime.Now - start).TotalMilliseconds}/ms");
            return await _db.ExecuteSqlAsync(sql, parameters);
        }

        /// <summary>
        /// 删除数据库连接字符串信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<int> DeleteAsync(int id)
        {
            string sql = $"delete from {nameof(T)} where id=@id";
            return await _db.ExecuteSqlAsync(sql, new Dictionary<string, object> { { "id", id } });
        }
        #endregion
    }
}
