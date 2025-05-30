using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.Common
{
    /// <summary>
    /// 预定义常用正则对象
    /// </summary>
    public class RegexConst
    {
        /// <summary>
        /// 匹配Oracle的整型类型
        /// </summary>
        public static readonly Regex OracleNumber = new("NUMBER\\((\\d+),\\s*0\\)");
        /// <summary>
        /// 匹配Oracle的字符串类型
        /// </summary>
        public static readonly Regex OracleVarchar = new("NVARCHAR2\\((\\d+)\\)");
        /// <summary>
        /// 匹配Oracle的时间类型
        /// </summary>
        public static readonly Regex OracleDateTime = new("TIMESTAMP\\(\\d\\)");
        /// <summary>
        /// 匹配Oracle的主键
        /// </summary>
        public static readonly Regex OraclePrimaryKey = new("CONSTRAINT\\s`\\w+`\\sPRIMARY\\sKEY\\(`(ID)`\\)");
        /// <summary>
        /// 匹配Oracle的USING INDEX表达式
        /// </summary>
        public static readonly Regex OracleUsingIndex = new("USING\\sINDEX.*");
        /// <summary>
        /// 匹配Oracle的表空间表达式
        /// </summary>
        public static readonly Regex OracleTableSpace = new("TABLESPACE.*");
        /// <summary>
        /// 匹配Oracle的SEGMENT表达式
        /// </summary>
        public static readonly Regex OracleSegment = new("(?<=\\)\\s*)SEGMENT\\s.*\\n.*");

        /// <summary>
        /// 匹配数据库连接字符串 - Sqlite
        /// </summary>
        /// <returns></returns>
        public static readonly Regex ConnectionStringSqlite = new("data source=(?<database>\\w+\\.db.*)", RegexOptions.IgnoreCase);
        /// <summary>
        /// 匹配数据库连接字符串 - dm
        /// </summary>
        /// <returns></returns>
        public static readonly Regex ConnectionStringDm = new("server\\s*=\\s*(?<host>[\\w\\d\\.]+);\\s*Port\\s*=\\s*(?<port>\\d+);\\s*userid\\s*=\\s*(?<username>\\w+);\\s*pwd\\s*=\\s*(?<password>.+).*", RegexOptions.IgnoreCase);
        /// <summary>
        /// 匹配数据库连接字符串 - mssqllocaldb
        /// </summary>
        /// <returns></returns>
        public static readonly Regex ConnectionStringMsLocalDb = new("server\\s*=\\s*\\(localdb\\)\\\\\\\\mssqllocaldb;\\s*database=(?<database>\\w+);.+", RegexOptions.IgnoreCase);
        /// <summary>
        /// 匹配数据库连接字符串 - sqlserver
        /// </summary>
        /// <returns></returns>
        public static readonly Regex ConnectionStringSqlServer = new("data\\s+source\\s*=(?<host>.+);initial\\s+catalog\\s*=\\s*(?<database>\\w+);user\\s+id=(?<username>\\w+);\\s*password\\s*=\\s*(?<password>.+?).*", RegexOptions.IgnoreCase);
        /// <summary>
        /// 匹配数据库连接字符串 - postgresql
        /// </summary>
        /// <returns></returns>
        public static readonly Regex ConnectionStringPg = new("User ID=(?<username>\\w+);Password=(?<password>.+?);Host=(?<host>.+);Port=(?<port>\\w+);Database=(?<database>\\w+)", RegexOptions.IgnoreCase);
        /// <summary>
        /// 匹配数据库连接字符串 - mysql
        /// </summary>
        /// <returns></returns>
        public static readonly Regex ConnectionStringMySql = new("server\\s*=\\s*(?<host>.+);port\\s*=\\s*(?<port>\\d+);.*database\\s*=\\s*(?<database>\\w+);uid\\s*=\\s*(?<username>\\w+);pwd=(?<password>.+?).*", RegexOptions.IgnoreCase);
        /// <summary>
        /// 匹配数据库连接字符串 - oracle
        /// </summary>
        /// <returns></returns>
        public static readonly Regex ConnectionStringOracle = new("data\\s+source\\s*=\\s*(?<host>[\\d\\w\\.]+):(?<port>\\d+)/(?<instance>\\w+);\\s*user\\s+id=(?<database>\\w+);password\\s*=\\s*(?<password>.+?);.+Min\\s+Pool\\s+Size\\s*=\\s*\\d+.*", RegexOptions.IgnoreCase);
        /// <summary>
        /// 数据库连接字符串正则对象集合
        /// </summary>
        public static readonly List<Regex> AllConnectionStringPatterns =
        [
                ConnectionStringMySql,
                ConnectionStringSqlServer,
                ConnectionStringOracle,
                ConnectionStringDm,
                ConnectionStringSqlite,
                ConnectionStringMsLocalDb,
        ];

        /// <summary>
        /// 匹配数据库连接字符串中的数据库名
        /// 尝试匹配顺序为: sqlserver -> oracle -> dm -> mslocaldb -> mysql -> sqlite
        /// </summary>
        /// <returns></returns>
        public static readonly Regex ConnectionStringDbName = new("(Initial\\s+Catalog\\s*=\\s*(?<database>\\w+))|(User\\s+ID\\s*=\\s*(?<database>\\w+))|(UserId\\s*=\\s*(?<database>\\w+))|(mssqllocaldb.+Database\\s*=\\s*(?<database>\\w+))|(Database\\s*=\\s*(?<database>\\w+))|(data\\s+source\\s*=\\s*(?<database>[\\w\\.]+))");

        /// <summary>
        /// 从数据库连接字符串获取Oracle数据库名
        /// </summary>
        /// <returns></returns>
        public static readonly Regex OracleDbName = new("(?<=user\\s*id\\s*=\\s*)\\w+", RegexOptions.IgnoreCase);
        /// <summary>
        /// 从数据库连接字符串获取SQL server数据库名
        /// </summary>
        /// <returns></returns>
        public static readonly Regex SqlServerDbName = new("(?<=Initial\\s*Catalog\\s*=\\s*)\\w+", RegexOptions.IgnoreCase);
        /// <summary>
        /// 从数据库连接字符串获取MySql数据库名
        /// </summary>
        /// <returns></returns>Database=
        public static readonly Regex MySqlDbName = new("(?<=Database\\s*=\\s*)\\w+", RegexOptions.IgnoreCase);

        /// <summary>
        /// 匹配字段类型是长整型
        /// </summary>
        /// <returns></returns>
        public static readonly Regex ColumnTypeLong = new("(bigint)|(long)");
        /// <summary>
        /// 匹配字段类型整型
        /// </summary>
        /// <returns></returns>
        public static readonly Regex ColumnTypeInt = new("(number)|(int)");

        /// <summary>
        /// 被引用的主键字段
        /// </summary>
        public static readonly Regex RefedPrimaryField = new("\\{\\{\\$primary\\.(\\w+)\\}\\}");

        /// <summary>
        /// 带变量的字符串模板, 比如变量name为动态的, 可以表示为{{name}}, 或者${name}, 或者$name, 或者XxxParser[...]
        /// </summary>
        /// <returns></returns>
        public static readonly Regex StringTmpl = new("(?<name>\\{{2}\\s*.+\\}{2})|(?<name>\\$[\\w\\.]+)|(?<name>\\$\\{.+?\\})|(?<name>(?<parser>\\w+Parser)\\[\\${0,1}\\{{0,1}\\w+(\\[\\d+\\])*(\\.\\w+)*.*\\}{0,1}\\])");

        /// <summary>
        /// 匹配正则表达式中的分组
        /// </summary>
        /// <returns></returns>
        public static readonly Regex PatternGroup = new("\\(\\?<(\\w+)>.+?\\)");


#pragma warning disable CS1570 // XML 注释出现 XML 格式错误
        /// <summary>
        /// 上传 upload   (?<local>[^\s]+) (?<remote>[^\s]+) -include=(?<include>[^\s+]) -exclude=(?<exclude>[^\s]+)
        /// </summary>
        /// <returns></returns>
        public static readonly Regex CommandRegex = new("(?<action>(upload|download))\\s+(\"|'){0,1}(?<local>[^\"]+?)(\"|'){0,1}\\s+(\"|'){0,1}(?<remote>[^\"]+)(\"|'){0,1}\\s*(-include=(?<include>[^\\s]+)){0,1}\\s*(-exclude=(?<exclude>[^\\s]+)){0,1}");
#pragma warning restore CS1570 // XML 注释出现 XML 格式错误
        /// <summary>
        /// 匹配字符串模板, 模板规定了如何将dataSource(JObject或JArray)中的符合条件的一条或多条数据的某个属性赋值给target的某个属性, 每条数据赋值一次都产生一个target副本
        /// 如 $primary.BodyDictionary.FilterItems.Value=$records[\"DATATYPE\"=21].REFMODELID 表示修改 target.BodyDictionary.FilterItems.Value 的值为 DataType为21的dataSource的RefModelId字段值, 可能多个
        /// </summary>
        /// <returns></returns>
        public static readonly Regex AssignmentRulesTmpl = new("\\$primary(?<targetProp>\\.\\w+){1,}=\\$records(\\[\"(?<filterProp>\\w+)\"={1,2}(?<filterValue>[^\\]]+)){0,1}\\]\\.(?<dataProp>\\w+)");

        /// <summary>
        /// 匹配一个方法的方法名和参数部分
        /// </summary>
        public static readonly Regex CodeMethodAndParams = new(@"(?<ExecutorName>\w+)\((?<Args>.*)\)");
    }

}
