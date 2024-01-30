namespace Sylas.RemoteTasks.Utils.Template.Parser
{
    /// <summary>
    /// 转换结果
    /// </summary>
    public class ParseResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string[]? DataSourceKeys { get; set; }
        /// <summary>
        /// 值
        /// </summary>
        public object? Value { get; set; }
        /// <summary>
        /// 使用是否成功描述转换结果
        /// </summary>
        /// <param name="success"></param>
        public ParseResult(bool success)
        {
            Success = success;
        }
        /// <summary>
        /// 转换结果
        /// </summary>
        /// <param name="success"></param>
        /// <param name="sourceKey"></param>
        /// <param name="result"></param>
        public ParseResult(bool success, string[] sourceKey, object? result)
        {
            Success = success;
            DataSourceKeys = sourceKey;
            Value = result;
        }
    }
}
