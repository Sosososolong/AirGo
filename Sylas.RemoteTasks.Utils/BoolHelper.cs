using System;

namespace Sylas.RemoteTasks.Utils
{
    /// <summary>
    /// 比较值大小帮助类
    /// </summary>
    public static class BoolHelper
    {
        /// <summary>
        /// 比较两个值类型
        /// </summary>
        /// <param name="sourceValue"></param>
        /// <param name="targetValue"></param>
        /// <param name="comparisonType"></param>
        /// <returns></returns>
        public static bool StringValueNotEquals(this object? sourceValue, object? targetValue, StringComparison comparisonType = StringComparison.Ordinal) => !string.Equals($"{sourceValue}", $"{targetValue}", comparisonType);

        /// <summary>
        /// 比较两个值类型
        /// </summary>
        /// <param name="sourceValue"></param>
        /// <param name="targetValue"></param>
        /// <param name="comparisonType"></param>
        /// <returns></returns>
        public static bool StringValueEquals(this object? sourceValue, object? targetValue, StringComparison comparisonType = StringComparison.Ordinal) => string.Equals($"{sourceValue}", $"{targetValue}", comparisonType);
    }
}
