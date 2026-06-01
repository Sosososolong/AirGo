using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.Database.Attributes;

namespace Sylas.RemoteTasks.App.ApiTester.Models.Entities
{
    /// <summary>
    /// API 接口 - 一个具体的 HTTP 接口测试条目
    /// </summary>
    [Table(TableName)]
    public class ApiEndpoint : EntityBase<int>
    {
        public const string TableName = "ApiEndpoints";
        /// <summary>
        /// 所属集合 Id
        /// </summary>
        public int CollectionId { get; set; }
        /// <summary>
        /// 分组 Tag(对应 swagger 的 tags), 手动接口默认 MANUAL
        /// </summary>
        public string Tag { get; set; } = "MANUAL";
        /// <summary>
        /// 接口名称(如 swagger 的 summary)
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// HTTP 方法: GET / POST / PUT / DELETE / PATCH / HEAD / OPTIONS
        /// </summary>
        public string Method { get; set; } = "GET";
        /// <summary>
        /// 接口路径(不含 BaseUrl)
        /// </summary>
        public string Path { get; set; } = string.Empty;
        /// <summary>
        /// Query 参数 JSON 数组, 每条 {enabled,name,value,description}
        /// </summary>
        public string Params { get; set; } = "[]";
        /// <summary>
        /// 请求头 JSON 数组, 每条 {enabled,name,value,description}
        /// </summary>
        public string Headers { get; set; } = "[]";
        /// <summary>
        /// 请求体内容
        /// </summary>
        public string Body { get; set; } = string.Empty;
        /// <summary>
        /// 请求体类型: none / json / form-urlencoded / form-data / text / xml
        /// </summary>
        public string BodyType { get; set; } = "none";
        /// <summary>
        /// 单接口授权配置 JSON, {inherit:bool,type,...}
        /// </summary>
        public string Auth { get; set; } = "{\"inherit\":true}";
        /// <summary>
        /// 提取规则 JSON 数组, 每条 {varName,dataPath,field,filter:{fieldName,matchValue}}
        /// </summary>
        public string Extractors { get; set; } = "[]";
        /// <summary>
        /// 校验规则 JSON 数组, 每条 {field,op,expected}
        /// </summary>
        public string Validators { get; set; } = "[]";
        /// <summary>
        /// 是否覆盖全局校验规则: true 仅用本接口规则, false 与全局规则合并
        /// </summary>
        public bool OverrideGlobalValidators { get; set; }
        /// <summary>
        /// 列表排序号
        /// </summary>
        public int OrderNo { get; set; }
    }
}
