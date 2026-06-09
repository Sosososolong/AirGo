using Newtonsoft.Json;
using Sylas.RemoteTasks.App.ApiTester.Models.Dtos;
using Sylas.RemoteTasks.App.ApiTester.Models.Entities;
using Sylas.RemoteTasks.App.ApiTester.Repositories;
using Sylas.RemoteTasks.Database;
using Sylas.RemoteTasks.Utils.CommandExecutor.Http;
using Sylas.RemoteTasks.Utils.CommandExecutor.Http.Models;

namespace Sylas.RemoteTasks.App.ApiTester.Services
{
    /// <summary>
    /// 后端代理转发服务: 模板解析 → 发送请求 → 返回响应 → 落 ApiHistories
    /// 变量提取与校验由 Task 4/5 的 VariableExtractorService / AssertionService 负责
    /// </summary>
    public class RequestProxyService(
        IHttpRequestPipeline pipeline,
        ApiTesterRepository repository,
        IDatabaseProvider db,
        VariableExtractorService extractor,
        ILogger<RequestProxyService> logger)
    {
        private readonly IHttpRequestPipeline _pipeline = pipeline;
        private readonly ApiTesterRepository _repo = repository;
        private readonly IDatabaseProvider _db = db;
        private readonly VariableExtractorService _extractor = extractor;
        private readonly ILogger<RequestProxyService> _logger = logger;

        public async Task<HttpRequestResultDto> SendAsync(SendRequestDto dto)
        {
            // 顶层入口：独立一次请求, 使用当前激活环境变量上下文
            var ctx = await BuildVariableContextAsync();
            return await SendAsync(dto, ctx);
        }

        /// <summary>
        /// 批量测试: 顺序执行多个接口, 共享同一份变量上下文(上一个接口提取的变量可被下一个接口直接引用)
        /// </summary>
        public async Task<BatchSendResult> BatchSendAsync(BatchSendDto dto)
        {
            var batch = new BatchSendResult();
            if (dto.EndpointIds is null || dto.EndpointIds.Count == 0) return batch;
            var ctx = await BuildVariableContextAsync();

            foreach (var epId in dto.EndpointIds)
            {
                var ep = await _repo.Endpoints.GetByIdAsync(epId);
                if (ep is null)
                {
                    batch.Items.Add(new BatchItemResult { EndpointId = epId, Error = "接口不存在" });
                    continue;
                }
                var col = ep.CollectionId > 0 ? await _repo.Collections.GetByIdAsync(ep.CollectionId) : null;
                var sendDto = BuildSendDtoFromEntity(ep, col);
                var r = await SendAsync(sendDto, ctx);
                batch.Items.Add(new BatchItemResult
                {
                    EndpointId = epId,
                    Name = ep.Name ?? string.Empty,
                    Method = ep.Method ?? string.Empty,
                    Path = ep.Path ?? string.Empty,
                    Status = r.Status,
                    DurationMs = r.DurationMs,
                    // 请求出错(Error 非空) 时 ValidatorResults 必为空, 不能让“未跑校验”被误报为“全部通过”
                    AllValidatorsPassed = string.IsNullOrEmpty(r.Error)
                        && (r.ValidatorResults.Count == 0 || r.ValidatorResults.All(v => v.Passed)),
                    ValidatorResults = r.ValidatorResults,
                    Error = r.Error,
                });
            }
            return batch;
        }

        static SendRequestDto BuildSendDtoFromEntity(ApiEndpoint ep, ApiCollection? col)
        {
            var baseUrl = col?.BaseUrl ?? string.Empty;
            return new SendRequestDto
            {
                EndpointId = ep.Id,
                CollectionId = ep.CollectionId,
                Method = ep.Method ?? "GET",
                Url = (baseUrl ?? string.Empty) + (ep.Path ?? string.Empty),
                Params = SafeDeserialize<List<KvRow>>(ep.Params) ?? [],
                Headers = SafeDeserialize<List<KvRow>>(ep.Headers) ?? [],
                Body = ep.Body ?? string.Empty,
                BodyType = ep.BodyType ?? "none",
                Auth = SafeDeserialize<AuthDto>(ep.Auth) ?? new AuthDto { Inherit = true },
                Extractors = SafeDeserialize<List<ExtractorDto>>(ep.Extractors) ?? [],
                Validators = SafeDeserialize<List<ValidatorDto>>(ep.Validators) ?? [],
                OverrideGlobalValidators = ep.OverrideGlobalValidators,
            };
        }

        static T? SafeDeserialize<T>(string? json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonConvert.DeserializeObject<T>(json); } catch { return null; }
        }
        async Task<HttpRequestResultDto> SendAsync(SendRequestDto dto, Dictionary<string, object?> variableMap)
        {
            // 1) ApiTester 业务专属: 合并集合级全局 Headers / Auth / Validators
            var mergedHeaders = await BuildEffectiveHeadersAsync(dto);
            var effectiveAuth = await ResolveEffectiveAuthAsync(dto);
            var effectiveValidators = await BuildEffectiveValidatorsAsync(dto);

            // 2) 映射成共享层 Spec
            var spec = new HttpRequestSpec
            {
                Method = dto.Method ?? "GET",
                Url = dto.Url ?? string.Empty,
                QueryParams = (dto.Params ?? []).Select(MapKv).ToList(),
                Headers = mergedHeaders,
                Body = dto.Body ?? string.Empty,
                BodyKind = ParseBodyKind(dto.BodyType),
                Auth = MapAuth(effectiveAuth),
                Extractors = (dto.Extractors ?? []).Select(MapExtractor).ToList(),
                Validators = [.. effectiveValidators.Select(t => MapValidator(t.Item1, t.Item2))],
                VariableContext = variableMap,
                TimeoutSeconds = 60
            };

            // 3) 调共享层
            var raw = await _pipeline.SendAsync(spec);

            // 4) 映射回 ApiTester DTO
            var result = new HttpRequestResultDto
            {
                Status = raw.Status,
                StatusText = raw.StatusText,
                Headers = raw.Headers,
                Body = raw.Body,
                Size = (int)raw.Size,
                DurationMs = raw.DurationMs,
                Error = raw.Error,
                ExtractedVars = [.. raw.ExtractedVars.Select(x => new ExtractedVarResult { Name = x.Name, Value = x.Value })],
                ValidatorResults = [.. raw.ValidatorResults.Select(x => new Models.Dtos.ValidatorResult { Field = x.Field, Op = x.Op, Expected = x.Expected, Actual = x.Actual, Passed = x.Passed, Source = x.Source })]
            };

            // 5) ApiTester 业务专属: 持久化提取的变量到激活环境
            if (string.IsNullOrWhiteSpace(result.Error) && result.ExtractedVars.Count > 0)
            {
                try
                {
                    await _extractor.PersistVariablesAsync(result.ExtractedVars);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "持久化提取变量失败");
                }
            }

            // 6) ApiTester 历史落库
            await SaveHistoryAsync(dto, raw, result);
            return result;
        }
        /// <summary>
        /// 合并集合全局 Headers + 接口 Headers, 接口同名优先
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        async Task<List<KvPair>> BuildEffectiveHeadersAsync(SendRequestDto dto)
        {
            var list = (dto.Headers ?? []).Where(h => h.Enabled && !string.IsNullOrWhiteSpace(h.Name)).Select(MapKv).ToList();
            if (dto.CollectionId > 0)
            {
                try
                {
                    var col = await _repo.Collections.GetByIdAsync(dto.CollectionId);
                    if (col is not null && !string.IsNullOrWhiteSpace(col.GlobalHeaders))
                    {
                        var globals = JsonConvert.DeserializeObject<List<KvRow>>(col.GlobalHeaders);
                        foreach (var g in globals ?? [])
                        {
                            if (!g.Enabled || string.IsNullOrWhiteSpace(g.Name))
                            {
                                continue;
                            }
                            if (list.Any(h => string.Equals(h.Name, g.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                continue;
                            }
                            list.Add(MapKv(g));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "合并全局 Headers 失败");
                }
            }

            return list;
        }
        #region DTO ↔ Spec 映射
        static KvPair MapKv(KvRow row) => new() { Name = row.Name, Value = row.Value, Enabled = row.Enabled, Description = row.Description };
        static BodyKind ParseBodyKind(string? bodyType) => (bodyType ?? "none").ToLowerInvariant() switch
        {
            "json" => BodyKind.Json,
            "form-urlencoded" => BodyKind.FormUrlEncoded,
            "form-data" => BodyKind.FormData,
            "xml" => BodyKind.Xml,
            "text" => BodyKind.Text,
            _ => BodyKind.None,
        };
        static AuthSpec? MapAuth(AuthDto? auth)
        {
            if (auth is null) return null;
            return new()
            {
                Type = auth.Type ?? "none",
                Token = auth.Token ?? string.Empty,
                Username = auth.Username ?? string.Empty,
                Password = auth.Password ?? string.Empty,
                KeyName = auth.KeyName ?? string.Empty,
                KeyValue = auth.KeyValue ?? string.Empty,
                KeyIn = auth.KeyIn ?? "header",
                CustomHeaders = [.. (auth.CustomHeaders ?? []).Select(MapKv)]
            };
        }
        static ExtractorSpec MapExtractor(ExtractorDto e) => new()
        {
            VarName = e.VarName,
            DataPath = e.DataPath,
            Field = e.Field,
            Filter = e.Filter is null ? null : new ExtractorFilter
            {
                FieldName = e.Filter.FieldName,
                MatchValue = e.Filter.MatchValue,
            },
        };
        static ValidatorSpec MapValidator(ValidatorDto v, string source) => new()
        {
            Field = v.Field,
            Op = v.Op,
            Expected = v.Expected,
            Source = source,
        };
        #endregion

        #region 内部辅助
        /// <summary>
        /// 解析有效 Auth: dto.Auth.Inherit=true 且集合存在 GlobalAuth 时返回全局, 否则返回接口本身
        /// </summary>
        async Task<AuthDto?> ResolveEffectiveAuthAsync(SendRequestDto dto)
        {
            if (dto.Auth is null) return null;
            if (!dto.Auth.Inherit) return dto.Auth;
            if (dto.CollectionId <= 0) return null;
            try
            {
                var col = await _repo.Collections.GetByIdAsync(dto.CollectionId);
                if (col is null || string.IsNullOrWhiteSpace(col.GlobalAuth) || col.GlobalAuth == "{}") return null;
                var g = JsonConvert.DeserializeObject<AuthDto>(col.GlobalAuth);
                if (g is null) return null;
                g.Inherit = false; // 全局 Auth 应被认为是代表本接口的生效配置
                return g;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "解析集合全局 Auth 失败");
                return null;
            }
        }

        /// <summary>
        /// 汇合集合全局校验 + 接口校验, 并标记来源 collection / endpoint
        /// dto.OverrideGlobalValidators=true 时只用接口本身校验
        /// </summary>
        async Task<List<(ValidatorDto, string)>> BuildEffectiveValidatorsAsync(SendRequestDto dto)
        {
            var list = new List<(ValidatorDto, string)>();
            if (!dto.OverrideGlobalValidators && dto.CollectionId > 0)
            {
                try
                {
                    var col = await _repo.Collections.GetByIdAsync(dto.CollectionId);
                    if (col is not null && !string.IsNullOrWhiteSpace(col.GlobalValidators) && col.GlobalValidators != "[]")
                    {
                        var globals = JsonConvert.DeserializeObject<List<ValidatorDto>>(col.GlobalValidators) ?? [];
                        foreach (var g in globals) list.Add((g, "collection"));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "解析集合全局校验失败");
                }
            }
            foreach (var v in dto.Validators ?? []) list.Add((v, "endpoint"));
            return list;
        }

        async Task<Dictionary<string, object?>> BuildVariableContextAsync()
        {
            var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var envPage = await _repo.Environments.GetPageAsync(new(1, int.MaxValue, null, [new("id", true)]));
                var activeEnv = envPage.Data.FirstOrDefault(e => e.IsActive) ?? envPage.Data.FirstOrDefault();
                if (activeEnv is null) return map;
                var vars = await _repo.GetVariablesByEnvAsync(activeEnv.Id);
                foreach (var v in vars.Data)
                {
                    map[v.Name] = v.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "构建变量上下文失败");
            }
            return map;
        }
        const int MaxBodyLen = 1024 * 1024; // 1MB

        async Task SaveHistoryAsync(SendRequestDto dto, HttpRequestResult raw, HttpRequestResultDto result)
        {
            try
            {
                var snapshot = JsonConvert.SerializeObject(new
                {
                    method = dto.Method,
                    url = raw.FinalUrl,
                    headers = raw.FinalHeaders,
                    body = Truncate(raw.FinalBody, MaxBodyLen),
                    bodyType = dto.BodyType
                });
                var record = new Dictionary<string, object?>
                {
                    { "endpointId", dto.EndpointId },
                    { "requestSnapshot", snapshot },
                    { "responseStatus", result.Status },
                    { "responseHeaders", JsonConvert.SerializeObject(result.Headers) },
                    { "responseBody", Truncate(result.Body, MaxBodyLen) },
                    { "durationMs", result.DurationMs },
                    { "extractedVars", JsonConvert.SerializeObject(result.ExtractedVars) },
                    { "validationResults", JsonConvert.SerializeObject(result.ValidatorResults) },
                    { "createTime", DateTime.Now },
                    { "updateTime", DateTime.Now },
                };

                await _db.InsertDataAsync(ApiHistory.TableName, [record]);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "保存请求历史失败");
            }
        }

        static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? string.Empty;
            return string.Concat(s.AsSpan(0, max), "...[truncated]");
        }
        #endregion
    }
}
