using Sylas.RemoteTasks.App.ApiTester.Models.Dtos;
using Sylas.RemoteTasks.App.ApiTester.Models.Entities;
using Sylas.RemoteTasks.App.ApiTester.Repositories;
using Sylas.RemoteTasks.Database.SyncBase;

namespace Sylas.RemoteTasks.App.ApiTester.Services
{
    /// <summary>
    /// ApiTester 业务服务: CRUD + Swagger 导入整合
    /// </summary>
    public class ApiTesterService(
        ApiTesterRepository repository,
        SwaggerImportService swaggerImport,
        ILogger<ApiTesterService> logger)
    {
        private readonly ApiTesterRepository _repo = repository;
        private readonly SwaggerImportService _swagger = swaggerImport;
        private readonly ILogger<ApiTesterService> _logger = logger;

        #region 集合
        public Task<PagedData<ApiCollection>> GetCollectionsAsync()
            => _repo.Collections.GetPageAsync(new DataSearch(1, int.MaxValue, null, [new("id", false)]));

        public async Task<int> SaveCollectionAsync(ApiCollectionSaveDto dto)
        {
            if (dto.Id > 0)
            {
                var existed = await _repo.Collections.GetByIdAsync(dto.Id) ?? throw new Exception($"集合不存在: {dto.Id}");
                existed.Name = dto.Name;
                existed.BaseUrl = dto.BaseUrl;
                existed.Description = dto.Description;
                existed.GlobalAuth = dto.GlobalAuth;
                existed.GlobalHeaders = dto.GlobalHeaders;
                existed.GlobalValidators = dto.GlobalValidators;
                await _repo.Collections.UpdateAsync(existed);
                return existed.Id;
            }
            var col = new ApiCollection
            {
                Name = dto.Name,
                BaseUrl = dto.BaseUrl,
                Description = dto.Description,
                GlobalAuth = dto.GlobalAuth,
                GlobalHeaders = dto.GlobalHeaders,
                GlobalValidators = dto.GlobalValidators,
                SourceType = "manual",
            };
            return await _repo.Collections.AddAsync(col);
        }

        public async Task<int> DeleteCollectionAsync(int id)
        {
            await _repo.DeleteEndpointsByCollectionAsync(id);
            return await _repo.Collections.DeleteAsync(id);
        }
        #endregion

        #region 接口
        public Task<PagedData<ApiEndpoint>> GetEndpointsAsync(int collectionId)
            => _repo.GetEndpointsByCollectionAsync(collectionId);

        public async Task<int> SaveEndpointAsync(ApiEndpointSaveDto dto)
        {
            if (dto.Id > 0)
            {
                var existed = await _repo.Endpoints.GetByIdAsync(dto.Id) ?? throw new Exception($"接口不存在: {dto.Id}");
                existed.CollectionId = dto.CollectionId;
                existed.Tag = dto.Tag;
                existed.Name = dto.Name;
                existed.Method = dto.Method;
                existed.Path = dto.Path;
                existed.Params = dto.Params;
                existed.Headers = dto.Headers;
                existed.Body = dto.Body;
                existed.BodyType = dto.BodyType;
                existed.Auth = dto.Auth;
                existed.Extractors = dto.Extractors;
                existed.Validators = dto.Validators;
                existed.OverrideGlobalValidators = dto.OverrideGlobalValidators;
                existed.OrderNo = dto.OrderNo;
                await _repo.Endpoints.UpdateAsync(existed);
                return existed.Id;
            }
            var ep = new ApiEndpoint
            {
                CollectionId = dto.CollectionId,
                Tag = string.IsNullOrWhiteSpace(dto.Tag) ? "MANUAL" : dto.Tag,
                Name = dto.Name,
                Method = dto.Method,
                Path = dto.Path,
                Params = dto.Params,
                Headers = dto.Headers,
                Body = dto.Body,
                BodyType = dto.BodyType,
                Auth = dto.Auth,
                Extractors = dto.Extractors,
                Validators = dto.Validators,
                OverrideGlobalValidators = dto.OverrideGlobalValidators,
                OrderNo = dto.OrderNo,
            };
            var newId = await _repo.Endpoints.AddAsync(ep);

            // 维护集合的 EndpointCount
            var col = await _repo.Collections.GetByIdAsync(dto.CollectionId);
            if (col is not null)
            {
                col.EndpointCount += 1;
                await _repo.Collections.UpdateAsync(col);
            }
            return newId;
        }

        public Task<int> DeleteEndpointAsync(int id) => _repo.Endpoints.DeleteAsync(id);

        /// <summary>
        /// 批量更新接口 OrderNo (拖拽排序专用, 局部更新仅动 OrderNo 字段)
        /// </summary>
        public async Task<int> UpdateEndpointsOrderAsync(EndpointsOrderDto dto)
        {
            if (dto?.Orders == null || dto.Orders.Count == 0) return 0;
            int total = 0;
            foreach (var item in dto.Orders)
            {
                if (item.Id <= 0) continue;
                var fields = new Dictionary<string, string>
                {
                    ["id"] = item.Id.ToString(),
                    ["orderno"] = item.OrderNo.ToString()
                };
                total += await _repo.Endpoints.UpdateAsync(fields);
            }
            return total;
        }
        #endregion

        #region 环境与变量
        public Task<PagedData<ApiEnvironment>> GetEnvironmentsAsync()
            => _repo.Environments.GetPageAsync(new DataSearch(1, int.MaxValue, null, [new("id", true)]));

        public async Task<int> SaveEnvironmentAsync(ApiEnvironmentSaveDto dto)
        {
            if (dto.Id > 0)
            {
                var existed = await _repo.Environments.GetByIdAsync(dto.Id) ?? throw new Exception($"环境不存在: {dto.Id}");
                existed.Name = dto.Name;
                await _repo.Environments.UpdateAsync(existed);
                return existed.Id;
            }
            var env = new ApiEnvironment { Name = dto.Name, IsActive = false };
            return await _repo.Environments.AddAsync(env);
        }

        public async Task<int> DeleteEnvironmentAsync(int id)
        {
            await _repo.DeleteVariablesByEnvAsync(id);
            return await _repo.Environments.DeleteAsync(id);
        }

        public Task<int> SetActiveEnvironmentAsync(int id) => _repo.SetActiveEnvironmentAsync(id);

        public Task<PagedData<ApiVariable>> GetVariablesAsync(int environmentId)
            => _repo.GetVariablesByEnvAsync(environmentId);

        public async Task<int> SaveVariableAsync(ApiVariableSaveDto dto)
        {
            if (dto.Id > 0)
            {
                var existed = await _repo.Variables.GetByIdAsync(dto.Id) ?? throw new Exception($"变量不存在: {dto.Id}");
                existed.EnvironmentId = dto.EnvironmentId;
                existed.Name = dto.Name;
                existed.Value = dto.Value;
                existed.Description = dto.Description;
                existed.IsSecret = dto.IsSecret;
                await _repo.Variables.UpdateAsync(existed);
                return existed.Id;
            }
            var v = new ApiVariable
            {
                EnvironmentId = dto.EnvironmentId,
                Name = dto.Name,
                Value = dto.Value,
                Description = dto.Description,
                IsSecret = dto.IsSecret,
            };
            return await _repo.Variables.AddAsync(v);
        }

        public Task<int> DeleteVariableAsync(int id) => _repo.Variables.DeleteAsync(id);
        #endregion

        #region Swagger 导入
        /// <summary>
        /// 导入 swagger / OpenAPI: 解析 → 创建 Collection → 批量插入 Endpoints
        /// </summary>
        public async Task<int> ImportSwaggerAsync(SwaggerImportDto dto)
        {
            string content = dto.Content;
            if (string.IsNullOrWhiteSpace(content) && !string.IsNullOrWhiteSpace(dto.Url))
            {
                content = await _swagger.FetchFromUrlAsync(dto.Url);
            }
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new Exception("未提供 swagger 内容(Content 或 Url)");
            }

            var (collection, endpoints) = _swagger.Parse(content, dto.CollectionName, dto.BaseUrl);
            var collectionId = await _repo.Collections.AddAsync(collection);

            int order = 0;
            foreach (var ep in endpoints)
            {
                ep.CollectionId = collectionId;
                ep.OrderNo = order++;
                await _repo.Endpoints.AddAsync(ep);
            }

            _logger.LogInformation("Swagger 导入完成: collection={CollectionId} endpoints={Count}", collectionId, endpoints.Count);
            return collectionId;
        }
        #endregion

        #region 导入导出 JSON
        /// <summary>
        /// 导出集合为自有 JSON 格式(含集合所有接口)
        /// </summary>
        public async Task<object> ExportCollectionAsync(int collectionId)
        {
            var col = await _repo.Collections.GetByIdAsync(collectionId) ?? throw new Exception("集合不存在");
            var eps = await _repo.GetEndpointsByCollectionAsync(collectionId);
            return new
            {
                format = "sylas-apitester",
                version = "1.0",
                collection = new
                {
                    name = col.Name,
                    baseUrl = col.BaseUrl,
                    description = col.Description,
                    globalAuth = col.GlobalAuth,
                    globalHeaders = col.GlobalHeaders,
                    globalValidators = col.GlobalValidators,
                },
                endpoints = eps.Data.Select(e => new
                {
                    tag = e.Tag,
                    name = e.Name,
                    method = e.Method,
                    path = e.Path,
                    @params = e.Params,
                    headers = e.Headers,
                    body = e.Body,
                    bodyType = e.BodyType,
                    auth = e.Auth,
                    extractors = e.Extractors,
                    validators = e.Validators,
                    overrideGlobalValidators = e.OverrideGlobalValidators,
                    orderNo = e.OrderNo,
                }).ToList()
            };
        }

        /// <summary>
        /// 导入自有 JSON 格式, 返回新建集合 Id
        /// </summary>
        public async Task<int> ImportJsonAsync(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) throw new Exception("JSON 内容为空");
            var root = Newtonsoft.Json.Linq.JObject.Parse(content);
            // 检测是否为 swagger: 含 swagger / openapi key
            if (root["swagger"] != null || root["openapi"] != null)
            {
                return await ImportSwaggerAsync(new SwaggerImportDto { Content = content });
            }
            var colObj = root["collection"] ?? throw new Exception("缺少 collection 节点");
            var col = new ApiCollection
            {
                Name = colObj.Value<string>("name") ?? "Imported",
                BaseUrl = colObj.Value<string>("baseUrl") ?? string.Empty,
                Description = colObj.Value<string>("description") ?? string.Empty,
                GlobalAuth = colObj.Value<string>("globalAuth") ?? "{}",
                GlobalHeaders = colObj.Value<string>("globalHeaders") ?? "[]",
                GlobalValidators = colObj.Value<string>("globalValidators") ?? "[]",
                SourceType = "manual",
            };
            var collectionId = await _repo.Collections.AddAsync(col);
            var endpointsArr = root["endpoints"] as Newtonsoft.Json.Linq.JArray;
            if (endpointsArr != null)
            {
                int order = 0;
                foreach (var item in endpointsArr)
                {
                    var ep = new ApiEndpoint
                    {
                        CollectionId = collectionId,
                        Tag = item.Value<string>("tag") ?? "MANUAL",
                        Name = item.Value<string>("name") ?? "Untitled",
                        Method = item.Value<string>("method") ?? "GET",
                        Path = item.Value<string>("path") ?? "/",
                        Params = item.Value<string>("params") ?? "[]",
                        Headers = item.Value<string>("headers") ?? "[]",
                        Body = item.Value<string>("body") ?? string.Empty,
                        BodyType = item.Value<string>("bodyType") ?? "none",
                        Auth = item.Value<string>("auth") ?? "{\"inherit\":true}",
                        Extractors = item.Value<string>("extractors") ?? "[]",
                        Validators = item.Value<string>("validators") ?? "[]",
                        OverrideGlobalValidators = item.Value<bool?>("overrideGlobalValidators") ?? false,
                        OrderNo = item.Value<int?>("orderNo") ?? order,
                    };
                    await _repo.Endpoints.AddAsync(ep);
                    order++;
                }
                col.EndpointCount = endpointsArr.Count;
                col.Id = collectionId;
                await _repo.Collections.UpdateAsync(col);
            }
            return collectionId;
        }
        #endregion
    }
}
