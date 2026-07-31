using System;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.Utils.Template
{
    /// <summary>
    /// 时间表达式解析器: 借鉴PostgreSQL的interval语法, 根据当前时间动态计算时间值
    /// <para>语法: now(['格式']) [+|- interval '数量 单位 [数量 单位 ...]'] ...</para>
    /// <para>示例:</para>
    /// <para>{{now()}} → 2026-06-25 14:30:52 (默认格式 yyyy-MM-dd HH:mm:ss)</para>
    /// <para>{{now('yyyyMMddHHmmss')}} → 20260625143052 (自定义.NET时间格式)</para>
    /// <para>{{now('timestamp')}} → Unix秒级时间戳; now('timestamp_ms') → 毫秒级; now('iso') → ISO 8601</para>
    /// <para>{{now() - interval '5 minutes'}} → 5分钟前</para>
    /// <para>{{now('yyyy-MM-dd') + interval '1 day 2 hours'}} → 明天(复合单位, 同PostgreSQL)</para>
    /// <para>单位支持: year(s)/y, month(s)/mon, week(s)/w, day(s)/d, hour(s)/h, minute(s)/min, second(s)/sec/s, millisecond(s)/ms</para>
    /// </summary>
    public static class TimeExprHelper
    {
        const string DefaultFormat = "yyyy-MM-dd HH:mm:ss";
        // 表达式主体: now(['fmt']) 后跟零或多个 ± interval 'xxx'
        const string ExprBody = @"now\(\s*(?:'(?<fmt>[^']*)')?\s*\)(?<ops>(?:\s*[+-]\s*interval\s*'[^']+')*)";
        // 模板中形如 {{ now() - interval '5 minutes' }} 的片段
        static readonly Regex TmplPattern = new(@"\{\{\s*" + ExprBody + @"\s*\}\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // 整个字符串就是一个时间表达式(不带花括号, 用于环境变量的值)
        static readonly Regex WholeExprPattern = new(@"^\s*" + ExprBody + @"\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // 单个 ± interval 'xxx' 运算
        static readonly Regex OpPattern = new(@"(?<sign>[+-])\s*interval\s*'(?<body>[^']+)'", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // interval内容中的 数量+单位 对, 如 "1 day 2 hours" 解析出 (1, day) (2, hours)
        static readonly Regex UnitPattern = new(@"(?<n>\d+(?:\.\d+)?)\s*(?<u>[a-zA-Z]+)", RegexOptions.Compiled);

        /// <summary>
        /// 将模板文本中所有 {{now()...}} 时间表达式替换为计算后的时间值; 解析失败的片段原样保留
        /// </summary>
        public static string ResolveTimeExpressions(string tmpl)
        {
            if (string.IsNullOrEmpty(tmpl) || tmpl.IndexOf("now(", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return tmpl;
            }
            return TmplPattern.Replace(tmpl, m => Evaluate(m) ?? m.Value);
        }

        /// <summary>
        /// 值本身就是一个时间表达式(如环境变量的值配置为 now() - interval '5 minutes')时计算其值, 否则原样返回
        /// </summary>
        public static string ResolveValueIfTimeExpression(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.IndexOf("now(", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return value;
            }
            var m = WholeExprPattern.Match(value);
            return m.Success ? Evaluate(m) ?? value : value;
        }

        /// <summary>
        /// 计算表达式匹配结果的时间值; 单位或格式非法时返回null(由调用方保留原文)
        /// </summary>
        static string? Evaluate(Match m)
        {
            var time = DateTime.Now;
            foreach (Match op in OpPattern.Matches(m.Groups["ops"].Value))
            {
                int sign = op.Groups["sign"].Value == "-" ? -1 : 1;
                var units = UnitPattern.Matches(op.Groups["body"].Value);
                if (units.Count == 0)
                {
                    return null;
                }
                foreach (Match unit in units)
                {
                    double n = double.Parse(unit.Groups["n"].Value) * sign;
                    switch (unit.Groups["u"].Value.ToLowerInvariant())
                    {
                        case "year" or "years" or "y": time = time.AddYears((int)n); break;
                        case "month" or "months" or "mon" or "mons": time = time.AddMonths((int)n); break;
                        case "week" or "weeks" or "w": time = time.AddDays(n * 7); break;
                        case "day" or "days" or "d": time = time.AddDays(n); break;
                        case "hour" or "hours" or "h": time = time.AddHours(n); break;
                        case "minute" or "minutes" or "min" or "mins": time = time.AddMinutes(n); break;
                        case "second" or "seconds" or "sec" or "secs" or "s": time = time.AddSeconds(n); break;
                        case "millisecond" or "milliseconds" or "ms": time = time.AddMilliseconds(n); break;
                        default: return null;
                    }
                }
            }

            string fmt = m.Groups["fmt"].Success && m.Groups["fmt"].Value.Length > 0 ? m.Groups["fmt"].Value : DefaultFormat;
            try
            {
                return fmt.ToLowerInvariant() switch
                {
                    "timestamp" => new DateTimeOffset(time).ToUnixTimeSeconds().ToString(),
                    "timestamp_ms" => new DateTimeOffset(time).ToUnixTimeMilliseconds().ToString(),
                    "iso" => time.ToString("o"),
                    _ => time.ToString(fmt)
                };
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
