using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.Utils.Template.Parser
{
    /// <summary>
    /// 
    /// </summary>
    public class DataPropertyParser : ITmplParser
    {
        /// <summary>
        /// 解析获取对象的属性值
        /// </summary>
        /// <param name="tmpl"></param>
        /// <param name="dataContext"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public ParseResult Parse(string tmpl, Dictionary<string, object> dataContext)
        {
            tmpl = tmpl.Trim().TrimStart('$', '{').TrimEnd('}');
            if (!dataContext.Any())
            {
                return new(true) { Value = tmpl };
            }
            var specifiedRecordFieldValueExpression = Regex.Match(tmpl, @"(?<key>[\$\{\}\w]+)(\[(?<index>\d+)\]){0,1}(?<props>(\.\w+)*)$");

            if (!specifiedRecordFieldValueExpression.Success)
            {
                throw new Exception($"{nameof(DataPropertyParser)} 表达式\"{tmpl}\"解析对象属性值失败");
            }

            var key = specifiedRecordFieldValueExpression.Groups["key"].Value;
            var currentKey = dataContext.Keys.FirstOrDefault(x => x.TrimStart('$').Equals(key, StringComparison.OrdinalIgnoreCase)) ?? throw new Exception($"{nameof(DataPropertyParser)} 数据上下中未发现数据{key}");
            var currentData = dataContext[currentKey];

            // 有index说明currentData是数组
            var index = specifiedRecordFieldValueExpression.Groups["index"].Value;
            object propertyValue;
            if (string.IsNullOrWhiteSpace(index))
            {
                propertyValue = currentData;
            }
            else
            {
                short indexVal = Convert.ToInt16(index);
                if (currentData is List<object> listData)
                {
                    propertyValue = listData[indexVal];
                }
                else if (currentData is JsonElement currentJE && currentJE.ValueKind == JsonValueKind.Array)
                {
                    propertyValue = currentJE.EnumerateArray().ToList()[indexVal];
                }
                else if(currentData is not string && currentData is IEnumerable ienumerableCollection)
                {
                    IEnumerable<object> ienumerableData = ienumerableCollection.Cast<object>();
                    propertyValue = ienumerableData.ToList()[indexVal];
                }
                else
                {
                    propertyValue = JArray.FromObject(currentData)[indexVal];
                } 
            }

            var propsValue = specifiedRecordFieldValueExpression.Groups["props"].Value;
            
            var props = propsValue.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (propertyValue is JsonElement jsonElement)
            {
                var result = JsonHelper.GetDataElement(jsonElement, props);
                return new ParseResult(true, [key], result.ValueKind == JsonValueKind.String ? result.ToString() : result);
            }
            foreach (var p in props)
            {
                if (propertyValue is null || string.IsNullOrWhiteSpace(propertyValue.ToString()))
                {
                    throw new Exception($"{nameof(DataPropertyParser)}无法根据模板[{tmpl}]找到属性{p}");
                }

                if (propertyValue is IDictionary<string, object> recordDictionary)
                {
                    var kv = recordDictionary.Where(x => x.Key.Equals(p, StringComparison.OrdinalIgnoreCase));
                    if (!kv.Any())
                    {
                        throw new Exception($"模板[{tmpl}]中无法找到属性: {p}");
                    }
                    propertyValue = kv.First().Value;
                }
                else
                {
                    if (propertyValue is not JObject pObj)
                    {
                        if (propertyValue is not string && propertyValue is IEnumerable recordCollection)
                        {
                            var records = recordCollection.Cast<object>();
                            pObj = JObject.FromObject(records.FirstOrDefault());
                        }
                        else if (propertyValue is string propertyStrVal && propertyStrVal.StartsWith("{"))
                        {
                            pObj = JsonConvert.DeserializeObject<JObject>(propertyStrVal)!;
                        }
                        else
                        {
                            pObj = JObject.FromObject(propertyValue);
                        }
                    }
                    var resolvedValue = pObj.Properties().FirstOrDefault(x => string.Equals(x.Name, p, StringComparison.OrdinalIgnoreCase))?.Value ?? throw new Exception($"无法找到属性{p}");
                    if (resolvedValue.Type == JTokenType.String)
                    {
                        propertyValue = resolvedValue.ToString();
                    }
                    else if (resolvedValue.Type == JTokenType.Integer)
                    {
                        if (int.TryParse(resolvedValue.ToString(), out int intProptyValue))
                        {
                            propertyValue = intProptyValue;
                        }
                        else
                        {
                            propertyValue = Convert.ToInt64(resolvedValue);
                        }
                    }
                    else
                    {
                        propertyValue = resolvedValue;
                    }
                }
            }
            return new ParseResult(true, [key], propertyValue);
        }
    }
}
