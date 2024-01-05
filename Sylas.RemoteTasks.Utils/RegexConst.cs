using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.App.RegexExp
{
    public class RegexConst
    {
        public static readonly Regex OracleNumber = new ("NUMBER\\((\\d+),\\s*0\\)");
        public static readonly Regex OracleVarchar = new ("NVARCHAR2\\((\\d+)\\)");
        public static readonly Regex OracleDateTime = new ("TIMESTAMP\\(\\d\\)");
        public static readonly Regex OraclePrimaryKey = new ("CONSTRAINT\\s`\\w+`\\sPRIMARY\\sKEY\\(`(ID)`\\)");
        public static readonly Regex OracleUsingIndex = new ("USING\\sINDEX.*");
        public static readonly Regex OracleTableSpace = new ("TABLESPACE.*");
        public static readonly Regex OracleSegment = new ("(?<=\\)\\s*)SEGMENT\\s.*\\n.*");

        /// <summary>
        /// 匹配数据库连接字符串 - Sqlite
        /// </summary>
        /// <returns></returns>
        public static readonly Regex ConnectionStringSqlite = new ("data source=(?<database>\\w+\\.db.*)", RegexOptions.IgnoreCase);
        /// <summary>
        /// 匹配数据库连接字符串 - dm
        /// </summary>
        /// <returns></returns>
        public static readonly Regex ConnectionStringDm = new ("server\\s*=\\s*(?<host>[\\w\\d\\.]+);\\s*Port\\s*=\\s*(?<port>\\d+);\\s*userid\\s*=\\s*(?<username>\\w+);\\s*pwd\\s*=\\s*(?<password>.+).*", RegexOptions.IgnoreCase);
        /// <summary>
        /// 匹配数据库连接字符串 - mssqllocaldb
        /// </summary>
        /// <returns></returns>
        public static readonly Regex ConnectionStringMslocaldb = new ("server\\s*=\\s*\\(localdb\\)\\\\\\\\mssqllocaldb;\\s*database=(?<database>\\w+);.+", RegexOptions.IgnoreCase);
        /// <summary>
        /// 匹配数据库连接字符串 - sqlserver
        /// </summary>
        /// <returns></returns>
        public static readonly Regex ConnectionStringSqlServer = new ("data\\s+source\\s*=(?<host>.+);initial\\s+catalog\\s*=\\s*(?<database>\\w+);user\\s+id=(?<username>\\w+);\\s*password\\s*=\\s*(?<password>.+?).*", RegexOptions.IgnoreCase);
        /// <summary>
        /// 匹配数据库连接字符串 - mysql
        /// </summary>
        /// <returns></returns>
        public static readonly Regex ConnectionStringMySql = new ("server\\s*=\\s*(?<host>.+);port\\s*=\\s*(?<port>\\d+);.*database\\s*=\\s*(?<database>\\w+);uid\\s*=\\s*(?<username>\\w+);pwd=(?<password>.+?).*", RegexOptions.IgnoreCase);
        /// <summary>
        /// 匹配数据库连接字符串 - oracle
        /// </summary>
        /// <returns></returns>
        public static readonly Regex ConnectionStringOracle = new ("data\\s+source\\s*=\\s*(?<host>[\\d\\w\\.]+):(?<port>\\d+)/(?<instance>\\w+);\\s*user\\s+id=(?<database>\\w+);password\\s*=\\s*(?<password>.+?);.+Min\\s+Pool\\s+Size\\s*=\\s*\\d+.*", RegexOptions.IgnoreCase);
        public static readonly List<Regex> AllConnectionStringPatterns =
        [
                ConnectionStringMySql,
                ConnectionStringSqlServer,
                ConnectionStringOracle,
                ConnectionStringDm,
                ConnectionStringSqlite,
                ConnectionStringMslocaldb,
        ];

        /// <summary>
        /// 匹配数据库连接字符串中的数据库名
        /// 尝试匹配顺序为: sqlserver -> oracle -> dm -> mslocaldb -> mysql -> sqlite
        /// </summary>
        /// <returns></returns>
        public static readonly Regex ConnectionStringDbName = new ("(Initial\\s+Catalog\\s*=\\s*(?<database>\\w+))|(User\\s+ID\\s*=\\s*(?<database>\\w+))|(UserId\\s*=\\s*(?<database>\\w+))|(mssqllocaldb.+Database\\s*=\\s*(?<database>\\w+))|(Database\\s*=\\s*(?<database>\\w+))|(data\\s+source\\s*=\\s*(?<database>[\\w\\.]+))");

        /// <summary>
        /// 从数据库连接字符串获取Oracle数据库名
        /// </summary>
        /// <returns></returns>
        public static readonly Regex OracleDbName = new ("(?<=user\\s*id\\s*=\\s*)\\w+", RegexOptions.IgnoreCase);
        /// <summary>
        /// 从数据库连接字符串获取SQL server数据库名
        /// </summary>
        /// <returns></returns>
        public static readonly Regex SqlServerDbName = new ("(?<=Initial\\s*Catalog\\s*=\\s*)\\w+", RegexOptions.IgnoreCase);
        /// <summary>
        /// 从数据库连接字符串获取MySql数据库名
        /// </summary>
        /// <returns></returns>Database=
        public static readonly Regex MySqlDbName = new ("(?<=Database\\s*=\\s*)\\w+", RegexOptions.IgnoreCase);

        /// <summary>
        /// 匹配字段类型是长整型
        /// </summary>
        /// <returns></returns>
        public static readonly Regex ColumnTypeLong = new ("(bigint)|(long)");
        /// <summary>
        /// 匹配字段类型整型
        /// </summary>
        /// <returns></returns>
        public static readonly Regex ColumnTypeInt = new ("(number)|(int)");

        /// <summary>
        /// 匹配字段类型Clob或者Blob
        /// </summary>
        /// <returns></returns>
        public static readonly Regex ColumnTypeBlob = new ("(byte\\[\\])|(lob)|(varbinary)");


        public static readonly Regex RefedPrimaryField = new ("\\{\\{\\$primary\\.(\\w+)\\}\\}");

        /// <summary>
        /// 字符串模板, 如: "ID为{id}, 姓名为{name}"
        /// </summary>
        /// <returns></returns>
        public static readonly Regex StringTmpl = new ("(?<rightQuotation>\"{0,1})\\{\\s*(?<name>\\w+)|(?<name>\\$\\w+)\\s*\\}(?<leftQuotation>\"{0,1})");

        /// <summary>
        /// 匹配正则表达式中的分组
        /// </summary>
        /// <returns></returns>
        public static readonly Regex PatternGroup = new ("\\(\\?<(\\w+)>.+?\\)");

        /// <summary>
        /// 字符串模板 - 获取当前对象的属性值
        /// </summary>
        /// <returns></returns>
        public static readonly Regex CurrentObjPropTmpl = new ("\\$RemoteHostInfo[\\.](?<propName>\\w+)");

        /// <summary>
        /// 上传 upload   (?<local>[^\s]+) (?<remote>[^\s]+) -include=(?<include>[^\s+]) -exclude=(?<exclude>[^\s]+)
        /// </summary>
        /// <returns></returns>
        public static readonly Regex CommandRegex = new ("(?<action>(upload|download))\\s+(\"|')(?<local>[^\"]+)(\"|')\\s*(\"|')(?<remote>[^\"]+)(\"|')\\s*(-include=(?<include>[^\\s+])){0,1}\\s*(-exclude=(?<exclude>[^\\s]+)){0,1}");
        /// <summary>
        /// 匹配字符串模板, 模板规定了如何将dataSource(JObject或JArray)中的符合条件的一条或多条数据的某个属性赋值给target的某个属性, 每条数据赋值一次都产生一个target副本
        /// 如 $primary.BodyDictionary.FilterItems.Value=$records[\"DATATYPE\"=21].REFMODELID 表示修改 target.BodyDictionary.FilterItems.Value 的值为 DataType为21的dataSource的RefModelId字段值, 可能多个
        /// </summary>
        /// <returns></returns>
        public static readonly Regex AssignmentRulesTmpl = new ("\\$primary(?<targetProp>\\.\\w+){1,}=\\$records(\\[\"(?<filterProp>\\w+)\"={1,2}(?<filterValue>[^\\]]+)){0,1}\\]\\.(?<dataProp>\\w+)");
    }

}
