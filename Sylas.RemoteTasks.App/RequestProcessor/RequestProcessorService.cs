using Sylas.RemoteTasks.App.Database.SyncBase;
using Sylas.RemoteTasks.App.Repositories;
using Sylas.RemoteTasks.App.Utils;

namespace Sylas.RemoteTasks.App.RequestProcessor
{
    public class RequestProcessorService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<RequestProcessorService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly HttpRequestProcessorRepository _repository;

        public RequestProcessorService(IConfiguration configuration, ILogger<RequestProcessorService> logger, IServiceProvider serviceProvider, HttpRequestProcessorRepository repository)
        {
            _configuration = configuration;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _repository = repository;
        }
        public async Task ExecuteFromAppsettingsAsync()
        {
            var requestProcessorDispatchItems = _configuration.GetSection("RequestPipeline:RequestProcessorDispatch").GetChildren();
            Dictionary<string, object>? dataContext = null;
            foreach (var requestProcessorDispatchItem in requestProcessorDispatchItems)
            {
                var requestProcessorName = requestProcessorDispatchItem.GetValue<string>("RequestProcessorName") ?? throw new Exception("[RequestProcessorName] is missing from the configuration"); //$"The provided \"{pipelineItem.Key}\" configuration is invalid"
                _logger.LogInformation($"{new string('*', 30)} A New RequestProcessor {requestProcessorName} {new string('*', 30)}");
                var requestProcessorUrl = requestProcessorDispatchItem.GetValue<string>("RequestProcessorUrl") ?? throw new Exception("[RequestProcessorUrl] is missing from the configuration");
                var requestProcessorType = ReflectionHelper.GetTypeByClassName(requestProcessorName);
                var requestProcessorInstance = _serviceProvider.GetService(requestProcessorType) ?? throw new Exception($"未能获取RequestProcessor实例: {requestProcessorName}");
                if (dataContext is not null)
                {
                    // 第一次迭代肯定为null, 但是第二次之后基本就不为null了
                    var dataContextProp = requestProcessorType.GetProperty(nameof(RequestProcessorBase.DataContext)) ?? throw new Exception($"获取{requestProcessorName}的{nameof(RequestProcessorBase.DataContext)}属性失败");
                    dataContextProp.SetValue(requestProcessorInstance, dataContext);
                }

                var startMethod = requestProcessorType.GetMethod("ExecuteStepsFromAppsettingsAsync") ?? throw new Exception($"IRequestProcessor的实现{requestProcessorName}中未找到StartAsync方法");
                var requestProcessorSteps = requestProcessorDispatchItem.GetSection("RequestProcessorSteps").GetChildren();
                var returned = startMethod.Invoke(requestProcessorInstance, new object[] { requestProcessorSteps, requestProcessorUrl });
                if (startMethod.ReturnType == typeof(Task) || (startMethod.ReturnType.IsGenericType && startMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)))
                {
                    var requestProcessor = await (returned as Task<RequestProcessorBase> ?? throw new Exception($"{requestProcessorName}.StartAsync()结果为NULL, 转换为Task失败"));
                    dataContext = requestProcessor.DataContext;
                }
                else
                {
                    dataContext = (returned as RequestProcessorBase ?? throw new Exception($"{requestProcessorName}对象转换为RequestProcessorBase失败")).DataContext;
                }
                if (requestProcessorDispatchItem.GetValue<bool>("Break"))
                {
                    break;
                }
            }
        }

        public async Task ExecuteFromDatabaseAsync()
        {
            var httpRequestProcessors = await _repository.GetPageAsync(1, 1000, "id", true, new DataFilter());
            Dictionary<string, object>? dataContext = null;
            
            foreach (var httpRequestProcessor in httpRequestProcessors.Data)
            {
                var requestProcessorName = httpRequestProcessor.Name ?? throw new Exception("[HttpRequestProcessor.Name] is missing"); //$"The provided \"{pipelineItem.Key}\" configuration is invalid"
                _logger.LogInformation($"{new string('*', 30)} A New RequestProcessor {requestProcessorName} {new string('*', 30)}");
                var requestProcessorUrl = httpRequestProcessor.Url ?? throw new Exception("[HttpRequestProcessor.Url] is missing");
                var requestProcessorType = ReflectionHelper.GetTypeByClassName(requestProcessorName);
                // BOOKMARK: HttpRequestProcessor - 10. DI容器中获取对应的实例对象
                var requestProcessorInstance = _serviceProvider.GetService(requestProcessorType) ?? throw new Exception($"未能获取RequestProcessor实例: {requestProcessorName}");
                if (dataContext is not null)
                {
                    // 第一次迭代肯定为null, 但是第二次之后基本就不为null了
                    var dataContextProp = requestProcessorType.GetProperty(nameof(RequestProcessorBase.DataContext)) ?? throw new Exception($"获取{requestProcessorName}的{nameof(RequestProcessorBase.DataContext)}属性失败");
                    dataContextProp.SetValue(requestProcessorInstance, dataContext);
                }

                var executeMethod = requestProcessorType.GetMethod("ExecuteStepsFromDbAsync") ?? throw new Exception($"IRequestProcessor的实现{requestProcessorName}中未找到StartAsync方法");

                // BOOKMARK: HttpRequestProcessor - 11. 获取所有需要执行的步骤Steps
                var stepsFilters = new List<FilterItem>
                {
                    new FilterItem { FieldName = "id", CompareType = "=", Value = httpRequestProcessor.Id.ToString() }
                };
                var steps = (await _repository.GetStepsPageAsync(1, 1000, "id", true, new DataFilter { FilterItems = stepsFilters })).Data;
                httpRequestProcessor.Steps = steps;

                // BOOKMARK: HttpRequestProcessor - 12. 获取所有需要执行的步骤对应的所有DataHandlers
                foreach (var step in steps)
                {
                    var filterCondition = new FilterItem { FieldName = "StepId", CompareType = "=", Value = step.Id.ToString() };
                    var dataHandlers = (await _repository.GetDataHandlersPageAsync(1, 1000, "id", true, new DataFilter { FilterItems = new List<FilterItem> { filterCondition } })).Data;
                    step.DataHandlers = dataHandlers;
                }

                var returned = executeMethod.Invoke(requestProcessorInstance, new object[] { httpRequestProcessor });
                if (executeMethod.ReturnType == typeof(Task) || (executeMethod.ReturnType.IsGenericType && executeMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)))
                {
                    var requestProcessor = await (returned as Task<RequestProcessorBase> ?? throw new Exception($"{requestProcessorName}.StartAsync()结果为NULL, 转换为Task失败"));
                    dataContext = requestProcessor.DataContext;
                }
                else
                {
                    dataContext = (returned as RequestProcessorBase ?? throw new Exception($"{requestProcessorName}对象转换为RequestProcessorBase失败")).DataContext;
                }
            }
        }
    }
}
