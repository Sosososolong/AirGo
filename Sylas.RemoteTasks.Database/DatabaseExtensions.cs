using Sylas.RemoteTasks.Utils.Constants;
using System;
using System.Linq;

namespace Sylas.RemoteTasks.Database
{
    /// <summary>
    /// 数据库相关扩展方法
    /// </summary>
    public static class DatabaseExtensions
    {
        /// <summary>
        /// 是否是数据库连接字符串
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static bool IsDbConnectionString(this string source)
        {
            return DatabaseConstants.ConnectionStringKeywords.Any(x => source.Contains(x, StringComparison.OrdinalIgnoreCase));
        }
    }
}
