using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.App.RegexExp
{
    public partial class RegexConst
    {
        [GeneratedRegex("NUMBER\\((\\d+),\\s*0\\)")]
        public static partial Regex OracleNumber();
        [GeneratedRegex("NVARCHAR2\\((\\d+)\\)")]
        public static partial Regex OracleVarchar();
        [GeneratedRegex("TIMESTAMP\\(\\d\\)")]
        public static partial Regex OracleDateTime();
        [GeneratedRegex("CONSTRAINT\\s`\\w+`\\sPRIMARY\\sKEY\\(`(ID)`\\)")]
        public static partial Regex OraclePrimaryKey();
        [GeneratedRegex("USING\\sINDEX.*")]
        public static partial Regex OracleUsingIndex();
        [GeneratedRegex("TABLESPACE.*")]
        public static partial Regex OracleTableSpace();
        [GeneratedRegex("(?<=\\)\\s*)SEGMENT\\s.*\\n.*")]
        public static partial Regex OracleSegment();


        /// <summary>
        /// 从数据库连接字符串获取Oracle数据库名
        /// </summary>
        /// <returns></returns>
        [GeneratedRegex("(?<=user\\s*id\\s*=\\s*)\\w+", RegexOptions.IgnoreCase, "zh-CN")]
        public static partial Regex OracleDbName();
        /// <summary>
        /// 从数据库连接字符串获取SQL server数据库名
        /// </summary>
        /// <returns></returns>
        [GeneratedRegex("(?<=Initial\\s*Catalog\\s*=\\s*)\\w+", RegexOptions.IgnoreCase, "zh-CN")]
        public static partial Regex SqlServerDbName();
        /// <summary>
        /// 从数据库连接字符串获取MySql数据库名
        /// </summary>
        /// <returns></returns>Database=
        [GeneratedRegex("(?<=Database\\s*=\\s*)\\w+", RegexOptions.IgnoreCase, "zh-CN")]
        public static partial Regex MySqlDbName();

        /// <summary>
        /// 匹配字段类型是长整型
        /// </summary>
        /// <returns></returns>
        [GeneratedRegex("(bigint)|(long)")]
        public static partial Regex ColumnTypeLong();
        /// <summary>
        /// 匹配字段类型整型
        /// </summary>
        /// <returns></returns>
        [GeneratedRegex("(number)|(int)")]
        public static partial Regex ColumnTypeInt();

        /// <summary>
        /// 匹配字段类型Clob或者Blob
        /// </summary>
        /// <returns></returns>
        [GeneratedRegex("(byte\\[\\])|(lob)|(varbinary)")]
        public static partial Regex ColumnTypeBlob();
    }

}
