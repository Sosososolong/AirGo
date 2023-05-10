namespace Sylas.RemoteTasks.App.Database.SyncBase
{
    public class TableSqlsInfo
    {
        public string TableName { get; set; } = string.Empty;

        public BatchInsertSqlInfo BatchInsertSqlInfo { get; set; } = new BatchInsertSqlInfo();
    }

    public class BatchInsertSqlInfo
    {
        /// <summary>表不存在的时候生成创建表的语句</summary>
        public string CreateTableSql { get; set; } = string.Empty;
        /// <summary>批量插入的insert语句</summary>
        public string BatchInsertSql { get; set; } = string.Empty;
        /// <summary>Sql属性所需要的参数</summary>
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }

    public class TableInsertSqlsInfo
    {
        public string TableName { get; set; } = string.Empty;

        public BatchInsertSqlOnly BatchInsertSqlOnly { get; set; } = new BatchInsertSqlOnly();
    }

    public class BatchInsertSqlOnly
    {
        /// <summary>批量插入的insert语句</summary>
        public string BatchInsertSql { get; set; } = string.Empty;
        /// <summary>Sql属性所需要的参数</summary>
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }
}
