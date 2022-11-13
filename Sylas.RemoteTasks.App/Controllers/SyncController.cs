using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.Utils;
using System.Text.RegularExpressions;

namespace Sylas.RemoteTasks.App.Controllers
{
    public partial class SyncController : Controller
    {
        private readonly HttpClient _httpClient;

        public SyncController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }
        public IActionResult Index() { return View(); }
        public async Task<IActionResult> SyncTableFromApi(string token, [FromServices]ILogger<SyncController> logger)
        {
            var queryStringDictionary = new Dictionary<string, object>
            {
            };
            var bodyDictionary = new Dictionary<string, object>
            {
                { "pageIndex", 1 },
                { "pageSize", 30 }
            };
            var start = DateTime.Now;
            var data = await RemoteHelpers.ApiDataGetAsync(
                $"http://192.168.1.100:4501/api/NewOchart/GetChildrenDepartments",
                "获取部门信息失败",
                queryStringDictionary,
                "pageIndex", // (queryString, pageIndex) => ReplacePageIndexValRegex().Replace(queryString, m => m.Groups[1].Value + pageIndex.ToString())
                false,
                bodyDictionary,
                response => response["code"]?.ToString() == "1",
                response => response["data"],
                _httpClient,
                "id",
                "parentId",
                false,
                token, string.Empty, logger);
            Console.WriteLine("查询结束一共消耗了" + (DateTime.Now - start).TotalSeconds + "秒");
            return Content(JsonConvert.SerializeObject(data));
        }
    }
}
