using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.ApiTester.Models.Dtos;
using Sylas.RemoteTasks.App.ApiTester.Models.Entities;
using Sylas.RemoteTasks.App.ApiTester.Repositories;
using Sylas.RemoteTasks.Database;
using Sylas.RemoteTasks.Utils.Template;

namespace Sylas.RemoteTasks.App.ApiTester.Services
{
    /// <summary>
    /// 变量提取服务: 按 ExtractorDto 解析响应 JSON, 写入当前激活环境变量
    /// </summary>
    public class VariableExtractorService(
        ApiTesterRepository repository,
        IDatabaseProvider db,
        ILogger<VariableExtractorService> logger)
    {
        private readonly ApiTesterRepository _repo = repository;
        private readonly IDatabaseProvider _db = db;
        private readonly ILogger<VariableExtractorService> _logger = logger;

        /// <summary>
        /// 执行 extractors, 返回结果 + 持久化到激活环境变量
        /// </summary>
        public async Task<List<ExtractedVarResult>> ExtractAsync(string responseBody, List<ExtractorDto> extractors, Dictionary<string, object?> variableContext)
        {
            var results = new List<ExtractedVarResult>();
            if (extractors is null || extractors.Count == 0 || string.IsNullOrWhiteSpace(responseBody))
                return results;

            JToken? root = null;
            try { root = JToken.Parse(responseBody); }
            catch
            {
                _logger.LogDebug("响应非 JSON, 跳过变量提取");
                return results;
            }

            foreach (var ex in extractors)
            {
                if (string.IsNullOrWhiteSpace(ex.VarName)) continue;
                try
                {
                    var value = ResolveExtractor(root, ex, variableContext);
                    var strVal = value?.ToString() ?? string.Empty;
                    results.Add(new ExtractedVarResult { Name = ex.VarName, Value = strVal });
                    variableContext[ex.VarName] = strVal;
                }
                catch (Exception exErr)
                {
                    _logger.LogWarning(exErr, "提取变量 {Name} 失败", ex.VarName);
                    results.Add(new ExtractedVarResult { Name = ex.VarName, Value = string.Empty });
                }
            }

            // 持久化到当前激活环境
            await PersistVariablesAsync(results);
            return results;
        }

        static JToken? ResolveExtractor(JToken root, ExtractorDto ex, Dictionary<string, object?> ctx)
        {
            // 1) 沿 dataPath 用 SelectToken 定位
            var node = string.IsNullOrWhiteSpace(ex.DataPath) ? root : root.SelectToken(ex.DataPath);
            if (node is null) return null;

            // 2) 若是数组 + 有 filter, 先过滤
            if (node is JArray arr && ex.Filter is not null && !string.IsNullOrWhiteSpace(ex.Filter.FieldName))
            {
                var matchValue = TmplHelper.ResolveExpressionValue(ex.Filter.MatchValue ?? string.Empty, ctx)?.ToString() ?? string.Empty;
                JToken? matched = null;
                foreach (var item in arr)
                {
                    var v = item.SelectToken(ex.Filter.FieldName)?.ToString();
                    if (string.Equals(v, matchValue, StringComparison.Ordinal))
                    {
                        matched = item;
                        break;
                    }
                }
                if (matched is null) return null;
                node = matched;
            }
            else if (node is JArray arr2 && arr2.Count > 0)
            {
                // 没有过滤但是数组, 取第一项
                node = arr2[0];
            }

            // 3) 取 field 字段
            if (string.IsNullOrWhiteSpace(ex.Field)) return node;
            return node.SelectToken(ex.Field);
        }

        async Task PersistVariablesAsync(List<ExtractedVarResult> results)
        {
            if (results.Count == 0) return;
            try
            {
                var envPage = await _repo.Environments.GetPageAsync(new(1, int.MaxValue, null, [new("id", true)]));
                var activeEnv = envPage.Data.FirstOrDefault(e => e.IsActive) ?? envPage.Data.FirstOrDefault();
                if (activeEnv is null) return;

                var existed = await _repo.GetVariablesByEnvAsync(activeEnv.Id);
                var byName = existed.Data.ToDictionary(v => v.Name, v => v, StringComparer.OrdinalIgnoreCase);
                foreach (var r in results)
                {
                    if (byName.TryGetValue(r.Name, out var v))
                    {
                        v.Value = r.Value;
                        await _repo.Variables.UpdateAsync(v);
                    }
                    else
                    {
                        await _repo.Variables.AddAsync(new ApiVariable
                        {
                            EnvironmentId = activeEnv.Id,
                            Name = r.Name,
                            Value = r.Value,
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "持久化提取变量失败");
            }
        }
    }
}
