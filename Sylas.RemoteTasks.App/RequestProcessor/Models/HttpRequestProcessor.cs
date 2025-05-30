using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.Database.Attributes;

namespace Sylas.RemoteTasks.App.RequestProcessor.Models
{
    /// <summary>
    /// Http请求处理器, 一个Http请求处理器将发送一个或者多个Http请求, 并处理每个请求的响应结果
    /// </summary>
    public class HttpRequestProcessor
    {
        public const string TableName = "HttpRequestProcessors";
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Headers { get; set; } = "{}";
        public string Remark { get; set; } = string.Empty;
        public bool? StepCirleRunningWhenLastStepHasData { get; set; } = false;
        public IEnumerable<HttpRequestProcessorStep> Steps { get; set; } = [];
    }
}
