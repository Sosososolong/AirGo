using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.Utils.CommandExecutor.Http.Models;
using Sylas.RemoteTasks.Utils.Template;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Utils.CommandExecutor.Http
{
    /// <summary>
    /// IHttpRequestPipeline 默认实现
    /// 职责: 发送HTTP请求, 包括模板解析 → Auth 应用 → Body 构建 → 发送 → 响应提取 → 校验
    /// </summary>
    public class HttpRequestPipeline(IHttpClientFactory httpClientFactory, ILogger<HttpRequestPipeline> logger) : IHttpRequestPipeline
    {
        /// <summary>
        /// 发送HTTP请求
        /// </summary>
        /// <param name="spec"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<HttpRequestResult> SendAsync(HttpRequestSpec spec, CancellationToken cancellationToken = default)
        {
            var result = new HttpRequestResult();
            var ctx = spec.VariableContext ?? [];

            // 1) 模板解析: Url + Query拼接 (注意 query.Value 必须先解模板再 EscapeDataString)
            string finalUrl = ResolveTemplate(spec.Url, ctx);
            string finalBody = ResolveTemplate(spec.Body ?? string.Empty, ctx);
            var finalHeaders = (spec.Headers ?? [])
                .Where(h => h.Enabled && !string.IsNullOrWhiteSpace(h.Name))
                .Select(h => new KvPair { Name = h.Name, Value = ResolveTemplate(h.Value ?? string.Empty, ctx), Enabled = true })
                .ToList();

            // 2) 构建 HttpRequestMessage
            var method = (spec.Method ?? "GET").ToUpperInvariant();
            using var req = new HttpRequestMessage(new HttpMethod(method), finalUrl);

            // Auth (可能修改finalUrl, 例如apikey-in-query
            ApplyAuth(req, spec.Auth, ctx, finalUrl, out finalUrl);
            if (req.RequestUri?.ToString() != finalUrl)
            {
                req.RequestUri = new Uri(finalUrl);
            }

            // Body
            req.Content = BuildContent(spec.BodyKind, finalBody, finalHeaders);

            // 非 content-类 header 设置到 req.Headers
            foreach (var h in finalHeaders)
            {
                if (IsContentHeader(h.Name))
                {
                    continue;
                }
                req.Headers.TryAddWithoutValidation(h.Name, h.Value);
            }

            // 3) 发送
            var sw = Stopwatch.StartNew();
            HttpResponseMessage? resp = null;
            string respBody = string.Empty;
            try
            {
                var client = httpClientFactory.CreateClient();
                int timeoutSeconds = spec.TimeoutSeconds > 0 ? spec.TimeoutSeconds : 60;
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                resp = await client.SendAsync(req, cancellationToken);
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
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "HTTP 请求失败: {Method} {Url}", method, finalUrl);
                result.Error = ex.Message;
            }
            finally
            {
                sw.Stop();
                result.DurationMs = (int)sw.ElapsedMilliseconds;
                resp?.Dispose();
            }
            result.FinalUrl = finalUrl;
            result.FinalBody = finalBody;
            result.FinalHeaders = finalHeaders;

            // 4) 提取变量(请求成功且有 extractors 时)
            if (string.IsNullOrEmpty(result.Error) && spec.Extractors is { Count: > 0 })
            {
                try
                {
                    result.ExtractedVars = ExtractVars(result.Body, spec.Extractors, ctx);
                    foreach (var ev in result.ExtractedVars)
                    {
                        if (!string.IsNullOrWhiteSpace(ev.Name))
                        {
                            ctx[ev.Name] = ev.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "变量提取出错:{detail}", $"Body: {result.Body}{Environment.NewLine}Extractors: {JsonConvert.SerializeObject(spec.Extractors)}");
                }
            }

            // 5) 校验
            if (string.IsNullOrWhiteSpace(result.Error) && spec.Validators is { Count: > 0 })
            {
                try
                {
                    result.ValidatorResults = Validate(result, spec.Validators, ctx);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "响应校验出错:{0}", $"HttpRequestResult: {JsonConvert.SerializeObject(result)}{Environment.NewLine}Validators: {JsonConvert.SerializeObject(spec.Validators)}");
                }
            }

            return result;
        }

        #region 处理模板变量
        /// <summary>
        /// 解析模板字符串, 替换其中的 {{var}}/${var}/$var 模板变量
        /// </summary>
        /// <param name="template"></param>
        /// <param name="variableContext"></param>
        /// <returns></returns>
        string ResolveTemplate(string template, Dictionary<string, object?> variableContext)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return template ?? string.Empty;
            }

            try
            {
                var resolved = TmplHelper2.ResolveTmpl(template, variableContext);
                return resolved ?? string.Empty;
            }
            catch (System.Exception ex)
            {
                logger.LogError(ex, "Failed to resolve template: {Template}", template);
                return template ?? string.Empty;
            }
        }

        string BuildFullUrl(HttpRequestSpec spec, Dictionary<string, object?> ctx)
        {
            var url = spec.Url ?? string.Empty;
            var enabled = (spec.QueryParams ?? []).FindAll(q => q.Enabled && !string.IsNullOrWhiteSpace(q.Name));
            if (enabled.Count == 0)
            {
                return url;
            }

            var queryStringBuilder = new StringBuilder();
            foreach (var item in enabled)
            {
                if (queryStringBuilder.Length > 0)
                {
                    queryStringBuilder.Append('&');
                }
                // 先解析模板, 再EscapeDataString, 否则{{var}}会被转义成%7B%7B...
                var resolvedValue = ResolveTemplate(item.Value ?? string.Empty, ctx);
                queryStringBuilder.Append(Uri.EscapeDataString(item.Name)).Append('=').Append(Uri.EscapeDataString(resolvedValue));
            }

            return url.Contains('?') ? $"{url}&{queryStringBuilder}" : $"{url}?{queryStringBuilder}";
        }
        #endregion

        #region 处理授权部分
        void ApplyAuth(HttpRequestMessage req, AuthSpec? auth, Dictionary<string, object?> ctx, string currentUrl, out string updatedUrl)
        {
            updatedUrl = currentUrl;
            if (auth is null)
            {
                return;
            }

            switch ((auth.Type ?? "none").ToLowerInvariant())
            {
                case "bearer":
                    {
                        var t = ResolveTemplate(auth.Token ?? string.Empty, ctx);
                        if (!string.IsNullOrWhiteSpace(t))
                        {
                            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", t);
                        }
                        break;
                    }

                case "basic":
                    {
                        var u = ResolveTemplate(auth.Username ?? string.Empty, ctx);
                        var p = ResolveTemplate(auth.Password ?? string.Empty, ctx);
                        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{u}:{p}"));
                        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", token);
                        break;
                    }

                case "apikey":
                    var name = ResolveTemplate(auth.KeyName ?? string.Empty, ctx);
                    var val = ResolveTemplate(auth.KeyValue ?? string.Empty, ctx);
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        break;
                    }

                    if (string.Equals(auth.KeyIn, "query", StringComparison.OrdinalIgnoreCase))
                    {
                        updatedUrl = currentUrl + (currentUrl.Contains('?') ? "&" : "?") + $"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(val)}";
                    }
                    else
                    {
                        req.Headers.TryAddWithoutValidation(name, val);
                    }
                    break;
                case "custom":
                    foreach (var h in auth.CustomHeaders ?? [])
                    {
                        if (!h.Enabled || string.IsNullOrWhiteSpace(h.Name)) continue;
                        req.Headers.TryAddWithoutValidation(h.Name, ResolveTemplate(h.Value ?? string.Empty, ctx));
                    }
                    break;
            }
        }

        HttpContent? BuildContent(BodyKind bodyKind, string body, List<KvPair> headers)
        {
            switch (bodyKind)
            {
                case BodyKind.None:
                    return null;
                case BodyKind.Json:
                    return new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json");
                case BodyKind.FormUrlEncoded:
                    return new FormUrlEncodedContent(ParseFormPairs(body));
                case BodyKind.FormData:
                    {
                        var mp = new MultipartFormDataContent();
                        foreach (var p in ParseFormPairs(body))
                        {
                            mp.Add(new StringContent(p.Value ?? string.Empty), p.Key);
                        }
                        return mp;
                    }
                case BodyKind.Xml:
                    return new StringContent(body ?? string.Empty, Encoding.UTF8, "text/xml");
                case BodyKind.Text:
                    return new StringContent(body ?? string.Empty, Encoding.UTF8, "text/plain");
                default:
                    return new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json");
            }
        }
        /// <summary>
        /// 给 form-urlencoded / form-data 两种 BodyType 提供的"输入归一化"工具
        /// 它要解决的痛点是：用户在前端 Body 文本框里输入表单数据时, 可能写成两种形态, 后端要把两种都识别成相同的键值对集合再交给 FormUrlEncodedContent / MultipartFormDataContent 去构建
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        IEnumerable<KeyValuePair<string, string>> ParseFormPairs(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                yield break;
            }

            var trimed = body.TrimStart();
            if (trimed.StartsWith('{'))
            {
                JObject? obj;
                try
                {
                    obj = JObject.Parse(body);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "ParseFormPairs方法异常:{body}", body);
                    obj = null;
                }

                if (obj is null)
                {
                    yield break;
                }

                foreach (var p in obj.Properties())
                {
                    yield return new KeyValuePair<string, string>(p.Name, p.Value?.ToString() ?? string.Empty);
                }
                yield break;
            }

            foreach (var pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var idx = pair.IndexOf('=');
                if (idx < 0)
                {
                    yield return new KeyValuePair<string, string>(pair, string.Empty);
                }
                else
                {
                    yield return new KeyValuePair<string, string>(pair[..idx], pair[(idx + 1)..]);
                }
            }
        }

        static bool IsContentHeader(string name) => name.StartsWith("Content-", StringComparison.OrdinalIgnoreCase);
        #endregion

        #region 提取器
        List<ExtractedVar> ExtractVars(string responseBody, List<ExtractorSpec> extractors, Dictionary<string, object?> ctx)
        {
            List<ExtractedVar> results = [];
            if (extractors is null || extractors.Count == 0 || string.IsNullOrWhiteSpace(responseBody))
            {
                return results;
            }

            JToken? root = null;
            try { root = JToken.Parse(responseBody); } catch { /* 非 JSON, regex 仍可用 */ }

            foreach (var ex in extractors)
            {
                if (string.IsNullOrWhiteSpace(ex.VarName)) continue;
                try
                {
                    var value = ResolveOne(root, responseBody, ex, ctx);
                    var strVal = value ?? string.Empty;
                    results.Add(new ExtractedVar { Name = ex.VarName, Value = strVal });
                    ctx[ex.VarName] = strVal;
                }
                catch
                {
                    results.Add(new ExtractedVar { Name = ex.VarName, Value = string.Empty });
                }
            }
            return results;
        }
        string? ResolveOne(JToken? root, string rawBody, ExtractorSpec ex, Dictionary<string, object?> ctx)
        {
            // 优先尝试 path(数据字段) + filter(过滤数据) + field(去哪个字段值) 路线
            if (root is not null)
            {
                JToken? node = string.IsNullOrWhiteSpace(ex.DataPath) ? root : root.SelectToken(ex.DataPath);
                if (node is JArray arr && ex.Filter is not null && !string.IsNullOrWhiteSpace(ex.Filter.FieldName))
                {
                    var matchValue = ResolveTemplate(ex.Filter.MatchValue ?? string.Empty, ctx);
                    JToken? matched = null;
                    foreach (var item in arr)
                    {
                        var v = item.SelectToken(ex.Filter.FieldName)?.ToString();
                        if (string.Equals(v, matchValue, StringComparison.Ordinal)) { matched = item; break; }
                    }
                    node = matched;
                }
                else if (node is JArray arr2 && arr2.Count > 0)
                {
                    node = arr2[0];
                }

                if (node is not null && !string.IsNullOrWhiteSpace(ex.Field))
                {
                    node = node.SelectToken(ex.Field);
                }

                // 如果配置了 regex, 在路径节点的字符串值上再做正则
                if (!string.IsNullOrWhiteSpace(ex.RegexPattern))
                {
                    var src = node?.ToString() ?? rawBody;
                    var m = Regex.Match(src, ex.RegexPattern);
                    if (m.Success) return m.Groups.Count > 1 ? m.Groups[1].Value : m.Value;
                    return null;
                }

                return node?.Type == JTokenType.String ? node.Value<string>() : node?.ToString(Newtonsoft.Json.Formatting.None);
            }

            // 没有 JSON 可解析时, 至少支持纯文本上的 regex
            if (!string.IsNullOrWhiteSpace(ex.RegexPattern))
            {
                var m = Regex.Match(rawBody, ex.RegexPattern);
                if (m.Success) return m.Groups.Count > 1 ? m.Groups[1].Value : m.Value;
            }
            return null;
        }
        #endregion

        #region 校验器
        List<ValidatorResult> Validate(HttpRequestResult resp, List<ValidatorSpec> validators, Dictionary<string, object?> ctx)
        {
            List<ValidatorResult> results = [];
            if (validators is null || validators.Count == 0)
            {
                return results;
            }

            JToken? bodyJson = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(resp.Body))
                {
                    var trimed = resp.Body.TrimStart();
                    if (trimed.StartsWith('{') || trimed.StartsWith('['))
                    {
                        bodyJson = JToken.Parse(resp.Body);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "HttpRequestPipeline Validate 解析Body异常: {body}", resp.Body);
                throw;
            }

            foreach (var v in validators)
            {
                var actual = ExtractActual(v.Field ?? string.Empty, resp, bodyJson);
                var expected = ResolveTemplate(v.Expected ?? string.Empty, ctx);
                var passed = Compare(actual, expected, v.Op ?? "eq");
                results.Add(new ValidatorResult
                {
                    Field = v.Field ?? string.Empty,
                    Op = v.Op ?? "eq",
                    Expected = expected,
                    Actual = actual ?? string.Empty,
                    Passed = passed,
                    Source = v.Source ?? string.Empty
                });
            }

            return results;
        }

        string? ExtractActual(string field, HttpRequestResult resp, JToken? bodyJson)
        {
            if (string.IsNullOrWhiteSpace(field))
            {
                return resp.Body;
            }

            if (string.Equals(field, "status", StringComparison.OrdinalIgnoreCase) || string.Equals(field, "StatusCode", StringComparison.OrdinalIgnoreCase))
            {
                return resp.Status.ToString();
            }

            // 请求头提取器表达式: headers.(如要获取请求头Authorization, 即headers.Authorization)
            const string headerExtractorExpression = "headers.";
            if (field.StartsWith(headerExtractorExpression, StringComparison.OrdinalIgnoreCase))
            {
                var headerName = field[headerExtractorExpression.Length..];
                return resp.Headers.TryGetValue(headerName, out var hv) ? hv : null;
            }

            if (bodyJson is not null)
            {
                var node = bodyJson.SelectToken(field);
                if (node is null)
                {
                    return null;
                }
                if (node.Type == JTokenType.String)
                {
                    return node.Value<string>();
                }
                return node.ToString(Newtonsoft.Json.Formatting.None);
            }

            return resp.Body;
        }

        static bool Compare(string? actual, string? expected, string op)
        {
            var o = (op ?? "eq").ToLowerInvariant();
            return o switch
            {
                "exists" => actual is not null,
                "contains" => actual is not null && expected is not null && actual.Contains(expected ?? string.Empty),
                "eq" => string.Equals(actual ?? string.Empty, expected ?? string.Empty, StringComparison.Ordinal),
                "ne" => !string.Equals(actual ?? string.Empty, expected ?? string.Empty, StringComparison.Ordinal),
                "gt" or "lt" or "ge" or "le" => CompareNumeric(actual, expected, o),
                _ => false,
            };
        }

        static bool CompareNumeric(string? actual, string? expected, string op)
        {
            if (!double.TryParse(actual, NumberStyles.Any, CultureInfo.InvariantCulture, out var a)) return false;
            if (!double.TryParse(expected, NumberStyles.Any, CultureInfo.InvariantCulture, out var e)) return false;
            return op switch
            {
                "gt" => a > e,
                "lt" => a < e,
                "ge" => a >= e,
                "le" => a <= e,
                _ => false,
            };
        }
        #endregion
    }
}
