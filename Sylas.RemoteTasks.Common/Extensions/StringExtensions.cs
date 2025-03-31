using System.Collections.Generic;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;

namespace Sylas.RemoteTasks.Common.Extensions
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
        /// <summary>
        /// 将蛇形命名转换为帕斯卡命名
        /// </summary>
        /// <param name="snake_case_name"></param>
        /// <returns></returns>
        public static string ConvertSnakeCaseToPascalCase(this string snake_case_name)
        {
            // 1.正则匹配字符串中所有的"Xxx",
            var res = Regex.Replace(snake_case_name, @"([A-Z])[a-z]+", match =>
            {
                // 2.然后在委托中替换大写字母"X"为"_x", 最后去掉开头的"_"
                var origin = match.Groups[0].Value;
                var target = match.Groups[1].Value;
                return origin.Replace(target, "_" + target.ToLower());
            }).TrimStart('_');
            return res;
        }
        /// <summary>
        /// 用于将大驼峰命名的单词修改为小驼峰命名
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string ToCamelCase(this string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }
            return char.ToLowerInvariant(str[0]) + str[1..];
        }
        /// <summary>
        /// 获取单词的复数形式
        /// </summary>
        /// <param name="word"></param>
        /// <returns></returns>
        public static string Pluralize(this string word)
        {
            // 不规则复数形式字典
            var irregularPlurals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "child", "children" },
                { "mouse", "mice" },
                { "person", "people" },
                { "goose", "geese" }, // [ɡus] 鹅; 呆头鹅
                { "man", "men" },
                { "woman", "women" },
                { "tooth", "teeth" },
                { "foot", "feet" },
                // 单复数同形
                { "sheep", "sheep" },
                { "deer", "deer" }, // 鹿
                { "fish", "fish" },
                { "species", "species" }, // [ˈspisiz] 物种; 种类; 异种
                { "aircraft", "aircraft" }, // [ˈerˌkræft] 飞机; 飞行器
                { "salmon", "salmon" }, // [ˈsæmən] 鲑鱼肉; [鱼]三文鱼; 鲑鱼肉色
                { "bison", "bison" }, // [ˈbaɪs(ə)n] 野牛（分北美野牛和欧洲野牛两类）
                { "moose", "moose" }, // [mus] 驼鹿（产于北美; 在欧洲和亚洲称为麋鹿）
                { "swine", "swine" }, // [swaɪn] 猪; 讨厌的人; 令人不愉快的事物; 难处理的东西
                { "corps", "corps" }, // [kɔr] 兵团; （陆军）特种部队; （从事某工作或活动的）一群人
                { "means", "means" }, // 方式; 方法; 途径; 财富; 钱财
                { "series", "series" },
                { "cod", "cod" }, // [kɒd] 鳕鱼
                { "shrimp", "shrimp" }, // [ʃrɪmp] 虾
                { "crab", "crab" }, // [kræb] 螃蟹
                { "squid", "squid" }, // 鱿鱼; 乌贼
                { "octopus", "octopus" }, // [ˈɒktəpəs] 章鱼
                { "cactus", "cacti" }, // ['kæktəs] 仙人掌
                { "focus", "focuses" },
                { "appendix", "appendices" }, // [ə'pendɪks] 阑尾; （书、文件的）附录
                { "index", "indices" },
                { "matrix", "matrices" }, // [ˈmeɪtrɪks] 矩阵; 基体; （人或社会成长发展的）社会环境; 线路网
                { "vertex", "vertices" }, // [ˈvɜː(r)teks] 三角形或锥形的）顶点; 角顶; 至高点
                { "parenthesis", "parentheses" }, // [pə'renθəsɪs] 括号; 插入语; 附录; 插曲
                { "memorandum", "memoranda" }, // [.memə'rændəm] 备忘录; 便笺; 通知; 备忘录
                { "syllabus", "syllabi" }, // ['sɪləbəs] 教学大纲
                { "analysis", "analyses" }, // ['ænələsɪs] 分析; 分解; 解析; 解剖; 病理分析
                { "axis", "axes" }, // ['æksɪs] 坐标轴; 轴（旋转物体假想的中心线; 对称中心线（将物体平分为二）
                { "basis", "bases" }, // 基础; 基准; 基点; 方式
                { "crisis", "crises" }, // ['kraɪsɪs] 危机; 危急关头; 危难时刻; 病危期
                { "diagnosis", "diagnoses" }, // [daɪəɡˈnəʊsɪs] 诊断; （问题原因的）判断
                { "ellipsis", "ellipses" }, // 省略; 省略号
                { "hypothesis", "hypotheses" }, // [haɪˈpɑθəsɪs] 假设; 猜想;
                { "thesis", "theses" }, // 毕业论文; 学位论文
                { "mythos", "mythoi" }, // ['maɪθɒs] 神话
            };

            // 检查是否是不规则复数形式
            if (irregularPlurals.TryGetValue(word, out string result))
            {
                return result;
            }

            // 常见规则
            if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) ||
                word.EndsWith("x", StringComparison.OrdinalIgnoreCase) ||
                word.EndsWith("z", StringComparison.OrdinalIgnoreCase) ||
                word.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
                word.EndsWith("sh", StringComparison.OrdinalIgnoreCase))
            {
                return word + "es";
            }

            if (word.EndsWith("y", StringComparison.OrdinalIgnoreCase) &&
                !"aeiouAEIOU".Contains(word[^2])) // 辅音字母 + y
            {
                return word[..^1] + "ies";
            }

            if (word.EndsWith("f", StringComparison.OrdinalIgnoreCase))
            {
                return word[..^1] + "ves";
            }

            if (word.EndsWith("fe", StringComparison.OrdinalIgnoreCase))
            {
                return word[..^2] + "ves";
            }

            // 默认规则
            return word + "s";
        }

        /// <summary>
        /// 解析一对符号之间(可能包含嵌套符号)的内容Groups["x"](Value包含符号)
        /// </summary>
        /// <param name="text">需要解析的文本</param>
        /// <param name="leftSymbol">一对符号的其中左边符号/第一个符号</param>
        /// <param name="rightSymbol">一对符号的其中右边符号/第二个符号</param>
        /// <param name="leftPattern">匹配符号范围左边文本的正则表达式</param>
        /// <param name="rightPattern">匹配符号范围右边文本的正则表达式</param>
        /// <returns></returns>
        public static IEnumerable<Match> ResolvePairedSymbolsContent(this string text, string leftSymbol, string rightSymbol, string leftPattern, string rightPattern)
        {
            string pattern = $@"{leftPattern}{leftSymbol}(?<x>[^{leftSymbol}{rightSymbol}]*(((?'Open'{leftSymbol})[^{leftSymbol}{rightSymbol}]*)+((?'-Open'{rightSymbol})[^{leftSymbol}{rightSymbol}]*)+)*)\){rightPattern}";
            var matches = Regex.Matches(text, pattern);
            List<Match> result = [];
            // 去重
            foreach (Match match in matches.Cast<Match>())
            {
                if (!result.Any(x => x.Value.Equals(match.Value)))
                {
                    result.Add(match);
                }
            }
            return result;
        }
        /// <summary>
        /// 去掉代码中的注释
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public static string RemoveComments(string code)
        {
            StringBuilder result = new();
            int length = code.Length;
            bool inSingleLineComment = false;
            bool inMultiLineComment = false;
            bool inString = false;

            for (int i = 0; i < length; i++)
            {
                // 检查是否进入字符串
                if (!inSingleLineComment && !inMultiLineComment && code[i] == '"')
                {
                    inString = !inString; // 切换字符串状态
                }

                // 检查是否进入单行注释
                if (!inString && !inMultiLineComment && i + 1 < length && code[i] == '/' && code[i + 1] == '/')
                {
                    inSingleLineComment = true;
                    i++; // 跳过第二个 '/'
                    continue;
                }

                // 检查是否进入多行注释
                if (!inString && !inSingleLineComment && i + 1 < length && code[i] == '/' && code[i + 1] == '*')
                {
                    inMultiLineComment = true;
                    i++; // 跳过 '*'
                    continue;
                }

                // 检查是否退出多行注释
                if (inMultiLineComment && i + 1 < length && code[i] == '*' && code[i + 1] == '/')
                {
                    inMultiLineComment = false;
                    i++; // 跳过 '/'
                    continue;
                }

                // 检查是否退出单行注释（遇到换行符）
                if (inSingleLineComment && code[i] == '\n')
                {
                    inSingleLineComment = false;
                }

                // 如果不在注释中，将字符添加到结果中
                if (!inSingleLineComment && !inMultiLineComment)
                {
                    result.Append(code[i]);
                }
            }

            return result.ToString();
        }
        /// <summary>
        /// 将字符串转换为Base64编码
        /// </summary>
        /// <param name="origin"></param>
        /// <returns></returns>
        public static string ToBase64(this string origin) => Convert.ToBase64String(Encoding.UTF8.GetBytes(origin));
        /// <summary>
        /// 将Base64编码的字符串还原为原始字符串
        /// </summary>
        /// <param name="base64"></param>
        /// <returns></returns>
        public static string FromBase64(this string base64) => Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }
}
