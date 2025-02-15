using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Sylas.RemoteTasks.Utils.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Utils
{
    /// <summary>
    /// 处理远程(HTTP)请求
    /// </summary>
    public static class RemoteHelpers
    {
        private static string GetQueryString(IDictionary<string, object>? queryDictionary)
        {
            if (queryDictionary is null || !queryDictionary.Any())
            {
                return string.Empty;
            }
            var queryStringBuilder = new StringBuilder();
            foreach (var parameterKeyVal in queryDictionary)
            {
                queryStringBuilder.Append($"{parameterKeyVal.Key}={parameterKeyVal.Value}&");
            }
            var queryString = queryStringBuilder.ToString().TrimEnd('&');
            return queryString;
        }
        /// <summary>
        /// 从Api接口获取所有数据
        /// </summary>
        /// <param name="requestConfig"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<object>?> FetchAllDataFromApiAsync(RequestConfig requestConfig)
        {
            // 备份原始配置对象, 用于递归发送新的请求时用 ...
            var originRequestConfig = MapHelper<RequestConfig, RequestConfig>.Map(requestConfig);

            ValidateHelper.ValidateArgumentIsNull(
                requestConfig,
                [
                    nameof(requestConfig.FailMsg),
                    nameof(requestConfig.Token),
                    nameof(requestConfig.QueryDictionary),
                    nameof(requestConfig.BodyDictionary),
                    nameof(requestConfig.ReturnPrimaryRequest),
                    nameof(requestConfig.UpdateBodyParentIdRegex),
                    nameof(requestConfig.UpdateBodyParentIdValue)
                ]);

            var data = await fetchDatasRecursivelyAsync(requestConfig);
            return data;

            async Task<IEnumerable<object>> fetchDatasRecursivelyAsync(RequestConfig config)
            {
                if (string.IsNullOrWhiteSpace(config.ResponseOkField) || string.IsNullOrWhiteSpace(config.ResponseDataField))
                {
                    throw new Exception($"ResponseOkField或者ResponseDataField为空");
                }
                bool responseOkPredicate(object response) => response.GetPropertyValue(config.ResponseOkField!)?.ToString() == config.ResponseOkValue;
                var responseDataFieldArr = config.ResponseDataField.Split(':', '.');
                IEnumerable<object>? getDataFunc(object response)
                {
                    object result = response;
                    foreach (var field in responseDataFieldArr)
                    {
                        if (result is not null && !string.IsNullOrWhiteSpace(field))
                        {
                            result = result.GetPropertyValue(field);
                        }
                    }
                    if (result is null)
                    {
                        return [];
                    }
                    else if (result is string)
                    {
                        throw new Exception(result.ToString());
                    }
                    else if (result is IEnumerable resultObj)
                    {
                        var resultList = resultObj.Cast<object>();
                        return resultList;
                    }
                    else if (result is JsonElement resultJsonEle)
                    {
                        if (resultJsonEle.ValueKind == JsonValueKind.Array)
                        {
                            return resultJsonEle.EnumerateArray().Select(x => x).Cast<object>();
                        }
                        else
                        {
                            return [resultJsonEle];
                        }
                    }
                    return [result];
                }

                using var httpClient = new HttpClient();
                var records = await FetchAllDataFromApiAsync(config.Url, config.FailMsg,
                                                                       config.QueryDictionary, config.PageIndexField, config.PageIndexParamInQuery, config.BodyDictionary,
                                                                       responseOkPredicate,
                                                                       getDataFunc,
                                                                       httpClient,
                                                                       config.IdFieldName, config.ParentIdFieldName,
                                                                       false,
                                                                       requestMethod: config.RequestMethod,
                                                                       updateBodyParentIdRegex: config.UpdateBodyParentIdRegex,
                                                                       updateBodyParentIdValue: config.UpdateBodyParentIdValue,
                                                                       authorizationHeaderToken: config.Token);
                return records;
            }
        }

        /// <summary>
        /// 通过API(分页)获取所有数据
        /// </summary>
        /// <param name="apiUrl"></param>
        /// <param name="errPrefix"></param>
        /// <param name="queryDictionary"></param>
        /// <param name="pageIndexParamName"></param>
        /// <param name="pageIndexParamInQuery"></param>
        /// <param name="requestBody"></param>
        /// <param name="responseOkPredicate"></param>
        /// <param name="getDataFunc"></param>
        /// <param name="httpClient"></param>
        /// <param name="idFieldName"></param>
        /// <param name="parentIdParamName"></param>
        /// <param name="parentIdParamInQuery"></param>
        /// <param name="requestMethod"></param>
        /// <param name="updateBodyParentIdRegex"></param>
        /// <param name="updateBodyParentIdValue"></param>
        /// <param name="authorizationHeaderToken"></param>
        /// <param name="mediaType"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task<List<object>> FetchAllDataFromApiAsync(
                string apiUrl,
                string errPrefix,
                IDictionary<string, object>?
                queryDictionary,
                string pageIndexParamName,
                bool pageIndexParamInQuery,
                IDictionary<string, object>? requestBody,
                Func<object, bool> responseOkPredicate,
                Func<object, object?> getDataFunc,
                HttpClient httpClient,
                string idFieldName,
                string parentIdParamName,
                bool parentIdParamInQuery,
                string requestMethod = "post",
                string updateBodyParentIdRegex = "",
                string updateBodyParentIdValue = "",
                string authorizationHeaderToken = "",
                string mediaType = "",
                ILogger? logger = null
            )
        {
            #region FormData还是application/json
            if (string.IsNullOrWhiteSpace(mediaType))
            {
                mediaType = "application/json";
            }
            #endregion

            #region Authorization请求头
            if (!string.IsNullOrWhiteSpace(authorizationHeaderToken))
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authorizationHeaderToken);
            }
            #endregion

            var queryString = (queryDictionary is null || !queryDictionary.Any()) ? string.Empty : GetQueryString(queryDictionary);
            #region 生成POST请求时body中的参数
            string bodyContent = "";

            // 如果请求方式是FormData/application/x-www-form-urlencoded, 那么body中的参数就跟QueryString一样: name=zhangsan&age=24, 
            // 如果请求方式是application/json, 那么body中的参数就是一个json字符串: { "name": "zhangsan", "age": 24 }, 
            bodyContent = mediaType == MediaType.FormuUrlEncoded
                    ? GetQueryString(requestBody)
                    : JsonConvert.SerializeObject(requestBody);
            #endregion


            var allDatas = new List<object>();
            // BOOKMARK: 子节点递归获取所有数据
            await FetchAllApiDataRecursivelyAsync();
            return allDatas;

            // **2. 子节点递归**
            async Task FetchAllApiDataRecursivelyAsync()
            {
                // BOOKMARK: 分页递归获取所有数据
                var records = await ApiChildrenDataAsync();
                if (records.Any())
                {
                    allDatas.AddRange(records);
                }
                if (string.IsNullOrWhiteSpace(parentIdParamName))
                {
                    return;
                }
                foreach (var record in records)
                {
                    if (record is JsonElement recordObj)
                    {
                        var idJE = recordObj.EnumerateObject().FirstOrDefault(x => x.Name.Equals(idFieldName, StringComparison.OrdinalIgnoreCase)).Value;
                        if (idJE.ValueKind == JsonValueKind.Undefined|| idJE.ValueKind == JsonValueKind.Null)
                        {
                            throw new Exception($"不存在主键{idFieldName}");
                        }

                        object id = idJE.ValueKind == JsonValueKind.Number ? idJE.GetInt64() : idJE.GetString() ?? throw new Exception($"id字段值异常:{idJE}");
                        // TODO: 根据parentIdParamName判断父级数据 更改为 关联数据会更为通用; "处理关联数据"部分的逻辑和参数名也需要替换
                        if (!string.IsNullOrWhiteSpace(parentIdParamName))
                        {
                            if (parentIdParamInQuery)
                            {
                                if (string.IsNullOrWhiteSpace(queryString))
                                {
                                    queryString = $"{parentIdParamName}={id}";
                                }
                                else if (!queryString.Contains(parentIdParamName))
                                {
                                    queryString += $"&{parentIdParamName}={id}";
                                }
                                else
                                {
                                    queryString = Regex.Replace(queryString, "(" + parentIdParamName + "=)\\s*\\w+", m => m.Groups[1].Value + id.ToString());
                                }
                            }
                            else
                            {
                                if (bodyContent.Contains(parentIdParamName))
                                {
                                    bodyContent = Regex.Replace(bodyContent, "\"" + parentIdParamName + "\":\\s*\"{0,1}(\\w+)", m => m.Groups[0].Value.Replace(m.Groups[1].Value, id.ToString()));
                                }
                                else
                                {
                                    // TODO: 处理关联数据
                                    if (string.IsNullOrWhiteSpace(updateBodyParentIdRegex))
                                    {
                                        bodyContent = bodyContent.TrimEnd('}') + $@",""{parentIdParamName}"":""{id}""}}";
                                    }
                                    else
                                    {
                                        bodyContent = string.IsNullOrWhiteSpace(updateBodyParentIdValue)
                                            ? Regex.Replace(bodyContent, updateBodyParentIdRegex, m => id.ToString())
                                            : Regex.Replace(bodyContent, updateBodyParentIdRegex, m => updateBodyParentIdValue.Replace("$parentId", id.ToString(), StringComparison.OrdinalIgnoreCase));
                                    }
                                }
                            }
                            await FetchAllApiDataRecursivelyAsync();
                        }
                    }
                }
                //return records;
            }

            // **1. 分页递归(根据父级Id获取所有子节点,可能有多页数据)**
            async Task<List<object>> ApiChildrenDataAsync()
            {
                List<object> allApiDatas = [];
                var pageIndex = 1;

                #region 请求方式和参数
                Func<Task<string>> queryAsync;
                if (string.Equals(requestMethod, "post", StringComparison.OrdinalIgnoreCase))
                {
                    queryAsync = async () =>
                    {
                        if (!string.IsNullOrWhiteSpace(pageIndexParamName))
                        {
                            if (pageIndexParamInQuery)
                            {
                                if (!queryString.Contains(pageIndexParamName, StringComparison.OrdinalIgnoreCase))
                                {
                                    queryString += string.IsNullOrWhiteSpace(queryString) ? $"&{pageIndexParamName}=1" : $"?{pageIndexParamName}=1";
                                }
                                queryString = Regex.Replace(queryString, pageIndexParamName + "=(\\d+)", m => m.Groups[0].Value.Replace(m.Groups[1].Value, pageIndex.ToString()), RegexOptions.IgnoreCase);
                            }
                            else
                            {
                                bodyContent = Regex.Replace(bodyContent, "\"" + pageIndexParamName + "\":\\s*\"{0,1}(\\w+)", m => m.Groups[0].Value.Replace(m.Groups[1].Value, pageIndex.ToString()));
                            }
                        }
                        logger?.LogCritical($"发送请求, body参数: {bodyContent}");
                        var parameters = new StringContent(bodyContent, Encoding.UTF8, mediaType);
                        var response = await httpClient.PostAsync($"{apiUrl}?{queryString}", parameters);
                        if (response.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            throw new Exception($"{errPrefix} {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
                        }
                        return await response.Content.ReadAsStringAsync();
                    };
                }
                else
                {
                    if (pageIndexParamInQuery)
                    {
                        queryString = Regex.Replace(queryString, "(" + pageIndexParamName + "=)\\s*\\w+", m => m.Groups[1].Value + pageIndex.ToString());
                    }
                    queryAsync = async () =>
                    {
                        var response = await httpClient.GetAsync(apiUrl + "?" + queryString);
                        if (!response.IsSuccessStatusCode)
                        {
                            throw new Exception(response.ReasonPhrase);
                        }
                        return await response.Content.ReadAsStringAsync();
                    };
                }
                #endregion

                await GetApiDataRecursivelyAsync();
                return allApiDatas;

                async Task GetApiDataRecursivelyAsync()
                {
                    // 开始发送请求
                    var responseContent = await queryAsync();

                    var responseObj = JsonDocument.Parse(responseContent).RootElement;
                    // 检查并获取响应数据
                    var data = GetData(responseObj, errPrefix, responseOkPredicate, getDataFunc);

                    if (data.Any())
                    {
                        allApiDatas.AddRange(data);
                    }
                    // 没有数据或者不分页时停止
                    if (!data.Any() || string.IsNullOrWhiteSpace(pageIndexParamName))
                    {
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(pageIndexParamName))
                    {
                        pageIndex++;
                        await GetApiDataRecursivelyAsync();
                    }
                }
            }
        }
        private static IEnumerable<object> GetData(object responseObj, string errPrefix, Func<object, bool> requestOkPredicate, Func<object, object?> getDataFunc)
        {
            if (responseObj is null)
            {
                throw new Exception($"{errPrefix}: API接口返回为空");
            }

            if (!requestOkPredicate(responseObj))
            {
                //TODO: 不直接获取数据返回数据, 返回源格式, 调用者自行处理, requestOkPredicate以及相关参数都不需要了
                throw new Exception($"{errPrefix}: 状态码与配置的ResponseOkValue值不一致{Environment.NewLine}{responseObj}");
            }

            var data = getDataFunc(responseObj) ?? throw new Exception($"{errPrefix}: API返回的数据为空");
            if (data is not IEnumerable<object> dataArr)
            {
                dataArr = [data];
            }
            if (dataArr is null)
            {
                throw new Exception($"{errPrefix}: API没有获取到任何数据");
            }
            return dataArr;
        }

        /// <summary>
        /// 通过AI模型的API获取答案
        /// </summary>
        /// <param name="question"></param>
        /// <param name="apiServer"></param>
        /// <param name="apiModel"></param>
        /// <param name="apiKey"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task<string> AskAiAsync(string question, string apiServer, string apiModel, string apiKey)
        {
            // 创建一个 HttpClient 对象
            using HttpClient client = new();
            //client.Timeout = TimeSpan.FromSeconds(180);
            client.Timeout = Timeout.InfiniteTimeSpan;
            // 设置请求头，包括 API 密钥和请求内容类型
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);

            // 设置请求内容，包括问题和 GPT 模型的名称
            // 设置请求内容类型为 application/json
            string requestBody = $$"""
                {
                  "model": "{{apiModel}}",
                  "max_tokens": 8192,
                  "messages": [
                    {
                      "role": "system",
                      "content": "You are a helpful assistant."
                    },
                    {
                      "role": "user",
                      "content": "{{question}}"
                    }
                  ],
                  "temperature": 0.7
                }
                """;
            HttpContent httpContent = new StringContent(requestBody, Encoding.UTF8, "application/json");

            // 发送 POST 请求并获取响应
            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync($"{apiServer.TrimEnd('/')}/v1/chat/completions", httpContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
            string responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Ai Response: {responseContent}{Environment.NewLine}");

            // 解析响应 JSON 并返回 GPT 的回答
            dynamic responseData = JsonConvert.DeserializeObject<dynamic>(responseContent) ?? throw new Exception("获取回答失败");
            string answer = responseData.choices[0].message.content;
            return answer;
        }
    }
    /// <summary>
    /// http请求的媒体类型
    /// </summary>
    public class MediaType
    {
        /// <summary>
        /// 媒体类型 - form-urlencoded
        /// </summary>
        public const string FormuUrlEncoded = "application/x-www-form-urlencoded";
        /// <summary>
        /// 媒体类型 - json
        /// </summary>
        public const string Json = "application/json";
    }
    /// <summary>
    /// Http请求参数配置
    /// </summary>
    public class RequestConfig
    {
        /// <summary>
        /// 请求地址
        /// </summary>
        public string Url { get; set; } = string.Empty;
        /// <summary>
        /// 分页页码字段
        /// </summary>
        public string PageIndexField { get; set; } = string.Empty;
        /// <summary>
        /// 分页每页多少条数据对应的字段
        /// </summary>
        public bool PageIndexParamInQuery { get; set; } = false;
        /// <summary>
        /// 数据的Id字段名
        /// </summary>
        public string IdFieldName { get; set; } = string.Empty;
        /// <summary>
        /// 父级数据的外键字段名称(如果有的话)
        /// </summary>
        public string ParentIdFieldName { get; set; } = string.Empty;
        /// <summary>
        /// querystring字典
        /// </summary>
        public IDictionary<string, object>? QueryDictionary { get; set; }
        /// <summary>
        /// 请求体中数据字典
        /// </summary>
        public IDictionary<string, object>? BodyDictionary { get; set; }
        /// <summary>
        /// 响应数据中表示成功的字段
        /// </summary>
        public string? ResponseOkField { get; set; }
        /// <summary>
        /// 响应json中表示成功的字段值是多少时表示成功
        /// </summary>
        public string? ResponseOkValue { get; set; }
        /// <summary>
        /// 响应json中数据所在的字段
        /// </summary>
        public string? ResponseDataField { get; set; }
        /// <summary>
        /// 请求失败时返回的信息
        /// </summary>
        public string FailMsg { get; set; } = string.Empty;
        /// <summary>
        /// 匹配ParentId的正则表达式
        /// </summary>
        public string UpdateBodyParentIdRegex { get; set; } = string.Empty;
        /// <summary>
        /// ParentId所要替换为的新值
        /// </summary>
        public string UpdateBodyParentIdValue { get; set; } = string.Empty;
        /// <summary>
        /// get or post
        /// </summary>
        public string RequestMethod { get; set; } = "GET";
        /// <summary>
        /// 请求的token
        /// </summary>
        public string Token { get; set; } = string.Empty;
        /// <summary>
        /// 递归回到主查询的请求, 基于当前数据字段值重新给请求参数赋值
        /// </summary>
        public string? ReturnPrimaryRequest { get; set; }
    }
}
