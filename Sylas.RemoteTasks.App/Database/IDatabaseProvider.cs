using Sylas.RemoteTasks.App.Database.SyncBase;

namespace Sylas.RemoteTasks.App.Database
{
    public interface IDatabaseProvider
    {
        /// <summary>
        /// 分页查询指定数据表, 可使用db参数切换到指定数据库
        /// </summary>
        /// <param name="table">查询的表明</param>
        /// <param name="pageIndex">第几页</param>
        /// <param name="pageSize">每页多少条数</param>
        /// <param name="orderField">排序字段</param>
        /// <param name="isAsc">是否升序</param>
        /// <param name="filters">查询条件</param>
        /// <param name="db">指定要切换查询的数据库, 不指定使用Default配置的数据库</param>
        /// <returns></returns>
        Task<PagedData<T>> QueryPagedDataAsync<T>(string table, int pageIndex, int pageSize, string? orderField, bool isAsc, DataFilter filters, string db = "") where T : new();
        /// <summary>
        /// 分页查询指定数据表, 可使用数据库连接字符串connectionString参数指定连接的数据库
        /// </summary>
        /// <param name="table">查询的表明</param>
        /// <param name="pageIndex">第几页</param>
        /// <param name="pageSize">每页多少条数</param>
        /// <param name="orderField">排序字段</param>
        /// <param name="isAsc">是否升序</param>
        /// <param name="filters">查询条件</param>
        /// <param name="connectionString">指定要切换查询的数据库, 不指定使用Default配置的数据库连接</param>
        /// <returns></returns>
        Task<PagedData<T>> QueryPagedDataWithConnectionStringAsync<T>(string table, int pageIndex, int pageSize, string? orderField, bool isAsc, DataFilter filters, string connectionString) where T : new();

        /// <summary>
        /// 执行增删改的SQL语句返回受影响的行数 - 可使用db参数指定切换到当前连接的用户有权限的其他数据库
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <param name="db"></param>
        /// <returns></returns>
        Task<int> ExecuteSqlAsync(string sql, object parameters, string db = "");
        /// <summary>
        /// 执行多条增删改的SQL语句返回受影响的行数 - 可使用db参数指定切换到当前连接的用户有权限的其他数据库
        /// </summary>
        /// <param name="sqls"></param>
        /// <param name="parameters"></param>
        /// <param name="db"></param>
        /// <returns></returns>
        Task<int> ExecuteSqlsAsync(IEnumerable<string> sqls, Dictionary<string, object> parameters, string db = "");
        /// <summary>
        /// 执行SQL语句并返回唯一一个值 - 可使用db参数指定切换到当前连接的用户有权限的其他数据库
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <param name="db"></param>
        /// <returns></returns>
        Task<int> ExecuteScalarAsync(string sql, Dictionary<string, object> parameters, string db = "");
        /// <summary>
        /// 执行增删改的SQL语句 - 可使用数据库连接字符串指定数据库
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        Task<int> ExecuteScalarWithConnectionStringAsync(string sql, Dictionary<string, object> parameters, string connectionString);
    }
}
