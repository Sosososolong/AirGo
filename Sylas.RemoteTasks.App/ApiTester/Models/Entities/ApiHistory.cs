using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.Database.Attributes;

namespace Sylas.RemoteTasks.App.ApiTester.Models.Entities
{
    /// <summary>
    /// 接口请求历史
    /// </summary>
    [Table(TableName)]
    public class ApiHistory : EntityBase<int>
    {
        public const string TableName = "ApiHistories";
        /// <summary>
        /// 接口 Id, 0 表示临时接口(未持久化)
        /// </summary>
        public int EndpointId { get; set; }
        /// <summary>
        /// 请求快照 JSON(method/url/headers/body 等)
        /// </summary>
        public string RequestSnapshot { get; set; } = "{}";
        /// <summary>
        /// 响应状态码
        /// </summary>
        public int ResponseStatus { get; set; }
        /// <summary>
        /// 响应头 JSON
        /// </summary>
        public string ResponseHeaders { get; set; } = "{}";
        /// <summary>
        /// 响应体, 超长时存为文件并存路径(参见服务实现)
        /// </summary>
        public string ResponseBody { get; set; } = string.Empty;
        /// <summary>
        /// 请求耗时(毫秒)
        /// </summary>
        public int DurationMs { get; set; }
        /// <summary>
        /// 提取的变量结果 JSON, [{name,value}]
        /// </summary>
        public string ExtractedVars { get; set; } = "[]";
        /// <summary>
        /// 校验结果 JSON, [{field,op,expected,actual,passed}]
        /// </summary>
        public string ValidationResults { get; set; } = "[]";
    }
}
