using Dapper;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oracle.ManagedDataAccess.Client;
using Sylas.RemoteTasks.App.Database.SyncBase;
using Sylas.RemoteTasks.App.Utils;
using Sylas.RemoteTasks.Test.AppSettingsOptions;
using System.Data;
using System.Text;
using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.Remote
{
    public partial class FetchAllDataByApiTest : IClassFixture<TestFixture>
    {
        private readonly ITestOutputHelper _outputHelper;
        private readonly IConfiguration _configuration;

        public FetchAllDataByApiTest(ITestOutputHelper outputHelper, TestFixture fixture)
        {
            _outputHelper = outputHelper;
            _configuration = fixture.ServiceProvider.GetRequiredService<IConfiguration>();
        }
        

        /// <summary>
        /// 获取数据模型数据
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task FetchDataModelAsync()
        {
            string gateway = _configuration["SyncFromApiToDb:Gateway"] ?? string.Empty;
            string mainModelId = _configuration["SyncFromApiToDb:MainModelId"] ?? string.Empty;
            string token = _configuration["SyncFromApiToDb:Token"] ?? string.Empty;
            string appDB = _configuration["SyncFromApiToDb:AppDB"] ?? string.Empty;
            string targetConnectionString = _configuration["SyncFromApiToDb:TargetDbConnectionString"] ?? string.Empty;

            await fetchDataModelsRecursivelyAsync(mainModelId);

            #region 同步数据模型
            async Task fetchDataModelsRecursivelyAsync(string modelId)
            {
                string table = "DevDataModel";
                var queryDictionary = new Dictionary<string, object>
                {
                    { "db",  appDB}, // AppDB
                    { "table",  table},
                    { "pageIndex", 1 },
                    { "pageSize", 20 },
                    { "isAsc", true }
                };
                var filterItem = new Dictionary<string, object> { { "FieldName", "id" }, { "CompareType", "=" }, { "Value", modelId } };
                var filterItems = new List<Dictionary<string, object>> { filterItem };
                var bodyDictionary = new Dictionary<string, object>
                {
                    {
                        "FilterItems", filterItems
                    }
                };
                var dataModel = await RemoteHelpers.FetchAllDataFromApiAsync($"{gateway}/form/api/DataSource/GetDataTable",
                                                                       "获取数据源MENUS数据失败",
                                                                       queryDictionary,
                                                                       "pageIndex",
                                                                       true,
                                                                       bodyDictionary,
                                                                       response => response["code"]?.ToString() == "1",
                                                                       response => response["data"] ?? new JArray(),
                                                                       new HttpClient(),
                                                                       "id",
                                                                       "",
                                                                       false,
                                                                       token);
                Assert.NotNull(dataModel);
                var dm = dataModel.FirstOrDefault();
                if (dm is null)
                {
                    System.Diagnostics.Debug.WriteLine($"================================================= DataModel Not Found: {modelId} =================================================");
                    return;
                }

                // 获取模型字段表数据
                table = "DevDataModelField";
                queryDictionary["table"] = table;
                filterItem = new Dictionary<string, object> { { "FieldName", "modelid" }, { "CompareType", "include" }, { "Value", modelId } };
                filterItems = new List<Dictionary<string, object>> { filterItem };
                bodyDictionary = new Dictionary<string, object>
                {
                    {
                        "FilterItems", filterItems
                    }
                };
                var modelFields = await RemoteHelpers.FetchAllDataFromApiAsync($"{gateway}/form/api/DataSource/GetDataTable",
                                                                       "获取数据源MENUS数据失败",
                                                                       queryDictionary,
                                                                       "pageIndex",
                                                                       true,
                                                                       bodyDictionary,
                                                                       response => response["code"]?.ToString() == "1",
                                                                       response => response["data"] ?? new JArray(),
                                                                       new HttpClient(),
                                                                       "id",
                                                                       "",
                                                                       false,
                                                                       token);
                Assert.True(modelFields.Any());
                System.Diagnostics.Debug.WriteLine($"================================================= {table}: {modelFields.Count} records =================================================");

                IDbConnection conn = DatabaseInfo.GetDbConnection(targetConnectionString);
                conn.Open();
                await DatabaseHelper.SyncDataAsync(conn, "DevDataModel", new List<JToken> { dm }, new string[] { "NO" }, new string[] { "CREATEDTIME", "UPDATEDTIME" });
                await DatabaseHelper.SyncDataAsync(conn, table, modelFields, new string[] { "NO" }, new string[] { "CREATEDTIME", "UPDATEDTIME" });

                // TODO: 切换到DatabaseInfo同步数据(支持创建表)


                var childModelFields = modelFields.Where(x => x["DATATYPE"]?.ToString() == "21");
                foreach (var field in childModelFields)
                {
                    var childModelId = field["REFMODELID"]?.ToString();
                    if (string.IsNullOrWhiteSpace(childModelId))
                    {
                        System.Diagnostics.Debug.WriteLine($"================================================= {field["ID"]}[{field["NAME"]}] is detail-field, but responding DataModel Id is empty =================================================");
                        continue;
                    }
                    System.Diagnostics.Debug.WriteLine($"================================================= 子模型{field["ID"]}[{field["NAME"]}]正在同步 =================================================");
                    await fetchDataModelsRecursivelyAsync(childModelId);
                }
            }
            #endregion
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
            var configFile = Path.Combine(ApplicationEnvironment.ApplicationBasePath, configFileRelativePath);
            var configContent = File.ReadAllText(configFile);
            var config = JsonConvert.DeserializeObject<RequestConfig>(configContent) ?? throw new Exception("API请求参数格式不正确");

            var configWithData = await RemoteHelpers.FetchAllDataFromApiAsync(config);
            var targetDbConnectionString = _configuration["SyncFromApiToDbOptions:TargetDbConnectionString"];
            Assert.NotNull(targetDbConnectionString);
            Assert.True(configWithData.Data is not null);
        }

        /// <summary>
        /// 表达式树拷贝对象测试
        /// </summary>
        /// <exception cref="Exception"></exception>
        [Fact]
        public void TransExpTestAsync()
        {
            var configFileRelativePath = _configuration["SyncFromApiToDbOptions:FetchDataModelParameters"] ?? throw new Exception("API请求参数没有配置");
            var configFile = Path.Combine(ApplicationEnvironment.ApplicationBasePath, configFileRelativePath);
            var configContent = File.ReadAllText(configFile);
            var config = JsonConvert.DeserializeObject<RequestConfig>(configContent) ?? throw new Exception("API请求参数格式不正确");
            var config2 = MapHelper<RequestConfig, RequestConfig>.Map(config);
            var config3 = JsonConvert.DeserializeObject<RequestConfig>(JsonConvert.SerializeObject(config));
            config.Url = "xxx";
            Assert.True(config2.Url != config.Url);
        }

        /// <summary>
        /// 对比数据: 数据库 和 json文件
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task DataCompareTestAsync()
        {
            var sourceConn = DatabaseInfo.GetDbConnection("Data Source=192.168.1.227:1521/helowin;User ID=iduo_ids4;Password=iduo2022;PERSIST SECURITY INFO=True;Pooling = True;Max Pool Size = 100;Min Pool Size = 1;");
            var compareResult = await DatabaseInfo.CompareRecordsFromDbWithDataAsync(sourceConn, "syncoc", "USERID", "LOGINNAME", new DatabaseInfo.DataInJson("D:/.NET/iduo/routine/db/同步SQL/杭师大/SYNC_OC.json", new string[] { "RECORDS" }));
            _outputHelper.WriteLine("success");
        }

        /// <summary>
        /// 从文本文件中提取出需要的数据
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task SearchTxtTestAsync()
        {
            await FileHelper.SearchTxt("D:/.NET/iduo/routine/txt/log-json.log", @"GetUsersByJobAndUserId:\s*jobId:\s*(?<jobId>.+),\s*userId:\s*(?<userId>.+?)(?=\\n"")");
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
        public async Task ReadFiles()
        {
            string dir = "D:\\.NET\\iduo\\iduo.SiteManagement\\iduo.sitemanagement.core\\";
            var files = FileHelper.FindFilesRecursive(dir, file => file.EndsWith(".cs"));
            var i = 0;
            StringBuilder fileContentBuilder = new();
            foreach (var file in files)
            {
                var filename = file.Replace(dir, string.Empty);
                fileContentBuilder.AppendLine(filename);
                fileContentBuilder.AppendLine(File.ReadAllText(file));
                fileContentBuilder.AppendLine();
            }
            File.WriteAllText(@"D:\.NET\iduo\routine\txt\sitecodes.txt", fileContentBuilder.ToString());
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
        public async Task SyncFromDBToDBSigleTable()
        {
            var syncFromDbToDbOptions = _configuration.GetSection(SyncFromDbToDbOptions.Key).Get<SyncFromDbToDbOptions>();
            if (syncFromDbToDbOptions is null || string.IsNullOrWhiteSpace(syncFromDbToDbOptions.SourceDb) || string.IsNullOrWhiteSpace(syncFromDbToDbOptions.SourceTable) || string.IsNullOrWhiteSpace(syncFromDbToDbOptions.SourceConnectionString) || string.IsNullOrWhiteSpace(syncFromDbToDbOptions.TargetConnectionString))
            {
                throw new Exception($"请配置同步参数: {JsonConvert.SerializeObject(new SyncFromDbToDbOptions())}");
            }
            using var targetConn = DatabaseInfo.GetDbConnection(syncFromDbToDbOptions.TargetConnectionString);
            targetConn.Open();

            // 1.生成所有表的insert语句
            List<TableSqlsInfo> allTablesInsertSqls = new();

            using (var sourceConn = DatabaseInfo.GetDbConnection(syncFromDbToDbOptions.SourceConnectionString))
            {
                TableSqlsInfo tableInsertSqlInfo = await DatabaseInfo.GetTableSqlsInfoAsync(syncFromDbToDbOptions.SourceTable, sourceConn, null, targetConn,
                    new DataFilter()
                    {
                        FilterItems = new List<FilterItem>() {
                            new FilterItem () { FieldName = "Id", CompareType = "=", Value = "" }
                        }
                    });
                allTablesInsertSqls.Add(tableInsertSqlInfo);
            }

            // 2. 执行每个表的insert语句
            var targetDbTransaction = targetConn.BeginTransaction();
            foreach (var tableSqlsInfo in allTablesInsertSqls)
            {
                if (string.IsNullOrEmpty(tableSqlsInfo.BatchInsertSqlInfo.BatchInsertSql))
                {
                    _outputHelper.WriteLine($"表{tableSqlsInfo.TableName}没有数据无需处理");
                    continue;
                }
                try
                {
                    if (!string.IsNullOrWhiteSpace(tableSqlsInfo.BatchInsertSqlInfo.CreateTableSql))
                    {
                        // 创建表(或者修改字段)不会回滚
                        _ = await targetConn.ExecuteAsync(tableSqlsInfo.BatchInsertSqlInfo.CreateTableSql, transaction: targetDbTransaction);
                    }
                    var affectedRowsCount = await targetConn.ExecuteAsync(tableSqlsInfo.BatchInsertSqlInfo.BatchInsertSql, tableSqlsInfo.BatchInsertSqlInfo.Parameters, transaction: targetDbTransaction);
                    _outputHelper.WriteLine($"{tableSqlsInfo.TableName}: {affectedRowsCount}条数据");
                }
                catch (Exception ex)
                {
                    _outputHelper.WriteLine(ex.Message);
                    _outputHelper.WriteLine(tableSqlsInfo.BatchInsertSqlInfo.BatchInsertSql);
                    targetDbTransaction.Rollback();
                    _outputHelper.WriteLine("同步结束");
                    return;
                }
            }
            targetDbTransaction.Commit();
            _outputHelper.WriteLine("同步结束");
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
            var sourceConnectionString = _configuration["SyncFromDbToDb:SourceConnectionString"] ?? throw new Exception($"请在配置文件中添加源数据库连接字符串");
            var targetConnectionString = _configuration["SyncFromDbToDb:TargetConnectionString"] ?? throw new Exception($"请在配置文件中添加目标数据库连接字符串");
            // 生成SQL: 获取所有表SqlGetDbTablesInfo -> 生成SQL: 获取表全名GetTableFullName -> 生成SQL: 获取表数据GetQuerySql
            #endregion

            using var targetConn = DatabaseInfo.GetDbConnection(targetConnectionString);
            targetConn.Open();

            // 1.生成所有表的insert语句
            List<TableSqlsInfo> allTablesInsertSqls = new();

            using (var conn = DatabaseInfo.GetDbConnection(sourceConnectionString))
            {
                // 数据源-数据库
                var res = await conn.QueryAsync(DatabaseInfo.GetAllTables(_configuration["SyncFromDbToDb:SourceDb"] ?? throw new Exception($"请在配置文件中添加源要同步的库")));
                foreach (var table in res)
                {
                    TableSqlsInfo tableInsertSqlInfo = await DatabaseInfo.GetTableSqlsInfoAsync(table, conn, null, targetConn, null);
                    allTablesInsertSqls.Add(tableInsertSqlInfo);
                }
            }

            // 2. 执行每个表的insert语句
            var dbTransaction = targetConn.BeginTransaction();
            foreach (var tableSqlsInfo in allTablesInsertSqls)
            {
                if (string.IsNullOrEmpty(tableSqlsInfo.BatchInsertSqlInfo.BatchInsertSql))
                {
                    _outputHelper.WriteLine($"表{tableSqlsInfo.TableName}没有数据无需处理");
                    continue;
                }
                try
                {
                    var affectedRowsCount = await targetConn.ExecuteAsync(tableSqlsInfo.BatchInsertSqlInfo.BatchInsertSql, tableSqlsInfo.BatchInsertSqlInfo.Parameters, transaction: dbTransaction);
                    _outputHelper.WriteLine($"{tableSqlsInfo.TableName}: {affectedRowsCount}条数据");
                }
                catch (Exception ex)
                {
                    _outputHelper.WriteLine(ex.Message);
                    _outputHelper.WriteLine(tableSqlsInfo.BatchInsertSqlInfo.BatchInsertSql);
                    dbTransaction.Rollback();
                    _outputHelper.WriteLine("同步结束");
                    return;
                }
            }
            dbTransaction.Commit();
            _outputHelper.WriteLine("同步结束");
        }
    }
}
