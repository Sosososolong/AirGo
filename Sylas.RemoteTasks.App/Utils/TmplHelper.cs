using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.RegexExp;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.App.Utils
{
    public static partial class TmplHelper
    {
        /// <summary>
        /// 替换复杂Json对象中的模板配置, {{$primary.xxx}}
        /// </summary>
        /// <param name="target"></param>
        /// <param name="dataSource"></param>
        public static void ResolveJTokenTmplValue(JToken? target, JObject dataSource)
        {
            if (dataSource is null)
            {
                return;
            }
            if (target is null || string.IsNullOrWhiteSpace(target.ToString()))
            {
                return;
            }
            var values = new JArray();
            if (target is JObject jObjValue)
            {
                values.Add(jObjValue);
            }
            else if (target is JArray jArrayValue)
            {
                values = jArrayValue;
            }
            if (values is not null && values.Any())
            {
                foreach (var parameterItem in values)
                {
                    if (parameterItem is JObject parameterItemJObj)
                    {
                        var keys = parameterItemJObj.Properties();
                        foreach (var key in keys)
                        {
                            if (key.Value is JObject || key.Value is JArray)
                            {
                                ResolveJTokenTmplValue(key.Value, dataSource);
                            }
                            else
                            {
                                var currentParameterVal = key.Value?.ToString();
                                if (!string.IsNullOrWhiteSpace(currentParameterVal))
                                {
                                    var rfedPrimaryGroups = RegexConst.RefedPrimaryField().Match(currentParameterVal).Groups;
                                    if (rfedPrimaryGroups.Count > 1)
                                    {
                                        string tmpl = rfedPrimaryGroups[0].Value;
                                        string refedPrimaryField = rfedPrimaryGroups[1].Value;

                                        var dataProperties = dataSource.Properties();
                                        var refedProp = dataProperties.FirstOrDefault(p => string.Equals(p.Name, refedPrimaryField, StringComparison.OrdinalIgnoreCase));
                                        if (refedProp is not null)
                                        {
                                            var primaryFieldValue = refedProp.Value;
                                            key.Value = primaryFieldValue;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        continue;
                    }
                }
            }
        }
        /// <summary>
        /// 根据模板将dataSource(JObject或JArray)中的符合条件的一条或多条数据的某个属性赋值给target的某个属性, 每条数据赋值一次都产生一个target副本
        /// 如 $primary.BodyDictionary.FilterItems.Value=$records[\"DATATYPE\"=21].REFMODELID 表示修改 target.BodyDictionary.FilterItems.Value 的值为 DataType为21的dataSource的RefModelId字段值, 可能多个
        /// </summary>
        /// <param name="target"></param>
        /// <param name="dataSource"></param>
        public static List<JObject> ResolveJTokenByDataSourceTmpl(JToken? target, JToken dataSource, string assignmentTmpl)
        {
            if (dataSource is null)
            {
                return new List<JObject>();
            }
            if (target is null || string.IsNullOrWhiteSpace(target.ToString()))
            {
                return new List<JObject>();
            }
            #region 解析模板
            // <filterProp>为<filterValue>的数据, 取<dataProp>的值 赋值给target的<targetPorp>属性
            var match = RegexConst.AssignmentRulesTmpl().Match(assignmentTmpl);
            var filterProp = match.Groups["filterProp"].Value; // DATATYPE
            var filterValue = match.Groups["filterValue"].Value; // 21
            var dataProp = match.Groups["dataProp"].Value; // REFMODELID
            var targetProp = match.Groups["targetPorp"].Value; // .BodyDictionary.FilterItems.Value
            #endregion
            
            var result = new List<JObject>();

            var targetValues = new JArray();
            if (target is JObject jObjValue)
            {
                targetValues.Add(jObjValue);
            }
            else if (target is JArray jArrayValue)
            {
                targetValues = jArrayValue;
            }
            if (targetValues is not null && targetValues.Any())
            {
                foreach (var targetValue in targetValues)
                {
                    if (targetValue is JObject targetJObj)
                    {
                        // 处理每个target
                        //var targetKeys = targetJObj.Properties();
                        var dataSourceValues = new JArray();
                        if (dataSource is JObject jObjDataSource)
                        {
                            dataSourceValues.Add(jObjDataSource);
                        }
                        else if (dataSource is JArray jArrayDataSource)
                        {
                            dataSourceValues = jArrayDataSource;
                        }
                        if (dataSourceValues is not null && dataSourceValues.Any())
                        {
                            foreach (var dataSourceItem in dataSourceValues)
                            {
                                if (dataSourceItem is JObject dataSourceJOjb)
                                {
                                    var dataSourceKeys = dataSourceJOjb.Properties();
                                    var dataSourceJOjbFilterPropValue = dataSourceKeys.FirstOrDefault(x => x.Name.Equals(filterProp, StringComparison.OrdinalIgnoreCase))?.Value;
                                    // 符合条件的数据
                                    if (dataSourceJOjbFilterPropValue?.ToString() == filterValue)
                                    {
                                        // 每个target, 即targetJObj在这里更新属性 产生一个新的副本
                                        var expectedValue = dataSourceKeys.FirstOrDefault(x => x.Name.Equals(dataProp, StringComparison.OrdinalIgnoreCase))?.Value;
                                        // BOOKMARK: TmplHelper 复制一份targetJObj -> targetJObjCopied, 再更新 targetJObjCopied, 添加到result中
                                        var targetJObjCopied = targetJObj.DeepClone() as JObject;
                                        var targetProps = targetProp.Split('.', StringSplitOptions.TrimEntries);
                                        JToken? targetCurrentVal = null;
                                        for (int i = 0; i < targetProps.Length; i++)
                                        {
                                            var copiedPropVal = targetJObjCopied[targetProps[i]];
                                            if (i == targetProps.Length - 1)
                                            {
                                                if (targetCurrentVal is null)
                                                {
                                                    copiedPropVal = expectedValue;
                                                    break;
                                                }
                                                else
                                                {
                                                    targetCurrentVal = expectedValue;
                                                }
                                            }
                                            else
                                            {
                                                targetCurrentVal = copiedPropVal;
                                            }
                                        }
                                        result.Add(targetJObjCopied);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        continue;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 替换字符串中的模板
        /// </summary>
        /// <param name="target"></param>
        /// <param name="dataSource"></param>
        public static string ResolveStringTmplValue(string target, JObject dataSource)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return target;
            }
            if (dataSource is null)
            {
                return target;
            }

            var stringTmplMatches = RegexConst.StringTmpl().Matches(target);
            foreach (var stringTmplMatch in stringTmplMatches.Cast<Match>())
            {
                var stringTmplGroups = stringTmplMatch.Groups;
                if (stringTmplGroups.Count > 1)
                {
                    string tmpl = stringTmplGroups[0].Value;
                    string tmplField = stringTmplGroups[1].Value;

                    var dataProperties = dataSource.Properties();
                    var refedProp = dataProperties.FirstOrDefault(p => string.Equals(p.Name, tmplField, StringComparison.OrdinalIgnoreCase));
                    if (refedProp is not null)
                    {
                        var tmplValue = refedProp.Value;
                        target = target.Replace(tmpl, tmplValue.ToString());
                    }
                }
            }

            return target;
        }

        #region 处理带数据上下文的模板字符串
        /// <summary>
        /// 构建数据上下文: 配置在使用时会产生或者获取一些数据; 数据产生并缓存一些变量供模板自己使用, 以达到动态配置的目的
        /// </summary>
        /// <param name="assignmentStatementTmpl">赋值语句模板, 即变量名=值</param>
        /// <param name="dataContext">包含原始的数据$data的数据上下文对象</param>
        /// <returns></returns>
        public static void BuildDataContext(this Dictionary<string, object> dataContext, List<string> assignmentStatementTmpls, IEnumerable<JToken> data, ILogger? logger = null)
        {
            dataContext["$data"] = data;
            // 解析DataContext
            foreach (var assignmentStatementTmpl in assignmentStatementTmpls)
            {
                // $datavar=data, $idpath=data[0].idpath, ...
                var tokens = assignmentStatementTmpl.Split("=", StringSplitOptions.RemoveEmptyEntries);
                var variableName = tokens[0];
                var variableValue = tokens[1];
                object? variableValueResolved = null;

                // 1. 缓存结果$data
                if (variableValue == "$data")
                {
                    variableValueResolved = dataContext["$data"];
                    logger?.LogDebug($"call {nameof(BuildDataContext)}, 构建数据上下文, key={variableName}, 缓存结果data");
                }
                else
                {
                    // 2. 缓存指定数据的某个属性值
                    var specifiedRecordFieldValueExpression = Regex.Match(variableValue, @"(?<key>\$\w+)(\[(?<index>\d+)\]){0,1}(?<props>\.\w+)*$");
                    if (specifiedRecordFieldValueExpression.Success)
                    {
                        var key = specifiedRecordFieldValueExpression.Groups["key"].Value;
                        var currentData = dataContext[key];

                        var index = specifiedRecordFieldValueExpression.Groups["index"].Value;
                        var record = string.IsNullOrWhiteSpace(index) ? currentData : JArray.FromObject(currentData).ToList()[Convert.ToInt16(index)];
                        
                        var propsValue = specifiedRecordFieldValueExpression.Groups["props"].Value;
                        var props = propsValue.Split('.', StringSplitOptions.RemoveEmptyEntries);

                        foreach (var p in props)
                        {
                            if (record is not JObject pObj)
                            {
                                throw new Exception($"无法找到属性{p}");
                            }
                            record = pObj?.Properties()?.FirstOrDefault(x => string.Equals(x.Name, p, StringComparison.OrdinalIgnoreCase))?.Value ?? throw new Exception($"无法找到属性{p}");
                        }
                        variableValueResolved = record;
                        logger?.LogDebug($"call {nameof(BuildDataContext)}, 构建数据上下文, key={variableName}, 缓存对象属性: `$key`[{index}]{propsValue}");
                    }
                    else
                    {
                        // 3. 缓存经过动态构建表达式树过滤的结果集
                        specifiedRecordFieldValueExpression = Regex.Match(variableValue, @"(?<key>\$\w+)\[(?<expression>\w+\s\w+\s\w+)\](?<props>.\w+)*");
                        if (specifiedRecordFieldValueExpression.Success)
                        {
                            logger?.LogError($"call {nameof(BuildDataContext)}, 构建数据上下文, key={variableName}, 缓存经过动态构建表达式树过滤的结果集 暂未实现, 请求结束");
                            throw new NotImplementedException();
                        }
                        else
                        {
                            // 4. 缓存正则获取字段值的一部分
                            specifiedRecordFieldValueExpression = Regex.Match(variableValue, @"(?<key>\$\w+) reg `(?<regex>.+)` (?<groupname>\w+)$");
                            if (specifiedRecordFieldValueExpression.Success)
                            {
                                var key = specifiedRecordFieldValueExpression.Groups["key"].Value;
                                var keyValue = dataContext[key]?.ToString() ?? throw new Exception($"DataContext获取{key}对应的值失败");
                                var regexPattern = specifiedRecordFieldValueExpression.Groups["regex"].Value;
                                var group = specifiedRecordFieldValueExpression.Groups["groupname"].Value;

                                variableValueResolved = Regex.Match(keyValue, regexPattern).Groups[group].Value;
                                logger?.LogDebug($"call {nameof(BuildDataContext)}, 构建数据上下文, key={variableName}, 缓存正则获取字段值的一部分: {variableValueResolved}");
                            }
                            else
                            {
                                // 5. 缓存类型转换后的结果
                                specifiedRecordFieldValueExpression = Regex.Match(variableValue, @"(?<key>\$\w+) as (?<type>[^\s]+)$");
                                if (specifiedRecordFieldValueExpression.Success)
                                {
                                    var key = specifiedRecordFieldValueExpression.Groups["key"].Value;
                                    var keyVal = dataContext[key];
                                    var type = specifiedRecordFieldValueExpression.Groups["type"].Value;
                                    if (string.Equals(type, "List<JObject>", StringComparison.OrdinalIgnoreCase) && JToken.FromObject(keyVal).Type == JTokenType.String)
                                    {
                                        variableValueResolved = JsonConvert.DeserializeObject<List<JObject>>(keyVal?.ToString() ?? throw new Exception($"上下文{key}的值无法转换为有效字符串"));
                                        logger?.LogDebug($"call {nameof(BuildDataContext)}, 构建数据上下文, key={variableName}, 缓存类型转换后的结果");
                                    }
                                    else
                                    {
                                        throw new NotImplementedException("当前类型转换未实现");
                                    }
                                }
                                else
                                {
                                    // 6. 缓存Select集合中的某个属性集合的结果
                                    specifiedRecordFieldValueExpression = Regex.Match(variableValue, @"(?<key>\$\w+) select (?<prop>[^\s]+)(?<recursively>\s+-r){0,1}$");
                                    var key = specifiedRecordFieldValueExpression.Groups["key"].Value;
                                    var prop = specifiedRecordFieldValueExpression.Groups["prop"].Value;
                                    var recursively = specifiedRecordFieldValueExpression.Groups["recursively"].Value;
                                    var keyVal = dataContext[key];
                                    var keyValJArray = JArray.FromObject(keyVal) ?? throw new Exception($"上下文数据{key}对应的值不是JArray类型, 无法执行Select操作");
                                    var keyValJObjList = keyValJArray.ToObject<List<JObject>>() ?? throw new Exception($"数据上下文{key}的值{keyValJArray}中的子项不能转换为JObject类型, 不能执行Select操作");
                                    
                                    if (!string.IsNullOrWhiteSpace(recursively))
                                    {
                                        keyValJObjList = NodesHelper.GetAll(keyValJObjList, "items");
                                    }
                                    variableValueResolved = keyValJObjList.Select(x => x.Properties().FirstOrDefault(p => string.Equals(p.Name, prop))?.Value ?? throw new Exception($"上下文{key}: {keyValJArray} 无法找到属性{prop}"));
                                    logger?.LogDebug($"call {nameof(BuildDataContext)}, 构建数据上下文, key={variableName}, 缓存Select集合中的某个属性集合的结果: {variableValueResolved}");
                                }
                            }
                        }
                    }
                }
                if (variableValueResolved is not null)
                {
                    dataContext[variableName] = variableValueResolved;
                }
            }
        }
        public static object ResolveByDataContext(Dictionary<string, object> dataContext, string tmpl)
        {
            tmpl = tmpl.Trim();
            if (!dataContext.Any())
            {
                return tmpl;
            }
            if (tmpl.StartsWith("$"))
            {
                return dataContext.FirstOrDefault(x => string.Equals(x.Key, tmpl, StringComparison.OrdinalIgnoreCase)).Value ?? throw new Exception($"数据上下文中未找到索引: {tmpl}");
            }
            return tmpl;
        }
        #endregion
    }
}
