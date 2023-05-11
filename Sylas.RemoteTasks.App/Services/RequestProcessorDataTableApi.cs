using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.Operations;
using Sylas.RemoteTasks.App.Utils;
using Sylas.RemoteTasks.App.Utils.Template;
using System.Reflection;
using static Sylas.RemoteTasks.App.RemoteHostModule.StartupHelper;

namespace Sylas.RemoteTasks.App.Services
{
    public class RequestProcessorDataTableApi : IRequestProcessor
    {
        private RequestConfig _requestConfig;
        private readonly ILogger<RequestProcessorDataTableApi> _logger;
        private readonly IServiceProvider _serviceProvider;

        public RequestProcessorDataTableApi(IConfiguration configuration, ILogger<RequestProcessorDataTableApi> logger, IServiceProvider serviceProvider)
        {
            var gateway = configuration["RequestPipeline:DataSourceGateway"] ?? throw new ArgumentNullException("DataSourceGateway");
            var token = configuration["RequestPipeline:Token"] ?? throw new ArgumentNullException("Token");
            _requestConfig = new()
            {
                Url = $"{gateway}/form/api/DataSource/GetDataTable",
                FailMsg = "",
                IdFieldName = "id",
                PageIndexField = "PageIndex",
                PageIndexParamInQuery = true,
                ParentIdFieldName = string.Empty,
                QueryDictionary = new Dictionary<string, object> { { "Db", "" }, { "Table", "" }, { "PageIndex", 1 }, { "PageSize", 5000 }, { "IsAsc", true } },
                BodyDictionary = new Dictionary<string, object> { { "FilterItems", new List<Dictionary<string, object>> { new Dictionary<string, object> { { "FieldName", "" }, { "CompareType", "" }, { "Value", "" } } } } },
                RequestMethod = "post",
                ResponseDataField = "data",
                ResponseOkField = "code",
                ResponseOkValue = "1",
                Token = token
            };
            _logger = logger;
            _serviceProvider = serviceProvider;
        }
        /// <summary>
        /// 解析参数发送请求并且构建数据上下文, 最后对数据进行对应的操作
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<RequestProcessorDataTableApi> StartAsync(IEnumerable<IConfigurationSection> requestProcessorSteps)
        {
            var dataContextDictionary = new Dictionary<string, object>();
            foreach (var requestItem in requestProcessorSteps)
            {
                // { "Parameters": "", "DataContext": [] }
                var parameters = requestItem.GetChildren();
                List<string> values = new();
                var dataContextTmpls = new List<string>();
                var dataContextHandlers = new List<DataHandler>();
                _logger.LogDebug($"{new string('*', 20)} Start A Request {new string('*', 20)}");
                foreach (var parameter in parameters)
                {
                    if (parameter.Key == "DataContext")
                    {
                        dataContextTmpls = parameter.GetChildren().Select(x => x.Value ?? "").ToList();
                        _logger?.LogDebug($"call ResolveAsync, 获取DataContext模板配置 {string.Join(',', dataContextTmpls)}");
                    }
                    else if (parameter.Key == "DataHandlers")
                    {
                        var dataHandlers = parameter.GetChildren();
                        foreach (var dataHandler in dataHandlers)
                        {
                            var handlerName = dataHandler.GetValue<string>("Handler") ?? throw new Exception("没有找到Handler");
                            var handlerParameters = dataHandler.GetSection("Parameters").GetChildren().Select(x => x.Value ?? "").ToList();
                            dataContextHandlers.Add(new DataHandler(handlerName, handlerParameters));
                            _logger?.LogDebug($"call ResolveAsync, 获取DataHandlers, DataHandler: {handlerName}, 参数:{Environment.NewLine}{string.Join(Environment.NewLine, handlerParameters)}");
                        }
                    }
                    else if (parameter.Key == "Parameters")
                    {
                        values = (parameter.Value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                        _logger?.LogDebug($"call ResolveAsync, 获取请求参数 {parameter.Value}");
                    }
                    else
                    {
                        throw new Exception($"无效的Task: {parameter.Key}");
                    }
                }
                await RequestAndBuildDataContextAsync(dataContextDictionary, values, dataContextTmpls, _logger);
                await ExecuteOperationAsync(dataContextDictionary, dataContextHandlers);
            }

            return this;
        }
        /// <summary>
        /// 发送网络请求后构建数据上下文
        /// </summary>
        /// <param name="dataContextDictionary"></param>
        /// <param name="values"></param>
        /// <param name="dataContextTmpls"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        async Task RequestAndBuildDataContextAsync(Dictionary<string, object> dataContextDictionary, List<string> values, List<string> dataContextTmpls, ILogger<RequestProcessorDataTableApi> logger)
        {
            var db = TmplHelper.ResolveByDataContext(dataContextDictionary, values[0]).ToString() ?? throw new ArgumentNullException("db");
            var table = TmplHelper.ResolveByDataContext(dataContextDictionary, values[1]).ToString() ?? throw new ArgumentNullException("table");
            var left = TmplHelper.ResolveByDataContext(dataContextDictionary, values[2]).ToString() ?? throw new ArgumentNullException("left");
            var op = TmplHelper.ResolveByDataContext(dataContextDictionary, values[3]).ToString() ?? throw new ArgumentNullException("op");
            var rightToken = JToken.FromObject(TmplHelper.ResolveByDataContext(dataContextDictionary, values[4]));
            string right;
            if (rightToken.Type == JTokenType.String)
            {
                right = rightToken.ToString();
            }
            else if (rightToken.Type == JTokenType.Array)
            {
                right = string.Join(',', rightToken.ToObject<List<string>>() ?? throw new Exception($"无法转换为字符串集合: {rightToken}"));
            }
            else
            {
                throw new NotImplementedException("未处理的数据库查询条件值类型");
            }
            
            SetDbName(db).SetTable(table);
            if (op == "include")
            {
                WhereFieldInclude(left, right);
            }
            else if (op == "in")
            {
                WhereFieldIn(left, right);
            }
            else if (op == "=")
            {
                WhereFieldEquals(left, right);
            }
            else
            {
                throw new Exception($"未知的参数Parameter3: {op}");
            }
            logger?.LogDebug($"call {nameof(RequestAndBuildDataContextAsync)}, 应用参数, 发送请求");
            var data = await RequestAsync();
            // TODO: 去掉RequerstConfig的Data属性
            _requestConfig.Data = null;

            logger?.LogDebug($"call {nameof(RequestAndBuildDataContextAsync)}, 请求结束, 获取data: {data.Count()}, 构建数据上下文");
            dataContextDictionary.BuildDataContextBySource(data, dataContextTmpls, logger);
        }
        async Task ExecuteOperationAsync(Dictionary<string, object> dataContextDictionary, List<DataHandler> dataHandlers)
        {
            LiftTimeTestContainer.serviceProviders.Add(_serviceProvider);
            foreach (var dataHandler in dataHandlers)
            {
                var handler = dataHandler.Handler;
                var opParameters = dataHandler.Parameters.Select(x => TmplHelper.ResolveByDataContext(dataContextDictionary, x)).ToArray();

                Type opType = ReflectionHelper.GetTypeByClassName(handler);
                MethodInfo operationMethod = opType.GetMethod("Start") ?? throw new Exception($"DataHandler {handler} 没有实现Operation方法");
                var opInstance = _serviceProvider.GetService(opType);
                var invokeResult = operationMethod.Invoke(opInstance, opParameters);
                if (operationMethod.ReturnType == typeof(Task) || (operationMethod.ReturnType.IsGenericType && operationMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)))
                {
                    await (invokeResult as Task ?? throw new Exception($"{handler}.Start()结果为NULL, 而非Task"));
                }
            }
        }
        private string _exceptionMsg = "";

        public RequestProcessorDataTableApi SetDbName(string db)
        {
            _requestConfig.QueryDictionary["Db"] = db;
            _requestConfig.FailMsg = $"数据库 {db} ";
            return this;
        }
        public RequestProcessorDataTableApi SetTable(string table)
        {
            _requestConfig.QueryDictionary["Table"] = table;
            _requestConfig.FailMsg += $"数据表 {table} ";
            return this;
        }
        public RequestProcessorDataTableApi WhereFieldInclude(string fieldname, string fieldvalue)
        {
            _requestConfig.BodyDictionary["FilterItems"] = new List<Dictionary<string, object>> { new Dictionary<string, object> { { "FieldName", fieldname }, { "CompareType", "include" }, { "Value", fieldvalue } } };
            _exceptionMsg = $"{fieldname} include {fieldvalue}";
            return this;
        }
        public RequestProcessorDataTableApi WhereFieldIn(string fieldname, string fieldvalue)
        {
            _requestConfig.BodyDictionary["FilterItems"] = new List<Dictionary<string, object>> { new Dictionary<string, object> { { "FieldName", fieldname }, { "CompareType", "in" }, { "Value", fieldvalue } } };
            _exceptionMsg = $"{fieldname} in {fieldvalue}";
            return this;
        }
        public RequestProcessorDataTableApi WhereFieldEquals(string fieldname, string fieldvalue)
        {
            _requestConfig.BodyDictionary["FilterItems"] = new List<Dictionary<string, object>> { new Dictionary<string, object> { { "FieldName", fieldname }, { "CompareType", "=" }, { "Value", fieldvalue } } };
            _exceptionMsg = $"{fieldname} = {fieldvalue}";
            return this;
        }
        public async Task<IEnumerable<JToken>> RequestAsync()
        {
            return (await RemoteHelpers.FetchAllDataFromApiAsync(_requestConfig)) ?? throw new Exception($"查询记录失败, 条件为 {_exceptionMsg}");
        }
    }
}
