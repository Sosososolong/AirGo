using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.Database.SyncBase;
using Sylas.RemoteTasks.App.DataHandlers;
using Sylas.RemoteTasks.App.Models.HttpRequestProcessor;
using Sylas.RemoteTasks.App.Repositories;
using Sylas.RemoteTasks.App.Utils;
using Sylas.RemoteTasks.App.Utils.Template;
using System.Reflection;
using static Sylas.RemoteTasks.App.RemoteHostModule.StartupHelper;

namespace Sylas.RemoteTasks.App.RequestProcessor
{
    public abstract class RequestProcessorBase : IRequestProcessor
    {
        protected RequestConfig _requestConfig;
        private readonly IConfiguration _configuration;
        protected readonly ILogger<RequestProcessorBase> _logger;
        protected readonly IServiceProvider _serviceProvider;
        public Dictionary<string, object> DataContext { get; set; }
        public RequestProcessorBase(IConfiguration configuration, ILogger<RequestProcessorBase> logger, IServiceProvider serviceProvider)
        {
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
                Token = ""
            };
            _configuration = configuration;
            _logger = logger;
            _serviceProvider = serviceProvider;
            DataContext = new Dictionary<string, object>();
            // TODO: 添加初始化配置
            //DataContext["$dataModelCodes"] = new JArray("yct_xkzygs");

            _logger.LogCritical("Processor Initialized");
        }
        protected abstract IEnumerable<RequestConfig> UpdateRequestConfig(Dictionary<string, object> dataContext, List<string> values);
        protected abstract IEnumerable<RequestConfig> UpdateRequestConfig2(Dictionary<string, object> dataContextDictionary, string? queryJson, string bodyJson);
        /// <summary>
        /// 解析参数发送请求并且构建数据上下文, 最后对数据进行对应的操作
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<RequestProcessorBase> ExecuteStepsFromAppsettingsAsync(IEnumerable<IConfigurationSection> requestProcessorSteps, string requestProcessorUrl)
        {
            // 初始化请求地址
            _requestConfig.Url = requestProcessorUrl;

            var requestProcessorStepsArray = requestProcessorSteps.ToArray();
            var stepCount = requestProcessorStepsArray.Length;
            var backtrackingNextStep = false;
            var currentStepIndex = 0;
            for (int stepIndex = 0; stepIndex < stepCount; stepIndex++)
            {
                currentStepIndex = stepIndex;
                // { "Parameters": "", "DataContextBuilder": [] }
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
                    else if (stepDetailItem.Key == "DataContextBuilder")
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
                    else if (stepDetailItem.Key == "BacktrackingStepIndex")
                    {
                        // 预期的值n需要-1, 因为for循环里面会执行++操作正好等于预期值n
                        stepIndex = Convert.ToInt16(stepDetailItem.Value) - 1;
                        backtrackingNextStep = true;
                    }
                    else if (stepDetailItem.Key == "DataContext")
                    {
                        // 初始化DataContext
                        var dataContextItems = stepDetailItem.GetChildren();
                        foreach (var dataContextItem in dataContextItems)
                        {
                            DataContext[dataContextItem.Key] = dataContextItem.Value ?? "";
                        }
                    }
                    else
                    {
                        _logger?.LogError($"无效的Task: {stepDetailItem.Key}");
                    }
                }
                var requestConfigs = UpdateRequestConfig(DataContext, values);

                #region 处理当前Step所有的Request, Datahandler
                foreach (var requestConfig in requestConfigs)
                {
                    _requestConfig = requestConfig;
                    // 1. 发起请求, 构建DataContext
                    var buildDetails = await RequestAndBuildDataContextAsync(dataContextTmpls, _logger);

                    if (backtrackingNextStep)
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
                            stepIndex = currentStepIndex;
                            _logger.LogInformation($"无需回溯, 还原stepIndex: {currentStepIndex}");
                        }
                    }
                    // 2. DataHandler处理数据
                    await ExecuteOperationAsync(dataContextHandlers);
                }
                #endregion
            }

            return this;
        }
        /// <summary>
        /// 解析参数发送请求并且构建数据上下文, 最后对数据进行对应的操作
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<RequestProcessorBase> ExecuteStepsFromDbAsync(HttpRequestProcessor processor)
        {
            if (!string.IsNullOrWhiteSpace(processor.Headers) && processor.Headers.Length > 2)
            {
                var headers = JsonConvert.DeserializeObject<Dictionary<string, string>>(processor.Headers);
                if (headers is not null)
                {
                    foreach (var header in headers)
                    {
                        if (header.Key == "Authorization" && header.Value.StartsWith("Bearer"))
                        {
                            _requestConfig.Token = header.Value.Replace("Bearer ", "");
                        }
                    }
                }
                else
                {
                    _logger.LogCritical("请求头{headers}格式不正确", processor.Headers);
                }
            }

            IEnumerable<HttpRequestProcessorStep> requestProcessorSteps = processor.Steps;
            string requestProcessorUrl = processor.Url;
            // 初始化请求地址
            _requestConfig.Url = requestProcessorUrl;

            var requestProcessorStepsArray = requestProcessorSteps.ToArray();
            var stepCount = requestProcessorStepsArray.Length;
            var backtrackingNextStep = false;
            var currentStepIndex = 0;
            for (int stepIndex = 0; stepIndex < stepCount; stepIndex++)
            {
                currentStepIndex = stepIndex;
                var step = requestProcessorStepsArray[stepIndex];

                #region 处理执行当前步骤所需要的参数
                // 跟HttpRequestProcessorStepDataHandler相比差不多, 可以考虑删除DataHandlerInfo类型
                var dataHandlerInfos = new List<DataHandlerInfo>();
                _logger.LogInformation($"{new string('*', 20)} New RequestProcessor Step {stepIndex} {new string('*', 20)}");

                // Step - Parameters
                List<string> stepParameters = (step.Parameters ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                _logger.LogDebug($"call ResolveAsync, 获取请求参数: {step.Parameters ?? ""}");
                // Step - DataContextBuilders
                List<string> dataContextTmpls = JsonConvert.DeserializeObject<List<string>>(step.DataContextBuilder) ?? new();
                _logger.LogDebug($"call ResolveAsync, 获取DataContext模板配置 {string.Join(',', dataContextTmpls)}");
                // Step - DataHandlers
                var dataHandlers = step.DataHandlers;
                foreach (var dataHandler in dataHandlers)
                {
                    var handlerName = dataHandler.DataHandler;
                    var handlerParameters = JsonConvert.DeserializeObject<List<string>>(dataHandler.ParametersInput) ?? new List<string>();
                    // BOOKMARK: HttpRequestProcessor - 11. 处理Step - 获取所有需要执行的步骤对应的所有DataHandlerInfos
                    dataHandlerInfos.Add(new DataHandlerInfo(handlerName, handlerParameters));
                    _logger.LogDebug($"call ResolveAsync, 获取DataHandlers, DataHandler: {handlerName}, 参数:{Environment.NewLine}{string.Join(Environment.NewLine, handlerParameters)}");
                }
                // Step - 最后一个执行完之后如果有数据, 是否要重新从第一个开始循环执行
                if (stepIndex == stepCount - 1 && processor.StepCirleRunningWhenLastStepHasData)
                {
                    // 预期的值n需要-1, 因为for循环里面会执行++操作正好等于预期值n
                    stepIndex = -1;
                    backtrackingNextStep = true;
                }
                // Step - 初始化DataContext
                var dataContextItems = JsonConvert.DeserializeObject<List<string>>(step.PresetDataContext) ?? new List<string>();
                foreach (var dataContextItem in dataContextItems)
                {
                    var dckv = dataContextItem.Split('=', StringSplitOptions.RemoveEmptyEntries);
                    if (dckv.Length != 2)
                    {
                        continue;
                    }
                    DataContext[dckv[0]] = dckv[1];
                }
                #endregion

                #region 处理当前Step所有的Request, Datahandler
                var requestConfigs = string.IsNullOrEmpty(step.RequestBody)
                    ? UpdateRequestConfig(DataContext, stepParameters)
                    : UpdateRequestConfig2(DataContext, step.Parameters, step.RequestBody);
                foreach (var requestConfig in requestConfigs)
                {
                    _requestConfig = requestConfig;
                    // 1. 发起请求, 构建DataContext
                    var buildDetails = await RequestAndBuildDataContextAsync(dataContextTmpls, _logger);

                    if (backtrackingNextStep)
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
                            stepIndex = currentStepIndex;
                            _logger.LogInformation($"无需回溯, 还原stepIndex: {currentStepIndex}");
                        }
                    }
                    // 2. DataHandler处理数据
                    await ExecuteOperationAsync(dataHandlerInfos);
                }
                #endregion
            }

            return this;
        }
        public RequestConfig CloneReqeustConfig()
        {
            _requestConfig.QueryDictionary ??= new Dictionary<string, object>();
            var copied = MapHelper<RequestConfig, RequestConfig>.Map(_requestConfig);

            // 处理QueryDictionary属性是同一个引用的问题
            copied.QueryDictionary = new Dictionary<string, object>();
            foreach (var key in _requestConfig.QueryDictionary.Keys)
            {
                copied.QueryDictionary[key] = _requestConfig.QueryDictionary[key];
            }
            return copied;
        }
        /// <summary>
        /// 发送请求,构建数据上下文
        /// </summary>
        /// <param name="dataContextTmpls"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        async Task<Dictionary<string, object?>> RequestAndBuildDataContextAsync(List<string> dataContextTmpls, ILogger<RequestProcessorBase> logger)
        {
            logger?.LogDebug($"call {nameof(RequestAndBuildDataContextAsync)}, 应用参数, 准备发送请求: {_requestConfig.Url}{Environment.NewLine}Query:{JsonConvert.SerializeObject(_requestConfig.QueryDictionary)}{Environment.NewLine}Body:{JsonConvert.SerializeObject(_requestConfig.BodyDictionary)}");
            IEnumerable<JToken>? data = null;
            try
            {
                DataContext["$QueryDictionary"] = _requestConfig.QueryDictionary ?? new Dictionary<string, object>();
                DataContext["$BodyDictionary"] = _requestConfig.BodyDictionary ?? new JObject();
                data = await RemoteHelpers.FetchAllDataFromApiAsync(_requestConfig) ?? throw new Exception($"查询记录失败, Query String: {JsonConvert.SerializeObject(_requestConfig.QueryDictionary)}{Environment.NewLine}PayLoad {JsonConvert.SerializeObject(_requestConfig.QueryDictionary)}");
            }
            catch (Exception)
            {
                throw;
            }
            // TODO: 去掉RequerstConfig的Data属性
            _requestConfig.Data = null;

            logger?.LogDebug($"call {nameof(RequestAndBuildDataContextAsync)}, 请求结束, 获取data: {data.Count()}, 构建数据上下文");

            var buildDetail = DataContext.BuildDataContextBySource(data, dataContextTmpls, logger);
            return buildDetail;
        }
        async Task ExecuteOperationAsync(List<DataHandlerInfo> dataHandlers)
        {
            foreach (var dataHandler in dataHandlers)
            {
                var handler = dataHandler.Handler;
                var opParameters = dataHandler.Parameters.Select(x => TmplHelper.ResolveVariableValue(DataContext, x)).ToArray();

                Type dataHandlerType = ReflectionHelper.GetTypeByClassName(handler);
                MethodInfo handlerStartMethod = dataHandlerType.GetMethod("StartAsync") ?? throw new Exception($"DataHandler {handler} 没有实现Operation方法");
                var opInstance = _serviceProvider.GetService(dataHandlerType);

                // BOOKMARK: DataHandler 调用StartAsync()方法
                var invokeResult = handlerStartMethod.Invoke(opInstance, new object[] { opParameters });
                if (handlerStartMethod.ReturnType == typeof(Task) || handlerStartMethod.ReturnType.IsGenericType && handlerStartMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    await (invokeResult as Task ?? throw new Exception($"{handler}.Start()结果为NULL, 而非Task"));
                }
            }
        }
    }
}
