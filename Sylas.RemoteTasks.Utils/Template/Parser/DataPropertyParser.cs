using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.Utils.Template.Parser
{
    public class DataPropertyParser : ITmplParser
    {
        public ParseResult Parse(string tmpl, Dictionary<string, object> dataContext)
        {
            var specifiedRecordFieldValueExpression = Regex.Match(tmpl, @"(?<key>\$\w+)(\[(?<index>\d+)\]){0,1}(?<props>(\.\w+)*)$");
            if (specifiedRecordFieldValueExpression.Success)
            {
                var key = specifiedRecordFieldValueExpression.Groups["key"].Value;
                var currentData = dataContext[key];

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
                    if (record is not JObject pObj)
                    {
                        throw new Exception($"{nameof(DataPropertyParser)}无法根据模板[{tmpl}]找到属性{p}");
                    }
                    record = pObj?.Properties()?.FirstOrDefault(x => string.Equals(x.Name, p, StringComparison.OrdinalIgnoreCase))?.Value ?? throw new Exception($"无法找到属性{p}");
                }
                return new ParseResult(true, [key], record);
            }
            return new ParseResult(false);
        }
    }
}
