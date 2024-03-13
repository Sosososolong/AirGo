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
            object record;
            if (string.IsNullOrWhiteSpace(index))
            {
                record = currentData;
            }
            else
            {
                var currentDataArray = JArray.FromObject(currentData).ToList();
                if (!currentDataArray.Any())
                {
                    return new ParseResult(false);
                }
                record = currentDataArray[Convert.ToInt16(index)];
            }

            var propsValue = specifiedRecordFieldValueExpression.Groups["props"].Value;
            var props = propsValue.Split('.', StringSplitOptions.RemoveEmptyEntries);

            foreach (var p in props)
            {
                if (record is null || string.IsNullOrWhiteSpace(record.ToString()))
                {
                    throw new Exception($"{nameof(DataPropertyParser)}无法根据模板[{tmpl}]找到属性{p}");
                }

                if (record is Dictionary<string, object> recordDictionary)
                {
                    record = recordDictionary[p];
                }
                else
                {
                    if (record is not JObject pObj)
                    {
                        pObj = JObject.FromObject(record);
                    }
                    record = pObj.Properties().FirstOrDefault(x => string.Equals(x.Name, p, StringComparison.OrdinalIgnoreCase))?.Value ?? throw new Exception($"无法找到属性{p}");
                }
            }
            return new ParseResult(true, [key], record);
        }
    }
}
