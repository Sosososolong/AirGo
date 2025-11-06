using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.Utils.Template;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

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
            if (command.Contains(nameof(MultiThreadsHttpRequestDto.ThreadVarsFile)) && command.Contains(nameof(MultiThreadsHttpRequestDto.Requests)))
            {
                MultiThreadsHttpRequestDto multiThreadsHttpRequestDto = JsonConvert.DeserializeObject<MultiThreadsHttpRequestDto>(command) ?? throw new Exception($"无法解析多线程Http请求命令:{command}");
                if (!string.IsNullOrWhiteSpace(multiThreadsHttpRequestDto.ThreadVarsFile))
                {
                    var lines = await File.ReadAllLinesAsync(multiThreadsHttpRequestDto.ThreadVarsFile);
                    if (lines.Length == 0)
                    {
                        yield return new CommandResult(false, $"线程变量文件{multiThreadsHttpRequestDto.ThreadVarsFile}为空");
                        yield break;
                    }
                    var headers = lines[0].Split(',', StringSplitOptions.RemoveEmptyEntries);
                    List<Dictionary<string, object>> datasource = [];
                    for (int i = 1; i < lines.Length; i++)
                    {
                        var dataRow = new Dictionary<string, object>();
                        var lineArr = lines[i].Split(',');
                        for (int j = 0; j < lineArr.Length; j++)
                        {
                            string field = headers[j];
                            dataRow[field] = lineArr[j];
                        }
                        datasource.Add(dataRow);
                    }

                    foreach (var threadVars in datasource)
                    {
                        // 按顺序执行请求, 如先获取token, 在获取userinfo, 业务数据
                        foreach (var requestDtos in multiThreadsHttpRequestDto.Requests)
                        {
                            #region 同一时间支持多个请求并行, 如userinfo和业务数据并行请求
                            List<Task> requestTasks = [];
                            foreach (var request in requestDtos)
                            {
                                // 解析请求中的变量
                                request.Url = TmplHelper.ResolveTemplate(request.Url, threadVars);
                                request.Headers = TmplHelper.ResolveTemplate(request.Headers, threadVars);
                                request.Body = TmplHelper.ResolveTemplate(request.Body, threadVars);
                                requestTasks.Add(SendRequestAsync(request, threadVars));
                            }
                            await Task.WhenAll(requestTasks);
                            #endregion
                        }
                    }
                }
            }
            else
            {
                HttpRequestDto requestDto = JsonConvert.DeserializeObject<HttpRequestDto>(command) ?? throw new Exception($"无法解析Http请求命令:{command}");
                yield return await SendRequestAsync(requestDto, []);
            }

            async Task<CommandResult> SendRequestAsync(HttpRequestDto dto, Dictionary<string, object> threadVars)
            {
                using var httpClient = httpClientFactory.CreateClient();
                var (statusCode, responseContent) = await RemoteHelpers.FetchAsync(httpClient, dto);

                CommandResult result;
                if (statusCode == System.Net.HttpStatusCode.OK)
                {
                    result = new CommandResult(statusCode == System.Net.HttpStatusCode.OK, responseContent);
                    if (!string.IsNullOrWhiteSpace(dto.ResponseExtractorsJson))
                    {
                        var extractors = JsonConvert.DeserializeObject<List<JsonExtractor>>(dto.ResponseExtractorsJson);
                        if (extractors is not null)
                        {
                            var responseObj = JsonConvert.DeserializeObject<JObject>(responseContent) ?? throw new Exception($"返序列化相应内容失败:{responseContent}");
                            JsonExtractor.ExtractVars(responseObj, extractors, threadVars);
                        }
                    }
                }
                {
                    result = new CommandResult(false, statusCode.ToString());
                }
                return result;
            }
        }
    }
}
