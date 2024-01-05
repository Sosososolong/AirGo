using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.Utils.Template.Parser
{
    public class CollectionPlusParser : ITmplParser
    {
        public ParseResult Parse(string tmpl, Dictionary<string, object> dataContext)
        {
            // $var1+$var2
            var specifiedRecordFieldValueExpression = Regex.Match(tmpl, @"(?<left>\$\w+)\s*\+\s*(?<right>\$\w+)");
            if (specifiedRecordFieldValueExpression.Success)
            {
                var left = specifiedRecordFieldValueExpression.Groups["left"].Value;
                var right = specifiedRecordFieldValueExpression.Groups["right"].Value;
                var currentDataLeft = dataContext[left] ?? throw new Exception($"PlusOperatorParser异常, DataContext中未找到左边表达式{left}");
                var currentDataRight = dataContext[right] ?? throw new Exception($"PlusOperatorParser异常, DataContext中未找到右边表达式{right}");

                if (currentDataLeft is JArray)
                {
                    Console.WriteLine("is jarray");
                }
                var currentDataLeftJarray = JArray.FromObject(currentDataLeft) ?? throw new Exception("PlusOperatorParser异常, 左边表达式不是集合类型");
                var currentDataRightJarray = JArray.FromObject(currentDataRight) ?? throw new Exception("PlusOperatorParser异常, 右边表达式不是集合类型");
                foreach ( var item in currentDataLeftJarray)
                {
                    currentDataRightJarray.Add(item);
                }
                return new ParseResult(true, new string[] { left, right }, currentDataRightJarray);
            }
            return new ParseResult(false);
        }
    }
}
