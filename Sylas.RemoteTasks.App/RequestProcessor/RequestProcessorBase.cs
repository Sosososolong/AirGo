using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.Operations;
using Sylas.RemoteTasks.App.Utils;
using Sylas.RemoteTasks.App.Utils.Template;
using System.Reflection;
using static Sylas.RemoteTasks.App.RemoteHostModule.StartupHelper;

namespace Sylas.RemoteTasks.App.RequestProcessor
{
    public abstract class RequestProcessorBase : IRequestProcessor
    {
        protected RequestConfig _requestConfig;
        protected readonly ILogger<RequestProcessorBase> _logger;
        protected readonly IServiceProvider _serviceProvider;
        public Dictionary<string, object> DataContext { get; set; }
        public RequestProcessorBase(IConfiguration configuration, ILogger<RequestProcessorBase> logger, IServiceProvider serviceProvider)
        {
            var token = configuration["RequestPipeline:Token"] ?? throw new Exception("Token was not found");
            _requestConfig = new()
            {
                Url = "",
                FailMsg = "",
                IdFieldName = "id",
                PageIndexField = "",
                PageIndexParamInQuery = false,
                ParentIdFieldName = string.Empty,
                QueryDictionary = null,
                BodyDictionary = null,
                RequestMethod = "post",
                ResponseDataField = "data",
                ResponseOkField = "code",
                ResponseOkValue = "1",
                Token = token
            };
            _logger = logger;
            _serviceProvider = serviceProvider;
            DataContext = new Dictionary<string, object>();
        }
        protected abstract IEnumerable<RequestConfig> UpdateRequestConfig(Dictionary<string, object> dataContext, List<string> values);
        /// <summary>
        /// 解析参数发送请求并且构建数据上下文, 最后对数据进行对应的操作
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<RequestProcessorBase> StartAsync(IEnumerable<IConfigurationSection> requestProcessorSteps, string requestProcessorUrl)
        {
            // 初始化请求地址
            _requestConfig.Url = requestProcessorUrl;

            var requestProcessorStepsArray = requestProcessorSteps.ToArray();
            var stepCount = requestProcessorStepsArray.Length;
            var loopedNextStep = false;
            for (int stepIndex = 0; stepIndex < stepCount; stepIndex++)
            {
                // { "Parameters": "", "DataContext": [] }
                var requestProcessorStep = requestProcessorStepsArray[stepIndex];
                var stepDetail = requestProcessorStep.GetChildren();
                List<string> values = new();
                var dataContextTmpls = new List<string>();
                var dataContextHandlers = new List<DataHandlerInfo>();
                _logger.LogInformation($"{new string('*', 20)} New RequestProcessor Step {stepIndex} {new string('*', 20)}");
                foreach (var stepDetailItem in stepDetail)
                {
                    if (stepDetailItem.Key == "Parameters")
                    {
                        values = (stepDetailItem.Value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                        _logger?.LogDebug($"call ResolveAsync, 获取请求参数 {stepDetailItem.Value}");
                    }
                    else if (stepDetailItem.Key == "DataContext")
                    {
                        dataContextTmpls = stepDetailItem.GetChildren().Select(x => x.Value ?? "").ToList();
                        _logger?.LogDebug($"call ResolveAsync, 获取DataContext模板配置 {string.Join(',', dataContextTmpls)}");
                    }
                    else if (stepDetailItem.Key == "DataHandlers")
                    {
                        var dataHandlers = stepDetailItem.GetChildren();
                        foreach (var dataHandler in dataHandlers)
                        {
                            var handlerName = dataHandler.GetValue<string>("Handler") ?? throw new Exception("没有找到Handler");
                            var handlerParameters = dataHandler.GetSection("Parameters").GetChildren().Select(x => x.Value ?? "").ToList();
                            dataContextHandlers.Add(new DataHandlerInfo(handlerName, handlerParameters));
                            _logger?.LogDebug($"call ResolveAsync, 获取DataHandlers, DataHandler: {handlerName}, 参数:{Environment.NewLine}{string.Join(Environment.NewLine, handlerParameters)}");
                        }
                    }
                    else if (stepDetailItem.Key == "LoopNextStepIndex")
                    {
                        // 预期的值n需要-1, 因为for循环里面会执行++操作正好等于预期值n
                        stepIndex = Convert.ToInt16(stepDetailItem.Value) - 1;
                        loopedNextStep = true;
                    }
                    else
                    {
                        throw new Exception($"无效的Task: {stepDetailItem.Key}");
                    }
                }
                var requestConfigs = UpdateRequestConfig(DataContext, values);
                var requestConfigCount = requestConfigs.Count();
                var shouldNotLoop = false;

                #region 处理当前Step所有的Request, Datahandler
                foreach (var requestConfig in requestConfigs)
                {
                    _requestConfig = requestConfig;
                    // 1. 发起请求, 构建DataContext
                    var buildDetails = await RequestAndBuildDataContextAsync(dataContextTmpls, requestConfigCount > 1, _logger);

                    if (loopedNextStep)
                    {
                        // 只要重置一项成功就算成功, 否则就是重置失败(认为是因为返回的数据无效, 不需要迭代了)
                        var resetDataContextSuccess = false;
                        foreach (var resetDataContextItem in buildDetails)
                        {
                            var resetValue = resetDataContextItem.Value;
                            if (!(resetValue is null || (resetValue is JArray itemJArray && itemJArray.Count == 0) || (resetValue is IEnumerable<JToken> itemList && !itemList.Any())))
                            {
                                // DataContext只要有一项重置成功就设为true
                                resetDataContextSuccess = true;
                                break;
                            }
                        }
                        if (!resetDataContextSuccess)
                        {
                            shouldNotLoop = true;
                            break;
                        }
                    }
                    // 2. DataHandler处理数据
                    await ExecuteOperationAsync(dataContextHandlers);
                }
                #endregion

                if (loopedNextStep && shouldNotLoop)
                {
                    _logger.LogInformation($"迭代Request Processor Steps的时候, 获取数据为空, Step Break");
                    break;
                }
            }

            return this;
        }
        /// <summary>
        /// 发送请求,构建数据上下文
        /// </summary>
        /// <param name="dataContextTmpls"></param>
        /// <param name="multiRequests">表示这一个步骤里面将会有多个请求, 此时请求结果需要合并</param>
        /// <param name="logger"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        async Task<Dictionary<string, object?>> RequestAndBuildDataContextAsync(List<string> dataContextTmpls, bool multiRequests, ILogger<RequestProcessorBase> logger)
        {
            logger?.LogDebug($"call {nameof(RequestAndBuildDataContextAsync)}, 应用参数, 准备发送请求: {_requestConfig.Url}{Environment.NewLine}Query:{JsonConvert.SerializeObject(_requestConfig.QueryDictionary)}{Environment.NewLine}Body:{JsonConvert.SerializeObject(_requestConfig.BodyDictionary)}");
            IEnumerable<JToken>? data = null;
            try
            {
                data = await RemoteHelpers.FetchAllDataFromApiAsync(_requestConfig) ?? throw new Exception($"查询记录失败, Query String: {JsonConvert.SerializeObject(_requestConfig.QueryDictionary)}{Environment.NewLine}PayLoad {JsonConvert.SerializeObject(_requestConfig.QueryDictionary)}");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex.Message);
                return new Dictionary<string, object?>();
            }
            // TODO: 去掉RequerstConfig的Data属性
            _requestConfig.Data = null;

            logger?.LogDebug($"call {nameof(RequestAndBuildDataContextAsync)}, 请求结束, 获取data: {data.Count()}, 构建数据上下文");
            var buildDetail = DataContext.BuildDataContextBySource(data, dataContextTmpls, multiRequests, logger);
            return buildDetail;
        }
        async Task ExecuteOperationAsync(List<DataHandlerInfo> dataHandlers)
        {
            foreach (var dataHandler in dataHandlers)
            {
                var handler = dataHandler.Handler;
                var opParameters = dataHandler.Parameters.Select(x => TmplHelper.GetTmplValueFromDataContext(DataContext, x)).ToArray();

                Type opType = ReflectionHelper.GetTypeByClassName(handler);
                MethodInfo operationMethod = opType.GetMethod("Start") ?? throw new Exception($"DataHandler {handler} 没有实现Operation方法");
                var opInstance = _serviceProvider.GetService(opType);
                var invokeResult = operationMethod.Invoke(opInstance, opParameters);
                if (operationMethod.ReturnType == typeof(Task) || operationMethod.ReturnType.IsGenericType && operationMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    await (invokeResult as Task ?? throw new Exception($"{handler}.Start()结果为NULL, 而非Task"));
                }
            }
        }
    }
}
