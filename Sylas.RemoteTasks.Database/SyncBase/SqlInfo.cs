using System.Collections.Generic;

namespace Sylas.RemoteTasks.Database.SyncBase
{
    /// <summary>
    /// 一个Sql执行所需要的信息
    /// </summary>
    public class SqlInfo
    {
        /// <summary>
        /// SQL语句
        /// </summary>
        public string Sql { get; set; }
        /// <summary>
        /// 执行Sql语句的参数
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; }
        /// <summary>
        /// 使用Sql语句以及执行它的参数初始化
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        public SqlInfo(string sql, Dictionary<string, object> parameters)
        {
            Sql = sql;
            Parameters = parameters;
        }
    }
}
