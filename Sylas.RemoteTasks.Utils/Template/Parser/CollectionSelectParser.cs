using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.Utils.Template.Parser
{
    public class CollectionSelectParser : ITmplParser
    {
        public ParseResult Parse(string tmpl, Dictionary<string, object> dataContext)
        {
            var selectExpression = Regex.Match(tmpl, @"(?<key>\$\w+) select (?<prop>[^\s]+)(?<recursively>\s+-r){0,1}$");
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
