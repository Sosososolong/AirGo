using Dapper;
using MySql.Data.MySqlClient;
//using MySqlConnector;
using Newtonsoft.Json.Linq;
using Oracle.ManagedDataAccess.Client;
using Sylas.RemoteTasks.App.RegexExp;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Text;
using System.Text.RegularExpressions;

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
            var dbRecords = await conn.QueryAsync($"select * from {table}");
            if (dbRecords is null)
            {
                throw new Exception($"获取{table}数据失败");
            }
            JArray inserts = new();
            JArray updates = new();
            JArray deletes = new();
            var sqlValueBuilder = new StringBuilder();
            var varFlag = ":";



            foreach (var sourceItem in sourcRecords)
            {
                var currentDbRecord = dbRecords?.FirstOrDefault(x => (x as IDictionary<string, object>)?["ID"]?.ToString() == sourceItem["ID"]?.ToString());
                if (currentDbRecord is null)
                {
                    inserts.Add(sourceItem);
                }
                else
                {
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
                                    var v1 = propKV.Value.ToString();
                                    var v2 = (currentDbRecord as IDictionary<string, object>)?[propKV.Key]?.ToString();
                                    v1EqualsV2 = v1 == v2;
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
            deletes = dbRecords?.Where(x => !updates.Any(u => u["ID"] == x["ID"])) as JArray ?? new JArray();

            var insertValueStatement = GetInsertSqlValueStatement(varFlag, dbRecords?.First());
            var updateValueStatement = GetUpdateSqlValueStatement(varFlag, dbRecords.First());
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
        
        public static async Task<bool> TableExist(this IDbConnection conn, string table)
        {
            var databaseType = GetDatabaseTypeName(conn.ConnectionString);
            var checkSql = string.Empty;
            switch (databaseType)
            {
                case DataBaseType.Oracle:
                    checkSql = $"select count(*) from all_tables where owner=upper('{conn.Database}') and table_name=upper('{table}')";
                    break;
                case DataBaseType.Mysql:
                    checkSql = $"select count(*) from information_schema.tables where table_name='{table}' and table_schema=(select database())";
                    break;
                case DataBaseType.SqlServer:
                    checkSql = $"select count(*) from sysobjects where id = object_id('{table}') and OBJECTPROPERTY(id, N'IsUserTable') = 1";
                    break;
                default:
                    break;
            }
            var tableCount = await conn.ExecuteScalarAsync<int>(checkSql);
            return tableCount > 0;
        }
        private static DataBaseType GetDatabaseTypeName(string connectionString)
        {
            if (!connectionString.ToLower().Contains("initial catalog"))
            {
                if (!connectionString.ToLower().Contains("server"))
                {
                    return DataBaseType.Oracle;
                }

                return DataBaseType.Mysql;
            }

            return DataBaseType.SqlServer;
        }
    }
    public enum DataBaseType
    {
        Oracle, Mysql, SqlServer
    }
}
