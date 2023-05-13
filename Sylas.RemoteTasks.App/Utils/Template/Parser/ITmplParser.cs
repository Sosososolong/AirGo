using Newtonsoft.Json.Linq;

namespace Sylas.RemoteTasks.App.Utils.Template.Parser
{
    /// <summary>
    /// 获取模板字符串的值
    /// 模板字符串如: $user, $user.name, $userList[0].name, $userList select name, $username reg `zhang(?<lastname>\w)` lastname
    /// $user等变量的值来自于数据上下文dataContext, 是一个字典
    /// </summary>
    public interface ITmplParser
    {
        ParseResult Parse(string tmpl, Dictionary<string, object> dataContext);
        public static ParseResult ResolveCollectionSelectTmpl(string key, string prop, string recursively, Dictionary<string, object> dataContext)
        {
            var keyVal = dataContext.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
            var keyValJArray = JArray.FromObject(keyVal) ?? throw new Exception($"上下文数据{key}对应的值不是JArray类型, 无法执行Select操作");
            if (string.Equals(prop, "SELF", StringComparison.OrdinalIgnoreCase))
            {
                //var keyValStringList = keyValJArray.ToObject<List<string>>() ?? throw new Exception($"Template select操作失败: 数据上下文{key}的值{keyValJArray}转换为List<string>失败, 不能执行Select操作");
                return new ParseResult(true, new string[] { key }, keyValJArray);
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
                return new ParseResult(true, new string[] { key }, JArray.FromObject(val));
            }
        }
    }
}
