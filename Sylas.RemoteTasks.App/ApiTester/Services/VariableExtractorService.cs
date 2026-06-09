using Sylas.RemoteTasks.App.ApiTester.Models.Dtos;
using Sylas.RemoteTasks.App.ApiTester.Models.Entities;
using Sylas.RemoteTasks.App.ApiTester.Repositories;

namespace Sylas.RemoteTasks.App.ApiTester.Services
{
    /// <summary>
    /// 变量提取服务: 按 ExtractorDto 解析响应 JSON, 写入当前激活环境变量
    /// </summary>
    public class VariableExtractorService(
        ApiTesterRepository repository,
        ILogger<VariableExtractorService> logger)
    {
        private readonly ApiTesterRepository _repo = repository;

        /// <summary>
        /// 执行 extractors, 返回结果 + 持久化到激活环境变量
        /// </summary>
        public async Task PersistVariablesAsync(List<ExtractedVarResult> results)
        {
            if (results is null || results.Count == 0)
            {
                return;
            }
            try
            {
                var envPage = await _repo.Environments.GetPageAsync(new(1, 1000000, null, [new("id", true)]));
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
                logger.LogWarning(ex, "持久化提取变量失败");
            }
        }
    }
}
