namespace Sylas.RemoteTasks.Utils.CommandExecutor
{
    /// <summary>
    /// 表示有先后顺序的一系列HTTP请求
    /// </summary>
    public class HttpRequestsFlowConfig
    {
        /// <summary>
        /// 环境变量, 对象的Json字符串格式, 用于存储一些公共的变量, 供所有请求使用
        /// </summary>
        public string EnvVars { get; set; } = string.Empty;
        /// <summary>
        /// 一系列Http请求内容, 包含了几个阶段的Http请求, 每个阶段的请求内容包含模板表达式, 解析后生成的是HttpRequestDto集合对象, 表示一些列按顺序执行的HTTP请求
        /// </summary>
        public string HttpRequestDtosJson { get; set; } = string.Empty;
    }
}
