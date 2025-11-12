using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.Common;
using Sylas.RemoteTasks.Common.Extensions;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils.Template;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
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
                #region 多线程执行一系列HTTP请求, 压力测试场景
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
                                request.Headers = request.Headers.Select(h => TmplHelper.ResolveTemplate(h, threadVars)).ToArray();
                                request.Body = TmplHelper.ResolveTemplate(request.Body, threadVars);
                                requestTasks.Add(SendRequestAsync(request, threadVars));
                            }
                            await Task.WhenAll(requestTasks);
                            #endregion
                        }
                    }
                }
                else
                {
                    throw new Exception($"缺少变量文件, 用来初始化每个线程的变量");
                }
                #endregion
            }
            else
            {
                command = command.Trim();
                if (command.StartsWith('{') && command.EndsWith('}'))
                {
                    // 发送一个HTTP请求, 适合简单场景
                    HttpRequestDto requestDto = JsonConvert.DeserializeObject<HttpRequestDto>(command) ?? throw new Exception($"无法解析Http请求命令:{command}");
                    yield return await SendRequestAsync(requestDto, []);
                }
                else
                {
                    // 发送一系列HTTP请求, 并处理HTTP请求的结果, 可以通过一系列接口调用实现复杂功能
                    var flowResults = ExecuteFetchFlowsAsync(command);
                    await foreach (var res in flowResults)
                    {
                        yield return res;
                    }
                }
            }
        }
        /// <summary>
        /// 发送一个HTTP请求
        /// </summary>
        /// <param name="dto"></param>
        /// <param name="threadVars"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        async Task<CommandResult> SendRequestAsync(HttpRequestDto dto, Dictionary<string, object> threadVars)
        {
            using var httpClient = httpClientFactory.CreateClient();
            var (statusCode, responseContent) = await RemoteHelpers.FetchAsync(httpClient, dto);

            CommandResult result;
            if (statusCode == System.Net.HttpStatusCode.OK)
            {
                if (!Regex.IsMatch(responseContent, dto.IsSuccessPattern))
                {
                    result = new CommandResult(false, responseContent);
                }
                else
                {
                    result = new CommandResult(statusCode == System.Net.HttpStatusCode.OK, responseContent);
                    if (!string.IsNullOrWhiteSpace(dto.ResponseExtractors))
                    {
                        var responseObj = JsonConvert.DeserializeObject<JToken>(responseContent) ?? throw new Exception($"返序列化相应内容失败:{responseContent}");
                        if (threadVars is not null && threadVars.Count > 0)
                        {
                            TmplHelper2.ResolveExtractors(dto.ResponseExtractors, threadVars);
                        }
                    }
                }
            }
            else
            {
                result = new CommandResult(false, statusCode.ToString());
            }
            return result;
        }
        /// <summary>
        /// 执行一系列HTTP请求流程
        /// </summary>
        /// <param name="configContent"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        /// <exception cref="NotImplementedException"></exception>
        async IAsyncEnumerable<CommandResult> ExecuteFetchFlowsAsync(string configContent)
        {
            var flow = configContent.ToObjectByKeyValuesContent<HttpRequestsFlowConfig>() ?? throw new Exception($"无法解析Http请求流程配置:{configContent}");
            var envVars = JsonConvert.DeserializeObject<Dictionary<string, object>>(flow.EnvVars) ?? [];
            var matches = Regex.Matches(flow.HttpRequestDtosJson, @"\[(?<x>[^\[\]]*(((?'Open'\[)[^\[\]]*)+((?'-Open'\])[^\[\]]*)+)*)\]");
            string[] configsFlow = matches.Select(x => x.Value).ToArray();
            foreach (var requestsConfigJson in configsFlow)
            {
                string resolvedConfigsJson = TmplHelper2.ResolveTmpl(requestsConfigJson, envVars, true);
                List<HttpRequestDto> configs = JsonConvert.DeserializeObject<List<HttpRequestDto>>(resolvedConfigsJson) ?? throw new Exception($"无效的请http请求配置:{resolvedConfigsJson}");
                foreach (var fetchConfig in configs)
                {
                    if (string.IsNullOrWhiteSpace(fetchConfig.IsSuccessPattern))
                    {
                        yield return new CommandResult(false, $"{fetchConfig.Url}{Environment.NewLine}Http请求没有配置是否成功模板");
                        yield break;
                    }
                    // 解析请求中的模板变量(可能有依赖于当前响应数据$data的不会解析)
                    fetchConfig.Url = TmplHelper2.ResolveTmpl(fetchConfig.Url, envVars, true);
                    fetchConfig.Body = TmplHelper2.ResolveTmpl(fetchConfig.Body, envVars, true);

                    // 发送HTTP请求
                    yield return new CommandResult(true, $"{fetchConfig.Url}");
                    yield return new CommandResult(true, $"开始请求");
                    var data = await SendRequestAsync(fetchConfig, []);
                    if (!data.Succeed)
                    {
                        LoggerHelper.LogError($"请求失败:{data.Message}; Url:{fetchConfig.Url}");
                        yield return new CommandResult(false, $"{fetchConfig.Url}{Environment.NewLine}{data.Message}");
                        yield break;
                    }
                    if (!Regex.IsMatch(data.Message, fetchConfig.IsSuccessPattern))
                    {
                        yield return new CommandResult(false, $"{fetchConfig.Url}{Environment.NewLine}结果不匹配成功模板{fetchConfig.IsSuccessPattern}");
                        yield break;
                    }
                    yield return new CommandResult(true, $"请求结束");

                    var responseData = TmplHelper2.ResolveExpression(fetchConfig.ResponseDataPropty, JsonConvert.DeserializeObject<JObject>(data.Message)).Item2 ?? throw new Exception($"响应对象中未找到数据属性{fetchConfig.ResponseDataPropty}");
                    // 添加响应数据data, 使得Extractors和Handlers可以解析$data相关模板变量(处理完毕移除data, 避免影响其他请求的$data会被提前错误地解析)
                    envVars["data"] = responseData;
                    // 从HTTP请求响应中提取数据; 
                    TmplHelper2.ResolveExtractors(fetchConfig.ResponseExtractors, envVars);
                    yield return new CommandResult(true, $"提取数据结束");
                    // 处理HTTP响应数据
                    if (fetchConfig.DataHandlers is not null && fetchConfig.DataHandlers.Length > 0)
                    {
                        foreach (var handler in fetchConfig.DataHandlers)
                        {
                            if (string.IsNullOrWhiteSpace(handler))
                            {
                                throw new Exception("数据处理器异常");
                            }
                            var nameLength = handler.IndexOf('(');
                            var handlerName = handler[..nameLength];
                            var handlerParamArr = handler[nameLength..].TrimStart('(').TrimEnd(')').Split(',');
                            if (handlerParamArr.Length < 4)
                            {
                                throw new Exception("TransferData函数参数不足");
                            }
                            if (handlerName.Equals(nameof(DatabaseInfo.TransferDataAsync), StringComparison.OrdinalIgnoreCase))
                            {
                                // 保存数据到数据库
                                string dataName = handlerParamArr[0].Trim().TrimStart('$');
                                var targetData = envVars.GetPropertyValue(dataName) as JArray;
                                var connStr = handlerParamArr[1].Trim();
                                if (connStr.StartsWith('$'))
                                {
                                    connStr = envVars.GetPropertyValue(connStr.TrimStart('$')).ToString();
                                }
                                var table = handlerParamArr[2].Trim();
                                var ignoreFieldsStatement = handlerParamArr[3].Trim();
                                string[]? ignoreFieldsParamVal = ignoreFieldsStatement.StartsWith("[") ? JsonConvert.DeserializeObject<string[]>(ignoreFieldsStatement) : ignoreFieldsStatement.Split(',');
                                string errMsg = string.Empty;
                                try
                                {
                                    await DatabaseInfo.TransferDataAsync([.. targetData], connStr, table, ignoreFieldsParamVal: ignoreFieldsParamVal);
                                }
                                catch (Exception ex)
                                {
                                    errMsg = ex.Message;
                                }
                                if (!string.IsNullOrWhiteSpace(errMsg))
                                {
                                    yield return new CommandResult(false, $"保存数据到表{table}失败: {errMsg}");
                                    yield break;
                                }
                                yield return new CommandResult(true, $"保存数据到表{table}成功!");
                            }
                            else
                            {
                                throw new NotImplementedException(handlerName);
                            }
                        }
                    }
                    // handler处理完毕, envVars中移除data, 如果后需要data, 可以使用extractor表达式保存到其他变量中
                    if (envVars.ContainsKey("data"))
                    {
                        envVars.Remove("data");
                    }
                }
            }
        }
    }
}
