using System.Linq;

namespace Sylas.RemoteTasks.Database.SyncBase
{
    /// <summary>
    /// 数据库字段条件比较类型
    /// </summary>
    public static class CompareTypeConsts
    {
        /// <summary>
        /// 大于
        /// </summary>
        public const string Gt = ">";
        /// <summary>
        /// 小于
        /// </summary>
        public const string Lt = "<";
        /// <summary>
        /// 等于
        /// </summary>
        public const string Eq = "=";
        /// <summary>
        /// 大于等于
        /// </summary>
        public const string GtEq = ">=";
        /// <summary>
        /// 小于等于
        /// </summary>
        public const string LtEq = "<=";
        /// <summary>
        /// 不等于
        /// </summary>
        public const string NotEq = "!=";
        /// <summary>
        /// 是否在某些值中, Sql语句in语法
        /// </summary>
        public const string In = "in";
        /// <summary>
        /// 包含, 模糊查询, SQL语句like语法
        /// </summary>
        public const string Include = "include";

        private readonly static string[] _compareTypes = [Gt, Lt, Eq, GtEq, LtEq, NotEq, In, Include];
        /// <summary>
        /// 检查字符串是否为系统支持的有效比较类型
        /// </summary>
        /// <param name="compareType"></param>
        /// <returns></returns>
        public static bool IsCompareType(this string compareType)
        {
            return _compareTypes.Contains(compareType);
        }
    }
}
