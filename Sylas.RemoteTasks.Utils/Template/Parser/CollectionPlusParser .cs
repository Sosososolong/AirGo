using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.Utils.Template.Parser
{
    /// <summary>
    /// 
    /// </summary>
    public class CollectionPlusParser : ITmplParser
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
            // $var1+$var2
            var specifiedRecordFieldValueExpression = Regex.Match(tmpl, @"(?<left>\${0,1}\w+)\s*\+\s*(?<right>\${0,1}\w+)");
            if (specifiedRecordFieldValueExpression.Success)
            {
                var left = specifiedRecordFieldValueExpression.Groups["left"].Value;
                var right = specifiedRecordFieldValueExpression.Groups["right"].Value;
                if (!dataContext.TryGetValue(left, out object currentDataLeft) && !dataContext.TryGetValue($"${left}", out currentDataLeft))
                {
                    throw new Exception($"PlusOperatorParser异常, DataContext中未找到左边表达式{left}");
                }
                if (!dataContext.TryGetValue(right, out object currentDataRight) && !dataContext.TryGetValue($"${right}", out currentDataRight))
                {
                    throw new Exception($"PlusOperatorParser异常, DataContext中未找到右边表达式{right}");
                }

                IEnumerable<object> currentDataLeftArr;
                if (currentDataLeft is not IEnumerable currentDataLeftCollection)
                {
                    currentDataLeftArr = [currentDataLeft];
                }
                else
                {
                    currentDataLeftArr = currentDataLeftCollection.Cast<object>();
                }

                IEnumerable<object> currentDataRightArr;
                if (currentDataRight is not IEnumerable<object> currentDataRightCollection)
                {
                    currentDataRightArr = [currentDataRight];
                }
                else
                {
                    currentDataRightArr = currentDataRightCollection.Cast<object>();
                }

                var resultDataList = currentDataLeftArr.ToList();
                resultDataList.AddRange(currentDataRightArr);

                return new ParseResult(true, [left, right], resultDataList);
            }
            return new ParseResult(false);
        }
    }
}
