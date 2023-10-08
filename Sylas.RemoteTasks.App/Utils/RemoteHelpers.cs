using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.Utils.Template;
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
        private static string GetQueryString(JObject queryObj)
        {
            var queryStringBuilder = new StringBuilder();
            foreach (var queryProp in queryObj.Properties())
            {
                queryStringBuilder.Append($"{queryProp.Name}={queryProp.Value}&");
            }
            var queryString = queryStringBuilder.ToString().TrimEnd('&');
            return queryString;
        }

        public static async Task<IEnumerable<JToken>?> FetchAllDataFromApiAsync(RequestConfig requestConfig)
        {
            // 备份原始配置对象, 用于递归发送新的请求时用 ...
            var originRequestConfig = MapHelper<RequestConfig, RequestConfig>.Map(requestConfig);

            ValidateHelper.ValidateArgumentIsNull(
                requestConfig,
                new List<string> { 
                    nameof(requestConfig.FailMsg),
                    nameof(requestConfig.Token),
                    nameof(requestConfig.Data),
                    nameof(requestConfig.QueryDictionary),
                    nameof(requestConfig.BodyDictionary),
                    nameof(requestConfig.ReturnPrimaryRequest),
                    nameof(requestConfig.UpdateBodyParentIdRegex),
                    nameof(requestConfig.UpdateBodyParentIdValue),
                    nameof(requestConfig.Details)
                });

            NodesHelper.FillChildrenValue(requestConfig, nameof(requestConfig.Details));


            await fetchDatasRecursivelyAsync(requestConfig);
            return requestConfig.Data;

            async Task fetchDatasRecursivelyAsync(RequestConfig config)
            {
                bool responseOkPredicate(JObject response) => response[config.ResponseOkField]?.ToString() == config.ResponseOkValue;
                var responseDataFieldArr = config.ResponseDataField.Split(':');
                JToken? getDataFunc(JObject response)
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
                }

                using var httpClient = new HttpClient();
                var records = await FetchAllDataFromApiAsync(config.Url, config.FailMsg,
                                                                       config.QueryDictionary, config.PageIndexField, config.PageIndexParamInQuery.Value, config.BodyDictionary,
                                                                       responseOkPredicate,
                                                                       getDataFunc,
                                                                       httpClient,
                                                                       config.IdFieldName, config.ParentIdFieldName,
                                                                       false,
                                                                       requestMethod: config.RequestMethod,
                                                                       updateBodyParentIdRegex: config.UpdateBodyParentIdRegex,
                                                                       updateBodyParentIdValue: config.UpdateBodyParentIdValue,
                                                                       authorizationHeaderToken: config.Token);
                if (config.Data is not null)
                {
                    var configData = config.Data.ToList();
                    configData.AddRange(records);
                    config.Data = configData;
                }
                else
                {
                    config.Data = records;
                }
                
                if (config.Details is not null && config.Details.Any())
                {
                    foreach (var record in records)
                    {
                        // 子查询
                        foreach (var detail in config.Details)
                        {
                            ResolveDictionaryTmplValue(JToken.FromObject(detail.QueryDictionary ?? new Dictionary<string, object>()), record);
                            ResolveDictionaryTmplValue(detail.BodyDictionary, record);
                            await fetchDatasRecursivelyAsync(detail);
                            if (!string.IsNullOrWhiteSpace(detail.ReturnPrimaryRequest))
                            {
                                var newConfigs = TmplHelper.ResolveJTokenByDataSourceTmpl(JToken.FromObject(config), JToken.FromObject(detail.Data), detail.ReturnPrimaryRequest);
                                foreach (var newConfig in newConfigs)
                                {
                                    var newConfigObj = newConfig.ToObject<RequestConfig>();
                                    newConfigObj.Data = config.Data;
                                    newConfigObj.Details = config.Details;
                                    await FetchAllDataFromApiAsync(newConfigObj);
                                }
                            }
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 将dictionary中的模板用data的对应属性填充
        /// </summary>
        /// <param name="target"></param>
        /// <param name="data"></param>
        private static void ResolveDictionaryTmplValue(JToken? target, JToken? data)
        {
            if (target is null)
            {
                return;
            }

            if (data is null || data is not JObject)
            {
                return;
            }
            if (target is JObject dataObj)
            {
                var dataProps = dataObj.Properties();
                foreach (var dataProp in dataProps)
                {
                    if (dataProp.Value is null || string.IsNullOrWhiteSpace(dataProp.Value.ToString()))
                    {
                        continue;
                    }
                    // 如果是string, int等基础类型, kv.Value as JToken 为null, 将不处理
                    TmplHelper.ResolveJTokenTmplValue(dataProp.Value, dataObj);
                }
            }
            else
            {
                // TODO: ...
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
        public static async Task<List<JToken>> FetchAllDataFromApiAsync(
                string apiUrl,
                string errPrefix,
                IDictionary<string, object>?
                queryDictionary,
                string pageIndexParamName,
                bool pageIndexParamInQuery,
                JToken? bodyDictionary,
                Func<JObject, bool> responseOkPredicate,
                Func<JObject, JToken?> getDataFunc,
                HttpClient httpClient,
                string idFieldName,
                string parentIdParamName,
                bool parentIdParamInQuery,
                string requestMethod = "post",
                string updateBodyParentIdRegex = "",
                string updateBodyParentIdValue = "",
                string authorizationHeaderToken = "",
                string mediaType = "",
                ILogger logger = null
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
            bodyDictionary ??= new JObject();

            // 如果请求方式是FormData/application/x-www-form-urlencoded, 那么body中的参数就跟QueryString一样: name=zhangsan&age=24, 
            // 如果请求方式是application/json, 那么body中的参数就是一个json字符串: { "name": "zhangsan", "age": 24 }, 
            if (bodyDictionary.Any())
            {
                if (mediaType == MediaType.FormuUrlencoded)
                {
                    if (bodyDictionary is JObject bodyObj)
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
                    bodyContent = JsonConvert.SerializeObject(bodyDictionary);
                }
            }
            #endregion


            var allDatas = new List<JToken>();
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
                    if (record is JObject recordObj)
                    {
                        var id = recordObj.Properties().FirstOrDefault(x => string.Equals(x.Name, idFieldName, StringComparison.OrdinalIgnoreCase)) ?? throw new Exception($"不存在主键{idFieldName}");
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
                                        bodyContent = bodyContent.TrimEnd('}') + $@",""{parentIdParamName}"":""{id.Value}""}}";
                                    }
                                    else
                                    {
                                        bodyContent = string.IsNullOrWhiteSpace(updateBodyParentIdValue)
                                            ? Regex.Replace(bodyContent, updateBodyParentIdRegex, m => id.Value.ToString())
                                            : Regex.Replace(bodyContent, updateBodyParentIdRegex, m => updateBodyParentIdValue.Replace("$parentId", id.Value.ToString(), StringComparison.OrdinalIgnoreCase));
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
            async Task<List<JToken>> ApiChildrenDataAsync()
            {
                List<JToken> allApiDatas = new();
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

                    var responseContent = await queryAsync();

                    var responseObj = JsonConvert.DeserializeObject<JObject>(responseContent) ?? throw new Exception($"返回数据不是一个对象: {responseContent}");
                    // 检查并获取响应数据
                    var data = GetData(responseObj, errPrefix, responseOkPredicate, getDataFunc);

                    if (data is JObject dataObj && dataObj is not null)
                    {
                        allApiDatas.Add(dataObj);
                    }
                    else if (data.Any())
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
        private static JToken GetData(JObject responseObj, string errPrefix, Func<JObject, bool> requestOkPredicate, Func<JObject, JToken?> getDataFunc)
        {
            if (responseObj is null)
            {
                throw new Exception($"{errPrefix}: API接口返回为空");
            }

            if (!requestOkPredicate(responseObj))
            {
                //TODO: 不直接获取数据返回数据, 返回源格式, 调用者自行处理, requestOkPredicate以及相关参数都不需要了
                throw new Exception($"{errPrefix}: 状态码与配置的ResponseOkValue值不一致{Environment.NewLine}{JsonConvert.SerializeObject(responseObj)}");
            }

            var data = getDataFunc(responseObj) ?? throw new Exception($"{errPrefix}: API返回的数据为空");
            if (data is not JArray dataArr)
            {
                data = dataArr = new JArray(data);
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
        public bool? PageIndexParamInQuery { get; set; }
        public string? IdFieldName { get; set; }
        public string? ParentIdFieldName { get; set; }
        public IDictionary<string, object>? QueryDictionary { get; set; }
        public JToken? BodyDictionary { get; set; }
        public string? ResponseOkField { get; set; }
        public string? ResponseOkValue { get; set; }
        public string? ResponseDataField { get; set; }
        public string? FailMsg { get; set; }
        public string? UpdateBodyParentIdRegex { get; set; }
        public string? UpdateBodyParentIdValue { get; set; }
        /// <summary>
        /// get or post
        /// </summary>
        public string RequestMethod { get; set; }
        public string? Token { get; set; }
        /// <summary>
        /// 请求获取的数据
        /// </summary>
        public IEnumerable<JToken>? Data { get; set; }
        /// <summary>
        /// 根据当前数据的值去查询关联的明细数据(外键表数据)
        /// </summary>
        public List<RequestConfig>? Details { get; set; }

        /// <summary>
        /// 递归回到主查询的请求, 基于当前数据字段值重新给请求参数赋值
        /// </summary>
        public string? ReturnPrimaryRequest { get; set; }
    }
}
