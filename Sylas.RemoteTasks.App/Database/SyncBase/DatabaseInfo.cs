//using MySqlConnector;
//using Microsoft.Data.SqlClient;
using Dapper;
using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI.Relational;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oracle.ManagedDataAccess.Client;
using Sylas.RemoteTasks.App.RegexExp;
using Sylas.RemoteTasks.App.Utils;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Text.RegularExpressions;
using static Sylas.RemoteTasks.App.RemoteHostModule.StartupHelper;

namespace Sylas.RemoteTasks.App.Database.SyncBase
{
    #region TODO
    // ...实现从任意数据库 生成创建MySQL表SQL(字段类型)
    // 1. 实现任意数据库到任意数据库的新增同步
    // 2. 实现对比数据进行新增,更新或删除(已经实现GetSyncData)
    // 3. 尝试重构
    #endregion

    public class DatabaseInfoFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public DatabaseInfoFactory(IServiceScopeFactory serviceScopeFactory, IServiceProvider serviceProvider)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _serviceProvider = serviceProvider;
        }

        public DatabaseInfo Create(string connectionString)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var databaseInfo = scope.ServiceProvider.GetRequiredService<DatabaseInfo>();
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                databaseInfo.ChangeDatabase(connectionString);
            }
            return databaseInfo;
        }

        public DatabaseInfo Clone()
        {
            var scopedDatabaseInfo = _serviceProvider.GetRequiredService<DatabaseInfo>();

            using var scope = _serviceScopeFactory.CreateScope();
            var databaseInfo = scope.ServiceProvider.GetRequiredService<DatabaseInfo>();
            // 新对象同步线程内对象的ConnectionString配置
            databaseInfo.ChangeDatabase(scopedDatabaseInfo);
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

        public DatabaseInfo(ILogger<DatabaseInfo> logger, IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("Default") ?? throw new Exception("DatabaseInfo: 数据库连接字符串为空");
            _logger = logger;
            _logger.LogCritical($"DatabaseInfo initialized");
            _dbType = GetDbType(_connectionString);
            _varFlag = GetDbParameterFlag(_dbType);
        }
        public void ChangeDatabase(IDbConnection conn, string db)
        {
            if (conn.State == ConnectionState.Closed)
            {
                conn.Open();
            }
            if (conn.Database != db)
            {
                conn.ChangeDatabase(db);
            }
        }
        public void ChangeDatabase(string connectionString)
        {
            _connectionString = connectionString;
            _dbType = GetDbType(_connectionString);
            _varFlag = GetDbParameterFlag(_dbType);
        }
        public void ChangeDatabase(DatabaseInfo databaseInfo)
        {
            ChangeDatabase(databaseInfo._connectionString);
        }
        #region 数据库连接对象
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
        private static IDbConnection GetOracleConnection(string host, string port, string instanceName, string username, string password) => new OracleConnection($"Data Source={host}:{port}/{instanceName};User ID={username};Password={password};PERSIST SECURITY INFO=True;Pooling = True;Max Pool Size = 100;Min Pool Size = 1;");
        private static IDbConnection GetMySqlConnection(string host, string port, string db, string username, string password) => new MySqlConnection($"Server={host};Port={port};Stmt=;Database={db};Uid={username};Pwd={password};Allow User Variables=true;");
        private static IDbConnection GetSqlServerConnection(string host, string port, string db, string username, string password) => new SqlConnection($"User ID={username};Password={password};Initial Catalog={db};Data Source={host},{(string.IsNullOrWhiteSpace(port) ? "1433" : port)}");
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
            using var conn = GetDbConnection(_connectionString);
            if (!string.IsNullOrWhiteSpace(db))
            {
                conn.ChangeDatabase(db);
            }

            var dbType = GetDbType(conn.ConnectionString);

            string sql = GetPagedSql(conn.Database, table, dbType, pageIndex, pageSize, orderField, isAsc, filters, out string condition, out Dictionary<string, object> parameters);

            string allCountSqlTxt = $"select count(*) from {table} where 1=1 {condition}";
            
            var allCount = await conn.ExecuteScalarAsync<int>(allCountSqlTxt, parameters);
            Console.WriteLine(sql);
            var data = await conn.QueryAsync<T>(sql, parameters);

            return new PagedData<T>  { Data = data, Count = allCount, TotalPages = (allCount + pageSize - 1) / pageSize };

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
        /// 执行增删改的SQL语句 - 可使用db参数指定切换到当前连接的用户有权限的其他数据库
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
            _logger.LogDebug(sql);
            return await conn.ExecuteAsync(sql, parameters);
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
            return await ExecuteScalarAsync(sql, parameters);
        }



        public async Task SyncDatabaseAsync(string sourceConnectionString, string targetConnectionString, string sourceSyncedDb = "")
        {
            await SyncDatabaseByConnectionStringsAsync(sourceConnectionString, targetConnectionString, sourceSyncedDb);
        }
        public async Task CreateTableIfNotExistAsync(string db, string tableName, IEnumerable<ColumnInfo> colInfos, object? tableRecords = null)
        {
            using var conn = GetDbConnection(_connectionString);
            conn.Open();
            ChangeDatabase(conn, db);
            try
            {
                var targetDataTable = await QueryAsync(conn, $"select * from {tableName}", new Dictionary<string, object>(), null);
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
        private static async Task SyncDatabaseByConnectionStringsAsync(string sourceConnectionString, string targetConnectionString, string sourceSyncedDb = "")
        {
            // TODO: 1.同步指定的表 2.不指定要同步的数据库sourceSyncedDb, 从数据库连接字符串sourceConnectionString中解析数据库名称
            using var targetConn = GetDbConnection(targetConnectionString);
            targetConn.Open();

            // 1.生成所有表的insert语句
            List<TableSqlsInfo> allTablesInsertSqls = new();

            using (var sourceConn =GetDbConnection(sourceConnectionString))
            {
                // 数据源-数据库
                var res = await GetAllTablesAsync(sourceConn, sourceSyncedDb);
                foreach (var table in res)
                {
                    TableSqlsInfo tableInsertSqlInfo = await GetTableSqlsInfoAsync(table, sourceConn, null, targetConn, null);
                    allTablesInsertSqls.Add(tableInsertSqlInfo);
                }
            }

            // 2. 执行每个表的insert语句
            var dbTransaction = targetConn.BeginTransaction();
            foreach (var tableSqlsInfo in allTablesInsertSqls)
            {
                if (string.IsNullOrEmpty(tableSqlsInfo.BatchInsertSqlInfo.BatchInsertSql))
                {
                    // $"表{tableSqlsInfo.TableName}没有数据无需处理"
                    continue;
                }
                try
                {
                    var affectedRowsCount = await targetConn.ExecuteAsync(tableSqlsInfo.BatchInsertSqlInfo.BatchInsertSql, tableSqlsInfo.BatchInsertSqlInfo.Parameters, transaction: dbTransaction);
                    // $"{tableSqlsInfo.TableName}: {affectedRowsCount}条数据"
                }
                catch (Exception ex)
                {
                    // 批量插入SQL语句: tableSqlsInfo.BatchInsertSqlInfo.BatchInsertSql
                    dbTransaction.Rollback();
                    return;
                }
            }
            dbTransaction.Commit();
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
        public async Task SyncDatabaseWithTargetConnectionStringAsync(string table, IEnumerable<JToken> sourceRecords, string[] ignoreFields, string sourceIdField = "", string targetIdField = "", string connectionString = "")
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
        public async Task SyncDatabaseWithTargetDbAsync(string table, IEnumerable<JToken> sourceRecords, string[] ignoreFields, string sourceIdField = "", string targetIdField = "", string db = "")
        {
            await SyncDatabaseAsync(table, sourceRecords, ignoreFields: ignoreFields, sourceIdField: sourceIdField, targetIdField: targetIdField, db);
        }
        /// <summary>
        /// 通用同步逻辑
        /// </summary>
        /// <param name="table"></param>
        /// <param name="sourceRecords"></param>
        /// <returns></returns>
        private async Task SyncDatabaseAsync(string table, IEnumerable<JToken> sourceRecords, string[] ignoreFields, string sourceIdField = "", string targetIdField = "", string db = "")
        {
            //conn.ConnectionString: "server=127.0.0.1;port=3306;database=engine;user id=root;allowuservariables=True"
            //conn.ConnectionTimeout: 15
            //conn.Database: "iduo_engine_hznu"
            using var conn = GetDbConnection(_connectionString);
            if (!string.IsNullOrWhiteSpace(db))
            {
                ChangeDatabase(conn, db);
            }
            var varFlag = GetDbParameterFlag(_connectionString);
            
            // 先获取数据
            var dbRecords = await conn.QueryAsync($"select * from {conn.Database}.{table}") ?? throw new Exception($"获取{table}数据失败");
            // 然后删除已存在
            await DeleteExistRecordsAsync(conn, table, sourceRecords, dbRecords, varFlag, ignoreFields, sourceIdField, targetIdField, _logger);
            // 再重新获取数据
            dbRecords = await conn.QueryAsync($"select * from {conn.Database}.{table}") ?? throw new Exception($"获取{table}数据失败");

            var compareResult = CompareRecords(sourceRecords, dbRecords, ignoreFields, sourceIdField, targetIdField);

            var inserts = compareResult.ExistInSourceOnly;
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
                    _logger.LogError(sqlInfo.Sql);
                    _logger.LogError(JsonConvert.SerializeObject(sqlInfo.Parameters));
                    throw;
                }

                #region 记录数据日志
                LogData(_logger, table, inserted, sqlInfo);
                #endregion
            }
        }
        
        static async Task DeleteExistRecordsAsync(IDbConnection conn, string targetTable, object source, object target, string varFlag, string[] ignoreFields, string sourceIdField, string targetIdField, ILogger? logger = null)
        {
            var sourceRecords = JArray.FromObject(source);
            var targetRecords = JArray.FromObject(target);
            if (targetRecords.Any())
            {
                var compareResult = CompareRecords(sourceRecords, targetRecords, ignoreFields, sourceIdField, targetIdField);
                var deleteSqlsBuilder = new StringBuilder();
                var index = 0;
                Dictionary<string, dynamic> parameters = new();
                foreach (var item in compareResult.Changed)
                {
                    var itemProps = item.TargetRecord.Properties();
                    targetIdField = string.IsNullOrEmpty(targetIdField) ? itemProps.First().Name : targetIdField;
                    var targetIdVal = itemProps.FirstOrDefault(x => string.Equals(x.Name, targetIdField, StringComparison.OrdinalIgnoreCase))?.Value;
                    if (targetIdVal is null)
                    {
                        var firstProp = itemProps.First();
                        targetIdField = firstProp.Name;
                        targetIdVal = firstProp.Value;
                    }
                    deleteSqlsBuilder.Append($"delete from {targetTable} where {targetIdField}={varFlag}id{index};{Environment.NewLine}");
                    parameters.Add($"id{index}", targetIdVal.ToObject<dynamic>());
                    index++;
                }
                var deleteSqls = $"SET foreign_key_checks=0;{Environment.NewLine}" + deleteSqlsBuilder.ToString() + $"SET foreign_key_checks=1;";
                logger?.LogInformation(deleteSqls);
                var deleted = await conn.ExecuteAsync(deleteSqls, parameters);
                logger?.LogInformation($"已经删除{deleted}条记录");
            }
        }
        static void LogData(ILogger? logger, string table, int inserted, SqlInfo sqlInfo)
        {
            logger?.LogInformation($"数据表{table}新增{inserted}条数据数据");

            var fieldsMatch = Regex.Match(sqlInfo.Sql, @"insert\s+into\s+\w+\s*\(`{0,1}\w+`{0,1}(?<otherFields>(,\s*`{0,1}\w+`{0,1}\s*)*)\)", RegexOptions.IgnoreCase);
            var fieldsCount = fieldsMatch.Groups["otherFields"].Value.Count(x => x == ',') + 1;

            var names = sqlInfo.Parameters.Keys;
            var parameterBuilder = new StringBuilder();
            var index = 0;
            foreach (var name in names)
            {
                index++;
                var val = sqlInfo.Parameters[name]?.ToString();
                val = val?.Length > 50 ? val?[..50] : val;
                if (!string.IsNullOrWhiteSpace(val))
                {
                    parameterBuilder.Append($"{val}\t");
                    if (index % fieldsCount == 0)
                    {
                        parameterBuilder.Append(Environment.NewLine);
                    }
                }
            }
            logger?.LogInformation(parameterBuilder.ToString());
        }
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
            var insertFieldsStatement = GetFieldsStatement(firstRecordJObj);
            
            var insertValuesStatement = "";
            int recordIndex = 0;
            //var parameters = new DynamicParameters();
            var parameters = new Dictionary<string, object>();
            string? batchInsertSql;
            foreach (JObject insert in inserts.Cast<JObject>())
            {
                #region 2. 为每条数据生成Values部分(@v1,@v2,...),\n
                insertValuesStatement += "(";
                insertValuesStatement += GenerateRecordValuesStatement(insert, tableCols, parameters, varFlag, recordIndex);
                insertValuesStatement = insertValuesStatement.TrimEnd(',') + $"),{Environment.NewLine}";
                #endregion

                // 3. 生成一条批量语句: 每100条数据生成一个批量语句, 然后重置语句拼接
                if (recordIndex > 0 && (recordIndex + 1) % 100 == 0)
                {
                    batchInsertSql = GetBatchInsertSql(table, insertFieldsStatement, insertValuesStatement, conn.Database); // insertSql.TrimEnd(',') + ";"
                    insertSqls.Add(new SqlInfo(batchInsertSql, parameters));
                    insertValuesStatement = "";
                    parameters = new Dictionary<string, object>();
                }
                recordIndex++;
            }

            // 4. 生成一条批量语句: 生成最后一条批量语句
            batchInsertSql = GetBatchInsertSql(table, insertFieldsStatement, insertValuesStatement, conn.Database);
            insertSqls.Add(new SqlInfo(batchInsertSql, parameters));

            return insertSqls;
        }
        /// <summary>
        /// 获取一个库中的所有表
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="dbName"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>

        public static async Task<IEnumerable<dynamic>> GetAllTablesAsync(IDbConnection conn, string dbName)
        {
            string connstr = conn.ConnectionString;
            var dbtype = GetDbType(connstr);
            return dbtype switch
            {
                DatabaseType.MySql => await conn.QueryAsync($"select * from information_schema.`TABLES` WHERE table_schema='{dbName}'"),
                DatabaseType.SqlServer => throw new NotImplementedException(),
                DatabaseType.Oracle => throw new NotImplementedException(),
                DatabaseType.Sqlite => throw new NotImplementedException(),
                _ => throw new NotImplementedException(),
            };
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
        public static string GetTableFullName(dynamic tableDynamic)
        {
            return $"{tableDynamic.TABLESPACE_NAME}.{tableDynamic.TABLE_NAME}";
        }
        private static void ValideParameter(string parameter)
        {
            if (parameter.Contains('-') || parameter.Contains('\''))
            {
                throw new Exception($"参数{parameter}不合法");
            }
        }

        public static string GetPagedSql(string db, string table, DatabaseType dbType, int pageIndex, int pageSize, string? orderField, bool isAsc, DataFilter filters, out string queryCondition, out Dictionary<string, object> queryConditionParameters)
        {
            string orderClause = string.Empty;
            if (!string.IsNullOrWhiteSpace(orderField))
            {
                orderField = orderField.Replace("-", string.Empty).Replace("'", string.Empty);
                ValideParameter(orderField);
                orderClause = $" order by {orderField} {(isAsc ? "asc" : "desc")}";
            }
            string parameterFlag = dbType == DatabaseType.Oracle ? ":" : "@";

            #region 处理过滤条件
            queryCondition = string.Empty;
            queryConditionParameters = new();
            if (filters != null)
            {
                if (filters.FilterItems != null && filters.FilterItems.Count > 0)
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
                            var val = filterField.Value;
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
                            keyWordsCondition += $" or {keyWordsField} like CONCAT(CONCAT('%', {parameterFlag}{varName}), '%')";
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

            string baseSqlTxt = $"select * from {db}.{table} where 1=1 {queryCondition}";
            
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
        public static async Task<PagedData> GetPagedDataAsync(string table, int pageIndex, int pageSize, string? orderField, bool isAsc, IDbConnection dbConn, DataFilter filters)
        {
            // TODO: 数据库类型 作为公共属性
            var dbType = GetDbType(dbConn.ConnectionString);

            string sql = GetPagedSql(dbConn.Database, table, dbType, pageIndex, pageSize, orderField, isAsc, filters, out string condition, out Dictionary<string, object> parameters);

            #region 不同数据库对应的SQL语句
            //            switch (dbType)
            //            {
            //                case DatabaseType.Oracle:
            //                    sql = $@"select * from 
            //	(
            //	select t.*,rownum no from 
            //		(
            //		{baseSqlTxt} {orderClause}
            //		) t 
            //	)
            //where no>({pageIndex}-1)*{pageSize} and no<=({pageIndex})*{pageSize}";
            //                    break;
            //                case DatabaseType.MySql:
            //                    sql = $@"{baseSqlTxt} {orderClause} limit {(pageIndex - 1) * pageSize},{pageSize}";
            //                    break;
            //                case DatabaseType.SqlServer:
            //                    sql = $"{baseSqlTxt} {(string.IsNullOrEmpty(orderClause) ? "order by id desc" : orderClause)} OFFSET ({pageIndex}-1)*{pageSize} ROWS FETCH NEXT {pageSize} ROW ONLY";
            //                    break;
            //                default:
            //                    sql = $@"{baseSqlTxt} {(string.IsNullOrEmpty(orderClause) ? "order by id desc" : orderClause)} OFFSET ({pageIndex}-1)*{pageSize} ROWS FETCH NEXT {pageSize} ROW ONLY";
            //                    break;
            //            }
            #endregion

            string allCountSqlTxt = $"select count(*) from {dbConn.Database}.{table} where 1=1 {condition}";
            var data = await dbConn.QueryAsync(sql, parameters);
            var allCount = await dbConn.ExecuteScalarAsync<int>(allCountSqlTxt, parameters);
            var dataReader = await dbConn.ExecuteReaderAsync(sql, parameters);
            return new PagedData { Data = data, DataReader = dataReader, Count = allCount };
        }

        // 获取insert语句的字段部分 Name,Age
        public static string GetFieldsStatement(IEnumerable<ColumnInfo> colInfos)
        {
            var columnBuilder = new StringBuilder();
            foreach (var colInfo in colInfos)
            {
                var columnName = colInfo.ColumnName;
                columnBuilder.Append($"{columnName},");
            }
            return columnBuilder.ToString().TrimEnd(',');
        }
        // 获取insert语句的字段部分 Name,Age
        public static string GetFieldsStatement(JObject obj)
        {
            var insertFields = obj.Properties().Where(x => x.Name != "NO").Select(x => x.Name);
            return string.Join(',', insertFields.Select(x => $"`{x}`"));
        }
        public static object GetColumnValue(object value, string type)
        {
            if (value.GetType().Name == "DBNull")
            {
                if (!string.IsNullOrWhiteSpace(type))
                {
                    var lowerType = type.ToLower();
                    if (lowerType.Contains("varchar") || lowerType.Contains("clob"))
                    {
                        return string.Empty;
                    }
                    if (lowerType.Contains("time"))
                    {
                        return DateTime.Now;
                    }
                    if (lowerType.Contains("int"))
                    {
                        return 0;
                    }
                    if (lowerType.Contains("byte") || lowerType.Contains("varbinary") || lowerType.Contains("blob"))
                    {
                        return Array.Empty<byte>();
                    }
                    throw new Exception($"未处理的DBNull参数: {type}");
                }
            }
            return value;
        }
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
        public static string GetBatchInsertSql(string table, string columnsStatement, string valuesStatement, string database)
        {
            string dbStatement = string.IsNullOrWhiteSpace(database) ? string.Empty : $"{database}.";
            return $"SET foreign_key_checks=0;{Environment.NewLine}insert into {dbStatement}{table}({columnsStatement}) values{Environment.NewLine}{valuesStatement.TrimEnd(',', '\r', '\n', ';')};";
        }

        // 获取创建表的字段声明部分
        public static string GetColumnsAssignment(IEnumerable<ColumnInfo> colInfos, DatabaseType dbType, object? tableRecords = null)
        {
            // 处理一条数据
            StringBuilder columnBuilder = new();
            foreach (var colInfo in colInfos)
            {
                // Age
                var columnName = colInfo.ColumnName;
                var timeTypeDefine = colInfo.ColumnType ?? string.Empty;
                switch (dbType)
                {
                    case DatabaseType.MySql:
                        columnName = $"`{columnName}`";
                        timeTypeDefine = $"datetime(6){Environment.NewLine}";
                        break;
                    case DatabaseType.Oracle:
                        columnName = $"{columnName}";
                        timeTypeDefine = $"TIMESTAMP(6) WITH TIME ZONE{Environment.NewLine}";
                        break;
                    case DatabaseType.SqlServer:
                        columnName = $"{columnName}";
                        timeTypeDefine = $"datetime2(7){Environment.NewLine}";
                        break;
                    case DatabaseType.Sqlite:
                        columnName = $"{columnName}";
                        timeTypeDefine = $"TEXT default current_timestamp {Environment.NewLine}";
                        break;
                    default:
                        columnName = $"{columnName}";
                        timeTypeDefine = $"datetime(6){Environment.NewLine}";
                        break;
                }

                if (string.IsNullOrWhiteSpace(colInfo.ColumnType))
                {
                    throw new Exception($"字段{colInfo.ColumnName}的类型为空");
                }

                var columnTypeAndLength = colInfo.ColumnType switch
                {
                    // 长整型字段
                    var type when RegexConst.ColumnTypeLong().IsMatch(type.ToLower()) => dbType switch
                    {
                        DatabaseType.Oracle => $"long{Environment.NewLine}",
                        _ => $"bigint{Environment.NewLine}"
                    },
                    // 整型字段
                    var type when RegexConst.ColumnTypeInt().IsMatch(type.ToLower()) => dbType switch
                    {
                        DatabaseType.Oracle => $"number",
                        _ => $"int{Environment.NewLine}"
                    },
                    var type when type.ToLower().Contains("date") => timeTypeDefine,
                    var type when type.ToLower().Contains("time") => timeTypeDefine,
                    // 大文本字段
                    var type when type.ToLower().Contains("clob") => dbType switch
                    {
                        DatabaseType.Oracle => $"blob{Environment.NewLine}",
                        _ => $"text{Environment.NewLine}"
                    },
                    // 二进制字段
                    var type when RegexConst.ColumnTypeBlob().IsMatch(type.ToLower()) => dbType switch
                    {
                        DatabaseType.Oracle => "blob",
                        _ => $"mediumblob{Environment.NewLine}"
                    },
                    var type when type.ToLower().Contains("varchar") && Convert.ToInt32(colInfo.ColumnLength) > 0 => $"varchar({GetStringColumnLengthBySourceData(colInfo.ColumnLength, colInfo.ColumnCode, tableRecords)}){Environment.NewLine}",
                    var type when type.ToLower().Contains("string") && Convert.ToInt32(colInfo.ColumnLength) > 2000000000 => $"blob{Environment.NewLine}",
                    var type when type.ToLower().Contains("string") && Convert.ToInt32(colInfo.ColumnLength) < 2000 => $"nvarchar({(colInfo.ColumnLength == "-1" ? string.Empty : colInfo.ColumnLength)}){Environment.NewLine}",
                    // 普通字符串字段
                    _ => dbType switch
                    {
                        DatabaseType.Oracle => $"nvarchar2({colInfo.ColumnLength})",
                        DatabaseType.MySql => $"nvarchar({colInfo.ColumnLength}){Environment.NewLine}",
                        DatabaseType.Sqlite => $"TEXT{Environment.NewLine}",
                        _ => $"varchar({colInfo.ColumnLength}){Environment.NewLine}"
                    }
                };
                columnBuilder.Append($"{columnName} {columnTypeAndLength},");
            }

            return columnBuilder.ToString().TrimEnd(',');
        }
        static int GetStringColumnLengthBySourceData(string? columnLength, string? columnCode, object? tableRecords)
        {
            if (string.IsNullOrWhiteSpace(columnLength))
            {
                columnLength = "0";
            }
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

        // 根据一条DataRow对象 生成对应的insert语句的值(参数)部分 @Name1,@Age1
        public static string GenerateRecordValuesStatement(object record, IEnumerable<ColumnInfo> colInfos, Dictionary<string, object> parameters, string dbVarFlag, int random = 0)
        {
            if (record is DataRow dataItem)
            {
                // 处理一条数据
                var valueBuilder = new StringBuilder();
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

                return valueBuilder.ToString().TrimEnd(',');
            }
            else
            {
                IEnumerable<JProperty> properties = record is JObject recordJObj ? recordJObj.Properties() : JObject.FromObject(record).Properties() ?? throw new Exception($"record不是DataRow或者记录对应的对象类型");
                var insertValuesStatement = "";
                foreach (var p in properties)
                {
                    if (p.Name == "NO")
                    {
                        continue;
                    }
                    var paramName = $"{p.Name}{random}";
                    insertValuesStatement += $"{dbVarFlag}{paramName},";
                    
                    var col = colInfos.FirstOrDefault(x => string.Equals(x.ColumnCode, p.Name, StringComparison.OrdinalIgnoreCase));
                    dynamic? pVal = GetColumnValue(p, col);
                    parameters.Add(paramName, pVal);
                }
                return insertValuesStatement;
            }
        }
        //public static string GenerateRecordValuesStatement(JObject dataItem, IEnumerable<ColumnInfo> colInfos, Dictionary<string, object> parameters, string dbVarFlag, int random = 0)
        //{
        //    // 处理一条数据
        //    var valueBuilder = new StringBuilder();
        //    var properties = dataItem.Properties();
        //    foreach (var colInfo in colInfos)
        //    {
        //        var columnName = colInfo.ColumnName;
        //        if (string.IsNullOrWhiteSpace(columnName))
        //        {
        //            throw new Exception($"包含空字段");
        //        }
        //        var columnValue = properties.FirstOrDefault(x => string.Equals(x.Name, columnName, StringComparison.OrdinalIgnoreCase))?.Value;
        //        var columnType = colInfo.ColumnType ?? string.Empty;
        //        var parameterName = $"{columnName}{random}";

        //        valueBuilder.Append($"{dbVarFlag}{parameterName},");

        //        parameters.Add(parameterName, GetColumnValue(columnValue, columnType));
        //    }

        //    return valueBuilder.ToString().TrimEnd(',');
        //}

        // 生成一条insert语句 - mysql, 不同于Oracle,这里不需要参数: string targetTable, string columnsStatement,
        public static string GenerateInsertSql(DataRow dataItem, IEnumerable<ColumnInfo> colInfos, Dictionary<string, object> parameters, string dbVarFlag, int random = 0)
        {
            // "@Name1, :Age1"
            string valuesStatement = GenerateRecordValuesStatement(dataItem, colInfos, parameters, dbVarFlag, random);
            // ("@Name1, :Age1"),
            string currentDataRowInsertStatement = $"({valuesStatement}),{Environment.NewLine}";
            return currentDataRowInsertStatement;
        }
        //public static string GenerateInsertSql(JObject dataItem, IEnumerable<ColumnInfo> colInfos, Dictionary<string, object> parameters, string dbVarFlag, int random = 0)
        //{
        //    // "@Name1, :Age1"
        //    string valuesStatement = GenerateRecordValuesStatement(dataItem, colInfos, parameters, dbVarFlag, random);
        //    // ("@Name1, :Age1"),
        //    string currentDataRowInsertStatement = $"({valuesStatement}),{Environment.NewLine}";
        //    return currentDataRowInsertStatement;
        //}
        /// <summary>生成创建表的SQL语句</summary>
        public static string GenerateCreateTableSql(string tableName, DatabaseType dbType, IEnumerable<ColumnInfo> colInfos, object? tableRecords = null)
        {
            var columnsAssignment = GetColumnsAssignment(colInfos, dbType, tableRecords);
            // TODO: 主键primaryKey 作为公共属性
            var columnPrimaryKey = colInfos.FirstOrDefault(c => c.IsPK == 1) ?? colInfos.FirstOrDefault();
            var primaryKeyAssignment = columnPrimaryKey is null ? string.Empty : $", PRIMARY KEY (`{columnPrimaryKey.ColumnCode}`) USING BTREE";
            string createSql = $@"CREATE TABLE `{tableName}` (
		      {columnsAssignment}
		      {primaryKeyAssignment}
		    ) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;";
            return createSql;
        }

        /// <summary>获取数据源中一个表的create语句和数据的insert语句</summary>
        public static async Task<TableSqlsInfo> GetTableSqlsInfoAsync(dynamic table, IDbConnection conn, IDbTransaction trans, IDbConnection targetConn, DataFilter filter)
        {
            // IDUO.IDS4.DEPARTMENT
            string targetTable = GetTableFullName(table);
            if (targetTable.Split('.').Last().StartsWith("_"))
            {
                return new TableSqlsInfo
                {
                    TableName = targetTable
                };
            }
            return await GetTableSqlsInfoAsync(targetTable, conn, trans, targetConn, filter);
        }
        public static DatabaseType GetDbType(string connectionString)
        {
            var lowerConnStr = connectionString.ToLower();
            return lowerConnStr switch
            {
                var connStr when connStr.Contains("initial catalog", StringComparison.OrdinalIgnoreCase) => DatabaseType.SqlServer,
                var connStr when connStr.Contains("server", StringComparison.OrdinalIgnoreCase) => DatabaseType.MySql,
                var connStr when RegexConst.ConnectionStringSqlite().IsMatch(connStr) => DatabaseType.Sqlite,
                _ => DatabaseType.Oracle
            };
        }
        public static string GetDbParameterFlag(string connectionString) => GetDbType(connectionString) == DatabaseType.Oracle ? ":" : "@";
        public static string GetDbParameterFlag(DatabaseType dbType) => dbType == DatabaseType.Oracle ? ":" : "@";

        /// <summary>获取数据源中一个表的数据的insert语句</summary>
        public static async Task<TableSqlsInfo> GetTableSqlsInfoAsync(string sourceTable, IDbConnection sourceConn, IDbTransaction trans, IDbConnection targetConn, DataFilter filter)
        {
            var tableName = sourceTable.Split('.').Last();
            // 1. 获取源表的所有字段信息
            #region 获取源表的所有字段信息
            IEnumerable<ColumnInfo> colInfos = await GetTableColumnsInfoAsync(sourceConn, tableName);
            var sourcePrimaryKey = colInfos.FirstOrDefault(x => x.IsPK == 1)?.ColumnCode ?? colInfos.FirstOrDefault()?.ColumnCode;
            #endregion

            // 2. 获取源表所有数据
            #region 获取源表所有数据
            DataTable sourceDataTable = new(tableName);
            try
            {
                var srouceTableDataReader = (await GetPagedDataAsync(sourceTable, 1, 3000, sourcePrimaryKey, true, sourceConn, filter)).DataReader ?? throw new Exception($"表{sourceTable}的数据读取器为空");
                sourceDataTable.Load(srouceTableDataReader);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"查询数据出错targetTableDataSql: {sourceTable}");
                Console.WriteLine(ex.ToString());
            }
            #endregion

            // 3. 获取目标表的所有字段信息
            IEnumerable<ColumnInfo> targetColInfos = await GetTableColumnsInfoAsync(targetConn, tableName);
            var targetPrimaryKey = colInfos.FirstOrDefault(x => x.IsPK == 1)?.ColumnName ?? "";

            // 4. 获取目标表的数据, 没有则创建

            #region 获取目标表的数据
            var targetDataTable = new DataTable();
            string createSql = string.Empty;
            try
            {
                targetDataTable = await QueryAsync(targetConn, $"select * from {tableName}", new Dictionary<string, object>(), trans);
            }
            catch (Exception ex)
            {
                if (ex.Message.ToLower().Contains("doesn't exist"))
                {
                    // 表不存在创建表
                    createSql = GenerateCreateTableSql(tableName, GetDbType(targetConn.ConnectionString), colInfos);
                    //await targetConn.ExecuteAsync(createSql);
                    //targetDataTable = await QueryAsync(targetConn, $"select * from {tableName}", new Dictionary<string, object>(), trans);
                }
            }
            #endregion

            // 5. 对比原表和目标表数据, 目标表没有的数据就创建对应的insert语句
            #region 对比原表和目标表数据, 目标表没有的数据就创建对应的insert语句
            // TODO: 数据库类型 作为公共属性
            var dbVarFlag = GetDbParameterFlag(targetConn.ConnectionString);
            var database = targetConn.Database;
            var insertSqlBuilder = new StringBuilder();
            // insert语句参数
            var parameters = new Dictionary<string, object>();
            // 参数名后缀(第一条数据的Name字段, 参数名为Name1, 第二条为Name2, ...)
            var random = 1;

            if (sourceDataTable.Rows.Count == 0)
            {
                return new TableSqlsInfo
                {
                    TableName = sourceTable
                };
            }

            // columnsStatement =           "Name, Age"
            string columnsStatement = GetFieldsStatement(colInfos);

            var comparedResult = CompareRecords(sourceDataTable.Rows.Cast<DataRow>(), targetDataTable.Rows.Cast<DataRow>(), Array.Empty<string>(), sourcePrimaryKey, targetPrimaryKey);

            // 为数据源中的每一条数据生成对应的insert语句的部分
            foreach (DataRow dataItem in sourceDataTable.Rows)
            {
                // 重复的数据不做处理
                if (targetDataTable.Rows.Count > 0 && AlreadyExistInTarget(targetDataTable.Rows, dataItem))
                {
                    continue;
                }

                // (@Name1, @Age1),
                string currentRecordSql = GenerateInsertSql(dataItem, colInfos, parameters, dbVarFlag, random);
                insertSqlBuilder.Append($"{currentRecordSql}");
                random++;
            }
            // 如果表中已经存在所有数据, 那么insertSqlBuilder为空的
            if (insertSqlBuilder.Length == 0)
            {
                Console.WriteLine($"{sourceTable}已经拥有所有记录");
                return new TableSqlsInfo
                {
                    TableName = sourceTable,
                };
            }
            // 最终的insert语句
            // var targetTableAllDataInsertSql = $"insert into {sourceTable}({columnsStatement}) values{Environment.NewLine}{insertSqlBuilder.ToString().TrimEnd(',', '\r', '\n')};";
            var targetTableAllDataInsertSql = GetBatchInsertSql(sourceTable, columnsStatement, insertSqlBuilder.ToString(), database);
            #endregion
            return new TableSqlsInfo
            {
                TableName = sourceTable,

                BatchInsertSqlInfo = new BatchInsertSqlInfo()
                {
                    CreateTableSql = createSql,
                    // mysql 本次会话取消外键约束检查
                    BatchInsertSql = targetTableAllDataInsertSql,
                    Parameters = parameters
                }
            };
        }
        
        //public static async Task<TableInsertSqlsInfo> GetTableInsertSqlsInfoAsync(IDbConnection targetConn, string table, List<JToken> dataSource)
        //{
        //    var dbVarFlag = GetDbParameterFlag(targetConn.ConnectionString);
        //    var insertSqlBuilder = new StringBuilder();
        //    // insert语句参数
        //    var parameters = new Dictionary<string, object>();
        //    // 参数名后缀(第一条数据的Name字段, 参数名为Name1, 第二条为Name2, ...)
        //    var random = 1;

        //    if (!dataSource.Any())
        //    {
        //        return new TableInsertSqlsInfo
        //        {
        //            TableName = table
        //        };
        //    }

        //    IEnumerable<ColumnInfo> targetTableColInfos = await GetTableColumnsInfoAsync(targetConn, table);
        //    // columnsStatement =           "Name, Age"
        //    string columnsStatement = GetFieldsStatement(targetTableColInfos);

        //    var targetDataTable = await QueryAsync(targetConn, $"select * from {table}", new Dictionary<string, object>(), null);
        //    var targetPrimaryKey = targetTableColInfos.FirstOrDefault(x => x.IsPK == 1)?.ColumnName ?? "Id";
        //    var comparedResult = CompareRecords(dataSource, targetDataTable.Rows.Cast<DataRow>(), Array.Empty<string>(), targetPrimaryKey, targetPrimaryKey);

        //    // 为数据源中的每一条数据生成对应的insert语句的部分
        //    foreach (JObject dataItem in dataSource.Cast<JObject>())
        //    {
        //        // 重复的数据不做处理
        //        if (targetDataTable.Rows.Count > 0 && AlreadyExistInTarget(targetDataTable.Rows, dataItem))
        //        {
        //            continue;
        //        }

        //        // (@Name1, @Age1),
        //        string currentRecordSql = GenerateInsertSql(dataItem, targetTableColInfos, parameters, dbVarFlag, random);
        //        insertSqlBuilder.Append($"{currentRecordSql}");
        //        random++;
        //    }
        //    // 如果表中已经存在所有数据, 那么insertSqlBuilder为空的
        //    if (insertSqlBuilder.Length == 0)
        //    {
        //        Console.WriteLine($"{table}已经拥有所有记录");
        //        return new TableInsertSqlsInfo
        //        {
        //            TableName = table,
        //        };
        //    }
        //    // 最终的insert语句
        //    // var targetTableAllDataInsertSql = $"insert into {table}({columnsStatement}) values{Environment.NewLine}{insertSqlBuilder.ToString().TrimEnd(',', '\r', '\n')};";
        //    var targetTableAllDataInsertSql = GetBatchInsertSql(table, columnsStatement, insertSqlBuilder.ToString());
        //    return new TableInsertSqlsInfo
        //    {
        //        TableName = table,

        //        BatchInsertSqlOnly = new BatchInsertSqlOnly()
        //        {
        //            // mysql 本次会话取消外键约束检查
        //            BatchInsertSql = targetTableAllDataInsertSql, // $"SET foreign_key_checks=0;{Environment.NewLine}{targetTableAllDataInsertSql}",
        //            Parameters = parameters
        //        }
        //    };
        //}

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
            return CompareRecords(sourceRecords, targetRecords, Array.Empty<string>(), sourceIdentityField, targetIdentityField);
        }

        /// <summary>
        /// **对比数据**
        /// </summary>
        /// <param name="sourceRecords">源数据</param>
        /// <param name="targetRecords">要对比的目标数据表中查询出的数据</param>
        /// <param name="sourceIdentityField">源数据中的标识字段</param>
        /// <param name="targetIdentityField">目标数据中的标识字段</param>
        /// <returns></returns>
        public static DataComparedResult CompareRecords(IEnumerable<object> sourceRecordsData, IEnumerable<object> targetRecordsData, string[] ignoreFields, string sourceIdentityField, string targetIdentityField)
        {
            var sourceRecords = sourceRecordsData.Select(JObject.FromObject).ToList();
            var targetRecords = targetRecordsData.Select(JObject.FromObject).ToList();
            var sourceIdentityFields = new string[] { sourceIdentityField, "Id", "GUID", sourceRecords?.FirstOrDefault()?.Properties()?.FirstOrDefault()?.Name ?? "" };
            var targetIdentityFields = new string[] { targetIdentityField, "Id", "GUID", targetRecords?.FirstOrDefault()?.Properties()?.FirstOrDefault()?.Name ?? "" };

            DataComparedResult compareResult = new();
            
            int i = 0;
            foreach (var sourceRecord in sourceRecords)
            {
                i++;
                JObject? target = null;
                foreach (var targetRecord in targetRecords)
                {
                    var sourceId = sourceRecord.Properties().FirstOrDefault(x => sourceIdentityFields.Any(idField => string.Equals(idField,  x.Name, StringComparison.OrdinalIgnoreCase)))?.Value?.ToString() ?? throw new Exception($"查找ID失败");
                    var targetId = targetRecord.Properties().FirstOrDefault(x => targetIdentityFields.Any(idField => string.Equals(idField, x.Name, StringComparison.OrdinalIgnoreCase)))?.Value?.ToString() ?? throw new Exception($"查找ID失败");

                    if (sourceId is not null && targetId is not null && sourceId.ToString() == targetId.ToString())
                    {
                        target = targetRecord; break;
                    }
                }

                // 目标中没有当前数据
                if (target is null)
                {
                    compareResult.ExistInSourceOnly.Add(sourceRecord);
                }
                else
                {
                    // TODO: 放到freach最外面, 分两种情况, 1targetRecords为空 2targetRecords不为空; 从指定的字段中对比数据
                    // 如果后面还需要使用到原始的targetRecords数据, 那么需要备份一份引用集合
                    targetRecords.Remove(target);

                    var keys = target.Properties();
                    var sourceRecordProps = sourceRecord.Properties();
                    
                    #region 比较每个字段的值, 判断同一条数据是否发生了变化
                    foreach (var key in keys)
                    {
                        // 忽略字段不比较
                        if (ignoreFields.Any() && ignoreFields.Any(x => string.Equals(x, key.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }
                        var sourceProp = sourceRecordProps.FirstOrDefault(p => string.Equals(p.Name, key.Name, StringComparison.OrdinalIgnoreCase));
                        if (sourceProp is not null)
                        {
                            var sourceValue = sourceProp.Value;
                            var targetValue = key.Value;
                            if (sourceValue.StringValueNotEquals(targetValue)) // 当前字段(key)的值不相同(至少有一个不为null且不为空字符串 并且两个字段值不相等)
                            {
                                compareResult.Changed.Add(new ChangedRecord { SourceRecord = sourceRecord, TargetRecord = target });
                                break;
                            }
                        }
                    }
                    #endregion
                }
            }

            foreach (var targetRecord in targetRecords)
            {
                compareResult.ExistInTargetOnly.Add(targetRecord);
            }

            return compareResult;
        }

        public class DataInJson
        {
            public DataInJson(string filePath, string[] dataFileds)
            {
                FilePath = filePath;
                DataFields = dataFileds;
            }
            public string FilePath { get; set; }
            public string[] DataFields { get; set; }
        }
        public class DataComparedResult
        {
            public JArray ExistInSourceOnly { get; set; } = new JArray();
            public JArray ExistInTargetOnly { get; set; } = new JArray();
            public List<ChangedRecord> Changed { get; set; } = new List<ChangedRecord>();
        }
        public class ChangedRecord
        {
            public JObject SourceRecord { get; set; } = new JObject();
            public JObject TargetRecord { get; set; } = new JObject();
        }
        public async Task<IEnumerable<ColumnInfo>> GetTableColumnsInfoAsync(string tableName)
        {
            using var conn = GetDbConnection(_connectionString);
            return await GetTableColumnsInfoAsync(conn, tableName);
        }
        /// <summary>
        /// 获取数据库表结构
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="database"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<ColumnInfo>> GetTableColumnsInfoAsync(IDbConnection conn, string tableName)
        {
            string sql;
            var database = conn.Database;
            if (string.IsNullOrWhiteSpace(database))
            {
                throw new Exception($"从连接字符串:{conn.ConnectionString}解析数据库名失败!");
            }
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
                    from information_schema.columns where table_schema = '{database}' and table_name = '{tableName}' 
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
                throw new Exception($"{database}中没有找到{tableName}表");
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

        public static async Task<DataTable> QueryAsync(IDbConnection conn, string sql, object parameters, IDbTransaction transaction)
        {
            var dataTable = new DataTable();
            var reader = await conn.ExecuteReaderAsync(sql, param: parameters, transaction: transaction);
            dataTable.Load(reader);
            return dataTable;
        }

        public static bool AlreadyExistInTarget(DataRowCollection rows, DataRow sourceRow)
        {
            foreach (DataRow row in rows)
            {
                // 比较第一列的值(Id)
                if (row[0].ToString() == sourceRow[0].ToString())
                {
                    return true;
                }
            }
            return false;
        }
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
                DataColumn targetCorrespondColumn = null;
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
                DataRow targetCorrespondRow = null;
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
                            if (sourceCol.DataType == typeof(byte[]) && (sourceRow[sourceCol.ColumnName] as byte[]).Length != (targetCorrespondRow[sourceCol.ColumnName] as byte[]).Length)
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
    }

    //SELECT a.BpmnBytes, a.ProcessImage FROM iduo_engine.devprocessmodel a; -- mediumblob
    //SELECT a.FieldConfigs FROM iduo_business.sys_datasource a; -- VARCHAR(3000)
    //SELECT a.ContentEn FROM iduo_portal.pt_joyrnalism a; -- mediumtext

    public class SyncData
    {
        public SyncData(List<DataColumn> deletingColumns, List<DataColumn> insertingColumns, List<DataRow> insertingRows, List<DataRow> updatingRows, List<DataRow> deletingRows)
        {
            DeletingColumns = deletingColumns ?? new List<DataColumn>();
            InsertingColumns = insertingColumns;

            DeletingRows = deletingRows;
            InsertingRows = insertingRows;
            UpdatingRows = updatingRows;
        }
        public List<DataColumn> DeletingColumns { get; set; }
        public List<DataColumn> InsertingColumns { get; set; }

        public List<DataRow> InsertingRows { get; set; }
        public List<DataRow> UpdatingRows { get; set; }
        public List<DataRow> DeletingRows { get; set; }
    }
}
