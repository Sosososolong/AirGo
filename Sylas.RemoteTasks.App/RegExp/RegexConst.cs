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
        /// 匹配数据库连接字符串 - Sqlite
        /// </summary>
        /// <returns></returns>
        [GeneratedRegex("data source=(?<database>\\w+\\.db.*)", RegexOptions.IgnoreCase, "zh-CN")]
        public static partial Regex ConnectionStringSqlite();
        /// <summary>
        /// 匹配数据库连接字符串 - dm
        /// </summary>
        /// <returns></returns>
        [GeneratedRegex("server\\s*=\\s*(?<host>[\\w\\d\\.]+);\\s*Port\\s*=\\s*(?<port>\\d+);\\s*userid\\s*=\\s*(?<username>\\w+);\\s*pwd\\s*=\\s*(?<password>.+).*", RegexOptions.IgnoreCase, "zh-CN")]
        public static partial Regex ConnectionStringDm();
        /// <summary>
        /// 匹配数据库连接字符串 - mssqllocaldb
        /// </summary>
        /// <returns></returns>
        [GeneratedRegex("server\\s*=\\s*\\(localdb\\)\\\\\\\\mssqllocaldb;\\s*database=(?<database>\\w+);.+", RegexOptions.IgnoreCase, "zh-CN")]
        public static partial Regex ConnectionStringMslocaldb();
        /// <summary>
        /// 匹配数据库连接字符串 - sqlserver
        /// </summary>
        /// <returns></returns>
        [GeneratedRegex("data\\s+source\\s*=(?<host>.+);initial\\s+catalog\\s*=\\s*(?<database>\\w+);user\\s+id=(?<username>\\w+);\\s*password\\s*=\\s*(?<password>.+?).*", RegexOptions.IgnoreCase, "zh-CN")]
        public static partial Regex ConnectionStringSqlServer();
        /// <summary>
        /// 匹配数据库连接字符串 - mysql
        /// </summary>
        /// <returns></returns>
        [GeneratedRegex("server\\s*=\\s*(?<host>.+);port\\s*=\\s*(?<port>\\d+);.*database\\s*=\\s*(?<database>\\w+);uid\\s*=\\s*(?<username>\\w+);pwd=(?<password>.+?).*", RegexOptions.IgnoreCase, "zh-CN")]
        public static partial Regex ConnectionStringMySql();
        /// <summary>
        /// 匹配数据库连接字符串 - oracle
        /// </summary>
        /// <returns></returns>
        [GeneratedRegex("data\\s+source\\s*=\\s*(?<host>[\\d\\w\\.]+):(?<port>\\d+)/(?<instance>\\w+);\\s*user\\s+id=(?<database>\\w+);password\\s*=\\s*(?<password>.+?);.+Min\\s+Pool\\s+Size\\s*=\\s*\\d+.*", RegexOptions.IgnoreCase, "zh-CN")]
        public static partial Regex ConnectionStringOracle();
        public static readonly List<Func<Regex>> AllConnectionStringPatterns = new()
        {
                ConnectionStringMySql,
                ConnectionStringSqlServer,
                ConnectionStringOracle,
                ConnectionStringDm,
                ConnectionStringSqlite,
                ConnectionStringMslocaldb,
        };

        /// <summary>
        /// 匹配数据库连接字符串中的数据库名
        /// 尝试匹配顺序为: sqlserver -> oracle -> dm -> mslocaldb -> mysql -> sqlite
        /// </summary>
        /// <returns></returns>
        [GeneratedRegex("(Initial\\s+Catalog\\s*=\\s*(?<database>\\w+))|(User\\s+ID\\s*=\\s*(?<database>\\w+))|(UserId\\s*=\\s*(?<database>\\w+))|(mssqllocaldb.+Database\\s*=\\s*(?<database>\\w+))|(Database\\s*=\\s*(?<database>\\w+))|(data\\s+source\\s*=\\s*(?<database>[\\w\\.]+))")]
        public static partial Regex ConnectionStringDbName();

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


        [GeneratedRegex("\\{\\{\\$primary\\.(\\w+)\\}\\}")]
        public static partial Regex RefedPrimaryField();

        /// <summary>
        /// 字符串模板, 如: "ID为{id}, 姓名为{name}"
        /// </summary>
        /// <returns></returns>
        [GeneratedRegex("(?<rightQuotation>\"{0,1})\\{\\s*(?<name>\\w+)|(?<name>\\$\\w+)\\s*\\}(?<leftQuotation>\"{0,1})")]
        public static partial Regex StringTmpl();

        /// <summary>
        /// 匹配正则表达式中的分组
        /// </summary>
        /// <returns></returns>
        [GeneratedRegex("\\(\\?<(\\w+)>.+?\\)")]
        public static partial Regex PatternGroup();

        /// <summary>
        /// 字符串模板 - 获取当前对象的属性值
        /// </summary>
        /// <returns></returns>
        [GeneratedRegex("\\$RemoteHostInfo[\\.](?<propName>\\w+)")]
        public static partial Regex CurrentObjPropTmpl();

        /// <summary>
        /// 上传 upload   (?<local>[^\s]+) (?<remote>[^\s]+) -include=(?<include>[^\s+]) -exclude=(?<exclude>[^\s]+)
        /// </summary>
        /// <returns></returns>
        [GeneratedRegex("(?<action>(upload|download))\\s+(\"|')(?<local>[^\"]+)(\"|')\\s*(\"|')(?<remote>[^\"]+)(\"|')\\s*(-include=(?<include>[^\\s+])){0,1}\\s*(-exclude=(?<exclude>[^\\s]+)){0,1}")]
        public static partial Regex CommandRegex();
        /// <summary>
        /// 匹配字符串模板, 模板规定了如何将dataSource(JObject或JArray)中的符合条件的一条或多条数据的某个属性赋值给target的某个属性, 每条数据赋值一次都产生一个target副本
        /// 如 $primary.BodyDictionary.FilterItems.Value=$records[\"DATATYPE\"=21].REFMODELID 表示修改 target.BodyDictionary.FilterItems.Value 的值为 DataType为21的dataSource的RefModelId字段值, 可能多个
        /// </summary>
        /// <returns></returns>
        [GeneratedRegex("\\$primary(?<targetProp>\\.\\w+){1,}=\\$records(\\[\"(?<filterProp>\\w+)\"={1,2}(?<filterValue>[^\\]]+)){0,1}\\]\\.(?<dataProp>\\w+)")]
        public static partial Regex AssignmentRulesTmpl();
    }

}
