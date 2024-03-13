using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.Database;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Test.AppSettingsOptions;
using Sylas.RemoteTasks.Utils;
using System.Text;
using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.Remote
{
    public partial class FetchAllDataByApiTest : IClassFixture<TestFixture>
    {
        private readonly ITestOutputHelper _outputHelper;
        private readonly DatabaseInfo _databaseInfo;
        private readonly IConfiguration _configuration;

        public FetchAllDataByApiTest(ITestOutputHelper outputHelper, TestFixture fixture)
        {
            _outputHelper = outputHelper;
            _databaseInfo = fixture.ServiceProvider.GetRequiredService<DatabaseInfo>();
            _configuration = fixture.ServiceProvider.GetRequiredService<IConfiguration>();
        }

        /// <summary>
        /// 数据库 数据脱敏
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task DesensitizeTest()
        {
            var affectedRows = await DatabaseInfo.DesensitizeAsync("Data Source=localhost:1521/helowin;User ID=userinfo;Password=123456;PERSIST SECURITY INFO=True;Pooling = True;Max Pool Size = 100;Min Pool Size = 1;"
                , "syncoc"
                , new List<string> { "username", "email", "phone" });
            _outputHelper.WriteLine($"已经脱敏{affectedRows}条数据");
        }

        /// <summary>
        /// 从配置文件调用API
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [Fact]
        public async Task FetchDataModelByConfigAsync()
        {
            var configFileRelativePath = _configuration["SyncFromApiToDbOptions:FetchDataModelParameters"] ?? throw new Exception("API请求参数没有配置");
            var configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFileRelativePath);
            var configContent = File.ReadAllText(configFile);
            var config = JsonConvert.DeserializeObject<RequestConfig>(configContent) ?? throw new Exception("API请求参数格式不正确");

            var configData = await RemoteHelpers.FetchAllDataFromApiAsync(config);
            var targetDbConnectionString = _configuration["SyncFromApiToDbOptions:TargetDbConnectionString"];
            Assert.NotNull(targetDbConnectionString);
            Assert.True(configData is not null);
            var conn = DatabaseInfo.GetDbConnection(targetDbConnectionString);
            await DatabaseHelper.SyncDataAsync(conn, "devdatamodel", config.Data?.ToList() ?? [], [], []);
        }

        /// <summary>
        /// 表达式树拷贝对象测试
        /// </summary>
        /// <exception cref="Exception"></exception>
        [Fact]
        public void TransExpTestAsync()
        {
            var configFileRelativePath = _configuration["SyncFromApiToDbOptions:FetchDataModelParameters"] ?? throw new Exception("API请求参数没有配置");
            var configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFileRelativePath);
            var configContent = File.ReadAllText(configFile);
            var config = JsonConvert.DeserializeObject<RequestConfig>(configContent) ?? throw new Exception("API请求参数格式不正确");
            var config2 = MapHelper<RequestConfig, RequestConfig>.Map(config);
            var config3 = JsonConvert.DeserializeObject<RequestConfig>(JsonConvert.SerializeObject(config));
            config.Url = "xxx";
            Assert.True(config2.Url != config.Url);
        }

        /// <summary>
        /// 用大批量的参数测试一个Api, 结果写入到文件中, 从索引为1000的参数开始, 执行2000个参数
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ApiBatchValidationTestAsync()
        {
            await ApiValidationHelpers.ApiBatchValidationAsync(
                        $"{_configuration["ValidationApi:Gateway"]}{_configuration["ValidationApi:Url"]}",
                        _configuration["ValidationApi:ParametersPath"] ?? throw new Exception("参数所在的文件路径不能为空"),
                        _configuration["ValidationApi:Token"] ?? string.Empty,
                        "userId",
                        1000,
                        2000
                    );
        }
        /// <summary>
        /// 将指定目录下的所有文件写入一个文件中
        /// </summary>
        /// <returns></returns>
        [Fact]
        public void ReadFiles()
        {
            string dir1 = "D:/.NET/Id/Id.SiteManagement/Id.sitemanagement.core/Entities";
            string dir2 = "D:/.NET/Id/Id.portal.api/Id.portal.core/Entities";
            string[] dirs = new string[] { dir1, dir2 };
            StringBuilder fileContentBuilder = new();
            foreach (var dir in dirs)
            {
                var files = FileHelper.FindFilesRecursive(dir, file => file.EndsWith(".cs"));
                foreach (var file in files)
                {
                    var filename = file.Replace('\\', '/').Replace(dir, string.Empty);
                    //fileContentBuilder.AppendLine(filename);
                    fileContentBuilder.AppendLine(File.ReadAllText(file));
                    fileContentBuilder.AppendLine();
                }
            }
            File.WriteAllText(@"D:\.NET\id\routine\txt\siteportalentities.txt", fileContentBuilder.ToString());

        }
        /// <summary>
        /// 将比较大的秒的值格式化为x天x时x分x秒的格式
        /// </summary>
        [Fact]
        public void DateTimeFormatterTest()
        {
            Assert.True(DateTimeHelper.FormatSeconds(62.235) == "1分2秒");
            Assert.True(DateTimeHelper.FormatSeconds(60 * 60 + 62.235) == "1时1分2秒");
            Assert.True(DateTimeHelper.FormatSeconds(24 * 60 * 60 + 60 * 60 + 62.235) == "1天1时1分2秒");
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
            string dataDir = "D:/.NET/iduo/routine/db/数据库结构同步数据迁移/org datasource json";
            string connectionString = "Server=192.168.1.40;Port=3306;Stmt=;Database=iduo_ids4_xtie;Uid=root;Pwd=123456;Allow User Variables=true;sslMode=None";
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
