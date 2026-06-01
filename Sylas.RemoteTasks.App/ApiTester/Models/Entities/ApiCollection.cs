using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.Database.Attributes;

namespace Sylas.RemoteTasks.App.ApiTester.Models.Entities
{
    /// <summary>
    /// API 接口集合 - 一个集合可包含多条 API 接口, 通常对应一份 Swagger 导入或一个手动维护的接口分组
    /// </summary>
    [Table(TableName)]
    public class ApiCollection : EntityBase<int>
    {
        public const string TableName = "ApiCollections";
        /// <summary>
        /// 集合名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Base URL, 例如 https://dev.iduo.cc:5004
        /// </summary>
        public string BaseUrl { get; set; } = string.Empty;
        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; } = string.Empty;
        /// <summary>
        /// 接口数量(冗余字段, 提升列表展示性能)
        /// </summary>
        public int EndpointCount { get; set; }
        /// <summary>
        /// 来源类型: swagger / manual
        /// </summary>
        public string SourceType { get; set; } = "manual";
        /// <summary>
        /// 原始来源内容(swagger.json 原文, 便于重新导入或对比)
        /// </summary>
        public string SourceContent { get; set; } = string.Empty;
        /// <summary>
        /// 全局授权 JSON, 例如 {"type":"bearer","token":"xxx"}
        /// </summary>
        public string GlobalAuth { get; set; } = "{}";
        /// <summary>
        /// 全局请求头 JSON
        /// </summary>
        public string GlobalHeaders { get; set; } = "[]";
        /// <summary>
        /// 全局校验规则 JSON
        /// </summary>
        public string GlobalValidators { get; set; } = "[]";
    }
}
