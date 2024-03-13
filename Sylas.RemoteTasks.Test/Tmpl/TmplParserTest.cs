using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sylas.RemoteTasks.Utils.Template;
using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.Tmpl
{
    public class TmplParserTest(ITestOutputHelper outputHelper, TestFixture fixture) : IClassFixture<TestFixture>
    {
        private readonly IConfiguration _configuration = fixture.ServiceProvider.GetRequiredService<IConfiguration>();

        [Fact]
        public void ResolveTemplateWithParserTest()
        {
            var dataContext = new Dictionary<string, object>
            {
                { "$formAppId", "8321c2a896b443c1ba9f5706ff424e05" },
                { "$QueryDictionary", new Dictionary<string, object>{ { "db", "bpmDB" }, { "table", "sysmenu" }, { "pageIndex", 1 }, { "pageSize", 500 } } },
                { "$data", new List<object> {
                        new { Id = "1113577E5EB550BD981012", ModuleCode = "bpm", Name = "客户环境信息", Url = "/iForm/1113577E5EB550BD981012", IconName = "#iconhuolongguo=22d7bb=#ff000000", OpenType = 1, OrderNo = 20, IsSetTop = false, IsSelected = false, IsDisabled = false,
                            ParentId = "8321c2a896b443c1ba9f5706ff424e05",
                            IdPath = "8321c2a896b443c1ba9f5706ff424e05/1113577E5EB550BD981012"
                        }
                    }
                }
            };

            var value = TmplHelper.ResolveExpressionValue("$data[0].IDPATH", dataContext);
            outputHelper.WriteLine(value.ToString());
            Assert.Equal(value, "8321c2a896b443c1ba9f5706ff424e05/1113577E5EB550BD981012");

            value = TmplHelper.ResolveExpressionValue("DataPropertyParser[$data[0].IDPATH]", dataContext);
            outputHelper.WriteLine(value.ToString());
            Assert.Equal(value, "8321c2a896b443c1ba9f5706ff424e05/1113577E5EB550BD981012");

            value = TmplHelper.ResolveExpressionValue("idpath value is DataPropertyParser[$data[0].IDPATH]", dataContext);
            outputHelper.WriteLine(value.ToString());
            Assert.Equal(value, "idpath value is 8321c2a896b443c1ba9f5706ff424e05/1113577E5EB550BD981012");
        }
    }
}
