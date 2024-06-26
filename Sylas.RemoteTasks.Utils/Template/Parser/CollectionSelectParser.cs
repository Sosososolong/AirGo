using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.Utils.Template.Parser
{
    /// <summary>
    /// 获取集合中的指定属性值
    /// </summary>
    public class CollectionSelectParser : ITmplParser
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tmpl"></param>
        /// <param name="dataContext"></param>
        /// <returns></returns>
        public ParseResult Parse(string tmpl, Dictionary<string, object> dataContext)
        {
            tmpl = tmpl.Trim().TrimStart('$', '{').TrimEnd('}');
            var selectExpression = Regex.Match(tmpl, @"(?<key>\${0,1}\w+) select (?<prop>[^\s]+)(?<recursively>\s+-\s*r){0,1}$");
            if (selectExpression.Success)
            {
                var key = selectExpression.Groups["key"].Value;
                var prop = selectExpression.Groups["prop"].Value;
                var recursively = selectExpression.Groups["recursively"].Value;

                return ITmplParser.ResolveCollectionSelectTmpl(key, prop, recursively, dataContext);
            }
            return new ParseResult(false);
        }
    }
}
