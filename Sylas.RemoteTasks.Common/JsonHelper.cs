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
        public static JToken GetMyToken(this JToken source, string expression)
        {
            var expressionMatch = Regex.Match(expression, @"(\[(?<index>\d+)\]){0,1}(?<props>\.\w+)*$");
            if (!expressionMatch.Success)
            {
                throw new Exception($"无法从表达式{expression}解析出正确的属性: {Environment.NewLine}{source}");
            }
            var key = expressionMatch.Groups["key"].Value;

            var index = expressionMatch.Groups["index"].Value;
            var record = string.IsNullOrWhiteSpace(index) ? source : JArray.FromObject(source).ToList()[Convert.ToInt16(index)];

            var props = expressionMatch.Groups["props"].Value.Split('.', StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in props)
            {
                if (record is not JObject pObj)
                {
                    throw new Exception($"无法找到属性{p}");
                }
                record = pObj?.Properties()?.FirstOrDefault(x => string.Equals(x.Name, p, StringComparison.OrdinalIgnoreCase))?.Value ?? throw new Exception($"无法找到属性{p}");
            }
            return record;
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
