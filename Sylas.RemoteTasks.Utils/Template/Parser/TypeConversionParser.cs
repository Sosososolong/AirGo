using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.Utils.Template.Parser
{
    /// <summary>
    /// 将字符串类型转换
    /// </summary>
    public class TypeConversionParser : ITmplParser
    {
        /// <summary>
        /// 将字符串转换为其他类型
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
                if (keyVal is string keyValString)
                {
                    if (string.Equals(type, "List", StringComparison.OrdinalIgnoreCase))
                    {
                        var val = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(keyValString);
                        return new ParseResult(true, [key], val);
                    }
                    else if (string.Equals(type, "Object", StringComparison.OrdinalIgnoreCase))
                    {
                        var val = JsonConvert.DeserializeObject<Dictionary<string, object>>(keyValString);
                        return new ParseResult(true, [key], val);
                    }
                    else
                    {
                        throw new NotImplementedException("当前类型转换未实现");
                    }
                }
                else if (keyVal is JsonElement jEVal)
                {
                    if (string.Equals(type, "List", StringComparison.OrdinalIgnoreCase))
                    {
                        if (jEVal.ValueKind == JsonValueKind.Array)
                        {
                            return new ParseResult(true, [key], jEVal.EnumerateArray().Select(x => x).ToList());
                        }
                        else
                        {
                            throw new Exception($"上下文{key}的值无法转换为List集合");
                        }
                    }
                    else if (string.Equals(type, "Object", StringComparison.OrdinalIgnoreCase))
                    {
                        return new ParseResult(true, [key], jEVal);
                    }
                    else
                    {
                        throw new NotImplementedException("当前类型转换未实现");
                    }
                }
                else if (keyVal is IEnumerable keyValCollection)
                {
                    keyVal = keyValCollection.Cast<object>().FirstOrDefault();
                }
                else
                {
                    throw new Exception($"上下文{key}的值无法转换为有效字符串");
                }
            }
            return new ParseResult(false);
        }
    }
}
