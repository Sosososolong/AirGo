using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.App.Utils.Template.Parser
{
    public class RegexSubStringParser : ITmplParser
    {
        public ParseResult Parse(string tmpl, Dictionary<string, object> dataContext)
        {
            var expression = Regex.Match(tmpl, @"(?<key>\$\w+) reg `(?<regex>.+)` (?<groupname>\w+)$");
            if (expression.Success)
            {
                var key = expression.Groups["key"].Value;
                var keyValue = dataContext[key]?.ToString() ?? throw new Exception($"DataContext获取{key}对应的值失败");
                var regexPattern = expression.Groups["regex"].Value;
                var group = expression.Groups["groupname"].Value;

                var val = Regex.Match(keyValue, regexPattern).Groups[group].Value;
                return new ParseResult(true, new string[] { key }, val);
            }
            return new ParseResult(false);
        }
    }
}
