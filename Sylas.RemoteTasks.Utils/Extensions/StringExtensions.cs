using System.Linq;

namespace Sylas.RemoteTasks.Utils.Extensions
{
    /// <summary>
    /// 字符串操作扩展类
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// 根据名称构建其副本名称
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetCopiedName(this string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "DefaultName";
            }
            const string tail = " - 副本";
            name = name.Trim();
            var splited = name.Split(tail);
            if (splited.Length == 1)
            {
                name = $"{name}{tail}";
            }
            else
            {
                string countStr = splited.Last();
                if (string.IsNullOrWhiteSpace(countStr))
                {
                    name = $"{name}(2)";
                }
                else
                {
                    if (countStr.StartsWith('(') && countStr.EndsWith(')') && int.TryParse(countStr.Trim('(', ')'), out int countValue))
                    {
                        name = name.Replace($"({countValue})", $"({countValue + 1})");
                    }
                }
            }
            return name;
        }
    }
}
