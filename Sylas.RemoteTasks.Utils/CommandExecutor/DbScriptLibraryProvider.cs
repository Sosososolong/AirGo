using Sylas.RemoteTasks.Database.Dtos;
using Sylas.RemoteTasks.Database.SyncBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Utils.CommandExecutor
{
    /// <summary>
    /// 默认实现: 从数据库表 <see cref="TableName"/> 读取公共脚本
    /// 片段在数据库中维护, 适配分布式部署(各节点读同一张表, 改完下次执行即生效)
    /// </summary>
    /// <param name="db">DatabaseInfo(DI注入)</param>
    public class DbScriptLibraryProvider(DatabaseInfo db) : IScriptLibraryProvider
    {
        /// <summary>
        /// 公共脚本库表名
        /// </summary>
        public const string TableName = "ScriptLibraries";

        private readonly DatabaseInfo _db = db;

        /// <summary>
        /// 从 ScriptLibraries 表读取指定语言的全部公共脚本
        /// </summary>
        /// <param name="lang">语言分区, 如: python/powershell/bash</param>
        /// <returns>相对路径 → 脚本内容</returns>
        public async Task<Dictionary<string, string>> GetScriptsAsync(string lang)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(lang))
            {
                return result;
            }

            var search = new DataSearch(1, 999,
                new DataFilter { FilterItems = [new("Lang", "=", lang)] }, null);
            var rows = (await _db.QueryPagedDataAsync<IDictionary<string, object>>(TableName, search)).Data;
            foreach (var row in rows)
            {
                string filePath = GetFieldValue(row, "FilePath");
                if (string.IsNullOrWhiteSpace(filePath)) continue;
                result[filePath] = GetFieldValue(row, "Content");
            }
            return result;
        }

        /// <summary>
        /// 按字段名取值(键名大小写不敏感, 不同数据库返回的列名大小写可能不同)
        /// </summary>
        static string GetFieldValue(IDictionary<string, object> row, string fieldName)
        {
            var key = row.Keys.FirstOrDefault(x => x.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
            return key is null ? string.Empty : $"{row[key]}";
        }
    }
}
