using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;

namespace Sylas.RemoteTasks.Common.Extensions
{
    /// <summary>
    /// object类型类型扩展
    /// </summary>
    public static class ObjectExtensions
    {
        /// <summary>
        /// 获取object类型对象指定的属性值
        /// </summary>
        /// <param name="source">对象</param>
        /// <param name="propName"></param>
        /// <returns></returns>
        public static object GetPropertyValue(this object source, string propName)
        {
            if (source is IDictionary<string, object> dataContextDictionary)
            {
                string key = dataContextDictionary.Keys.FirstOrDefault(x => x.Equals(propName, StringComparison.OrdinalIgnoreCase)) ?? throw new Exception($"无法获取属性{propName}的值");
                return dataContextDictionary[key];
            }
            else if (source is JObject jt)
            {
                if (string.IsNullOrWhiteSpace(propName))
                {
                    throw new Exception($"获取对象的指定属性时属性名为空");
                }
                return jt.Properties().FirstOrDefault(x => x.Name.Equals(propName, StringComparison.OrdinalIgnoreCase))?.Value ?? throw new Exception($"无法获取属性{propName}的值");
            }
            else if (source is JToken jtoken && jtoken.Type == JTokenType.String && string.IsNullOrWhiteSpace(propName))
            {
                return jtoken.ToString();
            }
            else if (source is JsonElement jsonEle)
            {
                return JsonHelper.GetDataElement(jsonEle, propName);
            }
            else
            {
                if (source is not JObject dataContextJObj)
                {
                    return JObject.FromObject(source)[propName] ?? throw new Exception($"无法获取属性{propName}的值");
                }
                return dataContextJObj.Properties().FirstOrDefault(p => p.Name.Equals(propName, StringComparison.OrdinalIgnoreCase))?.Value ?? throw new Exception($"无法获取属性{propName}的值");
            }
        }
        /// <summary>
        /// 复制字典
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static Dictionary<string, object?> Copy(this Dictionary<string, object?> source)
        {
            var result = new Dictionary<string, object?>();
            foreach (var item in source)
            {
                var itemValue = item.Value;
                if (itemValue != null)
                {
                    if (itemValue is Dictionary<string, object?> dic)
                    {
                        itemValue = dic.Copy();
                    }
                    else if (itemValue is IDictionary<string, object> idic)
                    {
                        itemValue = idic.CastToDictionary();
                    }
                }
                result.Add(item.Key, itemValue);
            }
            return result;
        }
        /// <summary>
        /// 对象转换为字典
        /// </summary>
        /// <param name="object"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static Dictionary<string, object?> CastToDictionary(this object obj)
        {
            Dictionary<string, object?> result;
            if (obj is DataRow row)
            {
                var columns = row.Table.Columns;
                Dictionary<string, object?> dictionary = [];
                foreach (DataColumn column in columns)
                {
                    dictionary[column.ColumnName] = row[column];
                }
                result = dictionary;
            }
            else if (obj is Dictionary<string, object?> dic)
            {
                result = dic;
            }
            else if (obj is IDictionary<string, object>)
            {
                Dictionary<string, object?> dictionary = [];
                foreach (var item in (obj as IDictionary<string, object?>)!)
                {
                    dictionary[item.Key] = item.Value;
                }
                result = dictionary;
            }
            else if (obj is JsonElement je)
            {
                var rawText = je.GetRawText();
                var dictionary = JsonConvert.DeserializeObject<Dictionary<string, object?>>(rawText) ?? throw new Exception("JsonElement转换为字典失败");
                result = dictionary;
            }
            else
            {
                result = JsonConvert.DeserializeObject<Dictionary<string, object?>>(JsonConvert.SerializeObject(obj)) ?? throw new Exception("对象集合转换为字典失败");
            }
            RemoveDictionaryJsonElementData(result);
            return result;
        }
        /// <summary>
        /// 对象转换为JToken类型
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static JToken? CastToJToken(this object obj)
        {
            var datasourceDictionary = obj.CastToDictionary();
            JToken? jt = JToken.FromObject(datasourceDictionary);
            return jt;
        }
        /// <summary>
        /// 移除字典中的JsonElement类型的数据
        /// </summary>
        /// <param name="datasource"></param>
        static void RemoveDictionaryJsonElementData(Dictionary<string, object?> datasource)
        {
            foreach (var key in datasource.Keys)
            {
                if (datasource[key] is string || datasource[key] is int || datasource[key] is long || datasource[key] is double || datasource[key] is float || datasource[key] is decimal)
                {
                    continue;
                }
                else if (datasource[key] is JsonElement je)
                {
                    datasource[key] = JsonConvert.DeserializeObject<JToken>(je.GetRawText());
                    //if (je.ValueKind == JsonValueKind.String)
                    //{
                    //    datasource[key] = je.GetRawText();
                    //}
                    //else if (je.ValueKind == JsonValueKind.Number)
                    //{
                    //    datasource[key] = je.GetInt32();
                    //}
                    //else if (je.ValueKind == JsonValueKind.True || je.ValueKind == JsonValueKind.False)
                    //{
                    //    datasource[key] = je.GetRawText() == "True";
                    //}
                    //else if (je.ValueKind == JsonValueKind.Null || je.ValueKind == JsonValueKind.Undefined)
                    //{
                    //    datasource[key] = null;
                    //}
                    //if (je.ValueKind == JsonValueKind.Array)
                    //{
                    //    int itemIndex = -1;
                    //    JsonValueKind itemKind = JsonValueKind.Object;
                    //    List<JsonElement> jes = [];
                    //    foreach (var item in je.EnumerateArray())
                    //    {
                    //        itemIndex++;
                    //        if (itemIndex == 0)
                    //        {
                    //            itemKind = item.ValueKind;
                    //        }
                    //        jes.Add(item);
                    //        //if (itemKindIsObject)
                    //        //{
                    //        //    JsonElementToDictionary(item);
                    //        //}
                    //    }
                    //    if (itemKind == JsonValueKind.Object)
                    //    {
                    //        List<Dictionary<string, object?>> list = [];
                    //        foreach (var item in jes)
                    //        {
                    //            var dic = JsonElementToDictionary(item);
                    //            list.Add(dic);
                    //        }
                    //        datasource[key] = list;
                    //    }
                    //    else if (true)
                    //    {

                    //    }
                    //}
                    //else
                    //{
                    //    var dic = JsonElementToDictionary(je);
                    //    datasource[key] = dic;
                    //}

                    //Dictionary<string, object?> JsonElementToDictionary(JsonElement je)
                    //{
                    //    var dic = JsonHelper.JsonElementToDictionary(je);
                    //    RemoveDictionaryJsonElementData(dic);
                    //    return dic;
                    //}
                }
                else if (datasource[key] is IEnumerable<object> values)
                {
                    List<Dictionary<string, object?>> list = [];
                    if (values.Count() > 0)
                    {
                        var first = values.First();
                        if (first is JsonElement)
                        {
                            datasource[key] = values.Cast<JsonElement>().Select(x =>
                            {
                                var dic = JsonHelper.JsonElementToDictionary(x);
                                RemoveDictionaryJsonElementData(dic);
                                list.Add(dic);
                                return dic;
                            }).ToList();
                        }
                    }
                }
                else if (datasource[key] is JToken jt)
                {
                    if (jt.Type == JTokenType.Object)
                    {
                        var dic = JsonConvert.DeserializeObject<Dictionary<string, object?>>(jt.ToString());
                        datasource[key] = dic;
                    }
                    else if (jt.Type == JTokenType.Array)
                    {
                        datasource[key] = JsonConvert.DeserializeObject<List<Dictionary<string, object?>>>(jt.ToString());
                    }
                }
            }
        }
    }
}
