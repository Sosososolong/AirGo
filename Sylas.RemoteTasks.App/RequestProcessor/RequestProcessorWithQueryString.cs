using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.Utils;
using Sylas.RemoteTasks.App.Utils.Template;

namespace Sylas.RemoteTasks.App.RequestProcessor
{
    public class RequestProcessorWithQueryString : RequestProcessorBase
    {
        public RequestProcessorWithQueryString(IConfiguration configuration, ILogger<RequestProcessorWithQueryString> logger, IServiceProvider serviceProvider) : base(configuration, logger, serviceProvider)
        {
            _requestConfig.QueryDictionary = new Dictionary<string, object>();
        }
        protected override IEnumerable<RequestConfig> UpdateRequestConfig(Dictionary<string, object> dataContextDictionary, List<string> values)
        {
            var copies = new List<RequestConfig>();

            var containsIn = values.Where(x => x.Contains(" in ", StringComparison.OrdinalIgnoreCase));
            var others = values.Where(x => !x.Contains(" in ", StringComparison.OrdinalIgnoreCase));
            if (!containsIn.Any() && !others.Any())
            {
                throw new Exception("更新RequestConfig的参数为空");
            }
            // 1. 先将普通参数值更新过来, 即更新普通的QueryString参数
            foreach ( var item in others)
            {
                var splitedParameter = item.Split('=');
                var equalOperatorLeft = splitedParameter[0];
                var equalOperatorRight = splitedParameter[1];
#pragma warning disable 8602 // Disabled because initialization is done in the constructor
                _requestConfig.QueryDictionary[equalOperatorLeft] = equalOperatorRight;
#pragma warning restore 8602
            }

            if (containsIn.Count() > 1)
            {
                throw new Exception($"in参数暂时只支持一个");
            }
            // 2. 处理in参数 基于最新_requestConfig创建多个RequestConfig对象副本添加到result作为最终结果;
            foreach (var inParameter in containsIn)
            {
                //formId=1; formId in $ids
                IEnumerable<string> rightValues = Array.Empty<string>();
                var splited = inParameter.Split("in", StringSplitOptions.TrimEntries);
                if (splited.Length != 2)
                {
                    throw new Exception($"参数格式不正确: {inParameter}");
                }
                var left = splited[0];
                var rightExpression = splited[1];
                var rightValue = TmplHelper.GetTmplValueFromDataContext(dataContextDictionary, rightExpression) ?? throw new Exception($"数据上下文中为解析出表达式: {rightExpression}");
                var rightToken = JToken.FromObject(rightValue);
                if (rightToken.Type == JTokenType.String)
                {
                    rightValues = rightToken.ToString().Split(',');
                }
                else
                {
                    rightValues = rightToken.ToObject<List<string>>() ?? throw new Exception($"{rightExpression}的值转换为字符串集合失败: {rightToken}");
                }
                if (rightValues.Count() > 500)
                {
                    throw new Exception("rightValues超过500个成员, 将发送500个请求");
                }
                foreach (var right in rightValues)
                {
                    //_requestConfig.QueryDictionary ??= new Dictionary<string, object>();
                    //var copied = MapHelper<RequestConfig, RequestConfig>.Map(_requestConfig);

                    //// 处理QueryDictionary属性是同一个引用的问题
                    //copied.QueryDictionary = new Dictionary<string, object>();
                    //foreach (var key in _requestConfig.QueryDictionary.Keys)
                    //{
                    //    copied.QueryDictionary[key] = _requestConfig.QueryDictionary[key];
                    //}
                    var copied = CloneReqeustConfig();
                    copied.QueryDictionary[left] = right;
                    copies.Add(copied);
                }
            }

            return copies.Any() ? copies : new RequestConfig[] { _requestConfig };
        }
    }
}
