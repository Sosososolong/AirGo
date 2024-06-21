using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.RegexExp;
using Sylas.RemoteTasks.Utils.Template.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.Utils.Template
{
    /// <summary>
    /// 文本模板帮助类
    /// </summary>
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
                                    var rfedPrimaryGroups = RegexConst.RefedPrimaryField.Match(currentParameterVal).Groups;
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
        /// <param name="assignmentTmpl"></param>
        public static List<JObject> ResolveJTokenByDataSourceTmpl(JToken? target, JToken dataSource, string assignmentTmpl)
        {
            if (dataSource is null)
            {
                return [];
            }
            if (target is null || string.IsNullOrWhiteSpace(target.ToString()))
            {
                return [];
            }
            #region 解析模板
            // <filterProp>为<filterValue>的数据, 取<dataProp>的值 赋值给target的<targetProp>属性
            var match = RegexConst.AssignmentRulesTmpl.Match(assignmentTmpl);
            var filterProp = match.Groups["filterProp"].Value; // DATATYPE
            var filterValue = match.Groups["filterValue"].Value; // 21
            var dataProp = match.Groups["dataProp"].Value; // REFMODELID
            var targetProp = match.Groups["targetProp"].Value; // .BodyDictionary.FilterItems.Value
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
                                        var targetJObjCopied = targetJObj.DeepClone() as JObject ?? throw new Exception("DeepClone异常");
                                        var targetProps = targetProp.Split('.', StringSplitOptions.RemoveEmptyEntries);
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
        /// 构建数据上下文dataContext, Key都是"$"开头:
        ///   根据source填充dataContextBuilderTmpls中所有模板, 并合并到dataContext中
        ///       CollectionJoinParser
        ///       CollectionPlusParser                      CollectionPlusParser[$allDevFormIds=$bizFormIds+$listFormIds]
        ///       CollectionSelectParser                    CollectionSelectParser[$deployIds=$data select DeployId]
        ///       CollectionSelectItemRegexSubStringParser
        ///       DataPropertyParser                        DataPropertyParser[$idpath=$data[0].IDPATH]
        ///       RegexSubStringParser
        ///       TypeConversionParser
        ///   source以"$data"缓存到dataContext中
        ///   配置在使用时会产生或者获取一些数据; 数据产生并缓存一些变量供模板使用, 以达到动态配置的目的
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="source">数据源 - 就是将它($data)或者它的部分属性存储到dataContext中</param>
        /// <param name="dataContextBuilderTmpls">赋值语句模板, 即变量名=值; 变量名就是dataContext的键, 值通过模板获取source的属性值;支持各种自定义XxxParser, 用于解析获取结构复杂的source属性值</param>
        /// <param name="dataContext">包含原始的数据$data的数据上下文对象</param>
        /// <returns></returns>
        public static Dictionary<string, object?> BuildDataContextBySource(this Dictionary<string, object> dataContext, IEnumerable<JToken> source, List<string> dataContextBuilderTmpls, ILogger? logger = null)
        {
            // key为要构建的dataContext的key, value为给key赋的值, 有可能会赋多次值
            Dictionary<string, object?> dataContextBuildDetail = [];
            dataContext["$data"] = source;
            // 解析DataContext
            foreach (var dataContextBuilderTmpl in dataContextBuilderTmpls)
            {
                // dataContextBuilderTmpls示例:
                // $datavar=data, $idpath=data[0].idpath, ...
                //"$mainDataModels=DataPropertyParser[$data[0].formConfiguration.dataModels]"
                //"$mainDataModelIds=CollectionSelectParser[$mainDataModels select Id]"

                object? variableValueResolved = null;
                string[]? sourceKeys = null;
                var originResponseMatch = Regex.Match(dataContextBuilderTmpl, @"(?<dataContextNewKey>^\$\w+)=\$data$");
                var dataContextBuilderMatch = Regex.Match(dataContextBuilderTmpl, @"(?<dataContextNewKey>[\w\$@]*)=(?<expressionWithParser>.+)");
                string dataContextNewKey = dataContextBuilderMatch.Groups["dataContextNewKey"].Value;
                var expressionWithParser = dataContextBuilderMatch.Groups["expressionWithParser"].Value;

                #region 解析数据
                variableValueResolved = ResolveExpressionValue(expressionWithParser, dataContext);
                dataContextBuildDetail[dataContextNewKey] = variableValueResolved;
                logger?.LogDebug($"{nameof(BuildDataContextBySource)}, key={dataContextNewKey}: {(variableValueResolved?.ToString()?.Length > 50 ? variableValueResolved?.ToString()?[..50] : variableValueResolved?.ToString())}");
                #endregion

                #region 处理解析好的数据
                if (variableValueResolved is not null)
                {
                    // key {dataContextNewKey}已经存在, 则追加值; 数据源来源于当前请求得到最新数据也就是当前$data, 将解析出来的值追加;
                    if (!dataContextNewKey.Contains("w_") && dataContext.ContainsKey(dataContextNewKey) && dataContext[dataContextNewKey] is not null && sourceKeys is not null && sourceKeys.Any(x => string.Equals(x, "$data", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (dataContext[dataContextNewKey] is not JArray oldValueJArray)
                        {
                            oldValueJArray = new JArray(dataContext[dataContextNewKey]);
                        }
                        if (variableValueResolved is JArray newValueJArray)
                        {
                            foreach (var newValueItem in newValueJArray)
                            {
                                oldValueJArray.Add(newValueItem);
                            }
                        }
                        else
                        {
                            oldValueJArray.Add(variableValueResolved);
                        }
                    }
                    else
                    {
                        dataContext[dataContextNewKey] = variableValueResolved;
                    }
                }
                #endregion
            }
            return dataContextBuildDetail;
        }

        /// <summary>
        /// 解析模板上下文后面的变量也可能会引用前面的变量, 这里依次解析出所有的值
        /// </summary>
        /// <param name="dataContext"></param>
        /// <returns></returns>
        public static void ResolveSelfTmplValues(this Dictionary<string, object> dataContext)
        {
            foreach (var item in dataContext)
            {
                if (item.Value is string stringValue && stringValue.TryGetExpressions(out IEnumerable<string> expressions))
                {
                    foreach (var expression in expressions)
                    {
                        var expressionValue = ResolveExpressionValue(expression, dataContext).ToString();
                        stringValue = stringValue.Replace(expression, expressionValue);
                    }
                    dataContext[item.Key] = stringValue;
                }
            }
        }

        static readonly Dictionary<string, ITmplParser> _parserObjectMap = [];
        /// <summary>
        /// dataContext的Key可能是"$expression"开头, 也可能被双花括号括起来"{{expression}}"
        /// dataContext: { "$ids": "[1,2,3]" }; tmpl: "$ids" => [1, 2, 3]
        /// </summary>
        /// <param name="tmplWithParser">字符串模板 $app/${app}/{{app}}</param>
        /// <param name="dataContextObject">模板上下文</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static object ResolveExpressionValue(string tmplWithParser, object dataContextObject)
        {
            if (string.IsNullOrWhiteSpace(tmplWithParser))
            {
                return tmplWithParser;
            }
            if (dataContextObject is null)
            {
                return tmplWithParser;
            }
            if (dataContextObject is not Dictionary<string, object> dataContextDictionary)
            {
                dataContextDictionary = JObject.FromObject(dataContextObject).ToObject<Dictionary<string, object>>() ?? throw new Exception("无法将模板的数据上下文转换为字典");
            }
            const string doubleFlag = "DOUBLEFLAGlsjflajflajf238024***%666666^^^^-+Oo)ukj(";
            tmplWithParser = tmplWithParser.Replace("$$", doubleFlag);
            var stringTmplMatches = RegexConst.StringTmpl.Matches(tmplWithParser);
            List<object> results = [tmplWithParser];
            foreach (var stringTmplMatch in stringTmplMatches.Cast<Match>())
            {
                var stringTmplGroups = stringTmplMatch.Groups;
                if (stringTmplGroups.Count > 1)
                {
                    string tmpl = stringTmplGroups["name"].Value;
                    var tmplValue = ResolveFromDictionary(tmpl, dataContextDictionary);
                    if (tmplValue is not string && tmplValue is not JValue && tmplValue is not JObject && tmplValue is IEnumerable<object> arrayValue)
                    {
                        List<object> newResults = [];
                        foreach (var arrayItem in arrayValue)
                        {
                            List<object> cpResults = [];
                            foreach (var resultItem in results)
                            {
                                if (arrayItem is JObject)
                                {
                                    // BOOKMARK: Tmpl-JToken http请求获取数据源的时候, 默认使用JToken(JObject)处理; 后续DataHandler中也会默认数据源为IEnumerable<JToken>类型进行进一步的处理
                                    cpResults.Add(arrayItem);
                                }
                                else
                                {
                                    var expValue = arrayItem.ToString();
                                    cpResults.Add(resultItem.ToString().Replace(tmpl, expValue));
                                }
                            }
                            newResults.AddRange(cpResults);
                        }
                        results = newResults;
                    }
                    else
                    {
                        for (int i = 0; i < results.Count; i++)
                        {
                            results[i] = results[i].ToString() == tmpl ? tmplValue : results[i].ToString().Replace(tmpl, tmplValue.ToString()).Replace(doubleFlag, "$");
                        }
                    }
                }
            }
            string[] baseTypes = ["String", "Int", "Double", "Float", "Decimal", "DateTime"];
            return results.Count == 1 && baseTypes.Any(x => results.First().GetType().Name.StartsWith(x)) ? results.First() : results;

            object ResolveFromDictionary(string tmplExpressionWithParser, Dictionary<string, object> dataContext)
            {
                tmplExpressionWithParser = tmplExpressionWithParser.TrimStart('$', '{').TrimEnd('}');
                #region 获取Parser名
                var dataContextBuilderMatch = Regex.Match(tmplExpressionWithParser, @"((?<parser>\w+Parser)\[(?<expression>.+)\]$)|(?<expression>.+)");
                var parserName = dataContextBuilderMatch.Groups["parser"].Value;
                var parserExpression = dataContextBuilderMatch.Groups["expression"].Value;
                if (string.IsNullOrWhiteSpace(parserName))
                {
                    parserName = "DataPropertyParser";
                }
                #endregion

                #region 获取Parser对象
                var parseResult = new ParseResult(false);
                if (!_parserObjectMap.TryGetValue(parserName, out ITmplParser parser))
                {
                    var tmplParsers = ReflectionHelper.GetTypes(typeof(ITmplParser));
                    var tmplParser = tmplParsers.FirstOrDefault(x => x.Name == parserName) ?? throw new Exception($"未找到Parser: {parserName}");

                    parser = ReflectionHelper.CreateInstance(tmplParser) as ITmplParser ?? throw new Exception($"Creating an instance of the type [{tmplParser.Name}] has failed");
                    _parserObjectMap[parserName] = parser;
                }
                #endregion

                #region 使用Parser解析模板值
                parseResult = parser.Parse(parserExpression, dataContext);
                var sourceKeys = parseResult.DataSourceKeys;
                if (sourceKeys is not null)
                {
                    if (parseResult.Success && parseResult.Value is not null)
                    {
                        return parseResult.Value;
                    }
                    return parserExpression;
                }
                #endregion

                throw new Exception($"TmplParser解析成功, 但是未返回解析模板中的数据源的Key {nameof(parseResult.DataSourceKeys)}");
            }
        }

        /// <summary>
        /// 获取字符串中的模板
        /// </summary>
        /// <param name="source">可能含模板的表达式</param>
        /// <param name="expressions">模板表达式</param>
        /// <returns></returns>
        static bool TryGetExpressions(this string source, out IEnumerable<string> expressions)
        {
            var tmplExpressionMatchs = Regex.Matches(source, @"(?<exp>\$\w+)|(?<exp>\$\{[^\{\}]+\})|(?<exp>\{\{[^\{\}]+\}\})");
            if (!tmplExpressionMatchs.Any())
            {
                expressions = Enumerable.Empty<string>();
                return false;
            }

            expressions = tmplExpressionMatchs.Cast<Match>().Select(m => m.Groups["exp"].Value);
            return true;
        }
    }
}