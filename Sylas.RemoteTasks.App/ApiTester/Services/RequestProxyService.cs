using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.ApiTester.Models.Dtos;
using Sylas.RemoteTasks.App.ApiTester.Models.Entities;
using Sylas.RemoteTasks.App.ApiTester.Repositories;
using Sylas.RemoteTasks.Database;
using Sylas.RemoteTasks.Utils.Template;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;

namespace Sylas.RemoteTasks.App.ApiTester.Services
{
    /// <summary>
    /// 后端代理转发服务: 模板解析 → 发送请求 → 返回响应 → 落 ApiHistories
    /// 变量提取与校验由 Task 4/5 的 VariableExtractorService / AssertionService 负责
    /// </summary>
    public class RequestProxyService(
        IHttpClientFactory httpClientFactory,
        ApiTesterRepository repository,
        IDatabaseProvider db,
        VariableExtractorService extractor,
        AssertionService assertion,
        ILogger<RequestProxyService> logger)
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ApiTesterRepository _repo = repository;
        private readonly IDatabaseProvider _db = db;
        private readonly VariableExtractorService _extractor = extractor;
        private readonly AssertionService _assertion = assertion;
        private readonly ILogger<RequestProxyService> _logger = logger;

        public async Task<SendRequestResult> SendAsync(SendRequestDto dto)
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

        async Task<SendRequestResult> SendAsync(SendRequestDto dto, Dictionary<string, object?> variableMap)
        {
            var result = new SendRequestResult();

            // 2) 模板解析
            //    注意: BuildFullUrl 需传入 variableMap, 让 param.Value 中的 {{var}} 在 EscapeDataString 之前被替换,
            //    否则 "{{newTypeId}}" 会被转义为 "%7B%7BnewTypeId%7D%7D", 导致后续 ResolveTemplate 正则匹配不到
            string finalUrl = ResolveTemplate(BuildFullUrl(dto, variableMap), variableMap);
            string finalBody = ResolveTemplate(dto.Body ?? string.Empty, variableMap);
            var finalHeaders = (dto.Headers ?? [])
                .Where(h => h.Enabled && !string.IsNullOrWhiteSpace(h.Name))
                .Select(h => new KeyValuePair<string, string>(
                    h.Name, ResolveTemplate(h.Value ?? string.Empty, variableMap)))
                .ToList();

            // 2b) 合并集合全局 Headers (名同名覆盖, 接口级优先)
            if (dto.CollectionId > 0)
            {
                try
                {
                    var col = await _repo.Collections.GetByIdAsync(dto.CollectionId);
                    if (col is not null && !string.IsNullOrWhiteSpace(col.GlobalHeaders) && col.GlobalHeaders != "[]")
                    {
                        var globals = JsonConvert.DeserializeObject<List<KvRow>>(col.GlobalHeaders) ?? [];
                        foreach (var g in globals)
                        {
                            if (!g.Enabled || string.IsNullOrWhiteSpace(g.Name)) continue;
                            if (finalHeaders.Any(h => string.Equals(h.Key, g.Name, StringComparison.OrdinalIgnoreCase))) continue;
                            finalHeaders.Add(new KeyValuePair<string, string>(g.Name, ResolveTemplate(g.Value ?? string.Empty, variableMap)));
                        }
                    }
                }
                catch (Exception ex) { _logger.LogWarning(ex, "合并全局 Headers 失败"); }
            }

            // 3) 构建 HttpRequestMessage
            var method = (dto.Method ?? "GET").ToUpperInvariant();
            using var req = new HttpRequestMessage(new HttpMethod(method), finalUrl);

            // Auth: 支持继承全局 Auth, 5 种类型全覆盖
            var effectiveAuth = await ResolveEffectiveAuthAsync(dto);
            ApplyAuth(req, effectiveAuth, variableMap, finalUrl, out finalUrl);
            if (req.RequestUri?.ToString() != finalUrl)
            {
                req.RequestUri = new Uri(finalUrl);
            }

            // 设置 Body
            req.Content = BuildContent(dto.BodyType, finalBody, finalHeaders);

            // 设置非 content 类 header
            foreach (var h in finalHeaders)
            {
                if (IsContentHeader(h.Key)) continue;
                req.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }

            // 4) 发送
            var sw = Stopwatch.StartNew();
            HttpResponseMessage? resp = null;
            string respBody = string.Empty;
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(60);
                resp = await client.SendAsync(req);
                respBody = await resp.Content.ReadAsStringAsync();

                result.Status = (int)resp.StatusCode;
                result.StatusText = resp.ReasonPhrase ?? string.Empty;
                result.Body = respBody;
                foreach (var h in resp.Headers)
                {
                    result.Headers[h.Key] = string.Join(", ", h.Value);
                }
                if (resp.Content.Headers != null)
                {
                    foreach (var h in resp.Content.Headers)
                    {
                        result.Headers[h.Key] = string.Join(", ", h.Value);
                    }
                }
                result.Size = Encoding.UTF8.GetByteCount(respBody ?? string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "代理请求失败: {Method} {Url}", method, finalUrl);
                result.Error = ex.Message;
            }
            finally
            {
                sw.Stop();
                result.DurationMs = (int)sw.ElapsedMilliseconds;
                resp?.Dispose();
            }

            // 5) 变量提取(Task 4): 请求成功且有 extractors 时执行
            if (string.IsNullOrEmpty(result.Error) && dto.Extractors is not null && dto.Extractors.Count > 0)
            {
                try
                {
                    result.ExtractedVars = await _extractor.ExtractAsync(result.Body, dto.Extractors, variableMap);
                    // 将提取出的变量同步到上下文, 供后续校验使用
                    foreach (var ev in result.ExtractedVars)
                    {
                        if (!string.IsNullOrEmpty(ev.Name)) variableMap[ev.Name] = ev.Value;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "变量提取出错");
                }
            }

            // 5b) 响应校验(Task 5): 合并集合全局校验 + 接口本身校验
            if (string.IsNullOrEmpty(result.Error))
            {
                try
                {
                    var validators = await BuildEffectiveValidatorsAsync(dto);
                    if (validators.Count > 0)
                    {
                        result.ValidatorResults = _assertion.Validate(result, validators.Select(x => x.Item1).ToList(), variableMap);
                        // 补上 Source
                        for (int i = 0; i < result.ValidatorResults.Count && i < validators.Count; i++)
                        {
                            result.ValidatorResults[i].Source = validators[i].Item2;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "响应校验出错");
                }
            }

            // 6) 落历史(限长保护)
            await SaveHistoryAsync(dto, finalUrl, method, finalHeaders, finalBody, result);

            return result;
        }

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

        static string ResolveTemplate(string tmpl, Dictionary<string, object?> ctx)
        {
            if (string.IsNullOrEmpty(tmpl)) return tmpl ?? string.Empty;
            try
            {
                var resolved = TmplHelper.ResolveExpressionValue(tmpl, ctx);
                return resolved?.ToString() ?? string.Empty;
            }
            catch
            {
                return tmpl;
            }
        }

        static string BuildFullUrl(SendRequestDto dto, Dictionary<string, object?> variableMap)
        {
            var url = dto.Url ?? string.Empty;
            var enabledParams = (dto.Params ?? []).Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Name)).ToList();
            if (enabledParams.Count == 0) return url;

            var sb = new StringBuilder();
            foreach (var p in enabledParams)
            {
                if (sb.Length > 0) sb.Append('&');
                // 先对 value 进行模板解析({{var}}/${var}/$var), 再 EscapeDataString
                var resolvedValue = ResolveTemplate(p.Value ?? string.Empty, variableMap);
                sb.Append(Uri.EscapeDataString(p.Name)).Append('=').Append(Uri.EscapeDataString(resolvedValue));
            }
            return url.Contains('?') ? $"{url}&{sb}" : $"{url}?{sb}";
        }

        static HttpContent? BuildContent(string bodyType, string body, List<KeyValuePair<string, string>> headers)
        {
            switch ((bodyType ?? "none").ToLowerInvariant())
            {
                case "none":
                    return null;
                case "json":
                    return new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json");
                case "form-urlencoded":
                    {
                        var pairs = ParseFormPairs(body);
                        return new FormUrlEncodedContent(pairs);
                    }
                case "form-data":
                    {
                        var mp = new MultipartFormDataContent();
                        foreach (var p in ParseFormPairs(body))
                        {
                            mp.Add(new StringContent(p.Value ?? string.Empty), p.Key);
                        }
                        return mp;
                    }
                case "xml":
                    return new StringContent(body ?? string.Empty, Encoding.UTF8, "text/xml");
                case "text":
                    return new StringContent(body ?? string.Empty, Encoding.UTF8, "text/plain");
                default:
                    return new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json");
            }
        }

        static IEnumerable<KeyValuePair<string, string>> ParseFormPairs(string body)
        {
            // 兼容 JSON 对象 与 a=1&b=2 两种形式
            if (string.IsNullOrWhiteSpace(body)) yield break;
            var trimmed = body.TrimStart();
            if (trimmed.StartsWith('{'))
            {
                JObject? obj = null;
                try { obj = JObject.Parse(body); } catch { obj = null; }
                if (obj is null) yield break;
                foreach (var p in obj.Properties())
                {
                    yield return new KeyValuePair<string, string>(p.Name, p.Value?.ToString() ?? string.Empty);
                }
                yield break;
            }
            foreach (var pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var idx = pair.IndexOf('=');
                if (idx < 0) yield return new KeyValuePair<string, string>(pair, string.Empty);
                else yield return new KeyValuePair<string, string>(pair[..idx], pair[(idx + 1)..]);
            }
        }

        static bool IsContentHeader(string name) =>
            name.StartsWith("Content-", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 应用 Auth 配置(简化版, Task 5 提供完整继承全局 / 5 种类型切换)
        /// </summary>
        static void ApplyAuth(HttpRequestMessage req, AuthDto? auth, Dictionary<string, object?> ctx, string currentUrl, out string updatedUrl)
        {
            updatedUrl = currentUrl;
            if (auth is null || auth.Inherit) return;
            switch ((auth.Type ?? "none").ToLowerInvariant())
            {
                case "bearer":
                    {
                        var t = ResolveTemplate(auth.Token ?? string.Empty, ctx);
                        if (!string.IsNullOrWhiteSpace(t))
                            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", t);
                        break;
                    }
                case "basic":
                    {
                        var u = ResolveTemplate(auth.Username ?? string.Empty, ctx);
                        var p = ResolveTemplate(auth.Password ?? string.Empty, ctx);
                        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{u}:{p}"));
                        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
                        break;
                    }
                case "apikey":
                    {
                        var name = ResolveTemplate(auth.KeyName ?? string.Empty, ctx);
                        var val = ResolveTemplate(auth.KeyValue ?? string.Empty, ctx);
                        if (string.IsNullOrWhiteSpace(name)) break;
                        if (string.Equals(auth.KeyIn, "query", StringComparison.OrdinalIgnoreCase))
                        {
                            updatedUrl = currentUrl + (currentUrl.Contains('?') ? "&" : "?") +
                                $"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(val)}";
                        }
                        else
                        {
                            req.Headers.TryAddWithoutValidation(name, val);
                        }
                        break;
                    }
                case "custom":
                    foreach (var h in auth.CustomHeaders ?? [])
                    {
                        if (!h.Enabled || string.IsNullOrWhiteSpace(h.Name)) continue;
                        req.Headers.TryAddWithoutValidation(h.Name, ResolveTemplate(h.Value ?? string.Empty, ctx));
                    }
                    break;
            }
        }

        const int MaxBodyLen = 1024 * 1024; // 1MB
        async Task SaveHistoryAsync(SendRequestDto dto, string finalUrl, string method, List<KeyValuePair<string, string>> headers, string finalBody, SendRequestResult result)
        {
            try
            {
                var snapshot = JsonConvert.SerializeObject(new
                {
                    method,
                    url = finalUrl,
                    headers,
                    body = Truncate(finalBody, MaxBodyLen),
                    bodyType = dto.BodyType,
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
            return s.Substring(0, max) + "...[truncated]";
        }
        #endregion
    }
}
