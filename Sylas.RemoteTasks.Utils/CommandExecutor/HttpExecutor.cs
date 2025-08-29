using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Sylas.RemoteTasks.Utils.CommandExecutor
{
    /// <summary>
    /// 用于执行Http请求
    /// </summary>
    [Executor]
    public class HttpExecutor(IHttpClientFactory httpClientFactory) : ICommandExecutor
    {
        /// <summary>
        /// 发送一个的HTTP请求
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async IAsyncEnumerable<CommandResult> ExecuteAsync(string command)
        {
            HttpRequestDto requestDto = JsonConvert.DeserializeObject<HttpRequestDto>(command) ?? throw new Exception($"无法解析Http请求命令:{command}");
            using var httpClient = httpClientFactory.CreateClient();
            var (statusCode, responseContent) = await RemoteHelpers.FetchAsync(httpClient, requestDto);
            yield return new CommandResult(statusCode == System.Net.HttpStatusCode.OK, responseContent);
        }
    }
}
