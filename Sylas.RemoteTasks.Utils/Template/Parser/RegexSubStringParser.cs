using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.Utils.Template.Parser
{
    /// <summary>
    /// 
    /// </summary>
    public class RegexSubStringParser : ITmplParser
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tmpl"></param>
        /// <param name="dataContext"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public ParseResult Parse(string tmpl, Dictionary<string, object> dataContext)
        {
            tmpl = tmpl.Trim().TrimStart('$', '{').TrimEnd('}');
            var expression = Regex.Match(tmpl, @"(?<key>\${0,1}\w+) reg `(?<regex>.+)` (?<groupname>\w+)$");
            if (expression.Success)
            {
                var key = expression.Groups["key"].Value;
                var keyValue = dataContext[key]?.ToString() ?? throw new Exception($"DataContext获取{key}对应的值失败");
                var regexPattern = expression.Groups["regex"].Value;
                var group = expression.Groups["groupname"].Value;

                var val = Regex.Match(keyValue, regexPattern).Groups[group].Value;
                return new ParseResult(true, [key], val);
            }
            return new ParseResult(false);
        }
    }
}
