using System.Collections.Generic;

namespace Sylas.RemoteTasks.App.ApiTester.Models.Dtos
{
    #region 集合
    /// <summary>
    /// 创建/更新集合 Dto
    /// </summary>
    public class ApiCollectionSaveDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string GlobalAuth { get; set; } = "{}";
        public string GlobalHeaders { get; set; } = "[]";
        public string GlobalValidators { get; set; } = "[]";
    }
    #endregion

    #region 接口
    /// <summary>
    /// 创建/更新接口 Dto
    /// </summary>
    public class ApiEndpointSaveDto
    {
        public int Id { get; set; }
        public int CollectionId { get; set; }
        public string Tag { get; set; } = "MANUAL";
        public string Name { get; set; } = string.Empty;
        public string Method { get; set; } = "GET";
        public string Path { get; set; } = string.Empty;
        public string Params { get; set; } = "[]";
        public string Headers { get; set; } = "[]";
        public string Body { get; set; } = string.Empty;
        public string BodyType { get; set; } = "none";
        public string Auth { get; set; } = "{\"inherit\":true}";
        public string Extractors { get; set; } = "[]";
        public string Validators { get; set; } = "[]";
        public bool OverrideGlobalValidators { get; set; }
        public int OrderNo { get; set; }
    }

    /// <summary>
    /// 一行 Param/Header
    /// </summary>
    public class KvRow
    {
        public bool Enabled { get; set; } = true;
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
    #endregion

    #region Swagger 导入
    public class SwaggerImportDto
    {
        /// <summary>
        /// swagger 内容(JSON 或 YAML)
        /// </summary>
        public string Content { get; set; } = string.Empty;
        /// <summary>
        /// swagger URL, 与 Content 二选一
        /// </summary>
        public string Url { get; set; } = string.Empty;
        /// <summary>
        /// 集合名称(覆盖 swagger 中的 info.title)
        /// </summary>
        public string CollectionName { get; set; } = string.Empty;
        /// <summary>
        /// Base URL(覆盖 swagger 中的 host/servers)
        /// </summary>
        public string BaseUrl { get; set; } = string.Empty;
    }
    #endregion

    #region 发送请求
    /// <summary>
    /// 发送单个接口请求(临时不一定持久化)
    /// </summary>
    public class SendRequestDto
    {
        public int EndpointId { get; set; }
        public int CollectionId { get; set; }
        public string Method { get; set; } = "GET";
        public string Url { get; set; } = string.Empty;
        public List<KvRow> Params { get; set; } = [];
        public List<KvRow> Headers { get; set; } = [];
        public string Body { get; set; } = string.Empty;
        public string BodyType { get; set; } = "none";
        public AuthDto Auth { get; set; } = new();
        public List<ExtractorDto> Extractors { get; set; } = [];
        public List<ValidatorDto> Validators { get; set; } = [];
        public bool OverrideGlobalValidators { get; set; }
    }

    public class AuthDto
    {
        /// <summary>
        /// true 时使用集合的全局 Auth
        /// </summary>
        public bool Inherit { get; set; } = true;
        /// <summary>
        /// none / bearer / basic / apikey / custom
        /// </summary>
        public string Type { get; set; } = "none";
        public string Token { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string KeyName { get; set; } = string.Empty;
        public string KeyValue { get; set; } = string.Empty;
        /// <summary>
        /// header / query
        /// </summary>
        public string KeyIn { get; set; } = "header";
        public List<KvRow> CustomHeaders { get; set; } = [];
    }

    public class ExtractorDto
    {
        public string VarName { get; set; } = string.Empty;
        public string DataPath { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
        public ExtractorFilterDto? Filter { get; set; }
    }

    public class ExtractorFilterDto
    {
        public string FieldName { get; set; } = string.Empty;
        public string MatchValue { get; set; } = string.Empty;
    }

    public class ValidatorDto
    {
        public string Field { get; set; } = string.Empty;
        /// <summary>
        /// eq / ne / gt / lt / ge / le / contains / exists
        /// </summary>
        public string Op { get; set; } = "eq";
        public string Expected { get; set; } = string.Empty;
    }

    /// <summary>
    /// 发送请求结果
    /// </summary>
    public class SendRequestResult
    {
        public int Status { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public Dictionary<string, string> Headers { get; set; } = [];
        public string Body { get; set; } = string.Empty;
        public int DurationMs { get; set; }
        public int Size { get; set; }
        /// <summary>
        /// 提取的变量
        /// </summary>
        public List<ExtractedVarResult> ExtractedVars { get; set; } = [];
        /// <summary>
        /// 校验结果
        /// </summary>
        public List<ValidatorResult> ValidatorResults { get; set; } = [];
        /// <summary>
        /// 异常信息(仅请求失败时)
        /// </summary>
        public string Error { get; set; } = string.Empty;
    }

    public class ExtractedVarResult
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class ValidatorResult
    {
        public string Field { get; set; } = string.Empty;
        public string Op { get; set; } = string.Empty;
        public string Expected { get; set; } = string.Empty;
        public string Actual { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public string Source { get; set; } = string.Empty;
    }
    #endregion

    #region 环境变量
    public class ApiEnvironmentSaveDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class ApiVariableSaveDto
    {
        public int Id { get; set; }
        public int EnvironmentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSecret { get; set; }
    }
    #endregion

    #region 批量
    public class BatchSendDto
    {
        public int CollectionId { get; set; }
        public List<int> EndpointIds { get; set; } = [];
    }

    public class BatchSendResult
    {
        public List<BatchItemResult> Items { get; set; } = [];
    }

    public class BatchItemResult
    {
        public int EndpointId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public int Status { get; set; }
        public int DurationMs { get; set; }
        public bool AllValidatorsPassed { get; set; }
        /// <summary>
        /// 每条校验详情(含全局+接口级), Source: collection/endpoint
        /// </summary>
        public List<ValidatorResult> ValidatorResults { get; set; } = [];
        public string Error { get; set; } = string.Empty;
    }
    #endregion

    #region 通用
    public class IdDto
    {
        public int Id { get; set; }
    }

    public class CollectionIdDto
    {
        public int CollectionId { get; set; }
    }

    public class ImportJsonDto
    {
        public string Content { get; set; } = string.Empty;
    }

    /// <summary>
    /// 接口拖拽排序: 一条接口 的新序号
    /// </summary>
    public class EndpointOrderItem
    {
        public int Id { get; set; }
        public int OrderNo { get; set; }
    }
    public class EndpointsOrderDto
    {
        public List<EndpointOrderItem> Orders { get; set; } = new();
    }
    #endregion
}
