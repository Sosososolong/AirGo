using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.Utils.Template.Parser
{
    /// <summary>
    /// 
    /// </summary>
    public class CollectionSelectItemRegexSubStringParser : ITmplParser
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
            var expression = Regex.Match(tmpl, @"(?<key>\${0,1}\w+) select (?<prop>[^\s]+)(?<recursively>\s+-r){0,1}\s+reg\s+`(?<regex>.+)` (?<groupname>\w+)");
            if (expression.Success)
            {
                var key = expression.Groups["key"].Value;
                var prop = expression.Groups["prop"].Value;
                var recursively = expression.Groups["recursively"].Value;

                var parseResult = ITmplParser.ResolveCollectionSelectTmpl(key, prop, recursively, dataContext);

                var regexPattern = expression.Groups["regex"].Value;
                var group = expression.Groups["groupname"].Value;

                List<string> result = [];
                var source = JArray.FromObject(parseResult.Value ?? throw new Exception($"ITmplParser -> CollectionSelectThenRegexSubStringParser select 结果为空"));
                foreach (var item in source)
                {
                    var itemVal = item.ToString();
                    var catchedSubStr = Regex.Match(itemVal, regexPattern).Groups[group].Value;
                    if (!string.IsNullOrWhiteSpace(catchedSubStr))
                    {
                        result.Add(catchedSubStr);
                    }
                }
                parseResult.Value = result;
                return parseResult;
            }
            return new ParseResult(false);
        }
    }
}
