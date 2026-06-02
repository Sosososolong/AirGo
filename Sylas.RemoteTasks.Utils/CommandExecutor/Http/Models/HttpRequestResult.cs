using System.Collections.Generic;

namespace Sylas.RemoteTasks.Utils.CommandExecutor.Http.Models
{
    /// <summary>
    /// HTTP请求结果
    /// </summary>
    public class HttpRequestResult
    {
        /// <summary>
        /// 响应HTTP状态码, 如200, 404等
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// 响应HTTP状态码 文本, 如 OK, Not Found等
        /// </summary>
        public string StatusText { get; set; } = "OK";

        /// <summary>
        /// 响应头信息
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = [];

        /// <summary>
        /// 响应体
        /// </summary>
        public string Body { get; set; } = string.Empty;

        /// <summary>
        /// 响应体大小, 单位字节
        /// </summary>
        public long Size { get; set; } = 0;

        /// <summary>
        /// HTTP请求耗时, 单位毫秒
        /// </summary>
        public int DurationMs { get; set; } = 0;

        /// <summary>
        /// HTTP状态码非2xx时的错误信息
        /// </summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// 响应结果验证器列表, 验证请求是否成功
        /// </summary>
        public List<ValidatorResult> ValidatorResults { get; set; } = [];

        /// <summary>
        /// 响应数据中提取的变量列表, 供后续HTTP请求或者其他数据操作使用
        /// </summary>
        public List<ExtractedVar> ExtractedVars { get; set; } = [];

        /// <summary>
        /// 发送时实际的 url(含 query 参数)
        /// </summary>
        public string FinalUrl { get; set; } = string.Empty;

        /// <summary>
        /// 发送时实际的 body(模板已替换)
        /// </summary>
        public string FinalBody { get; set; } = string.Empty;

        /// <summary>
        /// 发送时实际的 headers(已合并 auth)
        /// </summary>
        public List<KvPair> FinalHeaders { get; set; } = [];
    }
}
