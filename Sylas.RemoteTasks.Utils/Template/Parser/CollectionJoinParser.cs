using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.Utils.Template.Parser
{
    /// <summary>
    /// 解析模板表达式
    /// </summary>
    public class CollectionJoinParser : ITmplParser
    {
        /// <summary>
        /// 解析模板表达式
        /// </summary>
        /// <param name="tmpl"></param>
        /// <param name="dataContext"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public ParseResult Parse(string tmpl, Dictionary<string, object> dataContext)
        {
            tmpl = tmpl.Trim().TrimStart('$', '{').TrimEnd('}');
            var joinExpression = Regex.Match(tmpl, @"(?<key>\${0,1}\w+)\s+join\s+(?<splitor>.+)$");
            if (joinExpression.Success)
            {
                var key = joinExpression.Groups["key"].Value;
                var splitor = joinExpression.Groups["splitor"].Value;

                var joinValue = string.Join(splitor, (JArray.FromObject(dataContext[key]) ?? throw new Exception("JoinParser不支持非集合对象")).Select(x => x.ToString()).ToArray());
                return new ParseResult(true, [key], joinValue);
            }
            return new ParseResult(false);
        }
    }
}
