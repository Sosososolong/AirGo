using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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
                return dataContextJObj[propName] ?? throw new Exception($"无法获取属性{propName}的值");
            }
        }
        /// <summary>
        /// 复制字典
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static Dictionary<string, object> Copy(this Dictionary<string, object> source)
        {
            var result = new Dictionary<string, object>();
            foreach (var item in source)
            {
                result.Add(item.Key, item.Value);
            }
            return result;
        }
    }
}
