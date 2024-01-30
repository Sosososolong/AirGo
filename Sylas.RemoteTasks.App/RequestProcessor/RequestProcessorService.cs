using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.App.Repositories;
using Sylas.RemoteTasks.Utils;

namespace Sylas.RemoteTasks.App.RequestProcessor
{
    public class RequestProcessorService(ILogger<RequestProcessorService> logger, IServiceProvider serviceProvider, HttpRequestProcessorRepository repository)
    {
        public async Task<OperationResult> ExecuteFromDatabaseAsync(int[] ids, int stepId = 0)
        {
            var idsString = string.Join(',', ids);
            var httpRequestProcessors = await repository.GetPageAsync(1, 1000, "id", true, new DataFilter() { FilterItems = new List<FilterItem> { new FilterItem { CompareType = "in", FieldName = "id", Value = idsString } } });
            Dictionary<string, object>? dataContext = null;

            if (httpRequestProcessors.Data is null || !httpRequestProcessors.Data.Any())
            {
                return new OperationResult(false, $"没有找到Id为{idsString}的HttpRequestProcessor");
            }

            foreach (var httpRequestProcessor in httpRequestProcessors.Data)
            {
                var requestProcessorName = httpRequestProcessor.Name ?? throw new Exception("[HttpRequestProcessor.Name] is missing"); //$"The provided \"{pipelineItem.Key}\" configuration is invalid"
                logger.LogInformation($"{new string('*', 30)} A New RequestProcessor {requestProcessorName} {new string('*', 30)}");
                var requestProcessorUrl = httpRequestProcessor.Url ?? throw new Exception("[HttpRequestProcessor.Url] is missing");
                var requestProcessorType = ReflectionHelper.GetTypeByClassName(requestProcessorName);
                // BOOKMARK: HttpRequestProcessor - 10. DI容器中获取对应的实例对象
                var requestProcessorInstance = serviceProvider.GetService(requestProcessorType) ?? throw new Exception($"未能获取RequestProcessor实例: {requestProcessorName}");
                if (dataContext is not null)
                {
                    // 第一次迭代肯定为null, 但是第二次之后基本就不为null了
                    var dataContextProp = requestProcessorType.GetProperty(nameof(RequestProcessorBase.DataContext)) ?? throw new Exception($"获取{requestProcessorName}的{nameof(RequestProcessorBase.DataContext)}属性失败");
                    dataContextProp.SetValue(requestProcessorInstance, dataContext);
                }

                var executeStepMethod = requestProcessorType.GetMethod("ExecuteStepsFromDbAsync") ?? throw new Exception($"IRequestProcessor的实现{requestProcessorName}中未找到StartAsync方法");

                // BOOKMARK: HttpRequestProcessor - 11. 获取所有需要执行的步骤Steps(仓储中已获取)
                if (!httpRequestProcessor.Steps.Any())
                {
                    return new OperationResult(false, "没有需要执行的Step");
                }

                // BOOKMARK: HttpRequestProcessor - 12. 获取所有需要执行的步骤对应的所有DataHandlers(仓储中已获取)

                var returned = executeStepMethod.Invoke(requestProcessorInstance, new object[] { httpRequestProcessor, stepId });
                if (executeStepMethod.ReturnType == typeof(Task) || (executeStepMethod.ReturnType.IsGenericType && executeStepMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)))
                {
                    var requestProcessor = await (returned as Task<RequestProcessorBase> ?? throw new Exception($"{requestProcessorName}.StartAsync()结果为NULL, 转换为Task失败"));
                    dataContext = requestProcessor.DataContext;
                }
                else
                {
                    dataContext = (returned as RequestProcessorBase ?? throw new Exception($"{requestProcessorName}对象转换为RequestProcessorBase失败")).DataContext;
                }

                // BOOKMARK: 持久化当前步骤结束时候的上下文数据 2.持久化到数据库, 以便下一次可以直接执行它的下一步
                foreach (var step in httpRequestProcessor.Steps.Where(x => x.Id == stepId || stepId == 0))
                {
                    await repository.UpdateStepAsync(step);
                }

                if (stepId > 0)
                {
                    break;
                }
            }
            return new OperationResult(true);
        }
    }
}
