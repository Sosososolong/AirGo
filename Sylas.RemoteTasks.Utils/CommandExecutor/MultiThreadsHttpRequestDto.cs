using System.Collections.Generic;

namespace Sylas.RemoteTasks.Utils.CommandExecutor
{
    /// <summary>
    /// 多线程发送多个HTTP请求, 模拟多用户访问页面请求接口场景
    /// </summary>
    public class MultiThreadsHttpRequestDto
    {
        /// <summary>
        /// 线程变量文件路径, 每一行(除了第一行)表示一个线程的初始变量集合, 第一行变量名, 例如: username,pwd\nuser1,123456\nuser2,123456
        /// </summary>
        public string ThreadVarsFile { get; set; } = string.Empty;
        /// <summary>
        /// 一个线程所需要发送的请求列表, 按顺序执行每一项(第一维), 每一项中如果有多个请求(第二维)则表示并发发送这些请求
        /// </summary>
        public List<List<HttpRequestDto>> Requests { get; set; } = [];
    }
}
