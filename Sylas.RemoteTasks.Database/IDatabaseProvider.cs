using Sylas.RemoteTasks.Database.SyncBase;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Database
{
    /// <summary>
    /// 数据库基本操作
    /// </summary>
    public interface IDatabaseProvider
    {
        /// <summary>
        /// 分页查询指定数据表, 可使用db参数切换到指定数据库
        /// </summary>
        /// <param name="table">查询的表明</param>
        /// <param name="search">分页查询参数</param>
        /// <param name="db">指定要切换查询的数据库, 不指定使用Default配置的数据库</param>
        /// <returns></returns>
        Task<PagedData<T>> QueryPagedDataAsync<T>(string table, DataSearch search, string db = "");
        /// <summary>
        /// 分页查询指定数据表, 可使用数据库连接字符串connectionString参数指定连接的数据库
        /// </summary>
        /// <param name="table">查询的表明</param>
        /// <param name="search">分页查询参数</param>
        /// <param name="connectionString">指定要切换查询的数据库, 不指定使用Default配置的数据库连接</param>
        /// <returns></returns>
        Task<PagedData<T>> QueryPagedDataWithConnectionStringAsync<T>(string table, DataSearch? search, string connectionString) where T : new();

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
