using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.RegexExp;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.App.Utils
{
    public static partial class TmplHelper
    {
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
