using RazorEngine.Templating;
using System.Dynamic;
using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.Tmpl
{
    public class RazorEngineTest(ITestOutputHelper outputHelper) : IClassFixture<TestFixture>
    {
        /// <summary>
        /// 模板中使用变量 - Model是匿名对象
        /// </summary>
        [Fact]
        public void VariableFromDynamic_Test()
        {
            string currentTmpl = $"TmplTest";
            string template = "Hello, @Model.Name. Welcome to RazorEngine!";
            if (!RazorEngine.Engine.Razor.IsTemplateCached(currentTmpl, null))
            {
                RazorEngine.Engine.Razor.AddTemplate(currentTmpl, template);
                RazorEngine.Engine.Razor.Compile(currentTmpl);
            }

            string result = RazorEngine.Engine.Razor.Run(currentTmpl, modelType: null, model: new { Name = "zhangsan" });
            outputHelper.WriteLine(result);
        }
        /// <summary>
        /// 模板中使用变量 - Model是字典
        /// </summary>
        [Fact]
        public void VariableFromDictionary_Test()
        {
            string currentTmpl = $"TmplTest";
            string template = "Hello, @Model.Name. Welcome to RazorEngine!";
            if (!RazorEngine.Engine.Razor.IsTemplateCached(currentTmpl, null))
            {
                RazorEngine.Engine.Razor.AddTemplate(currentTmpl, template);
                RazorEngine.Engine.Razor.Compile(currentTmpl);
            }
            // 不能直接使用字典当作Model, 可以使用ExpandoObject(强转字典然后动态添加属性)
            object dataModel = new ExpandoObject();
            ((IDictionary<string, object>)dataModel)["Name"] = "zhangsan";
            string result = RazorEngine.Engine.Razor.Run(currentTmpl, modelType: null, model: dataModel);
            outputHelper.WriteLine(result);
        }
        /// <summary>
        /// 模板中使用方法
        /// </summary>
        [Fact]
        public void Method_Test()
        {
            string currentTmpl = $"TmplTest";
            string template = "@Sylas.RemoteTasks.Test.Tmpl.RazorEngineTest.GetStr(), 张三. Welcome to RazorEngine!";
            if (!RazorEngine.Engine.Razor.IsTemplateCached(currentTmpl, null))
            {
                RazorEngine.Engine.Razor.AddTemplate(currentTmpl, template);
                RazorEngine.Engine.Razor.Compile(currentTmpl);
            }
            string result = RazorEngine.Engine.Razor.Run(currentTmpl, modelType: null, model: null);
            outputHelper.WriteLine(result);
        }
        [Fact]
        public void MultiLineCode_Test()
        {
            string currentTmpl = $"TmplTest";
            string template = """
                @{
                    var name = "张三";
                    var age = 18;
                }
                @Sylas.RemoteTasks.Test.Tmpl.RazorEngineTest.GetStr(), @name. Welcome to RazorEngine! Your age is @age
                """;
            if (!RazorEngine.Engine.Razor.IsTemplateCached(currentTmpl, null))
            {
                RazorEngine.Engine.Razor.AddTemplate(currentTmpl, template);
                RazorEngine.Engine.Razor.Compile(currentTmpl);
            }
            string result = RazorEngine.Engine.Razor.Run(currentTmpl, modelType: null, model: null);
            outputHelper.WriteLine(result);
        }
        public static string GetStr()
        {
            return "Hello";
        }
    }
}
