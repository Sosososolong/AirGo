using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.Utils;
using Sylas.RemoteTasks.App.Utils.Template;

namespace Sylas.RemoteTasks.App.RequestProcessor
{
    public class RequestProcessorDataTable : RequestProcessorBase
    {
        public RequestProcessorDataTable(IConfiguration configuration, ILogger<RequestProcessorDataTable> logger, IServiceProvider serviceProvider) : base(configuration, logger, serviceProvider)
        {
            _requestConfig.PageIndexField = "PageIndex";
            _requestConfig.PageIndexParamInQuery = true;
            _requestConfig.QueryDictionary = new Dictionary<string, object>();
            _requestConfig.BodyDictionary = JObject.FromObject(new Dictionary<string, object> { { "FilterItems", new List<Dictionary<string, object>> { new Dictionary<string, object> { { "FieldName", "" }, { "CompareType", "" }, { "Value", "" } } } } });
        }

        /// <summary>
        /// 发送网络请求后构建数据上下文
        /// </summary>
        /// <param name="dataContextDictionary">数据上下文</param>
        /// <param name="values">更新的参数值</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        protected override IEnumerable<RequestConfig> UpdateRequestConfig(Dictionary<string, object> dataContextDictionary, List<string> values)
        {
            var tables = TmplHelper.ResolveVariableValue(dataContextDictionary, values[1]) ?? throw new ArgumentNullException("table");
            var db = TmplHelper.ResolveVariableValue(dataContextDictionary, values[0]).ToString() ?? throw new ArgumentNullException("db");
            List<string> tableList = (tables is JArray tablesJArray)
                ? tablesJArray.ToObject<List<string>>() ?? throw new Exception($"table参数不是字符串也不是字符串集合: {tables}")
                : (tables.ToString() ?? throw new Exception($"table参数不是字符串也不是字符串集合: {tables}")).Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

            var left = string.Empty;
            var op = string.Empty;
            JToken? rightToken = null;
            if (values.Count > 2)
            {
                left = TmplHelper.ResolveVariableValue(dataContextDictionary, values[2]).ToString() ?? throw new ArgumentNullException("left");
                op = TmplHelper.ResolveVariableValue(dataContextDictionary, values[3]).ToString() ?? throw new ArgumentNullException("op");
                rightToken = JToken.FromObject(TmplHelper.ResolveVariableValue(dataContextDictionary, values[4]));
            }

            string right = "";
            if (rightToken is not null && rightToken.Type == JTokenType.String)
            {
                right = rightToken.ToString();
            }
            else if (rightToken is not null && rightToken.Type == JTokenType.Array)
            {
                right = string.Join(',', rightToken.ToObject<List<string>>() ?? throw new Exception($"无法转换为字符串集合: {rightToken}"));
            }


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
            else if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
            {
                WhereFieldClear();
            }
            var requestConfigs = new List<RequestConfig>();
            var tableListDistincted = tableList.Distinct().ToList();
            foreach (var table in tableListDistincted)
            {
                SetDbName(db).SetTable(table);
                requestConfigs.Add(CloneReqeustConfig());
            }
            return requestConfigs;
        }
        protected override IEnumerable<RequestConfig> UpdateRequestConfig2(Dictionary<string, object> dataContextDictionary, string? queryJson, string bodyJson)
        {
            if (!string.IsNullOrWhiteSpace(queryJson))
            {
                var resolvedQueryJson = TmplHelper.ResolveStringWithTmpl(dataContextDictionary, queryJson);
                var queryParams = JsonConvert.DeserializeObject<Dictionary<string, object>>(resolvedQueryJson);
                _requestConfig.QueryDictionary = queryParams;
            }
            if (!string.IsNullOrWhiteSpace(bodyJson) && _requestConfig.RequestMethod.ToLower() == "post")
            {
                var resolvedBodyJson = TmplHelper.ResolveStringWithTmpl(dataContextDictionary, bodyJson);
                var bodyParams = JsonConvert.DeserializeObject<JToken>(resolvedBodyJson);
                _requestConfig.BodyDictionary = bodyParams;
            }
            return new List<RequestConfig>() { CloneReqeustConfig() };
        }

        private RequestProcessorDataTable SetDbName(string db)
        {
            _requestConfig.QueryDictionary["Db"] = db;
            _requestConfig.FailMsg = $"数据库 {db} ";
            return this;
        }
        public RequestProcessorDataTable SetTable(string table)
        {
            _requestConfig.QueryDictionary["Table"] = table;
            _requestConfig.FailMsg += $"数据表 {table} ";
            return this;
        }
        private RequestProcessorDataTable WhereFieldInclude(string fieldname, string fieldvalue)
        {
            _requestConfig.BodyDictionary["FilterItems"] = JObject.FromObject(new List<Dictionary<string, object>> { new Dictionary<string, object> { { "FieldName", fieldname }, { "CompareType", "include" }, { "Value", fieldvalue } } });
            return this;
        }
        private RequestProcessorDataTable WhereFieldIn(string fieldname, string fieldvalue)
        {
            _requestConfig.BodyDictionary["FilterItems"] = JObject.FromObject(new List<Dictionary<string, object>> { new Dictionary<string, object> { { "FieldName", fieldname }, { "CompareType", "in" }, { "Value", fieldvalue } } });
            return this;
        }
        public RequestProcessorDataTable WhereFieldEquals(string fieldname, string fieldvalue)
        {
            _requestConfig.BodyDictionary["FilterItems"] = JObject.FromObject(new List<Dictionary<string, object>> { new Dictionary<string, object> { { "FieldName", fieldname }, { "CompareType", "=" }, { "Value", fieldvalue } } });
            return this;
        }
        public RequestProcessorDataTable WhereFieldClear()
        {
            _requestConfig.BodyDictionary["FilterItems"] = new JObject();
            return this;
        }
    }
}
