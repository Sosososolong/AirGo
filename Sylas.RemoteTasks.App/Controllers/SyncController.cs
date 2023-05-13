using Microsoft.AspNetCore.Mvc;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.Database.SyncBase;
using Sylas.RemoteTasks.App.RequestProcessor;
using Sylas.RemoteTasks.App.Utils;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using static Sylas.RemoteTasks.App.RemoteHostModule.StartupHelper;

namespace Sylas.RemoteTasks.App.Controllers
{
    public partial class SyncController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SyncController> _logger;
        private readonly IConfiguration _configuration;

        public SyncController(IHttpClientFactory httpClientFactory, ILogger<SyncController> logger, IConfiguration configuration)
        {
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
            _configuration = configuration;
        }
        public async Task<IActionResult> Index([FromServices] IServiceProvider serviceProvider)
        {
            var requestProcessorDispatchItems = _configuration.GetSection("RequestPipeline:RequestProcessorDispatch").GetChildren();
            Dictionary<string, object> dataContext = null;
            foreach (var requestProcessorDispatchItem in requestProcessorDispatchItems)
            {
                var requestProcessorName = requestProcessorDispatchItem.GetValue<string>("RequestProcessorName") ?? throw new Exception("[RequestProcessorName] is missing from the configuration"); //$"The provided \"{pipelineItem.Key}\" configuration is invalid"
                 _logger.LogInformation($"{new string('*', 30)} A New RequestProcessor {requestProcessorName} {new string('*', 30)}");
                var requestProcessorUrl = requestProcessorDispatchItem.GetValue<string>("RequestProcessorUrl") ?? throw new Exception("[RequestProcessorUrl] is missing from the configuration");
                var requestProcessorType = ReflectionHelper.GetTypeByClassName(requestProcessorName);
                var requestProcessorInstance = serviceProvider.GetService(requestProcessorType) ?? throw new Exception($"未能获取RequestProcessor实例: {requestProcessorName}");
                if (dataContext is not null)
                {
                    var dataContextProp = requestProcessorType.GetProperty(nameof(RequestProcessorBase.DataContext)) ?? throw new Exception($"获取{requestProcessorName}的{nameof(RequestProcessorBase.DataContext)}属性失败");
                    dataContextProp.SetValue(requestProcessorInstance, dataContext);
                }
                
                var startMethod = requestProcessorType.GetMethod("StartAsync") ?? throw new Exception($"IRequestProcessor的实现{requestProcessorName}中未找到StartAsync方法");
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
            }

            #region 从配置动态地去发起请求, 同步数据
            //var requestPipeline = _configuration.GetSection("RequestPipeline:RequestProcessorDispatch").GetChildren();
            //await _requestTask.StartAsync(piplineItems);
            #endregion

            return View();
        }

        //private static async Task<Tuple<List<JObject>, List<JObject>>> GetAllDataModelsAsync(JToken datamodels, RequestProcessorDataTableApi requestConfig)
        //{
        //    const string devDataModelTable = "devDataModel";
        //    const string devDataModelFieldTable = "devDataModelField";
        //    var dataModels = new List<JObject>();
        //    var dataModelFields = new List<JObject>();

        //    foreach (var datamodel in datamodels)
        //    {
        //        var datamodelId = datamodel?["id"]?.ToString();
        //        if (!string.IsNullOrWhiteSpace(datamodelId) && dataModels.Any() && !dataModels.Any(x => x.Properties().FirstOrDefault(x => string.Equals(x.Name, "id", StringComparison.OrdinalIgnoreCase))?.Value.ToString() == datamodelId))
        //        {
        //            var dm = (await requestConfig.SetTable(devDataModelTable)
        //                .WhereFieldEquals("id", datamodelId)
        //                .RequestAsync())
        //                .FirstOrDefault();

        //            if (dm is JObject dmJObj)
        //            {
        //                await getChildDataModelsAsync(dmJObj, datamodelId);
        //            }
        //        }
        //    }
        //    return Tuple.Create(dataModels, dataModelFields);

        //    async Task getChildDataModelsAsync(JObject dataModel, string datamodelId)
        //    {
        //        //requestConfig.QueryDictionary["Table"] = devDataModelFieldTable;
        //        //requestConfig.FailMsg = $"getChildDataModels 获取DevDataModelField失败";
        //        //requestConfig.BodyDictionary["FilterItems"] = new List<Dictionary<string, object>> { new Dictionary<string, object> { { "FieldName", "ModelId" }, { "CompareType", "=" }, { "Value", datamodelId } } };
        //        //var fields = (await RemoteHelpers.FetchAllDataFromApiAsync(requestConfig))?.Data ?? throw new Exception($"未找到ModelId为{datamodelId}的DataModelField记录");
        //        var fields = await requestConfig.SetTable(devDataModelFieldTable)
        //            .WhereFieldEquals("ModelId", datamodelId)
        //            .RequestAsync();
        //        foreach (var dmItem in fields)
        //        {
        //            var dmItemJObj = dmItem as JObject ?? throw new Exception("获取的DataModelField数据异常");
        //            var dmItemId = dmItemJObj.Properties().FirstOrDefault(x => string.Equals(x.Name, "id", StringComparison.OrdinalIgnoreCase))?.Value?.ToString();
        //            if (!string.IsNullOrWhiteSpace(dmItemId) && !dataModelFields.Any(x => x.Properties().FirstOrDefault(x => string.Equals(x.Name, "id", StringComparison.OrdinalIgnoreCase))?.Value?.ToString() == dmItemId))
        //            {
        //                dataModelFields.Add(dmItemJObj);
        //            }
        //        }
        //        if (dataModels.Any() && !dataModels.Any(x => x.Properties().FirstOrDefault(x => string.Equals(x.Name, "id", StringComparison.OrdinalIgnoreCase))?.Value?.ToString() == datamodelId))
        //        {
        //            dataModels.Add(dataModel);
        //        }
                
        //        foreach (var field in fields)
        //        {
        //            var fieldJObj = field as JObject ?? throw new Exception($"数据模型字段数据异常: {field}");
        //            var fieldRefModelId = fieldJObj.Properties()?.FirstOrDefault(x => string.Equals(x.Name, "RefModelId", StringComparison.OrdinalIgnoreCase))?.Value?.ToString();
        //            if (!string.IsNullOrWhiteSpace(fieldRefModelId))
        //            {
        //                //requestConfig.QueryDictionary["Table"] = devDataModelTable;
        //                //requestConfig.FailMsg = $"getChildDataModelsAsync 获取DevDataModel失败";
        //                //requestConfig.BodyDictionary["FilterItems"] = new List<Dictionary<string, object>> { new Dictionary<string, object> { { "FieldName", "id" }, { "CompareType", "=" }, { "Value", fieldRefModelId } } };
        //                //var refModel = (await RemoteHelpers.FetchAllDataFromApiAsync(requestConfig))?.Data?.FirstOrDefault() ?? throw new Exception($"未找到Id为RefModelId:{fieldRefModelId}的DataModel记录");
        //                var refModel = (await requestConfig.SetTable(devDataModelTable)
        //                    .WhereFieldEquals("id", fieldRefModelId)
        //                    .RequestAsync())
        //                    .FirstOrDefault();
        //                if (refModel is not null && refModel is JObject refModelJObj)
        //                {
        //                    await getChildDataModelsAsync(refModelJObj, fieldRefModelId);
        //                }
        //            }
        //        }
        //    }
        //}
    }
}
