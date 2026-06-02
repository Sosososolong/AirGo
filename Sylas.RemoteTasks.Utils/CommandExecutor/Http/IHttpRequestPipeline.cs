using Sylas.RemoteTasks.Utils.CommandExecutor.Http.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Utils.CommandExecutor.Http
{
    /// <summary>
    /// HTTP 请求执行管道
    /// 责任: 发送HTTP请求, 包括模板解析 → Auth 应用 → Body 构建 → 发送 → 响应提取 → 校验
    /// </summary>
    public interface IHttpRequestPipeline
    {
        /// <summary>
        /// 执行一次 HTTP 请求
        /// </summary>
        Task<HttpRequestResult> SendAsync(HttpRequestSpec spec, CancellationToken cancellationToken = default);
    }
}
