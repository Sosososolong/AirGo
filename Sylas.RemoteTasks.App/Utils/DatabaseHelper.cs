using Dapper;
using MySql.Data.MySqlClient;
//using MySqlConnector;
using Newtonsoft.Json.Linq;
using Oracle.ManagedDataAccess.Client;
using Sylas.RemoteTasks.App.Database.SyncBase;
using Sylas.RemoteTasks.App.RegexExp;
using System.Data;
using System.Data.SqlClient;
using System.Text;

namespace Sylas.RemoteTasks.App.Utils
{
    public static partial class DatabaseHelper
    {
        public static string GetTableCreateSql(string tableName) => $"select dbms_metadata.get_ddl('TABLE','{tableName.ToUpper()}') from dual";

        public static IDbConnection GetOracleConnection(string host, string port, string instanceName, string username, string password) => new OracleConnection($"Data Source={host}:{port}/{instanceName};User ID={username};Password={password};PERSIST SECURITY INFO=True;Pooling = True;Max Pool Size = 100;Min Pool Size = 1;");
        public static IDbConnection GetMySqlConnection(string host, string port, string db, string username, string password) => new MySqlConnection($"Server={host};Port={port};Stmt=;Database={db};Uid={username};Pwd={password};Allow User Variables=true;");
        public static IDbConnection GetSqlServerConnection(string host, string port, string db, string username, string password) => string.IsNullOrWhiteSpace(port) ? new SqlConnection($"User ID={username};Password={password};Initial Catalog={db};Data Source={host}") : new SqlConnection($"User ID={username};Password={password};Initial Catalog={db};Data Source={host},{port}");
        /// <summary>
        /// 通用同步逻辑
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="table"></param>
        /// <param name="sourcRecords"></param>
        /// <returns></returns>
        public static async Task SyncDataAsync(IDbConnection conn, string table, List<JToken> sourcRecords, string[] ignoreFields, string[] dateTimeFields)
        {
            //conn.ConnectionString: "server=whitebox.com;port=3306;database=iduo_engine_hznu;user id=root;allowuservariables=True"
            //conn.ConnectionTimeout: 15
            //conn.Database: "iduo_engine_hznu"
            var varFlag = ":";
            var dbRecords = await conn.QueryAsync($"select * from {table}") ?? throw new Exception($"获取{table}数据失败");
            var compareResult = CompareRecordsForSyncDb(sourcRecords, dbRecords, ignoreFields, dateTimeFields, "ID", "ID");
            var inserts = compareResult.Inserts;
            var updates = compareResult.Updates;
            var deletes = compareResult.Deletes;

            var insertValueStatement = GetInsertSqlValueStatement(varFlag, dbRecords?.FirstOrDefault());
            var updateValueStatement = GetUpdateSqlValueStatement(varFlag, dbRecords?.FirstOrDefault());
            List<string> insertingSqls = new();
            List<string> updatingSqls = new();

            foreach (var insert in inserts)
            {
                insert["CREATEDTIME"] = (insert["CREATEDTIME"] is null || string.IsNullOrWhiteSpace(insert["CREATEDTIME"]?.ToString())) ? DateTime.Now : Convert.ToDateTime(insert["CREATEDTIME"]);
                insert["UPDATEDTIME"] = (insert["UPDATEDTIME"] is null || string.IsNullOrWhiteSpace(insert["UPDATEDTIME"]?.ToString())) ? DateTime.Now : Convert.ToDateTime(insert["UPDATEDTIME"]);
                var sql = $"insert into {table} values({insertValueStatement})";
                insertingSqls.Add(sql);

                var inserted = await conn.ExecuteAsync(sql, insert.ToObject<Dictionary<string, object>>());
                System.Diagnostics.Debug.WriteLine($"新增{inserted}条数据数据: {insert["ID"]}");
                if (inserted <= 0)
                {
                    throw new Exception($"添加数据失败{Environment.NewLine}{sql}");
                }
            }

            foreach (var update in updates)
            {
                update["CREATEDTIME"] = (update["CREATEDTIME"] is null || string.IsNullOrWhiteSpace(update["CREATEDTIME"]?.ToString())) ? DateTime.Now : Convert.ToDateTime(update["CREATEDTIME"]);
                update["UPDATEDTIME"] = (update["UPDATEDTIME"] is null || string.IsNullOrWhiteSpace(update["UPDATEDTIME"]?.ToString())) ? DateTime.Now : Convert.ToDateTime(update["UPDATEDTIME"]);
                var sql = $"update {table} set {updateValueStatement} where id={varFlag}ID";
                updatingSqls.Add(sql);

                var updated = await conn.ExecuteAsync(sql, update.ToObject<Dictionary<string, object>>());
                System.Diagnostics.Debug.WriteLine($"更新{updated}条数据数据: {update["ID"]}");
            }
        }

        /// <summary>
        /// 对比数据返回用于同步数据表操作的数据(要新增的数据, 要更新的数据, 要删除的数据)
        /// </summary>
        /// <param name="sourceRecords"></param>
        /// <param name="dbRecords"></param>
        /// <param name="ignoreFields"></param>
        /// <param name="dateTimeFields"></param>
        /// <returns></returns>
        public static CompareResult CompareRecordsForSyncDb(List<JToken> sourceRecords, IEnumerable<dynamic>? dbRecordsDynamic, string[] ignoreFields, string[] dateTimeFields, string sourceIdField, string targetIdField)
        {
            JArray inserts = new();
            JArray updates = new();
            JArray deletes = new();
            var sqlValueBuilder = new StringBuilder();

            bool dbRecordsIsJObject = true;
            var dbRecords = new List<JObject>();
            if (dbRecordsDynamic.Any())
            {
                dbRecordsIsJObject = dbRecordsDynamic.First() is JObject;
                foreach (var item in dbRecordsDynamic)
                {
                    dbRecords.Add(JObject.FromObject(item));
                }
            }

            foreach (var sourceItem in sourceRecords)
            {
                var currentDbRecord = dbRecords?.FirstOrDefault(x => x?[targetIdField]?.ToString() == sourceItem[sourceIdField]?.ToString());
                if (currentDbRecord is null)
                {
                    inserts.Add(sourceItem);
                }
                else
                {
                    dbRecords.Remove(currentDbRecord);

                    bool needUpdate = false;
                    foreach (var propKV in (JObject)sourceItem)
                    {
                        if (propKV.Value is not null && !string.IsNullOrWhiteSpace(propKV.Value.ToString()))
                        {
                            bool v1EqualsV2 = false;
                            if (dateTimeFields.Contains(propKV.Key))
                            {
                                if (propKV.Value is null)
                                {
                                    v1EqualsV2 = true;
                                }
                                else
                                {
                                    var v1 = Convert.ToDateTime(propKV.Value);
                                    var currentDbValue = (currentDbRecord as IDictionary<string, object>)?[propKV.Key];
                                    if (currentDbValue is null || string.IsNullOrWhiteSpace(currentDbValue.ToString()))
                                    {
                                        v1EqualsV2 = false;
                                    }
                                    else
                                    {
                                        var v2 = Convert.ToDateTime(propKV.Value);
                                        v1EqualsV2 = v1 == v2;
                                    }
                                }
                            }
                            else
                            {
                                if (ignoreFields.Contains(propKV.Key))
                                {
                                    // NO字段不作比较
                                    v1EqualsV2 = true;
                                }
                                else
                                {
                                    var v1 = propKV.Value;
                                    string v2 = currentDbRecord is JObject ? (currentDbRecord as JObject)[propKV.Key].ToString() : (currentDbRecord as IDictionary<string, object>)?[propKV.Key]?.ToString();
                                    // v2是dynamic类型时会报错
                                    v1EqualsV2 = v1.StringValueEquals(v2);
                                }
                            }
                            if (!v1EqualsV2)
                            {
                                needUpdate = true;
                                break;
                            }
                        }
                    }
                    if (needUpdate)
                    {
                        updates.Add(sourceItem);
                    }
                }
            }

            // dbRecords剩下的就是它自己独有的数据, 也就是需要删除的数据
            foreach (var dbRecord in dbRecords)
            {
                deletes.Add(dbRecord);
            }

            return new CompareResult() { Inserts = inserts, Updates = updates, Deletes = deletes };
        }

        private static string GetInsertSqlValueStatement(string varFlag, dynamic obj)
        {
            if (obj is not null)
            {
                var valueStatement = new StringBuilder();
                var objDictionary = (IDictionary<string, object>)obj;
                foreach (var prop in objDictionary)
                {
                    valueStatement.Append($"{varFlag}{prop.Key},");
                }
                return valueStatement.ToString().TrimEnd(',');
            }
            return string.Empty;
        }
        private static string GetUpdateSqlValueStatement(string varFlag, dynamic obj)
        {
            if (obj is not null)
            {
                var valueStatement = new StringBuilder();
                var objDictionary = (IDictionary<string, object>)obj;
                foreach (var prop in objDictionary)
                {
                    if (prop.Key == "ID")
                    {
                        continue;
                    }
                    valueStatement.Append($"{prop.Key}={varFlag}{prop.Key},");
                }
                return valueStatement.ToString().TrimEnd(',');
            }
            return string.Empty;
        }

        private static string ConvertCreateTableSql(string createTableSql)
        {
            #region Oracle创建表语句
            string oracleCreateTable = @"CREATE TABLE ""IDUO_IDS4"".""CLIENTS"" 
   (""ID"" NUMBER(10, 0) NOT NULL ENABLE,
	""ENABLED"" NUMBER(10, 0) NOT NULL ENABLE,
	""CLIENTID"" NVARCHAR2(200) NOT NULL ENABLE,
	""PROTOCOLTYPE"" NVARCHAR2(200) NOT NULL ENABLE,
	""REQUIRECLIENTSECRET"" NUMBER(10, 0) NOT NULL ENABLE,
	""CLIENTNAME"" NVARCHAR2(200),
	""DESCRIPTION"" NVARCHAR2(1000),
	""CLIENTURI"" NVARCHAR2(2000),
	""LOGOURI"" NVARCHAR2(2000),
	""REQUIRECONSENT"" NUMBER(10, 0) NOT NULL ENABLE,
	""ALLOWREMEMBERCONSENT"" NUMBER(10, 0) NOT NULL ENABLE,
	""ALWAYSINCLUDEUCLAIMS"" NUMBER(10, 0) NOT NULL ENABLE,
	""REQUIREPKCE"" NUMBER(10, 0) NOT NULL ENABLE,
	""ALLOWPLAINTEXTPKCE"" NUMBER(10, 0) NOT NULL ENABLE,
	""ALLOWACCESSTOKENSVIABROWSER"" NUMBER(10, 0) NOT NULL ENABLE,
	""FRONTCHANNELLOGOUTURI"" NVARCHAR2(2000),
	""FRONTCHANNELLOGOUT"" NUMBER(10, 0) NOT NULL ENABLE,
	""BACKCHANNELLOGOUTURI"" NVARCHAR2(2000),
	""BACKCHANNELLOGOUT"" NUMBER(10, 0) NOT NULL ENABLE,
	""ALLOWOFFLINEACCESS"" NUMBER(10, 0) NOT NULL ENABLE,
	""IDENTITYTOKENLIFETIME"" NUMBER(10, 0) NOT NULL ENABLE,
	""ACCESSTOKENLIFETIME"" NUMBER(10, 0) NOT NULL ENABLE,
	""AUTHORIZATIONCODELIFETIME"" NUMBER(10, 0) NOT NULL ENABLE,
	""CONSENTLIFETIME"" NUMBER(10, 0),
	""ABSOLUTEREFRESHTOKENLIFETIME"" NUMBER(10, 0) NOT NULL ENABLE,
	""SLIDINGREFRESHTOKENLIFETIME"" NUMBER(10, 0) NOT NULL ENABLE,
	""REFRESHTOKENUSAGE"" NUMBER(10, 0) NOT NULL ENABLE,
	""UPDATEACCESSONREFRESH"" NUMBER(10, 0) NOT NULL ENABLE,
	""REFRESHTOKENEXPIRATION"" NUMBER(10, 0) NOT NULL ENABLE,
	""ACCESSTOKENTYPE"" NUMBER(10, 0) NOT NULL ENABLE,
	""ENABLELOCALLOGIN"" NUMBER(10, 0) NOT NULL ENABLE,
	""INCLUDEJWTID"" NUMBER(10, 0) NOT NULL ENABLE,
	""ALWAYSSENDCLIENTCLAIMS"" NUMBER(10, 0) NOT NULL ENABLE,
	""CLIENTCLAIMSPREFIX"" NVARCHAR2(200),
	""PAIRWISESUBJECTSALT"" NVARCHAR2(200),
	""CREATED"" TIMESTAMP(6) NOT NULL ENABLE,
	""UPDATED"" TIMESTAMP(6),
	""LASTACCESSED"" TIMESTAMP(6),
	""USERSSOLIFETIME"" NUMBER(10, 0),
	""USERCODETYPE"" NVARCHAR2(100),
	""DEVICECODELIFETIME"" NUMBER(10, 0) NOT NULL ENABLE,
	""NONEDITABLE"" NUMBER(10, 0) NOT NULL ENABLE,
	 CONSTRAINT ""PK_CLIENTS"" PRIMARY KEY(""ID"")
  USING INDEX PCTFREE 10 INITRANS 2 MAXTRANS 255 COMPUTE STATISTICS NOCOMPRESS LOGGING
  TABLESPACE ""IDUO_IDS4""  ENABLE
   ) SEGMENT CREATION DEFERRED
  PCTFREE 10 PCTUSED 40 INITRANS 1 MAXTRANS 255 NOCOMPRESS LOGGING
  TABLESPACE ""IDUO_IDS4"" ";
            #endregion

            var mysqlCreateTable = createTableSql.Replace('"', '`').Replace(" ENABLE", string.Empty);
            mysqlCreateTable = RegexConst.OracleNumber().Replace(mysqlCreateTable, m => $"int({m.Groups[1].Value})");
            mysqlCreateTable = RegexConst.OracleVarchar().Replace(mysqlCreateTable, m => $"varchar({m.Groups[1].Value})");
            mysqlCreateTable = RegexConst.OracleDateTime().Replace(mysqlCreateTable, "datetime");
            mysqlCreateTable = RegexConst.OraclePrimaryKey().Replace(mysqlCreateTable, m => $"PRIMARY KEY (`{m.Groups[1].Value}`)");
            mysqlCreateTable = RegexConst.OracleUsingIndex().Replace(mysqlCreateTable, string.Empty);
            mysqlCreateTable = RegexConst.OracleTableSpace().Replace(mysqlCreateTable, string.Empty);
            mysqlCreateTable = RegexConst.OracleSegment().Replace(mysqlCreateTable, "ENGINE=InnoDB DEFAULT CHARSET=utf8");
            return mysqlCreateTable;
        }
        private static DatabaseType GetDatabaseTypeName(string connectionString)
        {
            if (!connectionString.ToLower().Contains("initial catalog"))
            {
                if (!connectionString.ToLower().Contains("server"))
                {
                    return DatabaseType.Oracle;
                }

                return DatabaseType.MySql;
            }

            return DatabaseType.SqlServer;
        }
    }

    public class CompareResult
    {
        public JArray Inserts { get; set; }
        public JArray Updates { get; set; }
        public JArray Deletes { get; set; }
    }
}
