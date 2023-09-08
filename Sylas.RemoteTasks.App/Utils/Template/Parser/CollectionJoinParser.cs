using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.App.Utils.Template.Parser
{
    public class CollectionJoinParser : ITmplParser
    {
        public ParseResult Parse(string tmpl, Dictionary<string, object> dataContext)
        {
            var joinExpression = Regex.Match(tmpl, @"(?<key>\$\w+)\s+join\s+(?<splitor>.+)$");
            if (joinExpression.Success)
            {
                var key = joinExpression.Groups["key"].Value;
                var splitor = joinExpression.Groups["splitor"].Value;

                var joinValue = string.Join(splitor, (JArray.FromObject(dataContext[key]) ?? throw new Exception("JoinParser不支持非集合对象")).Select(x => x.ToString()).ToArray());
                return new ParseResult(true, new string[] { key }, joinValue);
            }
            return new ParseResult(false);
        }
    }
}
