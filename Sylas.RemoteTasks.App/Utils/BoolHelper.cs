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
        public static bool StringValueNotEquals(this object? sourceValue, object? targetValue) =>
            ((sourceValue is not null && !string.IsNullOrWhiteSpace(sourceValue.ToString())) || (targetValue is not null && !string.IsNullOrWhiteSpace(targetValue.ToString()))) && sourceValue?.ToString() != targetValue?.ToString();

        /// <summary>
        /// 比较两个值类型
        /// </summary>
        /// <param name="sourceValue"></param>
        /// <param name="targetValue"></param>
        /// <returns></returns>
        public static bool StringValueEquals(this object? sourceValue, object? targetValue) =>
            (string.IsNullOrWhiteSpace(sourceValue?.ToString()) && string.IsNullOrWhiteSpace(targetValue?.ToString())) || sourceValue?.ToString() == targetValue?.ToString();
    }
}
