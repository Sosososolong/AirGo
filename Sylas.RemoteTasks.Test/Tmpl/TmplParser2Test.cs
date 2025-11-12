using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.Common.Extensions;
using Sylas.RemoteTasks.Utils.Template;
using System.Collections;
using System.Text.Json;
using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.Tmpl
{
    public class TmplParser2Test(ITestOutputHelper outputHelper) : IClassFixture<TestFixture>
    {
        #region 常量
        const string _idPathValue = "App1/M1";
        const string _appIdValue = "App1";
        const string _appItems = """
                        [
              {
                "Id": "ttnc1",
                "Name": "天天农场",
                "Type": "report",
                "DisplayType": null,
                "BusId": "ttnc1",
                "BusCode": null,
                "OpenUrl": null,
                "Icon": "#icon-huolongguo,#22d7bb,#ff000000",
                "Items": [],
                "ApplicationData": null
              },
              {
                "Id": "M1",
                "Name": "农场商品",
                "Type": "form",
                "DisplayType": null,
                "BusId": "M1",
                "BusCode": null,
                "OpenUrl": null,
                "Icon": "#icon-huolongguo,#22d7bb,#ff000000",
                "Items": [],
                "ApplicationData": null
              },
              {
                "Id": "X1",
                "Name": "农场基本信息",
                "Type": "report",
                "DisplayType": null,
                "BusId": "X1",
                "BusCode": null,
                "OpenUrl": null,
                "Icon": "#icon-huolongguo,#22d7bb,#ff000000",
                "Items": [],
                "ApplicationData": null
              },
              {
                "Id": "L1",
                "Name": "农场列表",
                "Type": "report",
                "DisplayType": null,
                "BusId": "L1",
                "BusCode": null,
                "OpenUrl": null,
                "Icon": "#icon-huolongguo,#22d7bb,#ff000000",
                "Items": [
                    {
                        "Id": "L1-1",
                        "Name": "农场列表-1",
                        "Type": "report",
                        "DisplayType": null,
                        "BusId": "L1-1",
                        "BusCode": null,
                        "OpenUrl": null,
                        "Icon": "#icon-huolongguo,#22d7bb,#ff000000",
                        "Items": [],
                        "ApplicationData": null
                    }
                ],
                "ApplicationData": null
              }
            ]
            """;
        #endregion
        static readonly Dictionary<string, object?> _dataContext = new()
        {
            { "formAppId", _appIdValue },
            { "QueryDictionary", new Dictionary<string, object?>{ { "db", "ttnc" }, { "table", "menus" }, { "pageIndex", 1 }, { "pageSize", 500 } } },
            { "data", new List<object> {
                new {
                        Id = "M1",
                        ModuleCode = "system",
                        Name = "天天农场",
                        Url = "/view/M1",
                        IconName = "#iconhuolongguo=22d7bb=#ff000000", OpenType = 1, OrderNo = 20, IsSetTop = false,    IsSelected = false, IsDisabled = false,
                        ParentId = _appIdValue,
                        IdPath = _idPathValue,
                        Items = _appItems
                    }
                }
            }
        };

        private static readonly string[] _someNames = ["zhangsan", "lisi", "王五"];
        [Fact]
        public void DataExtractor()
        {
            // 复制一份数据上下文, 这样修改$data不会影响原始数据(防止干扰其他测试用例)
            Dictionary<string, object?> dataContext = _dataContext.Copy();
            // 扩展数据上下文(环境变量)(key相同则覆盖)
            TmplHelper2.ResolveExtractors("idpath=$data.0.IDPATH;titles=$data.selectr(Name,天天(?<appid>\\w+))", dataContext);
            outputHelper.WriteLine($"idpath:{dataContext.FirstOrDefault(x => x.Key.Equals("idpath", StringComparison.OrdinalIgnoreCase)).Value}");
        }
        /// <summary>
        /// 正则表达式提取文本
        /// </summary>
        [Fact]
        public void StringRegexExtractorTest()
        {
            Dictionary<string, object?> dataContext = _dataContext.Copy();

            string appid = TmplHelper2.ResolveTmpl("${data.0.idpath.r((\\w+)/\\w).0}", dataContext);
            Assert.Equal("App1", appid);

            TmplHelper2.ResolveExtractors("appid=${data.0.idpath.r((\\w+)/\\w).0}", dataContext);
            Assert.Equal("App1", dataContext["appid"]?.ToString());
        }

        /// <summary>
        /// JsonElement对象作为环境变量
        /// </summary>
        [Fact]
        public void JsonElementObjectTest()
        {
            // 复制一份数据上下文, 这样修改$data不会影响原始数据(防止干扰其他测试用例)
            Dictionary<string, object?> dataContext = _dataContext.Copy();
            using var doc = JsonDocument.Parse(JsonConvert.SerializeObject(dataContext["data"]));
            dataContext["data"] = doc.RootElement;

            // 使用索引从集合中获取对象
            JToken? appName = TmplHelper2.ResolveExpression("$data.0.name", dataContext).Item2;
            Assert.Equal("天天农场", appName);
        }
        [Fact]
        public void DictionaryContainsJsonElementValuesTest()
        {
            var json = JsonConvert.SerializeObject((_dataContext["data"] as IEnumerable)!.Cast<object>().First());
            using JsonDocument doc = JsonDocument.Parse(json);
            Dictionary<string, object?> dataContextJE = new()
            {
                { "formAppId", _appIdValue },
                { "QueryDictionary", new Dictionary<string, object?>{ { "db", "ttnc" }, { "table", "menus" }, { "pageIndex", 1 }, { "pageSize", 500 } } },
                {
                    "data", new List<object> {
                        doc.RootElement,
                    }
                }
            };

            // 模板动态取值 - 获取某个属性
            var idPath = TmplHelper2.ResolveTmpl("$data.0.IDPATH", dataContextJE);
            Assert.Equal(_idPathValue, idPath);

            // 正则表达式提取文本
            string appid = TmplHelper2.ResolveTmpl("${data.0.idpath.r((\\w+)/\\w).0}", dataContextJE);
            Assert.Equal("App1", appid);

            // 提取数据存储到数据上下文
            TmplHelper2.ResolveExtractors("appid=${data.0.idpath.r((\\w+)/\\w).0}", dataContextJE);
            Assert.Equal("App1", dataContextJE["appid"]?.ToString());

            // 字符串模板解析测试
            dataContextJE["idpath"] = _idPathValue;
            // 字符串中使用模板
            var valueWithParser = TmplHelper2.ResolveTmpl("idpath value is $data.0.IDPATH", dataContextJE);
            Assert.Equal($"idpath value is {_idPathValue}", valueWithParser);
        }
        /// <summary>
        /// 字符串模板解析函数(ResolveTmpl)测试
        /// </summary>
        [Fact]
        public void StringTemplateUseResolveTmplTest()
        {
            outputHelper.WriteLine(_idPathValue);
            _dataContext["idpath"] = _idPathValue;
            // 字符串中使用模板
            // idpath value is App1/M1
            var resolved = TmplHelper2.ResolveTmpl("idpath value is $data.0.IDPATH", _dataContext);
            Assert.Equal($"idpath value is {_idPathValue}", resolved);

            var valueWithKey = TmplHelper2.ResolveTmpl("idpath value is ${idpath}", _dataContext);
            outputHelper.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {valueWithKey}");
            Assert.Equal($"idpath value is {_idPathValue}", valueWithKey);
        }

        /// <summary>
        /// 字符串中使用模板表达式 - "属性解析器" - 解析的值是"值数组", 生成多个字符串
        /// </summary>
        [Fact]
        public void ForLoopTest()
        {
            // 数据上下文添加值数组
            _dataContext["names"] = _someNames;
            // 生成"hello zhangsan", "hello lisi", "hello 王五"三个
            string tmpl = """
                for(item in $names){
                hello $item
                }
                """;
            string result = TmplHelper2.ResolveTmpl(tmpl, _dataContext);
            var arr = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(3, arr.Length);
            Assert.Equal("hello zhangsan", arr[0]);
            Assert.Equal("hello lisi", arr[1]);
            Assert.Equal("hello 王五", arr[2]);
        }
        /// <summary>
        /// 字符串中使用模板表达式 - "属性解析器" - 解析的值是"值数组", 生成多个字符串
        /// </summary>
        [Fact]
        public void ForLoopWithJsonElementDataContextTest()
        {
            using var doc = JsonDocument.Parse(JsonConvert.SerializeObject(_someNames));
            // 数据上下文添加值数组
            _dataContext["names"] = doc.RootElement;
            // 生成"hello zhangsan", "hello lisi", "hello 王五"三个
            string tmpl = """
                for(item in $names){
                hello $item
                }
                """;
           string result = TmplHelper2.ResolveTmpl(tmpl, _dataContext);
            var arr = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(3, arr.Length);
            Assert.Equal("hello zhangsan", arr[0]);
            Assert.Equal("hello lisi", arr[1]);
            Assert.Equal("hello 王五", arr[2]);
        }

        /// <summary>
        /// select集合所有元素的指定属性, 支持递归子集
        /// </summary>
        [Fact]
        public void Select_Test()
        {
            // 复制一份数据上下文, 这样修改$data不会影响原始数据(防止干扰其他测试用例)
            Dictionary<string, object?> dataContext = _dataContext.Copy();

            dataContext["appItemList"] = dataContext["appItems"] = JsonConvert.DeserializeObject<List<Dictionary<string, object?>>>(_appItems) ?? throw new Exception("无法解析字典集合");
            // 集合中选取元素组成新集合, 支持包含子节点的递归取值 $menuIds = CollectionSelectParser[$appItemList select Id -r]
            var menuIds = TmplHelper2.ResolveExpression("$appItemList.select(id)", dataContext).Item2;
            string menuIdsJson = JsonConvert.SerializeObject(menuIds);
            // L1-1来自L1的子元素
            Assert.Equal("[\"ttnc1\",\"M1\",\"X1\",\"L1\"]", menuIdsJson);

            // 子集属性Items
            menuIds = TmplHelper2.ResolveExpression("$appItemList.select(id,items)", dataContext).Item2;
            menuIdsJson = JsonConvert.SerializeObject(menuIds);
            // L1-1来自L1的子元素
            Assert.Equal("[\"ttnc1\",\"M1\",\"X1\",\"L1\",\"L1-1\"]", menuIdsJson);
        }
        /// <summary>
        /// 使用模板表达式 - "数组元素属性解析器" - 将数组的每个元素的指定字段取出来组成新的数组
        /// </summary>
        [Fact]
        public void Select_JETest()
        {
            // 复制一份数据上下文, 这样修改$data不会影响原始数据(防止干扰其他测试用例)
            Dictionary<string, object?> dataContext = _dataContext.Copy();

            using var doc = JsonDocument.Parse(_appItems) ?? throw new Exception("无法解析字典集合");
            dataContext["appItemList"] = dataContext["appItems"] = doc.RootElement;
            var menuIds = TmplHelper2.ResolveExpression("$appItemList.select(id)", dataContext).Item2;
            string? menuIdsJson = JsonConvert.SerializeObject(menuIds);
            // L1-1来自L1的子元素
            Assert.Equal("[\"ttnc1\",\"M1\",\"X1\",\"L1\"]", menuIdsJson);

            // 子集Items
            menuIds = TmplHelper2.ResolveExpression("$appItemList.select(id,items)", dataContext).Item2;
            menuIdsJson = JsonConvert.SerializeObject(menuIds);
            // L1-1来自L1的子元素
            Assert.Equal("[\"ttnc1\",\"M1\",\"X1\",\"L1\",\"L1-1\"]", menuIdsJson);
        }
        /// <summary>
        /// selectr结合元素不是对象而是字符串, 那么要取元素自身(字符串), 所以就不需要第一个属性参数了
        /// </summary>
        [Fact]
        public void SelectR_Test()
        {
            // 复制一份数据上下文, 这样修改$data不会影响原始数据(防止干扰其他测试用例)
            Dictionary<string, object?> dataContext = _dataContext.Copy();

            dataContext["allMenuUrls"] = new List<string> { "/iList/ttnc1", "/iList/M1", "/iList/X1", "/iList/L1" };
            string tmpl = "listFormIds=$allMenuUrls.selectr(,/iList/(?<formId>\\w+))";
            TmplHelper2.ResolveExtractors(tmpl, dataContext);
            Assert.Equal("ttnc1", (dataContext["listFormIds"] as IEnumerable<object>)!.First().ToString());
        }
        /// <summary>
        /// selectr结合元素不是对象而是字符串, 那么要取元素自身(字符串), 所以就不需要第一个属性参数了
        /// </summary>
        [Fact]
        public void SelectR_JETest()
        {
            // 复制一份数据上下文, 这样修改$data不会影响原始数据(防止干扰其他测试用例)
            Dictionary<string, object?> dataContext = _dataContext.Copy();

            using JsonDocument doc = JsonDocument.Parse(JsonConvert.SerializeObject(new List<string> { "/iList/ttnc1", "/iList/M1", "/iList/X1", "/iList/L1" }));
            dataContext["allMenuUrls"] = new List<string> { "/iList/ttnc1", "/iList/M1", "/iList/X1", "/iList/L1" };
            string tmpl = "listFormIds=$allMenuUrls.selectr(,/iList/(?<formId>\\w+))";
            TmplHelper2.ResolveExtractors(tmpl, dataContext);
            Assert.Equal("ttnc1", (dataContext["listFormIds"] as IEnumerable<object>)!.First().ToString());
        }
    }
}
