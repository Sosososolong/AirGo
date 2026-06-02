namespace Sylas.RemoteTasks.Utils.CommandExecutor.Http.Models
{
    /// <summary>
    /// 校验结果
    /// </summary>
    public class ValidatorResult
    {
        /// <summary>
        /// 校验字段路径: code / status / statusCode / headers.X / JSON Path
        /// </summary>
        public string Field { get; set; } = string.Empty;

        /// <summary>
        /// 比较操作符: eq/ne/gt/lt/ge/le/contains/exists
        /// </summary>
        public string Op { get; set; } = "eq";

        /// <summary>
        /// 预期正确的值, 当Op为exists时, Expected表示字段是否存在(true/false)
        /// </summary>
        public string Expected { get; set; } = string.Empty;

        /// <summary>
        /// 实际值
        /// </summary>
        public string Actual { get; set; } = string.Empty;

        /// <summary>
        /// 是否通过校验
        /// </summary>
        public bool Passed { get; set; }

        /// <summary>
        /// 来源标记(用于结果回溯, 如 "collection"/"endpoint"/"command")
        /// </summary>
        public string Source { get; set; } = string.Empty;
    }
}
