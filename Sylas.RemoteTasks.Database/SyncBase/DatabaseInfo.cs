//using MySqlConnector;
//using Microsoft.Data.SqlClient;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI.Relational;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oracle.ManagedDataAccess.Client;
using Sylas.RemoteTasks.App.RegexExp;
using Sylas.RemoteTasks.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Database.SyncBase
{
    #region TODO
    // ...实现从任意数据库 生成创建MySQL表SQL(字段类型)
    // 1. 实现任意数据库到任意数据库的新增同步
    // 2. 实现对比数据进行新增,更新或删除(已经实现GetSyncData)
    // 3. 尝试重构
    #endregion
    /// <summary>
    /// DatabaseInfo工厂
    /// </summary>
    /// <param name="serviceScopeFactory"></param>
    /// <param name="serviceProvider"></param>
    public class DatabaseInfoFactory(IServiceScopeFactory serviceScopeFactory, IServiceProvider serviceProvider)
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
        /// <summary>
        /// 创建一个DatabaseInfo对象
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public DatabaseInfo Create(string connectionString)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var databaseInfo = scope.ServiceProvider.GetRequiredService<DatabaseInfo>();
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                databaseInfo.ChangeDatabase(connectionString);
            }
            else
            {
                databaseInfo.ChangeDatabase(_serviceProvider.GetRequiredService<DatabaseInfo>());
            }
            return databaseInfo;
        }
    }

    /// <summary>
    /// 提供数据同步, 数据查询等功能
    ///     同一个实例 每调用一次同步方法SyncDatabaseAsync都会创建新的数据库连接, 可实现多线程分批处理数据; 但是不同连接交给不同的对象, 有助于做状态存储管理
    /// </summary>
    public partial class DatabaseInfo : IDatabaseProvider
    {
        private string _connectionString;
        private DatabaseType _dbType;
        private string _varFlag;
        private readonly ILogger _logger;
        /// <summary>
        /// DatabaseInfo初始化
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="configuration"></param>
        /// <exception cref="Exception"></exception>
        public DatabaseInfo(ILogger<DatabaseInfo> logger, IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("Default") ?? throw new Exception("DatabaseInfo: 没有配置\"ConnectionStrings:Default\"数据库连接字符串");
            _logger = logger;
            _dbType = GetDbType(_connectionString);
            _varFlag = GetDbParameterFlag(_dbType);
        }
        /// <summary>
        /// 切换数据库 - 对指定的数据库连接对象切换到指定的数据库
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="db"></param>
        public void ChangeDatabase(IDbConnection conn, string db)
        {
            if (conn.State == ConnectionState.Closed)
            {
                conn.Open();
            }
            if (!string.IsNullOrWhiteSpace(db) && conn.Database != db)
            {
                conn.ChangeDatabase(db);
            }
        }
        /// <summary>
        /// 切换数据库 - 根据连接字符串
        /// </summary>
        /// <param name="connectionString"></param>
        public void ChangeDatabase(string connectionString)
        {
            _connectionString = connectionString;
            _dbType = GetDbType(_connectionString);
            _varFlag = GetDbParameterFlag(_dbType);
        }
        /// <summary>
        /// 切换数据库 - 切换到另一个DatabaseInfo的连接对象
        /// </summary>
        /// <param name="databaseInfo"></param>
        public void ChangeDatabase(DatabaseInfo databaseInfo)
        {
            ChangeDatabase(databaseInfo._connectionString);
        }
        #region 数据库连接对象
        /// <summary>
        /// 根据数据库连接字符串(支持MySQL, SqlServer, Oracle, Sqlite), 获取数据库连接对象
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static IDbConnection GetDbConnection(string connectionString)
        {
            var dbType = GetDbType(connectionString);
            return dbType switch
            {
                DatabaseType.MySql => new MySqlConnection(connectionString),
                DatabaseType.Oracle => new OracleConnection(connectionString),
                DatabaseType.SqlServer => new SqlConnection(connectionString),
                DatabaseType.Sqlite => new SqliteConnection(connectionString),
                _ => throw new Exception($"不支持的数据库连接字符串: {connectionString}"),
            };
        }
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
        public static IDbConnection GetSqlServerConnection(string host, string port, string db, string username, string password) => new SqlConnection($"User ID={username};Password={password};Initial Catalog={db};Data Source={host},{(string.IsNullOrWhiteSpace(port) ? "1433" : port)}");
        /// <summary>
        /// 解析数据库连接字符串, 获取数据库详细信息
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static DbConnectionDetial GetDbConnectionDetial(string connectionString)
        {
            var match = RegexConst.ConnectionStringSqlite.Match(connectionString);
            if (match.Success)
            {
                return new DbConnectionDetial(match.Groups["database"].Value, DatabaseType.Sqlite);
            }
            match = RegexConst.ConnectionStringMslocaldb.Match(connectionString);
            if (match.Success)
            {
                return new DbConnectionDetial(match.Groups["database"].Value, DatabaseType.MsSqlLocalDb);
            }
            match = RegexConst.ConnectionStringOracle.Match(connectionString);
            if (match.Success)
            {
                return new DbConnectionDetial
                {
                    Host = match.Groups["host"].Value,
                    Port = Convert.ToInt32(match.Groups["port"].Value),
                    Account = match.Groups["database"].Value,
                    Password = match.Groups["password"].Value,
                    Database = match.Groups["database"].Value,
                    DatabaseType = DatabaseType.Oracle,
                    InstanceName = match.Groups["instance"].Value
                };
            }
            match = RegexConst.ConnectionStringMySql.Match(connectionString);
            if (match.Success)
            {
                return new DbConnectionDetial
                {
                    Host = match.Groups["host"].Value,
                    Port = Convert.ToInt32(match.Groups["port"].Value),
                    Account = match.Groups["username"].Value,
                    Password = match.Groups["password"].Value,
                    Database = match.Groups["database"].Value,
                    DatabaseType = DatabaseType.MySql
                };
            }
            match = RegexConst.ConnectionStringSqlServer.Match(connectionString);
            if (match.Success)
            {
                var host = match.Groups["host"].Value;
                var port = 1433;
                if (host.Contains(','))
                {
                    var hostArr = host.Split(",");
                    host = hostArr[0];
                    port = Convert.ToInt32(hostArr[1]);
                }
                return new DbConnectionDetial
                {
                    Host = host,
                    Port = port,
                    Account = match.Groups["username"].Value,
                    Password = match.Groups["password"].Value,
                    Database = match.Groups["database"].Value,
                    DatabaseType = DatabaseType.SqlServer
                };
            }
            match = RegexConst.ConnectionStringDm.Match(connectionString);
            if (match.Success)
            {
                return new DbConnectionDetial
                {
                    Host = match.Groups["host"].Value,
                    Port = Convert.ToInt32(match.Groups["port"].Value),
                    Account = match.Groups["username"].Value,
                    Password = match.Groups["password"].Value,
                    Database = match.Groups["username"].Value,
                    DatabaseType = DatabaseType.Dm
                };
            }

            throw new Exception($"连接字符串解析失败: {connectionString}");
        }
        #endregion

        /// <summary>
        /// 分页查询指定数据表, 可使用db参数切换到指定数据库
        /// </summary>
        /// <param name="table">查询的表明</param>
        /// <param name="pageIndex">第几页</param>
        /// <param name="pageSize">每页多少条数</param>
        /// <param name="orderField">排序字段</param>
        /// <param name="isAsc">是否升序</param>
        /// <param name="filters">查询条件</param>
        /// <param name="db">指定要切换查询的数据库, 不指定使用Default配置的数据库</param>
        /// <returns></returns>
        public async Task<PagedData<T>> QueryPagedDataAsync<T>(string table, int pageIndex, int pageSize, string? orderField, bool isAsc, DataFilter filters, string db = "") where T : new()
        {
            if (pageIndex == 0)
            {
                pageIndex = 1;
            }
            if (pageSize == 0)
            {
                pageSize = 20;
            }

            using var conn = GetDbConnection(_connectionString);
            if (!string.IsNullOrWhiteSpace(db))
            {
                conn.ChangeDatabase(db);
            }

            var dbType = GetDbType(conn.ConnectionString);

            string sql = GetPagedSql(conn.Database, table, dbType, pageIndex, pageSize, orderField, isAsc, filters, out string condition, out Dictionary<string, object> parameters);

            string allCountSqlTxt = $"select count(*) from {table} where 1=1 {condition}";

            var allCount = await conn.ExecuteScalarAsync<int>(allCountSqlTxt, parameters);

            var data = await conn.QueryAsync<T>(sql, parameters);

            return new PagedData<T> { Data = data, Count = allCount, TotalPages = (allCount + pageSize - 1) / pageSize };

        }
        /// <summary>
        /// 分页查询指定数据表, 可使用数据库连接字符串connectionString参数指定连接的数据库
        /// </summary>
        /// <param name="table">查询的表明</param>
        /// <param name="pageIndex">第几页</param>
        /// <param name="pageSize">每页多少条数</param>
        /// <param name="orderField">排序字段</param>
        /// <param name="isAsc">是否升序</param>
        /// <param name="filters">查询条件</param>
        /// <param name="connectionString">指定要切换查询的数据库, 不指定使用Default配置的数据库连接</param>
        /// <returns></returns>
        public async Task<PagedData<T>> QueryPagedDataWithConnectionStringAsync<T>(string table, int pageIndex, int pageSize, string? orderField, bool isAsc, DataFilter filters, string connectionString) where T : new()
        {
            _connectionString = connectionString;
            return await QueryPagedDataAsync<T>(table, pageIndex, pageSize, orderField, isAsc, filters);
        }
        /// <summary>
        /// 执行增删改的SQL语句返回受影响的行数 - 可使用db参数指定切换到当前连接的用户有权限的其他数据库
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <param name="db"></param>
        /// <returns></returns>
        public async Task<int> ExecuteSqlAsync(string sql, object parameters, string db = "")
        {
            using var conn = GetDbConnection(_connectionString);
            if (!string.IsNullOrWhiteSpace(db))
            {
                conn.ChangeDatabase(db);
            }
            _logger.LogDebug(sql);

            conn.Open();
            using IDbTransaction trans = conn.BeginTransaction();
            int res;
            try
            {
                res = await conn.ExecuteAsync(sql, parameters, trans);
            }
            catch (Exception)
            {
                trans.Rollback();
                throw;
            }

            trans.Commit();
            conn.Close();
            return res;
        }
        /// <summary>
        /// 执行多条增删改的SQL语句 - 可使用db参数指定切换到当前连接的用户有权限的其他数据库
        /// </summary>
        /// <param name="sqls"></param>
        /// <param name="parameters"></param>
        /// <param name="db"></param>
        /// <returns></returns>
        public async Task<int> ExecuteSqlsAsync(IEnumerable<string> sqls, Dictionary<string, object> parameters, string db = "")
        {
            using var conn = GetDbConnection(_connectionString);
            if (!string.IsNullOrWhiteSpace(db))
            {
                conn.ChangeDatabase(db);
            }
            var affectedRows = 0;
            conn.Open();
            using var trans = conn.BeginTransaction();
            try
            {
                foreach (var sql in sqls)
                {
                    _logger.LogDebug(sql);
                    affectedRows += await conn.ExecuteAsync(sql, parameters, transaction: trans);
                }
            }
            catch (Exception)
            {
                trans.Rollback();
                throw;
            }
            trans.Commit();
            return affectedRows;
        }
        /// <summary>
        /// 执行SQL语句并返回唯一一个值 - 可使用db参数指定切换到当前连接的用户有权限的其他数据库
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <param name="db"></param>
        /// <returns></returns>
        public async Task<int> ExecuteScalarAsync(string sql, Dictionary<string, object> parameters, string db = "")
        {
            using var conn = GetDbConnection(_connectionString);
            if (!string.IsNullOrWhiteSpace(db))
            {
                conn.ChangeDatabase(db);
            }
            var lastInsertRowId = 0;
            conn.Open();
            using var trans = conn.BeginTransaction();
            try
            {
                _logger.LogDebug(sql);
                lastInsertRowId = await conn.ExecuteScalarAsync<int>(sql, parameters, transaction: trans);
            }
            catch (Exception)
            {
                trans.Rollback();
                throw;
            }
            trans.Commit();
            return lastInsertRowId;
        }
        /// <summary>
        /// 执行增删改的SQL语句 - 可使用数据库连接字符串指定数据库
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public async Task<int> ExecuteScalarWithConnectionStringAsync(string sql, Dictionary<string, object> parameters, string connectionString)
        {
            _connectionString = connectionString;
            return await ExecuteSqlAsync(sql, parameters);
        }


        /// <summary>
        /// 同步数据库
        /// </summary>
        /// <param name="sourceConnectionString"></param>
        /// <param name="targetConnectionString"></param>
        /// <param name="sourceSyncedDb"></param>
        /// <param name="sourceSyncedTable"></param>
        /// <returns></returns>
        public async Task SyncDatabaseAsync(string sourceConnectionString, string targetConnectionString, string sourceSyncedDb = "", string sourceSyncedTable = "")
        {
            await SyncDatabaseByConnectionStringsAsync(sourceConnectionString, targetConnectionString, sourceSyncedDb, sourceSyncedTable);
        }

        /// <summary>
        /// 如果表不存在则创建表
        /// </summary>
        /// <param name="db"></param>
        /// <param name="tableName"></param>
        /// <param name="colInfos"></param>
        /// <param name="tableRecords"></param>
        /// <returns></returns>
        public async Task CreateTableIfNotExistAsync(string db, string tableName, IEnumerable<ColumnInfo> colInfos, object? tableRecords = null)
        {
            using var conn = GetDbConnection(_connectionString);
            ChangeDatabase(conn, db);

            try
            {
                var targetDataTable = await QueryAsync(conn, $"select 1 from {tableName} where 1=2", new Dictionary<string, object>(), null); //select count(*) from {tableName}
            }
            catch (Exception ex)
            {
                if (ex.Message.ToLower().Contains("doesn't exist"))
                {
                    // 表不存在创建表
                    var createSql = GenerateCreateTableSql(tableName, _dbType, colInfos, tableRecords);
                    _logger.LogInformation($"数据表{tableName}不存在, 将创建数据表:{Environment.NewLine}{createSql}");
                    _ = await conn.ExecuteAsync(createSql);
                }
            }
        }
        /// <summary>
        /// 如果表不存在则创建表
        /// </summary>
        /// <param name="sourceConnectionString"></param>
        /// <param name="db"></param>
        /// <param name="tableName"></param>
        /// <param name="tableRecords"></param>
        /// <returns></returns>
        public async Task CreateTableIfNotExistAsync(string sourceConnectionString, string db, string tableName, object? tableRecords = null)
        {
            IEnumerable<ColumnInfo> colInfos = await GetTableColumnsInfoAsync(GetDbConnection(sourceConnectionString), tableName);
            using var conn = GetDbConnection(_connectionString);
            ChangeDatabase(conn, db);

            try
            {
                var targetDataTable = await QueryAsync(conn, $"select 1 from {tableName} where 1=2", new Dictionary<string, object>(), null); //select count(*) from {tableName}
            }
            catch (Exception ex)
            {
                if (ex.Message.ToLower().Contains("doesn't exist"))
                {
                    // 表不存在创建表
                    var createSql = GenerateCreateTableSql(tableName, _dbType, colInfos, tableRecords);
                    _logger.LogInformation($"数据表{tableName}不存在, 将创建数据表:{Environment.NewLine}{createSql}");
                    _ = await conn.ExecuteAsync(createSql);
                }
            }
        }
        /// <summary>
        /// 同步数据库
        /// </summary>
        /// <exception cref="Exception"></exception>
        public static async Task SyncDatabaseByConnectionStringsAsync(string sourceConnectionString, string targetConnectionString, string syncedDb = "", string syncedTable = "", bool ignoreException = false)
        {
            using var sourceConn = GetDbConnection(sourceConnectionString);
            if (!string.IsNullOrWhiteSpace(syncedDb))
            {
                sourceConn.ChangeDatabase(syncedDb);
            }
            // 数据源-数据库
            var res = await GetAllTablesAsync(sourceConn, syncedDb);

            DatabaseType targetDbType = GetDbType(targetConnectionString);
            DatabaseType sourceDbType = GetDbType(sourceConnectionString);

            using var targetConn = GetDbConnection(targetConnectionString);
            targetConn.Open();
            foreach (var table in res)
            {
                if (!string.IsNullOrWhiteSpace(syncedTable) && !table.Equals(syncedTable, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                #region 表名信息
                string tableName = table.Split('.').Last();
                
                string tableNameStatement = GetTableStatement(tableName, targetDbType);
                string sourceTableStatement = GetTableStatement(tableName, sourceDbType);
                if (targetDbType == DatabaseType.SqlServer)
                {
                    tableNameStatement = $"[dbo].{tableNameStatement}";
                }
                #endregion

                #region 获取源表的所有字段信息
                IEnumerable<ColumnInfo> colInfos = await GetTableColumnsInfoAsync(sourceConn, tableName);
                string[] primaryKeys = colInfos.Where(x => x.IsPK == 1 && !string.IsNullOrWhiteSpace(x.ColumnName)).Select(x => x.ColumnName).ToArray();
                if (primaryKeys.Length == 0)
                {
                    primaryKeys = [colInfos.First().ColumnCode];
                }
                var primaryKeyStr = string.Join(',', primaryKeys);
                #endregion

                #region 获取表在目标库中的所有数据的主键值集合
                string createSql = string.Empty;
                var readAllPkValuesStart = DateTime.Now;
                List<string> targetPkValues = await GetTableAllPkValuesAsync(targetConn, tableNameStatement, primaryKeys, null);
                var readAllPkValuesEnd = DateTime.Now;
                var readAllPkValuesSeconds = (readAllPkValuesEnd - readAllPkValuesStart).TotalSeconds;
                if (readAllPkValuesSeconds > 30)
                {
                    LoggerHelper.LogCritical($"获取目标表{tableName}所有记录({targetPkValues.Count})的主键值完毕, 消耗{DateTimeHelper.FormatSeconds(readAllPkValuesSeconds)}/s");
                }

                if (targetPkValues.Count == 1 && targetPkValues.First() == "-1")
                {
                    // 表不存在, 生成创建表的语句
                    createSql = GenerateCreateTableSql(tableName, targetDbType, colInfos);
                    // 创建表(或者修改字段)不会回滚
                    try
                    {
                        _ = await targetConn.ExecuteAsync(createSql);
                    }
                    catch (Exception ex)
                    {
                        LoggerHelper.LogError($"创建表出错: {ex.Message}{Environment.NewLine}{createSql}");
                        throw;
                    }
                }
                #endregion

                // 1.生成所有表的insert语句; 异步迭代器(每个子对象包含一个批量插入数据的SQL语句)
                IAsyncEnumerable<TableSqlInfo> tableSqlsInfoCollection = GetTableSqlInfosAsync(table, sourceConn, colInfos, primaryKeys, null, targetConn, targetDbType, targetPkValues, tableNameStatement, null);

                // 2. 执行每个表的insert语句
                using var dbTransaction = targetConn.BeginTransaction();
                await foreach (var tableSqlsInfo in tableSqlsInfoCollection)
                {
                    if (!string.IsNullOrWhiteSpace(tableSqlsInfo.DeleteExistSqlInfo.Sql))
                    {
                        int deletedRows = await targetConn.ExecuteAsync(tableSqlsInfo.DeleteExistSqlInfo.Sql, tableSqlsInfo.DeleteExistSqlInfo.Parameters, transaction: dbTransaction);
                        LoggerHelper.LogInformation($"{table}删除已存在的数据: {deletedRows}");
                    }
                    if (!string.IsNullOrEmpty(tableSqlsInfo.BatchInsertSqlInfo.Sql))
                    {
                        try
                        {
                            var affectedRowsCount = await targetConn.ExecuteAsync(tableSqlsInfo.BatchInsertSqlInfo.Sql, tableSqlsInfo.BatchInsertSqlInfo.Parameters, transaction: dbTransaction);
                            if (targetDbType == DatabaseType.Oracle && affectedRowsCount == -1)
                            {
                                affectedRowsCount = Regex.Matches(tableSqlsInfo.BatchInsertSqlInfo.Sql, @"insert", RegexOptions.IgnoreCase).Count;
                            }
                            LoggerHelper.LogInformation($"{table}添加数据: {affectedRowsCount}");
                        }
                        catch (Exception ex)
                        {
                            // MySQL偶现了一次表情包无法插入的问题(表和字段字符集都是utf8mb4); 后面测试又都正常了
                            if (Regex.IsMatch(ex.Message, @"Incorrect string value:") && targetDbType == DatabaseType.MySql)
                            {
                                var affectedRowsCount = await targetConn.ExecuteAsync($"SET NAMES utf8mb4;{tableSqlsInfo.BatchInsertSqlInfo.Sql}", tableSqlsInfo.BatchInsertSqlInfo.Parameters, transaction: dbTransaction);
                                LoggerHelper.LogInformation($"{table}添加数据: {affectedRowsCount}");
                            }
                            else
                            {
                                LoggerHelper.LogInformation(ex.ToString());
                                LoggerHelper.LogInformation($"tableSqlsInfo.BatchInsertSqlInfo.BatchInsertSql: {tableSqlsInfo.BatchInsertSqlInfo.Sql}");
                                if (!ignoreException)
                                {
                                    dbTransaction.Rollback();
                                    throw;
                                }
                                dbTransaction.Commit();
                            }
                        }
                    }
                }
                dbTransaction.Commit();
            }
        }
        /// <summary>
        /// 将数据集同步到指定的数据库(根据提供的数据库连接字符串)中指定的数据表
        /// </summary>
        /// <param name="table"></param>
        /// <param name="sourceRecords"></param>
        /// <param name="ignoreFields"></param>
        /// <param name="connectionString"></param>
        /// <param name="sourceIdField"></param>
        /// <param name="targetIdField"></param>
        /// <returns></returns>
        public async Task SyncDataToDbWithTargetConnectionStringAsync(string table, IEnumerable<JToken> sourceRecords, string[] ignoreFields, string sourceIdField = "", string targetIdField = "", string connectionString = "")
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                _connectionString = connectionString;
            }
            await SyncDatabaseAsync(table, sourceRecords, ignoreFields, sourceIdField: sourceIdField, targetIdField: targetIdField);
        }
        /// <summary>
        /// 将数据集同步到指定的数据库(根据指定数据库名)中的指定的数据表
        /// </summary>
        /// <param name="table"></param>
        /// <param name="sourceRecords"></param>
        /// <param name="ignoreFields"></param>
        /// <param name="db"></param>
        /// <param name="sourceIdField"></param>
        /// <param name="targetIdField"></param>
        /// <returns></returns>
        public async Task SyncDataToDbAsync(string table, IEnumerable<JToken> sourceRecords, string[] ignoreFields, string sourceIdField = "", string targetIdField = "", string db = "")
        {
            await SyncDatabaseAsync(table, sourceRecords, ignoreFields: ignoreFields, sourceIdField: sourceIdField, targetIdField: targetIdField, db);
        }
        /// <summary>
        /// 通用同步逻辑
        /// </summary>
        /// <param name="table"></param>
        /// <param name="sourceRecords"></param>
        /// <param name="ignoreFields"></param>
        /// <param name="sourceIdField"></param>
        /// <param name="targetIdField"></param>
        /// <param name="db"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task SyncDatabaseAsync(string table, IEnumerable<JToken> sourceRecords, string[] ignoreFields, string sourceIdField = "", string targetIdField = "", string db = "")
        {
            //conn.ConnectionString: "server=127.0.0.1;port=3306;database=engine;user id=root;allowuservariables=True"
            //conn.ConnectionTimeout: 15
            //conn.Database: "my_engine"
            using var conn = GetDbConnection(_connectionString);
            if (!string.IsNullOrWhiteSpace(db))
            {
                ChangeDatabase(conn, db);
            }
            await SyncDatabaseAsync(conn, table, sourceRecords, ignoreFields, sourceIdField, targetIdField, _logger);
        }

        /// <summary>
        /// 把数据同步到指定数据表
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="table"></param>
        /// <param name="sourceRecords"></param>
        /// <param name="ignoreFields"></param>
        /// <param name="sourceIdField"></param>
        /// <param name="targetIdField"></param>
        /// <param name="db"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task SyncDatabaseAsync(string connectionString, string table, IEnumerable<JToken> sourceRecords, string[] ignoreFields, string sourceIdField = "", string targetIdField = "", string db = "", ILogger? logger = null)
        {
            using var conn = GetDbConnection(connectionString);
            if (!string.IsNullOrWhiteSpace(db) && conn.Database != db)
            {
                conn.ChangeDatabase(db);
            }

            await SyncDatabaseAsync(conn, table, sourceRecords, ignoreFields, sourceIdField, targetIdField, logger);
        }
        /// <summary>
        /// 把数据同步到指定数据表
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="table"></param>
        /// <param name="sourceRecords"></param>
        /// <param name="ignoreFields"></param>
        /// <param name="sourceIdField"></param>
        /// <param name="targetIdField"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task SyncDatabaseAsync(IDbConnection conn, string table, IEnumerable<JToken> sourceRecords, string[] ignoreFields, string sourceIdField = "", string targetIdField = "", ILogger? logger = null)
        {
            var varFlag = GetDbParameterFlag(conn.ConnectionString);
            // 先获取数据
            var dbRecords = await conn.QueryAsync($"select * from {conn.Database}.{table}") ?? throw new Exception($"获取{table}数据失败");

            var compareResult = await CompareRecordsAsync(sourceRecords, dbRecords, ignoreFields, sourceIdField, targetIdField);
            // 然后删除已存在并且发生了变化的数据
            await DeleteExistRecordsAsync(conn, compareResult, table, varFlag, targetIdField, logger);

            var inserts = compareResult.ExistInSourceOnly;
            if (compareResult.Changed.Any())
            {
                compareResult.Changed.ForEach(x => inserts.Add(x.SourceRecord));
            }

            await InsertDataAsync(inserts, table, conn);
        }

        /// <summary>
        /// 将数据添加到表中
        /// </summary>
        /// <param name="inserts"></param>
        /// <param name="table"></param>
        /// <param name="conn"></param>
        /// <returns></returns>
        public static async Task InsertDataAsync(JArray inserts, string table, IDbConnection conn)
        {
            // TODO: 尝试将GetInsertSqlInfosAsync的分批批量插入语句逻辑移植到GetTableSqlsInfoAsync, 然后这里替换为GetTableSqlsInfoAsync, 好处是表不存在可以创建表
            var insertSqlInfos = await GetInsertSqlInfosAsync(inserts, table, conn);
            foreach (var sqlInfo in insertSqlInfos)
            {
                int inserted = 0;
                try
                {
                    inserted = await conn.ExecuteAsync(sqlInfo.Sql, sqlInfo.Parameters);
                }
                catch (Exception)
                {
                    LoggerHelper.LogInformation(sqlInfo.Sql);
                    LoggerHelper.LogInformation(JsonConvert.SerializeObject(sqlInfo.Parameters));
                    throw;
                }

                #region 记录数据日志
                LogData(null, table, inserted, sqlInfo);
                #endregion
            }
        }

        /// <summary>
        /// 获取主键
        /// </summary>
        /// <param name="sourceRecord"></param>
        /// <param name="sourceIdField"></param>
        /// <returns></returns>
        static List<string> GetPrimaryKeys(JObject? sourceRecord, string sourceIdField)
        {
            List<string> sourcePrimaryKeys = [];
            var sourceFirstPropertieNames = sourceRecord?.Properties()?.Select(x => x.Name.ToLower())?.ToList();
            sourcePrimaryKeys = CheckRecordIdFields(sourceIdField ?? string.Empty, sourceFirstPropertieNames);
            return sourcePrimaryKeys;

            #region 本地方法, 校验参数提供的数据Id字段, 支持组合Id, 不存在的去掉; 如果提供的字段都不存在则校验有可能存在的主键字段如: id,guid, 存在则添加.
            List<string> CheckRecordIdFields(string idFieldName, List<string>? allFields)
            {
                if (allFields is null || !allFields.Any())
                {
                    return idFieldName.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                List<string> idFields = [];
                var sourceIdFieldArr = idFieldName.Split(',');
                // 校验参数提供的Id字段是否存在
                foreach (var idField in sourceIdFieldArr)
                {
                    if (allFields.Any(x => x == idField.ToLower()))
                    {
                        idFields.Add(idField);
                    }
                }
                if (!idFields.Any())
                {
                    if (allFields.Any(x => x == "id"))
                    {
                        idFields.Add("id");
                    }
                    else if (allFields.Any(x => x == "guid"))
                    {
                        idFields.Add("guid");
                    }
                    else
                    {
                        idFields.Add(allFields.First());
                    }
                }
                return idFields;
            }
            #endregion
        }

        static async Task DeleteExistRecordsAsync(IDbConnection conn, DataComparedResult compareResult, string targetTable, string varFlag, string targetIdField, ILogger? logger = null)
        {
            if (!compareResult.Changed.Any())
            {
                return;
            }
            var targetDbType = GetDbType(conn.ConnectionString);
            StringBuilder deleteSqlsBuilder = new();
            var index = 0;
            Dictionary<string, dynamic> parameters = [];
            foreach (var item in compareResult.Changed)
            {
                var itemProps = item.TargetRecord.Properties();

                if (targetIdField.Contains(','))
                {
                    var targetPrimaryKeys = GetPrimaryKeys(compareResult.Changed.FirstOrDefault()?.TargetRecord, targetIdField);
                    StringBuilder conditionsBuilder = new();
                    for (int i = 0; i < targetPrimaryKeys.Count; i++)
                    {
                        var targetPrimaryKey = targetPrimaryKeys[i];
                        var targetIdVal = itemProps.FirstOrDefault(x => string.Equals(x.Name, targetPrimaryKey, StringComparison.OrdinalIgnoreCase))?.Value;
                        if (targetIdVal is null)
                        {
                            var firstProp = itemProps.First();
                            targetIdField = firstProp.Name;
                            targetIdVal = firstProp.Value;
                        }

                        conditionsBuilder.Append($" and {targetPrimaryKey}={varFlag}id{i}{index}");
                        parameters.Add($"id{i}{index}", targetIdVal.ToObject<dynamic>());
                    }
                    deleteSqlsBuilder.Append($"delete from {targetTable} where 1=1 {conditionsBuilder};{Environment.NewLine}");
                }
                else
                {
                    targetIdField = string.IsNullOrEmpty(targetIdField) ? itemProps.First().Name : targetIdField;
                    var targetIdVal = itemProps.FirstOrDefault(x => string.Equals(x.Name, targetIdField, StringComparison.OrdinalIgnoreCase))?.Value;
                    if (targetIdVal is null)
                    {
                        var firstProp = itemProps.First();
                        targetIdField = firstProp.Name;
                        targetIdVal = firstProp.Value;
                    }

                    deleteSqlsBuilder.Append($"{varFlag}id{index},");
                    parameters.Add($"id{index}", targetIdVal.ToObject<dynamic>());
                }


                index++;

                if (index > 0 && index % 100 == 0 || index >= compareResult.Changed.Count)
                {
                    if (deleteSqlsBuilder.Length > 0)
                    {
                        string deleteSqls;
                        if (targetIdField.Contains(','))
                        {
                            deleteSqls = $"SET foreign_key_checks=0;{Environment.NewLine}{deleteSqlsBuilder}{Environment.NewLine}SET foreign_key_checks=1;";
                        }
                        else
                        {
                            deleteSqls = $"SET foreign_key_checks=0;{Environment.NewLine}delete from {GetTableStatement(targetTable, targetDbType)} where {targetIdField} in({deleteSqlsBuilder.ToString().TrimEnd(',')});{Environment.NewLine}SET foreign_key_checks=1;";
                        }
                        var deleted = await conn.ExecuteAsync(deleteSqls, parameters);
                        logger?.LogInformation($"已经删除{deleted}条记录");
                        deleteSqlsBuilder.Clear();
                    }
                    else
                    {
                        logger?.LogInformation($"没有旧数据需要删除");
                    }
                }
            }
        }
        static void LogData(ILogger? logger, string table, int inserted, SqlInfo sqlInfo)
        {
            string info = $"数据表{table}新增{inserted}条数据数据";
            if (logger is null)
            {
                LoggerHelper.LogInformation(info);
            }
            else
            {
                logger?.LogInformation(info);
            }

            //var fieldsMatch = Regex.Match(sqlInfo.Sql, @"insert\s+into\s+\w+\s*\(`{0,1}\w+`{0,1}(?<otherFields>(,\s*`{0,1}\w+`{0,1}\s*)*)\)", RegexOptions.IgnoreCase);
            //var fieldsCount = fieldsMatch.Groups["otherFields"].Value.Count(x => x == ',') + 1;

            //var names = sqlInfo.Parameters.Keys;
            //var parameterBuilder = new StringBuilder();
            //var index = 0;
            //foreach (var name in names)
            //{
            //    index++;
            //    var val = sqlInfo.Parameters[name]?.ToString();
            //    val = val?.Length > 50 ? val?[..50] : val;
            //    if (!string.IsNullOrWhiteSpace(val))
            //    {
            //        parameterBuilder.Append($"{val}\t");
            //        if (index % fieldsCount == 0)
            //        {
            //            parameterBuilder.Append(Environment.NewLine);
            //        }
            //    }
            //}
            //logger?.LogInformation(parameterBuilder.ToString());
        }

        /// <summary>
        /// 获取批量插入的SQL语句信息
        /// </summary>
        /// <param name="data"></param>
        /// <param name="table"></param>
        /// <param name="conn"></param>
        /// <returns></returns>
        public static async Task<List<SqlInfo>> GetInsertSqlInfosAsync(object data, string table, IDbConnection conn)
        {
            var inserts = data is JArray jarray ? jarray : JArray.FromObject(data);
            var dbType = GetDbType(conn.ConnectionString);
            var tableCols = await GetTableColumnsInfoAsync(conn, table);
            var varFlag = GetDbParameterFlag(dbType);

            var firstRecord = inserts.FirstOrDefault();

            var insertSqls = new List<SqlInfo>();
            if (firstRecord is null)
            {
                return insertSqls;
            }
            if (firstRecord is not JObject firstRecordJObj)
            {
                return insertSqls;
            }

            // 1. 获取字段部分
            var insertFieldsStatement = GetFieldsStatement(firstRecordJObj, dbType);

            var insertValuesStatementBuilder = new StringBuilder();
            int recordIndex = 0;
            
            var parameters = new Dictionary<string, object?>();
            string? batchInsertSql;
            foreach (JObject insert in inserts.Cast<JObject>())
            {
                #region 2. 为每条数据生成Values部分(@v1,@v2,...),\n
                insertValuesStatementBuilder.Append("(");
                insertValuesStatementBuilder.Append(GenerateRecordValuesStatement(insert, tableCols, parameters, varFlag, recordIndex));
                insertValuesStatementBuilder.Append($"),{Environment.NewLine}");
                #endregion

                // 3. 生成一条批量语句: 每100条数据生成一个批量语句, 然后重置语句拼接
                if (recordIndex > 0 && (recordIndex + 1) % 100 == 0)
                {
                    batchInsertSql = GetBatchInsertSql(table, insertFieldsStatement, insertValuesStatementBuilder.ToString(), dbType);
                    insertSqls.Add(new SqlInfo(batchInsertSql, parameters));
                    insertValuesStatementBuilder.Clear();
                    parameters = [];
                }
                recordIndex++;
            }

            // 4. 生成一条批量语句: 生成最后一条批量语句
            if (parameters.Count > 0) // 有可能正好100条数据已经全部处理, parameters处于清空状态
            {
                batchInsertSql = GetBatchInsertSql(table, insertFieldsStatement, insertValuesStatementBuilder.ToString(), dbType);
                insertSqls.Add(new SqlInfo(batchInsertSql, parameters));
            }

            return insertSqls;
        }
        /// <summary>
        /// 获取一个库中的所有表
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="dbName"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>

        public static async Task<IEnumerable<string>> GetAllTablesAsync(IDbConnection conn, string dbName)
        {
            string connstr = conn.ConnectionString;
            var dbtype = GetDbType(connstr);
            if (string.IsNullOrWhiteSpace(dbName))
            {
                dbName = conn.Database;
            }
            var tables = dbtype switch
            {
                DatabaseType.MySql => await conn.QueryAsync($"select TABLE_NAME from information_schema.`TABLES` WHERE table_schema='{dbName}'"),
                DatabaseType.SqlServer => await conn.QueryAsync("SELECT name AS TABLE_NAME FROM sys.tables;"),
                DatabaseType.Oracle => await conn.QueryAsync($"SELECT TABLE_NAME FROM user_tables"),
                DatabaseType.Sqlite => await conn.QueryAsync($"SELECT name as TABLE_NAME FROM sqlite_master WHERE type='table';"),
                _ => throw new NotImplementedException(),
            };
            return tables.Select(x => (x as IDictionary<string, object> ?? throw new Exception("DapperRow转换为字典失败"))["TABLE_NAME"].ToString());
        }
        /// <summary>
        /// 获取一个库中的所有表
        /// </summary>
        /// <param name="connStr"></param>
        /// <param name="dbName"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<dynamic>> GetAllTablesAsync(string connStr, string dbName)
        {
            var conn = GetDbConnection(connStr);
            return await GetAllTablesAsync(conn, dbName);
        }
        /// <summary>
        /// 表是否存在
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="table"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static async Task<bool> TableExist(IDbConnection conn, string table)
        {
            var databaseType = GetDbType(conn.ConnectionString);
            var checkSql = string.Empty;
            switch (databaseType)
            {
                case DatabaseType.Oracle:
                    checkSql = $"select count(*) from all_tables where owner=upper('{conn.Database}') and table_name=upper('{table}')";
                    break;
                case DatabaseType.MySql:
                    checkSql = $"select count(*) from information_schema.tables where table_name='{table}' and table_schema=(select database())";
                    break;
                case DatabaseType.SqlServer:
                    checkSql = $"select count(*) from sysobjects where id = object_id('{table}') and OBJECTPROPERTY(id, N'IsUserTable') = 1";
                    break;
                case DatabaseType.Sqlite:
                    throw new NotImplementedException();
                default:
                    break;
            }
            var tableCount = await conn.ExecuteScalarAsync<int>(checkSql);
            return tableCount > 0;
        }
        
        private static void ValideParameter(string parameter)
        {
            if (parameter.Contains('-') || parameter.Contains('\''))
            {
                throw new Exception($"参数{parameter}不合法");
            }
        }

        /// <summary>
        /// 获取分页查询的SQL语句
        /// </summary>
        /// <param name="db"></param>
        /// <param name="table"></param>
        /// <param name="dbType"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="orderField"></param>
        /// <param name="isAsc"></param>
        /// <param name="filters"></param>
        /// <param name="queryCondition"></param>
        /// <param name="queryConditionParameters"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="Exception"></exception>
        public static string GetPagedSql(string db, string table, DatabaseType dbType, int pageIndex, int pageSize, string? orderField, bool isAsc, DataFilter? filters, out string queryCondition, out Dictionary<string, object> queryConditionParameters)
        {
            string orderClause = string.Empty;
            if (!string.IsNullOrWhiteSpace(orderField))
            {
                orderField = orderField.Replace("-", string.Empty).Replace("'", string.Empty);
                ValideParameter(orderField);
                orderClause = $" order by {orderField} {(isAsc ? "asc" : "desc")}";
            }
            string parameterFlag = dbType == DatabaseType.Oracle || dbType == DatabaseType.Dm ? ":" : "@";

            #region 处理过滤条件
            queryCondition = string.Empty;
            queryConditionParameters = [];
            if (filters != null)
            {
                if (filters.FilterItems != null && filters.FilterItems.Count() > 0)
                {
                    var filterItems = filters.FilterItems;
                    foreach (var filterField in filterItems)
                    {
                        if (filterField.FieldName is null || filterField.CompareType is null || filterField.Value is null || string.IsNullOrWhiteSpace(filterField.Value.ToString()))
                        {
                            continue;
                        }
                        var key = filterField.FieldName;
                        if (!queryConditionParameters.ContainsKey(key))
                        {
                            var compareType = filterField.CompareType;
                            var compareTypes = new List<string> { ">", "<", "=", ">=", "<=", "!=", "include", "in" };
                            if (!compareTypes.Contains(compareType))
                            {
                                throw new ArgumentException("过滤条件比较类型不正确");
                            }
                            var val = filterField?.Value ?? string.Empty;
                            ValideParameter(key);
                            if (compareType == "in")
                            {
                                var valList = val.Split(',');
                                queryCondition += $" and {key} in(";
                                int valListIndex = 0;
                                foreach (var valItem in valList)
                                {
                                    var paramName = valListIndex == 0 ? key : $"{key}{valListIndex}";
                                    queryCondition += $"{parameterFlag}{paramName},";
                                    queryConditionParameters.Add(paramName, valItem);
                                    valListIndex++;
                                }
                                queryCondition = queryCondition.TrimEnd(',');
                                queryCondition += ")";
                            }
                            else
                            {
                                if (compareType == "include")
                                {
                                    queryCondition += $" and {key} like CONCAT(CONCAT('%', {parameterFlag}{key}), '%')";
                                }
                                else
                                {
                                    queryCondition += $" and {key}{compareType}{parameterFlag}{key}";
                                }
                                queryConditionParameters.Add(key, val);
                            }
                        }
                    }
                }
                if (filters.Keywords != null && filters.Keywords.Fields != null && filters.Keywords.Fields.Length > 0 && !string.IsNullOrWhiteSpace(filters.Keywords.Value))
                {
                    var keyWordsCondition = string.Empty;
                    foreach (var keyWordsField in filters.Keywords.Fields)
                    {
                        string varName = $"keyWords{keyWordsField}";
                        if (!queryConditionParameters.ContainsKey(varName))
                        {
                            if (dbType == DatabaseType.MySql)
                            {
                                keyWordsCondition += $" or {keyWordsField} like CONCAT(CONCAT('%', {parameterFlag}{varName}), '%')";
                            }
                            else if (dbType == DatabaseType.SqlServer)
                            {
                                keyWordsCondition += $" or {keyWordsField} like '%'+{parameterFlag}{varName}+'%'";
                            }
                            else
                            {
                                keyWordsCondition += $" or {keyWordsField} like '%'||{parameterFlag}{varName}||'%'";
                            }
                            queryConditionParameters.Add(varName, filters.Keywords.Value);
                        }
                    }
                    if (!string.IsNullOrEmpty(keyWordsCondition))
                    {
                        queryCondition += $" and ({keyWordsCondition[4..]})";
                    }
                }
            }
            #endregion

            string baseSqlTxt = $"select * from {table} where 1=1 {queryCondition}";

            #region 不同数据库对应的SQL语句
            string sql = dbType switch
            {
                DatabaseType.Oracle => $@"select * from 
	(
	select t.*,rownum no from 
		(
		{baseSqlTxt} {orderClause}
		) t 
	)
where no>({pageIndex}-1)*{pageSize} and no<=({pageIndex})*{pageSize}",
                DatabaseType.MySql => $@"{baseSqlTxt} {orderClause} limit {(pageIndex - 1) * pageSize},{pageSize}",
                DatabaseType.SqlServer => $"{baseSqlTxt} {(string.IsNullOrEmpty(orderClause) ? "order by id desc" : orderClause)} OFFSET ({pageIndex}-1)*{pageSize} ROWS FETCH NEXT {pageSize} ROW ONLY",
                DatabaseType.Sqlite => $@"{baseSqlTxt} {orderClause} limit {pageSize} offset {(pageIndex - 1) * pageSize}",
                _ => throw new Exception("未知的数据库类型")

            };
            #endregion

            return sql;
        }
        /// <summary>
        /// 数据库分页数据
        /// </summary>
        /// <param name="table"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="orderField"></param>
        /// <param name="isAsc"></param>
        /// <param name="dbConn"></param>
        /// <param name="filters"></param>
        /// <returns></returns>
        public static async Task<PagedData> GetPagedDataAsync(string table, int pageIndex, int pageSize, string? orderField, bool isAsc, IDbConnection dbConn, DataFilter? filters)
        {
            // TODO: 数据库类型 作为公共属性
            var dbType = GetDbType(dbConn.ConnectionString);

            string sql = GetPagedSql(dbConn.Database, table, dbType, pageIndex, pageSize, orderField, isAsc, filters, out string condition, out Dictionary<string, object> parameters);

            string allCountSqlTxt = $"select count(*) from {table} where 1=1 {condition}";
            var data = await dbConn.QueryAsync(sql, parameters);
            var allCount = await dbConn.ExecuteScalarAsync<int>(allCountSqlTxt, parameters);
            var dataReader = await dbConn.ExecuteReaderAsync(sql, parameters);
            return new PagedData { Data = data, DataReader = dataReader, Count = allCount };
        }

        /// <summary>
        /// 获取insert语句的字段部分 Name,Age
        /// </summary>
        /// <param name="colInfos"></param>
        /// <param name="dbtype"></param>
        /// <returns></returns>
        public static string GetFieldsStatement(IEnumerable<ColumnInfo> colInfos, DatabaseType dbtype)
        {
            var columnBuilder = new StringBuilder();
            var colCodes = colInfos.Select(x => x.ColumnCode);
            colCodes = GetTableStatements(colCodes, dbtype);
            
            return string.Join(',', colCodes);
        }
        /// <summary>
        /// 获取insert语句的字段部分 Name,Age
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="dbType"></param>
        /// <returns></returns>
        public static string GetFieldsStatement(JObject obj, DatabaseType dbType)
        {
            var insertFields = obj.Properties().Where(x => x.Name != "NO").Select(x => x.Name);
            insertFields = GetTableStatements(insertFields, dbType);
            return string.Join(',', insertFields);
        }
        /// <summary>
        /// 获取字段值
        /// </summary>
        /// <param name="value"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static object? GetColumnValue(object value, string type)
        {
            if (value.GetType().Name == "DBNull")
            {
                // 不能使用DBNull类型的数据, 返回默认值或者null
                if (!string.IsNullOrWhiteSpace(type))
                {
                    var lowerType = type.ToLower();
                    if (lowerType.Contains("varchar") || lowerType.Contains("clob") || lowerType.Contains("text"))
                    {
                        return null;
                    }
                    if (lowerType.Contains("time"))
                    {
                        return DateTime.Now;
                    }
                    if (lowerType.Contains("int") || lowerType.Contains("number"))
                    {
                        return 0;
                    }
                    if (lowerType.Contains("byte") || lowerType.Contains("varbinary") || lowerType.Contains("blob"))
                    {
                        return Array.Empty<byte>();
                    }
                    if (lowerType.Contains("bit"))
                    {
                        return false;
                    }
                    return null;
                }
            }
            return value;
        }
        /// <summary>
        /// 获取字段值
        /// </summary>
        /// <param name="p"></param>
        /// <param name="col"></param>
        /// <returns></returns>
        public static dynamic? GetColumnValue(JProperty p, ColumnInfo? col)
        {
            dynamic? pVal = null;
            // 实际是字节数组这里获得的是字节数组转换为base64字符串的结果
            if (p.Value.Type == JTokenType.String && col is not null && !string.IsNullOrWhiteSpace(col.ColumnType) && col.ColumnType.Contains("blob", StringComparison.OrdinalIgnoreCase))
            {
                byte[] bytes = new byte[p.Value.ToString().Length];
                if (Convert.TryFromBase64String(p.Value.ToString(), bytes, out int bytesWritten))
                {
                    pVal = bytes;
                }
            }
            if (p.Value.Type == JTokenType.String && col is not null && !string.IsNullOrWhiteSpace(col.ColumnType) && col.ColumnType.Contains("time", StringComparison.OrdinalIgnoreCase))
            {
                if (DateTime.TryParse(p.Value.ToString(), out DateTime timeValue))
                {
                    pVal = timeValue;
                }
                else
                {
                    pVal = DateTime.Now;
                }
            }

            // BOOKMARK: ※※※※ 转为dynamic就不需要转换为具体的类型了 ※※※※
            pVal ??= p.Value.ToObject<dynamic>();

            return pVal;
        }
        /// <summary>
        /// 获取批量插入的SQL语句
        /// </summary>
        /// <param name="table"></param>
        /// <param name="columnsStatement"></param>
        /// <param name="valuesStatement"></param>
        /// <param name="dbType"></param>
        /// <returns></returns>
        public static string GetBatchInsertSql(string table, string columnsStatement, string valuesStatement, DatabaseType dbType)
        {
            string tableStatement = GetTableStatement(table, dbType);
            if (dbType == DatabaseType.SqlServer)
            {
                tableStatement = $"[dbo].{tableStatement}";
            }
            valuesStatement = valuesStatement.TrimEnd(',', '\r', '\n', ';');
            return dbType switch
            {
                var type when type == DatabaseType.Oracle || type == DatabaseType.Dm => $"BEGIN{Environment.NewLine}{Regex.Replace(Regex.Replace(valuesStatement, @"\(:", m => $"insert into {tableStatement}({columnsStatement}) values{m.Value}"), @"\),", m => $"{m.Value.TrimEnd(',')};")};{Environment.NewLine}END;",
                DatabaseType.MySql => $"SET foreign_key_checks=0;{Environment.NewLine}insert into {tableStatement}({columnsStatement}) values{Environment.NewLine}{valuesStatement};{Environment.NewLine}SET foreign_key_checks=1;",
                _ => $"insert into {tableStatement}({columnsStatement}) values{Environment.NewLine}{valuesStatement};"
            };
        }
        /// <summary>
        /// 获取批量删除的SQL语句
        /// </summary>
        /// <param name="table"></param>
        /// <param name="statement"></param>
        /// <param name="pks"></param>
        /// <param name="dbType"></param>
        /// <returns></returns>
        public static string GetBatchDeleteSql(string table, string statement, string[] pks, DatabaseType dbType)
        {
            if (string.IsNullOrWhiteSpace(statement))
            {
                return string.Empty;
            }
            if (pks.Length > 1)
            {
                return dbType switch {
                    var type when type == DatabaseType.Oracle || type == DatabaseType.Dm => $"BEGIN{Environment.NewLine}{statement}{Environment.NewLine}END;",
                    DatabaseType.MySql => $"SET foreign_key_checks=0;{Environment.NewLine}{statement}{Environment.NewLine}SET foreign_key_checks=1;",
                    _ => statement
                };
            }
            else
            {
                return dbType switch
                {
                    DatabaseType.MySql => $"SET foreign_key_checks=0;{Environment.NewLine}delete from {table} where {pks.First()} in({statement.Trim(',', '\r', '\n', ';')});{Environment.NewLine}SET foreign_key_checks=1;",
                    _ => $"delete from {table} where {pks.First()} in({statement.Trim(',', '\r', '\n', ';')})",
                };
            }
        }

        static readonly string[] _fieldTypesOfLongText = ["text", "clob"];
        static readonly string[] _fieldTypesOfBin = ["blob", "binary"];
        /// <summary>
        /// 获取创建表的字段声明部分
        /// </summary>
        /// <param name="colInfos"></param>
        /// <param name="dbType"></param>
        /// <param name="tableRecords"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static string GetColumnDefinitions(IEnumerable<ColumnInfo> colInfos, DatabaseType dbType, object? tableRecords = null)
        {
            // 处理一条数据
            StringBuilder columnBuilder = new();
            foreach (var colInfo in colInfos)
            {
                // Age
                var columnName = GetTableStatement(colInfo.ColumnName, dbType);
                var timeTypeDefine = colInfo.ColumnType ?? string.Empty;
                timeTypeDefine = dbType switch
                {
                    DatabaseType.MySql => $"datetime(6){Environment.NewLine}",
                    DatabaseType.Oracle => $"TIMESTAMP(6) WITH TIME ZONE{Environment.NewLine}",
                    DatabaseType.SqlServer => $"datetime2(7){Environment.NewLine}",
                    DatabaseType.Sqlite => $"TEXT default current_timestamp {Environment.NewLine}",
                    _ => $"datetime(6){Environment.NewLine}",
                };
                if (string.IsNullOrWhiteSpace(colInfo.ColumnType))
                {
                    throw new Exception($"字段{colInfo.ColumnName}的类型为空");
                }

                var columnTypeAndLength = colInfo.ColumnType switch
                {
                    // longtext字段
                    var type when type.ToLower().Contains("longtext") => $"longtext{Environment.NewLine}",
                    // 长整型字段
                    var type when RegexConst.ColumnTypeLong.IsMatch(type.ToLower()) => dbType switch
                    {
                        DatabaseType.Oracle => $"long{Environment.NewLine}",
                        _ => $"bigint{Environment.NewLine}"
                    },
                    // 整型字段
                    var type when RegexConst.ColumnTypeInt.IsMatch(type.ToLower()) => dbType switch
                    {
                        DatabaseType.Oracle => $"number",
                        _ => $"int{Environment.NewLine}"
                    },
                    // decimal字段
                    var type when "decimal".Contains(type.ToLower()) => dbType switch
                    {
                        DatabaseType.Oracle => "decimal(18, 2)",
                        DatabaseType.MySql => "decimal(18, 2)",
                        _ => "decimal"
                    },
                    var type when type.ToLower().Contains("date") => timeTypeDefine,
                    var type when type.ToLower().Contains("time") => timeTypeDefine,
                    // text clob 大文本字段
                    var type when _fieldTypesOfLongText.Any(x => type.ToLower().Contains(x)) => dbType switch
                    {
                        DatabaseType.Oracle => $"clob{Environment.NewLine}",
                        _ => $"text{Environment.NewLine}"
                    },
                    // 二进制字段
                    var type when _fieldTypesOfBin.Any(x => type.ToLower().Contains(x)) => dbType switch
                    {
                        DatabaseType.MySql => "blob",
                        DatabaseType.Oracle => "blob",
                        DatabaseType.SqlServer => "varbinary(max)",
                        _ => $"text{Environment.NewLine}"
                    },
                    var type when type.ToLower().Contains("varchar") && !string.IsNullOrWhiteSpace(colInfo.ColumnLength) && Convert.ToInt32(colInfo.ColumnLength.TrimStart('-')) > 0 => dbType switch
                    {
                        DatabaseType.Oracle => $"nvarchar2({GetStringColumnLengthBySourceData(colInfo.ColumnLength, colInfo.ColumnCode, tableRecords)}){Environment.NewLine}",
                        DatabaseType.MySql => $"varchar({GetStringColumnLengthBySourceData(colInfo.ColumnLength, colInfo.ColumnCode, tableRecords)}){Environment.NewLine}",
                        DatabaseType.SqlServer => $"nvarchar({GetStringColumnLengthBySourceData(colInfo.ColumnLength, colInfo.ColumnCode, tableRecords)}){Environment.NewLine}",
                        _ => $"TEXT{Environment.NewLine}"
                    },
                    // 普通字符串字段
                    _ => dbType switch
                    {
                        DatabaseType.Oracle => $"nvarchar2({colInfo.ColumnLength})",
                        DatabaseType.MySql => $"nvarchar({colInfo.ColumnLength}){Environment.NewLine}",
                        DatabaseType.Sqlite => $"TEXT{Environment.NewLine}",
                        _ => $"varchar({colInfo.ColumnLength}){Environment.NewLine}"
                    }
                };
                if (colInfo.IsPK == 1 && dbType == DatabaseType.SqlServer)
                {
                    columnTypeAndLength = $"columnTypeAndLength.TrimEnd('\r', '\n') NOT NULL PRIMARY KEY,{Environment.NewLine}";
                }
                columnBuilder.Append($"{columnName} {columnTypeAndLength},");
            }

            var columnsAssignment = columnBuilder.ToString().TrimEnd(',');

            // 如果单行的长度超过65535, 将字符集设置为utf8(比utf8mb4单个字符使用4个字节少一个字节)
            if (dbType == DatabaseType.MySql && Regex.Matches(columnsAssignment, @"varchar\((?<length>\d+)\)").Select(m => Convert.ToInt32(m.Groups["length"].Value) * 4).Sum() > 65535)
            {
                columnsAssignment = Regex.Replace(columnsAssignment, @"varchar\(\d+\)", m => $"{m.Value} CHARACTER SET utf8");
            }
            return columnsAssignment;
        }
        /// <summary>
        /// 获取SQL语句中带引号的数据库名, 表名, 字段
        /// </summary>
        /// <param name="name"></param>
        /// <param name="dbType"></param>
        /// <returns></returns>
        static string GetTableStatement(string name, DatabaseType dbType)
        {
            return dbType switch
            {
                DatabaseType.MySql => $"`{name}`",
                DatabaseType.Oracle => $"\"{name}\"".ToUpper(),
                DatabaseType.SqlServer => $"[{name}]",
                _ => name
            };
        }
        /// <summary>
        /// 获取SQL语句中带引号的数据库名, 表名, 字段
        /// </summary>
        /// <param name="names"></param>
        /// <param name="dbType"></param>
        /// <returns></returns>
        static IEnumerable<string> GetTableStatements(IEnumerable<string> names, DatabaseType dbType)
        {
            return dbType switch
            {
                DatabaseType.MySql => names.Select(name => $"`{name}`"),
                DatabaseType.Oracle => names.Select(name => $"\"{name}\""),
                DatabaseType.SqlServer => names.Select(name => $"[{name}]"),
                _ => names
            };
        }
        static int GetStringColumnLengthBySourceData(string? columnLength, string? columnCode, object? tableRecords)
        {
            if (string.IsNullOrWhiteSpace(columnLength))
            {
                columnLength = "0";
            }
            columnLength = columnLength.TrimStart('-');
            if (tableRecords is null)
            {
                return Convert.ToInt32(columnLength);
            }
            if (string.IsNullOrWhiteSpace(columnCode))
            {
                throw new ArgumentNullException(nameof(columnCode));
            }
            int length = Convert.ToInt32(columnLength);

            JArray? records = tableRecords is JArray array ? array : JArray.FromObject(tableRecords);
            if (records is null || !records.Any())
            {
                return length;
            }
            var max = records.Select(x => (x as JObject)?.Properties()?.FirstOrDefault(p => string.Equals(p.Name, columnCode, StringComparison.OrdinalIgnoreCase))?.Value?.ToString()?.Length ?? 0).Max();
            return CeilingColumnLength(max);
        }
        static int CeilingColumnLength(int number)
        {
            if (number <= 10)
            {
                return 20;
            }
            else if (number <= 50)
            {
                return 50;
            }
            else if (number <= 100)
            {
                return 100;
            }
            else if (number <= 255)
            {
                return 255;
            }
            else
            {
                var numberString = number.ToString();
                var hightBitVal = Convert.ToInt16(numberString[0].ToString());
                var result = $"{hightBitVal + 1}{new string('0', numberString.Length - 1)}";
                return Convert.ToInt32(result);
            }
        }

        /// <summary>
        /// 根据一条DataRow对象 生成对应的insert语句的值(参数)部分 @Name1,@Age1
        /// </summary>
        /// <param name="record"></param>
        /// <param name="colInfos"></param>
        /// <param name="parameters"></param>
        /// <param name="dbVarFlag"></param>
        /// <param name="random"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static string GenerateRecordValuesStatement(object record, IEnumerable<ColumnInfo> colInfos, Dictionary<string, object?> parameters, string dbVarFlag, int random = 0)
        {
            var valueBuilder = new StringBuilder();
            if (record is DataRow dataItem)
            {
                // 处理一条数据
                foreach (var colInfo in colInfos)
                {
                    var columnName = colInfo.ColumnName;
                    if (string.IsNullOrWhiteSpace(columnName))
                    {
                        throw new Exception($"包含空字段");
                    }
                    var columnValue = dataItem[columnName];
                    var columnType = colInfo.ColumnType ?? string.Empty;
                    var parameterName = $"{columnName}{random}";

                    valueBuilder.Append($"{dbVarFlag}{parameterName},");

                    parameters.Add(parameterName, GetColumnValue(columnValue, columnType));
                }
            }
            else
            {
                IEnumerable<JProperty> properties = record is JObject recordJObj ? recordJObj.Properties() : JObject.FromObject(record).Properties() ?? throw new Exception($"record不是DataRow或者记录对应的对象类型");
                foreach (var p in properties)
                {
                    if (p.Name == "NO")
                    {
                        continue;
                    }
                    var paramName = $"{p.Name}{random}";
                    valueBuilder.Append($"{dbVarFlag}{paramName},");

                    var col = colInfos.FirstOrDefault(x => string.Equals(x.ColumnCode, p.Name, StringComparison.OrdinalIgnoreCase));
                    dynamic? pVal = GetColumnValue(p, col);
                    parameters.Add(paramName, pVal);
                }
            }
            return valueBuilder.ToString().TrimEnd(',');
        }

        /// <summary>生成创建表的SQL语句</summary>
        public static string GenerateCreateTableSql(string tableName, DatabaseType dbType, IEnumerable<ColumnInfo> colInfos, object? tableRecords = null)
        {
            var columnsAssignment = GetColumnDefinitions(colInfos, dbType, tableRecords);

            var tableStatement = GetTableStatement(tableName, dbType);
            if (dbType == DatabaseType.SqlServer)
            {
                tableStatement = $"[dbo].{tableStatement}";
            }

            var columnPrimaryKeys = colInfos.Where(c => c.IsPK == 1).ToList();
            string primaryKeyAssignment = string.Empty;
            if (columnPrimaryKeys.Count > 0)
            {
                List<string> pks = [];
                foreach (var pk in columnPrimaryKeys)
                {
                    pks.Add($"{GetTableStatement(pk.ColumnCode, dbType)}");
                }

                var pksStatement = string.Join(',', pks);
                primaryKeyAssignment = dbType switch
                {
                    var type when type == DatabaseType.Oracle || type == DatabaseType.Dm => $", PRIMARY KEY ({pksStatement})",

                    DatabaseType.MySql => $", PRIMARY KEY ({pksStatement}) USING BTREE",

                    // sqlserver直接在字段声明语句后面指定了主键
                    _ => ""
                };
            }

            string createSql = dbType switch
            {
                DatabaseType.MySql => $@"CREATE TABLE {tableStatement} ({Environment.NewLine}{columnsAssignment}{Environment.NewLine}{primaryKeyAssignment}{Environment.NewLine}) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci ROW_FORMAT = Dynamic;",
                
                DatabaseType.Oracle => $@"CREATE TABLE {tableStatement} ({Environment.NewLine}{columnsAssignment}{Environment.NewLine}{primaryKeyAssignment})".ToUpper(),
                
                // sqlserver
                _ => $@"CREATE TABLE {tableStatement} ({Environment.NewLine}{columnsAssignment}{Environment.NewLine}){Environment.NewLine};{Environment.NewLine}{primaryKeyAssignment}"
            };
            return createSql;
        }

        /// <summary>
        /// 生成删除已存在的数据的SQL语句信息
        /// </summary>
        /// <param name="dataItem"></param>
        /// <param name="table"></param>
        /// <param name="primaryKeys"></param>
        /// <param name="targetPkValues"></param>
        /// <param name="parameters"></param>
        /// <param name="dbVarFlag"></param>
        /// <param name="random"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static string GenerateDeleteExistSql(DataRow dataItem, string table, string[] primaryKeys, string[] targetPkValues, Dictionary<string, object?> parameters, string dbVarFlag, int random = 0)
        {
            if (primaryKeys.Length == 0)
            {
                throw new Exception("主键不能为空");
            }
            if (primaryKeys.Length == 1)
            {
                string pk = primaryKeys.First();
                string pkValue = targetPkValues.First();
                string parameterName = $"{pk}{random}";
                parameters.Add(parameterName, pkValue);
                return $",{dbVarFlag}{parameterName}";
            }
            else
            {
                List<string> deleteConditions = [];
                for (int i = 0; i < primaryKeys.Length; i++)
                {
                    string pk = primaryKeys[i];
                    string pkValue = targetPkValues[i];
                    string parameterName = $"{pk}{random}";
                    parameters.Add(parameterName, pkValue);
                    deleteConditions.Add($"{pk}={dbVarFlag}{parameterName}");
                }
                string conditions = string.Join(" and ", deleteConditions);
                return $"delete from {table} where {conditions};{Environment.NewLine}";
            }
        }


        /// <summary>
        /// 根据数据库连接字符串判断数据库类型
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public static DatabaseType GetDbType(string connectionString)
        {
            var lowerConnStr = connectionString.ToLower();
            return lowerConnStr switch
            {
                var connStr when connStr.Contains("initial catalog", StringComparison.OrdinalIgnoreCase) => DatabaseType.SqlServer,
                var connStr when connStr.Contains("server", StringComparison.OrdinalIgnoreCase) => DatabaseType.MySql,
                var connStr when RegexConst.ConnectionStringSqlite.IsMatch(connStr) => DatabaseType.Sqlite,
                _ => DatabaseType.Oracle
            };
        }
        /// <summary>
        /// 根据数据库类型获取SQL语句中参数的标识符
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public static string GetDbParameterFlag(string connectionString) => GetDbType(connectionString) == DatabaseType.Oracle ? ":" : "@";
        /// <summary>
        /// 根据数据库类型获取SQL语句中参数的标识符
        /// </summary>
        /// <param name="dbType"></param>
        /// <returns></returns>
        public static string GetDbParameterFlag(DatabaseType dbType) => dbType == DatabaseType.Oracle ? ":" : "@";
        /// <summary>
        /// 获取数据源中一个表的数据的insert语句, 表不存在则生成创建表语句
        /// </summary>
        /// <param name="sourceTableName"></param>
        /// <param name="sourceConn"></param>
        /// <param name="colInfos"></param>
        /// <param name="primaryKeys"></param>
        /// <param name="trans"></param>
        /// <param name="targetConn"></param>
        /// <param name="targetDbType"></param>
        /// <param name="targetPkValues">目标表所有数据的主键值集合</param>
        /// <param name="targetTableStatement"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<TableSqlInfo> GetTableSqlInfosAsync(string sourceTableName, IDbConnection sourceConn, IEnumerable<ColumnInfo> colInfos, string[] primaryKeys, IDbTransaction? trans, IDbConnection targetConn, DatabaseType targetDbType, List<string> targetPkValues, string targetTableStatement, DataFilter? filter)
        {
            if (sourceTableName.StartsWith('_'))
            {
                yield break;
            }

            int pageIndex = 1;
            DataTable sourceDataTable = new(sourceTableName);

            
            #region 获取目标表的数据
            
            // : @
            var dbVarFlag = GetDbParameterFlag(targetConn.ConnectionString);
            
            // columnsStatement =           "Name, Age"
            string columnsStatement = GetFieldsStatement(colInfos, targetDbType);
            #endregion

            var sourceDbType = GetDbType(sourceConn.ConnectionString);
            var sourceTableStatement = GetTableStatement(sourceTableName, sourceDbType);
            while (true)
            {
                #region 获取源表数据
                try
                {
                    sourceDataTable.Clear();
                    var srouceTableDataReader = (await GetPagedDataAsync(sourceTableStatement, pageIndex, 3000, string.Join(',', primaryKeys), true, sourceConn, filter)).DataReader ?? throw new Exception($"源表{sourceTableName}的数据读取器为空");
                    sourceDataTable.Load(srouceTableDataReader);
                }
                catch (Exception ex)
                {
                    LoggerHelper.LogInformation($"查询数据出错sourceTable: {sourceTableName}");
                    LoggerHelper.LogInformation(ex.ToString());
                }
                if (sourceDataTable.Rows.Count == 0)
                {
                    yield break;
                }
                #endregion

                #region 对比原表和目标表数据, 目标表没有的数据就创建对应的insert语句
                var insertSqlBuilder = new StringBuilder();
                var deleteExistSqlBuilder = new StringBuilder();
                // sql语句参数
                Dictionary<string, object?> parameters = [];
                Dictionary<string, object?> deleteExistParameters = [];
                
                // 参数名后缀(第一条数据的Name字段, 参数名为Name1, 第二条为Name2, ...)
                var random = 1;
                
                // 为数据源中的每一条数据生成对应的insert语句的部分
                foreach (DataRow dataItem in sourceDataTable.Rows)
                {
                    var dataItemPkValues = primaryKeys.Select(x => dataItem[x].ToString()).ToArray();
                    string dataItemPkValue = string.Join(',', dataItemPkValues);
                    // 重复的数据先删除老的再插入新的达到更新的目的
                    if (targetPkValues.Count > 0 && targetPkValues.Contains(dataItemPkValue))
                    {
                        // ,@Id1
                        string deleteSqlCurrentRecordPart = GenerateDeleteExistSql(dataItem, targetTableStatement, primaryKeys, dataItemPkValues, deleteExistParameters, dbVarFlag, random);
                        deleteExistSqlBuilder.Append(deleteSqlCurrentRecordPart);
                    }

                    // "@Name1, :Age1"
                    string valuesStatement = GenerateRecordValuesStatement(dataItem, colInfos, parameters, dbVarFlag, random);
                    // ("@Name1, :Age1"),
                    string currentRecordSql = $"({valuesStatement}),{Environment.NewLine}";

                    insertSqlBuilder.Append($"{currentRecordSql}");
                    random++;
                }

                // 最终的insert语句
                var targetTableAllDataInsertSql = GetBatchInsertSql(sourceTableName, columnsStatement, insertSqlBuilder.ToString(), targetDbType);
                var targetTableDeleteExistSql = GetBatchDeleteSql(targetTableStatement, deleteExistSqlBuilder.ToString(), primaryKeys, targetDbType);
                #endregion
                yield return new TableSqlInfo
                {
                    TableName = sourceTableName,

                    BatchInsertSqlInfo = new SqlInfo()
                    {
                        Sql = targetTableAllDataInsertSql,
                        Parameters = parameters
                    },

                    DeleteExistSqlInfo = new SqlInfo()
                    {
                        Sql = targetTableDeleteExistSql,
                        Parameters = deleteExistParameters
                    }
                };
                pageIndex++;
            }
        }
        /// <summary>
        /// 获取表所有数据的主键值集合
        /// </summary>
        /// <param name="targetConn"></param>
        /// <param name="targetTable"></param>
        /// <param name="primaryKeys"></param>
        /// <param name="trans"></param>
        /// <returns></returns>
        public static async Task<List<string>> GetTableAllPkValuesAsync(IDbConnection targetConn, string targetTable, string[] primaryKeys, IDbTransaction? trans)
        {
            List<string> targetPkValues = [];
            try
            {
                // 超时数据库连接则会关闭
                using var reader = await targetConn.ExecuteReaderAsync($"select * from {targetTable}", param: new Dictionary<string, object>(), transaction: trans, commandTimeout: 60 * 60);
                while (reader.Read())
                {
                    string pkValue = string.Join(',', primaryKeys.Select(x => reader[x]));
                    targetPkValues.Add(pkValue);
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("doesn't exist", StringComparison.CurrentCultureIgnoreCase) || ex.Message.Contains("表或视图不存在") || ex.Message.Contains("table or view does not exist") || (ex.Message.Contains("对象名") && ex.Message.Contains("无效")))
                {
                    // 表不存在, 生成创建表的语句
                    targetPkValues = ["-1"];
                }
                else
                {
                    LoggerHelper.LogError($"读取表所有记录的主键值异常, 将忽略此异常:{ex}");
                }
            }
            return targetPkValues;
        }

        /// <summary>
        /// 将数据库中的数据和指定的数据集合进行对比
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="table"></param>
        /// <param name="sourceIdentityField"></param>
        /// <param name="targetIdentityField"></param>
        /// <param name="targetJsonInfo"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task<DataComparedResult> CompareRecordsFromDbWithDataAsync(IDbConnection conn, string table, string sourceIdentityField, string targetIdentityField, DataInJson targetJsonInfo)
        {
            var sourceRecordsDynamic = await conn.QueryAsync($"select * from {table}");
            var sourceRecords = sourceRecordsDynamic.Select(x => JObject.FromObject(x) ?? throw new Exception("数据库数据异常, 数据转换失败")).ToList();
            var targetRecords = FileHelper.GetRecords(targetJsonInfo.FilePath, targetJsonInfo.DataFields);
            return await CompareRecordsAsync(sourceRecords, targetRecords, [], sourceIdentityField, targetIdentityField);
        }

        /// <summary>
        /// **对比数据**
        /// </summary>
        /// <param name="sourceRecordsData">源数据</param>
        /// <param name="targetRecordsData">要对比的目标数据表中查询出的数据</param>
        /// <param name="ignoreFields"></param>
        /// <param name="sourceIdField">源数据中的标识字段</param>
        /// <param name="targetIdField">目标数据中的标识字段</param>
        /// <returns></returns>
        public static async Task<DataComparedResult> CompareRecordsAsync(IEnumerable<object> sourceRecordsData, IEnumerable<object> targetRecordsData, string[] ignoreFields, string sourceIdField, string targetIdField)
        {
            #region 先将集合转换为JArray类型; 处理DataRow对象
            List<JObject> sourceArray = [];
            List<JObject> targetArray = [];
            if (sourceRecordsData is not null && sourceRecordsData.Any())
            {
                var first = sourceRecordsData.First();
                // BOOKMARK: DataRow是一个非常复杂的对象; 如果DataRow集合直接转换为JArray(DataRow直接转换为JObject)会非常耗时非常占用内存(几十个字段的数据几万条占用几十G内存); 提取数据转为字典几万数据只要几秒便可完成
                if (first is DataRow firstRow)
                {
                    var columns = firstRow.Table.Columns;
                    foreach (DataRow record in sourceRecordsData.Cast<DataRow>())
                    {
                        Dictionary<string, object> recordDictionary = [];
                        foreach (DataColumn column in columns)
                        {
                            recordDictionary[column.ColumnName] = record[column];
                        }
                        sourceArray.Add(JObject.FromObject(recordDictionary));
                    }
                }
                else
                {
                    sourceArray = sourceRecordsData.Select(JObject.FromObject).ToList();
                }
            }
            if (targetRecordsData is not null && targetRecordsData.Any())
            {
                var firstTarget = targetRecordsData.First();
                // BOOKMARK: DataRow是一个非常复杂的对象; 如果DataRow集合直接转换为JArray(DataRow直接转换为JObject)会非常耗时非常占用内存(几十个字段的数据几万条占用几十G内存); 提取数据转为字典几万数据只要几秒便可完成
                if (firstTarget is DataRow firstTargetRow)
                {
                    var columns = firstTargetRow.Table.Columns;
                    foreach (DataRow record in targetRecordsData.Cast<DataRow>())
                    {
                        Dictionary<string, object> recordDictionary = [];
                        foreach (DataColumn column in columns)
                        {
                            recordDictionary[column.ColumnName] = record[column];
                        }
                        targetArray.Add(JObject.FromObject(recordDictionary));
                    }
                }
                else
                {
                    targetArray = targetRecordsData.Select(JObject.FromObject).ToList();
                }
            }

            if (sourceArray.Count == 0 && targetArray.Count == 0)
            {
                return new DataComparedResult();
            }

            if (sourceArray.Count == 0 && targetArray.Count != 0)
            {
                return new DataComparedResult { ExistInTargetOnly = JArray.FromObject(targetArray) };
            }

            if (sourceArray.Count != 0 && targetArray.Count == 0)
            {
                return new DataComparedResult { ExistInSourceOnly = JArray.FromObject(sourceArray) };
            }
            #endregion

            #region 参数校验
            if (sourceIdField.Count(x => x == ',') != targetIdField.Count(x => x == ','))
            {
                throw new Exception($"提供Source的主键字段{sourceIdField}与Target的主键字段{targetIdField}无法一一对应");
            }

            if (sourceArray is null)
            {
                throw new Exception("对比数据源数据集为空");
            }
            if (targetArray is null)
            {
                throw new Exception("对比数据目标数据集为空");
            }
            #endregion

            #region 获取主键字段
            var sourcePrimaryKeys = GetPrimaryKeys(sourceArray.FirstOrDefault(), sourceIdField);
            var targetPrimaryKeys = GetPrimaryKeys(targetArray.FirstOrDefault(), targetIdField);
            if (sourcePrimaryKeys.Count == 0 || targetPrimaryKeys.Count == 0)
            {
                throw new Exception("主键字段不能为空");
            }
            if (sourcePrimaryKeys.Count != targetPrimaryKeys.Count)
            {
                throw new Exception($"解析后的主键字段无法一一对应");
            }
            #endregion

            #region 定义线程安全的数据容器和其他变量
            // 假设Target中都是全新数据
            var existInSourceOnly = new List<JObject>();
            existInSourceOnly.AddRange(sourceArray);

            // 遍历Target, Source中没有找到的项目
            ConcurrentBag<JObject> existInTarget = [];
            // Source找到了 - 数据变化了
            ConcurrentBag<ChangedRecord> changed = [];
            // Source找到了 - 数据变化了
            ConcurrentBag<ChangedRecord> notChanged = [];
            #endregion

            #region 调用CPU密集型任务帮助类进行多线程分批对比数据
            await BatchesScheme.CpuTasksExecuteAsync(targetArray, CompareBatchData);
            #endregion

            #region 构建数据对比结果并返回

            // 对比结果
            DataComparedResult compareResult = new()
            {
                ExistInTargetOnly = JArray.FromObject(existInTarget),
                Changed = [.. changed],
                NotChanged = [.. notChanged]
            };

            // 计算对比结果中的ExistInSourceOnly: Source中出去变化的和没变化的(Souce中找到的)即Source中独有的数据
            compareResult.Changed.ForEach(x => existInSourceOnly.Remove(x.SourceRecord));
            compareResult.NotChanged.ForEach(x => existInSourceOnly.Remove(x.SourceRecord));
            if (existInSourceOnly.Count != 0)
            {
                existInSourceOnly.ForEach(compareResult.ExistInSourceOnly.Add);
            }

            // 返回
            return compareResult;
            #endregion

            #region 本地方法, 数据对比逻辑
            void CompareBatchData(IEnumerable<object> batchData)
            {
                var start = DateTime.Now;
                foreach (var targetBatchItem in batchData.Cast<JObject>())
                {
                    JObject? existedSourceRecord = null;
                    Dictionary<string, string> targetIdValues = [];
                    // target 主键值; 支持多个
                    foreach (var targetPk in targetPrimaryKeys)
                    {
                        targetIdValues.Add(targetPk, targetBatchItem.Properties().FirstOrDefault(x => string.Equals(targetPk, x.Name, StringComparison.OrdinalIgnoreCase))?.Value?.ToString() ?? throw new Exception($"查找ID失败"));
                    }

                    #region 数据源中查找对应的数据
                    foreach (var sourceRecord in sourceArray)
                    {
                        // 获取所有主键字段的值
                        Dictionary<string, string> sourceIdValues = [];
                        foreach (var sourcePrimaryKey in sourcePrimaryKeys)
                        {
                            var sourcePkValue = sourceRecord.Properties().FirstOrDefault(x => string.Equals(sourcePrimaryKey, x.Name, StringComparison.OrdinalIgnoreCase))?.Value?.ToString() ?? throw new Exception($"查找ID失败");
                            sourceIdValues.Add(sourcePrimaryKey, sourcePkValue);
                        }

                        // 比较是否所有主键字段值都相同
                        bool allPksHasSameValue = true;
                        foreach (var sourceIdValue in sourceIdValues)
                        {
                            if ($"{sourceIdValue.Value}" != $"{targetIdValues[sourceIdValue.Key]}")
                            {
                                allPksHasSameValue = false;
                                break;
                            }
                        }

                        // 都相同表示同一条数据, Source中找到数据, 查找数据结束
                        if (allPksHasSameValue)
                        {
                            existedSourceRecord = sourceRecord;
                            break;
                        }
                    }
                    #endregion

                    // 数据源中没有当前数据
                    if (existedSourceRecord is null)
                    {
                        existInTarget.Add(targetBatchItem);
                    }
                    else
                    {
                        var existedSourceProps = existedSourceRecord.Properties();
                        var existedTargetProps = targetBatchItem.Properties();

                        #region 比较每个字段的值, 判断同一条数据是否发生了变化
                        bool hasChanged = false;
                        foreach (var sourceProp in existedSourceProps)
                        {
                            // 忽略字段不比较
                            if (ignoreFields.Any() && ignoreFields.Any(x => string.Equals(x, sourceProp.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                continue;
                            }
                            var targetProp = existedTargetProps.FirstOrDefault(p => string.Equals(p.Name, sourceProp.Name, StringComparison.OrdinalIgnoreCase));
                            if (targetProp is not null)
                            {
                                if (sourceProp.Value.StringValueNotEquals(targetProp.Value)) // 当前字段(key)的值不相同(至少有一个不为null且不为空字符串 并且两个字段值不相等)
                                {
                                    changed.Add(new ChangedRecord { SourceRecord = existedSourceRecord, TargetRecord = targetBatchItem });
                                    hasChanged = true;
                                    break;
                                }
                            }
                        }
                        if (!hasChanged)
                        {
                            notChanged.Add(new ChangedRecord { SourceRecord = existedSourceRecord, TargetRecord = targetBatchItem });
                        }
                        #endregion
                    }
                }
                var end = DateTime.Now;
                LoggerHelper.LogInformation($"数据对比: 已经处理了{batchData.Count()}; 耗时: {(end - start).TotalSeconds}/s{Environment.NewLine}");
            }
            #endregion
        }

        /// <summary>
        /// 描述处于json文件中的数据
        /// </summary>
        public class DataInJson(string filePath, string[] dataFileds)
        {
            /// <summary>
            /// json文件路径
            /// </summary>
            public string FilePath { get; set; } = filePath;
            /// <summary>
            /// 数据包含的字段
            /// </summary>
            public string[] DataFields { get; set; } = dataFileds;
        }
        /// <summary>
        /// 数据对比结果对象
        /// </summary>
        public class DataComparedResult
        {
            /// <summary>
            /// 仅存在于Source中
            /// </summary>
            public JArray ExistInSourceOnly { get; set; } = [];
            /// <summary>
            /// 仅存在于Target中
            /// </summary>
            public JArray ExistInTargetOnly { get; set; } = [];
            /// <summary>
            /// Source和Target中存在, 改变了的数据
            /// </summary>
            public List<ChangedRecord> Changed { get; set; } = [];
            /// <summary>
            /// Source和Target中存在, 没有发生改变的数据
            /// </summary>
            public List<ChangedRecord> NotChanged { get; set; } = [];
        }
        /// <summary>
        /// 改变了的数据
        /// </summary>
        public class ChangedRecord
        {
            /// <summary>
            /// Source中的值
            /// </summary>
            public JObject SourceRecord { get; set; } = [];
            /// <summary>
            /// Target中的值
            /// </summary>
            public JObject TargetRecord { get; set; } = [];
        }
        /// <summary>
        /// 获取数据表的字段信息
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public async Task<IEnumerable<ColumnInfo>> GetTableColumnsInfoAsync(string tableName)
        {
            using var conn = GetDbConnection(_connectionString);
            return await GetTableColumnsInfoAsync(conn, tableName);
        }
        /// <summary>
        /// 获取数据库表结构
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<ColumnInfo>> GetTableColumnsInfoAsync(IDbConnection conn, string tableName)
        {
            string sql;
            // TODO: 数据库类型 作为公共属性
            var dbType = GetDbType(conn.ConnectionString);
            if (dbType == DatabaseType.SqlServer)
            {
                sql = $@"SELECT
                                  C.name 										as [ColumnCode]
                                 ,C.name 										as [ColumnName]
                                 ,T.name 										as [ColumnType]
                                 ,COLUMNPROPERTY(C.id,C.name,'PRECISION') 		as [ColumnLength]
                                 ,'textbox' as [ControlType] 
                                 ,convert(bit,case when exists(SELECT 1 FROM sysobjects where xtype='PK' and parent_obj=c.id and name in (
                                     SELECT name FROM sysindexes WHERE indid in(
                                         SELECT indid FROM sysindexkeys WHERE id = c.id AND colid=c.colid))) then 1 else 0 end) 
                                             									as [IsPK]
                                 ,convert(bit,C.IsNullable)  					as [IsNullable]
                                 ,ISNULL(CM.text,'') 							as [DefaultValue]
								 ,C.colid										as [OrderNo]
                            FROM syscolumns C
                            INNER JOIN systypes T ON C.xusertype = T.xusertype 
                            left JOIN sys.extended_properties ETP   ON  ETP.major_id = c.id AND ETP.minor_id = C.colid AND ETP.name ='MS_Description' 
                            left join syscomments CM on C.cdefault=CM.id
                            WHERE C.id = object_id('{tableName}')";
            }
            //else if (dbType == DatabaseType.Dm)
            //{
            //    sql = $@" SELECT
            //                            column_name      ColumnCode,
            //                            column_name      ColumnName,
            //                            data_type        ColumnType,
            //                            (case data_type when 'NVARCHAR2' then to_char(DATA_LENGTH) when 'NUMBER' then DATA_PRECISION||','||DATA_SCALE else to_char(data_length) end)      ColumnLength,
            //                            'textbox'          ControlType,
            //                            0                  IsPK, 
            //                            CASE nullable 
            //                                WHEN 'N' then 0
            //                                ELSE 1
            //                                END  IsEmpty,
            //                            Data_default     DefaultValue
            //                    from  all_tab_columns
            //                    WHERE owner='{sqlExecute.DbConnection.Database}' and table_name='{table}'
            //                    ORDER BY  COLUMN_ID ";
            //}
            else if (dbType == DatabaseType.MySql)
            {
                sql = $@"select
                        column_name 									ColumnCode,
                        column_name 									ColumnName,
                        data_type 										ColumnType,
                        CASE data_type
                        WHEN 'varchar' THEN
	                        character_maximum_length
                      	WHEN 'nvarchar' THEN
                          character_maximum_length
                        WHEN 'bit' THEN
                          1
                        WHEN 'int' THEN
                          numeric_precision
                        WHEN 'datetime' THEN
                          datetime_precision
                        ELSE
	                        0
                        END 											ColumnLength,
                        CASE is_nullable  WHEN 'NO' then 0 ELSE 1 END  	IsNullable,
						CASE WHEN column_key='PRI' THEN 1 ELSE 0 END 	IsPK,
                        column_default 									DefaultValue,
						ORDINAL_POSITION 								OrderNo
                    from information_schema.columns where table_schema = '{conn.Database}' and table_name = '{tableName}' 
                    order by ORDINAL_POSITION asc";
            }
            else
            {
                sql = $@" SELECT 
                                    A.column_name       ColumnCode,
                                    A.column_name       ColumnName,
                                    A.data_type         ColumnType,
                                    (case A.data_type when 'NVARCHAR2' then to_char(A.CHAR_LENGTH) when 'NUMBER' then A.DATA_PRECISION||','||A.DATA_SCALE else to_char(A.data_length) end)      ColumnLength,
                                    CASE WHEN A.COLUMN_ID=1 THEN 1 ELSE 0 END IsPK, 
                                    CASE A.nullable 
                                        WHEN 'N' then 0
                                        ELSE 1
                                        END  IsNullable,
                                    A.Data_default      DefaultValue,
									A.COLUMN_ID		    OrderNo,
									B.comments		    Remark
                            from  user_tab_columns A,user_col_comments B
                            WHERE a.COLUMN_NAME=b.column_name and A.Table_Name = B.Table_Name and 
                                A.Table_Name=upper('{tableName}')
                            ORDER BY  A.COLUMN_ID ";
            }

            var result = await conn.QueryAsync<ColumnInfo>(sql);
            if (!result.Any())
            {
                var db = string.IsNullOrWhiteSpace(conn.Database) ? Regex.Match(conn.ConnectionString, @"(?<=user\s+id=).*?(?=;)", RegexOptions.IgnoreCase).Value : conn.Database;
                throw new Exception($"{db}中没有找到{tableName}表");
            }
            // ColumnLength: Oracle如果是Number类型, ColumnLength值可能是"10,0"
            AnalysisColumnCSharpType(result);
            return result;
        }

        /// <summary>
        /// 根据字段(ColumnInfo)的数据库类型判断CSharp类型
        /// </summary>
        /// <param name="columnInfos"></param>
        private static void AnalysisColumnCSharpType(IEnumerable<ColumnInfo> columnInfos)
        {
            foreach (var col in columnInfos)
            {
                col.ColumnCSharpType = col.ColumnType switch
                {
                    _ when string.IsNullOrWhiteSpace(col.ColumnType) => "",
                    _ when col.ColumnType.Contains("time", StringComparison.OrdinalIgnoreCase) => "DateTime",
                    _ when col.ColumnType.Contains("bit", StringComparison.OrdinalIgnoreCase) => "bool",
                    _ when col.ColumnType.Contains("int", StringComparison.OrdinalIgnoreCase) => "int",
                    _ when col.ColumnType.Contains("lob", StringComparison.OrdinalIgnoreCase) => "byte[]",
                    _ => "string",
                };
            }
        }
        /// <summary>
        /// 执行查询
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        public static async Task<DataTable> QueryAsync(IDbConnection conn, string sql, object parameters, IDbTransaction? transaction)
        {
            var dataTable = new DataTable();
            var reader = await conn.ExecuteReaderAsync(sql, param: parameters, transaction: transaction);
            dataTable.Load(reader);
            return dataTable;
        }
        /// <summary>
        /// 是否已经存在于Target中
        /// </summary>
        /// <param name="rows"></param>
        /// <param name="sourceRow"></param>
        /// <param name="primaryKeys"></param>
        /// <returns></returns>
        public static bool AlreadyExistInTarget(DataRowCollection rows, DataRow sourceRow, string?[] primaryKeys)
        {
            foreach (DataRow row in rows)
            {
                if (primaryKeys is not null && primaryKeys.Any())
                {
                    var exist = true;
                    foreach (var pk in primaryKeys)
                    {
                        if (string.IsNullOrWhiteSpace(pk) || row[pk].ToString() != sourceRow[pk].ToString())
                        {
                            exist = false;
                            break;
                        }
                    }
                    if (exist)
                    {
                        return true;
                    }
                }
                // 比较第一列(Id)的值
                else if (row[0].ToString() == sourceRow[0].ToString())
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 是否已经存在于Target中
        /// </summary>
        /// <param name="rows"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public static bool AlreadyExistInTarget(DataRowCollection rows, JObject source)
        {
            var sourceProperties = source.Properties();
            var colName = rows[0].Table.Columns[0].ColumnName;
            foreach (DataRow row in rows)
            {
                // 比较第一列的值(Id)
                if (row[0].ToString() == sourceProperties.FirstOrDefault(x => string.Equals(x.Name, colName, StringComparison.OrdinalIgnoreCase))?.Value?.ToString())
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>获取需要同步的DataColumn和DataRow</summary>
        public static SyncData GetSyncData(DataTable sourceDataTable, DataTable targetDataTable)
        {
            var columnNames = new List<string>();
            var sourceColumns = sourceDataTable.Columns;
            var targetColumns = targetDataTable.Columns;

            var insertingColumns = new List<DataColumn>();
            var deletingColumns = new List<DataColumn>();
            var targetColumnsCopied = new DataColumn[targetColumns.Count];
            targetColumns.CopyTo(targetColumnsCopied, 0);
            var targetColumnsCopiedList = targetColumnsCopied.ToList();
            foreach (DataColumn sourceColumn in sourceColumns)
            {
                columnNames.Add(sourceColumn.ColumnName);
                DataColumn? targetCorrespondColumn = null;
                foreach (DataColumn targetColumn in targetColumns)
                {
                    if (targetColumn.ColumnName == sourceColumn.ColumnName)
                    {
                        targetCorrespondColumn = targetColumn;
                        break;
                    }
                }

                if (targetCorrespondColumn is not null)
                {
                    targetColumnsCopiedList.RemoveAll(col => col.ColumnName == targetCorrespondColumn.ColumnName);
                    if (sourceColumn.DataType.Name != targetCorrespondColumn.DataType.Name)
                    {
                        deletingColumns.Add(targetCorrespondColumn);
                        insertingColumns.Add(sourceColumn);
                    }
                }
                else
                {
                    insertingColumns.Add(sourceColumn);
                }
            }
            deletingColumns.AddRange(targetColumnsCopiedList);

            var sourceRows = sourceDataTable.Rows;
            var targetRows = targetDataTable.Rows;
            var insertingRows = new List<DataRow>();
            var updatingRows = new List<DataRow>();
            var targetRowsCopied = new DataRow[targetRows.Count];
            sourceRows.CopyTo(targetRowsCopied, 0);
            var deletingRows = targetRowsCopied.ToList();
            foreach (DataRow sourceRow in sourceRows)
            {
                DataRow? targetCorrespondRow = null;
                foreach (DataRow targetRow in targetRowsCopied)
                {
                    if (sourceRow[0].ToString() == targetRow[0].ToString())
                    {
                        targetCorrespondRow = sourceRow;
                    }
                }
                if (targetCorrespondRow is not null)
                {
                    deletingRows.Remove(targetCorrespondRow);
                    if (deletingColumns.Any())
                    {
                        updatingRows.Add(sourceRow);
                    }
                    else
                    {
                        foreach (DataColumn sourceCol in sourceColumns)
                        {
                            if (sourceCol.DataType == typeof(byte[]) && (sourceRow[sourceCol.ColumnName] as byte[])?.Length != (targetCorrespondRow[sourceCol.ColumnName] as byte[])?.Length)
                            {
                                updatingRows.Add(sourceRow);
                            }
                            else if (sourceRow[sourceCol.ColumnName].ToString() != targetCorrespondRow[sourceCol.ColumnName].ToString())
                            {
                                updatingRows.Add(sourceRow);
                            }
                        }
                    }
                }
                else
                {
                    insertingRows.Add(sourceRow);
                }
            }

            return new SyncData(deletingColumns, insertingColumns, insertingRows, updatingRows, deletingRows);
        }
        /// <summary>
        /// 数据脱敏
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="table"></param>
        /// <param name="fields"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task<int> DesensitizeAsync(string connectionString, string table, IEnumerable<string> fields)
        {
            int affectedRows = 0;

            var dbType = GetDbType(connectionString);
            var varFlag = GetDbParameterFlag(dbType);
            using var conn = GetDbConnection(connectionString);
            conn.Open();
            using var connUpdate = GetDbConnection(connectionString);
            connUpdate.Open();

            var cols = await GetTableColumnsInfoAsync(conn, table);
            var idField = cols.FirstOrDefault(x => x.IsPK == 1);
            var idFieldName = idField is null ? "id" : idField.ColumnCode;

            fields = fields.Select(x => x.ToLower());
            var desensitizeCols = cols.Where(x => !string.IsNullOrWhiteSpace(x.ColumnCode) && fields.Contains(x.ColumnCode.ToLower())).ToList();
            if (desensitizeCols.Any())
            {
                var records = await conn.QueryAsync($"select * from {table}");
                foreach (var recordObj in records)
                {
                    JObject record = JObject.FromObject(recordObj);
                    var properties = record.Properties();
                    var idProp = properties.FirstOrDefault(x => x.Name.Equals(idFieldName, StringComparison.OrdinalIgnoreCase)) ?? throw new Exception($"无法找到表{table}主键");

                    var parameters = new Dictionary<string, object>
                    {
                        { "id", idProp.Value.ToString() }
                    };

                    var updateBuiler = new StringBuilder();
                    foreach (var col in desensitizeCols)
                    {
                        var p = properties.FirstOrDefault(x => x.Name.Equals(col.ColumnCode, StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrWhiteSpace(col.ColumnCode) && p is not null)
                        {
                            var val = p.Value.ToString();
                            if (!string.IsNullOrWhiteSpace(val))
                            {
                                string newVal = "***";
                                if (val.Length > 1)
                                {
                                    var hiddenIndex = (int)Math.Ceiling(val.Length / 2.0);
                                    newVal = val.StartsWith("***") ? val : "***" + val[hiddenIndex..];
                                }

                                parameters.Add(col.ColumnCode, newVal);
                                updateBuiler.Append($"{col.ColumnCode}={varFlag}{col.ColumnCode},");
                            }
                        }
                    }
                    var updateStatement = updateBuiler.ToString().TrimEnd(',');

                    string updateSql = $"update {table} set {updateStatement} where {idFieldName}={varFlag}id";
                    affectedRows += await connUpdate.ExecuteAsync(updateSql, parameters);
                }
            }
            conn.Close();
            connUpdate.Close();
            return affectedRows;
        }

        /// <summary>
        /// 将表和字段名改为大写
        /// </summary>
        /// <param name="connStr"></param>
        /// <param name="database"></param>
        /// <param name="table"></param>
        /// <returns></returns>
        public static async Task<string> UpperTableAndColumnNames(string connStr, string database, string table)
        {
            string sql = $"""
                DECLARE  
                  v_table_name VARCHAR2(100) := '{table}'; -- 替换为你的表名  
                  v_owner      VARCHAR2(100) := '{database}'; -- 替换为你的模式名  
                  v_new_table_name VARCHAR2(100); 
                BEGIN
                  -- 检查表名是否是小写并转换为大写
                  SELECT DECODE(UPPER(table_name), table_name, NULL, UPPER(table_name))
                  INTO v_new_table_name  
                  FROM all_tables  
                  WHERE table_name = v_table_name AND owner = v_owner AND table_name <> UPPER(table_name);


                  IF v_new_table_name IS NOT NULL THEN  
                    -- DBMS_OUTPUT.PUT_LINE('ALTER TABLE ' || v_owner || '.' || v_table_name || ' RENAME TO ' || v_new_table_name);
                    EXECUTE IMMEDIATE 'ALTER TABLE ' || v_owner || '.' || '"' || v_table_name || '"' || ' RENAME TO ' || v_new_table_name;  
                    DBMS_OUTPUT.PUT_LINE('Renamed table ' || v_owner || '.' || v_table_name || ' to ' || v_new_table_name);  
                    v_table_name := v_new_table_name; -- 更新表名为新的大写名称
                  ELSE
                    DBMS_OUTPUT.PUT_LINE('Table ' || v_owner || '.' || v_table_name || ' is already uppercase or not created with quotes.');  
                  END IF; 

                  FOR col IN (SELECT column_name, table_name, owner FROM all_tab_columns WHERE table_name = v_table_name AND owner = v_owner) LOOP  
                    -- 检查字段名是否已经是大写（精确匹配）  
                    IF col.column_name <> upper(col.column_name) THEN  
                      EXECUTE IMMEDIATE 'ALTER TABLE ' || col.owner || '.' || col.table_name || ' RENAME COLUMN "' || col.column_name || '" TO ' || '"' || upper(col.column_name) || '"';  
                      DBMS_OUTPUT.PUT_LINE('Renamed column ' || col.owner || '.' || col.table_name || '.' || col.column_name || ' to ' || upper(col.column_name));  
                    ELSE  
                      DBMS_OUTPUT.PUT_LINE('Column ' || col.owner || '.' || col.table_name || '.' || col.column_name || ' is already uppercase.');  
                    END IF;  
                  END LOOP;  
                END;
                """;
            var conn = GetDbConnection(connStr);
            var res = await conn.ExecuteAsync(sql);
            return res.ToString();
        }
    }

    /// <summary>
    /// 同步的数据对象
    /// </summary>
    public class SyncData(List<DataColumn> deletingColumns, List<DataColumn> insertingColumns, List<DataRow> insertingRows, List<DataRow> updatingRows, List<DataRow> deletingRows)
    {
        /// <summary>
        /// 删除的字段集合
        /// </summary>
        public List<DataColumn> DeletingColumns { get; set; } = deletingColumns ?? [];
        /// <summary>
        /// 插入的字段集合
        /// </summary>
        public List<DataColumn> InsertingColumns { get; set; } = insertingColumns;
        /// <summary>
        /// 插入的DataRow集合
        /// </summary>
        public List<DataRow> InsertingRows { get; set; } = insertingRows;
        /// <summary>
        /// 更新的DataRow集合
        /// </summary>
        public List<DataRow> UpdatingRows { get; set; } = updatingRows;
        /// <summary>
        /// 删除的DataRow集合
        /// </summary>
        public List<DataRow> DeletingRows { get; set; } = deletingRows;
    }
}
