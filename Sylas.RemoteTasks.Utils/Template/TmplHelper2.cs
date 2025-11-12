using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Sylas.RemoteTasks.Common;
using System.Text.Json;
using Sylas.RemoteTasks.Common.Extensions;

namespace Sylas.RemoteTasks.Utils.Template
{
    /// <summary>
    /// 文本模板帮助类
    /// </summary>
    public static partial class TmplHelper2
    {
        /// <summary>
        /// 解析模板字符串
        /// </summary>
        /// <param name="tmpl"></param>
        /// <param name="globalVars"></param>
        /// <param name="ignoreNotExistExpressions"></param>
        /// <returns></returns>
        public static string ResolveTmpl(string tmpl, object globalVars, bool ignoreNotExistExpressions = false)
        {
            string resolved = ResolveTmplForLoopInfos(tmpl, globalVars);
            return ResolveTmplExpressions(resolved, globalVars, ignoreNotExistExpressions);
        }
        /// <summary>
        /// 解析模板字符串中的变量
        /// </summary>
        /// <param name="tmpl"></param>
        /// <param name="globalVars">
        /// <param name="ignoreNotExistExpressions"></param>
        /// <returns></returns>
        static string ResolveTmplExpressions(string tmpl, object globalVars, bool ignoreNotExistExpressions = false)
        {
            var matches = RegexConst.StringTmpl.Matches(tmpl);
            List<string> resolvedVarNames = [];
            if (globalVars is not Dictionary<string, object> envVars)
            {
                //envVars = globalVars is JsonElement je
                //    ? JsonConvert.DeserializeObject<JObject>(globalVars.ToString())?.ToObject<Dictionary<string, object>>() ?? throw new Exception("无法解析模板字符串, 因为数据源上下文无法转化为字典")
                //    : JObject.FromObject(globalVars).ToObject<Dictionary<string, object>>() ?? throw new Exception("无法解析模板字符串, 因为数据源上下文无法转化为字典");
                envVars = JObject.FromObject(globalVars).ToObject<Dictionary<string, object>>() ?? throw new Exception("无法解析模板字符串, 因为数据源上下文无法转化为字典");
            }
            foreach (Match m in matches)
            {
                var varName = m.Value.TrimStart('$');
                if (varName.StartsWith('{') && varName.EndsWith('}'))
                {
                    varName = varName[1..^1];
                }
                (int code, JToken? varVal, string errMsg) = ResolveExpression(varName, envVars);
                if (code != 1)
                {
                    if (ignoreNotExistExpressions)
                    {
                        continue;
                    }
                    else
                    {
                        throw new Exception($"环境变量中不存在{varName}");
                    }
                }
                if (varVal is JArray valArr)
                {
                    var val = string.Join(',', valArr.Select(x => x.ToString()));
                    tmpl = tmpl.Replace(m.Value, val);
                }
                else
                {
                    tmpl = tmpl.Replace(m.Value, $"{varVal}");
                }
                resolvedVarNames.Add(varName);
            }
            return tmpl;
        }

        /// <summary>
        /// 解析表达式提取数据
        /// </summary>
        /// <param name="extractorStatement"></param>
        /// <param name="storeResultVars"></param>
        /// <exception cref="Exception"></exception>
        public static void ResolveExtractors(string extractorStatement, object storeResultVars)
        {
            if (string.IsNullOrWhiteSpace(extractorStatement))
            {
                return;
            }
            // menuIds=data.0.Items|GetNodePropValues({0},Id,Items)
            var extractorArr = extractorStatement.Split(';');
            foreach (var item in extractorArr)
            {
                if (string.IsNullOrWhiteSpace(item))
                {
                    continue;
                }
                // item为当前表达式, 如: username=$data.0.name 或者userIds.add($data.0.id)
                string key = "";
                string extractor = "";
                bool isAddItemToCollection = false;
                var m = Regex.Match(item, @"\${0,1}(?<key>\w+)\.add\((?<extractor>.*\))");
                if (m.Success)
                {
                    isAddItemToCollection = true;
                    key = m.Groups["key"].Value;
                    extractor = m.Groups["extractor"].Value;
                    if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(extractor))
                    {
                        throw new Exception($".add表达式异常:{item}");
                    }
                }
                else
                {
                    int equalsIndex = item.IndexOf('=');
                    key = item[..equalsIndex];
                    extractor = item[(equalsIndex + 1)..];
                }

                string[] extractors = extractor.Split('|');
                //bool firstExtractor = true;
                JToken? value = storeResultVars is JToken jt ? jt : JToken.FromObject(storeResultVars);
                // 管道命令链式更新value值
                foreach (var extractorItem in extractors)
                {
                    var extractorTrimedFlags = extractorItem.TrimStart('$');
                    if (extractorTrimedFlags.StartsWith('{') && extractorTrimedFlags.EndsWith('}'))
                    {
                        extractorTrimedFlags = extractorTrimedFlags[1..^1];
                    }

                    value = ResolveExpression(extractorTrimedFlags, value).Item2;
                }

                #region 将解析的值存入环境变量字典中
                if (storeResultVars is not Dictionary<string, object?> storeResultVarsDic)
                {
                    storeResultVarsDic = JObject.FromObject(storeResultVars).ToObject<Dictionary<string, object?>>() ?? throw new Exception("无法解析表达式, 因为存储解析结果的对象无法转化为字典");
                }
                if (isAddItemToCollection)
                {
                    if (!storeResultVarsDic.ContainsKey(key))
                    {
                        storeResultVarsDic[key] = new JArray();
                    }
                    if (!storeResultVarsDic.ContainsKey(key))
                    {
                        storeResultVarsDic[key] = new JArray();
                    }
                    var kvArr = (storeResultVarsDic[key] as JArray) ?? throw new Exception($"{key}值不是有效的集合, 无法解析add元素操作");

                    if (value is JArray valueArr)
                    {
                        bool itemIsString = !valueArr.All(x => x is null) && valueArr.Any(x => x.Type == JTokenType.String);
                        foreach (var v in valueArr)
                        {
                            var vStr = $"{v}";
                            if (v is not null && !string.IsNullOrWhiteSpace(vStr) && (!itemIsString || !kvArr.Any(x => $"{x}" == vStr)))
                            {
                                kvArr.Add(v);
                            }
                        }
                    }
                }
                else
                {
                    storeResultVarsDic[key] = value;
                }
                #endregion
            }
        }

        /// <summary>
        /// 解析一个表达式
        /// </summary>
        /// <param name="expression">去掉$符号的表达式</param>
        /// <param name="datasourceObj"></param>
        /// <returns>code:0解析失败-key不存在;1解析成功</returns>
        /// <exception cref="Exception"></exception>
        public static (int, JToken?, string) ResolveExpression(string expression, object? datasourceObj)
        {
            if (datasourceObj is null)
            {
                throw new Exception("数据源为空");
            }
            if (expression.StartsWith('$'))
            {
                expression = expression.TrimStart('$');
            }
            if (expression.StartsWith('{') && expression.EndsWith('}'))
            {
                expression = expression[1..^1];
            }
            JToken? datasource = datasourceObj.CastToJToken();
            // 可以执行方法, 后续可以实现其他方法
            if (expression.StartsWith("XXXXXX"))
            {
                
            }
            else
            {
                string[] props = expression.Split('.');
                string lastProp = string.Empty;
                for (int i = 0; i < props.Length; i++)
                {
                    if (datasource is null && !string.IsNullOrWhiteSpace(lastProp))
                    {
                        throw new Exception($"{lastProp}值为空, 无法继续解析");
                    }
                    string currentProp = props[i];
                    lastProp = currentProp;
                    if (Int32.TryParse(currentProp, out int index))
                    {
                        // 获取数组的index项
                        if (datasource is JArray dsArr)
                        {
                            if (index > dsArr.Count - 1)
                            {
                                throw new Exception(currentProp + $"数据源数组越界, 索引 {index} 超过数组最大索引 {dsArr.Count - 1}");
                            }
                            datasource = dsArr[index];
                        }
                        else
                        {
                            return (2, null, $"数据源({lastProp}值)不是数组类型, 无法通过索引 {index} 访问");
                        }
                    }
                    else if (currentProp.StartsWith("r(", StringComparison.OrdinalIgnoreCase))
                    {
                        if (datasource is null)
                        {
                            throw new Exception("数据源为空, 无法继续解析正则提取表达式r");
                        }
                        string selectChildPropName = currentProp[1..];
                        if (!selectChildPropName.StartsWith('(') || !selectChildPropName.EndsWith(')'))
                        {
                            throw new Exception("正则提取r格式不正确: r(属性名,pattern1,pattern2,...)");
                        }
                        if (datasource.Type != JTokenType.String)
                        {
                            throw new Exception("正则提取r只能作用于字符串类型的数据源");
                        }
                        // 去掉首尾括号
                        selectChildPropName = selectChildPropName[1..^1];
                        string[] arr = selectChildPropName.Split(',');
                        if (arr.Length == 0)
                        {
                            throw new Exception("正则提取r必须要至少一个正则表达式");
                        }
                        var sourceStr = datasource.ToString();
                        List<string> result = [];
                        // 参数第一项(j=0)是提取的字段/属性名, 如.r(Url,REGEX_PATTERN/\\w+)中的Url
                        for (int j = 0; j < arr.Length; j++)
                        {
                            string pattern = arr[j];
                            var m = Regex.Match(sourceStr, pattern);
                            if (m.Success && m.Groups.Count > 1)
                            {
                                var val = m.Groups[1].Value;
                                if (!string.IsNullOrWhiteSpace(val) && !result.Contains(val))
                                {
                                    result.Add(val);
                                }
                            }
                        }
                        datasource = JArray.FromObject(result);
                    }
                    else if (currentProp.StartsWith("selectr", StringComparison.OrdinalIgnoreCase))
                    {
                        // 提取数组所有项的指定属性(如select(name): 提取数组中所有元素的name属性值集合)
                        string selectedPropName = currentProp[7..];
                        if (!selectedPropName.StartsWith('(') || !selectedPropName.EndsWith(')'))
                        {
                            throw new Exception("selectr格式不正确: selectr(属性名,pattern1,pattern2,...)");
                        }
                        // 去掉首尾括号
                        selectedPropName = selectedPropName[1..^1];

                        string[] arr = selectedPropName.Split(',');
                        if (arr.Length == 1)
                        {
                            throw new Exception("selectr必须要包含','用于分隔提取属性和至少一个正则表达式");
                        }
                        selectedPropName = arr[0];

                        // 如果数据源是字符串, 先反序列化为集合
                        if (datasource != null && datasource.Type == JTokenType.String)
                        {
                            var sourceStr = datasource.ToString();
                            datasource = JsonConvert.DeserializeObject<JArray>(sourceStr) ?? throw new Exception("数据源是字符串, 但无法反序列化为集合, 无法解析selectr表达式");
                        }
                        var propValues = datasource.Select(x => x.GetValue(selectedPropName));
                        List<string> result = [];
                        foreach (var pv in propValues)
                        {
                            if (pv is not null && pv.Type != JTokenType.Null && pv.Type != JTokenType.None)
                            {
                                // 参数第一项(j=0)是提取的字段/属性名, 如.selectr(Url,REGEX_PATTERN/\\w+)中的Url
                                for (int j = 1; j < arr.Length; j++)
                                {
                                    string pattern = arr[j];
                                    var m = Regex.Match(pv.ToString(), pattern);
                                    if (m.Success && m.Groups.Count > 1)
                                    {
                                        if (!result.Contains(m.Groups[1].Value))
                                        {
                                            var val = m.Groups[1].Value;
                                            if (!string.IsNullOrWhiteSpace(val) && !result.Contains(val))
                                            {
                                                result.Add(val);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        datasource = JArray.FromObject(result);
                    }
                    else if (currentProp.StartsWith("select", StringComparison.OrdinalIgnoreCase))
                    {
                        // 提取数组所有项的指定属性(如select(name): 提取数组中所有元素的name属性值集合)
                        string selectedPropName = currentProp[6..].TrimStart('(').TrimEnd(')');
                        // 集合元素包含子集时, 子集的字段名
                        string childrenFieldName = string.Empty;
                        if (selectedPropName.Contains(','))
                        {
                            // 元素包含子集(需要提取值的属性,子集的字段名)
                            var selectChildPropNameArr = selectedPropName.Split(',');
                            selectedPropName = selectChildPropNameArr[0].Trim();
                            childrenFieldName = selectChildPropNameArr[1].Trim();
                        }

                        // 如果数据源是字符串, 先反序列化为集合
                        if (datasource != null && datasource.Type == JTokenType.String)
                        {
                            var sourceStr = datasource.ToString();
                            datasource = JsonConvert.DeserializeObject<JArray>(sourceStr) ?? throw new Exception("数据源是字符串, 但无法反序列化为集合, 无法解析selectr表达式");
                        }
                        datasource = !string.IsNullOrWhiteSpace(childrenFieldName)
                            ? JArray.FromObject(NodesHelper.GetNodePropValues((datasource as JArray)!, selectedPropName, childrenFieldName))
                            : JArray.FromObject(datasource.Select(x => x.GetValue(selectedPropName)));
                    }
                    else
                    {
                        // 获取对象的currentProp属性
                        // TODO:简化代码
                        JObject? o = datasource as JObject;
                        if (o is null)
                        {
                            throw new Exception($"数据源不是有效的对象, 无法获取属性({currentProp})值");
                        }
                        JProperty p = o.Properties().FirstOrDefault(x => x.Name.Equals(currentProp, StringComparison.OrdinalIgnoreCase));
                        if (p is null)
                        {
                            return (0, null, string.Empty);
                        }
                        datasource = p.Value;
                    }
                }
            }
            return (1, datasource, string.Empty);
        }
        /// <summary>
        /// 解析for循环
        /// </summary>
        /// <param name="tmpl"></param>
        /// <param name="envVars"></param>
        /// <returns></returns>
        public static string ResolveTmplForLoopInfos(string tmpl, object envVars)
        {
            var matches = Regex.Matches(tmpl, @"for\s*\((?<item>\w+)\s+in\s+\$(?<collection>\w+)\)\s*\{\s*\n*(?<content>[^\{\}]*(((?'Open'\{)[^\{\}]*)+((?'-Open'\})[^\{\}]*)+)*)\}");
            for (int i = 0; i < matches.Count; i++)
            {
                var content = matches[i].Groups["content"].Value;
                var collection = matches[i].Groups["collection"].Value;
                var collectionItem = matches[i].Groups["item"].Value;

                JToken envVarsObj = envVars.CastToJToken() ?? throw new Exception("数据上下文对象无法转换为JToken"); // envVars is JObject ? (envVars as JObject)! : JObject.FromObject(envVars);
                var collectionValue = envVarsObj[collection];
                StringBuilder contentResultBuilder = new();
                if (collectionValue is IEnumerable collectionEnumerable)
                {
                    foreach (var item in collectionEnumerable.Cast<JToken>())
                    {
                        envVarsObj[collectionItem] = item;
                        // 有一些变量是从上下文取出的数据中获取的, 初始时不存在无法解析, 则忽略
                        string resolved = ResolveTmplExpressions(content, envVarsObj, true);
                        contentResultBuilder.Append(resolved);
                    }
                }

                // 去掉for循环表达式
                tmpl = tmpl.Replace(matches[i].Value, contentResultBuilder.ToString());
            }
            return tmpl;
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