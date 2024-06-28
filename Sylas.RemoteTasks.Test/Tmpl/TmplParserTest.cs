using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Sylas.RemoteTasks.Utils.Extensions.Text;
using Sylas.RemoteTasks.Utils.Template;
using Xunit.Abstractions;
using static System.Reflection.Metadata.BlobBuilder;

namespace Sylas.RemoteTasks.Test.Tmpl
{
    public class TmplParserTest(ITestOutputHelper outputHelper, TestFixture fixture) : IClassFixture<TestFixture>
    {
        private readonly IConfiguration _configuration = fixture.ServiceProvider.GetRequiredService<IConfiguration>();
        const string _idPathValue = "App1/M1";
        const string _appIdValue = "App1";
        const string _appItems = @"[{""Id"":""ttnc1"",""Name"":""天天农场"",""Type"":""report"",""DisplayType"":null,""BusId"":""ttnc1"",""BusCode"":null,""OpenUrl"":null,""Icon"":""#icon-huolongguo,#22d7bb,#ff000000"",""Items"":[],""ApplicationData"":null},{""Id"":""M1"",""Name"":""农场商品"",""Type"":""form"",""DisplayType"":null,""BusId"":""M1"",""BusCode"":null,""OpenUrl"":null,""Icon"":""#icon-huolongguo,#22d7bb,#ff000000"",""Items"":[],""ApplicationData"":null},{""Id"":""X1"",""Name"":""农场基本信息"",""Type"":""report"",""DisplayType"":null,""BusId"":""X1"",""BusCode"":null,""OpenUrl"":null,""Icon"":""#icon-huolongguo,#22d7bb,#ff000000"",""Items"":[],""ApplicationData"":null},{""Id"":""L1"",""Name"":""农场列表"",""Type"":""report"",""DisplayType"":null,""BusId"":""L1"",""BusCode"":null,""OpenUrl"":null,""Icon"":""#icon-huolongguo,#22d7bb,#ff000000"",""Items"":[],""ApplicationData"":null}]";
        Dictionary<string, object> _dataContext = new()
        {
                { "$formAppId", _appIdValue },
                { "$QueryDictionary", new Dictionary<string, object>{ { "db", "ttnc" }, { "table", "menus" }, { "pageIndex", 1 }, { "pageSize", 500 } } },
                { "$data", new List<object> {
                        new { Id = "M1", ModuleCode = "system", Name = "天天农场", Url = "/view/M1", IconName = "#iconhuolongguo=22d7bb=#ff000000", OpenType = 1, OrderNo = 20, IsSetTop = false, IsSelected = false, IsDisabled = false,
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
            // 模板动态取值 - 获取某个属性
            var idPath = TmplHelper.ResolveExpressionValue("DataPropertyParser[$data[0].IDPATH]", _dataContext);
            Assert.Equal(idPath, _idPathValue);

            // 扩展数据上下文(key相同则覆盖)
            _dataContext.BuildDataContextBySource(_dataContext["$data"], ["$idpath=DataPropertyParser[$data[0].IDPATH]", "$appid=RegexSubStringParser[$idpath reg `(?<appid>\\w+)/` appid]"]);
            Assert.Equal(_dataContext["$idpath"], _idPathValue);

            // 模板动态取值 - 正则表达式匹配获取某个分组
            var appId = TmplHelper.ResolveExpressionValue("RegexSubStringParser[$idpath reg `(?<appid>\\w+)/` appid]", _dataContext);
            Assert.Equal(appId, _appIdValue);
        }

        /// <summary>
        /// 使用模板表达式 - "属性解析器", 获取集合属性的指定索引的子项
        /// </summary>
        [Fact]
        public void DataPropertyParserWithCollectionIndexTest()
        {
            // 使用索引从集合中获取对象
            string parser = "DataPropertyParser[$data[0]]";
            var app = TmplHelper.ResolveExpressionValue(parser, _dataContext);
            string appType = app.GetType().Name;
            Assert.Contains("AnonymousType", appType);
            Assert.Contains("List", _dataContext["$data"].GetType().Name);

            // 校验给$data重新赋值, $data保持原始属性, 不会丢失原始属性
            _dataContext.BuildDataContextBySource(_dataContext["$data"], [$"$app={parser}"]);
            Assert.Contains("List", _dataContext["$data"].GetType().Name);
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
        /// 使用模板表达式 - "类型转换解析器" - 将json字符串转换为数组
        /// </summary>
        [Fact]
        public void TypeConversionParserTest()
        {
            _dataContext["appItems"] = _appItems;

            // 字符串转换为集合类型
            var parser = "TypeConversionParser[$appItems as List]";
            var itemsList = TmplHelper.ResolveExpressionValue(parser, _dataContext);
            Assert.Equal((itemsList as List<object>)?.Count, 4);

            // 扩展数据上下文
            _dataContext.BuildDataContextBySource(_dataContext["$data"], [$"$appItemList={parser}"]);
            var item2Name = TmplHelper.ResolveExpressionValue("DataPropertyParser[$appItemList[1].name]", _dataContext).ToString();
            Assert.Equal("农场商品", item2Name);
        }
        /// <summary>
        /// 使用模板表达式 - "数组元素属性解析器" - 将数组的每个元素的指定字段取出来组成新的数组
        /// </summary>
        [Fact]
        public void CollectionSelectParserTest()
        {
            _dataContext["appItemList"] = _dataContext["appItems"] = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(_appItems) ?? throw new Exception("无法解析字典集合");
            // 集合中选取元素组成新集合, 支持包含子节点的递归取值 $menuIds = CollectionSelectParser[$appItemList select Id -r]
            string parser = "CollectionSelectParser[$appItemList select Id -r]";
            var menuIds = TmplHelper.ResolveExpressionValue(parser, _dataContext);
            string menuIdsJson = JsonConvert.SerializeObject(menuIds);
            Assert.Equal("[\"ttnc1\",\"M1\",\"X1\",\"L1\"]", menuIdsJson);
            _dataContext.BuildDataContextBySource(_dataContext["$data"], [$"menuIds={parser}"]);
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

        string _text = $"111{Environment.NewLine} {Environment.NewLine} 222{Environment.NewLine}   $for  item in users   {Environment.NewLine} <span>${{item.name}}</span> <span>${{item.age}}</span> <span>$${{item.email}}</span>{Environment.NewLine} $for  item in imgs   {Environment.NewLine} <image href=\"${{item.url}}\" width=\"item.width\" />{Environment.NewLine} $forend{Environment.NewLine} $forend";
        /// <summary>
        /// 解析出for循环脚本块
        /// </summary>
        [Fact]
        public void TextBlocksResolverTest()
        {
            var resolvedInfo = _text.GetBlocks("$for", "$forend");
            var blocks = resolvedInfo.SpecifiedBlocks;
            var lineInfos = resolvedInfo.SequenceLineInfos;
            Assert.Single(blocks);
            Assert.Equal(3, blocks.First().Count);
            Assert.Equal("   $for  item in users   ", blocks.First()[0].Content);
            Assert.Equal(" <span>${item.name}</span> <span>${item.age}</span> <span>$${item.email}</span>", blocks.First()[1].Content);
            Assert.Equal(" $forend", blocks.First()[2].Content);
            
            Assert.Single(blocks.First().Children);
            var firstChild = blocks.First().Children.First();
            Assert.Equal(3, firstChild.Count);
            Assert.Equal(" $for  item in imgs   ", firstChild[0].Content);
            Assert.Equal(" <image href=\"${item.url}\" width=\"item.width\" />", firstChild[1].Content);
            Assert.Equal(" $forend", firstChild[2].Content);

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
    }
}
