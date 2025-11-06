using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System;

namespace Sylas.RemoteTasks.Utils.CommandExecutor
{
    /// <summary>
    /// http请求参数
    /// </summary>
    public class HttpRequestDto
    {
        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>
        /// Url地址
        /// </summary>
        public string Url { get; set; } = string.Empty;
        /// <summary>
        /// 请求头
        /// </summary>
        public string Headers { get; set; } = string.Empty;
        /// <summary>
        /// 请求方法
        /// </summary>
        public string Method { get; set; } = string.Empty;
        /// <summary>
        /// 请求的媒体类型
        /// </summary>
        public string ContentType { get; set; } = string.Empty;
        /// <summary>
        /// 请求体内容
        /// </summary>
        public string Body { get; set; } = string.Empty;
        /// <summary>
        /// 响应内容中提取数据存储起来供后面的HTTTP请求使用
        /// </summary>
        public string ResponseExtractorsJson = string.Empty;
    }

    /// <summary>
    /// 记录Json提取的Key和提取值所存储的Key
    /// </summary>
    public class JsonExtractor
    {
        /// <summary>
        /// 要提取的Key
        /// </summary>
        public string SourceKey { get; set; } = string.Empty;
        /// <summary>
        /// 指定提取出来的值存储的Key
        /// </summary>
        public string StoredKey { get; set; } = string.Empty;
        /// <summary>
        /// 提取值并存储到datasource中
        /// </summary>
        /// <param name="result"></param>
        /// <param name="extracts"></param>
        /// <param name="datasource"></param>
        public static void ExtractVars(JObject result, List<JsonExtractor> extracts, Dictionary<string, object> datasource)
        {
            if (result is null || extracts is null || extracts.Count == 0)
            {
                return;
            }
            foreach (var extract in extracts)
            {
                string keyPath = extract.SourceKey;
                string valueVarName = extract.StoredKey;
                JToken value = "";
                int kIndex = -1;
                foreach (var k in keyPath.Split('.'))
                {
                    kIndex++;
                    if (kIndex == 0)
                    {
                        value = result[k] ?? string.Empty;
                    }
                    else
                    {
                        value = value[k] ?? string.Empty;
                    }
                }
                datasource[valueVarName] = value?.ToString() ?? string.Empty;
            }
        }
    }
}
