using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Test.AppSettingsOptions;
using Xunit.Abstractions;
using Dapper;
using MySqlX.XDevAPI.Relational;

namespace Sylas.RemoteTasks.Test.Remote
{
    public class DatabaseInfoTest : IClassFixture<TestFixture>
    {
        private readonly ITestOutputHelper _outputHelper;
        private readonly DatabaseInfo _databaseInfo;
        private readonly IConfiguration _configuration;
        private readonly Dictionary<string, string> _connectionStrings;
        public DatabaseInfoTest(ITestOutputHelper outputHelper, TestFixture fixture)
        {
            _outputHelper = outputHelper;
            _databaseInfo = fixture.ServiceProvider.GetRequiredService<DatabaseInfo>();
            _configuration = fixture.ServiceProvider.GetRequiredService<IConfiguration>();
            _connectionStrings = new()
            {
                { DatabaseType.Sqlite.ToString(), _configuration.GetConnectionString("Sqlite")! },
                { DatabaseType.Pg.ToString(), _configuration.GetConnectionString("PgR")! },
                { DatabaseType.SqlServer.ToString(), _configuration.GetConnectionString("SqlServerM")! },
                { DatabaseType.Dm.ToString(), _configuration.GetConnectionString("DmI")! },
                { DatabaseType.Oracle.ToString(), _configuration.GetConnectionString("OracleE")! },
                { DatabaseType.MySql.ToString(), _configuration.GetConnectionString("MySqlEH")! },
            };
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

            await DatabaseInfo.TransferDataAsync(syncFromDbToDbOptions.SourceConnectionString, syncFromDbToDbOptions.TargetConnectionString, syncFromDbToDbOptions.SourceDb, syncFromDbToDbOptions.SourceTable);
        }

        /// <summary>
        /// 同步数据库, 指定库的所有表
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [Fact]
        async Task SyncFromDBToDB_AllTables()
        {
            #region 参数
            var sourceConnectionString = _configuration["SyncFromDbToDbOptions:SourceConnectionString"] ?? throw new Exception($"请在配置文件中添加源数据库连接字符串");
            var targetConnectionString = _configuration["SyncFromDbToDbOptions:TargetConnectionString"] ?? throw new Exception($"请在配置文件中添加目标数据库连接字符串");
            // 生成SQL: 获取所有表SqlGetDbTablesInfo -> 生成SQL: 获取表全名GetTableFullName -> 生成SQL: 获取表数据GetQuerySql
            #endregion

            await DatabaseInfo.TransferDataAsync(sourceConnectionString, targetConnectionString);
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
                await DatabaseInfo.InsertDataAsync(targetConn, targetTable, records.Cast<IDictionary<string, object>>());
            }
        }

        /// <summary>
        /// 复制表 - 不同数据库之间
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task GetCreateTableStatement_CopyTable_AcrossDatabases()
        {
            string log = @"D:\.NET\iduo\routine\db\bak\create_statement1.txt";
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
                    await DatabaseInfo.InsertDataAsync(targetConn, targetTable, records.Cast<IDictionary<string, object>>());
                }

                File.AppendAllText(log, Environment.NewLine + Environment.NewLine);
            }
        }
    }
}
