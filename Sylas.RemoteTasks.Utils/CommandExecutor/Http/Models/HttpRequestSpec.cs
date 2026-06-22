using System.Collections.Generic;

namespace Sylas.RemoteTasks.Utils.CommandExecutor.Http.Models
{
    /// <summary>
    /// 共享层统一的请求描述(替代 HttpRequestDto/SendRequestDto 直传 pipeline)
    /// </summary>
    public class HttpRequestSpec
    {
        /// <summary>
        /// HTTP方法, 如 GET/POST/PUT/DELETE 等, 默认 GET
        /// </summary>
        public string Method { get; set; } = "GET";
        /// <summary>
        /// 已经拼好 BaseUrl 的 Url 模板(可含 {{var}}/${var}/$var)
        /// </summary>
        public string Url { get; set; } = string.Empty;
        /// <summary>
        /// Query 参数(将被 EscapeDataString 后拼接)
        /// </summary>
        public List<KvPair> QueryParams { get; set; } = [];
        /// <summary>
        /// 请求头
        /// </summary>
        public List<KvPair> Headers { get; set; } = [];
        /// <summary>
        /// 请求体类型
        /// </summary>
        public BodyKind BodyKind { get; set; } = BodyKind.None;
        /// <summary>
        /// 请求体内容, 具体取决于 BodyKind 的值
        /// </summary>
        public string Body { get; set; } = string.Empty;
        /// <summary>
        /// 授权方式
        /// </summary>
        public AuthSpec? Auth { get; set; }
        /// <summary>
        /// 响应结果验证器列表
        /// </summary>
        public List<ValidatorSpec> Validators { get; set; } = [];
        /// <summary>
        /// 响应数据提取器列表
        /// </summary>
        public List<ExtractorSpec> Extractors { get; set; } = [];
        /// <summary>
        /// 请求超时(秒), 0 = 默认60s, -1 = 无限超时(适用于AI/长耗时请求)
        /// </summary>
        public int TimeoutSeconds { get; set; }
        /// <summary>
        /// 变量上下文(模板解析用), 不为 null 时 pipeline 会做模板替换
        /// </summary>
        public Dictionary<string, object?> VariableContext { get; set; } = [];
    }
}
