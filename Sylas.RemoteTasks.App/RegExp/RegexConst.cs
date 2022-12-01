using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.App.RegexExp
{
    public partial class RegexConst
    {
        [GeneratedRegex("NUMBER\\((\\d+),\\s*0\\)")]
        public static partial Regex RegexOracleNumber();
        [GeneratedRegex("NVARCHAR2\\((\\d+)\\)")]
        public static partial Regex RegexOracleVarchar();
        [GeneratedRegex("TIMESTAMP\\(\\d\\)")]
        public static partial Regex RegexOracleDateTime();
        [GeneratedRegex("CONSTRAINT\\s`\\w+`\\sPRIMARY\\sKEY\\(`(ID)`\\)")]
        public static partial Regex RegexOraclePrimaryKey();
        [GeneratedRegex("USING\\sINDEX.*")]
        public static partial Regex RegexOracleUsingIndex();
        [GeneratedRegex("TABLESPACE.*")]
        public static partial Regex RegexOracleTableSpace();
        [GeneratedRegex("(?<=\\)\\s*)SEGMENT\\s.*\\n.*")]
        public static partial Regex RegexOracleSegment();
    }

}
