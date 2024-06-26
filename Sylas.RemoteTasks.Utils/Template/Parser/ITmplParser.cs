using Newtonsoft.Json.Linq;
using Renci.SshNet.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.Utils.Template.Parser
{

#pragma warning disable CS1570 // XML 注释出现 XML 格式错误
    /// <summary>
    /// 获取模板字符串的值
    /// 模板字符串如: $user, $user.name, $userList[0].name, $userList select name, $username reg `zhang(?<lastname>\w)` lastname
    /// $user等变量的值来自于数据上下文dataContext, 是一个字典
    /// </summary>
    public interface ITmplParser
#pragma warning restore CS1570 // XML 注释出现 XML 格式错误
    {
        /// <summary>
        /// 解析模板表达式
        /// </summary>
        /// <param name="tmpl"></param>
        /// <param name="dataContext"></param>
        /// <returns></returns>
        ParseResult Parse(string tmpl, Dictionary<string, object> dataContext);
        /// <summary>
        /// 从集合中选取每一项的指定属性组成新集合, 类似于Linq: $LIST.Select(x => x.$PROP)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="prop"></param>
        /// <param name="recursively"></param>
        /// <param name="dataContext"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static ParseResult ResolveCollectionSelectTmpl(string key, string prop, string recursively, Dictionary<string, object> dataContext)
        {
            var keyVal = dataContext.FirstOrDefault(x => string.Equals(x.Key.TrimStart('$'), key, StringComparison.OrdinalIgnoreCase)).Value;
            bool selectSelf = string.Equals(prop, "SELF", StringComparison.OrdinalIgnoreCase);

            // 1. 理想情况: 因为我希望数据处理的输入输出都是基础类型,对象用字典,集合用字典集合表示这样的常见类型, 所以这里的集合很可能是一个集合字典
            if (keyVal is List<object> valueList)
            {
                if (valueList.Count == 0)
                {
                    return new ParseResult(true, [key], new List<object>());
                }
                if (selectSelf)
                {
                    return new ParseResult(true, [key], valueList);
                }
                else
                {
                    List<Dictionary<string, object>> valueDictionaryList = [];
                    valueList.ForEach(x =>
                    {
                        var dictionary = x as Dictionary<string, object>;
                        if (dictionary is not null)
                        {
                            valueDictionaryList.Add(dictionary);
                        }
                    });

                    if (!string.IsNullOrWhiteSpace(recursively))
                    {
                        valueDictionaryList = NodesHelper.GetAll(valueDictionaryList, "items");
                    }
                    var result = valueDictionaryList
                        .Where(x => x.Keys.Any(k => k.Equals(prop, StringComparison.OrdinalIgnoreCase)))
                        .Select(x =>
                        {
                            var k = x.Keys.First(x => x.Equals(prop));
                            return x[k];
                        });
                    if (result.Count() > 0)
                    {
                        return new ParseResult(true, [key], result);
                    }
                }
            }


            // 2. 当数据处理链的第一环节, 数据源本身就是一个JToken类型
            var keyValJArray = JArray.FromObject(keyVal) ?? throw new Exception($"上下文数据{key}对应的值不是JArray类型, 无法执行Select操作");
            if (keyValJArray.Count == 0)
            {
                return new ParseResult(true, [key], new List<object>());
            }

            if (selectSelf)
            {
                return new ParseResult(true, [key], keyValJArray);
            }
            var keyValJObjList = keyValJArray.ToObject<List<JObject>>() ?? throw new Exception($"Template select操作失败: 数据上下文{key}的值{keyValJArray}转换为List<JObject>失败, 不能执行Select操作");

            if (!string.IsNullOrWhiteSpace(recursively))
            {
                keyValJObjList = NodesHelper.GetAll(keyValJObjList, "items");
            }
            var val = keyValJObjList.Select(x => x.Properties().FirstOrDefault(p => string.Equals(p.Name, prop, StringComparison.OrdinalIgnoreCase))?.Value ?? throw new Exception($"上下文{key}: {keyValJArray} 无法找到属性{prop}"));
            val = val.Where(x => !string.IsNullOrWhiteSpace(x?.ToString()));
            return new ParseResult(true, [key], JArray.FromObject(val));
        }
    }
}
