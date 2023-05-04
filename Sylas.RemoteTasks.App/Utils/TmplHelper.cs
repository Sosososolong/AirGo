﻿using Newtonsoft.Json.Linq;
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
    }
}
