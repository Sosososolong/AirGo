namespace Sylas.RemoteTasks.App.Utils
{
    public static class BoolHelper
    {
        /// <summary>
        /// 比较两个值类型
        /// </summary>
        /// <param name="sourceValue"></param>
        /// <param name="targetValue"></param>
        /// <returns></returns>
        public static bool StringValueNotEquals(this object? sourceValue, object? targetValue, StringComparison comparisonType = StringComparison.Ordinal) => !string.Equals($"{sourceValue}", $"{targetValue}", comparisonType);

        /// <summary>
        /// 比较两个值类型
        /// </summary>
        /// <param name="sourceValue"></param>
        /// <param name="targetValue"></param>
        /// <returns></returns>
        public static bool StringValueEquals(this object? sourceValue, object? targetValue, StringComparison comparisonType = StringComparison.Ordinal) => string.Equals($"{sourceValue}", $"{targetValue}", comparisonType);
    }
}
