using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System;
using System.Linq;

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
        /// 请求方法
        /// </summary>
        public string Method { get; set; } = string.Empty;
        /// <summary>
        /// 请求的媒体类型
        /// </summary>
        public string ContentType { get; set; } = string.Empty;
        /// <summary>
        /// 请求头
        /// </summary>
        public string[] Headers { get; set; } = [];
        /// <summary>
        /// 请求体内容
        /// </summary>
        public string Body { get; set; } = string.Empty;
        /// <summary>
        /// 是否打印响应内容
        /// </summary>
        public bool? PrintResponseContent { get; set; }
        /// <summary>
        /// 正则表达式, 响应内容匹配则表示成功, 不匹配则表示失败
        /// </summary>
        public string IsSuccessPattern { get; set; } = string.Empty;
        /// <summary>
        /// 响应内容中提取数据存储起来供后续的HTTTP请求或者其他数据操作使用
        /// </summary>
        public string ResponseExtractors = string.Empty;
        /// <summary>
        /// 响应对象中存储数据的属性名称, 如data
        /// </summary>
        public string ResponseDataPropty { get; set; } = string.Empty;
        /// <summary>
        /// 响应数据处理器列表, 用来处理响应数据
        /// </summary>
        public string[] DataHandlers { get; set; } = [];
        /// <summary>
        /// 复制一份当前对象
        /// </summary>
        /// <returns></returns>
        public HttpRequestDto Copy()
        {
            return new HttpRequestDto
            {
                Url = Url,
                Method = Method,
                ContentType = ContentType,
                Headers = Headers.Select(h => h).ToArray(),
                Body = Body,
                IsSuccessPattern = IsSuccessPattern,
                ResponseDataPropty = ResponseDataPropty,
                ResponseExtractors = ResponseExtractors,
                DataHandlers = DataHandlers.Select(dh => dh).ToArray()
            };
        }
    }
}
