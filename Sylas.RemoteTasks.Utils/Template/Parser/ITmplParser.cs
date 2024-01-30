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
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="prop"></param>
        /// <param name="recursively"></param>
        /// <param name="dataContext"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static ParseResult ResolveCollectionSelectTmpl(string key, string prop, string recursively, Dictionary<string, object> dataContext)
        {
            var keyVal = dataContext.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
            var keyValJArray = JArray.FromObject(keyVal) ?? throw new Exception($"上下文数据{key}对应的值不是JArray类型, 无法执行Select操作");
            if (string.Equals(prop, "SELF", StringComparison.OrdinalIgnoreCase))
            {
                return new ParseResult(true, [key], keyValJArray);
            }
            else
            {
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
}
