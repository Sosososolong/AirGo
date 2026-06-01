using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Sylas.RemoteTasks.App.ApiTester.Models.Dtos;
using Sylas.RemoteTasks.App.ApiTester.Models.Entities;
using YamlDotNet.Serialization;

namespace Sylas.RemoteTasks.App.ApiTester.Services
{
    /// <summary>
    /// Swagger v2 / OpenAPI 3.x 导入解析(支持 JSON / YAML)
    /// </summary>
    public class SwaggerImportService(ILogger<SwaggerImportService> logger, IHttpClientFactory httpClientFactory)
    {
        private readonly ILogger<SwaggerImportService> _logger = logger;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        // 序列化 KvRow 等集合为 JSON 字段时统一 camelCase, 与前端一致 (row.name/value/enabled/description)
        private static readonly JsonSerializerSettings CamelCaseSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        /// <summary>
        /// 从 URL 拉取 swagger 文档原文
        /// </summary>
        public async Task<string> FetchFromUrlAsync(string url)
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            return await client.GetStringAsync(url);
        }

        /// <summary>
        /// 解析原文(自动识别 JSON / YAML)为 ApiCollection 与 ApiEndpoint 列表
        /// </summary>
        public (ApiCollection collection, List<ApiEndpoint> endpoints) Parse(string content, string fallbackName, string baseUrlOverride)
        {
            var json = ContentToJObject(content);

            var collection = new ApiCollection
            {
                Name = !string.IsNullOrWhiteSpace(fallbackName)
                    ? fallbackName
                    : json.SelectToken("info.title")?.ToString() ?? "Imported",
                Description = json.SelectToken("info.description")?.ToString() ?? string.Empty,
                BaseUrl = !string.IsNullOrWhiteSpace(baseUrlOverride) ? baseUrlOverride : ResolveBaseUrl(json),
                SourceType = "swagger",
                SourceContent = content,
            };

            var endpoints = new List<ApiEndpoint>();
            var paths = json["paths"] as JObject;
            if (paths is null)
            {
                _logger.LogWarning("Swagger 文档不包含 paths 节点");
                return (collection, endpoints);
            }

            int orderNo = 0;
            foreach (var pathItem in paths.Properties())
            {
                var pathStr = pathItem.Name;
                var pathObj = pathItem.Value as JObject;
                if (pathObj is null) continue;

                foreach (var op in pathObj.Properties())
                {
                    var method = op.Name.ToUpperInvariant();
                    if (!IsHttpMethod(method)) continue;
                    var opObj = op.Value as JObject;
                    if (opObj is null) continue;

                    var endpoint = new ApiEndpoint
                    {
                        Method = method,
                        Path = pathStr,
                        Name = opObj.Value<string>("summary")
                               ?? opObj.Value<string>("operationId")
                               ?? $"{method} {pathStr}",
                        Tag = (opObj["tags"] as JArray)?.FirstOrDefault()?.ToString() ?? "default",
                        Params = JsonConvert.SerializeObject(BuildParams(opObj), CamelCaseSettings),
                        Headers = JsonConvert.SerializeObject(BuildHeaders(opObj), CamelCaseSettings),
                        Body = BuildBody(opObj, json, out var bodyType),
                        BodyType = bodyType,
                        OrderNo = orderNo++,
                    };
                    endpoints.Add(endpoint);
                }
            }

            collection.EndpointCount = endpoints.Count;
            return (collection, endpoints);
        }

        static JObject ContentToJObject(string content)
        {
            content ??= string.Empty;
            var trimmed = content.TrimStart();
            if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            {
                return JObject.Parse(content);
            }
            // 视为 YAML, 转 JSON 再解析
            var deserializer = new DeserializerBuilder().Build();
            var yamlObj = deserializer.Deserialize<object>(content) ?? new object();
            var serializer = new SerializerBuilder().JsonCompatible().Build();
            var jsonStr = serializer.Serialize(yamlObj);
            return JObject.Parse(jsonStr);
        }

        static string ResolveBaseUrl(JObject json)
        {
            // OpenAPI 3.x: servers[0].url
            var server0 = json.SelectToken("servers[0].url")?.ToString();
            if (!string.IsNullOrWhiteSpace(server0)) return server0;

            // Swagger 2.0: schemes[0] + host + basePath
            var scheme = (json["schemes"] as JArray)?.FirstOrDefault()?.ToString() ?? "https";
            var host = json.Value<string>("host") ?? string.Empty;
            var basePath = json.Value<string>("basePath") ?? string.Empty;
            return string.IsNullOrWhiteSpace(host) ? string.Empty : $"{scheme}://{host}{basePath}";
        }

        static bool IsHttpMethod(string m) =>
            m is "GET" or "POST" or "PUT" or "DELETE" or "PATCH" or "HEAD" or "OPTIONS";

        static List<KvRow> BuildParams(JObject op)
        {
            var rows = new List<KvRow>();
            var parameters = op["parameters"] as JArray;
            if (parameters is null) return rows;
            foreach (var p in parameters.OfType<JObject>())
            {
                var inLoc = p.Value<string>("in");
                if (inLoc != "query" && inLoc != "path") continue;
                rows.Add(new KvRow
                {
                    Enabled = true,
                    Name = p.Value<string>("name") ?? string.Empty,
                    Value = string.Empty,
                    Description = p.Value<string>("description") ?? string.Empty,
                });
            }
            return rows;
        }

        static List<KvRow> BuildHeaders(JObject op)
        {
            var rows = new List<KvRow>();
            var parameters = op["parameters"] as JArray;
            if (parameters is null) return rows;
            foreach (var p in parameters.OfType<JObject>())
            {
                if (p.Value<string>("in") != "header") continue;
                rows.Add(new KvRow
                {
                    Enabled = true,
                    Name = p.Value<string>("name") ?? string.Empty,
                    Value = string.Empty,
                    Description = p.Value<string>("description") ?? string.Empty,
                });
            }
            return rows;
        }

        /// <summary>
        /// 构造 Body 默认值(支持 swagger v2 的 parameters[in=body] 与 OpenAPI 3 的 requestBody)
        /// </summary>
        static string BuildBody(JObject op, JObject root, out string bodyType)
        {
            bodyType = "none";

            // OpenAPI 3.x
            var requestBody = op["requestBody"] as JObject;
            if (requestBody is not null)
            {
                var content = requestBody["content"] as JObject;
                if (content is null) return string.Empty;
                var firstMedia = content.Properties().FirstOrDefault();
                if (firstMedia is null) return string.Empty;
                var mediaName = firstMedia.Name;
                bodyType = MapContentTypeToBodyType(mediaName);

                var schema = firstMedia.Value?["schema"] as JObject;
                var sample = SchemaToSample(schema, root);
                return sample is null ? string.Empty : JsonConvert.SerializeObject(sample, Formatting.Indented);
            }

            // Swagger 2.0: parameters[in=body]
            var parameters = op["parameters"] as JArray;
            if (parameters is not null)
            {
                var bodyParam = parameters.OfType<JObject>().FirstOrDefault(p => p.Value<string>("in") == "body");
                if (bodyParam is not null)
                {
                    bodyType = "json";
                    var schema = bodyParam["schema"] as JObject;
                    var sample = SchemaToSample(schema, root);
                    return sample is null ? string.Empty : JsonConvert.SerializeObject(sample, Formatting.Indented);
                }

                if (parameters.OfType<JObject>().Any(p => p.Value<string>("in") == "formData"))
                {
                    bodyType = "form-urlencoded";
                }
            }

            return string.Empty;
        }

        static string MapContentTypeToBodyType(string mediaType) => mediaType switch
        {
            var m when m.Contains("json") => "json",
            "application/x-www-form-urlencoded" => "form-urlencoded",
            "multipart/form-data" => "form-data",
            "text/xml" or "application/xml" => "xml",
            "text/plain" => "text",
            _ => "json",
        };

        /// <summary>
        /// 简易的 schema → 示例值生成(支持 $ref/object/array 基本类型, 不展开循环引用)
        /// </summary>
        static JToken? SchemaToSample(JObject? schema, JObject root, int depth = 0)
        {
            if (schema is null || depth > 6) return null;

            var refPath = schema.Value<string>("$ref");
            if (!string.IsNullOrWhiteSpace(refPath))
            {
                var refSchema = ResolveRef(root, refPath);
                return SchemaToSample(refSchema, root, depth + 1);
            }

            var type = schema.Value<string>("type");
            if (type == "object" || schema["properties"] is JObject)
            {
                var obj = new JObject();
                if (schema["properties"] is JObject props)
                {
                    foreach (var p in props.Properties())
                    {
                        obj[p.Name] = SchemaToSample(p.Value as JObject, root, depth + 1) ?? JValue.CreateNull();
                    }
                }
                return obj;
            }
            if (type == "array")
            {
                var items = schema["items"] as JObject;
                var sample = SchemaToSample(items, root, depth + 1);
                return new JArray(sample ?? JValue.CreateNull());
            }

            return type switch
            {
                "integer" => new JValue(0),
                "number" => new JValue(0.0),
                "boolean" => new JValue(false),
                _ => new JValue(string.Empty),
            };
        }

        static JObject? ResolveRef(JObject root, string refPath)
        {
            // "#/components/schemas/User" or "#/definitions/User"
            if (!refPath.StartsWith("#/")) return null;
            var parts = refPath.Substring(2).Split('/');
            JToken? cur = root;
            foreach (var p in parts)
            {
                cur = cur?[p];
                if (cur is null) return null;
            }
            return cur as JObject;
        }
    }
}
