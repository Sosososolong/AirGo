using MySql.Data.MySqlClient;
//using MySqlConnector;
using Newtonsoft.Json.Linq;
using Oracle.ManagedDataAccess.Client;
using Sylas.RemoteTasks.Common;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils.Extensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace Sylas.RemoteTasks.Database
{
    /// <summary>
    /// 数据库操作助手
    /// </summary>
    public static partial class DatabaseHelper
    {
        /// <summary>
        /// 获取创建表的Sql语句
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public static string GetTableCreateSql(string tableName) => $"select dbms_metadata.get_ddl('TABLE','{tableName.ToUpper()}') from dual";
        /// <summary>
        /// 获取Oracle连接字符串
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <param name="instanceName"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public static IDbConnection GetOracleConnection(string host, string port, string instanceName, string username, string password) => new OracleConnection($"Data Source={host}:{port}/{instanceName};User ID={username};Password={password};PERSIST SECURITY INFO=True;Pooling = True;Max Pool Size = 100;Min Pool Size = 1;");
        /// <summary>
        /// 获取MySql连接字符串
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <param name="db"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public static IDbConnection GetMySqlConnection(string host, string port, string db, string username, string password) => new MySqlConnection($"Server={host};Port={port};Stmt=;Database={db};Uid={username};Pwd={password};Allow User Variables=true;");
        /// <summary>
        /// 获取SqlServer连接字符串
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <param name="db"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public static IDbConnection GetSqlServerConnection(string host, string port, string db, string username, string password) => string.IsNullOrWhiteSpace(port) ? new SqlConnection($"User ID={username};Password={password};Initial Catalog={db};Data Source={host}") : new SqlConnection($"User ID={username};Password={password};Initial Catalog={db};Data Source={host},{port}");

        /// <summary>
        /// 对比数据返回用于同步数据表操作的数据(要新增的数据, 要更新的数据, 要删除的数据)
        /// </summary>
        /// <param name="sourceRecords"></param>
        /// <param name="dbRecordsDynamic"></param>
        /// <param name="ignoreFields"></param>
        /// <param name="dateTimeFields"></param>
        /// <param name="sourceIdField"></param>
        /// <param name="targetIdField"></param>
        /// <returns></returns>
        public static CompareResult CompareRecordsForSyncDb(List<object> sourceRecords, IEnumerable<dynamic>? dbRecordsDynamic, string[] ignoreFields, string[] dateTimeFields, string sourceIdField, string targetIdField)
        {
            JArray inserts = [];
            JArray updates = [];
            JArray deletes = [];
            var sqlValueBuilder = new StringBuilder();

            bool dbRecordsIsJObject = true;
            var dbRecords = new List<JObject>();
            if (dbRecordsDynamic is not null && dbRecordsDynamic.Any())
            {
                dbRecordsIsJObject = dbRecordsDynamic.First() is JObject;
                foreach (var item in dbRecordsDynamic)
                {
                    dbRecords.Add(JObject.FromObject(item));
                }
            }

            foreach (var sourceItem in sourceRecords)
            {
                var currentDbRecord = dbRecords?.FirstOrDefault(x => x?[targetIdField]?.ToString() == sourceItem.GetPropertyValue(sourceIdField).ToString());
                if (currentDbRecord is null)
                {
                    inserts.Add(sourceItem);
                }
                else
                {
                    dbRecords?.Remove(currentDbRecord);

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
                                    string v2 = currentDbRecord is JObject ? (currentDbRecord as JObject)[propKV.Key]?.ToString() ?? "" : (currentDbRecord as IDictionary<string, object>)?[propKV.Key]?.ToString() ?? "";
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
            if (dbRecords is not null)
            {
                foreach (var dbRecord in dbRecords)
                {
                    deletes.Add(dbRecord);
                }
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
            var mysqlCreateTable = createTableSql.Replace('"', '`').Replace(" ENABLE", string.Empty);
            mysqlCreateTable = RegexConst.OracleNumber.Replace(mysqlCreateTable, m => $"int({m.Groups[1].Value})");
            mysqlCreateTable = RegexConst.OracleVarchar.Replace(mysqlCreateTable, m => $"varchar({m.Groups[1].Value})");
            mysqlCreateTable = RegexConst.OracleDateTime.Replace(mysqlCreateTable, "datetime");
            mysqlCreateTable = RegexConst.OraclePrimaryKey.Replace(mysqlCreateTable, m => $"PRIMARY KEY (`{m.Groups[1].Value}`)");
            mysqlCreateTable = RegexConst.OracleUsingIndex.Replace(mysqlCreateTable, string.Empty);
            mysqlCreateTable = RegexConst.OracleTableSpace.Replace(mysqlCreateTable, string.Empty);
            mysqlCreateTable = RegexConst.OracleSegment.Replace(mysqlCreateTable, "ENGINE=InnoDB DEFAULT CHARSET=utf8");
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
    /// <summary>
    /// 两个集合比较结果
    /// </summary>
    public class CompareResult
    {
        /// <summary>
        /// 需要插入的数据
        /// </summary>
        public JArray Inserts { get; set; } = [];
        /// <summary>
        /// 需要更新的数据
        /// </summary>
        public JArray Updates { get; set; } = [];
        /// <summary>
        /// 需要删除的数据
        /// </summary>
        public JArray Deletes { get; set; } = [];
    }
}
