using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace Sylas.RemoteTasks.App.Utils
{
    public static class ApiValidationHelpers
    {
        private static readonly string _sameWithLastMsgPrefix = "same";
        private static readonly string _changedMsgPrefix = "changed";
        private static readonly string _getResponseFirstTimePrefix = "first time";
        private static readonly string _responseField = "response";
        /// <summary>
        /// 使用大量的参数集测试指定API
        /// </summary>
        /// <param name="url">测试的api的Url地址</param>
        /// <param name="parametersFile">测试参数集所在的文件</param>
        /// <param name="authorizationHeaderToken">token</param>
        /// <param name="responseIdentityField">返回对象的标识字段</param>
        /// <param name="firstIndex">从索引为firstIndex的参数开始</param>
        /// <param name="count">本次一共测试几个参数</param>
        /// <param name="countPerClient">每个HttpClient对象执行几个http请求</param>
        /// <returns></returns>
        public static async Task ApiBatchValidationAsync(string url, string parametersFile, string authorizationHeaderToken, string responseIdentityField, int firstIndex, int count, int countPerClient = 10)
        {
            // 如果firstIndex = 10, 那么从索引为10的(第11个)参数开始
            // var firstIndex = 1737;

            var tasks = new List<Task>();
            var lastIndex = firstIndex + count - 1;
            List<StringBuilder> clientValidationLogs = new();

            var json = await File.ReadAllTextAsync(parametersFile);
            var allInstanceParameters = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
            if (allInstanceParameters is null)
            {
                throw new Exception($"文件{parametersFile}中的参数不正确");
            }

            // D:\\.NET\\iduo\\routine\\txt\\validatioinrange0-9920230206145503.log
            var rangeInfo = $"range{firstIndex}-{firstIndex + count - 1}";
            var logFile = Path.Combine(Path.GetDirectoryName(parametersFile) ?? throw new Exception("获取参数文件的目录失败"), $"validatioin{rangeInfo}{DateTime.Now:yyyyMMddHHmmss}.log");

            while (true)
            {
                var end = firstIndex + countPerClient - 1;
                if (end > lastIndex)
                {
                    end = lastIndex;
                }

                var validationLog = new StringBuilder();
                clientValidationLogs.Add(validationLog);

                // 是否有参数首次被请求

                tasks.Add(
                    ApiBatchValidationAsync(
                        url,
                        parametersFile,
                        allInstanceParameters,
                        authorizationHeaderToken,
                        responseIdentityField,
                        validationLog,
                        Tuple.Create(firstIndex, end) // firstIndex:0, 100个参数(即索引为0-99): 0-9; 10-19; 20-29; ...; 90-99
                    )
                );

                if (end == lastIndex)
                {
                    break;
                }
                firstIndex = end + 1;
            }

            await Task.WhenAll(tasks);

            using var writer = new StreamWriter(logFile, true);
            for (int i = 0; i < clientValidationLogs.Count; i++)
            {
                writer.WriteLine(clientValidationLogs[i]);
            }

            if (clientValidationLogs.Any(log => log.ToString().IndexOf(_getResponseFirstTimePrefix) > -1))
            {
                await File.WriteAllTextAsync(parametersFile, JsonConvert.SerializeObject(allInstanceParameters));
            }
        }
        private static async Task ApiBatchValidationAsync(string url,
            string parametersFile,
            List<Dictionary<string, object>> allInstanceParameters,
            string authorizationHeaderToken,
            string responseIdentityField,
            StringBuilder validationLog,
            Tuple<int, int>? parametersRange = null)
        {
            var httpClient = new HttpClient();

            var allInstanceParametersCount = allInstanceParameters.Count;
            parametersRange ??= Tuple.Create(0, allInstanceParametersCount - 1);
            if (!(parametersRange.Item1 <= parametersRange.Item2 && parametersRange.Item1 >= 0 && parametersRange.Item2 <= allInstanceParametersCount - 1))
            {
                throw new Exception($"请求的参数范围{parametersRange.Item1} - {parametersRange.Item2}不在0-{allInstanceParametersCount - 1}之间");
            }
            var start = DateTime.Now;

            var responseInfo = new StringBuilder();
            Action<StringBuilder, int, string> appendValidationLog = (logBuilder, i, log) =>
            {
                if (i < parametersRange.Item2)
                {
                    logBuilder.AppendLine(log);
                }
                else
                {
                    logBuilder.Append(log);
                }
            };
            for (int i = parametersRange.Item1; i <= parametersRange.Item2; i++)
            {
                var requestParameters = allInstanceParameters[i];
                List<JToken>? result = null;
                try
                {
                    result = await RemoteHelpers.FetchAllDataFromApiAsync(url, "接口测试请求失败", requestParameters, string.Empty, false, null, res => res["code"]?.ToString() == "1", res => res["data"], httpClient, null, null, false, "get", authorizationHeaderToken: authorizationHeaderToken);
                }
                catch (Exception ex)
                {
                    validationLog.AppendLine(ex.ToString());
                    break;
                }
                responseInfo.Clear();
                foreach (var data in result)
                {
                    if (data is JObject dataObj)
                    {
                        responseInfo.Append(dataObj[responseIdentityField]?.ToString() + ",");
                    }
                }

                var requestParametersJson = JsonConvert.SerializeObject(requestParameters);
                if (requestParameters.TryGetValue(_responseField, out object? lastResponseValue) && lastResponseValue is not null && lastResponseValue.ToString() == responseInfo.ToString())
                {
                    appendValidationLog(validationLog, i, $"{_sameWithLastMsgPrefix} - {i}: {requestParametersJson}");
                }
                else
                {
                    if (!requestParameters.ContainsKey(_responseField))
                    {
                        requestParameters.Add(_responseField, responseInfo.ToString());
                        appendValidationLog(validationLog, i, $"{_getResponseFirstTimePrefix} - {i}: {requestParametersJson}");
                        // hasParametersFirstRequested = true;
                    }
                    else
                    {
                        appendValidationLog(validationLog, i, $"{_changedMsgPrefix} - {i}: {requestParametersJson} ====> {responseInfo}");
                    }
                }
            }

            var duration = (DateTime.Now - start).TotalSeconds;
            validationLog.Append($"耗时{DateTimeHelper.FormatSeconds(duration)}, 平均每个请求耗时{duration / (parametersRange.Item2 - parametersRange.Item1 + 1)}/s");
        }
    }
}
