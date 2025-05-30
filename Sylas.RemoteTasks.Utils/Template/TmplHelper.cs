using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.Common;
using Sylas.RemoteTasks.Utils.Extensions.Text;
using Sylas.RemoteTasks.Utils.Template.Parser;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
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
        /// <param name="source">数据源 - 就是将它($data)或者它的部分属性存储到dataContext中</param>
        /// <param name="dataContextBuilderTmpls">赋值语句模板, 即变量名=值; 变量名就是dataContext的键, 值通过模板获取source的属性值;支持各种自定义XxxParser, 用于解析获取结构复杂的source属性值</param>
        /// <param name="dataContext">包含原始的数据$data的数据上下文对象</param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static Dictionary<string, object?> BuildDataContextBySource(this Dictionary<string, object> dataContext, object source, List<string> dataContextBuilderTmpls, ILogger? logger = null)
        {
            LogTmplResolving(Environment.NewLine + Environment.NewLine, "------------------->");
            LogTmplResolving(source, "上下文");
            // key为要构建的dataContext的key, value为给key赋的值, 有可能会赋多次值
            Dictionary<string, object?> dataContextBuildDetail = [];
            // 1. source不能确定是什么类型, 只能定位object;
            // 2. 如设为JToken(或其他), 这里会给"$data"重新赋值(变为$data的JToken形式, 丢失了$data的原类型)
            // 3. 我希望JToken类型只作为数据处理过程的工具(很多时候它都无需参与), 输入输出永远是系统的常见类型, 这样数据处理的每一环类型判断简单了一些(输出类型有可能只是处理链上的一环, 会作为下一环的输入...)
            dataContext["$data"] = source;
            // 解析DataContext
            foreach (var dataContextBuilderTmpl in dataContextBuilderTmpls)
            {
                LogTmplResolving(dataContextBuilderTmpl, "模板表达式");
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
                        LogTmplResolving(variableValueResolved, "模板解析结果");
                        dataContext[dataContextNewKey] = variableValueResolved;
                    }
                }
                #endregion
            }
            return dataContextBuildDetail;
        }

        static void LogTmplResolving(object source, string sourceName = "上下文")
        {
            LoggerHelper.RecordLog($"正在记录{sourceName}", "TmplResoving");
            if (source is string sourceStr)
            {
                LoggerHelper.RecordLog(sourceStr, "TmplResoving");
            }
            else if (source is IEnumerable sourceEnumerable)
            {
                IEnumerable<object> sourceObjs = sourceEnumerable.Cast<object>();
                if (sourceObjs.Any())
                {
                    object first = sourceObjs.First();
                    if (first is JsonElement)
                    {
                        IEnumerable<JsonElement> sourceJsonEles = sourceObjs.Cast<JsonElement>();
                        LoggerHelper.RecordLog($"集合每一项为JsonElement集合:", "TmplResoving");
                        StringBuilder itemBuilder = new();
                        foreach (var item in sourceJsonEles)
                        {
                            itemBuilder.AppendLine(item.ToString());
                        }
                        LoggerHelper.RecordLog($"[{itemBuilder}]", "TmplResoving");
                    }
                }
                else
                {
                    LoggerHelper.RecordLog(JsonConvert.SerializeObject(sourceObjs), "TmplResoving");
                }
            }
            else
            {
                LoggerHelper.RecordLog(source is JsonElement ? source.ToString() : JsonConvert.SerializeObject(source), "TmplResoving");
            }
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
        const string _doubleFlag = "DOUBLEFLAGlsjflajflajf238024***%666666^^^^-+Oo)ukj(";
        const string _doubleFlagLeftBrace = $"LEFTBRACE_{_doubleFlag}";
        const string _doubleFlagRigthBrace = $"RIGHTBRACE_{_doubleFlag}";
        /// <summary>
        /// 解析模板
        /// </summary>
        /// <param name="tmplContent"></param>
        /// <param name="dataContextObject"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static string ResolveTemplate(string tmplContent, object dataContextObject)
        {
            Dictionary<string, object> dataContext;
            if (dataContextObject is null)
            {
                dataContext = [];
            }
            else if (dataContextObject is Dictionary<string, object> dataContextDictionary)
            {
                dataContext = dataContextDictionary;
            }
            else
            {
                dataContext = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(dataContextObject)) ?? [];
            }

            tmplContent = tmplContent.Replace("$$", _doubleFlag);
            string[] tmplArray = tmplContent.Split('\n');
            var resolvedInfo = tmplArray.GetAllBlocksAndAllLines("$for", $"$forend");
            var blocks = resolvedInfo.SpecifiedBlocks;
            var lineInfos = resolvedInfo.SequenceLineInfos;
            List<string> resolvedList = [];

            int nextResoledIndex = 0;
            foreach (var block in blocks)
            {
                if (block.Any())
                {
                    var first = block.First();
                    if (first.LineIndex > nextResoledIndex)
                    {
                        // 处理普通模板表达式
                        for (int i = nextResoledIndex; i < first.LineIndex; i++)
                        {
                            resolvedList.Add(tmplArray[i]);
                        }
                    }
                    else
                    {
                        // 处理for循环
                        if (block.Count < 3)
                        {
                            throw new Exception("for循环不足三行");
                        }
                        string line = block[0].Content;
                        var match = Regex.Match(line.Trim(), @"\$for\s+(<?itemKey>\w+)\s+in\s+(<?exp>\w+)");
                        string exp = match.Groups["exp"].Value;
                        string itemKey = match.Groups["itemKey"].Value;
                        if (string.IsNullOrWhiteSpace(exp) || string.IsNullOrWhiteSpace(itemKey))
                        {
                            throw new Exception($"for循环脚本解析错误: {line}");
                        }
                        var expObj = ResolveExpressionValue(exp, dataContext);
                        IEnumerable<object> expList;
                        if (expObj is not IEnumerable expCollection)
                        {
                            throw new Exception($"表达式\"{exp}\"的值无法迭代: {JsonConvert.SerializeObject(expObj)}");
                        }
                        else
                        {
                            expList = expCollection.Cast<object>();
                        }

                        Dictionary<string, object> tempDC = [];
                        for (int i = 1; i < block.Count - 1; i++)
                        {
                            string lineExp = block[i].Content;
                            foreach (var expItem in expList)
                            {
                                tempDC[itemKey] = expItem;
                                string lineExpValue = ResolveExpressionValue(lineExp, tempDC).ToString();
                            }
                        }
                    }
                    nextResoledIndex = block.Last().LineIndex + 1;
                }
            }

            int allTmplLines = tmplArray.Length;

            List<Tuple<int, int>> forBlocks;
            for (int i = 0; i < allTmplLines; i++)
            {
                string line = tmplArray[i];
                line = line.Replace("$$", _doubleFlag);
                forBlocks = [];
                // 如果是for循环模板
                if (line.Trim().StartsWith("$for"))
                {
                    var forPartsMatch = Regex.Match(line, @"\$for\s+(?<item>\w+)\s+in\s+(<?expression>[\w\.\[\]]+)");
                    string expression = forPartsMatch.Groups["expression"].Value;
                    if (string.IsNullOrWhiteSpace(expression))
                    {
                        throw new Exception("for循环模板迭代对象解析异常");
                    }

                    object targetObj = ResolveExpressionValue(expression, dataContext);
                    if (targetObj is not IEnumerable)
                    {
                        throw new Exception("for循环模板迭代对象不可迭代");
                    }

                    // TODO: 实现for循环脚本
                }

                // 如果是扩展数据上下文
            }


            return "";
        }

        static readonly Dictionary<string, ITmplParser> _parserObjectMap = [];

        /// <summary>
        /// 解析模板表达式 dataContext的Key可能是"$expression"开头, 也可能被双花括号括起来"{{expression}}"
        /// dataContext: { "$ids": "[1,2,3]" }; tmpl: "$ids" => [1, 2, 3]
        /// </summary>
        /// <param name="tmplWithParser">字符串模板 $app/${app}/{{app}}</param>
        /// <param name="dataContextObject">模板上下文</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static object ResolveExpressionValue(string tmplWithParser, object dataContextObject)
        {
            if (tmplWithParser.IndexOf('\n') != tmplWithParser.LastIndexOf('\n') && tmplWithParser.Contains("$for"))
            {
                return RenderTemplateWithForLoopBlocks(tmplWithParser, dataContextObject);
            }
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

            tmplWithParser = tmplWithParser.Replace("$$", _doubleFlag);
            string originTmplParser = tmplWithParser;
            // 提取所有模板表达式
            var stringTmplMatches = RegexConst.StringTmpl.Matches(tmplWithParser);

            List<object> result = [];
            List<string> resolvedExp = [];
            if (stringTmplMatches.Count == 0)
            {
                return tmplWithParser;
            }

            // 如果只有一个表达式, 且表达式与源模板相等, 则直接返回解析后的值
            string firstExpression = stringTmplMatches[0].Value;
            if (stringTmplMatches.Count == 1 && tmplWithParser == firstExpression)
            {
                var tmplValue = ResolveFromDictionary(firstExpression, dataContextDictionary);
                return tmplValue;
            }

            bool returnCollection = false;
            // 否则, 逐个解析表达式, 并使用解析的值替换源模板中的表达式
            foreach (var stringTmplMatch in stringTmplMatches.Cast<Match>())
            {
                var stringTmplGroups = stringTmplMatch.Groups;
                if (stringTmplGroups.Count > 1)
                {
                    string exp = stringTmplGroups["name"].Value;

                    if (exp.Trim().Equals("$"))
                    {
                        continue;
                    }
                    // 当前表达式已经解析过, 跳过
                    if (resolvedExp.Count > 0 && resolvedExp.Contains(stringTmplMatch.Value))
                    {
                        continue;
                    }

                    resolvedExp.Add(exp);

                    var tmplValue = ResolveFromDictionary(exp, dataContextDictionary);
                    if (tmplValue is null)
                    {
                        continue;
                    }

                    IEnumerable<object>? arrayValue = null;
                    if (tmplValue is not string && tmplValue is IEnumerable enumerableVal)
                    {
                        arrayValue = enumerableVal.Cast<object>();
                    }
                    else if (tmplValue is JsonElement jEVal && jEVal.ValueKind == JsonValueKind.Array)
                    {
                        arrayValue = jEVal.EnumerateArray().Cast<object>();
                    }
                    // 字符串中包含数组表达式, 那么数组中的每一项都替换掉模板生成一个新的字符串(生成一个字符串集合)
                    if (arrayValue is not null)
                    {
                        if (!returnCollection)
                        {
                            returnCollection = true;
                        }

                        List<object> newResults = [];
                        foreach (var valueItem in arrayValue)
                        {
                            if (valueItem is string)
                            {
                                var expValue = valueItem.ToString();
                                newResults.Add(tmplWithParser.ToString().Replace(exp, expValue).Replace(_doubleFlag, "$"));
                            }
                            else if (valueItem is JsonElement valueItemJE && valueItemJE.ValueKind == JsonValueKind.String)
                            {
                                newResults.Add(tmplWithParser.ToString().Replace(exp, valueItemJE.GetString()).Replace(_doubleFlag, "$"));
                            }
                            else
                            {
                                newResults.Add(tmplWithParser.ToString().Replace(exp, valueItem.ToString()).Replace(_doubleFlag, "$"));
                            }
                        }
                        result.AddRange(newResults);
                    }
                    // 有可能只是不断地替换表达式, 直到没有表达式为止, 那么最终result为空, 返回值为最终的tmplWithParser
                    else if (tmplValue is string tmplStrVal)
                    {
                        tmplWithParser = tmplWithParser.Replace(exp, tmplStrVal);
                    }
                    else if (tmplValue is JsonElement je && je.ValueKind == JsonValueKind.String)
                    {
                        tmplWithParser = tmplWithParser.Replace(exp, tmplValue.ToString());
                    }
                    else
                    {
                        tmplWithParser = tmplWithParser.Replace(exp, tmplValue.ToString());
                    }
                }
            }
            if (returnCollection)
            {
                return result;
            }
            else
            {
                return tmplWithParser.Replace(_doubleFlag, "$");
            }


            object ResolveFromDictionary(string tmplExpressionWithParser, Dictionary<string, object> dataContext)
            {
                tmplExpressionWithParser = tmplExpressionWithParser.TrimStart('$', '{').TrimEnd('}');
                // menuIdsStr=CollectionJoinParser[$menuIds join ,]
                #region 获取Parser名
                var dataContextBuilderMatch = Regex.Match(tmplExpressionWithParser, @"(?<parser>\w+Parser)\[(?<expression>.+)\]");
                if (!dataContextBuilderMatch.Success)
                {
                    // 匹配直接的表达式prop.items[1].propx: 默认的DataPropertyParser
                    dataContextBuilderMatch = Regex.Match(tmplExpressionWithParser, @"(?<expression>.+)");
                }
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
        /// 解析for循环
        /// </summary>
        /// <param name="tmplContent"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        static string RenderTemplateWithForLoopBlocks(string tmplContent, object context)
        {
            StringBuilder bodyBuilder = new();
            var resolvedInfo = tmplContent.GetBlocks("$for", "$forend");
            if (resolvedInfo.SpecifiedBlocks.Count == 0)
            {
                // 没有for循环块, 直接返回原始内容
                return TmplHelper.ResolveExpressionValue(tmplContent, context).ToString()!;
            }
            var blocks = resolvedInfo.SpecifiedBlocks;
            var allLineInfos = resolvedInfo.SequenceLineInfos;

            int blockRenderingLine = 0;
            foreach (var block in blocks)
            {
                // 输出for循环前的普通行
                if (block.FirstLine > blockRenderingLine)
                {
                    for (int i = blockRenderingLine; i < block.FirstLine; i++)
                    {
                        var line = allLineInfos[i].Line.Content;
                        var rendered = TmplHelper.ResolveExpressionValue(line, context).ToString();
                        bodyBuilder.AppendLine(rendered);
                    }
                }
                blockRenderingLine = block.LastLine + 1;

                // 解析for语句
                var forLine = block[0].Content.Trim();
                var match = Regex.Match(forLine, @"^\$for\s+(?<item>\w+)\s+in\s+(?<collection>\w+)", RegexOptions.IgnoreCase);
                if (!match.Success)
                    throw new Exception($"无法解析for语句: {forLine}");

                string itemVar = match.Groups["item"].Value;
                string collectionVar = match.Groups["collection"].Value;
                if (!collectionVar.StartsWith('$'))
                    collectionVar = $"${collectionVar}";

                // 获取集合
                var collectionObj = TmplHelper.ResolveExpressionValue(collectionVar, context);
                if (collectionObj is not IEnumerable enumerable)
                    throw new Exception($"变量{collectionVar}不是可枚举类型");

                // 获取for块体内容(去掉for和forend行, 保留中间内容)
                string blockContent = string.Join(Environment.NewLine, allLineInfos.Skip(block.FirstLine + 1).Take(block.LastLine - block.FirstLine - 1).Select(x => x.Line.Content));

                foreach (var item in enumerable)
                {
                    // 构造新的上下文，包含item变量; **嵌套的循环每项变量可以与前面相同: 每次进入嵌套的for循环中, 都会生成一个全新的context, 不会与父级的context冲突**
                    var itemContext = new Dictionary<string, object>(JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(context))!)
                    {
                        [itemVar] = item
                    };

                    // 递归处理内嵌的for循环
                    string bodyResolved = RenderTemplateWithForLoopBlocks(blockContent, itemContext);
                    if (bodyResolved.Contains('\n'))
                    {
                        bodyBuilder.Append(bodyResolved);
                    }
                    else
                    {
                        bodyBuilder.AppendLine(bodyResolved);
                    }
                }
            }

            // 输出剩余普通行
            if (blockRenderingLine < allLineInfos.Count)
            {
                for (int i = blockRenderingLine; i < allLineInfos.Count; i++)
                {
                    var line = allLineInfos[i].Line.Content;
                    var rendered = TmplHelper.ResolveExpressionValue(line, context).ToString();
                    bodyBuilder.AppendLine(rendered);
                }
            }
            return bodyBuilder.ToString();
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
                expressions = [];
                return false;
            }

            expressions = tmplExpressionMatchs.Cast<Match>().Select(m => m.Groups["exp"].Value);
            return true;
        }
    }
}