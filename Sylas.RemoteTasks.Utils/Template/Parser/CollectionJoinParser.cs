using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.Utils.Template.Parser
{
    /// <summary>
    /// 解析数组, 连接成字符串
    /// </summary>
    public class CollectionJoinParser : ITmplParser
    {
        /// <summary>
        /// 解析数组, 连接成字符串
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

                var dataContextKey = dataContext.Keys.FirstOrDefault(x => x.TrimStart('$').Equals(key, StringComparison.OrdinalIgnoreCase)) ?? throw new Exception($"{nameof(CollectionJoinParser)} 数据上下中未发现数据{key}");
                var target = dataContext[dataContextKey];
                string joinValue;
                if (target.GetType().Name != "String")
                {
                    if (target is not IEnumerable<object> enumerableData)
                    {
                        throw new Exception($"{nameof(CollectionJoinParser)} 数据不是集合类型,无法进行Join操作");
                    }
                    else if (!enumerableData.Any())
                    {
                        joinValue = string.Empty;
                    }
                    else
                    {
                        var first = enumerableData.First();
                        // 可能是JToken, 可能是JsonElement类型, 也可能是基本类型, 都直接ToString()
                        joinValue = string.Join(splitor, enumerableData.Select(x => x.ToString()).ToArray());
                    }
                    //joinValue = string.Join(splitor, JArray.FromObject(target).Select(x => x.ToString()).ToArray());
                }
                else
                {
                    joinValue = target.ToString();
                }
                return new ParseResult(true, [key], joinValue);
            }
            return new ParseResult(false);
        }
    }
}
