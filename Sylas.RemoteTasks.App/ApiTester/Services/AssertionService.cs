using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.ApiTester.Models.Dtos;
using Sylas.RemoteTasks.Utils.Template;

namespace Sylas.RemoteTasks.App.ApiTester.Services
{
    /// <summary>
    /// 响应断言服务: 对 SendRequestResult 执行一组 ValidatorDto, 输出 ValidatorResult 列表
    /// 支持 8 种条件: eq / ne / gt / lt / ge / le / contains / exists
    /// field 支持 JSON 路径(JToken.SelectToken), 特殊 field "status" 表示状态码, "headers.XXX" 表示响应头
    /// expected 支持 {{var}} 模板
    /// </summary>
    public class AssertionService(ILogger<AssertionService> logger)
    {
        private readonly ILogger<AssertionService> _logger = logger;

        public List<ValidatorResult> Validate(SendRequestResult resp, List<ValidatorDto> validators, Dictionary<string, object?> variableContext, string source = "endpoint")
        {
            var results = new List<ValidatorResult>();
            if (validators is null || validators.Count == 0) return results;

            JToken? bodyJson = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(resp.Body))
                {
                    var trimmed = resp.Body.TrimStart();
                    if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
                    {
                        bodyJson = JToken.Parse(resp.Body);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "响应体不是合法 JSON, 仅支持纯文本断言");
            }

            foreach (var v in validators)
            {
                var actual = ExtractActual(v.Field ?? string.Empty, resp, bodyJson);
                var expected = ResolveTemplate(v.Expected ?? string.Empty, variableContext);
                var passed = Compare(actual, expected, v.Op ?? "eq");
                results.Add(new ValidatorResult
                {
                    Field = v.Field ?? string.Empty,
                    Op = v.Op ?? "eq",
                    Expected = expected,
                    Actual = actual ?? string.Empty,
                    Passed = passed,
                    Source = source,
                });
            }
            return results;
        }

        static string? ExtractActual(string field, SendRequestResult resp, JToken? bodyJson)
        {
            if (string.IsNullOrWhiteSpace(field)) return resp.Body;
            // 状态码
            if (string.Equals(field, "status", StringComparison.OrdinalIgnoreCase)
                || string.Equals(field, "statusCode", StringComparison.OrdinalIgnoreCase))
            {
                return resp.Status.ToString();
            }
            // 响应头 headers.XXX
            if (field.StartsWith("headers.", StringComparison.OrdinalIgnoreCase))
            {
                var headerName = field.Substring("headers.".Length);
                if (resp.Headers.TryGetValue(headerName, out var hv)) return hv;
                return null;
            }
            // 响应体 JSON 路径
            if (bodyJson is not null)
            {
                var node = bodyJson.SelectToken(field);
                if (node is null) return null;
                if (node.Type == JTokenType.String) return node.Value<string>();
                return node.ToString(Newtonsoft.Json.Formatting.None);
            }
            // 纯文本
            return resp.Body;
        }

        static string ResolveTemplate(string s, Dictionary<string, object?> ctx)
        {
            if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
            try { return TmplHelper.ResolveExpressionValue(s, ctx)?.ToString() ?? string.Empty; }
            catch { return s; }
        }

        static bool Compare(string? actual, string expected, string op)
        {
            var o = (op ?? "eq").ToLowerInvariant();
            switch (o)
            {
                case "exists":
                    return actual is not null;
                case "contains":
                    return actual is not null && actual.Contains(expected ?? string.Empty);
                case "eq":
                    return string.Equals(actual ?? string.Empty, expected ?? string.Empty, StringComparison.Ordinal);
                case "ne":
                    return !string.Equals(actual ?? string.Empty, expected ?? string.Empty, StringComparison.Ordinal);
                case "gt":
                case "lt":
                case "ge":
                case "le":
                    return CompareNumeric(actual, expected, o);
            }
            return false;
        }

        static bool CompareNumeric(string? actual, string expected, string op)
        {
            if (!double.TryParse(actual, out var a)) return false;
            if (!double.TryParse(expected, out var e)) return false;
            return op switch
            {
                "gt" => a > e,
                "lt" => a < e,
                "ge" => a >= e,
                "le" => a <= e,
                _ => false,
            };
        }
    }
}
