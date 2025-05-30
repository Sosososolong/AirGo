using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Ocsp;
using Sylas.RemoteTasks.Common;
using Sylas.RemoteTasks.Common.Extensions;
using Sylas.RemoteTasks.Utils.Extensions.Text;
using Sylas.RemoteTasks.Utils.Template;
using System.Collections;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.Tmpl
{
    public class TmplParserTest(ITestOutputHelper outputHelper, TestFixture fixture) : IClassFixture<TestFixture>
    {
        private readonly IConfiguration _configuration = fixture.ServiceProvider.GetRequiredService<IConfiguration>();
        const string _idPathValue = "App1/M1";
        const string _appIdValue = "App1";
        const string _appItems = @"[{""Id"":""ttnc1"",""Name"":""天天农场"",""Type"":""report"",""DisplayType"":null,""BusId"":""ttnc1"",""BusCode"":null,""OpenUrl"":null,""Icon"":""#icon-huolongguo,#22d7bb,#ff000000"",""Items"":[],""ApplicationData"":null},{""Id"":""M1"",""Name"":""农场商品"",""Type"":""form"",""DisplayType"":null,""BusId"":""M1"",""BusCode"":null,""OpenUrl"":null,""Icon"":""#icon-huolongguo,#22d7bb,#ff000000"",""Items"":[],""ApplicationData"":null},{""Id"":""X1"",""Name"":""农场基本信息"",""Type"":""report"",""DisplayType"":null,""BusId"":""X1"",""BusCode"":null,""OpenUrl"":null,""Icon"":""#icon-huolongguo,#22d7bb,#ff000000"",""Items"":[],""ApplicationData"":null},{""Id"":""L1"",""Name"":""农场列表"",""Type"":""report"",""DisplayType"":null,""BusId"":""L1"",""BusCode"":null,""OpenUrl"":null,""Icon"":""#icon-huolongguo,#22d7bb,#ff000000"",""Items"":[],""ApplicationData"":null}]";
        static readonly Dictionary<string, object> _dataContext = new()
        {
            { "$formAppId", _appIdValue },
            { "$QueryDictionary", new Dictionary<string, object>{ { "db", "ttnc" }, { "table", "menus" }, { "pageIndex", 1 }, { "pageSize", 500 } } },
            { "$data", new List<object> {
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
        /// <summary>
        /// 使用模板表达式 - "属性解析器"
        /// </summary>
        [Fact]
        public void DataPropertyParserTest()
        {
            // 复制一份数据上下文, 这样修改$data不会影响原始数据(防止干扰其他测试用例)
            Dictionary<string, object> dataContext = _dataContext.Copy();

            // 模板动态取值 - 获取某个属性
            var idPath = TmplHelper.ResolveExpressionValue("DataPropertyParser[$data[0].IDPATH]", dataContext);
            Assert.Equal(idPath, _idPathValue);

            // 扩展数据上下文(key相同则覆盖)
            dataContext.BuildDataContextBySource(dataContext["$data"], ["$idpath=DataPropertyParser[$data[0].IDPATH]", "$appid=RegexSubStringParser[$idpath reg `(?<appid>\\w+)/` appid]"]);
            Assert.Equal(dataContext["$idpath"], _idPathValue);

            // 模板动态取值 - 正则表达式匹配获取某个分组
            var appId = TmplHelper.ResolveExpressionValue("RegexSubStringParser[$idpath reg `(?<appid>\\w+)/` appid]", dataContext);
            Assert.Equal(appId, _appIdValue);
        }
        [Fact]
        public void DataPropertyParser_JETest()
        {
            var json = JsonConvert.SerializeObject((_dataContext["$data"] as IEnumerable)!.Cast<object>().First());
            using JsonDocument doc = JsonDocument.Parse(json);
            Dictionary<string, object>  dataContextJE = new()
            {
                { "$formAppId", _appIdValue },
                { "$QueryDictionary", new Dictionary<string, object>{ { "db", "ttnc" }, { "table", "menus" }, { "pageIndex", 1 }, { "pageSize", 500 } } },
                {
                    "$data", new List<object> {
                        doc.RootElement,
                    }
                }
            };
            // 模板动态取值 - 获取某个属性
            var idPath = TmplHelper.ResolveExpressionValue("DataPropertyParser[$data[0].IDPATH]", _dataContext);
            Assert.Equal(idPath, _idPathValue);

            // 扩展数据上下文(key相同则覆盖)
            dataContextJE.BuildDataContextBySource(dataContextJE["$data"], ["$idpath=DataPropertyParser[$data[0].IDPATH]", "$appid=RegexSubStringParser[$idpath reg `(?<appid>\\w+)/` appid]"]);
            Assert.Equal(dataContextJE["$idpath"], _idPathValue);

            // 模板动态取值 - 正则表达式匹配获取某个分组
            var appId = TmplHelper.ResolveExpressionValue("RegexSubStringParser[$idpath reg `(?<appid>\\w+)/` appid]", dataContextJE);
            Assert.Equal(appId, _appIdValue);
        }

        /// <summary>
        /// 使用模板表达式 - "属性解析器", 获取集合属性的指定索引的子项
        /// </summary>
        [Fact]
        public void DataPropertyParserWithCollectionIndexTest()
        {
            // 复制一份数据上下文, 这样修改$data不会影响原始数据(防止干扰其他测试用例)
            Dictionary<string, object> dataContext = _dataContext.Copy();

            // 使用索引从集合中获取对象
            string parser = "DataPropertyParser[$data[0]]";
            var app = TmplHelper.ResolveExpressionValue(parser, dataContext);
            string appType = app.GetType().Name;
            Assert.Contains("AnonymousType", appType);
            Assert.Contains("List", dataContext["$data"].GetType().Name);

            // 校验给$data重新赋值, $data保持原始属性, 不会丢失原始属性
            dataContext.BuildDataContextBySource(dataContext["$data"], [$"$app={parser}"]);
            Assert.Contains("List", dataContext["$data"].GetType().Name);
        }
        /// <summary>
        /// 使用模板表达式 - "属性解析器", 获取集合属性的指定索引的子项
        /// </summary>
        [Fact]
        public void DataPropertyParserWithCollectionIndexTest_JE()
        {
            // 复制一份数据上下文, 这样修改$data不会影响原始数据(防止干扰其他测试用例)
            Dictionary<string, object> dataContext = _dataContext.Copy();
            using var doc = JsonDocument.Parse(JsonConvert.SerializeObject(dataContext["$data"]));
            dataContext["$data"] = doc.RootElement;

            // 使用索引从集合中获取对象
            string parser = "DataPropertyParser[$data[0].name]";
            var appName = TmplHelper.ResolveExpressionValue(parser, dataContext);
            Assert.Equal("天天农场", appName);

            // 校验给$data重新赋值, $data保持原始属性, 不会丢失原始属性
            dataContext.BuildDataContextBySource(dataContext["$data"], [$"$app={parser}"]);
            appName = TmplHelper.ResolveExpressionValue(parser, dataContext);
            Assert.Equal("天天农场", appName);

            // 集合中的对象是JsonElement类型
            JsonElement je = ((JsonElement)dataContext["$data"]).EnumerateArray().First();
            dataContext["$data"] = new List<object>() { je };
            // 使用索引从集合中获取对象
            parser = "DataPropertyParser[$data[0].name]";
            appName = TmplHelper.ResolveExpressionValue(parser, dataContext);
            Assert.Equal("天天农场", appName);

            // 校验给$data重新赋值, $data保持原始属性, 不会丢失原始属性
            dataContext.BuildDataContextBySource(dataContext["$data"], [$"$app={parser}"]);
            appName = TmplHelper.ResolveExpressionValue(parser, dataContext);
            Assert.Equal("天天农场", appName);
        }

        /// <summary>
        /// 字符串中使用模板表达式 - "属性解析器"
        /// </summary>
        [Fact]
        public void DataPropertyParserWithStringTest()
        {
            _dataContext["idpath"] = _idPathValue;
            // 字符串中使用模板
            var valueWithParser = TmplHelper.ResolveExpressionValue("idpath value is DataPropertyParser[$data[0].IDPATH]", _dataContext);
            Assert.Equal(valueWithParser, $"idpath value is {_idPathValue}");
            var valueWithKey = TmplHelper.ResolveExpressionValue("idpath value is ${idpath}", _dataContext);
            outputHelper.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {valueWithKey}");
            Assert.Equal(valueWithKey, $"idpath value is {_idPathValue}");
        }
        /// <summary>
        /// 字符串中使用模板表达式 - "属性解析器"
        /// </summary>
        [Fact]
        public void DataPropertyParserWithStringTest_JE()
        {
            var json = JsonConvert.SerializeObject((_dataContext["$data"] as IEnumerable)!.Cast<object>().First());
            using JsonDocument doc = JsonDocument.Parse(json);
            Dictionary<string, object> dataContextJE = new()
            {
                { "$formAppId", _appIdValue },
                { "$QueryDictionary", new Dictionary<string, object>{ { "db", "ttnc" }, { "table", "menus" }, { "pageIndex", 1 }, { "pageSize", 500 } } },
                {
                    "$data", new List<object> {
                        doc.RootElement,
                    }
                }
            };

            dataContextJE["idpath"] = _idPathValue;
            // 字符串中使用模板
            var valueWithParser = TmplHelper.ResolveExpressionValue("idpath value is DataPropertyParser[$data[0].IDPATH]", dataContextJE);
            Assert.Equal(valueWithParser, $"idpath value is {_idPathValue}");
            var valueWithKey = TmplHelper.ResolveExpressionValue("idpath value is ${idpath}", dataContextJE);
            outputHelper.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {valueWithKey}");
            Assert.Equal(valueWithKey, $"idpath value is {_idPathValue}");
        }

        /// <summary>
        /// 字符串中使用模板表达式 - "属性解析器" - 解析的值是"值数组", 生成多个字符串
        /// </summary>
        [Fact]
        public void DataPropertyParserWithStringAndCollectionTest()
        {
            // 数据上下文添加值数组
            _dataContext["names"] = _someNames;
            // 生成"hello zhangsan", "hello lisi", "hello 王五"三个
            var parsedValue = TmplHelper.ResolveExpressionValue("hello ${names}", _dataContext);
            Assert.Equal($"hello {_someNames[0]}", (parsedValue as List<object>)?[0]?.ToString());
            Assert.Equal($"hello {_someNames[1]}", (parsedValue as List<object>)?[1]?.ToString());
            Assert.Equal($"hello {_someNames[2]}", (parsedValue as List<object>)?[2]?.ToString());
        }
        /// <summary>
        /// 字符串中使用模板表达式 - "属性解析器" - 解析的值是"值数组", 生成多个字符串
        /// </summary>
        [Fact]
        public void DataPropertyParserWithStringAndCollectionTest_JE()
        {
            using var doc = JsonDocument.Parse(JsonConvert.SerializeObject(_someNames));
            // 数据上下文添加值数组
            _dataContext["names"] = doc.RootElement;
            // 生成"hello zhangsan", "hello lisi", "hello 王五"三个
            var parsedValue = TmplHelper.ResolveExpressionValue("hello ${names}", _dataContext);
            Assert.Equal($"hello {_someNames[0]}", (parsedValue as List<object>)?[0]?.ToString());
            Assert.Equal($"hello {_someNames[1]}", (parsedValue as List<object>)?[1]?.ToString());
            Assert.Equal($"hello {_someNames[2]}", (parsedValue as List<object>)?[2]?.ToString());
        }

        /// <summary>
        /// 使用模板表达式 - "类型转换解析器" - 将json字符串转换为数组
        /// </summary>
        [Fact]
        public void TypeConversionParserTest()
        {
            // 复制一份数据上下文, 这样修改$data不会影响原始数据(防止干扰其他测试用例)
            Dictionary<string, object> dataContext = _dataContext.Copy();

            dataContext["appItems"] = _appItems;

            // 字符串转换为集合类型
            var parser = "TypeConversionParser[$appItems as List]";
            var itemsList = TmplHelper.ResolveExpressionValue(parser, dataContext);
            Assert.Equal(4, (itemsList as IEnumerable)!.Cast<object>().Count());

            // 扩展数据上下文
            dataContext.BuildDataContextBySource(dataContext["$data"], [$"$appItemList={parser}"]);
            var item2Name = TmplHelper.ResolveExpressionValue("DataPropertyParser[$appItemList[1].name]", dataContext).ToString();
            Assert.Equal("农场商品", item2Name);
        }
        /// <summary>
        /// 使用模板表达式 - "类型转换解析器" - 将json字符串转换为数组
        /// </summary>
        [Fact]
        public void TypeConversionParser_JETest()
        {
            // 复制一份数据上下文, 这样修改$data不会影响原始数据(防止干扰其他测试用例)
            Dictionary<string, object> dataContext = _dataContext.Copy();

            using var doc = JsonDocument.Parse(_appItems) ?? throw new Exception("无法解析字典集合");
            dataContext["appItems"] = doc.RootElement;

            // 字符串转换为集合类型
            var parser = "TypeConversionParser[$appItems as List]";
            var itemsList = TmplHelper.ResolveExpressionValue(parser, dataContext);
            IEnumerable itemsEnumerable = (itemsList as IEnumerable)!;
            IEnumerable<object> objects = itemsEnumerable.Cast<object>();
            Assert.Equal(4, objects.Count());

            // 扩展数据上下文
            dataContext.BuildDataContextBySource(dataContext["$data"], [$"$appItemList={parser}"]);
            var item2Name = TmplHelper.ResolveExpressionValue("DataPropertyParser[$appItemList[1].name]", dataContext).ToString();
            Assert.Equal("农场商品", item2Name);
        }
        /// <summary>
        /// 使用模板表达式 - "数组元素属性解析器" - 将数组的每个元素的指定字段取出来组成新的数组
        /// </summary>
        [Fact]
        public void CollectionSelectParserTest()
        {
            // 复制一份数据上下文, 这样修改$data不会影响原始数据(防止干扰其他测试用例)
            Dictionary<string, object> dataContext = _dataContext.Copy();

            dataContext["appItemList"] = dataContext["appItems"] = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(_appItems) ?? throw new Exception("无法解析字典集合");
            // 集合中选取元素组成新集合, 支持包含子节点的递归取值 $menuIds = CollectionSelectParser[$appItemList select Id -r]
            string parser = "CollectionSelectParser[$appItemList select Id -r]";
            var menuIds = TmplHelper.ResolveExpressionValue(parser, dataContext);
            string menuIdsJson = JsonConvert.SerializeObject(menuIds);
            Assert.Equal("[\"ttnc1\",\"M1\",\"X1\",\"L1\"]", menuIdsJson);
            dataContext.BuildDataContextBySource(dataContext, [$"menuIds={parser}"]);
        }
        /// <summary>
        /// 使用模板表达式 - "数组元素属性解析器" - 将数组的每个元素的指定字段取出来组成新的数组
        /// </summary>
        [Fact]
        public void CollectionSelectParser_JETest()
        {
            // 复制一份数据上下文, 这样修改$data不会影响原始数据(防止干扰其他测试用例)
            Dictionary<string, object> dataContext = _dataContext.Copy();

            using var doc = JsonDocument.Parse(_appItems) ?? throw new Exception("无法解析字典集合");
            dataContext["appItemList"] = dataContext["appItems"] = doc.RootElement;
            // 集合中选取元素组成新集合, 支持包含子节点的递归取值 $menuIds = CollectionSelectParser[$appItemList select Id -r]
            string parser = "CollectionSelectParser[$appItemList select Id -r]";
            var menuIds = TmplHelper.ResolveExpressionValue(parser, dataContext);
            string menuIdsJson;
            if (menuIds is not string && menuIds is IEnumerable<object> menuIdsEnumerable)
            {
                menuIdsJson = JsonConvert.SerializeObject(menuIdsEnumerable.Select(x => x.ToString()));
            }
            else
            {
                menuIdsJson = JsonConvert.SerializeObject(menuIds);
            }
            Assert.Equal("[\"ttnc1\",\"M1\",\"X1\",\"L1\"]", menuIdsJson);
            dataContext.BuildDataContextBySource(dataContext, [$"menuIds={parser}"]);
        }

        /// <summary>
        /// 使用表达式 将数组拼接成字符串
        /// </summary>
        [Fact]
        public void CollectionJoinParserTest()
        {
            _dataContext["menuIds"] = new List<string> { "ttnc1", "M1", "X1", "L1" };
            // 集合拼接成字符串
            string parser = "$menuIdsStr=CollectionJoinParser[menuIds join ,]";
            var menuIdsStr = TmplHelper.ResolveExpressionValue(parser, _dataContext);
            Assert.Equal("ttnc1,M1,X1,L1", menuIdsStr);
        }
        /// <summary>
        /// 使用表达式 将数组拼接成字符串
        /// </summary>
        [Fact]
        public void CollectionJoinParser_JETest()
        {
            using JsonDocument doc = JsonDocument.Parse(JsonConvert.SerializeObject(new List<string> { "ttnc1", "M1", "X1", "L1" }));
            _dataContext["menuIds"] = doc.RootElement;
            // 集合拼接成字符串
            string parser = "$menuIdsStr=CollectionJoinParser[menuIds join ,]";
            var menuIdsStr = TmplHelper.ResolveExpressionValue(parser, _dataContext);
            Assert.Equal("ttnc1,M1,X1,L1", menuIdsStr);
        }

        [Fact]
        public void CollectionSelectItemRegexSubStringParserTest()
        {
            // 复制一份数据上下文, 这样修改$data不会影响原始数据(防止干扰其他测试用例)
            Dictionary<string, object> dataContext = _dataContext.Copy();

            dataContext["$allMenuUrls"] = new List<string> { "/iList/ttnc1", "/iList/M1", "/iList/X1", "/iList/L1" };
            string tmpl = "$listFormIds=CollectionSelectItemRegexSubStringParser[$allMenuUrls select SELF reg `/iList/(?<formId>\\w+)` formId]";
            dataContext.BuildDataContextBySource(dataContext, [ tmpl ]);
            Assert.Equal("ttnc1", (dataContext["$listFormIds"] as IEnumerable<object>)!.First().ToString());
        }
        [Fact]
        public void CollectionSelectItemRegexSubStringParser_JETest()
        {
            // 复制一份数据上下文, 这样修改$data不会影响原始数据(防止干扰其他测试用例)
            Dictionary<string, object> dataContext = _dataContext.Copy();

            using JsonDocument doc = JsonDocument.Parse(JsonConvert.SerializeObject(new List<string> { "/iList/ttnc1", "/iList/M1", "/iList/X1", "/iList/L1" }));
            dataContext["$allMenuUrls"] = doc.RootElement;
            string tmpl = "$listFormIds=CollectionSelectItemRegexSubStringParser[$allMenuUrls select SELF reg `/iList/(?<formId>\\w+)` formId]";
            dataContext.BuildDataContextBySource(dataContext, [ tmpl ]);
            Assert.Equal("ttnc1", (dataContext["$listFormIds"] as IEnumerable<object>)!.First().ToString());
        }

        const string _text = """
            111
            
            222
            $for item in users
            <span>$item.name</span> <span>$item.age</span> <span>$item.email</span>
            $for item in imgs
            <image href="$item.url" width="$item.width" />
            $forend
            其他
            $forend
            999
            """;
        /// <summary>
        /// 解析出for循环脚本块
        /// </summary>
        [Fact]
        public void TemplateForLoopTest()
        {
            var resolvedInfo = _text.GetBlocks("$for", "$forend");
            var blocks = resolvedInfo.SpecifiedBlocks;
            var lineInfos = resolvedInfo.SequenceLineInfos;
            Assert.Single(blocks);
            Assert.Equal(3, blocks.First().Count);
            Assert.Equal("$for item in users", blocks.First()[0].Content);
            Assert.Equal("<span>${item.name}</span> <span>${item.age}</span> <span>${item.email}</span>", blocks.First()[1].Content);
            Assert.Equal("$forend", blocks.First()[2].Content);

            Assert.Single(blocks.First().Children);
            var firstChild = blocks.First().Children.First();
            Assert.Equal(3, firstChild.Count);
            Assert.Equal("$for item in imgs", firstChild[0].Content);
            Assert.Equal("<image href=\"${item.url}\" width=\"item.width\" />", firstChild[1].Content);
            Assert.Equal("$forend", firstChild[2].Content);

            PrintBlocks(blocks);
            void PrintBlocks(List<TextBlock> blocks)
            {
                foreach (var block in blocks)
                {
                    // 先判断有没有嵌套文本片段, 有的话先打印
                    if (block.Children.Count > 0)
                    {
                        PrintBlocks(block.Children);
                    }

                    foreach (var line in block)
                    {
                        outputHelper.WriteLine($"{line.LineIndex}: {line.Content}");
                    }
                }
            }

            int i = 0;
            foreach (var line in lineInfos)
            {
                // 0: xxx
                // 1: 111
                Assert.Equal(i, line.Line.LineIndex);
                outputHelper.WriteLine($"{line.Line.LineIndex}: {line.Line.Content}");
                i++;
            }
        }
        [Fact]
        public void GetFunctionsTest()
        {
            string originTxt = """
                BuildEntityClassCodeAsync({{数据库连接字符串}}|||{{数据表表名}})->EntityClassCode->["EntityCalssName", "EntityClassSourceCode", "PrimaryKeyType", "DtoExceptIdPropsCode", "DtoExceptIdAndDateTimePropsCode", "TimeRangeFilterProps"]
                GetDateTimePropAssignCode({EntityClassSourceCode})->DateTimePropAssignCode->[AssignDateTimeWhenAddEntity, AssignDateTimeWhenUpdateEntity]
                {{数据表业务名称}}
                GetFileProjDirAndNamespace({WorkingDir}|||/service/)->ServiceInfo->[ServiceRootDir,ServiceRootNS]
                Pluralize({EntityCalssName})->EntityPluralizeName->[EntityPluralizeName]
                """;
            var matches = originTxt.ResolvePairedSymbolsContent("\\(", "\\)", "(?<FunctionName>[A-Z]\\w+)", "->(?<SavedKey>\\w+)->\\[(?<index>\\d+|[,A-Za-z,\\s,\"]+)\\]");
            foreach (var match in matches)
            {
                outputHelper.WriteLine(match.Value);
                outputHelper.WriteLine($"FunctionName: {match.Groups["FunctionName"]}");
                outputHelper.WriteLine($"SavedKey: {match.Groups["SavedKey"]}");
                outputHelper.WriteLine($"Index: {match.Groups["Index"]}");
                outputHelper.WriteLine($"Content: {match.Groups["Content"]}");
                outputHelper.WriteLine($"OriginText: {match.Groups["OriginText"]}{Environment.NewLine}");
            }
        }
    }
}
