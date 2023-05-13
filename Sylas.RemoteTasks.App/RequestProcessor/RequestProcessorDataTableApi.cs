using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.Operations;
using Sylas.RemoteTasks.App.Utils;
using Sylas.RemoteTasks.App.Utils.Template;
using System.Reflection;
using static Sylas.RemoteTasks.App.RemoteHostModule.StartupHelper;

namespace Sylas.RemoteTasks.App.RequestProcessor
{
    public class RequestProcessorDataTableApi : RequestProcessorBase
    {
        public RequestProcessorDataTableApi(IConfiguration configuration, ILogger<RequestProcessorDataTableApi> logger, IServiceProvider serviceProvider) : base(configuration, logger, serviceProvider)
        {
            _requestConfig.PageIndexField = "PageIndex";
            _requestConfig.PageIndexParamInQuery = true;
            _requestConfig.QueryDictionary = new Dictionary<string, object> { { "Db", "" }, { "Table", "" }, { "PageIndex", 1 }, { "PageSize", 5000 }, { "IsAsc", true } };
            _requestConfig.BodyDictionary = new Dictionary<string, object> { { "FilterItems", new List<Dictionary<string, object>> { new Dictionary<string, object> { { "FieldName", "" }, { "CompareType", "" }, { "Value", "" } } } } };
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
            var db = TmplHelper.GetTmplValueFromDataContext(dataContextDictionary, values[0]).ToString() ?? throw new ArgumentNullException("db");
            var table = TmplHelper.GetTmplValueFromDataContext(dataContextDictionary, values[1]).ToString() ?? throw new ArgumentNullException("table");
            var left = TmplHelper.GetTmplValueFromDataContext(dataContextDictionary, values[2]).ToString() ?? throw new ArgumentNullException("left");
            var op = TmplHelper.GetTmplValueFromDataContext(dataContextDictionary, values[3]).ToString() ?? throw new ArgumentNullException("op");
            var rightToken = JToken.FromObject(TmplHelper.GetTmplValueFromDataContext(dataContextDictionary, values[4]));
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
            return new RequestConfig[] { _requestConfig };
        }

        private RequestProcessorDataTableApi SetDbName(string db)
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
        private RequestProcessorDataTableApi WhereFieldInclude(string fieldname, string fieldvalue)
        {
            _requestConfig.BodyDictionary["FilterItems"] = new List<Dictionary<string, object>> { new Dictionary<string, object> { { "FieldName", fieldname }, { "CompareType", "include" }, { "Value", fieldvalue } } };
            return this;
        }
        private RequestProcessorDataTableApi WhereFieldIn(string fieldname, string fieldvalue)
        {
            _requestConfig.BodyDictionary["FilterItems"] = new List<Dictionary<string, object>> { new Dictionary<string, object> { { "FieldName", fieldname }, { "CompareType", "in" }, { "Value", fieldvalue } } };
            return this;
        }
        public RequestProcessorDataTableApi WhereFieldEquals(string fieldname, string fieldvalue)
        {
            _requestConfig.BodyDictionary["FilterItems"] = new List<Dictionary<string, object>> { new Dictionary<string, object> { { "FieldName", fieldname }, { "CompareType", "=" }, { "Value", fieldvalue } } };
            return this;
        }
    }
}
