using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.Common
{
    /// <summary>
    /// JToken助手
    /// </summary>
    public static class JsonHelper
    {
        /// <summary>
        /// 获取指定token
        /// </summary>
        /// <param name="source"></param>
        /// <param name="expression"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static JToken? GetValue(this JToken source, string propName)
        {
            if (source.Type == JTokenType.String && string.IsNullOrWhiteSpace(propName))
            {
                return source?.ToString();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(propName))
                {
                    throw new Exception($"select对象的指定属性为空");
                }
                var obj = JObject.FromObject(source);
                var val = obj.Properties().FirstOrDefault(x => x.Name.Equals(propName, StringComparison.OrdinalIgnoreCase))?.Value;
                return val;
            }
        }
        /// <summary>
        /// 获取JsonElement对象的属性值
        /// </summary>
        /// <param name="root"></param>
        /// <param name="pathSegments"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static JsonElement GetDataElement(JsonElement root, string[] pathSegments)
        {
            JsonElement currentElement = root;

            foreach (string segment in pathSegments)
            {
                JsonElement? nextElement = null;
                foreach (var item in currentElement.EnumerateObject())
                {
                    if (item.Name.Equals(segment, StringComparison.OrdinalIgnoreCase))
                    {
                        nextElement = item.Value;
                        break;
                    }
                }
                // 区分大小写
                if (nextElement is null)
                {
                    currentElement = default;
                }
                else
                {
                    currentElement = nextElement.Value;
                }
            }

            return currentElement;
        }
        /// <summary>
        /// 获取JsonElement对象的属性值
        /// </summary>
        /// <param name="root"></param>
        /// <param name="configPath"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static JsonElement GetDataElement(JsonElement root, string configPath)
        {
            if (string.IsNullOrWhiteSpace(configPath))
            {
                return root;
            }
            string[] pathSegments = configPath.Split([':', '.'], StringSplitOptions.RemoveEmptyEntries);
            return GetDataElement(root, pathSegments);
        }
        /// <summary>
        /// 将JsonElement转换为Dictionary
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public static Dictionary<string, object?> JsonElementToDictionary(JsonElement element)
        {
            var dictionary = new Dictionary<string, object?>();

            foreach (JsonProperty property in element.EnumerateObject())
            {
                dictionary[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.Object => JsonElementToDictionary(property.Value),
                    JsonValueKind.Array => property.Value.EnumerateArray().Select(JsonElementToDictionary).ToList(),
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetDecimal(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    JsonValueKind.Undefined => null,
                    _ => property.Value.GetRawText()
                };
            }

            return dictionary;
        }
    }
}
