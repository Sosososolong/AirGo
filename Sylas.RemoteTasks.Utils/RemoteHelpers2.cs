using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Sylas.RemoteTasks.Utils.Template;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text.Json;

namespace Sylas.RemoteTasks.Utils
{
    /// <summary>
    /// 网络请求工具类
    /// </summary>
    public static class RemoteHelpers2
    {
        private static string GetQueryString(IDictionary<string, object> queryDictionary)
        {
            var queryStringBuilder = new StringBuilder();
            foreach (var parameterKeyVal in queryDictionary)
            {
                queryStringBuilder.Append($"{parameterKeyVal.Key}={parameterKeyVal.Value}&");
            }
            var queryString = queryStringBuilder.ToString().TrimEnd('&');
            return queryString;
        }
        private static string GetQueryString(JsonElement queryObj)
        {
            var queryStringBuilder = new StringBuilder();
            foreach (var queryProp in queryObj.EnumerateObject())
            {
                queryStringBuilder.Append($"{queryProp.Name}={queryProp.Value}&");
            }
            var queryString = queryStringBuilder.ToString().TrimEnd('&');
            return queryString;
        }
        /// <summary>
        /// 从Api接口获取所有数据
        /// </summary>
        /// <param name="requestConfig"></param>
        /// <returns></returns>
        public static async IAsyncEnumerable<IEnumerable<JsonElement>?> FetchAllDataFromApiAsync(RequestConfig requestConfig)
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

            await foreach (var item in fetchDatasRecursivelyAsync(requestConfig))
            {
                yield return item;
            }

            async IAsyncEnumerable<IEnumerable<JsonElement>> fetchDatasRecursivelyAsync(RequestConfig config)
            {
                if (string.IsNullOrWhiteSpace(config.ResponseOkField) || string.IsNullOrWhiteSpace(config.ResponseDataField))
                {
                    throw new Exception($"ResponseOkField或者ResponseDataField为空");
                }
                bool responseOkPredicate(JsonElement response) => string.IsNullOrWhiteSpace(config.ResponseOkField) || response.GetPropertyIgnoreCase(config.ResponseOkField).ToString() == config.ResponseOkValue;
                var responseDataFieldArr = config.ResponseDataField.Split(':');
                JsonElement? getDataFunc(JsonElement response)
                {
                    return response.GetPropertyIgnoreCase(config.ResponseDataField);
                }

                using var httpClient = new HttpClient();
                await foreach(var records in FetchAllDataFromApiAsync(config.Url, config.FailMsg,
                                                                       config.QueryDictionary, config.PageIndexField, config.PageIndexParamInQuery, config.BodyDictionary,
                                                                       responseOkPredicate,
                                                                       getDataFunc,
                                                                       httpClient,
                                                                       config.IdFieldName, config.ParentIdFieldName,
                                                                       false,
                                                                       requestMethod: config.RequestMethod,
                                                                       updateBodyParentIdRegex: config.UpdateBodyParentIdRegex,
                                                                       updateBodyParentIdValue: config.UpdateBodyParentIdValue,
                                                                       authorizationHeaderToken: config.Token))
                {
                    yield return records;
                }
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
        /// <param name="bodyDictionary"></param>
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
        public static async IAsyncEnumerable<IEnumerable<JsonElement>> FetchAllDataFromApiAsync(
                string apiUrl,
                string errPrefix,
                IDictionary<string, object>? queryDictionary,
                string pageIndexParamName,
                bool pageIndexParamInQuery,
                object? bodyDictionary,
                Func<JsonElement, bool> responseOkPredicate,
                Func<JsonElement, JsonElement?> getDataFunc,
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
            if (bodyDictionary is not null)
            {
                if (mediaType == MediaType.FormuUrlEncoded)
                {
                    if (bodyDictionary is JsonElement bodyObj)
                    {
                        bodyContent = GetQueryString(bodyObj);
                    }
                    else
                    {
                        bodyContent = bodyDictionary.ToString();
                    }
                }
                else
                {
                    if (bodyDictionary is JObject)
                    {
                        bodyContent = bodyDictionary.ToString();
                    }
                    else if (bodyDictionary is JsonElement)
                    {
                        bodyContent = bodyDictionary.ToString();
                    }
                    else
                    {
                        bodyContent = JsonConvert.SerializeObject(bodyDictionary);
                    }
                }
            }
            #endregion


            var allDatas = new List<JToken>();
            await foreach(var item in StartFetchAllDataFromApiAsync())
            {
                yield return item;
            }

            // C -> yield return -> ... -> pageindex++ -> C

            // B1                                              B1-> foreach records; pid=record.pid(2) -> ...
            // C1(page 1, pid 1) -> yield return records -> ...             

            //                                                    B1-> foreach records; pid=record.pid(3) -> ...
            // -> C2(page 1, pid 2) -> yield reutrn records -> ...

            //                                                     B1-> foreach records; pid=record.pid(4) -> ...
            // --> C3(page 1, pid 3) -> yield return records -> ...

            //                                                     B1-> foreach空集合 -> C4 END
            // --> C4(page 1, pid 4) -> empty -> yield break -> ...

            // --> C3(page2, pid 3) -> yield return records -> ...

            // 1 开始: 使用分页条件和ParentId条件请求数据
            async IAsyncEnumerable<IEnumerable<JsonElement>> StartFetchAllDataFromApiAsync()
            {
                // BOOKMARK: FetchAllDataFromApiAsync - 1. 请求分页数据(递归)
                await foreach (var records in FetchAllPagedDataFromApiAsync())
                {
                    if (records.Any())
                    {
                        yield return records;
                    }
                    //if (string.IsNullOrWhiteSpace(parentIdParamName))
                    //{
                    //    yield break;
                    //}

                    // BOOKMARK: FetchAllDataFromApiAsync - 2 请求子节点数据(递归)
                    if (!string.IsNullOrWhiteSpace(parentIdParamName))
                    {
                        foreach (var record in records)
                        {
                            var id = record.GetPropertyIgnoreCase(idFieldName);
                            // TODO: 根据parentIdParamName判断父级数据 更改为 关联数据会更为通用; "处理关联数据"部分的逻辑和参数名也需要替换
                            #region 更新parentId参数值, 继续处理当前pageIndex所有的子数据
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
                            await foreach (var item in StartFetchAllDataFromApiAsync())
                            {
                                yield return item;
                            }
                            #endregion
                        }
                    }
                }
            }

            async IAsyncEnumerable<IEnumerable<JsonElement>> FetchAllPagedDataFromApiAsync()
            {
                var pageIndex = 1;

                #region 2. 更新pageIndex参数值, 准备好发送HTTP请求
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

                // xx
                // BOOKMARK: FetchAllDataFromApiAsync - 递归 99 (分页)获取所有数据 - 调用
                await foreach (var item in GetApiDataRecursivelyAsync())
                {
                    yield return item;
                }
                //xx
                async IAsyncEnumerable<IEnumerable<JsonElement>> GetApiDataRecursivelyAsync()
                {
                    // 开始发送请求
                    var responseContent = await queryAsync();

                    JsonDocument doc = JsonDocument.Parse(responseContent);
                    var root = doc.RootElement;

                    // 检查并获取响应数据
                    var data = GetData(root, errPrefix, responseOkPredicate, getDataFunc);

                    if (data.ValueKind == JsonValueKind.Null)
                    {
                        LoggerHelper.LogCritical($"API接口返回数据为空, 停止查询");
                        yield break;
                    }
                    // 没有数据或者不分页时停止
                    else if ((data.ValueKind == JsonValueKind.Array && !data.EnumerateArray().Any()))
                    {
                        LoggerHelper.LogCritical($"API接口返回数据为空, 停止查询");
                        yield break;
                    }
                    else if (string.IsNullOrWhiteSpace(pageIndexParamName))
                    {
                        LoggerHelper.LogCritical($"API接口无需分页, 返回数据并停止查询");
                        yield return data.GetElements();
                        yield break;
                    }
                    else if (data.ValueKind == JsonValueKind.Object)
                    {
                        LoggerHelper.LogCritical($"API接口返回单个数据对象并非数据集合, 返回数据并停止查询");
                        IEnumerable<JsonElement> result = [data];
                        var arr = data.EnumerateArray();
                        yield return data.GetElements();
                        yield break;
                    }
                    else
                    {
                        LoggerHelper.LogInformation($"API接口成功获取第{pageIndex}页数据");
                        yield return data.GetElements();

                        pageIndex++;
                        LoggerHelper.LogInformation($"递归获取下一页({pageIndex})数据");
                        await foreach (var item in GetApiDataRecursivelyAsync())
                        {
                            yield return item;
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 消除JsonElement的ValueKind为对象数组和单个对象的数据差异, 统一返回JsonElement的可迭代集合
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        static IEnumerable<JsonElement> GetElements(this JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    yield return item;
                }
            }
            else
            {
                yield return element;
            }
        }
        private static JsonElement GetProperty(this JsonElement element, string path)
        {
            string[] pathSegments = path.Split(':', '.');
            JsonElement currentElement = element;

            foreach (string segment in pathSegments)
            {
                if (currentElement.TryGetProperty(segment, out JsonElement nextElement))
                {
                    currentElement = nextElement;
                }
                else
                {
                    throw new InvalidOperationException($"Path segment '{segment}' not found in JSON.");
                }
            }

            return currentElement;
        }
        static JsonElement GetPropertyIgnoreCase(this JsonElement element, string propertyPath)
        {
            string[] properties = propertyPath.Split(':', '.');
            JsonElement result = element;
            foreach (var propertyName in properties)
            {
                if (result.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in result.EnumerateObject())
                    {
                        if (propertyName.Equals(prop.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            result = prop.Value;
                        }
                        else
                        {
                            throw new KeyNotFoundException($"Property '{propertyName}' not found.");
                        }
                    }
                }
                else if (result.ValueKind == JsonValueKind.Array && int.TryParse(propertyName, out int index))
                {
                    result = result[index];
                }
                else
                {
                    throw new Exception($"属性{propertyName}既不是对象属性也不是集合的子项.");
                }
            }
            return result;
        }
        private static JsonElement GetData(JsonElement responseObj, string errPrefix, Func<JsonElement, bool> requestOkPredicate, Func<JsonElement, JsonElement?> getDataFunc)
        {
            if (responseObj.ValueKind == JsonValueKind.Null)
            {
                throw new Exception($"{errPrefix}: API接口返回为空");
            }

            if (!requestOkPredicate(responseObj))
            {
                //TODO: 不直接获取数据返回数据, 返回源格式, 调用者自行处理, requestOkPredicate以及相关参数都不需要了
                throw new Exception($"{errPrefix}: 状态码与配置的ResponseOkValue值不一致{Environment.NewLine}{JsonConvert.SerializeObject(responseObj)}");
            }

            var data = getDataFunc(responseObj) ?? throw new Exception($"{errPrefix}: API返回的数据为空");

            return data;
        }
    }
}
