using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.Utils.Template.Parser
{
    /// <summary>
    /// 正则截取部分字符串
    /// </summary>
    public class RegexSubStringParser : ITmplParser
    {
        /// <summary>
        /// 正则使用匹配分组的方式获取子字符串
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
                string dataContextKey = dataContext.Keys.FirstOrDefault(x => x.TrimStart('$').Equals(key, StringComparison.OrdinalIgnoreCase)) ?? throw new Exception($"{nameof(RegexSubStringParser)} 数据上下中未发现数据{key}");
                var keyValue = dataContext[dataContextKey]?.ToString() ?? throw new Exception($"DataContext获取{key}对应的值失败");
                var regexPattern = expression.Groups["regex"].Value;
                var group = expression.Groups["groupname"].Value;

                var val = Regex.Match(keyValue, regexPattern).Groups[group].Value;
                return new ParseResult(true, [key], val);
            }
            return new ParseResult(false);
        }
    }
}
