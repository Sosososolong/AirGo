using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.App.Utils
{
    public static class JTokenHelper
    {
        public static JToken GetMyToken(this JToken source, string expression)
        {
            var expressionMatch = Regex.Match(expression, @"(\[(?<index>\d+)\]){0,1}(?<props>\.\w+)*$");
            if (!expressionMatch.Success)
            {
                throw new Exception($"无法从表达式{expression}解析出正确的属性: {Environment.NewLine}{source}");
            }
            var key = expressionMatch.Groups["key"].Value;

            var index = expressionMatch.Groups["index"].Value;
            var record = string.IsNullOrWhiteSpace(index) ? source : JArray.FromObject(source).ToList()[Convert.ToInt16(index)];

            var props = expressionMatch.Groups["props"].Value.Split('.', StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in props)
            {
                if (record is not JObject pObj)
                {
                    throw new Exception($"无法找到属性{p}");
                }
                record = pObj?.Properties()?.FirstOrDefault(x => string.Equals(x.Name, p, StringComparison.OrdinalIgnoreCase))?.Value ?? throw new Exception($"无法找到属性{p}");
            }
            return record;
        }
    }
}
