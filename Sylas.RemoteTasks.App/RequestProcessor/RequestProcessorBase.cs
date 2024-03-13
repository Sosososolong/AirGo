using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.DataHandlers;
using Sylas.RemoteTasks.App.Models.HttpRequestProcessor;
using Sylas.RemoteTasks.Utils;
using Sylas.RemoteTasks.Utils.Template;
using System.Reflection;

namespace Sylas.RemoteTasks.App.RequestProcessor
{
    public class RequestProcessorBase : IRequestProcessor
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
                PageIndexField = "PageIndex",
                PageIndexParamInQuery = true,
                ParentIdFieldName = string.Empty,
                QueryDictionary = new Dictionary<string, object>(),
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
            DataContext = [];

            _logger.LogCritical("Processor Initialized");
        }
        /// <summary>
        /// 设置分页页码参数名
        /// </summary>
        /// <param name="pageIndexField"></param>
        public void SetPageIndexField(string pageIndexField)
        {
            _requestConfig.PageIndexField = pageIndexField;
        }
        /// <summary>
        /// 处理QueryString参数和Body参数
        /// </summary>
        /// <param name="dataContextDictionary"></param>
        /// <param name="queryJson"></param>
        /// <param name="bodyJson"></param>
        /// <returns></returns>
        protected virtual IEnumerable<RequestConfig> UpdateRequestConfig(Dictionary<string, object> dataContextDictionary, string? queryJson, string bodyJson)
        {
            if (!string.IsNullOrWhiteSpace(queryJson))
            {
                object resolvedQueryJson = TmplHelper.ResolveExpressionValue(queryJson, dataContextDictionary);
                var queryParams = JsonConvert.DeserializeObject<Dictionary<string, object>>(resolvedQueryJson?.ToString() ?? "");
                _requestConfig.QueryDictionary = queryParams;
            }
            if (!string.IsNullOrWhiteSpace(bodyJson) && _requestConfig.RequestMethod.Equals("post", StringComparison.CurrentCultureIgnoreCase))
            {
                var resolvedBodyJson = TmplHelper.ResolveExpressionValue(bodyJson, dataContextDictionary);
                var bodyParams = JsonConvert.DeserializeObject<JToken>(resolvedBodyJson.ToString() ?? "");
                _requestConfig.BodyDictionary = bodyParams;
            }
            return new List<RequestConfig>() { CloneRequestConfig() };
        }

        /// <summary>
        /// 解析参数发送请求并且构建数据上下文, 最后对数据进行对应的操作
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="stepId">指定执行processor的具体某个步骤; 0表示执行所有步骤</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<RequestProcessorBase> ExecuteStepsFromDbAsync(HttpRequestProcessor processor, int stepId)
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

            string requestProcessorUrl = processor.Url;
            // 初始化请求地址
            _requestConfig.Url = requestProcessorUrl;

            var requestProcessorStepsArray = processor.Steps.ToArray();
            if (stepId > 0)
            {
                var targetStep = requestProcessorStepsArray.FirstOrDefault(x => x.Id == stepId) ?? throw new Exception($"没有找到指定步骤{stepId}");
                var previousStep = requestProcessorStepsArray.FirstOrDefault(x => x.OrderNo < targetStep.OrderNo);
                if (previousStep is not null)
                {
                    if (previousStep.EndDataContext.Length > 0)
                    {
                        DataContext = JsonConvert.DeserializeObject<Dictionary<string, object>>(previousStep.EndDataContext) ?? throw new Exception($"继承上一个步骤的数据上下文失败: {previousStep.EndDataContext}");
                    }
                }

                // 只执行指定步骤
                requestProcessorStepsArray = new HttpRequestProcessorStep[] { targetStep };
            }
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

                // Step - DataContextBuilders
                List<string> dataContextTmpls = JsonConvert.DeserializeObject<List<string>>(step.DataContextBuilder) ?? [];
                _logger.LogDebug($"call ResolveAsync, 获取DataContext模板配置 {string.Join(',', dataContextTmpls)}");
                // Step - DataHandlers
                var dataHandlers = step.DataHandlers;
                // BOOKMARK: HttpRequestProcessor - 11. 处理Step - 获取所有需要执行的步骤对应的所有DataHandlerInfos

                // Step - 最后一个执行完之后如果有数据, 是否要重新从第一个开始循环执行
                if (stepIndex == stepCount - 1 && processor.StepCirleRunningWhenLastStepHasData)
                {
                    // 预期的值n需要-1, 因为for循环里面会执行++操作正好等于预期值n
                    stepIndex = -1;
                    backtrackingNextStep = true;
                }
                // Step - 初始化DataContext
                var dataContextItems = JsonConvert.DeserializeObject<List<string>>(step.PresetDataContext) ?? [];
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
                // requestConfigs - 当前步骤可能会生成多个请求(参数)
                var requestConfigs = UpdateRequestConfig(DataContext, step.Parameters, step.RequestBody);
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
                    await ExecuteDataHandlersAsync(dataHandlers);
                }

                // BOOKMARK: 持久化当前步骤结束时候的上下文数据 1.准备工作, 当前Http请求获取到的数据$data不要, $data数据量可能巨大, 下一步骤需要的数据应该先从$data中提取出来
                var persistingDataContex = new Dictionary<string, object>();
                foreach (var item in DataContext)
                {
                    if (item.Key != "$data")
                    {
                        persistingDataContex.Add(item.Key, item.Value);
                    }
                }
                step.EndDataContext = JsonConvert.SerializeObject(persistingDataContex);
                #endregion
            }

            return this;
        }
        /// <summary>
        /// 克隆一个RequestConfig对象(Http请求参数)
        /// </summary>
        /// <returns></returns>
        public RequestConfig CloneRequestConfig()
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
            logger?.LogDebug($"{nameof(RequestAndBuildDataContextAsync)}, 应用参数, 准备发送请求: {_requestConfig.Url}{Environment.NewLine}Query:{JsonConvert.SerializeObject(_requestConfig.QueryDictionary)}{Environment.NewLine}Body:{JsonConvert.SerializeObject(_requestConfig.BodyDictionary)}");
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

            logger?.LogDebug($"{nameof(RequestAndBuildDataContextAsync)}, 请求结束, 获取data: {data.Count()}, 构建数据上下文");

            var buildDetail = DataContext.BuildDataContextBySource(data, dataContextTmpls, logger);
            return buildDetail;
        }
        async Task ExecuteDataHandlersAsync(IEnumerable<HttpRequestProcessorStepDataHandler> dataHandlers)
        {
            dataHandlers = dataHandlers.OrderBy(x => x.OrderNo).ToList();
            foreach (var dataHandler in dataHandlers)
            {

                var handler = dataHandler.DataHandler;
                var handlerParameters = JsonConvert.DeserializeObject<List<string>>(dataHandler.ParametersInput) ?? [];
                var opParameters = handlerParameters.Select(x => TmplHelper.ResolveExpressionValue(x, DataContext)).ToArray();

                Type dataHandlerType = ReflectionHelper.GetTypeByClassName(handler);
                MethodInfo handlerStartMethod = dataHandlerType.GetMethod("StartAsync") ?? throw new Exception($"DataHandler {handler} 没有实现Operation方法");
                var handlerInstance = _serviceProvider.GetService(dataHandlerType);

                // BOOKMARK: DataHandler 调用StartAsync()方法
                var invokeResult = handlerStartMethod.Invoke(handlerInstance, new object[] { opParameters });
                if (handlerStartMethod.ReturnType == typeof(Task) || handlerStartMethod.ReturnType.IsGenericType && handlerStartMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    await (invokeResult as Task ?? throw new Exception($"{handler}.Start()结果为NULL, 而非Task"));
                }
            }
        }
    }
}
