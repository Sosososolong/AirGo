using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.App.Utils
{
    public static class RemoteHelpers
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

        public static async Task<RequestConfig> FetchAllDataFromApiAsync(RequestConfig requestConfig)
        {
            ValidateHelper.ValidateArgumentIsNull(requestConfig, new List<string> { nameof(requestConfig.FailMsg), nameof(requestConfig.Token) });

            NodesHelper.FillChildrenValue(requestConfig, nameof(requestConfig.Details));


            await fetchDataModelsRecursivelyAsync(requestConfig);
            return requestConfig;

            async Task fetchDataModelsRecursivelyAsync(RequestConfig config)
            {
                Func<JObject, bool> responseOkPredicate = response => response[config.ResponseOkField]?.ToString() == config.ResponseOkValue;
                var responseDataFieldArr = config.ResponseDataField.Split(':');
                Func<JObject, JToken?> getDataFunc = response =>
                {
                    JToken? result = response;
                    foreach (var field in responseDataFieldArr)
                    {
                        if (result is not null && !string.IsNullOrWhiteSpace(field))
                        {
                            result = result[field];
                        }
                    }
                    return result ?? new JArray();
                };

                var records = await RemoteHelpers.FetchAllDataFromApiAsync(config.Url, config.FailMsg,
                                                                       config.QueryDictionary, config.PageIndexField, config.PageIndexParamInQuery, config.BodyDictionary,
                                                                       responseOkPredicate,
                                                                       getDataFunc,
                                                                       new HttpClient(),
                                                                       config.IdFieldName, config.ParentIdFieldName, false,
                                                                       config.Token);
                config.Data = records;
                if (config.Details is not null && config.Details.Any())
                {
                    foreach (var record in records)
                    {
                        foreach (var detail in config.Details)
                        {
                            await fetchDataModelsRecursivelyAsync(detail);
                        }
                    }
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
        /// <param name="authorizationHeaderToken"></param>
        /// <param name="mediaType"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task<List<JToken>> FetchAllDataFromApiAsync(string apiUrl, string errPrefix, IDictionary<string, object>? queryDictionary, string pageIndexParamName, bool pageIndexParamInQuery, IDictionary<string, object>? bodyDictionary, Func<JObject, bool> responseOkPredicate, Func<JObject, JToken?> getDataFunc, HttpClient httpClient, string idFieldName, string parentIdParamName, bool parentIdParamInQuery, string authorizationHeaderToken = "", string mediaType = "", ILogger logger = null)
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
            bodyDictionary ??= new Dictionary<string, object>();

            // 如果请求方式是FormData/application/x-www-form-urlencoded, 那么body中的参数就跟QueryString一样: name=zhangsan&age=24, 
            // 如果请求方式是application/json, 那么body中的参数就是一个json字符串: { "name": "zhangsan", "age": 24 }, 
            if (bodyDictionary.Any())
            {
                if (mediaType == MediaType.FormuUrlencoded)
                {
                    bodyContent = GetQueryString(bodyDictionary);
                }
                else
                {
                    bodyContent = JsonConvert.SerializeObject(bodyDictionary);
                }
            }
            #endregion


            var allDatas = new List<JToken>();
            await FetchAllApiDataRecursivelyAsync();
            return allDatas;

            // **2. 子节点递归**
            async Task FetchAllApiDataRecursivelyAsync()
            {
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
                    if (record is JObject recordObj)
                    {
                        var id = recordObj[idFieldName];
                        if (id is null)
                        {
                            throw new Exception($"不存在主键{idFieldName}");
                        }
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
                                    bodyContent = bodyContent.TrimEnd('}') + $@",""{parentIdParamName}"":""{id}""}}";
                                }
                            }
                        }
                        await FetchAllApiDataRecursivelyAsync();
                    }
                }
                //return records;
            }

            // **1. 分页递归(根据父级Id获取所有子节点,可能有多页数据)**
            async Task<List<JToken>> ApiChildrenDataAsync()
            {
                List<JToken> allApiDatas = new();
                var pageIndex = 1;

                #region 请求方式和参数
                Func<Task<string>> queryAsync;
                if (bodyDictionary is not null && bodyDictionary.Any())
                {
                    queryAsync = async () =>
                    {
                        if (!string.IsNullOrWhiteSpace(pageIndexParamName))
                        {
                            if (pageIndexParamInQuery)
                            {
                                queryString = Regex.Replace(queryString, pageIndexParamName + "=(\\d+)", m => m.Groups[0].Value.Replace(m.Groups[1].Value, pageIndex.ToString()));
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
                            throw new Exception($"{errPrefix} {response.ReasonPhrase}");
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
                    queryAsync = async () => await (await httpClient.GetAsync(apiUrl + queryString)).Content.ReadAsStringAsync();
                }
                #endregion

                await GetApiDataRecursivelyAsync();
                return allApiDatas;

                async Task GetApiDataRecursivelyAsync()
                {

                    var responseContent = await queryAsync();

                    var responseObj = JsonConvert.DeserializeObject<JObject>(responseContent);

                    if (responseObj is null)
                    {
                        throw new Exception($"返回数据不是一个对象: {responseContent}");
                    }
                    // 检查并获取响应数据
                    var data = GetData(responseObj, errPrefix, responseOkPredicate, getDataFunc);

                    // 没有数据或者分页时停止
                    if (!data.Any() || string.IsNullOrWhiteSpace(pageIndexParamName))
                    {
                        return;
                    }
                    allApiDatas.AddRange(data);

                    if (!string.IsNullOrWhiteSpace(pageIndexParamName))
                    {
                        pageIndex++;
                        await GetApiDataRecursivelyAsync();
                    }
                }
            }
        }
        private static JToken GetData(JObject responseObj, string errPrefix, Func<JObject, bool> requestOkPredicate, Func<JObject, JToken?> getDataFunc)
        {
            if (responseObj is null)
            {
                throw new Exception($"{errPrefix}: API接口返回为空");
            }

            if (!requestOkPredicate(responseObj))
            {
                throw new Exception($"{errPrefix}: {JsonConvert.SerializeObject(responseObj)}");
            }

            var data = getDataFunc(responseObj);
            if (data is null)
            {
                throw new Exception($"{errPrefix}: API返回的数据为空");
            }
            if (data is not JArray dataArr)
            {
                throw new Exception($"{errPrefix}: API返回的数据不是一个对象数组");
            }
            if (dataArr is null)
            {
                throw new Exception($"{errPrefix}: API没有获取到任何数据");
            }
            return data;
        }
    }
    public class MediaType
    {
        public const string FormuUrlencoded = "application/x-www-form-urlencoded";
        public const string Json = "application/json";
    }

    public class RequestConfig
    {
        public string? Url { get; set; }
        public string? PageIndexField { get; set; }
        public bool PageIndexParamInQuery { get; set; }
        public string? IdFieldName { get; set; }
        public string? ParentIdFieldName { get; set; }
        public IDictionary<string, object>? QueryDictionary { get; set; }
        public IDictionary<string, object>? BodyDictionary { get; set; }
        public string? ResponseOkField { get; set; }
        public string? ResponseOkValue { get; set; }
        public string? ResponseDataField { get; set; }
        public string? FailMsg { get; set; }
        public string? Token { get; set; }
        /// <summary>
        /// 请求获取的数据
        /// </summary>
        public IEnumerable<JToken>? Data { get; set; }
        /// <summary>
        /// 根据当前数据的值去查询关联的明细数据(外键表数据)
        /// </summary>
        public List<RequestConfig>? Details { get; set; }
    }
}
