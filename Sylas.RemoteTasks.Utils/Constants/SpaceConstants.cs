using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.Utils.Constants
{
    /// <summary>
    /// 空格常量
    /// </summary>
    public static class SpaceConstants
    {
        /// <summary>
        /// 匹配空格占位符 {sp} 或 {sp:N} 的正则表达式
        /// </summary>
        private static readonly Regex SpacePlaceholderRegex = new(@"\{sp(?::(\d+))?\}", RegexOptions.Compiled);

        /// <summary>
        /// 匹配换行占位符 {br} 或 {br:N} 的正则表达式
        /// </summary>
        private static readonly Regex BreakPlaceholderRegex = new(@"\{br(?::(\d+))?\}", RegexOptions.Compiled);

        /// <summary>
        /// 替换空格占位符 {sp} 和 {sp:N} 以及旧版 &amp;nbsp;（不处理换行占位符）
        /// </summary>
        /// <param name="input">输入字符串</param>
        /// <returns>替换后的字符串</returns>
        public static string ReplaceSpacePlaceholders(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            // 先处理新语法 {sp} 和 {sp:N}
            string result = SpacePlaceholderRegex.Replace(input, match =>
            {
                // 如果没有捕获组或捕获组为空，默认为1个空格
                int count = match.Groups[1].Success && int.TryParse(match.Groups[1].Value, out int n) ? n : 1;
                return new string(OneSpace, count);
            });

            // 保持向后兼容：处理旧版 &nbsp; 语法
            result = result.Replace("&nbsp;", OneSpaceStr);

            return result;
        }

        /// <summary>
        /// 替换换行占位符 {br} 和 {br:N}
        /// </summary>
        /// <param name="input">输入字符串</param>
        /// <returns>替换后的字符串</returns>
        public static string ReplaceBreakPlaceholders(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            // 处理换行占位符 {br} 和 {br:N}
            return BreakPlaceholderRegex.Replace(input, match =>
            {
                // 如果没有捕获组或捕获组为空，默认为1个换行
                int count = match.Groups[1].Success && int.TryParse(match.Groups[1].Value, out int n) ? n : 1;
                return new string('\n', count);
            });
        }
        /// <summary>
        /// 一个空格字节
        /// </summary>
        public const char OneSpace = ' ';
        /// <summary>
        /// 一个空格字符串
        /// </summary>
        public const string OneSpaceStr = " ";
        /// <summary>
        /// 两个空格
        /// </summary>
        public static readonly string TwoSpaces = new(OneSpace, 2);
        /// <summary>
        /// 一个Tab对应的空格(4个空格)
        /// </summary>
        public static readonly string OneTabSpaces = new(OneSpace, 4);
        /// <summary>
        /// 2个Tab对应的空格(8个空格)
        /// </summary>
        public static readonly string TwoTabsSpaces = new(OneSpace, 8);
        /// <summary>
        /// 3个Tab对应的空格(12个空格)
        /// </summary>
        public static readonly string ThreeTabsSpaces = new(OneSpace, 12);
        /// <summary>
        /// 4个Tab对应的空格(16个空格)
        /// </summary>
        public static readonly string FourTabsSpaces = new(OneSpace, 16);
        /// <summary>
        /// 5个Tab对应的空格(20个空格)
        /// </summary>
        public static readonly string FiveTabsSpaces = new(OneSpace, 20);
        /// <summary>
        /// 6个Tab对应的空格(24个空格)
        /// </summary>
        public static readonly string SixTabsSpaces = new(OneSpace, 24);
        /// <summary>
        /// 7个Tab对应的空格(28个空格)
        /// </summary>
        public static readonly string SevenTabsSpaces = new(OneSpace, 28);
    }
}
