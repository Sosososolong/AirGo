using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Sylas.RemoteTasks.Database.SyncBase;
using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.Database
{
    public class QueryConditionBuilderTest : IClassFixture<TestFixture>
    {
        private readonly ITestOutputHelper _outputHelper;
        private readonly DatabaseInfo _databaseInfo;
        private readonly IConfiguration _configuration;
        private readonly Dictionary<string, string> _connectionStrings;
        public QueryConditionBuilderTest(ITestOutputHelper outputHelper, TestFixture fixture)
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
        /// 分页查询测试 - 是否能够处理长整型
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task QueryPagedData()
        {
            //string connName = "MySqlIX";
            //var connectionString = _configuration.GetConnectionString(connName) ?? throw new Exception($"数据库连接字符串配置{connName}错误");
            
            var connectionString = _connectionStrings[DatabaseType.Pg.ToString()];
            using var conn = DatabaseInfo.GetDbConnection(connectionString);
            var filter = new DataFilter()
            {
                FilterItems = [
                    new FilterItem("formmodelType", "=", 11),
                ]
            };
            var records = await DatabaseInfo.QueryPagedDataAsync<IDictionary<string, object>>("dev_copy", 1, 10, conn, filter, null);
            _outputHelper.WriteLine(JsonConvert.SerializeObject(records.Data.First()));
        }

        /// <summary>
        /// 使用多个QueryTablesSqlBuilder构建复杂的条件的Sql查询
        /// </summary>
        [Fact]
        public async Task QueryTablesSqlBuilder_BuildMultiTablesQuerySql_TestAsync()
        {
            string nickNamekeyword1 = "方";
            string nickNamekeyword2 = "张";
            string json = """
        {
          "select": {
            "tableName": "users",
            "alias": "alias_users",
            "selectColumns": [],
            "onConditions": null
          },
          "leftJoins": [
            {
              "tableName": "userroles",
              "alias": "alias_userroles",
              "selectColumns": [],
              "onConditions": {
                "filterItems": [
                  { "fieldName": "alias_users.id", "compareType": "=", "value": "alias_userroles.UserId" }
                ],
                "filterItemsLogicType": 2
              }
            },
            {
              "tableName": "roles",
              "alias": "alias_roles",
              "selectColumns": ["name"],
              "onConditions": {
                "filterItems": [
                  { "fieldName": "alias_userroles.RoleId", "compareType": "=", "value": "alias_roles.Id" }
                ],
                "filterItemsLogicType": 2
              }
            }
          ],
          "where": {
            "filterItems": [
              {
                "filterItems": [
                  { "fieldName": "alias_users.OrderNo", "compareType": ">", "value": "0" },
                  { "fieldName": "alias_users.NickName", "compareType": "include", "value": "{NICKNAME1}" },
                ],
                "filterItemsLogicType": 0
              },
              {
                "filterItems": [
                  { "fieldName": "alias_users.OrderNo", "compareType": "<", "value": "25" },
                  { "fieldName": "alias_users.NickName", "compareType": "include", "value": "{NICKNAME2}" },
                ],
                "filterItemsLogicType": 0
              }
            ],
            "filterItemsLogicType": 1,
          },
          "orderBy": [
            { "code": "alias_users.OrderNo", "orderType": "asc" },
            { "code": "alias_users.CreatedTime", "orderType": "desc" }
          ],
          "page": { "pageIndex": 1, "pageSize": 10 }
        }
        """;
            json = json.Replace("{NICKNAME1}", nickNamekeyword1).Replace("{NICKNAME2}", nickNamekeyword2);

            var selectTablesDto = JsonConvert.DeserializeObject<QueryTablesInDto>(json) ?? throw new Exception("测试json异常");

            QuerySqlBuilder querySqlBuilder = QuerySqlBuilder
                .UseDatabase(DatabaseType.MySql)
                .Select(selectTablesDto.Select.TableName)
                .LeftJoins(selectTablesDto.LeftJoins)
                .Where(selectTablesDto.Where)
                .OrderBy(new OrderField("alias_users.OrderNo", true), new OrderField("alias_users.CreatedTime", false))
                .Page(new());
            SqlInfo sql = querySqlBuilder.Build();

            string expectedSql = "SELECT alias_users.*,alias_roles.name FROM users AS alias_users LEFT JOIN userroles AS alias_userroles ON alias_users.id = alias_userroles.UserId LEFT JOIN roles AS alias_roles ON alias_userroles.RoleId = alias_roles.Id WHERE (alias_users.OrderNo > @OrderNo AND alias_users.NickName LIKE CONCAT(CONCAT('%', @NickName), '%')) OR (alias_users.OrderNo < @OrderNo1 AND alias_users.NickName LIKE CONCAT(CONCAT('%', @NickName1), '%')) ORDER BY alias_users.OrderNo ASC, alias_users.CreatedTime DESC LIMIT 0,10";
            Dictionary<string, object> expectedParameters = new()
            {
                ["OrderNo"] = "0",
                ["NickName"] = nickNamekeyword1,
                ["OrderNo1"] = "25",
                ["NickName1"] = nickNamekeyword2
            };
            _outputHelper.WriteLine(sql.Sql);
            Assert.Equal(expectedSql, sql.Sql);
            Assert.Equal(JsonConvert.SerializeObject(expectedParameters), JsonConvert.SerializeObject(sql.Parameters));

            string connName = "MySqlIX";
            var connectionString = _configuration.GetConnectionString(connName) ?? throw new Exception($"数据库连接字符串配置{connName}错误");
            using var sqlExecute = DatabaseInfo.GetDbConnection(connectionString);
            var expectedQueryResult = (await sqlExecute.QueryAsync<dynamic>(expectedSql, expectedParameters)).ToList();
            var actualQueryResult = (await sqlExecute.QueryAsync<dynamic>(sql.Sql, sql.Parameters)).ToList();

            Assert.True(expectedQueryResult.Count > 0);

            for (int i = 0; i < expectedQueryResult.Count; i++)
            {
                _outputHelper.WriteLine($"第{i + 1}条数据: expected: {expectedQueryResult[i].NickName}; actual: {actualQueryResult[i].NickName}");
            }

            Assert.True(expectedQueryResult.Count == actualQueryResult.Count);
            Assert.True(JsonConvert.SerializeObject(expectedQueryResult) == JsonConvert.SerializeObject(actualQueryResult));
        }

        /// <summary>
        /// 使用多个QueryTablesSqlBuilder构建复杂的条件, 包含GroupBy
        /// </summary>
        //[Fact]
        public async Task QueryTablesSqlBuilder_BuildMultiTablesQuerySql_WithGroupBy_TestAsync()
        {
            string nickNamekeyword1 = "方";
            string nickNamekeyword2 = "张";
            string json = """
        {
          "select": {
            "tableName": "users",
            "alias": "alias_users",
            "selectColumns": [],
            "onConditions": null
          },
          "leftJoins": [
            {
              "tableName": "userroles",
              "alias": "alias_userroles",
              "selectColumns": [],
              "onConditions": {
                "filterItems": [
                  { "fieldName": "alias_users.id", "compareType": "=", "value": "alias_userroles.UserId" }
                ],
                "filterItemsLogicType": 2
              }
            },
            {
              "tableName": "roles",
              "alias": "alias_roles",
              "selectColumns": ["name"],
              "onConditions": {
                "filterItems": [
                  { "fieldName": "alias_userroles.RoleId", "compareType": "=", "value": "alias_roles.Id" }
                ],
                "filterItemsLogicType": 2
              }
            }
          ],
          "where": {
            "filterItems": [
              {
                "filterItems": [
                  { "fieldName": "alias_users.OrderNo", "compareType": ">", "value": "0" },
                  { "fieldName": "alias_users.NickName", "compareType": "include", "value": "{NICKNAME1}" },
                ],
                "filterItemsLogicType": 0
              },
              {
                "filterItems": [
                  { "fieldName": "alias_users.OrderNo", "compareType": "<", "value": "25" },
                  { "fieldName": "alias_users.NickName", "compareType": "include", "value": "{NICKNAME2}" },
                ],
                "filterItemsLogicType": 0
              }
            ],
            "filterItemsLogicType": 1,
          },
          "group": {
            "fieldName": "username",
            "having": {
              "filterItems": [{ "fieldName": "alias_users.UserName", "compareType": "include", "value": "zhang" }],
              "filterItemsLogicType": 2
            }
          },
          "orderBy": [
            { "code": "alias_users.OrderNo", "orderType": "asc" },
            { "code": "alias_users.CreatedTime", "orderType": "desc" }
          ],
          "page": { "pageIndex": 1, "pageSize": 10 }
        }
        """;
            json = json.Replace("{NICKNAME1}", nickNamekeyword1).Replace("{NICKNAME2}", nickNamekeyword2);

            var selectTablesDto = JsonConvert.DeserializeObject<QueryTablesInDto>(json) ?? throw new Exception("测试json异常");
            var topLevelFilterGroup = selectTablesDto.Where;

            QuerySqlBuilder querySqlBuilder = QuerySqlBuilder
                .UseDatabase(DatabaseType.MySql)
                .Select(selectTablesDto.Select.TableName)
                .LeftJoins(selectTablesDto.LeftJoins)
                .Where(topLevelFilterGroup)
                .Group(selectTablesDto.Group)
                .OrderBy(new OrderField("alias_users.OrderNo", true), new OrderField("alias_users.CreatedTime", false))
                .Page(new());
            SqlInfo sql = querySqlBuilder.Build();

            string expectedSql = "SELECT alias_users.*,alias_roles.name FROM users AS alias_users LEFT JOIN userroles AS alias_userroles ON alias_users.id = alias_userroles.UserId LEFT JOIN roles AS alias_roles ON alias_userroles.RoleId = alias_roles.Id WHERE (alias_users.OrderNo > @OrderNo AND alias_users.NickName LIKE CONCAT(CONCAT('%', @NickName), '%')) OR (alias_users.OrderNo < @OrderNo1 AND alias_users.NickName LIKE CONCAT(CONCAT('%', @NickName1), '%')) GROUP BY username HAVING alias_users.UserName LIKE CONCAT(CONCAT('%', @UserName), '%') ORDER BY alias_users.OrderNo ASC, alias_users.CreatedTime DESC LIMIT 0,10";
            Dictionary<string, object> expectedParameters = new()
            {
                ["OrderNo"] = "0",
                ["NickName"] = nickNamekeyword1,
                ["OrderNo1"] = "25",
                ["NickName1"] = nickNamekeyword2,
                ["UserName"] = "zhang"
            };
            _outputHelper.WriteLine(sql.Sql);
            Assert.Equal(expectedSql, sql.Sql);
            Assert.Equal(JsonConvert.SerializeObject(expectedParameters), JsonConvert.SerializeObject(sql.Parameters));

            string connName = "MySqlIX";
            var connectionString = _configuration.GetConnectionString(connName) ?? throw new Exception($"缺少数据库连接字符串配置:{connName}");
            using var sqlExecute = DatabaseInfo.GetDbConnection(connectionString);
            var expectedQueryResult = (await sqlExecute.QueryAsync<dynamic>(expectedSql, expectedParameters)).ToList();
            var actualQueryResult = (await sqlExecute.QueryAsync<dynamic>(sql.Sql, sql.Parameters)).ToList();

            Assert.True(expectedQueryResult.Count > 0);

            for (int i = 0; i < expectedQueryResult.Count; i++)
            {
                _outputHelper.WriteLine($"第{i + 1}条数据: expected: {expectedQueryResult[i].NickName}({expectedQueryResult[i].UserName}); actual: {actualQueryResult[i].NickName}({actualQueryResult[i].UserName})");
            }

            Assert.True(expectedQueryResult.Count == actualQueryResult.Count);
            Assert.True(JsonConvert.SerializeObject(expectedQueryResult) == JsonConvert.SerializeObject(actualQueryResult));
        }
    }
}
