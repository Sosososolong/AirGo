using Dapper;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Oracle.ManagedDataAccess.Client;
using Sylas.RemoteTasks.App.Utils;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text;
using Xunit;

namespace Sylas.RemoteTasks.Test.RemoteHelper
{
    public class FetchAllDataByApiTest
    {
        private readonly IConfiguration _configuration;
        public FetchAllDataByApiTest()
        {
            using var fs = new FileStream("parameters.log", FileMode.Open);
            _configuration = new ConfigurationBuilder()
                .AddJsonStream(fs)
                .Build();
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
            string gateway = _configuration["gateway"] ?? string.Empty;
            string mainModelId = _configuration["mainModelId"] ?? string.Empty;
            string token = _configuration["token"] ?? string.Empty;
            string appDB = _configuration["appDB"] ?? string.Empty;
            string targetConnectionString = _configuration["targetConnectionString"] ?? string.Empty;

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
    }
}
