//using MySqlConnector;
//using Microsoft.Data.SqlClient;
using Dapper;
using Dm;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI.Relational;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using Sylas.RemoteTasks.Common;
using Sylas.RemoteTasks.Common.Extensions;
using Sylas.RemoteTasks.Database.Dtos;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Database.SyncBase
{
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
    /// </summary>
    public partial class DatabaseInfo : IDatabaseProvider
    {
        private string _connectionString;
        private DatabaseType _dbType;
        static readonly string[] _dangerousSqlStatements = ["delete ", "drop ", "truncate ", "alter ", "create ", "insert ", "update ", "--"];
        /// <summary>
        /// 数据库类型
        /// </summary>
        public DatabaseType DbType => _dbType;
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
            _connectionString = configuration.GetConnectionString("Default") ?? throw new Exception("没有配置默认的数据库连接字符串");
            _connectionString = CheckConnectionString(_connectionString);
            _logger = logger;
            _dbType = GetDbType(_connectionString);
            _varFlag = GetDbParameterFlag(_dbType);
        }
        /// <summary>
        /// 设置数据库连接字符串
        /// </summary>
        /// <param name="connectionString"></param>
        public void SetDb(string connectionString)
        {
            _connectionString = connectionString;
        }
        /// <summary>
        /// 检验连接字符串是否加密, 如果加密则解密
        /// </summary>
        /// <param name="connectionString"></param>
        static string CheckConnectionString(string connectionString)
        {
            if (Regex.IsMatch(connectionString, @"\s+"))
            {
                connectionString = connectionString.RemoveConfusedChars();
            }
            return connectionString;
        }
        /// <summary>
        /// 切换数据库 - 对指定的数据库连接对象切换到指定的数据库
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="db"></param>
        public static void ChangeDatabase(IDbConnection conn, string db)
        {
            if (conn.State == ConnectionState.Closed)
            {
                conn.Open();
            }
            if (!string.IsNullOrWhiteSpace(db) && GetDatabaseName(conn) != db)
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
                DatabaseType.Pg => new NpgsqlConnection(connectionString),
                DatabaseType.Sqlite => new SqliteConnection(connectionString),
                DatabaseType.Dm => new DmConnection(connectionString),
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
        /// 获取postgresql数据库连接对象
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <param name="db"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public static IDbConnection GetPgConnection(string host, string port, string db, string username, string password) => new SqlConnection($"User ID={username};Password={password};Host={host};Port={(string.IsNullOrWhiteSpace(port) ? "5432" : port)};Database={db}");
        /// <summary>
        /// 解析数据库连接字符串, 获取数据库详细信息
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static DbConnectionDetail GetDbConnectionDetail(string connectionString)
        {
            connectionString = CheckConnectionString(connectionString);
            var match = RegexConst.ConnectionStringSqlite.Match(connectionString);
            if (match.Success)
            {
                return new DbConnectionDetail(match.Groups["database"].Value, DatabaseType.Sqlite);
            }
            match = RegexConst.ConnectionStringMsLocalDb.Match(connectionString);
            if (match.Success)
            {
                return new DbConnectionDetail(match.Groups["database"].Value, DatabaseType.MsSqlLocalDb);
            }
            match = RegexConst.ConnectionStringOracle.Match(connectionString);
            if (match.Success)
            {
                return new DbConnectionDetail
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
                return new DbConnectionDetail
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
                return new DbConnectionDetail
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
                return new DbConnectionDetail
                {
                    Host = match.Groups["host"].Value,
                    Port = Convert.ToInt32(match.Groups["port"].Value),
                    Account = match.Groups["username"].Value,
                    Password = match.Groups["password"].Value,
                    Database = match.Groups["username"].Value,
                    DatabaseType = DatabaseType.Dm
                };
            }
            match = RegexConst.ConnectionStringPg.Match(connectionString);
            if (match.Success)
            {
                return new DbConnectionDetail
                {
                    Host = match.Groups["host"].Value,
                    Port = Convert.ToInt32(match.Groups["port"].Value),
                    Account = match.Groups["username"].Value,
                    Password = match.Groups["password"].Value,
                    Database = match.Groups["database"].Value,
                    DatabaseType = DatabaseType.Pg
                };
            }

            throw new Exception($"连接字符串解析失败: {connectionString}");
        }
        #endregion

        /// <summary>
        /// 分页查询指定数据表, 可使用db参数切换到指定数据库
        /// </summary>
        /// <param name="table">查询的表明</param>
        /// <param name="search">查询参数</param>
        /// <param name="db">指定要切换查询的数据库, 不指定使用Default配置的数据库</param>
        /// <returns></returns>
        public async Task<PagedData<T>> QueryPagedDataAsync<T>(string table, DataSearch? search, string db = "")
        {
            search ??= new();

            using var conn = GetDbConnection(_connectionString);
            if (!string.IsNullOrWhiteSpace(db))
            {
                conn.ChangeDatabase(db);
            }

            var dbType = GetDbType(conn.ConnectionString);

            string sql = GetPagedSql(table, dbType, search.PageIndex, search.PageSize, search.Filter, search.Rules, out string condition, out Dictionary<string, object?> parameters);

            string allCountSqlTxt = $"select count(*) from {table}{condition}";

            var start = DateTime.Now;
            await Console.Out.WriteLineAsync("执行SQL语句" + Environment.NewLine + allCountSqlTxt);

            var allCount = await conn.ExecuteScalarAsync<int>(allCountSqlTxt, parameters);

            var t1 = DateTime.Now;
            await Console.Out.WriteLineAsync($"耗时: {(t1 - start).TotalMilliseconds}/ms");
            await Console.Out.WriteLineAsync("执行SQL语句" + Environment.NewLine + sql);

            IEnumerable<T> data;
            if (typeof(T) == typeof(IDictionary<string, object>))
            {
                data = (await conn.QueryAsync(sql, parameters)).Cast<T>();
            }
            else if (typeof(IDictionary<string, object>).IsAssignableFrom(typeof(T)))
            {
                throw new Exception($"不支持字典类型{typeof(T).Name}, 请替换为IDictionary<string, object>");
            }
            else
            {
                data = await conn.QueryAsync<T>(sql, parameters);
            }

            await Console.Out.WriteLineAsync($"耗时: {(DateTime.Now - t1).TotalMilliseconds}/ms{Environment.NewLine}");

            return new PagedData<T> { Data = data, Count = allCount, TotalPages = (allCount + search.PageSize - 1) / search.PageSize };

        }
        /// <summary>
        /// 分页查询指定数据表, 可使用数据库连接字符串connectionString参数指定连接的数据库
        /// </summary>
        /// <param name="table">查询的表明</param>
        /// <param name="search">查询参数</param>
        /// <param name="connectionString">指定要切换查询的数据库, 不指定使用Default配置的数据库连接</param>
        /// <returns></returns>
        public async Task<PagedData<T>> QueryPagedDataWithConnectionStringAsync<T>(string table, DataSearch? search, string connectionString) where T : new()
        {
            search ??= new();
            _connectionString = connectionString;
            return await QueryPagedDataAsync<T>(table, search);
        }
        /// <summary>
        /// 执行增删改的SQL语句返回受影响的行数 - 可使用db参数指定切换到当前连接的用户有权限的其他数据库
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <param name="db"></param>
        /// <returns></returns>
        public async Task<int> ExecuteSqlAsync(string sql, object? parameters = null, string db = "")
        {
            if (sql.Contains('@') && (_dbType == DatabaseType.Oracle || _dbType == DatabaseType.Dm))
            {
                sql = sql.Replace('@', ':');
            }
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
            return res;
        }
        /// <summary>
        /// 执行多条增删改的SQL语句 - 可使用db参数指定切换到当前连接的用户有权限的其他数据库
        /// </summary>
        /// <param name="sqls"></param>
        /// <param name="parameters"></param>
        /// <param name="db"></param>
        /// <returns></returns>
        public async Task<int> ExecuteSqlsAsync(IEnumerable<string> sqls, Dictionary<string, object?> parameters, string db = "")
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
        /// 执行SQL语句, 返回第一条数据的第一个字段
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connectionString"></param>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static async Task<T?> ExecuteScalarAsync<T>(string connectionString, string sql, object? parameters = null)
        {
            using var conn = GetDbConnection(connectionString);
            return await conn.ExecuteScalarAsync<T>(sql, parameters);
        }
        /// <summary>
        /// 执行SQL语句并返回唯一一个值 - 可使用db参数指定切换到当前连接的用户有权限的其他数据库
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <param name="db"></param>
        /// <returns></returns>
        public async Task<int> ExecuteScalarAsync(string sql, object? parameters, string db = "")
        {
            using var conn = GetDbConnection(_connectionString);
            if (!string.IsNullOrWhiteSpace(db))
            {
                conn.ChangeDatabase(db);
            }
            var result = 0;
            conn.Open();
            using var trans = conn.BeginTransaction();
            try
            {
                _logger.LogDebug(sql);
                result = await conn.ExecuteScalarAsync<int>(sql, parameters, transaction: trans);
            }
            catch (Exception)
            {
                trans.Rollback();
                throw;
            }
            trans.Commit();
            return result;
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
        /// 动态更新一条数据
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="idAndUpdatingFields"></param>
        /// <param name="idFieldName"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<bool> UpdateAsync(string tableName, Dictionary<string, object> idAndUpdatingFields, string idFieldName = "")
        {
            if ((!string.IsNullOrWhiteSpace(idFieldName) && !idAndUpdatingFields.ContainsKey(idFieldName)) || !idAndUpdatingFields.Keys.Any(x => x.Equals("id", StringComparison.OrdinalIgnoreCase)))
            {
                throw new Exception("更新数据id不能为空");
            }
            return await UpdateAsync(_connectionString, tableName, idAndUpdatingFields, idFieldName);
        }
        /// <summary>
        /// 删除指定表的指定记录
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="ids"></param>
        /// <returns></returns>
        public async Task<int> DeleteAsync(string tableName, IEnumerable<object> ids)
        {
            return await DeleteAsync(_connectionString, tableName, ids);
        }
        static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, (Type, Func<string, object?>)>> _tableAndStringFieldsConverter = [];
        /// <summary>
        /// 获取表字段转换器, 可以将表字段的字符串值形式转换为对应的数据类型(如int, long, float, double, decimal, datetime)
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        static async Task<ConcurrentDictionary<string, (Type, Func<string, object?>)>> GetTableFieldsConverterAsync(IDbConnection conn, string tableName)
        {
            string tableKey = $"{conn.ConnectionString}_{tableName}";
            if (_tableAndStringFieldsConverter.TryGetValue(tableKey, out var stringFieldsConvert))
            {
                return stringFieldsConvert;
            }
            var colInfos = await GetTableColumnsInfoAsync(conn, tableName);
            ConcurrentDictionary<string, (Type, Func<string, object?>)> fieldsConverter = [];
            foreach (var colInfo in colInfos)
            {
                if (colInfo.ColumnCSharpType == "string")
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(colInfo.ColumnCSharpType))
                {
                    Type columnType = GetCSharpType(colInfo.ColumnCSharpType);
                    Func<string, object?> converter = ExpressionBuilder.CreateStringConverter(columnType);

                    fieldsConverter.TryAdd(colInfo.ColumnCode, (columnType, converter));
                }
            }
            _tableAndStringFieldsConverter.TryAdd(tableKey, fieldsConverter);
            return fieldsConverter;
        }
        /// <summary>
        /// 动态更新一条数据
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="tableName"></param>
        /// <param name="idAndUpdatingFields"></param>
        /// <param name="idFieldName"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task<bool> UpdateAsync(string connectionString, string tableName, Dictionary<string, object> idAndUpdatingFields, string idFieldName = "")
        {
            if ((!string.IsNullOrWhiteSpace(idFieldName) && !idAndUpdatingFields.ContainsKey(idFieldName)) || !idAndUpdatingFields.Keys.Any(x => x.Equals("id", StringComparison.OrdinalIgnoreCase)))
            {
                throw new Exception("更新数据id不能为空");
            }
            var dbType = GetDbType(connectionString);

            using var conn = GetDbConnection(connectionString);

            return await UpdateAsync(conn, tableName, idAndUpdatingFields, idFieldName);
        }
        static string[] GetTableIdFieldsFromParameters(string[] parameterKeys, string specifiedIdFieldName)
        {
            if (string.IsNullOrWhiteSpace(specifiedIdFieldName))
            {
                var idFieldNames = parameterKeys.Where(x => x.Equals("id", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (idFieldNames.Length > 0)
                {
                    return idFieldNames;
                }
            }
            else
            {
                var specifiedIdFieldNames = specifiedIdFieldName.Split(',', ';').Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
                bool allFieldNamesInParameters = specifiedIdFieldNames.Length > 0 && specifiedIdFieldNames.All(x => parameterKeys.Any(y => y.Equals(x, StringComparison.OrdinalIgnoreCase)));
                if (allFieldNamesInParameters)
                {
                    return parameterKeys.Where(x => specifiedIdFieldNames.Contains(x, StringComparer.OrdinalIgnoreCase)).ToArray();
                }
            }
            throw new Exception("id不能为空");
        }
        /// <summary>
        /// 查找表主键
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        static async Task<IEnumerable<ColumnInfo>> GetTablePkColInfosAsync(IDbConnection conn, string tableName)
        {
            var colInfos = await GetTableColumnsInfoAsync(conn, tableName);
            return colInfos.Where(x => x.IsPK == 1);
        }
        /// <summary>
        /// 动态更新一条数据
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="tableName"></param>
        /// <param name="idAndUpdatingFields"></param>
        /// <param name="idFieldName"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task<bool> UpdateAsync(IDbConnection conn, string tableName, Dictionary<string, object> idAndUpdatingFields, string idFieldName = "")
        {
            string[] idFields = GetTableIdFieldsFromParameters([.. idAndUpdatingFields.Keys], idFieldName);

            DateTime start = DateTime.Now;

            // 检查参数值是否需要转换(不是字符串就需要转换)
            Dictionary<string, object?> parameters = [];
            var tableFieldsConverter = await GetTableFieldsConverterAsync(conn, tableName);
            foreach (var idOrUpdatingField in idAndUpdatingFields)
            {
                var fieldToConvert = tableFieldsConverter.Keys.ToList().FirstOrDefault(x => x.Equals(idOrUpdatingField.Key, StringComparison.OrdinalIgnoreCase));
                if (fieldToConvert is not null)
                {
                    parameters.Add(idOrUpdatingField.Key, tableFieldsConverter[fieldToConvert].Item2(idOrUpdatingField.Value.ToString()));
                }
                else
                {
                    parameters.Add(idOrUpdatingField.Key, idOrUpdatingField.Value);
                }
            }

            var dbType = GetDbType(conn.ConnectionString);
            var varFlag = GetDbParameterFlag(dbType);

            string idCondition = string.Join(" and ", idFields.Select(x => $"{x}={varFlag}{x}"));
            string setStatement = string.Join(',', idAndUpdatingFields.Keys.Where(x => !idFields.Contains(x)).Select(x => $"{x}={varFlag}{x}"));

            if (string.IsNullOrWhiteSpace(setStatement))
            {
                throw new Exception("缺少更新的字段");
            }

            // 身为DateTime类型(非string类型)的UpdateTime字段肯定会在表字段类型转换器中
            string? updateTimeField = GetUpdateTimeField([.. tableFieldsConverter.Keys]);
            if (!string.IsNullOrWhiteSpace(updateTimeField))
            {
                setStatement += $",{updateTimeField}={varFlag}now";
                parameters.Add("now", DateTime.Now);
            }

            string sql = $"update {tableName} set {setStatement} where {idCondition}";

            var t1 = DateTime.Now;
            LoggerHelper.LogInformation($"动态获取局部更新Sql语句{Environment.NewLine}{sql}{Environment.NewLine}和参数共耗时: {(t1 - start).TotalMilliseconds}/ms");

            var res = await conn.ExecuteAsync(sql, parameters);

            LoggerHelper.LogInformation($"执行更新的SQL语句: {sql}; 耗时: {(DateTime.Now - t1).TotalMilliseconds}/ms");

            return res > 0;
        }

        /// <summary>
        /// 批量删除指定表的指定记录
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="tableName"></param>
        /// <param name="ids"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task<int> DeleteAsync(string connectionString, string tableName, IEnumerable<object> ids)
        {
            using var conn = GetDbConnection(connectionString);
            var pkColInfos = await GetTablePkColInfosAsync(conn, tableName);
            if (pkColInfos.Count() != 1)
            {
                throw new Exception(tableName + "表没有主键或者主键不唯一");
            }
            var pkColInfo = pkColInfos.First();
            var pkColName = pkColInfo.ColumnCode;
            var dbType = GetDbType(conn.ConnectionString);
            var varFlag = GetDbParameterFlag(dbType);

            Dictionary<string, object> parameters = [];
            List<Tuple<string, Dictionary<string, object>>> statementAndParamsList = [];
            int i = -1;
            StringBuilder statementBuilder = new();
            foreach (var id in ids)
            {
                i++;
                statementBuilder.Append($"{varFlag}id{i},");
                parameters.Add($"id{i}", pkColInfo.ColumnCSharpType == "string" ? id.ToString() : Convert.ToInt64(id));
                if ((i + 1) > 0 && (i + 1) % 500 == 0)
                {
                    statementAndParamsList.Add(Tuple.Create(statementBuilder.ToString().TrimEnd(','), parameters));
                    statementBuilder.Clear();
                    parameters = [];
                }
            }
            if (parameters.Count > 0)
            {
                statementAndParamsList.Add(Tuple.Create(statementBuilder.ToString().TrimEnd(','), parameters));
            }
            int deleted = 0;
            foreach (var item in statementAndParamsList)
            {
                var sql = $"delete from {tableName} where {pkColName} in ({item.Item1})";
                deleted += await conn.ExecuteAsync(sql, item.Item2);
            }
            return deleted;
        }
        /// <summary>
        /// 向指定数据表添加指定的数据
        /// </summary>
        /// <param name="table"></param>
        /// <param name="records"></param>
        /// <returns></returns>
        public async Task<int> InsertDataAsync(string table, IEnumerable<Dictionary<string, object?>> records)
        {
            using var conn = GetDbConnection(_connectionString);

            return await InsertDataAsync(conn, table, records);
        }

        static readonly string[] _notExistKeywords = ["does not exist", "doesn't exist", "不存在", "无效的表", "/对象名.+无效/", "no such table", "Invalid object name"];
        /// <summary>
        /// 判断异常是否是表不存在异常
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        static bool IsTableNotExistException(Exception ex)
            => _notExistKeywords.Any(keyword => (keyword.StartsWith('/') && keyword.EndsWith('/') && Regex.IsMatch(ex.Message, keyword.Trim('/'), RegexOptions.IgnoreCase))
            || ex.Message.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// 如果表不存在则创建表
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="colInfos"></param>
        /// <param name="db"></param>
        /// <returns></returns>
        public async Task CreateTableIfNotExistAsync(string tableName, IEnumerable<ColumnInfo> colInfos, string db = "")
        {
            using var conn = GetDbConnection(_connectionString);
            if (!string.IsNullOrWhiteSpace(db))
            {
                ChangeDatabase(conn, db);
            }

            if (!await TableExistAsync(conn, tableName))
            {
                // 表不存在创建表
                var createSql = GetCreateTableStatement(tableName, colInfos, _dbType);
                _logger.LogInformation($"数据表{tableName}不存在, 将创建数据表:{Environment.NewLine}{createSql}");
                _ = await conn.ExecuteAsync(createSql);
            }
        }

        /// <summary>
        /// 如果表不存在则创建表
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="tableName"></param>
        /// <param name="colInfos"></param>
        /// <param name="db"></param>
        /// <returns></returns>
        public static async Task CreateTableIfNotExistAsync(string connectionString, string tableName, IEnumerable<ColumnInfo> colInfos, string db = "")
        {
            using var conn = GetDbConnection(connectionString);
            await CreateTableIfNotExistAsync(conn, tableName, colInfos, db);
        }
        /// <summary>
        /// 如果表不存在则创建表
        /// </summary>
        /// <param name="targetConn"></param>
        /// <param name="tableName"></param>
        /// <param name="colInfos"></param>
        /// <param name="db"></param>
        /// <returns></returns>
        public static async Task CreateTableIfNotExistAsync(IDbConnection targetConn, string tableName, IEnumerable<ColumnInfo> colInfos, string db = "")
        {
            if (!string.IsNullOrWhiteSpace(db))
            {
                ChangeDatabase(targetConn, db);
            }
            var dbType = GetDbType(targetConn.ConnectionString);

            if (!await TableExistAsync(targetConn, tableName))
            {
                // 表不存在创建表
                var createSql = GetCreateTableStatement(tableName, colInfos, dbType);
                LoggerHelper.LogInformation($"数据表{tableName}不存在, 将创建数据表:{Environment.NewLine}{createSql}");
                _ = await targetConn.ExecuteAsync(createSql);
            }
        }
        /// <summary>
        /// 如果表不存在则创建表
        /// </summary>
        /// <param name="targetConnectionString"></param>
        /// <param name="tableName"></param>
        /// <param name="sourceConnectionString"></param>
        /// <param name="db"></param>
        /// <returns></returns>
        public static async Task CreateTableIfNotExistAsync(string targetConnectionString, string tableName, string sourceConnectionString, string db = "")
        {
            using var sourceConn = GetDbConnection(sourceConnectionString);
            var sourceColInfos = await GetTableColumnsInfoAsync(sourceConn, tableName);
            await CreateTableIfNotExistAsync(targetConnectionString, tableName, sourceColInfos, db);
        }
        /// <summary>
        /// 如果表不存在则创建表
        /// </summary>
        /// <param name="targetConn"></param>
        /// <param name="tableName"></param>
        /// <param name="sourceConn"></param>
        /// <param name="sourceTable"></param>
        /// <param name="db"></param>
        /// <returns></returns>
        public static async Task CreateTableIfNotExistAsync(IDbConnection targetConn, string tableName, IDbConnection sourceConn, string sourceTable = "", string db = "")
        {
            if (string.IsNullOrWhiteSpace(sourceTable))
            {
                sourceTable = tableName;
            }
            var sourceColInfos = await GetTableColumnsInfoAsync(sourceConn, sourceTable);
            await CreateTableIfNotExistAsync(targetConn, tableName, sourceColInfos, db);
        }
        /// <summary>
        /// 如果表不存在则创建表
        /// </summary>
        /// <param name="sourceConnectionString">获取数据表结构的数据库</param>
        /// <param name="tableName"></param>
        /// <param name="db">需要切换数据库时提供此参数</param>
        /// <returns></returns>
        public async Task CreateTableIfNotExistAsync(string tableName, string sourceConnectionString, string db = "")
        {
            IEnumerable<ColumnInfo> colInfos = await GetTableColumnsInfoAsync(GetDbConnection(sourceConnectionString), tableName);
            using var conn = GetDbConnection(_connectionString);
            if (!string.IsNullOrWhiteSpace(db))
            {
                ChangeDatabase(conn, db);
            }

            if (!await TableExistAsync(conn, tableName))
            {
                // 表不存在创建表
                var createSql = GetCreateTableStatement(tableName, colInfos, _dbType);
                _logger.LogInformation($"数据表{tableName}不存在, 将创建数据表:{Environment.NewLine}{createSql}");
                _ = await conn.ExecuteAsync(createSql);
            }
        }
        
        /// <summary>
        /// 备份数据表到指定目录中
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="backupDbName"></param>
        /// <param name="tables"></param>
        /// <param name="backupName"></param>
        /// <returns></returns>
        public static async Task<string> BackupDataAsync(string connectionString, string backupDbName, string tables = "", string backupName = "")
        {
            Dictionary<string, string> tableAndConditions = [];
            using var conn = GetDbConnection(connectionString);
            var dbTables = (await GetAllTablesAsync(conn));
            if (string.IsNullOrWhiteSpace(tables))
            {
                foreach (var table in dbTables)
                {
                    tableAndConditions.Add(table, string.Empty);
                }
            }
            else
            {
                GetTableQueryConditions(tables, tableAndConditions);
            }
            if (string.IsNullOrWhiteSpace(backupName))
            {
                backupName = DateTime.Now.ToString("yyyyMMddHHmmss");
            }
            if (string.IsNullOrWhiteSpace(backupDbName))
            {
                throw new Exception("backupName不能为空");
            }
            backupDbName = Path.Combine(AppStatus.StaticDirectory, "Backup", backupDbName, backupName);
            if (!Directory.Exists(backupDbName))
            {
                Directory.CreateDirectory(backupDbName);
            }

            string logFile = Path.Combine(backupDbName, $"backup.log");
            var dbType = GetDbType(conn.ConnectionString);
            var dbParamFlag = GetDbParameterFlag(dbType);
            foreach (var tableAndCondition in tableAndConditions)
            {
                string tableName = tableAndCondition.Key;
                tableName = dbTables.FirstOrDefault(x => x.Equals(tableName, StringComparison.OrdinalIgnoreCase)) ?? throw new Exception($"没有找到表:{tableName}");
                
                // 获取表的字段信息
                var columns = await GetTableColumnsInfoAsync(conn, tableName);

                // 解析条件, 将需要类型转换的条件值参数化
                var conditionAndParameters = await ResolveTableQueryConditions(tableAndCondition.Value, conn, tableName);
                var condition = conditionAndParameters.Item1;
                var parameters = conditionAndParameters.Item2;
                
                string tableStatement = GetTableStatement(tableName, dbType);
                if (!condition.StartsWith("where", StringComparison.OrdinalIgnoreCase))
                {
                    condition = string.IsNullOrWhiteSpace(condition) ? string.Empty : $" where {condition}";
                }
                string sql = $"select * from {tableStatement}{condition}";

                // 使用DataReader一条一条地读取数据, 避免一次性读取数据量过大
                using var reader = await conn.ExecuteReaderAsync(sql, param: parameters);
                string backupFile = Path.Combine(backupDbName, tableName);

                using var writer = new StreamWriter(backupFile, false);
                // 1.第一行记录表字段
                await writer.WriteLineAsync(JsonConvert.SerializeObject(columns));
                //File.AppendAllLines(logFile, [$"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 开始备份表:{table}"]);
                LoggerHelper.LogInformation($"开始备份表:{tableAndCondition}");
                // 2. 记录所有行
                int lines = 0;
                while (reader.Read())
                {
                    var values = new object[reader.FieldCount];
                    reader.GetValues(values);

                    var fieldStrVals = GetFieldsStringValues(values, columns.ToArray());
                    string recordLine = string.Join(',', fieldStrVals);
                    await writer.WriteLineAsync(recordLine);
                    lines++;
                }
                //File.AppendAllLines(logFile, [$"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {lines}条记录备份结束{Environment.NewLine}"]);
                LoggerHelper.LogInformation($"{lines}条记录备份结束{Environment.NewLine}");
            }
            return backupDbName;
        }
        /// <summary>
        /// 解析表的查询条件(只解析需要类型转换的值, 替换为参数化形式, 这样可无需解析表达式结构, 适用于复杂的多条件情况)
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="conn"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        static async Task<(string, Dictionary<string, object?>?)> ResolveTableQueryConditions(string condition, IDbConnection conn, string tableName)
        {
            var columnConverters = await GetTableFieldsConverterAsync(conn, tableName);
            Dictionary<string, object?>? parameters = null;
            var dbType = GetDbType(conn.ConnectionString);
            var dbParamFlag = GetDbParameterFlag(dbType);
            if (!string.IsNullOrWhiteSpace(condition))
            {
                if (_dangerousSqlStatements.Any(x => condition.Contains(x, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new Exception("备份表的查询条件不支持");
                }
                // TODO: 实现多个条件的解析
                foreach (var converter in columnConverters)
                {
                    if (converter.Value.Item1 != typeof(string)
                        && converter.Value.Item1 != typeof(int)
                        && converter.Value.Item1 != typeof(long)
                        && converter.Value.Item1 != typeof(double)
                        && converter.Value.Item1 != typeof(float)
                        && converter.Value.Item1 != typeof(decimal))
                    {
                        string columnOriginName = converter.Key;
                        var matches = Regex.Matches(condition, $@"{columnOriginName}\s*(?<operator>>|=|<|=|(>=)|(<=)|(!=)|(like)|(is))\s*(?<value>.+?)(?>=$|\s)", RegexOptions.IgnoreCase);
                        foreach (Match match in matches.Cast<Match>())
                        {
                            string columnStatement = GetTableStatement(columnOriginName, dbType);
                            string operatorStr = match.Groups["operator"].Value;
                            string valueStr = match.Groups["value"].Value;
                            if (!string.IsNullOrWhiteSpace(valueStr) && !string.IsNullOrWhiteSpace(valueStr) && valueStr != "null")
                            {
                                condition = condition.Replace(match.Value, $" {columnStatement}{operatorStr}{dbParamFlag}{columnOriginName}");
                                parameters ??= [];
                                parameters.Add(columnOriginName, converter.Value.Item2(valueStr));
                            }
                        }
                    }
                }
            }
            return (condition, parameters);
        }
        /// <summary>
        /// 解析表的查询条件
        /// </summary>
        /// <param name="tables">表和查询条件, 比如users(createdtime > 2024-10-01), 多个用逗号隔开</param>
        /// <param name="tableQueryConditions">存储解析结果的字典</param>
        static void GetTableQueryConditions(string tables, Dictionary<string, string> tableQueryConditions)
        {
            if (string.IsNullOrWhiteSpace(tables))
            {
                throw new Exception($"数据表及查询条件表达式{nameof(tables)}不能为空");
            }
            var matches = tables.ResolvePairedSymbolsContent("(", ")", @"[\w-]+", @"", true);
            var tableStatements = matches.Select(x => x.Value);
            foreach (var tableStatement in tableStatements)
            {
                int firstLeftParenIndex = tableStatement.IndexOf('(');
                string tableName = tableStatement.ToLower();
                string tableCondition = string.Empty;
                if (firstLeftParenIndex > 0 && tableStatement.EndsWith(')'))
                {
                    tableName = tableStatement[..firstLeftParenIndex];
                    tableQueryConditions[tableName.ToLower()] = string.Empty;
                    var condition = tableStatement[(firstLeftParenIndex + 1)..].TrimEnd(')');
                    if (!string.IsNullOrWhiteSpace(condition) && Regex.IsMatch(condition, @"(((and)|(or)){0,1}\s*\w+\s*(>|<|=|(>=)|(<=)(is))\s*[\w\.]+)+"))
                    {
                        tableCondition = condition;
                    }
                }
                tableQueryConditions[tableName] = tableCondition;
            }
        }
        const int batchSize = 1000;
        /// <summary>
        /// 还原数据表
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="backupDir"></param>
        /// <param name="tables"></param>
        /// <returns></returns>
        public static async Task RestoreTablesAsync(string connectionString, string backupDir, string tables = "")
        {
            tables ??= string.Empty;
            Dictionary<string, string> tableAndConditions = [];
            if (!string.IsNullOrWhiteSpace(tables))
            {
                GetTableQueryConditions(tables, tableAndConditions);
            }

            if (string.IsNullOrWhiteSpace(backupDir))
            {
                throw new Exception("数据备份目录不能为空");
            }

            string logFile = Path.Combine(backupDir, $"restore.log");
            var files = Directory.GetFiles(backupDir).Where(x => !x.EndsWith(".log"));
            foreach (var tableFile in files)
            {
                string table = tableFile.Split('/', '\\').Last();
                if (tableAndConditions.Count > 0 && !tableAndConditions.ContainsKey(table.ToLower()))
                {
                    continue;
                }

                Dictionary<string, Func<object?, bool>>? recordColIsMatchCondition = [];

                int i = 0;
                ColumnInfo[] cols = [];
                //string[] cols = [];
                List<Dictionary<string, object?>> records = [];

                //string paramFlag = GetDbParameterFlag(GetDbType(conn.ConnectionString));
                //string colsStatement = string.Empty;
                //string valuesStatement = string.Empty;
                using var conn = GetDbConnection(connectionString);
                foreach (var line in File.ReadLines(tableFile))
                {
                    if (i == 0)
                    {
                        #region 第一行, 字段信息, 解析条件
                        cols = JsonConvert.DeserializeObject<ColumnInfo[]>(line) ?? throw new Exception("从备份文件解析字段信息失败");
                        await CreateTableIfNotExistAsync(conn, table, cols);

                        if (!tableAndConditions.TryGetValue(table.ToLower(), out string tableCondition))
                        {
                            tableCondition = string.Empty;
                        }
                        if (!string.IsNullOrWhiteSpace(tableCondition))
                        {
                            // 只有第一行会解析数据过滤条件
                            var conditionMatch = Regex.Match(tableCondition, @"(?<l>\w+)(?<op>(>=)|(<=)|>|<|=|(==))(?<r>.+)");
                            if (conditionMatch.Groups.Count >= 3)
                            {
                                string l = conditionMatch.Groups["l"].Value;
                                string op = conditionMatch.Groups["op"].Value;
                                string r = conditionMatch.Groups["r"].Value;
                                ColumnInfo col = cols.FirstOrDefault(x => x.ColumnCode.Equals(l, StringComparison.OrdinalIgnoreCase));
                                recordColIsMatchCondition[col.ColumnCode] = (object? colVal) =>
                                {
                                    if (col.ColumnCSharpType == "int")
                                    {
                                        return (colVal as int? ?? 0) == Convert.ToInt32(r);
                                    }
                                    else if (col.ColumnCSharpType == "string")
                                    {
                                        string strVal = $"{r}";
                                        return op.StartsWith('>') ? string.Compare($"{colVal}", strVal) >= 0 : string.Compare($"{colVal}", strVal) <= 0;
                                    }
                                    else if (col.ColumnCSharpType == "long")
                                    {
                                        long longVal = Convert.ToInt64(r);
                                        return op.StartsWith('>') ? (colVal as long? ?? 0) >= longVal : (colVal as long? ?? 0) <= longVal;
                                    }
                                    else if (col.ColumnCSharpType == "decimal")
                                    {
                                        decimal decimalVal = Convert.ToDecimal(r);
                                        return op.StartsWith('>') ? (colVal as decimal? ?? 0) >= decimalVal : (colVal as decimal? ?? 0) <= decimalVal;
                                    }
                                    else if (col.ColumnCSharpType == "double" || col.ColumnCSharpType == "float")
                                    {
                                        double doubleVal = Convert.ToDouble(r);
                                        return op.StartsWith('>') ? (colVal as double? ?? 0) >= doubleVal : (colVal as double? ?? 0) <= doubleVal;
                                    }
                                    else if (col.ColumnCSharpType == "datetime")
                                    {
                                        if (colVal is null || colVal is not DateTime colDateTimeVal)
                                        {
                                            return false;
                                        }
                                        var datetimeVal = Convert.ToDateTime(r);
                                        return op.StartsWith('>') ? colDateTimeVal >= datetimeVal : colDateTimeVal <= datetimeVal;
                                    }
                                    else
                                    {
                                        LoggerHelper.LogCritical($"不支持比较大小的字段类型: {col.ColumnCSharpType}");
                                        return false;
                                    }
                                };
                            }
                        }
                        #endregion
                    }
                    else
                    {
                        // 其他行, 数据
                        var values = line.Split(',');
                        
                        bool ignoreRecord = false;

                        var parameters = new Dictionary<string, object?>();
                        for (int j = 0; j < cols.Length; j++)
                        {
                            if (j >= values.Length)
                            {
                                break;
                            }
                            var colInfo = cols[j];
                            object? fieldValue = GetFieldValue(values[j], colInfo.ColumnCSharpType ?? "string");
                            if (recordColIsMatchCondition.ContainsKey(colInfo.ColumnCode) && !recordColIsMatchCondition[colInfo.ColumnCode](fieldValue))
                            {
                                ignoreRecord = true;
                                break; ;
                            }
                            parameters.Add(colInfo.ColumnCode, fieldValue);
                        }
                        if (ignoreRecord)
                        {
                            continue;
                        }

                        records.Add(parameters);

                        if (records.Count > 0 && records.Count % batchSize == 0)
                        {
                            await TransferDataAsync(records, conn, table);
                            DateTime start = DateTime.Now;
                            await File.AppendAllLinesAsync(logFile, [$"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {batchSize}条记录备份结束, 耗时: {(DateTime.Now - start).TotalSeconds}/s"]);
                            records.Clear();
                        }
                    }
                    i++;
                }
                if (records.Any())
                {
                    await TransferDataAsync(records, conn, table);
                    //await InsertDataAsync(conn, table, records);
                    await File.AppendAllLinesAsync(logFile, [$"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {records.Count}条记录备份结束, 已还原所有数据"]);
                }
                LoggerHelper.LogInformation($"表{table}已还原");
            }
        }
        const string _randomStringForComma = "COMMAFLAGlsjflajflajf238024***%666666^^^^-+Oo)ukj(";
        const string _randomStringForEnter = "ENTERFLAGlsjflajflajf238024***%666666^^^^-+Oo)ukj(";
        static string GetFieldStringValue(object origin, ColumnInfo columnInfo)
        {
            string strVal;
            if (columnInfo.ColumnCSharpType == "byte[]")
            {
                strVal = origin is not byte[] bytes ? "null" : Convert.ToBase64String(bytes, Base64FormattingOptions.None);
            }
            else if (origin is null || origin.GetType() == typeof(DBNull))
            {
                strVal = "null";
            }
            else
            {
                strVal = origin.ToString();
            }
            return strVal.Replace(",", _randomStringForComma).Replace("\r\n", _randomStringForEnter).Replace("\n", _randomStringForEnter);
        }
        static string[] GetFieldsStringValues(object[] origins, ColumnInfo[] columnInfos)
        {
            string[] strVals = new string[origins.Length];
            for (int i = 0; i < origins.Length; i++)
            {
                strVals[i] = GetFieldStringValue(origins[i], columnInfos[i]);
            }
            return strVals;
        }
        static object? GetFieldValue(string fieldStringValue, string csharpType)
        {
            if (fieldStringValue == "null")
            {
                return null;
            }
            if (csharpType.Equals("int"))
            {
                if (fieldStringValue == "True")
                {
                    return 1;
                }
                if (fieldStringValue == "False")
                {
                    return 0;
                }
            }
            object? value = csharpType switch
            {
                "int" => Convert.ToInt32(fieldStringValue),
                "long" => Convert.ToInt64(fieldStringValue),
                "decimal" => Convert.ToDecimal(fieldStringValue),
                "double" or "float" => Convert.ToDouble(fieldStringValue),
                "byte[]" => Convert.FromBase64String(fieldStringValue),
                "bool" => fieldStringValue == "1",
                "datetime" => DateTime.TryParse(fieldStringValue, out DateTime datetimeValue) ? datetimeValue : null,
                _ => fieldStringValue.Replace(_randomStringForComma, ",").Replace(_randomStringForEnter, "\n")
            };
            return value;
        }
        /// <summary>
        /// 同步数据库
        /// </summary>
        /// <exception cref="Exception"></exception>
        public static async Task TransferDataAsync(string sourceConnectionString, string targetConnectionString, string sourceDb = "", string sourceTable = "", string targetTable = "", bool ignoreException = false, Func<string, bool>? tableCondition = null, bool insertOnly = false)
        {
            DateTime start = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(sourceTable) && string.IsNullOrWhiteSpace(targetTable))
            {
                targetTable = sourceTable;
            }
            if (string.IsNullOrWhiteSpace(sourceTable) && !string.IsNullOrWhiteSpace(targetTable))
            {
                throw new Exception($"请指定要同步到表{targetTable}源表名");
            }
            using var sourceConn = GetDbConnection(sourceConnectionString);
            if (!string.IsNullOrWhiteSpace(sourceDb) && GetDatabaseName(sourceConn) != sourceDb)
            {
                if (sourceConn.State != ConnectionState.Open)
                {
                    sourceConn.Open();
                }
                sourceConn.ChangeDatabase(sourceDb);
            }
            // 数据源-数据库
            var res = await GetAllTablesAsync(sourceConn);

            DatabaseType targetDbType = GetDbType(targetConnectionString);
            DatabaseType sourceDbType = GetDbType(sourceConnectionString);

            foreach (var sourceTableName in res)
            {
                if (!string.IsNullOrWhiteSpace(sourceTable) && !sourceTableName.Equals(sourceTable, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (tableCondition is not null && !tableCondition(sourceTableName))
                {
                    continue;
                }

                using var targetConnQuery = GetDbConnection(targetConnectionString);
                targetConnQuery.Open();
                // 1.生成所有表的insert语句; 异步迭代器(每个子对象包含一个批量插入数据的SQL语句)
                //IAsyncEnumerable<List<TableSqlInfo>> tableSqlsInfoCollection = GetDataTransferSqlInfosAsync(sourceConnectionString, sourceTableName, targetConnQuery, targetTable);
                IAsyncEnumerable<List<TableSqlInfo>> tableSqlsInfoCollection = GetDataTransferSqlInfosByDataReaderAsync(sourceConnectionString, sourceTableName, targetConnQuery, targetTable);

                // 2. 执行每个表的insert语句
                int allTransferCount = 0;
                DateTime t1 = DateTime.Now;
                await foreach (var tableSqlsInfos in tableSqlsInfoCollection)
                {
                    //List<Task> transferTasks = [];
                    //if (insertOnly)
                    //{
                    //    foreach (var tableSqlsInfo in tableSqlsInfos)
                    //    {
                    //        // TODO: 这里不知道必须加上Task.Run才会异步并行, 否则是串行执行的
                    //        var task = Task.Run(() => TransferByTableSqlsInfoAsync(tableSqlsInfo, targetConnectionString, targetTable, ignoreException, insertOnly));
                    //        transferTasks.Add(task);
                    //    }
                    //}
                    //else
                    //{
                    //    foreach (var tableSqlsInfo in tableSqlsInfos)
                    //    {
                    //        await TransferByTableSqlsInfoAsync(tableSqlsInfo, targetConnectionString, targetTable, ignoreException, insertOnly);
                    //    }
                    //}

                    //// 3. 等待所有迁移语句执行完成
                    //if (transferTasks.Count > 0)
                    //{
                    //    await Task.WhenAll(transferTasks);
                    //}
                    await TransferByTableSqlInfosAsync(tableSqlsInfos, targetConnectionString, targetTable, insertOnly, ignoreException);

                    int perBatchCount = Convert.ToInt32(Regex.Match(tableSqlsInfos.First().BatchInsertSqlInfo.Parameters.Last().Key, @"\d+").Value) + 1;
                    int transferCount = tableSqlsInfos.Count * perBatchCount;
                    allTransferCount += transferCount;
                    LoggerHelper.LogInformation($"表{sourceTableName}迁移{transferCount}/{allTransferCount}条记录, 耗时:{DateTimeHelper.FormatSeconds((DateTime.Now - t1).TotalSeconds)}");

                    t1 = DateTime.Now;
                }
            }
            LoggerHelper.LogInformation($"迁移结束, 耗时:{DateTimeHelper.FormatSeconds((DateTime.Now - start).TotalSeconds)}");
        }
        static async Task TransferByTableSqlInfosAsync(List<TableSqlInfo> tableSqlsInfos, string targetConnectionString, string targetTable, bool insertOnly, bool ignoreException)
        {
            List<Task> transferTasks = [];
            if (insertOnly)
            {
                foreach (var tableSqlsInfo in tableSqlsInfos)
                {
                    // TODO: 这里不知道必须加上Task.Run才会异步并行, 否则是串行执行的
                    var task = Task.Run(() => TransferByTableSqlsInfoAsync(tableSqlsInfo, targetConnectionString, targetTable, ignoreException, insertOnly));
                    transferTasks.Add(task);
                }
            }
            else
            {
                foreach (var tableSqlsInfo in tableSqlsInfos)
                {
                    await TransferByTableSqlsInfoAsync(tableSqlsInfo, targetConnectionString, targetTable, ignoreException, insertOnly);
                }
            }
            if (transferTasks.Count > 0)
            {
                await Task.WhenAll(transferTasks);
            }
        }
        static async Task TransferByTableSqlInfosAsync(List<TableSqlInfo> tableSqlsInfos, IDbConnection targetConn, string targetTable, bool insertOnly, bool ignoreException)
        {
            List<Task> transferTasks = [];
            if (insertOnly)
            {
                foreach (var tableSqlsInfo in tableSqlsInfos)
                {
                    // TODO: 这里不知道必须加上Task.Run才会异步并行, 否则是串行执行的
                    var task = Task.Run(() => TransferByTableSqlsInfoAsync(tableSqlsInfo, targetConn, targetTable, ignoreException, insertOnly));
                    transferTasks.Add(task);
                }
            }
            else
            {
                foreach (var tableSqlsInfo in tableSqlsInfos)
                {
                    await TransferByTableSqlsInfoAsync(tableSqlsInfo, targetConn, targetTable, ignoreException, insertOnly);
                }
            }
            if (transferTasks.Count > 0)
            {
                await Task.WhenAll(transferTasks);
            }
        }
        static async Task<(int, int)> TransferByTableSqlsInfoAsync(TableSqlInfo tableSqlsInfo, string targetConnectionString, string targetTable, bool ignoreException = false, bool insertOnly = false)
        {
            using var targetConnTransfer = GetDbConnection(targetConnectionString);
            targetConnTransfer.Open();
            return await TransferByTableSqlsInfoAsync(tableSqlsInfo, targetConnTransfer, targetTable, ignoreException, insertOnly);
        }
        
        static async Task<(int, int)> TransferByTableSqlsInfoAsync(TableSqlInfo tableSqlsInfo, IDbConnection targetConnTransfer, string targetTable, bool ignoreException = false, bool insertOnly = false)
        {
            int deletedRows = 0;
            int affectedRowsCount = 0;
            var targetDbType = GetDbType(targetConnTransfer.ConnectionString);
            if (targetConnTransfer.State != ConnectionState.Open)
            {
                targetConnTransfer.Open();
            }
            try
            {
                if (insertOnly)
                {
                    if (!string.IsNullOrEmpty(tableSqlsInfo.BatchInsertSqlInfo.Sql))
                    {
                        affectedRowsCount = await targetConnTransfer.ExecuteAsync(tableSqlsInfo.BatchInsertSqlInfo.Sql, tableSqlsInfo.BatchInsertSqlInfo.Parameters);
                        if (targetDbType == DatabaseType.Oracle && affectedRowsCount == -1)
                        {
                            affectedRowsCount = Regex.Matches(tableSqlsInfo.BatchInsertSqlInfo.Sql, @"insert", RegexOptions.IgnoreCase).Count;
                        }
                    }
                }
                else
                {
                    using var transferTransaction = targetConnTransfer.BeginTransaction();
                    if (!string.IsNullOrWhiteSpace(tableSqlsInfo.DeleteExistSqlInfo.Sql))
                    {
                        deletedRows = await targetConnTransfer.ExecuteAsync(tableSqlsInfo.DeleteExistSqlInfo.Sql, tableSqlsInfo.DeleteExistSqlInfo.Parameters, transaction: transferTransaction);
                        //LoggerHelper.LogInformation($"{targetTable}删除已存在的数据: {deletedRows}");
                    }
                    if (!string.IsNullOrEmpty(tableSqlsInfo.BatchInsertSqlInfo.Sql))
                    {
                        affectedRowsCount = await targetConnTransfer.ExecuteAsync(tableSqlsInfo.BatchInsertSqlInfo.Sql, tableSqlsInfo.BatchInsertSqlInfo.Parameters, transaction: transferTransaction);
                        if (targetDbType == DatabaseType.Oracle && affectedRowsCount == -1)
                        {
                            affectedRowsCount = Regex.Matches(tableSqlsInfo.BatchInsertSqlInfo.Sql, @"insert", RegexOptions.IgnoreCase).Count;
                        }
                    }
                    transferTransaction.Commit();
                }
            }
            catch (Exception ex)
            {
                // MySQL偶现了一次表情包无法插入的问题(表和字段字符集都是utf8mb4); 后面测试又都正常了
                if (Regex.IsMatch(ex.Message, @"Incorrect string value:") && targetDbType == DatabaseType.MySql)
                {
                    affectedRowsCount = await targetConnTransfer.ExecuteAsync($"SET NAMES utf8mb4;{tableSqlsInfo.BatchInsertSqlInfo.Sql}", tableSqlsInfo.BatchInsertSqlInfo.Parameters);
                    LoggerHelper.LogInformation($"{targetTable}添加数据: {affectedRowsCount}");
                }
                else
                {
                    LoggerHelper.LogInformation(ex.ToString());
                    LoggerHelper.LogInformation($"tableSqlsInfo.BatchInsertSqlInfo.BatchInsertSql: {tableSqlsInfo.BatchInsertSqlInfo.Sql}");
                }
            }

            return (deletedRows, affectedRowsCount);
        }

        /// <summary>
        /// 通用同步逻辑
        /// </summary>
        /// <param name="sourceRecords"></param>
        /// <param name="targetTable"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task TransferDataAsync(IEnumerable<object> sourceRecords, string targetTable)
        {
            await TransferDataAsync(sourceRecords, _connectionString, targetTable, logger: _logger);
        }

        /// <summary>
        /// 把数据同步到指定数据表
        /// </summary>
        /// <param name="sourceRecords"></param>
        /// <param name="targetConnectionString"></param>
        /// <param name="targetTable"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task TransferDataAsync(IEnumerable<object> sourceRecords, string targetConnectionString, string targetTable, ILogger? logger = null)
        {
            using var targetConn = GetDbConnection(targetConnectionString);
            await TransferDataAsync(sourceRecords, targetConn, targetTable, logger);
        }
        /// <summary>
        /// 把数据同步到指定数据表
        /// </summary>
        /// <param name="sourceRecords"></param>
        /// <param name="targetConn"></param>
        /// <param name="targetTable"></param>
        /// <param name="logger"></param>
        /// <param name="insertOnly"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task TransferDataAsync(IEnumerable<object> sourceRecords, IDbConnection targetConn, string targetTable, ILogger? logger = null, bool insertOnly = false)
        {
            if (sourceRecords is null || !sourceRecords.Any())
            {
                return;
            }
            IEnumerable<IDictionary<string, object?>> source = sourceRecords.CastToDictionaries();

            var varFlag = GetDbParameterFlag(targetConn.ConnectionString);
            await CreateTableIfNotExistAsync(targetConn, targetTable, targetConn, targetTable); // .Split('_')[0] (有些表名中包含下划线"_")

            var targetDbType = GetDbType(targetConn.ConnectionString);
            var targetTableColInfos = await GetTableColumnsInfoAsync(targetConn, targetTable);
            
            Dictionary<string, string> targetColumnMapToSource = [];
            var firstSourceRecordKeys = source.First().Keys;
            foreach (var colInfo in targetTableColInfos)
            {
                var sourceKey = firstSourceRecordKeys.FirstOrDefault(x => x.Equals(colInfo.ColumnCode, StringComparison.OrdinalIgnoreCase));
                if (sourceKey is not null)
                {
                    targetColumnMapToSource[colInfo.ColumnCode] = sourceKey;
                }
            }

            string[] tablePks = targetTableColInfos.Where(x => x.IsPK == 1).Select(x => x.ColumnCode).ToArray();
            string[] sourcePks = tablePks.Select(x => targetColumnMapToSource[x]).ToArray();

            var transferSqlInfos = GetDataTransferSqlInfos([source], sourcePks, targetTable, targetTableColInfos, targetDbType, targetColumnMapToSource);

            int perBatchCount = Convert.ToInt32(Regex.Match(transferSqlInfos.First().BatchInsertSqlInfo.Parameters.Last().Key, @"\d+").Value) + 1;
            DateTime t1 = DateTime.Now;

            await TransferByTableSqlInfosAsync(transferSqlInfos, targetConn, targetTable, insertOnly, false);
            int transferCount = transferSqlInfos.Count * perBatchCount;
            string log = $"表{targetTable}迁移{transferCount}条记录, 耗时:{DateTimeHelper.FormatSeconds((DateTime.Now - t1).TotalSeconds)}";
            if (logger is null)
            {
                LoggerHelper.LogInformation(log);
            }
            else
            {
                logger.LogInformation(log);
            }
        }
        /// <summary>
        /// 将数据添加到表中
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="table"></param>
        /// <param name="records"></param>
        /// <returns></returns>
        public static async Task<int> InsertDataAsync(string connectionString, string table, IEnumerable<IDictionary<string, object?>> records)
        {
            using var conn = GetDbConnection(connectionString);
            return await InsertDataAsync(conn, table, records);
        }
        /// <summary>
        /// 将数据添加到表中
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="table"></param>
        /// <param name="records"></param>
        /// <returns></returns>
        public static async Task<int> InsertDataAsync(IDbConnection conn, string table, IEnumerable<IDictionary<string, object?>> records)
        {
            if (!records.Any())
            {
                return 0;
            }
            if (string.IsNullOrWhiteSpace(table))
            {
                throw new Exception("表名不能为空");
            }

            var t1 = DateTime.Now;
            #region 处理数据 - 类型转换, 检查是否需要补充主键字段, 给创建/更新时间字段赋值
            List<Dictionary<string, object?>> list = [];
            var tableFieldsConverter = await GetTableFieldsConverterAsync(conn, table);

            var first = records.First();
            var recordKeys = first.Keys.ToArray();
            var colInfos = await GetTableColumnsInfoAsync(conn, table);
            List<string> notNeedConvertColumns = [];
            foreach (var colItem in first)
            {
                var colInfo = colInfos.FirstOrDefault(x => x.ColumnCode.Equals(colItem.Key, StringComparison.OrdinalIgnoreCase));
                if (colInfo is null)
                {
                    // 额外添加的字段
                    continue;
                }
                if ($"{colInfo.ColumnCSharpType}".Equals("string") || (colItem.Value is not null && colInfo.ColumnCSharpType is not null && colItem.Value.GetType().Equals(GetCSharpType(colInfo.ColumnCSharpType))))
                {
                    notNeedConvertColumns.Add(colInfo.ColumnCode);
                }
            }

            var createTimeColInfo = colInfos.FirstOrDefault(x => x.ColumnCode.Contains("create", StringComparison.OrdinalIgnoreCase) && x.ColumnCode.Contains("time", StringComparison.OrdinalIgnoreCase));
            var updateTimeColInfo = colInfos.FirstOrDefault(x => x.ColumnCode.Contains("update", StringComparison.OrdinalIgnoreCase) && x.ColumnCode.Contains("time", StringComparison.OrdinalIgnoreCase));
            foreach (var record in records)
            {
                // 1.先处理数据类型转换, 得到新的数据dataItem
                Dictionary<string, object?> dataItem = [];
                list.Add(dataItem);
                foreach (var item in record)
                {
                    var colInfo = colInfos.FirstOrDefault(x => x.ColumnCode.Equals(item.Key, StringComparison.OrdinalIgnoreCase));
                    if (colInfo is null)
                    {
                        continue;
                    }
                    if (item.Value is null || notNeedConvertColumns.Contains(item.Key))
                    {
                        dataItem.Add(item.Key, item.Value);
                    }
                    else
                    {
                        var fieldToConvert = tableFieldsConverter.Keys.ToList().FirstOrDefault(x => x.Equals(item.Key, StringComparison.OrdinalIgnoreCase));
                        if (fieldToConvert is not null)
                        {
                            var fieldStringValue = $"{item.Value}";
                            var fieldType = tableFieldsConverter[fieldToConvert].Item1;
                            var fieldConverter = tableFieldsConverter[fieldToConvert].Item2;
                            if (string.IsNullOrWhiteSpace(fieldStringValue))
                            {
                                dataItem.Add(item.Key, null);
                            }
                            else
                            {
                                if (fieldType == typeof(DateTime))
                                {
                                    if (fieldStringValue.Contains('/'))
                                    {
                                        var match = Regex.Match(fieldStringValue, @"(?<day>\d+)/(?<month>\d+)/(?<year>\d{4}) (?<time>\d{1,2}:\d{1,2}:\d{1,2})");
                                        if (match.Success)
                                        {
                                            string year = match.Groups["year"].Value;
                                            string month = match.Groups["month"].Value;
                                            string day = match.Groups["day"].Value;
                                            string time = match.Groups["time"].Value;
                                            fieldStringValue = Convert.ToInt32(month) <= 12 ? $"{year}/{month}/{day} {time}" : $"{year}/{day}/{month} {time}";
                                        }
                                    }
                                }
                                dataItem.Add(item.Key, fieldConverter(fieldStringValue));
                            }
                        }
                        else
                        {
                            dataItem.Add(item.Key, $"{item.Value}");
                        }
                    }
                }

                // 2. 处理主键字段
                if (!recordKeys.Any(x => x.Equals("id", StringComparison.OrdinalIgnoreCase)) || string.IsNullOrWhiteSpace(first.FirstOrDefault(x => x.Key.Equals("id")).Value?.ToString()))
                {
                    var pkColInfos = colInfos.Where(x => x.IsPK == 1);
                    // 为每条数据的所有字符串类型的主键赋值
                    foreach (var pkColInfo in pkColInfos)
                    {
                        string? pkKey = recordKeys.FirstOrDefault(x => x.Equals(pkColInfo.ColumnCode));
                        if (pkKey is null || string.IsNullOrWhiteSpace($"{record[pkKey]}"))
                        {
                            if (pkColInfo.ColumnCSharpType == "string")
                            {
                                dataItem[pkColInfo.ColumnCode] = $"{DateTime.Now:yyyyMMddHHmmssFFFFFFF}";
                            }
                        }
                    }
                }

                // 3. 处理创建时间字段
                if (createTimeColInfo is not null)
                {
                    var recordCreateTimeKey = first.Keys.FirstOrDefault(x => x.Equals(createTimeColInfo.ColumnCode, StringComparison.OrdinalIgnoreCase));
                    string recordKey = recordCreateTimeKey is null ? createTimeColInfo.ColumnCode : recordCreateTimeKey;
                    dataItem[recordKey] = DateTime.Now;
                }

                // 4. 更新时间字段
                if (updateTimeColInfo is not null)
                {
                    var recordupdateTimeKey = first.Keys.FirstOrDefault(x => x.Equals(updateTimeColInfo.ColumnCode, StringComparison.OrdinalIgnoreCase));
                    string recordKey = recordupdateTimeKey is null ? updateTimeColInfo.ColumnCode : recordupdateTimeKey;
                    dataItem[recordKey] = DateTime.Now;
                }
            }
            #endregion

            var t2 = DateTime.Now;
            LoggerHelper.LogInformation($"处理数据的类型转换, 主键字段, 创建时间, 更新时间字段耗时:{(t2 - t1).TotalMilliseconds}/ms");

            var insertSqlInfos = await GetInsertSqlInfosAsync(list, table, conn);
            var t3 = DateTime.Now;
            LoggerHelper.LogInformation($"获取insert SQL语句耗时: {(t3 - t2).TotalMilliseconds}/ms");

            int affectedRows = 0;
            foreach (var sqlInfo in insertSqlInfos)
            {
                int inserted = 0;
                try
                {
                    inserted = await conn.ExecuteAsync(sqlInfo.Sql, sqlInfo.Parameters);
                    LoggerHelper.LogInformation($"执行insert SQL语句耗时:{(DateTime.Now - t3).TotalSeconds}/ms");
                    affectedRows += inserted;
                }
                catch (Exception ex)
                {
                    LoggerHelper.LogInformation(ex.Message);
                    LoggerHelper.LogInformation(sqlInfo.Sql);
                    LoggerHelper.LogInformation(JsonConvert.SerializeObject(sqlInfo.Parameters));
                    throw;
                }

                #region 记录数据日志
                LogData(null, table, inserted, sqlInfo);
                #endregion
            }
            return affectedRows;
        }
        /// <summary>
        /// 从源数据库中查询数据并插入到目标数据库中
        /// </summary>
        /// <param name="sourceConn">源库的数据库连接对象</param>
        /// <param name="sourceQuerySql">源库的查询语句</param>
        /// <param name="sourceQueryParameters">源库的查询参数</param>
        /// <param name="targetTable">目标库对应的表</param>
        /// <param name="targetConn">目标库的数据库连接对象</param>
        /// <returns></returns>
        public static async Task<int> InsertDataAsync(IDbConnection sourceConn, string sourceQuerySql, object? sourceQueryParameters, string targetTable, IDbConnection targetConn)
        {
            var records = await sourceConn.QueryAsync(sourceQuerySql, sourceQueryParameters);
            var inserts = records.Cast<IDictionary<string, object?>>();
            return await InsertDataAsync(targetConn, targetTable, inserts);
        }
        /// <summary>
        /// 从源数据库中查询数据并插入到目标数据库中
        /// </summary>
        /// <param name="sourceConnectionString">源库的数据库连接字符串</param>
        /// <param name="sourceQuerySql">源库的查询语句</param>
        /// <param name="sourceQueryParameters">源库的查询参数</param>
        /// <param name="targetTable">目标库对应的表</param>
        /// <param name="targetConnectionString">目标库的数据库连接字符串</param>
        /// <returns></returns>
        public static async Task<int> InsertDataAsync(string sourceConnectionString, string sourceQuerySql, object? sourceQueryParameters, string targetTable, string targetConnectionString)
        {
            using var sourceConn = GetDbConnection(sourceConnectionString);
            var records = await sourceConn.QueryAsync(sourceQuerySql, sourceQueryParameters);
            var inserts = records.Cast<IDictionary<string, object?>>();
            using var targetConn = GetDbConnection(targetConnectionString);
            return await InsertDataAsync(targetConn, targetTable, inserts);
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
        /// <summary>
        /// 获取主键
        /// </summary>
        /// <param name="dataKeys"></param>
        /// <param name="sourceIdField"></param>
        /// <returns></returns>
        static List<string> GetPrimaryKeys(ICollection<string> dataKeys, string sourceIdField)
        {
            return GetPrimaryKeys(dataKeys.ToList(), sourceIdField);
        }
        /// <summary>
        /// 获取主键
        /// </summary>
        /// <param name="dataKeys"></param>
        /// <param name="sourceIdField"></param>
        /// <returns></returns>
        static List<string> GetPrimaryKeys(List<string> dataKeys, string sourceIdField)
        {
            List<string> sourcePrimaryKeys = [];
            sourcePrimaryKeys = CheckRecordIdFields(sourceIdField ?? string.Empty, dataKeys);
            return sourcePrimaryKeys;

            #region 本地方法, 校验参数提供的数据Id字段, 支持组合Id, 不存在的去掉; 如果提供的字段都不存在则校验有可能存在的主键字段如: id,guid, 存在则添加.
            List<string> CheckRecordIdFields(string idFieldName, List<string>? allFields)
            {
                if (allFields is null || !allFields.Any())
                {
                    return [.. idFieldName.Split(',', StringSplitOptions.RemoveEmptyEntries)];
                }

                List<string> idFields = [];
                var idFieldArr = idFieldName.Split(',');
                // 校验参数提供的Id字段是否存在
                foreach (var idField in idFieldArr)
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
            if (!compareResult.Intersection.Any())
            {
                return;
            }
            var targetDbType = GetDbType(conn.ConnectionString);
            StringBuilder deleteSqlsBuilder = new();
            var index = 0;
            Dictionary<string, dynamic> parameters = [];
            foreach (var item in compareResult.Intersection)
            {
                var itemProps = item.Keys.ToList();

                if (targetIdField.Contains(','))
                {
                    // 当前数据的主键字段集合
                    var primaryKeys = GetPrimaryKeys(itemProps, targetIdField);
                    StringBuilder conditionsBuilder = new();
                    for (int i = 0; i < primaryKeys.Count; i++)
                    {
                        var primaryKey = primaryKeys[i];
                        var targetIdVal = item[primaryKey] ?? throw new Exception($"主键字段{primaryKey}值为空");

                        conditionsBuilder.Append($" and {primaryKey}={varFlag}id{i}{index}");
                        parameters.Add($"id{i}{index}", targetIdVal);
                    }
                    deleteSqlsBuilder.Append($"delete from {targetTable} where 1=1 {conditionsBuilder};{Environment.NewLine}");
                }
                else
                {
                    targetIdField = string.IsNullOrEmpty(targetIdField) ? itemProps.First() : targetIdField;
                    // 当前数据的主键字段
                    var primaryKey = itemProps.FirstOrDefault(x => x.Equals(targetIdField, StringComparison.OrdinalIgnoreCase)) ?? throw new Exception($"无效的主键字段{targetIdField}");
                    var targetIdVal = item[primaryKey] ?? throw new Exception($"主键字段{primaryKey}值为空");

                    deleteSqlsBuilder.Append($"{varFlag}id{index},");
                    parameters.Add($"id{index}", targetIdVal);
                }


                index++;

                if (index > 0 && index % 100 == 0 || index >= compareResult.Intersection.Count)
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
                        if (logger is null)
                        {
                            LoggerHelper.LogInformation($"已经删除{deleted}条记录");
                        }
                        deleteSqlsBuilder.Clear();
                    }
                    else
                    {
                        logger?.LogInformation("没有旧数据需要删除");
                        if (logger is null)
                        {
                            LoggerHelper.LogInformation("没有旧数据需要删除");
                        }
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
        /// <param name="dataSource"></param>
        /// <param name="targetTableName"></param>
        /// <param name="targetConn"></param>
        /// <returns></returns>
        public static async Task<List<SqlInfo>> GetInsertSqlInfosAsync(object dataSource, string targetTableName, IDbConnection targetConn)
        {
            IEnumerable<IDictionary<string, object>> sourceRecords;
            if (dataSource is IEnumerable dataList)
            {
                sourceRecords = dataList.Cast<object>().CastToDictionaries();
            }
            else
            {
                sourceRecords = [JObject.FromObject(dataSource).ToObject<Dictionary<string, object>>() ?? throw new Exception("数据转字典失败")];
            }

            var dbType = GetDbType(targetConn.ConnectionString);
            var targetTableColumns = await GetTableColumnsInfoAsync(targetConn, targetTableName);
            var varFlag = GetDbParameterFlag(dbType);

            var firstRecord = sourceRecords.FirstOrDefault();

            var insertSqls = new List<SqlInfo>();
            if (firstRecord is null)
            {
                return insertSqls;
            }

            // 目标字段和源字段的映射
            Dictionary<string, string> targetColumnMapToSource = [];
            GetSourceColumnTargetColumnMap(firstRecord, targetTableColumns, targetColumnMapToSource);

            // 1. 获取字段部分, 以目标表字段为准
            var insertFieldsStatement = GetFieldsStatement(targetTableColumns, dbType);

            var insertValuesStatementBuilder = new StringBuilder();
            int recordIndex = 0;

            var parameters = new Dictionary<string, object?>();
            string? batchInsertSql;

            foreach (IDictionary<string, object> sourceRecord in sourceRecords)
            {
                #region 2. 为每条数据生成Values部分(@v1,@v2,...),\n
                insertValuesStatementBuilder.Append("(");
                insertValuesStatementBuilder.Append(GenerateRecordValuesStatement(sourceRecord, targetTableColumns, parameters, varFlag, targetColumnMapToSource, recordIndex));
                insertValuesStatementBuilder.Append($"),{Environment.NewLine}");
                #endregion

                // 3. 生成一条批量语句: 每100条数据生成一个批量语句, 然后重置语句拼接
                if (recordIndex > 0 && (recordIndex + 1) % 100 == 0)
                {
                    batchInsertSql = GetBatchInsertSql(targetTableName, insertFieldsStatement, insertValuesStatementBuilder.ToString(), dbType);
                    insertSqls.Add(new SqlInfo(batchInsertSql, parameters));
                    insertValuesStatementBuilder.Clear();
                    parameters = [];
                }
                recordIndex++;
            }

            // 4. 生成一条批量语句: 生成最后一条批量语句
            if (parameters.Count > 0) // 有可能正好100条数据已经全部处理, parameters处于清空状态
            {
                batchInsertSql = GetBatchInsertSql(targetTableName, insertFieldsStatement, insertValuesStatementBuilder.ToString(), dbType);
                insertSqls.Add(new SqlInfo(batchInsertSql, parameters));
            }

            return insertSqls;
        }
        /// <summary>
        /// 获取源数据字段名和目标表字段的映射
        /// </summary>
        /// <param name="sourceRecord">一条源数据</param>
        /// <param name="targetTableColumns">目标表的所有字段信息</param>
        /// <param name="targetColumnMapToSourceColumn">用来保存映射信息的字典</param>
        static void GetSourceColumnTargetColumnMap(IDictionary<string, object?> sourceRecord, IEnumerable<ColumnInfo> targetTableColumns, Dictionary<string, string> targetColumnMapToSourceColumn)
        {
            targetColumnMapToSourceColumn ??= [];
            if (targetColumnMapToSourceColumn.Any())
            {
                targetColumnMapToSourceColumn.Clear();
            }

            var sourceKeys = sourceRecord.Keys;
            foreach (var colInfo in targetTableColumns)
            {
                var sourceKey = sourceKeys.FirstOrDefault(x => x.Equals(colInfo.ColumnCode, StringComparison.OrdinalIgnoreCase));
                if (sourceKey is not null)
                {
                    targetColumnMapToSourceColumn.Add(colInfo.ColumnCode, sourceKey);
                }
            }
        }
        /// <summary>
        /// 获取数据迁移的SQL语句, 包含删除已存在的数据和插入新数据的SQL语句; 目标表不存在则创建
        /// </summary>
        /// <param name="sourceConnStr"></param>
        /// <param name="sourceTableName"></param>
        /// <param name="targetConn"></param>
        /// <param name="targetTable"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<List<TableSqlInfo>> GetDataTransferSqlInfosAsync(
            string sourceConnStr,
            string sourceTableName,
            IDbConnection targetConn,
            string? targetTable = null,
            DataFilter? filter = null)
        {
            #region 获取源表的所有字段信息
            var sourceDbType = GetDbType(sourceConnStr);
            var sourceTableStatement = GetTableStatement(sourceTableName, sourceDbType);

            IEnumerable<ColumnInfo> sourceColInfos = await GetTableColumnsInfoAsync(sourceConnStr, sourceTableName);
            string[] sourcePrimaryKeys = sourceColInfos.Where(x => x.IsPK == 1 && !string.IsNullOrWhiteSpace(x.ColumnCode)).Select(x => x.ColumnCode).ToArray();
            if (sourcePrimaryKeys.Length == 0)
            {
                sourcePrimaryKeys = [sourceColInfos.First().ColumnCode];
            }
            #endregion

            #region 获取表在目标库中的所有数据的主键值集合
            var targetDbType = GetDbType(targetConn.ConnectionString);
            if (string.IsNullOrWhiteSpace(targetTable))
            {
                targetTable = sourceTableName;
            }

            string targetTableStatement = GetTableStatement(targetTable, targetDbType);

            if (targetDbType == DatabaseType.SqlServer)
            {
                targetTableStatement = $"[dbo].{targetTableStatement}";
            }

            // 目标数据表不存在则创建
            await CreateTableIfNotExistAsync(targetConn, targetTable, sourceColInfos);
            #endregion

            if (sourceTableName.StartsWith('_'))
            {
                yield break;
            }

            int pageIndex = 1;

            #region 获取目标表的信息
            IEnumerable<ColumnInfo> targetColInfos = await GetTableColumnsInfoAsync(targetConn, targetTable);
            string[] sourceFields = sourceColInfos.Select(x => x.ColumnCode).ToArray();
            targetColInfos = targetColInfos.Where(x => sourceFields.Contains(x.ColumnCode, StringComparer.OrdinalIgnoreCase));
            #endregion

            // 目标字段和源字段的映射
            Dictionary<string, string> targetColumnMapToSource = [];
            bool queryEnd = false;
            const int queryConnCount = 100;
            while (true)
            {
                #region 获取源表数据
                ConcurrentBag<IEnumerable<IDictionary<string, object?>>> datalists = [];
                DateTime start = DateTime.Now;
                try
                {
                    List<Task> queryTasks = [];
                    LoggerHelper.LogInformation($"开始{queryConnCount}线程同时查询源表(pageIndex:{pageIndex} - {pageIndex + queryConnCount - 1})");
                    for (int i = 0; i < queryConnCount; i++)
                    {
                        int page = pageIndex + i;
                        Task task = Task.Run(async () =>
                        {
                            using var targetConnQuery = GetDbConnection(sourceConnStr);
                            targetConnQuery.Open();
                            List<OrderField> orderRules = sourcePrimaryKeys.Select(x => new OrderField { FieldName = x, IsAsc = true }).ToList();
                            var datalist = (await QueryPagedDataAsync<IDictionary<string, object?>>(sourceTableStatement, page, 1000, targetConnQuery, filter, orderRules)).Data;
                            if (datalist.Any())
                            {
                                datalists.Add(datalist);
                            }
                            else
                            {
                                queryEnd = true;
                            }
                        });
                        queryTasks.Add(task);
                    }
                    await Task.WhenAll(queryTasks);
                }
                catch (Exception ex)
                {
                    LoggerHelper.LogInformation($"查询数据出错sourceTable: {sourceTableName}");
                    LoggerHelper.LogInformation(ex.ToString());
                }
                DateTime t1 = DateTime.Now;
                LoggerHelper.LogInformation($"获取源表{datalists.Select(x => x.Count()).Sum()}条数据(PageIndex:{pageIndex})耗时:{(t1 - start).TotalSeconds}/s");
                if (!datalists.Any())
                {
                    yield break;
                }
                #endregion

                #region 数据源字段名和目标表字段的映射关系
                if (targetColumnMapToSource.Count == 0)
                {
                    var first = datalists.First().First();
                    GetSourceColumnTargetColumnMap(first, sourceColInfos, targetColumnMapToSource);
                }
                #endregion

                yield return GetDataTransferSqlInfos(
                    datalists,
                    sourcePrimaryKeys,
                    targetTableStatement,
                    targetColInfos,
                    targetDbType,
                    targetColumnMapToSource
                );

                if (queryEnd)
                {
                    LoggerHelper.LogCritical($"queryEnd:{queryEnd}, 查询结束");
                    yield break;
                }
                pageIndex += queryConnCount;
            }
        }

        /// <summary>
        /// 获取数据迁移的SQL语句, 包含删除已存在的数据和插入新数据的SQL语句; 目标表不存在则创建
        /// </summary>
        /// <param name="sourceConnStr"></param>
        /// <param name="sourceTableName"></param>
        /// <param name="targetConn"></param>
        /// <param name="targetTable"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<List<TableSqlInfo>> GetDataTransferSqlInfosByDataReaderAsync(
            string sourceConnStr,
            string sourceTableName,
            IDbConnection targetConn,
            string? targetTable = null,
            DataFilter? filter = null)
        {
            #region 获取源表的所有字段信息
            var sourceDbType = GetDbType(sourceConnStr);
            var sourceTableStatement = GetTableStatement(sourceTableName, sourceDbType);

            IEnumerable<ColumnInfo> sourceColInfos = await GetTableColumnsInfoAsync(sourceConnStr, sourceTableName);
            string[] sourcePrimaryKeys = sourceColInfos.Where(x => x.IsPK == 1 && !string.IsNullOrWhiteSpace(x.ColumnCode)).Select(x => x.ColumnCode).ToArray();
            if (sourcePrimaryKeys.Length == 0)
            {
                sourcePrimaryKeys = [sourceColInfos.First().ColumnCode];
            }
            #endregion

            #region 获取表在目标库中的所有数据的主键值集合
            var targetDbType = GetDbType(targetConn.ConnectionString);
            if (string.IsNullOrWhiteSpace(targetTable))
            {
                targetTable = sourceTableName;
            }

            string targetTableStatement = GetTableStatement(targetTable, targetDbType);

            if (targetDbType == DatabaseType.SqlServer)
            {
                targetTableStatement = $"[dbo].{targetTableStatement}";
            }

            // 目标数据表不存在则创建
            await CreateTableIfNotExistAsync(targetConn, targetTable, sourceColInfos);
            #endregion

            if (sourceTableName.StartsWith('_'))
            {
                yield break;
            }

            #region 获取目标表的信息
            IEnumerable<ColumnInfo> targetColInfos = await GetTableColumnsInfoAsync(targetConn, targetTable);
            string[] sourceFields = sourceColInfos.Select(x => x.ColumnCode).ToArray();
            targetColInfos = targetColInfos.Where(x => sourceFields.Contains(x.ColumnCode, StringComparer.OrdinalIgnoreCase));
            #endregion

            Queue<List<IDictionary<string, object?>>> _transferQueue = new();
            bool _stopEnqueue = false;
            bool _queryCompleteCompleted = false;
            const int _transferQueueMaxLength = 100;
            // 目标字段和源字段的映射
            Dictionary<string, string> targetColumnMapToSource = [];
            while (true)
            {
                //ConcurrentBag<IEnumerable<IDictionary<string, object>>> datalists = [];
                DateTime start = DateTime.Now;
                #region 获取100组数据
                List<IDictionary<string, object?>> datalist = [];
                Task queryTask = Task.Run(async () =>
                {
                    using var sourceConn = GetDbConnection(sourceConnStr);
                    try
                    {
                        using var reader = await sourceConn.ExecuteReaderAsync($"select * from {sourceTableStatement}", commandTimeout: 60 * 60);
                        int queryedRecordsCount = 0;
                        while (true)
                        {
                            if (queryedRecordsCount == 0)
                            {
                                start = DateTime.Now;
                            }
                            if (sourceConn.State == ConnectionState.Closed)
                            {
                                sourceConn.Open();
                            }
                            bool readed = reader.Read();
                            if (!readed)
                            {
                                break;
                            }
                            IDictionary<string, object?> record = new Dictionary<string, object?>();
                            int sourceColIndex = 0;
                            foreach (var sourceColInfo in sourceColInfos)
                            {
                                var val = reader.GetValue(sourceColIndex);
                                if (val.GetType() == typeof(DBNull))
                                {
                                    val = null;
                                }
                                record[sourceColInfo.ColumnCode] = val;
                                sourceColIndex++;
                            }
                            datalist.Add(record);
                            queryedRecordsCount++;

                            if (datalist.Count > 0 && datalist.Count % 1000 == 0)
                            {
                                _transferQueue.Enqueue(datalist);
                                datalist = [];

                                if (_transferQueue.Count >= _transferQueueMaxLength)
                                {
                                    DateTime t1 = DateTime.Now;
                                    LoggerHelper.LogInformation($"已获取源表{queryedRecordsCount}条数据(队列长度{_transferQueue.Count}到达队列最大长度{_transferQueueMaxLength}), 耗时:{DateTimeHelper.FormatSeconds((t1 - start).TotalSeconds)}");
                                    _stopEnqueue = true;
                                }
                            }

                            int waitedSeconds = 0;
                            while (_stopEnqueue)
                            {
                                await Task.Delay(1000);
                                waitedSeconds++;
                                if (waitedSeconds == 1 || waitedSeconds % 10 == 0)
                                {
                                    LoggerHelper.LogInformation($"数据迁移中, 暂停查询, 已经等待{DateTimeHelper.FormatSeconds(waitedSeconds)}");
                                }
                                if (_transferQueue.Count < _transferQueueMaxLength)
                                {
                                    _stopEnqueue = false;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerHelper.LogInformation($"查询数据出错sourceTable: {sourceTableName}");
                        LoggerHelper.LogInformation(ex.ToString());
                        LoggerHelper.LogError($"连接状态:{sourceConn.State};超时时间:{sourceConn.ConnectionTimeout}");
                    }
                    _queryCompleteCompleted = true;
                });
                #endregion

                List<List<IDictionary<string, object?>>> datalists = [];
                while (true)
                {
                    if (_transferQueue.TryDequeue(out List<IDictionary<string, object?>> list))
                    {
                        datalists.Add(list);
                        // 一次取10个元素(每个1000条数据, 供10000)
                        if (datalists.Count > 0 && datalists.Count % 20 == 0)
                        {
                            #region 数据源字段名和目标表字段的映射关系
                            if (targetColumnMapToSource.Count == 0)
                            {
                                var first = datalists.First().First();
                                GetSourceColumnTargetColumnMap(first, sourceColInfos, targetColumnMapToSource);
                            }
                            #endregion

                            yield return GetDataTransferSqlInfos(
                                datalists,
                                sourcePrimaryKeys,
                                targetTableStatement,
                                targetColInfos,
                                targetDbType,
                                targetColumnMapToSource
                            );

                            _stopEnqueue = false;
                            // 重置datalists
                            datalists.Clear();
                        }
                    }
                    else if (_queryCompleteCompleted)
                    {
                        if (datalists.Count > 0 || datalist.Count > 0)
                        {
                            datalists.Add(datalist);
                            LoggerHelper.LogInformation($"最后一次迁移, 剩余数据: {datalists.Select(x => x.Count).Sum()}");
                            #region 数据源字段名和目标表字段的映射关系
                            if (targetColumnMapToSource.Count == 0)
                            {
                                var first = datalists.First().First();
                                GetSourceColumnTargetColumnMap(first, sourceColInfos, targetColumnMapToSource);
                            }
                            #endregion

                            yield return GetDataTransferSqlInfos(
                                datalists,
                                sourcePrimaryKeys,
                                targetTableStatement,
                                targetColInfos,
                                targetDbType,
                                targetColumnMapToSource
                            );
                        }
                        Console.WriteLine($"队列中无数据并且查询状态标记为已结束, 后台查询任务状态:{queryTask.Status}");
                        yield break;
                    }
                    else
                    {
                        await Task.Delay(100);
                    }
                }
            }
        }
        /// <summary>
        /// 获取数据迁移的SQL语句, 包含删除已存在的数据和插入新数据的SQL语句
        /// </summary>
        /// <param name="datalists"></param>
        /// <param name="sourcePrimaryKeys"></param>
        /// <param name="targetTableStatement"></param>
        /// <param name="targetColInfos"></param>
        /// <param name="targetDbType"></param>
        /// <param name="targetColumnMapToSource">目标表字段和源字段的映射关系</param>
        /// <returns></returns>
        static List<TableSqlInfo> GetDataTransferSqlInfos(
            IEnumerable<IEnumerable<IDictionary<string, object?>>> datalists,
            string[] sourcePrimaryKeys,
            string targetTableStatement,
            IEnumerable<ColumnInfo> targetColInfos,
            DatabaseType targetDbType,
            Dictionary<string, string> targetColumnMapToSource
        )
        {
            List<TableSqlInfo> tableSqlInfos = [];
            // 为数据源中的每一条数据生成对应的insert语句的部分
            foreach (var datalist in datalists)
            {
                #region 创建对应的insert, delete语句
                var insertSqlBuilder = new StringBuilder();
                var deleteExistSqlBuilder = new StringBuilder();
                // sql语句参数
                Dictionary<string, object?> parameters = [];
                Dictionary<string, object?> deleteExistParameters = [];

                // 参数名后缀(第一条数据的Name字段, 参数名为Name1, 第二条为Name2, ...)
                var random = 0;

                foreach (IDictionary<string, object?> dataItem in datalist)
                {
                    var dataItemPkValues = sourcePrimaryKeys.Select(x => dataItem[x]?.ToString() ?? string.Empty).ToArray();
                    string dataItemPkValue = string.Join(',', dataItemPkValues);

                    string targetDbVarFlag = GetDbParameterFlag(targetDbType);
                    string deleteSqlCurrentRecordPart = GenerateDeleteExistSql(targetTableStatement, sourcePrimaryKeys, dataItemPkValues, deleteExistParameters, targetDbVarFlag, random);
                    deleteExistSqlBuilder.Append(deleteSqlCurrentRecordPart);

                    // "@Name1, @Age1"
                    string valuesStatement = GenerateRecordValuesStatement(dataItem, targetColInfos, parameters, targetDbVarFlag, targetColumnMapToSource, random);
                    // ("@Name1, :Age1"),
                    string currentRecordSql = $"({valuesStatement}),{Environment.NewLine}";

                    insertSqlBuilder.Append($"{currentRecordSql}");
                    random++;
                }

                // columnsStatement =           "Name, Age"
                string targetColumnsStatement = GetFieldsStatement(targetColInfos, targetDbType);
                // 最终的insert语句
                var targetTableAllDataInsertSql = GetBatchInsertSql(targetTableStatement, targetColumnsStatement, insertSqlBuilder.ToString(), targetDbType);
                var targetTableDeleteExistSql = GetBatchDeleteSql(targetTableStatement, deleteExistSqlBuilder.ToString(), sourcePrimaryKeys, targetDbType);
                #endregion
                var tableSqlInfo = new TableSqlInfo
                {
                    TableName = GetTableOriginName(targetTableStatement),

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
                tableSqlInfos.Add(tableSqlInfo);
            }
            return tableSqlInfos;
        }
        /// <summary>
        /// 获取一个库中的所有表
        /// </summary>
        /// <param name="conn"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>

        public static async Task<IEnumerable<string>> GetAllTablesAsync(IDbConnection conn)
        {
            string connstr = conn.ConnectionString;
            var dbtype = GetDbType(connstr);

            var tables = dbtype switch
            {
                DatabaseType.MySql => await conn.QueryAsync($"select TABLE_NAME from information_schema.`TABLES` WHERE table_schema='{conn.Database}'"),
                DatabaseType.SqlServer => await conn.QueryAsync("SELECT name AS TABLE_NAME FROM sys.tables;"),
                DatabaseType.Oracle => await conn.QueryAsync($"SELECT TABLE_NAME FROM user_tables"),
                DatabaseType.Sqlite => await conn.QueryAsync($"SELECT name as TABLE_NAME FROM sqlite_master WHERE type='table';"),
                DatabaseType.Dm => await conn.QueryAsync($"SELECT TABLE_NAME FROM user_tables"),
                _ => throw new NotImplementedException(),
            };
            return tables.Select(x => (x as IDictionary<string, object> ?? throw new Exception("DapperRow转换为字典失败"))["TABLE_NAME"].ToString());
        }
        /// <summary>
        /// 获取一个库中的所有表
        /// </summary>
        /// <param name="connStr"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<dynamic>> GetAllTablesAsync(string connStr)
        {
            if (!connStr.Contains(' ') && !connStr.Contains(';'))
            {
                try
                {
                    connStr = await SecurityHelper.AesDecryptAsync(connStr);
                }
                catch (Exception)
                {
                    throw new Exception($"无效的数据库连接字符串:{connStr}");
                }
            }
            var conn = GetDbConnection(connStr);
            return await GetAllTablesAsync(conn);
        }
        /// <summary>
        /// 获取数据库名
        /// </summary>
        /// <param name="conn"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static string GetDatabaseName(IDbConnection conn)
        {
            return GetDbType(conn.ConnectionString) switch
            {
                DatabaseType.MySql or DatabaseType.Pg or DatabaseType.SqlServer or DatabaseType.Sqlite => conn.Database,
                DatabaseType.Oracle => GetOracleDatabaseName(conn),
                DatabaseType.Dm => GetDmDatabaseName(conn),
                _ => throw new NotImplementedException(),
            };
        }
        static string GetOracleDatabaseName(IDbConnection conn) => Regex.Match(conn.ConnectionString, @"user\s+id\s*=\s*(?<user>\w+)", RegexOptions.IgnoreCase).Groups["user"].Value;
        static string GetDmDatabaseName(IDbConnection conn) => Regex.Match(conn.ConnectionString, @"userid\s*=\s*(?<user>\w+)", RegexOptions.IgnoreCase).Groups["user"].Value;
        /// <summary>
        /// 表是否存在
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="table"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static async Task<bool> TableExistAsync(IDbConnection conn, string table)
        {
            var databaseType = GetDbType(conn.ConnectionString);
            var checkSql = string.Empty;
            switch (databaseType)
            {
                case DatabaseType.Pg:
                    checkSql = $"SELECT COUNT(1) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = \"lower\"('{table}');";
                    break;
                case DatabaseType.MySql:
                    checkSql = $"select count(*) from information_schema.tables where table_name='{table}' and table_schema=(select database())";
                    break;
                case DatabaseType.Oracle:
                    string dbuser = GetOracleDatabaseName(conn);
                    checkSql = $"select count(*) from all_tables where owner=upper('{dbuser}') and table_name=upper('{table}')";
                    break;
                case DatabaseType.SqlServer:
                    checkSql = $"select count(*) from sysobjects where id = object_id('{table}') and OBJECTPROPERTY(id, N'IsUserTable') = 1";
                    break;
                case DatabaseType.Dm:
                    dbuser = GetDmDatabaseName(conn);
                    checkSql = $"SELECT COUNT(1) FROM ALL_TABLES WHERE OWNER=UPPER('{dbuser}') AND TABLE_NAME=UPPER('{table}')";
                    break;
                case DatabaseType.Sqlite:
                    checkSql = $"SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name='{table}';";
                    break;
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
        /// <param name="table"></param>
        /// <param name="dbType"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="filters"></param>
        /// <param name="queryCondition"></param>
        /// <param name="queryConditionParameters"></param>
        /// <param name="orderRules">排序规则</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="Exception"></exception>
        public static string GetPagedSql(string table, DatabaseType dbType, int pageIndex, int pageSize, DataFilter? filters, List<OrderField>? orderRules, out string queryCondition, out Dictionary<string, object?> queryConditionParameters)
        {
            #region 排序表达式
            orderRules ??= [new() { FieldName = "id", IsAsc = false }];

            string orderClause = string.Empty;
            foreach (var orderRule in orderRules)
            {
                ValideParameter(orderRule.FieldName);
                orderClause += $"{orderRule.FieldName} {(orderRule.IsAsc ? "asc" : "desc")},";
            }
            if (!string.IsNullOrWhiteSpace(orderClause))
            {
                orderClause = $" order by {orderClause.TrimEnd(',')}";
            }
            #endregion

            #region 处理过滤条件
            queryCondition = string.Empty;
            queryConditionParameters = [];
            if (filters is not null)
            {
                if (filters.FilterItems is not null)
                {
                    IEnumerable<object> filterItems = filters.FilterItems.Where(x => !string.IsNullOrWhiteSpace(x.FieldName)).Cast<object>();
                    var filterGroup = new FilterGroup(filterItems, SqlLogic.And)
                    .AddKeywordsQuerying(filters.Keywords.Fields, filters.Keywords.Value);
                    var sqlInfo = filterGroup.BuildConditions(dbType);
                    if (!string.IsNullOrWhiteSpace(sqlInfo.Sql))
                    {
                        queryCondition = $" WHERE {sqlInfo.Sql}";
                        queryConditionParameters = sqlInfo.Parameters;
                    }
                }
            }
            #endregion

            string baseSqlTxt = $"select * from {table}{queryCondition}";

            #region 不同数据库对应的SQL语句
            string sql = dbType switch
            {
                DatabaseType.Oracle => $@"select * from 
    (
    select t.*,rownum no from 
        (
        {baseSqlTxt}{orderClause}
        ) t 
    )
where no>({pageIndex}-1)*{pageSize} and no<=({pageIndex})*{pageSize}",
                DatabaseType.MySql => $@"{baseSqlTxt}{orderClause} limit {(pageIndex - 1) * pageSize},{pageSize}",
                DatabaseType.SqlServer => $"{baseSqlTxt}{(string.IsNullOrEmpty(orderClause) ? " order by id desc" : orderClause)} OFFSET ({pageIndex}-1)*{pageSize} ROWS FETCH NEXT {pageSize} ROW ONLY",
                DatabaseType.Sqlite => $@"{baseSqlTxt}{orderClause} limit {pageSize} offset {(pageIndex - 1) * pageSize}",
                DatabaseType.Pg => $@"{baseSqlTxt}{orderClause} limit {pageSize} offset {(pageIndex - 1) * pageSize}",
                _ => $"""
                SELECT * FROM (
                    SELECT a.*, ROWNUM rnum FROM (
                        {baseSqlTxt}{orderClause}
                    ) a WHERE ROWNUM <= {pageIndex * pageSize}
                ) WHERE rnum > {(pageIndex - 1) * pageSize}
                """

            };
            #endregion

            return sql;
        }
        /// <summary>
        /// 获取分页查询的SQL语句 - 多表联查
        /// </summary>
        /// <param name="queryTablesDto"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static string GetPagedSql(QueryTablesInDto queryTablesDto, out Dictionary<string, object?> parameters)
        {
            SqlInfo querySqlInfo = QuerySqlBuilder
                .UseDatabase(DatabaseType.MySql)
                .Select(queryTablesDto.Select.TableName)
                .LeftJoins(queryTablesDto.LeftJoins)
                .Where(queryTablesDto.Where)
                .OrderBy([.. queryTablesDto.OrderBy])
                .Page(new())
                .Build();
            parameters = querySqlInfo.Parameters;
            return querySqlInfo.Sql;
        }
        /// <summary>
        /// 数据库分页数据
        /// </summary>
        /// <param name="table"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="dbConn"></param>
        /// <param name="filters"></param>
        /// <param name="orderRules">排序规则</param>
        /// <returns></returns>
        public static async Task<PagedData<T>> QueryPagedDataAsync<T>(string table, int pageIndex, int pageSize, IDbConnection dbConn, DataFilter? filters, List<OrderField>? orderRules)
        {
            // TODO: 数据库类型 作为公共属性
            var dbType = GetDbType(dbConn.ConnectionString);

            string sql = GetPagedSql(table, dbType, pageIndex, pageSize, filters, orderRules, out string condition, out Dictionary<string, object?> parameters);

            string allCountSqlTxt = $"select count(*) from {table}{condition}";
            var allCount = await dbConn.ExecuteScalarAsync<int>(allCountSqlTxt, parameters);
            IEnumerable<T> data;
            if (typeof(T) == typeof(IDictionary<string, object>))
            {
                data = (await dbConn.QueryAsync(sql, parameters)).Cast<T>();
            }
            else
            {
                data = await dbConn.QueryAsync<T>(sql, parameters);
            }
            using var dataReader = await dbConn.ExecuteReaderAsync(sql, parameters);
            return new PagedData<T> { Data = data, Count = allCount, TotalPages = (allCount + pageSize - 1) / pageSize };
        }

        /// <summary>
        /// 获取insert语句的字段部分 Name,Age
        /// </summary>
        /// <param name="colInfos"></param>
        /// <param name="dbtype"></param>
        /// <returns></returns>
        static string GetFieldsStatement(IEnumerable<ColumnInfo> colInfos, DatabaseType dbtype)
        {
            var columnBuilder = new StringBuilder();
            var colCodes = colInfos.Select(x => x.ColumnCode);
            colCodes = GetTableStatements(colCodes, dbtype);

            return string.Join(',', colCodes);
        }
        /// <summary>
        /// 获取字段值
        /// </summary>
        /// <param name="value"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        static object? GetColumnValue(object value, string type)
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
        /// <param name="pv"></param>
        /// <param name="col"></param>
        /// <returns></returns>
        static dynamic? GetColumnValue(object pv, ColumnInfo? col)
        {
            dynamic? pVal = null;
            // 实际是字节数组这里获得的是字节数组转换为base64字符串的结果
            if (pv is JToken pvJToken && pvJToken.Type == JTokenType.String && col is not null && !string.IsNullOrWhiteSpace(col.ColumnType) && col.ColumnType.Contains("blob", StringComparison.OrdinalIgnoreCase))
            {
                byte[] bytes = new byte[pvJToken.ToString().Length];
                if (Convert.TryFromBase64String(pvJToken.ToString(), bytes, out _))
                {
                    pVal = bytes;
                }
            }
            else if (pv is JToken pvJToken2 && pvJToken2.Type == JTokenType.String && col is not null && !string.IsNullOrWhiteSpace(col.ColumnType) && col.ColumnType.Contains("time", StringComparison.OrdinalIgnoreCase))
            {
                if (DateTime.TryParse(pvJToken2.ToString(), out DateTime timeValue))
                {
                    pVal = timeValue;
                }
                else
                {
                    pVal = DateTime.Now;
                }
            }

            // BOOKMARK: ※※※※ 转为dynamic就不需要转换为具体的类型了 ※※※※
            pVal ??= JObject.FromObject(pv).ToObject<dynamic>();

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
                // WARNING: Oracle数据库批量插入使用INSERT ALL INTO ... SELECT * FROM DUAL 结尾不能有分号";"!
                var type when type == DatabaseType.Oracle || type == DatabaseType.Dm => $"""
                INSERT ALL
                {Regex.Replace(Regex.Replace(valuesStatement, @"\(:", m => $"INTO {tableStatement}({columnsStatement}) VALUES{m.Value}"), @"\),", m => ")")}
                SELECT * FROM DUAL
                """,
                DatabaseType.MySql => $"SET foreign_key_checks=0;{Environment.NewLine}insert into {tableStatement}({columnsStatement}) values{Environment.NewLine}{valuesStatement};{Environment.NewLine}SET foreign_key_checks=1;",
                _ => $"insert into {tableStatement}({columnsStatement}) values{Environment.NewLine}{valuesStatement};"
            };
        }
        /// <summary>
        /// 获取批量删除的SQL语句
        /// </summary>
        /// <param name="table"></param>
        /// <param name="deleteStatement"></param>
        /// <param name="pks"></param>
        /// <param name="dbType"></param>
        /// <returns></returns>
        public static string GetBatchDeleteSql(string table, string deleteStatement, string[] pks, DatabaseType dbType)
        {
            if (string.IsNullOrWhiteSpace(deleteStatement))
            {
                return string.Empty;
            }
            if (pks.Length > 1)
            {
                return dbType switch
                {
                    var type when type == DatabaseType.Oracle || type == DatabaseType.Dm => $"BEGIN{Environment.NewLine}{deleteStatement}{Environment.NewLine}END;",
                    DatabaseType.MySql => $"SET foreign_key_checks=0;{Environment.NewLine}{deleteStatement}{Environment.NewLine}SET foreign_key_checks=1;",
                    _ => deleteStatement
                };
            }
            else
            {
                return dbType switch
                {
                    DatabaseType.MySql => $"SET foreign_key_checks=0;{Environment.NewLine}delete from {table} where {pks.First()} in({deleteStatement.Trim(',', '\r', '\n', ';')});{Environment.NewLine}SET foreign_key_checks=1;",
                    _ => $"delete from {table} where {pks.First()} in({deleteStatement.Trim(',', '\r', '\n', ';')})",
                };
            }
        }

        /// <summary>
        /// 获取SQL语句中带引号的数据库名, 表名, 字段
        /// </summary>
        /// <param name="name"></param>
        /// <param name="dbType"></param>
        /// <returns></returns>
        static string GetTableStatement(string name, DatabaseType dbType)
        {
            name = GetTableOriginName(name);
            return dbType switch
            {
                DatabaseType.MySql => $"`{name}`",
                DatabaseType.Oracle => $"\"{name}\"".ToUpper(),
                DatabaseType.Dm => $"\"{name}\"".ToUpper(),
                DatabaseType.SqlServer => $"[{name}]",
                DatabaseType.Pg => $"\"{name}\"".ToLower(),
                DatabaseType.Sqlite => $"\"{name}\"",
                _ => name
            };
        }
        static string GetTableOriginName(string tableName)
        {
            // 去掉数据库名
            if (tableName.Contains('.'))
            {
                tableName = tableName[(tableName.LastIndexOf('.') + 1)..];
            }
            // 去掉引号
            tableName = tableName.Trim('[', ']', '`', '"');
            return tableName;
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
                DatabaseType.Dm => names.Select(name => $"\"{name}\""),
                DatabaseType.SqlServer => names.Select(name => $"[{name}]"),
                DatabaseType.Pg => names.Select(name => $"\"{name}\""),
                DatabaseType.Sqlite => names.Select(name => $"\"{name}\""),
                _ => names
            };
        }

        /// <summary>
        /// 根据一条数据 生成对应的insert语句的值(参数)部分 @Name1,@Age1
        /// </summary>
        /// <param name="sourceRecord"></param>
        /// <param name="targetColInfos"></param>
        /// <param name="parameters"></param>
        /// <param name="dbVarFlag"></param>
        /// <param name="targetColumnMapToSource"></param>
        /// <param name="random"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        static string GenerateRecordValuesStatement(object sourceRecord,
            IEnumerable<ColumnInfo> targetColInfos,
            Dictionary<string, object?> parameters,
            string dbVarFlag,
            Dictionary<string, string> targetColumnMapToSource,
            int random = 0
        )
        {
            var valueBuilder = new StringBuilder();
            if (sourceRecord is DataRow dataItem)
            {
                // 处理一条数据
                foreach (var colInfo in targetColInfos)
                {
                    var columnCode = colInfo.ColumnCode;
                    if (string.IsNullOrWhiteSpace(columnCode))
                    {
                        throw new Exception($"包含空字段");
                    }
                    string dataKey = targetColumnMapToSource is null || targetColumnMapToSource.Count == 0 ? columnCode : targetColumnMapToSource[columnCode];
                    var columnValue = dataItem[dataKey];
                    var columnType = colInfo.ColumnType ?? string.Empty;
                    var parameterName = $"{columnCode}{random}";

                    valueBuilder.Append($"{dbVarFlag}{parameterName},");

                    parameters.Add(parameterName, GetColumnValue(columnValue, columnType));
                }
            }
            else if (sourceRecord is IDictionary<string, object> recordDictionary)
            {
                // 遍历targetColInfos目的是以目标表的字段为基准(有哪些字段用哪些字段, 并且生成的SQL语句字段顺序与目标表保持一致)
                foreach (var targetColInfo in targetColInfos)
                {
                    var sourceKey = recordDictionary.Keys.FirstOrDefault(x => x.Equals(targetColInfo.ColumnCode, StringComparison.OrdinalIgnoreCase));
                    object? parameterValue = null;
                    if (sourceKey is null)
                    {
                        parameterValue = GetColumnDefaultValue(targetColInfo);
                    }
                    else
                    {
                        parameterValue = recordDictionary[sourceKey];
                    }
                    //if (sourceColumnMapToTargetColumn is not null && sourceColumnMapToTargetColumn.Any() && !sourceColumnMapToTargetColumn.TryGetValue(sourceKey, out _))
                    //{
                    //    continue;
                    //}
                    var paramName = $"{targetColInfo.ColumnCode}{random}";
                    valueBuilder.Append($"{dbVarFlag}{paramName},");
                    if (targetColInfo.ColumnCSharpType == "datetime" && parameterValue is string)
                    {
                        parameterValue = string.IsNullOrWhiteSpace($"{parameterValue}") ? null : DateTime.Parse(parameterValue.ToString());
                    }
                    parameters.Add(paramName, parameterValue);
                }
            }
            else
            {
                IEnumerable<JProperty> properties = sourceRecord is JObject recordJObj ? recordJObj.Properties() : JObject.FromObject(sourceRecord).Properties() ?? throw new Exception($"record不是DataRow或者记录对应的对象类型");

                foreach (var targetColInfo in targetColInfos)
                {
                    var sourceKey = properties.FirstOrDefault(x => x.Name.Equals(targetColInfo.ColumnCode, StringComparison.OrdinalIgnoreCase));
                    dynamic? parameterValue = null;
                    if (sourceKey is null)
                    {
                        parameterValue = GetColumnDefaultValue(targetColInfo);
                    }
                    else
                    {
                        parameterValue = GetColumnValue(sourceKey, targetColInfo); ;
                    }
                    //if (sourceColumnMapToTargetColumn is not null && sourceColumnMapToTargetColumn.Any() && !sourceColumnMapToTargetColumn.TryGetValue(sourceKey.Name, out _))
                    //{
                    //    continue;
                    //}
                    var paramName = $"{targetColInfo.ColumnCode}{random}";
                    valueBuilder.Append($"{dbVarFlag}{paramName},");
                    if (targetColInfo.ColumnCSharpType == "datetime" && parameterValue is string)
                    {
                        parameterValue = string.IsNullOrWhiteSpace($"{parameterValue}") ? null : DateTime.Parse(parameterValue.ToString());
                    }
                    parameters.Add(paramName, parameterValue);
                }
            }
            return valueBuilder.ToString().TrimEnd(',');
        }
        static object? GetColumnDefaultValue(ColumnInfo targetColInfo)
        {
            if (targetColInfo.ColumnCSharpType is null)
            {
                if (string.IsNullOrWhiteSpace(targetColInfo.DefaultValue))
                {
                    return null;
                }
                else
                {
                    return targetColInfo.DefaultValue;
                }
            }
            else
            {
                if (targetColInfo.ColumnCSharpType.Contains("time"))
                {
                    return DateTime.Now;
                }
                else if (targetColInfo.ColumnCSharpType.Contains("int") || targetColInfo.ColumnCSharpType.Contains("long")
                    || targetColInfo.ColumnCSharpType.Contains("float")
                    || targetColInfo.ColumnCSharpType.Contains("double")
                    || targetColInfo.ColumnCSharpType.Contains("decimal")
                    )
                {
                    return 0;
                }
                else
                {
                    return string.Empty;
                }
            }
        }
        /// <summary>
        /// 创建表
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="columnInfos"></param>
        /// <param name="tablename"></param>
        /// <returns></returns>
        public static async Task CreateTableAsync(IDbConnection conn, IEnumerable<ColumnInfo> columnInfos, string tablename)
        {
            #region 确定数据库类型和表名
            var dbType = GetDbType(conn.ConnectionString);
            if (tablename.Contains('.'))
            {
                var tableInfo = tablename.Split('.');
                tablename = tableInfo.Last();
                string schema = tableInfo.First();
                if (schema != "dbo" && schema != "public")
                {
                    try
                    {
                        conn.ChangeDatabase(schema);
                    }
                    catch (Exception ex)
                    {
                        LoggerHelper.LogError($"尝试切换数据库{schema}失败: {ex.Message}");
                    }
                }
            }
            #endregion

            string createTableStatement = GetCreateTableStatement(tablename, columnInfos, dbType);
            int result = await conn.ExecuteAsync(createTableStatement);
            LoggerHelper.LogInformation($"已创建表{tablename}: {result}");
        }
        /// <summary>
        /// 生成创建表的SQL语句
        /// </summary>
        /// <param name="table">要生成的表的名称</param>
        /// <param name="columns">要生成的数据表的字段信息(可以来源于任何数据库, 主要是根据ColumnCSharpType)</param>
        /// <param name="dbType">要生成什么数据库的表</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public static string GetCreateTableStatement(string table, IEnumerable<ColumnInfo> columns, DatabaseType dbType)
        {
            table = GetTableOriginName(table);

            string columnsDdl = GetColumnDdlStatement(columns, dbType);
            columnsDdl = $"{Environment.NewLine}{columnsDdl}";

            string tableStatement = GetTableStatement(table, dbType);
            if (dbType == DatabaseType.SqlServer)
            {
                tableStatement = $"[dbo].{tableStatement}";
            }

            return dbType switch
            {
                DatabaseType.MySql => $"CREATE TABLE {tableStatement} ({columnsDdl}) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci ROW_FORMAT = Dynamic;",
                DatabaseType.SqlServer => $"CREATE TABLE {tableStatement} ({columnsDdl});",
                DatabaseType.Pg => $"CREATE TABLE {tableStatement} ({columnsDdl})",
                DatabaseType.Oracle => $"CREATE TABLE {tableStatement.ToUpper()} ({columnsDdl})",
                DatabaseType.Dm => $"CREATE TABLE {tableStatement.ToUpper()} ({columnsDdl})",
                DatabaseType.Sqlite => $"CREATE TABLE {tableStatement} ({columnsDdl});",
                _ => throw new NotSupportedException($"Unsupported database type: {dbType}")
            };
        }
        /// <summary>
        /// 获取字段的DDL表达式
        /// </summary>
        /// <param name="columns"></param>
        /// <param name="dbType"></param>
        /// <returns></returns>
        static string GetColumnDdlStatement(IEnumerable<ColumnInfo> columns, DatabaseType dbType)
        {
            var columnDdlBuilder = new StringBuilder();
            int pkCount = columns.Count(x => x.IsPK == 1);
            bool hasAutoIncreamentPk = false;
            foreach (var column in columns)
            {
                string columnCode = column.ColumnCode;
                if (dbType == DatabaseType.Oracle || dbType == DatabaseType.Dm)
                {
                    columnCode = columnCode.ToUpper();
                }
                else if (dbType == DatabaseType.Pg)
                {
                    columnCode = columnCode.ToLower();
                }
                // 声明字段名称和字段类型
                columnDdlBuilder.Append($"  {GetTableStatement(column.ColumnCode, dbType)} {GetColumnType(column, dbType)}");

                if (column.IsPK == 1 && column.ColumnCSharpType == "int" && pkCount == 1)
                {
                    // 记录当前表有唯一自增主键
                    hasAutoIncreamentPk = true;

                    string pkStatement = GetAutoIncrementPkDdl(dbType);
                    columnDdlBuilder.Append(pkStatement);
                }

                if (column.IsNullable == 0 || column.IsPK == 1)
                {
                    columnDdlBuilder.Append(" NOT NULL");
                }

                if (!string.IsNullOrWhiteSpace($"{column.DefaultValue}"))
                {
                    columnDdlBuilder.Append($" DEFAULT {column.DefaultValue}");
                }

                columnDdlBuilder.AppendLine(",");
            }
            if (!hasAutoIncreamentPk)
            {
                string endStatement = dbType == DatabaseType.MySql ? " USING BTREE," : ",";
                string[] pks = columns.Where(x => x.IsPK == 1).Select(x => GetTableStatement(x.ColumnCode, dbType)).ToArray();
                // 构建主键声明语句
                string pksDeclaration = string.Empty;
                if (pks.Length > 0)
                {
                    pksDeclaration = $"PRIMARY KEY ({string.Join(',', pks)}){endStatement}";
                    columnDdlBuilder.AppendLine($"  {pksDeclaration}");
                }
            }
            // Remove the last comma
            if (columnDdlBuilder.Length > 0)
            {
                int removedLength = $",{Environment.NewLine}".Length;
                columnDdlBuilder.Remove(columnDdlBuilder.Length - removedLength, removedLength);
            }

            return columnDdlBuilder.ToString();
        }
        private static string GetAutoIncrementPkDdl(DatabaseType dbType)
        {
            return dbType switch
            {
                DatabaseType.SqlServer => " PRIMARY KEY IDENTITY(1,1)",
                DatabaseType.MySql => " PRIMARY KEY AUTO_INCREMENT",

                // Pg,Oracle自增都是GENERATED BY DEFAULT AS IDENTITY
                DatabaseType.Pg => " PRIMARY KEY GENERATED BY DEFAULT AS IDENTITY (INCREMENT 1 MINVALUE  1 MAXVALUE 2147483647 START 1 CACHE 1)",
                DatabaseType.Oracle => " GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY",

                DatabaseType.Dm => " IDENTITY(1, 1) PRIMARY KEY",

                DatabaseType.Sqlite => " PRIMARY KEY AUTOINCREMENT",
                _ => throw new NotSupportedException($"Unsupported database type: {dbType}")
            };
        }
        private static string GetColumnType(ColumnInfo column, DatabaseType dbType)
        {
            return dbType switch
            {
                DatabaseType.SqlServer => GetSqlServerColumnType(column),
                DatabaseType.MySql => GetMySqlColumnType(column),
                DatabaseType.Pg => GetPostgreSqlColumnType(column),
                DatabaseType.Oracle => GetOracleColumnType(column),
                DatabaseType.Sqlite => GetSqliteColumnType(column),
                DatabaseType.Dm => GetDmColumnType(column),
                _ => throw new NotSupportedException($"Unsupported database type: {dbType}")
            };
        }

        private static string GetSqlServerColumnType(ColumnInfo column)
        {
            if (string.IsNullOrWhiteSpace(column.ColumnLength) || !int.TryParse(column.ColumnLength, out int length))
            {
                length = -1;
            }
            return column.ColumnCSharpType switch
            {
                "int" => "INT",
                "long" => "BIGINT",
                "float" => "FLOAT",
                "double" => "FLOAT",
                "decimal" => "DECIMAL(18,2)",
                "string" => length > 0 && length <= 2000 ? $"NVARCHAR({length})" : "NVARCHAR(MAX)",
                "bool" => "bit",
                "datetime" => "datetime2(7)",
                "byte[]" => "varbinary(max)",
                _ => throw new NotSupportedException($"Unsupported column type: {column.ColumnCSharpType}")
            };
        }

        private static string GetMySqlColumnType(ColumnInfo column)
        {
            if (string.IsNullOrWhiteSpace(column.ColumnLength) || !int.TryParse(column.ColumnLength, out int length))
            {
                length = -1;
            }
            return column.ColumnCSharpType switch
            {
                "int" => "int",
                "long" => "bigint",
                "float" => "float",
                "double" => "double",
                "decimal" => "decimal(18,2)",
                "string" => length > 0 && length <= 2000 ? $"VARCHAR({length})" : "TEXT",
                "bool" => "bit",
                "datetime" => "datetime(6)",
                "byte[]" => "longblob",
                _ => throw new NotSupportedException($"Unsupported column type: {column.ColumnCSharpType}")
            };
        }

        private static string GetPostgreSqlColumnType(ColumnInfo column)
        {
            if (string.IsNullOrWhiteSpace(column.ColumnLength) || !int.TryParse(column.ColumnLength, out int length))
            {
                length = -1;
            }
            return column.ColumnCSharpType switch
            {
                "int" => "int4",
                "long" => "int8",
                "float" => "float4",
                "double" => "float8",
                "decimal" => "decimal(18,2)",
                "string" => length > 0 && length <= 2000 ? $"varchar({length})" : "text",
                "bool" => "bool",
                "datetime" => "timestamp(6)",
                "byte[]" => "bytea",
                _ => throw new NotSupportedException($"Unsupported column type: {column.ColumnCSharpType}")
            };
        }

        private static string GetOracleColumnType(ColumnInfo column)
        {
            if (string.IsNullOrWhiteSpace(column.ColumnLength) || !int.TryParse(column.ColumnLength, out int length))
            {
                length = -1;
            }
            return column.ColumnCSharpType switch
            {
                "int" => "NUMBER(10)",
                "long" => "NUMBER(19)",
                "float" => "FLOAT",
                "double" => "NUMBER",
                "decimal" => "DECIMAL(18,2)",
                "string" => length <= 0 || length > 2000 ? "NCLOB" : $"NVARCHAR2({length})",
                "bool" => "NUMBER(1)",
                "datetime" => "TIMESTAMP",
                "byte[]" => "BLOB",
                _ => throw new NotSupportedException($"Unsupported column type: {column.ColumnCSharpType}")
            };
        }
        private static string GetDmColumnType(ColumnInfo column)
        {
            if (string.IsNullOrWhiteSpace(column.ColumnLength) || !int.TryParse(column.ColumnLength, out int length))
            {
                length = -1;
            }
            return column.ColumnCSharpType switch
            {
                "int" => "INT",
                "long" => "BIGINT",
                "float" => "DECIMAL(22,6)",
                "double" => "FLOAT",
                "decimal" => "DECIMAL(18,2)",
                "string" => length * 3 <= 0 || length * 3 > 2000 ? "CLOB" : $"VARCHAR2({length * 3})",
                "bool" => "BIT",
                "datetime" => "TIMESTAMP",
                "byte[]" => "BLOB",
                _ => throw new NotSupportedException($"Unsupported column type: {column.ColumnCSharpType}")
            };
        }
        private static string GetSqliteColumnType(ColumnInfo column)
        {
            return column.ColumnCSharpType switch
            {
                "int" => "INTEGER",
                "long" => "INTEGER",
                "float" => "REAL",
                // Sqlite中INTERGER可以保存小数
                "double" => "INTEGER",
                "decimal" => "INTEGER",
                "string" => "TEXT",
                "bool" => "INTEGER(1)",
                "datetime" => "TEXT(20)",
                "byte[]" => "BLOB",
                _ => throw new NotSupportedException($"Unsupported column type: {column.ColumnCSharpType}")
            };
        }

        /// <summary>
        /// 生成删除已存在的数据的SQL语句信息
        /// </summary>
        /// <param name="table"></param>
        /// <param name="primaryKeys"></param>
        /// <param name="targetPkValues"></param>
        /// <param name="parameters"></param>
        /// <param name="dbVarFlag"></param>
        /// <param name="random"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static string GenerateDeleteExistSql(string table, string[] primaryKeys, string[] targetPkValues, Dictionary<string, object?> parameters, string dbVarFlag, int random = 0)
        {
            if (primaryKeys.Length == 0)
            {
                throw new Exception($"{table}主键不能为空");
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
                var connStr when connStr.Contains("server") && connStr.Contains("userid") => DatabaseType.Dm,
                var connStr when connStr.Contains("server") && connStr.Contains("port") && (connStr.Contains("uid") || connStr.Contains("user id")) => DatabaseType.MySql,
                var connStr when (connStr.Contains("server") && connStr.Contains("user")) || connStr.Contains("initial catalog") || connStr.Contains("mssqllocaldb") => DatabaseType.SqlServer,
                var connStr when connStr.Contains("host") => DatabaseType.Pg,
                var connStr when connStr.Contains("data source") && connStr.Contains("user id") => DatabaseType.Oracle,
                var connStr when connStr.Contains("data source") => DatabaseType.Sqlite,
                _ => throw new Exception($"解析连接字符串的数据库类型失败: {connectionString}")
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
        public static string GetDbParameterFlag(DatabaseType dbType) => dbType == DatabaseType.Oracle || dbType == DatabaseType.Dm ? ":" : "@";
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
            // TODO: 数据超过一定量的时候, 一行一行写入文件; 返回值改为迭代器
            //List<string> targetPkValues = [];
            try
            {
                // 超时数据库连接则会关闭
                //using var reader = await targetConn.ExecuteReaderAsync($"select * from {targetTable}", param: new Dictionary<string, object>(), transaction: trans, commandTimeout: 60 * 60);
                //while (reader.Read())
                //{
                //    string pkValue = string.Join(',', primaryKeys.Select(x => reader[x]));
                //    targetPkValues.Add(pkValue);
                //}
                var pkValues = (await targetConn.QueryAsync<dynamic>($"select {string.Join(',', primaryKeys)} from {targetTable}"));

                return pkValues.Select(x => string.Join(',', (x as IDictionary<string, object>)!.Values)).ToList();
            }
            catch (Exception ex)
            {
                if (IsTableNotExistException(ex))
                {
                    // 表不存在, 生成创建表的语句
                    return ["-1"];
                }
                else
                {
                    LoggerHelper.LogError($"读取表所有记录的主键值异常, 将忽略此异常:{ex}");
                }
            }
            return [];
        }

        /// <summary>
        /// **对比数据**
        /// </summary>
        /// <param name="sourceRecordsData">源数据</param>
        /// <param name="targetRecordsData">要对比的目标数据表中查询出的数据</param>
        /// <param name="sourceIdField">源数据中的标识字段</param>
        /// <returns></returns>
        public static async Task<DataComparedResult> CompareRecordsAsync(IEnumerable<object> sourceRecordsData, IEnumerable<object> targetRecordsData, string sourceIdField)
        {
            #region 先将集合转换为JArray类型; 处理DataRow对象
            IEnumerable<IDictionary<string, object?>> sourceArray = sourceRecordsData.CastToDictionaries();
            IEnumerable<IDictionary<string, object?>> targetArray = targetRecordsData.CastToDictionaries();

            if (sourceArray.Count() == 0 && targetArray.Count() == 0)
            {
                return new DataComparedResult();
            }

            if (sourceArray.Count() == 0 && targetArray.Count() != 0)
            {
                return new DataComparedResult { ExistInTargetOnly = [.. targetArray] };
            }

            if (sourceArray.Count() != 0 && targetArray.Count() == 0)
            {
                return new DataComparedResult { ExistInSourceOnly = sourceArray.ToList() };
            }
            #endregion

            #region 参数校验
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
            var sourcePrimaryKeys = GetPrimaryKeys(sourceArray.FirstOrDefault().Keys, sourceIdField);
            var targetPrimaryKeys = GetPrimaryKeys(targetArray.FirstOrDefault().Keys, sourceIdField);
            if (sourcePrimaryKeys.Count == 0 || targetPrimaryKeys.Count == 0)
            {
                throw new Exception("主键字段不能为空");
            }
            if (sourcePrimaryKeys.Count != targetPrimaryKeys.Count)
            {
                throw new Exception($"解析后的主键字段无法一一对应");
            }
            bool equalsLogic(IDictionary<string, object?> x, IDictionary<string, object?> y)
            {
                return DictionaryComparer.EqualsByPrimaryKeys(x, y, sourcePrimaryKeys);
            }
            int hashCodeGetter(IDictionary<string, object?> target)
            {
                return DictionaryComparer.GetHashCodeByPrimaryKeys(target, sourcePrimaryKeys);
            }
            #endregion

            #region 定义线程安全的数据容器和其他变量
            // Source和Target的交集
            ConcurrentBag<IDictionary<string, object?>> intersectInSourceAndTarget = [];
            #endregion

            #region 调用CPU密集型任务帮助类进行多线程分批对比数据
            await BatchesScheme.CpuTasksExecuteAsync(targetArray, CompareBatchData);
            #endregion

            #region 构建数据对比结果并返回

            // target中独有的(existInTarget) = target所有 - 交集
            var existInSourceOnly = sourceArray.Except(intersectInSourceAndTarget, new DictionaryComparer(equalsLogic, hashCodeGetter)).ToList();
            // source中独有的(existInSource) = source所有 - 交集
            var existInTargetOnly = targetArray.Except(intersectInSourceAndTarget, new DictionaryComparer(equalsLogic, hashCodeGetter)).ToList();
            DataComparedResult compareResult = new()
            {
                Intersection = [.. intersectInSourceAndTarget],
                ExistInSourceOnly = existInSourceOnly,
                ExistInTargetOnly = existInTargetOnly,
            };

            // 返回
            return compareResult;
            #endregion

            #region 本地方法, 数据对比逻辑
            void CompareBatchData(IEnumerable<IDictionary<string, object?>> batchData)
            {
                var start = DateTime.Now;

                // 交集 - Linq比较数据效率比普通手动遍历比较高很多
                var intersect = sourceArray.Intersect(batchData, new DictionaryComparer(equalsLogic, hashCodeGetter)).ToList();
                intersect.ForEach(intersectInSourceAndTarget.Add);
                var end = DateTime.Now;

                var totalSeconds = (end - start).TotalSeconds;
                LoggerHelper.LogInformation($"数据对比: 已经处理了{batchData.Count()}; 耗时: {DateTimeHelper.FormatSeconds(totalSeconds)}{Environment.NewLine}");
                return;
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
            public List<IDictionary<string, object?>> ExistInSourceOnly { get; set; } = [];
            /// <summary>
            /// 仅存在于Target中
            /// </summary>
            public List<IDictionary<string, object?>> ExistInTargetOnly { get; set; } = [];
            /// <summary>
            /// Source和Target中存在, 改变了的数据
            /// </summary>
            public List<IDictionary<string, object?>> Intersection { get; set; } = [];
        }
        /// <summary>
        /// 改变了的数据
        /// </summary>
        public class ChangedRecord
        {
            /// <summary>
            /// Source中的值
            /// </summary>
            public Dictionary<string, object> SourceRecord { get; set; } = [];
            /// <summary>
            /// Target中的值
            /// </summary>
            public Dictionary<string, object> TargetRecord { get; set; } = [];
        }

        /// <summary>
        /// 获取数据表的更新时间字段
        /// </summary>
        /// <param name="fields"></param>
        /// <returns></returns>
        static string? GetUpdateTimeField(IEnumerable<string> fields)
        {
            return fields.FirstOrDefault(x => x.Contains("update", StringComparison.OrdinalIgnoreCase) && x.Contains("time", StringComparison.OrdinalIgnoreCase));
        }
        /// <summary>
        /// 获取数据表的字段信息
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public async Task<IEnumerable<ColumnInfo>> GetTableColumnsInfoAsync(string tableName)
        {
            if (_tableColumnsInfo.TryGetValue(getTableKey(_connectionString, tableName), out var tableInfo))
            {
                return tableInfo;
            }
            using var conn = GetDbConnection(_connectionString);
            return await GetTableColumnsInfoAsync(conn, tableName);
        }
        /// <summary>
        /// 获取数据库表结构
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<ColumnInfo>> GetTableColumnsInfoAsync(string connectionString, string tableName)
        {
            if (_tableColumnsInfo.TryGetValue(getTableKey(connectionString, tableName), out var tableInfo))
            {
                return tableInfo;
            }
            using var conn = GetDbConnection(connectionString);
            return await GetTableColumnsInfoAsync(conn, tableName);
        }

        static readonly ConcurrentDictionary<string, IEnumerable<ColumnInfo>> _tableColumnsInfo = [];
        static Func<string, string, string> getTableKey = (string connectionStrin, string tableName) => $"{connectionStrin}_{tableName}";
        /// <summary>
        /// 获取数据库表结构
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<ColumnInfo>> GetTableColumnsInfoAsync(IDbConnection conn, string tableName)
        {
            string tableKey = getTableKey(conn.ConnectionString, tableName);
            if (_tableColumnsInfo.TryGetValue(tableKey, out var tableInfo))
            {
                return tableInfo;
            }

            string sql;
            // TODO: 数据库类型 作为公共属性
            var dbType = GetDbType(conn.ConnectionString);
            if (dbType == DatabaseType.SqlServer)
            {
                sql = $"""
                    SELECT
                        C.name                                          as [ColumnCode]
                        ,T.name                                         as [ColumnType]
                        ,COLUMNPROPERTY(C.id,C.name,'PRECISION')        as [ColumnLength]
                        ,'textbox' as [ControlType] 
                        ,convert(bit,case when exists(SELECT 1 FROM sysobjects where xtype='PK' and parent_obj=c.id and name in (
                            SELECT name FROM sysindexes WHERE indid in(
                                SELECT indid FROM sysindexkeys WHERE id = c.id AND colid=c.colid))) then 1 else 0 end) 
                                                                    as [IsPK]
                        ,convert(bit,C.IsNullable)                  as [IsNullable]
                        ,ISNULL(CM.text,'')                         as [DefaultValue]
                        ,C.colid                                    as [OrderNo]
                        ,ETP.value                                  as [Remark]
                    FROM syscolumns C
                    INNER JOIN systypes T ON C.xusertype = T.xusertype 
                    left JOIN sys.extended_properties ETP   ON  ETP.major_id = c.id AND ETP.minor_id = C.colid AND ETP.name ='MS_Description' 
                    left join syscomments CM on C.cdefault=CM.id
                    WHERE C.id = object_id('{tableName}')
                    """;
            }
            else if (dbType == DatabaseType.Sqlite)
            {
                var res = await conn.QueryAsync($"PRAGMA table_info('{tableName}');");
                var records = res.Cast<IDictionary<string, object>>();
                var columns = records.Select(x =>
                {
                    string typeInfo = x["type"].ToString();
                    string length = "";
                    if (typeInfo.EndsWith(')') && typeInfo.Contains('('))
                    {
                        length = typeInfo[(typeInfo.IndexOf('(') + 1)..].TrimEnd(')');
                    }
                    return new ColumnInfo
                    {
                        ColumnCode = x["name"].ToString(),
                        ColumnType = typeInfo,
                        ColumnLength = length,
                        IsNullable = x["notnull"].ToString() == "0" ? 1 : 0,
                        IsPK = x["pk"].ToString() == "1" ? 1 : 0,
                        DefaultValue = x["dflt_value"]?.ToString(),
                        OrderNo = int.Parse(x["cid"]?.ToString())
                    };
                }).ToList();
                AnalysisColumnCSharpType(columns);
                return columns;
            }
            else if (dbType == DatabaseType.MySql)
            {
                sql = $"""
                    select
                        column_name                                     ColumnCode,
                        data_type                                       ColumnType,
                        CASE data_type
                        WHEN 'varchar' THEN
                            character_maximum_length
                        WHEN 'nvarchar' THEN
                            character_maximum_length
                        WHEN 'text' THEN
                            4000
                        WHEN 'bit' THEN
                            1
                        WHEN 'int' THEN
                            numeric_precision
                        WHEN 'datetime' THEN
                            datetime_precision
                        ELSE
                            0
                        END                                             ColumnLength,
                        CASE is_nullable  WHEN 'NO' then 0 ELSE 1 END   IsNullable,
                        CASE WHEN column_key='PRI' THEN 1 ELSE 0 END    IsPK,
                        column_default                                  DefaultValue,
                        ORDINAL_POSITION                                OrderNo,
                        COLUMN_COMMENT                                  Remark
                    from information_schema.columns where table_schema = '{GetDatabaseName(conn)}' and table_name = '{tableName}' 
                    order by ORDINAL_POSITION asc
                    """;
            }
            else if (dbType == DatabaseType.Pg)
            {
                sql = $"""
                    SELECT 
                        a.column_name AS "ColumnCode",
                        a.data_type AS "ColumnType",
                        CASE 
                            WHEN a.data_type = 'character varying' THEN a.character_maximum_length::text
                            WHEN a.data_type = 'numeric' THEN a.numeric_precision || ',' || a.numeric_scale
                            ELSE a.character_octet_length::text
                        END AS "ColumnLength",
                        CASE 
                            WHEN cont.constraint_type = 'PRIMARY KEY' THEN 1 
                            ELSE 0 
                        END AS "IsPK",
                        CASE 
                            WHEN a.is_nullable = 'NO' THEN 0
                            ELSE 1
                        END AS "IsNullable",
                        a.column_default AS "DefaultValue",
                        a.ordinal_position AS "OrderNo",
                        col_description(('"' || a.table_schema || '"."' || a.table_name || '"')::regclass::oid, a.ordinal_position::int) AS "Remark"
                    FROM 
                        information_schema.columns a
                    LEFT JOIN information_schema.key_column_usage kcu
                        ON a.table_name = kcu.table_name
                        AND a.column_name = kcu.column_name
                    LEFT JOIN information_schema.table_constraints cont
                        ON kcu.constraint_name = cont.constraint_name
                        AND cont.constraint_type = 'PRIMARY KEY'
                    WHERE 
                        a.table_name = LOWER('{tableName}')
                    ORDER BY 
                        a.ordinal_position;
                    """;
            }
            else
            {
                sql = $@" SELECT 
                                    A.column_name       ColumnCode,
                                    A.data_type         ColumnType,
                                    (case A.data_type when 'NVARCHAR2' then to_char(A.CHAR_LENGTH) when 'NUMBER' then A.DATA_PRECISION||','||A.DATA_SCALE else to_char(A.data_length) end)      ColumnLength,
                                    CASE WHEN A.COLUMN_ID=1 THEN 1 ELSE 0 END IsPK, 
                                    CASE A.nullable 
                                        WHEN 'N' then 0
                                        ELSE 1
                                        END  IsNullable,
                                    A.Data_default      DefaultValue,
                                    A.COLUMN_ID         OrderNo,
                                    B.comments          Remark
                            from  user_tab_columns A,user_col_comments B
                            WHERE a.COLUMN_NAME=b.column_name and A.Table_Name = B.Table_Name and 
                                A.Table_Name=upper('{tableName}')
                            ORDER BY  A.COLUMN_ID ";
            }

            var result = await conn.QueryAsync<ColumnInfo>(sql);
            if (!result.Any())
            {
                var db = GetDatabaseName(conn);
                throw new Exception($"{db}中没有找到{tableName}表");
            }
            // ColumnLength: Oracle如果是Number类型, ColumnLength值可能是"10,0"
            AnalysisColumnCSharpType(result);

            _tableColumnsInfo.TryAdd(tableKey, result);
            return result;
        }
        /// <summary>
        /// 获取CSharp中对应的Type
        /// </summary>
        /// <param name="csharpType"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        static Type GetCSharpType(string csharpType)
        {
            return csharpType switch
            {
                "string" => typeof(string),
                "bool" => typeof(bool),
                "int" => typeof(int),
                "long" => typeof(long),
                "float" => typeof(float),
                "double" => typeof(double),
                "decimal" => typeof(decimal),
                "datetime" => typeof(DateTime),
                "byte[]" => typeof(byte[]),
                _ => throw new Exception($"不支持的数据类型: {csharpType}")
            };
        }
        static readonly string[] _dbTypeKeywordsDateTime = ["time", "date", "text(6)"];
        static readonly string[] _dbTypeKeywordsBytes = ["lob", "binary", "bytea"];
        static readonly string[] _dbTypeKeywordsInt = ["int", "number"];
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
                    _ when _dbTypeKeywordsDateTime.Any(x => col.ColumnType.Contains(x, StringComparison.OrdinalIgnoreCase)) => "datetime",
                    _ when col.ColumnType.Contains("bit", StringComparison.OrdinalIgnoreCase) => "bool",
                    _ when col.ColumnType.Contains("bool", StringComparison.OrdinalIgnoreCase) => "bool",

                    _ when col.ColumnType.Contains("number", StringComparison.OrdinalIgnoreCase)
                        && (col.ColumnLength == "," || (col.ColumnLength is not null && col.ColumnLength.Contains(',')
                        && col.ColumnLength.Split(',')[1] != "0")) => "double",

                    _ when _dbTypeKeywordsInt.Any(x => col.ColumnType.Contains(x, StringComparison.OrdinalIgnoreCase)) => "int",
                    _ when col.ColumnType.Contains("float", StringComparison.OrdinalIgnoreCase) => "float",
                    _ when col.ColumnType.Contains("double", StringComparison.OrdinalIgnoreCase) => "double",
                    _ when col.ColumnType.Contains("decimal", StringComparison.OrdinalIgnoreCase) || col.ColumnType.StartsWith("real") => "decimal",
                    _ when col.ColumnType.Contains("clob", StringComparison.OrdinalIgnoreCase) => "string",

                    _ when _dbTypeKeywordsBytes.Any(x => col.ColumnType.Contains(x, StringComparison.OrdinalIgnoreCase)) => "byte[]",
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
        public static async Task<DataTable> QueryAsync(IDbConnection conn, string sql, object? parameters, IDbTransaction? transaction)
        {
            var dataTable = new DataTable();
            var reader = await conn.ExecuteReaderAsync(sql, param: parameters, transaction: transaction);
            dataTable.Load(reader);
            return dataTable;
        }
        /// <summary>
        /// 执行查询
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        public static async Task<IDataReader> QueryReaderAsync(IDbConnection conn, string sql, object? parameters, IDbTransaction? transaction)
        {
            var dataTable = new DataTable();
            var reader = await conn.ExecuteReaderAsync(sql, param: parameters, transaction: transaction);
            return reader;
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
}
