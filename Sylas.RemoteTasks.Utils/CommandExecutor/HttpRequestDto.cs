namespace Sylas.RemoteTasks.Utils.CommandExecutor
{
    /// <summary>
    /// http请求参数
    /// </summary>
    public class HttpRequestDto
    {
        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>
        /// Url地址
        /// </summary>
        public string Url { get; set; } = string.Empty;
        /// <summary>
        /// 请求头
        /// </summary>
        public string Headers { get; set; } = string.Empty;
        /// <summary>
        /// 请求方法
        /// </summary>
        public string Method { get; set; } = string.Empty;
        /// <summary>
        /// 请求的媒体类型
        /// </summary>
        public string ContentType { get; set; } = string.Empty;
        /// <summary>
        /// 请求体内容
        /// </summary>
        public string Body { get; set; } = string.Empty;
    }
}
