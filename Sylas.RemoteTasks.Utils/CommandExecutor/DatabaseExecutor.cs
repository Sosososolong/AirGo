using Dapper;
using Sylas.RemoteTasks.Common;
using Sylas.RemoteTasks.Database.Attributes;
using Sylas.RemoteTasks.Database.Dtos;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Utils.CommandExecutor
{
    /// <summary>
    /// 数据库执行器
    /// </summary>
    [Executor]
    public class DatabaseExecutor(DatabaseInfo db) : ICommandExecutor
    {
        /// <summary>
        /// 执行SQL语句, 返回第一条数据的第一个字段 - 作为命令执行器
        /// </summary>
        /// <param name="cmdTxt"></param>
        /// <returns></returns>
        public async IAsyncEnumerable<CommandResult> ExecuteAsync(string cmdTxt)
        {
            string targetDbName = cmdTxt[..cmdTxt.IndexOf(':')];
            if (string.IsNullOrWhiteSpace(targetDbName))
            {
                throw new Exception("命令内容比如以数据库名为开头(DB_NAME:EXE_SQL)");
            }

            string sql = cmdTxt.Substring(targetDbName.Length + 1).Trim();
            string table = TableAttribute.GetTableName(typeof(DbConnectionInfo));
            var filter = new DataFilter
            {
                FilterItems =
                [
                    new("alias", "like", targetDbName)
                ]
            };
            var targetConnectionInfo = (await db.QueryPagedDataAsync<DbConnectionInfo>(table, new DataSearch
            {
                PageIndex = 1,
                PageSize = 1,
                Filter = filter,
            })).Data.FirstOrDefault();

            if (targetConnectionInfo is null)
            {
                yield return new CommandResult(false, $"没有找到数据库{targetDbName}");
                yield break;
            }

            string targetConnnectionString = targetConnectionInfo.ConnectionString;
            if (!DatabaseConstants.ConnectionStringKeywords.Any(targetConnnectionString.Contains))
            {
                targetConnnectionString = await SecurityHelper.AesDecryptAsync(targetConnnectionString);
            }
            using var targetConn = DatabaseInfo.GetDbConnection(targetConnnectionString);
            CommandResult cmdRes;
            try
            {
                var result = await targetConn.ExecuteScalarAsync<object>(sql);
                cmdRes = new(true, $"{result}");
            }
            catch (Exception ex)
            {
                cmdRes = new CommandResult(false, ex.Message);
            }
            yield return cmdRes;
        }
    }
}
