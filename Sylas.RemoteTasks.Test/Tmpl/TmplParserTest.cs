using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.RemoteHostModule;
using Sylas.RemoteTasks.Utils.Template;
using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.Tmpl
{
    public class TmplParserTest(ITestOutputHelper outputHelper, TestFixture fixture) : IClassFixture<TestFixture>
    {
        private readonly IConfiguration _configuration = fixture.ServiceProvider.GetRequiredService<IConfiguration>();

        /// <summary>
        /// 获取DataContext中的用户集合的所有用户的Last Name
        /// 数据是字典结合
        /// </summary>
        [Fact]
        public void ParseTmpl_SELECT_REGEX_With_DictionaryCollection_GetUsersLastNames()
        {
            #region 准备几个测试模板的数据源
            var dataContext1 = new Dictionary<string, object>
            {
                {
                    "$Users", new List<Dictionary<string, object>>
                    {
                        new() { { "Name", "John Smith"}, { "Email", "john.smith@example.com" }, { "Others", new { P1 = "P1", P2 = new { P21 = "VP211111111111111111" } } } },
                        new() { { "Name", "Emily Johnson"}, {"Email", "emily.johnson@example.com" } },
                        new() { { "Name", "Michael Williams"}, {"Email", "michael.williams@example.com" } },
                        new() { { "Name", "Sophia Brown"}, {"Email", "sophia.brown@example.com" } },
                        new() { { "Name", "Matthew Jones"}, {"Email", "matthew.jones@example.com" } },
                        new() { { "Name", "Olivia Davis"}, {"Email", "olivia.davis@example.com" } },
                        new() { { "Name", "Daniel Wilson"}, {"Email", "daniel.wilson@example.com" } },
                        new() { { "Name", "Isabella Taylor"}, {"Email", "isabella.taylor@example.com" } },
                        new() { { "Name", "Alexander Miller"}, {"Email", "alexander.miller@example.com" } },
                        new() { { "Name", "Ava Anderson"}, {"Email", "ava.anderson@example.com" } },
                    }
                },
                {
                    "$User", new Dictionary<string, object>
                    {
                        { "Name", "Emily Johnson"}, {"Email", "emily.johnson@example.com" }, { "Others", new { P1 = "P1", P2 = new { P21 = "VP211111111111111111" } } }
                    }
                }
            };
            var dataContext2 = new Dictionary<string, object>
            {
                {
                    "$Users", new List<dynamic>() {
                        new { Name = "John Smith", Email = "john.smith@example.com", Others = new { P1 = "P1", P2 = new { P21 = "VP211111111111111111" } } },
                        new { Name = "Emily Johnson", Email = "emily.johnson@example.com" },
                        new { Name = "Michael Williams", Email = "michael.williams@example.com" },
                        new { Name = "Sophia Brown", Email = "sophia.brown@example.com" },
                        new { Name = "Matthew Jones", Email = "matthew.jones@example.com" },
                        new { Name = "Olivia Davis", Email = "olivia.davis@example.com" },
                        new { Name = "Daniel Wilson", Email = "daniel.wilson@example.com" },
                        new { Name = "Isabella Taylor", Email = "isabella.taylor@example.com" },
                        new { Name = "Alexander Miller", Email = "alexander.miller@example.com" },
                        new { Name = "Ava Anderson", Email = "ava.anderson@example.com" }
                    }
                },
                {
                    "$User", new
                    {
                        Name = "Emily Johnson", Email = "emily.johnson@example.com", Others = new { P1 = "P1", P2 = new { P21 = "VP211111111111111111" } }
                    }
                }
            };
            var dataContext3 = new Dictionary<string, object>
            {
                {
                    "$Users", JArray.FromObject(new List<dynamic>() {
                        new { Name = "John Smith", Email = "john.smith@example.com", Others = new { P1 = "P1", P2 = new { P21 = "VP211111111111111111" } } },
                        new { Name = "Emily Johnson", Email = "emily.johnson@example.com" },
                        new { Name = "Michael Williams", Email = "michael.williams@example.com" },
                        new { Name = "Sophia Brown", Email = "sophia.brown@example.com" },
                        new { Name = "Matthew Jones", Email = "matthew.jones@example.com" },
                        new { Name = "Olivia Davis", Email = "olivia.davis@example.com" },
                        new { Name = "Daniel Wilson", Email = "daniel.wilson@example.com" },
                        new { Name = "Isabella Taylor", Email = "isabella.taylor@example.com" },
                        new { Name = "Alexander Miller", Email = "alexander.miller@example.com" },
                        new { Name = "Ava Anderson", Email = "ava.anderson@example.com" }
                    })
                },
                {
                    "$User", JObject.FromObject(new
                    {
                        Name = "Emily Johnson", Email = "emily.johnson@example.com", Others = new { P1 = "P1", P2 = new { P21 = "VP211111111111111111" } }
                    })
                }
            };
            #endregion
            List<Dictionary<string, object>> dataContexts = [dataContext1, dataContext2, dataContext3];
            foreach (var dataContext in dataContexts)
            {
                var start = DateTime.Now;
                #region Parser解析对象
                var result2 = TmplHelper.ResolveExpressionValue("$User.Others.P2.P21", dataContext);
                outputHelper.WriteLine($"Parser解析模板表达式 - 数据源是集合: {result2}");
                #endregion

                #region Parser解析集合
                var result3 = TmplHelper.ResolveExpressionValue(@"{{CollectionSelectItemRegexSubStringParser[$users select name reg `\w+\s+(?<lastname>\w+)` lastname]}}", dataContext);
                if (result3 is IEnumerable<object> lastNames)
                {
                    outputHelper.WriteLine($"Parser解析模板表达式 - 数据源是对象: {string.Join(',', lastNames)}");
                }
                #endregion

                outputHelper.WriteLine($"{(DateTime.Now - start).TotalMilliseconds}/ms{Environment.NewLine}");
            }
        }

        /// <summary>
        /// 含模板表达式的字符串, 表达式的皆悉值是集合, 将生成多个结果字符串
        /// </summary>
        [Fact]
        public void ParseTmpl_StringTmpl_ValueIsList()
        {
            Dictionary<string, object> dataConetxt = new() {
            {
                "containers",
                new List<dynamic> {
                    new { ShortName = "con1", Name = "xx.con1.api" },
                    new { ShortName = "con2", Name = "xx.con2.api" },
                    new { ShortName = "con3", Name = "xx.con3.api" },
                    new { ShortName = "con4", Name = "xx.con4.api" },
                    new { ShortName = "con5", Name = "xx.con5.api" },
                    new { ShortName = "con6", Name = "xx.con6.api" },
                    new { ShortName = "con7", Name = "xx.con7.api" },
                }
            },
            { "con1", "xx.con1.api" },
            { "con2", "xx.con2.api" },
            { "con3", "xx.con3.api" },
            { "con4", "xx.con4.api" },
            { "con5", "xx.con5.api" },
            { "con6", "xx.con6.api" },
            { "con7", "xx.con7.api" },
        };
            // 添加 Handler 自定义处理程序
            CommandInfo[] cmds = [
                new() { CommandTxt = "ssh 192.168.1.2 start_1", Name = "192.168.1.2" },
                new() { CommandTxt = "ssh 192.168.1.2 start_2", Name = "192.168.1.2" },

                // 
                new() { CommandTxt = @"dkmgr {{$containers[*].ShortName}}", Name = "{{$containers[*].Name}}" },
                // new() { CommandTxt = "dkmgr con1", Label = "xx.con1.api" },
                // new() { CommandTxt = "dkmgr con2", Label = "xx.con2.api" },
                // new() { CommandTxt = "dkmgr con3", Label = "xx.con3.api" },
                // new() { CommandTxt = "dkmgr con4", Label = "xx.con4.api" },
                // new() { CommandTxt = "dkmgr con5", Label = "xx.con5.api" },
                // new() { CommandTxt = "dkmgr con6", Label = "xx.con6.api" },
                // new() { CommandTxt = "dkmgr con7", Label = "xx.con7.api" },

                new() { CommandTxt = "docker stop xx.con1;docker rm xx.con1.api;cd /www/wwwroot/xx.server/;docker-compose up -d", Name = "xx.con1.api" },
                new() { CommandTxt = "docker stop xx.con2.api;docker rm xx.con2.api;cd /www/wwwroot/xx.server/;docker-compose up -d", Name = "xx.con2.api" },
                new() { CommandTxt = "docker stop xx.con3.api;docker rm xx.con3.api;cd /www/wwwroot/xx.server/;docker-compose up -d", Name = "xx.con3.api" },
                new() { CommandTxt = "docker stop xx.con4.api;docker rm xx.con4.api;cd /www/wwwroot/xx.server/;docker-compose up -d", Name = "xx.con4.api" },
                new() { CommandTxt = "docker stop xx.con5.api;docker rm xx.con5.api;cd /www/wwwroot/xx.server/;docker-compose up -d", Name = "xx.con5.api" },
                new() { CommandTxt = "docker stop xx.con6.api;docker rm xx.con6.api;cd /www/wwwroot/xx.server/;docker-compose up -d", Name = "xx.con6.api" },
                new() { CommandTxt = "docker stop xx.con7.api;docker rm xx.con7.api;cd /www/wwwroot/xx.server/;docker-compose up -d", Name = "xx.con7.api" },

                new() { CommandTxt = "publish_image con1", Name = "xx.con1.api" },
                new() { CommandTxt = "publish_image con2", Name = "xx.con2.api" },
                new() { CommandTxt = "publish_image con3", Name = "xx.con3.api" },
                new() { CommandTxt = "publish_image con4", Name = "xx.con4.api" },
                new() { CommandTxt = "publish_image con5", Name = "xx.con5.api" },
                new() { CommandTxt = "publish_image con6", Name = "xx.con6.api" },
                new() { CommandTxt = "publish_image con7", Name = "xx.con7.api" },

                new() { CommandTxt = "upload con1", Name = "xx.con1.api" },
                new() { CommandTxt = "upload con2", Name = "xx.con2.api" },
                new() { CommandTxt = "upload con3", Name = "xx.con3.api" },
                new() { CommandTxt = "upload con4", Name = "xx.con4.api" },
                new() { CommandTxt = "upload con5", Name = "xx.con5.api" },
                new() { CommandTxt = "upload con6", Name = "xx.con6.api" },
                new() { CommandTxt = "upload con7", Name = "xx.con7.api" },
            ];
            var resolvedResult = TmplHelper.ResolveExpressionValue(cmds[2].CommandTxt, dataConetxt);
            if (resolvedResult is IEnumerable<object> resolvedCommands)
            {
                foreach (var item in resolvedCommands)
                {
                    outputHelper.WriteLine(item.ToString());
                }
            }
        }
    }
}
