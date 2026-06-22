using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.Utils.CommandExecutor.Http;
using Sylas.RemoteTasks.Utils.CommandExecutor.Http.Models;
using Sylas.RemoteTasks.Utils.Dtos;
using System;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Utils
{
    /// <summary>
    /// Ai调用实现
    /// </summary>
    public class AiService(IHttpRequestPipeline pipenline, AiConfig aiConfig, ILogger<AiService> logger)
    {
        private readonly IHttpRequestPipeline _pipeline = pipenline;
        private readonly AiConfig _config = aiConfig;
        private readonly ILogger<AiService> _logger = logger;

        /// <summary>
        /// 向AI模型提问并获取回答
        /// </summary>
        /// <param name="question"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<string> AskAsync(string question)
        {
            if (string.IsNullOrWhiteSpace(_config.Server) || string.IsNullOrWhiteSpace(_config.Model))
            {
                throw new Exception("AI 配置不完整: Server 或 Model 为空, 请检查 appsettings.json 的 AiConfig 节");
            }
            var spec = new HttpRequestSpec
            {
                Method = "POST",
                Url = $"{_config.Server.TrimEnd('/')}/chat/completions",
                BodyKind = BodyKind.Json,
                Body = BuildChatBody(_config.Model, question),
                TimeoutSeconds = -1, // AI调用可能需要较长时间，设置为无限超时
                Extractors = [],
                Validators = []
            };

            var response = await _pipeline.SendAsync(spec);
            if (!string.IsNullOrWhiteSpace(response.Error))
            {
                throw new Exception($"AI调用失败: {response.Error}");
            }

            _logger.LogInformation("AI Response status: {Status}, , duration: {Ms}ms", response.Status, response.DurationMs);

            // 解析choices[0].message.content
            var obj = JObject.Parse(response.Body);
            var answer = obj.SelectToken("choices[0].message.content")?.ToString();
            if (string.IsNullOrWhiteSpace(answer))
            {
                throw new Exception($"AI 返回格式异常: {response.Body}");
            }
            return answer;
        }

        static string BuildChatBody(string model, string question) => $$"""
            {
              "model": "{{model}}",
              "max_tokens": 700,
              "messages": [
                {
                  "role": "system",
                  "content": "You are a helpful assistant."
                },
                {
                  "role": "user",
                  "content": "{{question}}"
                }
              ]
            }
            """;
    }
}
