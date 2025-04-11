using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Test.AppSettingsOptions;
using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.Database
{
    public class DatabaseInfoTest : IClassFixture<TestFixture>
    {
        private readonly ITestOutputHelper _outputHelper;
        private readonly DatabaseInfo _databaseInfo;
        private readonly IConfiguration _configuration;
        private readonly Dictionary<string, string> _connectionStrings;
        public readonly string _connectionStringDev;
        public readonly string _connectionStringMySqlIX;
        public readonly string _cus1;
        public readonly string _cus2;
        private readonly string _createStatementLog;

        public DatabaseInfoTest(ITestOutputHelper outputHelper, TestFixture fixture)
        {
            _outputHelper = outputHelper;
            _databaseInfo = fixture.ServiceProvider.GetRequiredService<DatabaseInfo>();
            _configuration = fixture.ServiceProvider.GetRequiredService<IConfiguration>();
            _connectionStringMySqlIX = _configuration.GetConnectionString("MySqlIX")!;
            _connectionStrings = new()
            {
                { DatabaseType.Sqlite.ToString(), _configuration.GetConnectionString("Sqlite")! },
                { DatabaseType.Pg.ToString(), _configuration.GetConnectionString("PgR")! },
                { DatabaseType.SqlServer.ToString(), _configuration.GetConnectionString("SqlServerM")! },
                { DatabaseType.Dm.ToString(), _configuration.GetConnectionString("DmI")! },
                { DatabaseType.Oracle.ToString(), _configuration.GetConnectionString("OracleE")! },
                { DatabaseType.MySql.ToString(), _configuration.GetConnectionString("MySqlEH")! },
            };
            _connectionStringDev = _configuration.GetConnectionString("MySqlDEV")!;
            _cus1 = _configuration.GetConnectionString("cus1")!;
            _cus2 = _configuration.GetConnectionString("cus2")!;
            _createStatementLog = _configuration["CreateStatementLog"] ?? throw new Exception("请在配置文件中添加创建表语句日志CreateStatementLog");
        }

        /// <summary>
        /// 同步数据库, 一张表
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [Fact]
        public async Task SyncFromDBToDBSingleTable()
        {
            var syncFromDbToDbOptions = _configuration.GetSection(SyncFromDbToDbOptions.Key).Get<SyncFromDbToDbOptions>() ?? throw new Exception($"请在配置文件中添加同步的数据库配置");

            await DatabaseInfo.TransferDataAsync(syncFromDbToDbOptions.SourceConnectionString, syncFromDbToDbOptions.TargetConnectionString, syncFromDbToDbOptions.SourceTable);
        }

        /// <summary>
        /// 根据数据列表获取数据插入的SQL语句
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task GetInsertSqlInfoAsync_ColumnsSortByTargetTable()
        {
            using var targetConn = DatabaseInfo.GetDbConnection(_connectionStringDev);
            List<object> list = [
                new{ Id = 1, Message = "Hello", MessageTemplate = "Tem123123", Level = "info", TimeStamp = DateTime.Now, Exception = "Exp....", LogEvent = "event...", Properties = "Properties" },
            ];
            var insertSqlInfo1 = await DatabaseInfo.GetInsertSqlInfosAsync(list, "log", targetConn);

            List<object> list2 = [
                new{ Properties = "Properties", LogEvent = "event...", Exception = "Exp....", Message = "Hello", MessageTemplate = "Tem123123", Level = "info", TimeStamp = DateTime.Now, Id = 1 },
            ];
            var insertSqlInfo2 = await DatabaseInfo.GetInsertSqlInfosAsync(list, "log", targetConn);
            Assert.Equal(insertSqlInfo1[0].Sql, insertSqlInfo2[0].Sql);
        }

        /// <summary>
        /// 同步数据库, 指定库的所有表
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [Fact]
        public async Task SyncFromDBToDB_AllTables()
        {
            #region 参数
            var sourceConnectionString = _configuration["SyncFromDbToDbOptions:SourceConnectionString"] ?? throw new Exception($"请在配置文件中添加源数据库连接字符串");
            var targetConnectionString = _configuration["SyncFromDbToDbOptions:TargetConnectionString"] ?? throw new Exception($"请在配置文件中添加目标数据库连接字符串");
            // 生成SQL: 获取所有表SqlGetDbTablesInfo -> 生成SQL: 获取表全名GetTableFullName -> 生成SQL: 获取表数据GetQuerySql
            #endregion

            await DatabaseInfo.TransferDataAsync(sourceConnectionString, targetConnectionString, sourceTable: "userroles_20240318mgrscope已更新", targetTable: "userroles");
        }
        /// <summary>
        /// 根据连接字符串判断数据库类型
        /// </summary>
        [Fact]
        public void GetDbType()
        {
            foreach (var item in _connectionStrings)
            {
                Assert.Equal(item.Key, DatabaseInfo.GetDbType(item.Value).ToString());
            }
        }

        /// <summary>
        /// 数据迁移 - 跨库批量插入数据
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task InsertDataAsync()
        {
            string sourceTable = "dev";
            string targetTable = $"{sourceTable}_copy";
            var sourceConn = DatabaseInfo.GetDbConnection(_connectionStrings.First().Value);
            foreach (var targetConnectionString in _connectionStrings)
            {
                using var targetConn = DatabaseInfo.GetDbConnection(targetConnectionString.Value);
                var dbType = DatabaseInfo.GetDbType(targetConnectionString.Value);
                await targetConn.ExecuteAsync($"DELETE FROM {targetTable}");

                var records = await sourceConn.QueryAsync($"select * from {sourceTable}");
                await DatabaseInfo.InsertDataAsync(targetConn, targetTable, records.Cast<IDictionary<string, object?>>());
            }
        }

        /// <summary>
        /// 复制表 - 不同数据库之间
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task GetCreateTableStatement_CopyTable_AcrossDatabases()
        {
            string log = _createStatementLog;
            File.WriteAllText(log, string.Empty);

            foreach (var connectionString in _connectionStrings)
            {
                var sourceConn = DatabaseInfo.GetDbConnection(connectionString.Value);
                string table = "dev";
                var columns = await DatabaseInfo.GetTableColumnsInfoAsync(sourceConn, table);

                foreach (var column in columns)
                {
                    File.AppendAllText(log, $"Name: {column.ColumnCode}; Code: {column.ColumnCode}; Type: {column.ColumnType} - {column.ColumnCSharpType}; Length: {column.ColumnLength}; IsPK: {column.IsPK}; DefaultValue: {column.DefaultValue}; IsNullable: {column.IsNullable}; Remark: {column.Remark};;".Replace(";;", Environment.NewLine));
                }

                foreach (var targetConnectionString in _connectionStrings)
                {
                    using var targetConn = DatabaseInfo.GetDbConnection(targetConnectionString.Value);
                    string targetTable = $"{table}_copy";
                    var dbType = DatabaseInfo.GetDbType(targetConnectionString.Value);
                    if (await DatabaseInfo.TableExistAsync(targetConn, targetTable))
                    {
                        File.AppendAllText(log, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 目标: {targetTable}已存在, 删除表");
                        await targetConn.ExecuteAsync($"drop table {targetTable}");
                    }

                    File.AppendAllText(log, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} 目标: {targetConnectionString}{Environment.NewLine}");
                    File.AppendAllText(log, dbType.ToString() + Environment.NewLine);
                    string createTableStatement = DatabaseInfo.GetCreateTableStatement(targetTable, columns, dbType);
                    File.AppendAllText(log, createTableStatement + Environment.NewLine);

                    _ = await targetConn.ExecuteAsync(createTableStatement);
                    File.AppendAllText(log, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {new string('-', 50)}创建表结束{new string('-', 50)}{Environment.NewLine}{Environment.NewLine}");

                    var records = await sourceConn.QueryAsync($"select * from {table}");
                    await DatabaseInfo.InsertDataAsync(targetConn, targetTable, records.Cast<IDictionary<string, object?>>());
                }

                File.AppendAllText(log, Environment.NewLine + Environment.NewLine);
            }
        }

        /// <summary>
        /// 备份数据表到文件中
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task BackupTablesTest()
        {
            await DatabaseInfo.BackupDataAsync(_connectionStringDev.Replace("ids4", "engine2"), "deploymentresource2");
        }
        /// <summary>
        /// 还原数据表
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task RestoreTablesTest()
        {
            string connStr = _connectionStringDev.Replace("ids4", "engine2");
            await DatabaseInfo.RestoreTablesAsync(connStr, "D:\\.NET\\my\\Sylas.RemoteTasks\\Sylas.RemoteTasks.Test\\bin\\Debug\\net9.0\\Backup\\20250411233906");
        }
    }
}
