using Dapper;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oracle.ManagedDataAccess.Client;
using Sylas.RemoteTasks.App.Database.SyncBase;
using Sylas.RemoteTasks.App.Utils;
using Sylas.RemoteTasks.Test.AppSettingsOptions;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.RemoteHelper
{
    public class FetchAllDataByApiTest : TestBase
    {
        private readonly ITestOutputHelper _outputHelper;

        public FetchAllDataByApiTest(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }
        //[Fact]
        //public async Task FetchProcessData()
        //{
        //    var parameters = File.ReadAllLines("D:\\.NET\\my\\Sylas.RemoteTasks\\Sylas.RemoteTasks.Test\\parameters.log");
        //    string gateway = parameters[1]; // "http://xxx.gateway.com:8008";
        //    string processDefId = parameters[2]; // "Process221009145756";
        //    string token = parameters[3];
        //    string appDB = parameters[4]; // AppDB
        //    string table = "SYSMENU";

        //    string targetConnectionString = parameters[16];

        //    var queryDictionary = new Dictionary<string, object>
        //    {
        //        { "db",  appDB}, // AppDB
        //        { "table",  table},
        //        { "pageIndex", 1 },
        //        { "pageSize", 20 },
        //        { "isAsc", true }
        //    };
        //    var filterItem = new Dictionary<string, object> { { "FieldName", "url" }, { "CompareType", "include" }, { "Value", processDefId } };
        //    var filterItems = new List<Dictionary<string, object>> { filterItem };
        //    var bodyDictionary = new Dictionary<string, object>
        //    {
        //        {
        //            "FilterItems", filterItems
        //        }
        //    };
        //    var menus = await RemoteHelpers.FetchAllDataFromApiAsync($"{gateway}/form/api/DataSource/GetDataTable",
        //                                                           "获取数据源MENUS数据失败",
        //                                                           queryDictionary,
        //                                                           "pageIndex",
        //                                                           true,
        //                                                           bodyDictionary,
        //                                                           response => response["code"]?.ToString() == "1",
        //                                                           response => response["data"] ?? new JArray(),
        //                                                           new HttpClient(),
        //                                                           "id",
        //                                                           "",
        //                                                           false,
        //                                                           token);
        //    Assert.NotNull(menus);
        //    var menu = menus.FirstOrDefault();
        //    Assert.NotNull(menu);

        //    var idpath = menu["IDPATH"]?.ToString();
        //    Assert.NotNull(idpath);
        //    var appId = idpath;
        //    if (idpath.Contains('/'))
        //    {
        //        appId = idpath.Split('/')[0];
        //    }

        //    // 获取所有菜单
        //    filterItem = new Dictionary<string, object> { { "FieldName", "idpath" }, { "CompareType", "include" }, { "Value", appId } };
        //    var appAllMenus = await RemoteHelpers.FetchAllDataFromApiAsync($"{gateway}/form/api/DataSource/GetDataTable",
        //                                                           "获取数据源MENUS数据失败",
        //                                                           queryDictionary,
        //                                                           "pageIndex",
        //                                                           true,
        //                                                           bodyDictionary,
        //                                                           response => response["code"]?.ToString() == "1",
        //                                                           response => response["data"] ?? new JArray(),
        //                                                           new HttpClient(),
        //                                                           "id",
        //                                                           "",
        //                                                           false,
        //                                                           token);
        //    Assert.True(appAllMenus.Any());
        //    System.Diagnostics.Debug.WriteLine($"================================================= {appAllMenus.Count} 个菜单 =================================================");



        //    IDbConnection conn = new OracleConnection(targetConnectionString);
        //    conn.Open();
        //    var dbRecords = await conn.QueryAsync($"select * from {table}");
        //    Assert.NotNull(dbRecords);
        //    JArray inserts = new();
        //    JArray updates = new();
        //    JArray deletes = new();
        //    var sqlValueBuilder = new StringBuilder();
        //    var varFlag = ":";


        //    foreach (var appMenu in appAllMenus)
        //    {
        //        var currentDbApp = dbRecords?.FirstOrDefault(x => (x as JObject)?["ID"]?.ToString() == appMenu["ID"]?.ToString());
        //        if (currentDbApp is null)
        //        {
        //            inserts.Add(appMenu);
        //        }
        //        else
        //        {
        //            bool needUpdate = false;
        //            foreach (var propKV in (JObject)appMenu)
        //            {
        //                if (propKV.Value != currentDbApp[propKV.Key])
        //                {
        //                    needUpdate = true;
        //                    break;
        //                }
        //            }
        //            if (needUpdate)
        //            {
        //                updates.Add(appMenu);
        //            }
        //        }
        //    }
        //    deletes = dbRecords?.Where(x => !updates.Any(u => u["ID"] == x["ID"])) as JArray ?? new JArray();

        //    Assert.NotNull(dbRecords);
        //    var insertValueStatement = DatabaseHelper.GetInsertSqlValueStatement(varFlag, dbRecords.First());
        //    var updateValueStatement = DatabaseHelper.GetUpdateSqlValueStatement(varFlag, dbRecords.First());
        //    List<string> insertingSqls = new();
        //    List<string> updatingSqls = new();
        //    var formattedInserts = NodesHelper.GetDynamicChildrenRecursively(inserts, "ID", "PARENTID", "Children");
        //    foreach (var insert in formattedInserts)
        //    {
        //        insert["CREATEDTIME"] = Convert.ToDateTime(insert["CREATEDTIME"]);
        //        var sql = $"insert into {table} values({insertValueStatement})";
        //        insertingSqls.Add(sql);

        //        var parameter = insert.ToObject<Dictionary<string, object>>();

        //        var inserted = await conn.ExecuteAsync(sql, parameter);
        //        Assert.True(inserted > 0);

        //    }

        //    foreach (var update in updates)
        //    {
        //        var sql = $"update {table} set {updateValueStatement}";
        //        updatingSqls.Add(sql);
        //        var updated = await conn.ExecuteAsync(sql, update);
        //        Assert.True(updated > 0);
        //    }
        //}

        [Fact]
        public async Task FetchDataModelAsync()
        {
            string gateway = _configuration["SyncFromApiToDb:Gateway"] ?? string.Empty;
            string mainModelId = _configuration["SyncFromApiToDb:MainModelId"] ?? string.Empty;
            string token = _configuration["SyncFromApiToDb:Token"] ?? string.Empty;
            string appDB = _configuration["SyncFromApiToDb:AppDB"] ?? string.Empty;
            string targetConnectionString = _configuration["SyncFromApiToDb:TargetConnectionString"] ?? string.Empty;

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
                    System.Diagnostics.Debug.WriteLine($"================================================= 数据模型未找到 {modelId} =================================================");
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
                System.Diagnostics.Debug.WriteLine($"================================================= {table}: {modelFields.Count} 条数据 =================================================");

                IDbConnection conn = new OracleConnection(targetConnectionString);
                conn.Open();
                await DatabaseHelper.SyncDataAsync(conn, "DevDataModel", new List<JToken> { dm }, new string[] { "NO" }, new string[] { "CREATEDTIME", "UPDATEDTIME" });
                await DatabaseHelper.SyncDataAsync(conn, table, modelFields, new string[] { "NO" }, new string[] { "CREATEDTIME", "UPDATEDTIME" });


                var childModelFields = modelFields.Where(x => x["DATATYPE"]?.ToString() == "21");
                foreach (var field in childModelFields)
                {
                    var childModelId = field["REFMODELID"]?.ToString();
                    if (string.IsNullOrWhiteSpace(childModelId))
                    {
                        System.Diagnostics.Debug.WriteLine($"================================================= {field["ID"]}[{field["NAME"]}]是明细表字段, 但是对应数据模型ID为空 =================================================");
                        continue;
                    }
                    System.Diagnostics.Debug.WriteLine($"================================================= 子模型{field["ID"]}[{field["NAME"]}]正在同步 =================================================");
                    await fetchDataModelsRecursivelyAsync(childModelId);
                }
            }
            #endregion

            #region 同步表单

            #endregion
        }

        [Fact]
        public async Task SyncFromDBToDB_SigleTable()
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

        [Fact]
        async Task SyncFromDBToDB_AllTables()
        {
            #region 参数
            var sourceConnectionString = _configuration["SyncFromDbToDb:SourceConnectionString"] ?? throw new Exception($"请在配置文件中添加源数据库连接字符串");
            var targetConnectionString = _configuration["SyncFromDbToDb:TargetConnectionString"] ?? throw new Exception($"请在配置文件中添加目标数据库连接字符串");
            var _sourceDb = "IDUO_ENGINE";
            // 生成SQL: 获取所有表SqlGetDbTablesInfo -> 生成SQL: 获取表全名GetTableFullName -> 生成SQL: 获取表数据GetQuerySql
            #endregion

            using var targetConn = DatabaseInfo.GetDbConnection(targetConnectionString);
            targetConn.Open();

            // 1.生成所有表的insert语句
            List<TableSqlsInfo> allTablesInsertSqls = new();

            using (var conn = DatabaseInfo.GetDbConnection(sourceConnectionString))
            {
                // 数据源-数据库
                var res = await conn.QueryAsync(DatabaseInfo.SqlGetDbTablesInfo(_sourceDb));
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
