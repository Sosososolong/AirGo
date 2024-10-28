using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Test.AppSettingsOptions;
using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.Remote
{
    public class DatabaseInfoTest(ITestOutputHelper outputHelper, TestFixture fixture) : IClassFixture<TestFixture>
    {
        private readonly ITestOutputHelper _outputHelper = outputHelper;
        private readonly DatabaseInfo _databaseInfo = fixture.ServiceProvider.GetRequiredService<DatabaseInfo>();
        private readonly IConfiguration _configuration = fixture.ServiceProvider.GetRequiredService<IConfiguration>();
        /// <summary>
        /// 同步数据库, 一张表
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [Fact]
        public async Task SyncFromDBToDBSingleTable()
        {
            var syncFromDbToDbOptions = _configuration.GetSection(SyncFromDbToDbOptions.Key).Get<SyncFromDbToDbOptions>() ?? throw new Exception($"请在配置文件中添加同步的数据库配置");

            await DatabaseInfo.SyncDatabaseByConnectionStringsAsync(syncFromDbToDbOptions.SourceConnectionString, syncFromDbToDbOptions.TargetConnectionString, syncFromDbToDbOptions.SourceDb, syncFromDbToDbOptions.SourceTable);
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

            await DatabaseInfo.SyncDatabaseByConnectionStringsAsync(sourceConnectionString, targetConnectionString);
        }
        /// <summary>
        /// 从json数据导入数据
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task SyncJsonData()
        {
            string dataDir = "D:/.NET/Id/routine/db/数据库结构同步数据迁移/org datasource json";
            string connectionString = "Server=127.0.0.1;Port=3306;Stmt=;Database=id_org_xtie;Uid=root;Pwd=123456;Allow User Variables=true;sslMode=None";
            var files = Directory.GetFiles(dataDir);
            foreach (var file in files)
            {
                var f = file.Replace('\\', '/');
                string table = f[(f.LastIndexOf('/') + 1)..].Replace(".json", "");

                string dataSourceFile = f;
                string dataSourceIdField = "id";
                if (table == "userroles")
                {
                    dataSourceIdField = "UserId,RoleId";
                }

                string dataSourceJson = File.ReadAllText(f);

                IEnumerable<JToken> dataList;
                if (dataSourceJson.StartsWith('{'))
                {
                    dataList = JsonConvert.DeserializeObject<JObject>(dataSourceJson)?["RECORDS"] ?? throw new Exception("");
                }
                else
                {
                    dataList = JsonConvert.DeserializeObject<List<JToken>>(dataSourceJson) ?? throw new Exception("获取json数据源失败");
                }

                await DatabaseInfo.SyncDatabaseAsync(connectionString, table, dataList, [], dataSourceIdField, dataSourceIdField, "", null);
            }
        }
    }
}
