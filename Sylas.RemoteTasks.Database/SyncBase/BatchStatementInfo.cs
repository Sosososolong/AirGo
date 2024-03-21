using System.Collections.Generic;

namespace Sylas.RemoteTasks.Database.SyncBase
{
    /// <summary>
    /// 数据表相关的操作对应的Sql语句信息
    /// </summary>
    public class TableSqlInfos
    {
        /// <summary>
        /// 表名
        /// </summary>
        public string TableName { get; set; } = string.Empty;
        /// <summary>
        /// 批量插入的SQL语句信息
        /// </summary>
        public BatchInsertSqlInfo BatchInsertSqlInfo { get; set; } = new();
        /// <summary>
        /// 删除已存在的数据的Sql语句信息
        /// </summary>
        public SqlInfo DeleteExistSqlInfo { get; set; } = new();
    }
    /// <summary>
    /// 批量插入Sql语句信息
    /// </summary>
    public class BatchInsertSqlInfo : SqlInfo
    {
        /// <summary>表不存在的时候生成创建表的语句</summary>
        public string CreateTableSql { get; set; } = string.Empty;
    }
}
