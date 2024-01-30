using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Sylas.RemoteTasks.Utils.Extensions
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
            if (source is Dictionary<string, object> dataContextDictionary)
            {
                if (!dataContextDictionary.TryGetValue(propName, out object firstPropValueObj) || firstPropValueObj is null)
                {
                    throw new Exception($"无法获取属性{propName}的值");
                }
                return firstPropValueObj;
            }
            else if (source is JObject dataContextJObj)
            {
                return dataContextJObj[propName] ?? throw new Exception($"无法获取属性{propName}的值");
            }
            else
            {
                return JObject.FromObject(source)[propName] ?? throw new Exception($"无法获取属性{propName}的值");
            }
        }
    }
}
