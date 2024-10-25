using Newtonsoft.Json.Linq;
using Renci.SshNet.Security;
using Sylas.RemoteTasks.Utils.Extensions;
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
            if (keyVal is IEnumerable<object> valueList)
            {
                if (!valueList.Any())
                {
                    return new ParseResult(true, [key], new List<object>());
                }
                if (selectSelf)
                {
                    return new ParseResult(true, [key], valueList);
                }
                else
                {
                    var list = string.IsNullOrWhiteSpace(recursively)
                        ? valueList
                        : NodesHelper.GetAll(valueList, "items");
                    var result = list.Select(x => x.GetPropertyValue(prop));
                    return new ParseResult(true, [key], result);

                    //if (!string.IsNullOrWhiteSpace(recursively))
                    //{
                    //    valueDictionaryList = NodesHelper.GetAll(valueDictionaryList, "items");
                    //}
                    //var result = valueDictionaryList
                    //    .Where(x => x.Keys.Any(k => k.Equals(prop, StringComparison.OrdinalIgnoreCase)))
                    //    .Select(x =>
                    //    {
                    //        var k = x.Keys.First(x => x.Equals(prop));
                    //        return x[k];
                    //    });
                    //if (result.Count() > 0)
                    //{
                    //    return new ParseResult(true, [key], result);
                    //}
                }
            }
            throw new Exception("不是集合无法执行Select操作");
        }
    }
}
