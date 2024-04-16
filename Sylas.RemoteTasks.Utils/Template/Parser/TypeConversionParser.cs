using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.Utils.Template.Parser
{
    /// <summary>
    /// 
    /// </summary>
    public class TypeConversionParser : ITmplParser
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tmpl"></param>
        /// <param name="dataContext"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        /// <exception cref="NotImplementedException"></exception>
        public ParseResult Parse(string tmpl, Dictionary<string, object> dataContext)
        {
            tmpl = tmpl.Trim().TrimStart('$', '{').TrimEnd('}');
            var expression = Regex.Match(tmpl, @"(?<key>\${0,1}\w+) as (?<type>[^\s]+)$");
            if (expression.Success)
            {
                var key = expression.Groups["key"].Value;
                var dataContextKey = dataContext.Keys.FirstOrDefault(x => x.TrimStart('$').Equals(key, StringComparison.OrdinalIgnoreCase)) ?? throw new Exception($"{nameof(TypeConversionParser)} 数据上下中未发现数据{key}"); ;
                var keyVal = dataContext[dataContextKey];
                var type = expression.Groups["type"].Value;
                if (keyVal is IEnumerable<object> keyVals)
                {
                    keyVal = keyVals.FirstOrDefault();
                }
                if (string.Equals(type, "List<JObject>", StringComparison.OrdinalIgnoreCase) && JToken.FromObject(keyVal).Type == JTokenType.String)
                {
                    var val = JsonConvert.DeserializeObject<List<JObject>>(keyVal?.ToString() ?? throw new Exception($"上下文{key}的值无法转换为有效字符串"));
                    return new ParseResult(true, [key], val);
                    
                }
                else
                {
                    throw new NotImplementedException("当前类型转换未实现");
                }
            }
            return new ParseResult(false);
        }
    }
}
